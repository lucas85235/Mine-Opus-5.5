using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mine
{
    /// <summary>Plain mesh arrays produced on a worker thread and uploaded on the main thread.</summary>
    public sealed class MeshData
    {
        public readonly List<Vector3> Vertices = new List<Vector3>(16384);
        public readonly List<Vector3> UVs = new List<Vector3>(16384);
        public readonly List<Color32> Colors = new List<Color32>(16384);
        public readonly List<int>[] Indices = { new List<int>(24576), new List<int>(4096), new List<int>(4096) };
        public float MinY;
        public float MaxY;

        public void Clear()
        {
            Vertices.Clear();
            UVs.Clear();
            Colors.Clear();
            for (int i = 0; i < Indices.Length; i++) Indices[i].Clear();
            MinY = float.MaxValue;
            MaxY = float.MinValue;
        }
    }

    /// <summary>
    /// Builds one combined mesh per chunk. Only faces between a block and a non-occluding neighbour are emitted.
    /// Works on a padded copy of the 3x3 chunk neighbourhood, which also lets it flood-fill sky and block light
    /// (Minecraft style, levels 0-15) so faces get smooth lighting and ambient occlusion baked into vertex colours.
    /// One instance per worker thread (not thread-safe itself).
    /// </summary>
    public sealed class ChunkMesher
    {
        const int P = 15;                          // padding: light travels at most 15 blocks
        const int W = Chunk.Size + 2 * P;          // 46
        const int WW = W * W;
        const int H = Chunk.Height + 2;            // y = -1 .. 128
        const int Total = WW * H;

        readonly byte[] vox = new byte[Total];
        readonly byte[] sky = new byte[Total];
        readonly byte[] blk = new byte[Total];
        readonly short[] heights = new short[WW];
        int[] queue = new int[1 << 16];
        int qHead;
        int qTail;

        // Face order: +X, -X, +Y, -Y, +Z, -Z
        static readonly int[] FaceStep = { 1, -1, WW, -WW, W, -W };
        static readonly float[] FaceShade = { 0.6f, 0.6f, 1.0f, 0.5f, 0.8f, 0.8f };
        static readonly float[] AoCurve = { 1f, 0.78f, 0.62f, 0.47f };

        static readonly Vector3Int[] Corners =
        {
            new Vector3Int(1, 0, 0), new Vector3Int(1, 1, 0), new Vector3Int(1, 1, 1), new Vector3Int(1, 0, 1), // +X
            new Vector3Int(0, 0, 1), new Vector3Int(0, 1, 1), new Vector3Int(0, 1, 0), new Vector3Int(0, 0, 0), // -X
            new Vector3Int(0, 1, 0), new Vector3Int(0, 1, 1), new Vector3Int(1, 1, 1), new Vector3Int(1, 1, 0), // +Y
            new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0), new Vector3Int(1, 0, 1), new Vector3Int(0, 0, 1), // -Y
            new Vector3Int(1, 0, 1), new Vector3Int(1, 1, 1), new Vector3Int(0, 1, 1), new Vector3Int(0, 0, 1), // +Z
            new Vector3Int(0, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(1, 1, 0), new Vector3Int(1, 0, 0), // -Z
        };

        static readonly Vector2[] QuadUV = { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
        static readonly Vector2[] BottomUV = { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };

        static readonly int[] Side1 = new int[24];
        static readonly int[] Side2 = new int[24];

        static ChunkMesher()
        {
            int[] axisOfFace = { 0, 0, 1, 1, 2, 2 };
            int[] stride = { 1, WW, W }; // x, y, z
            for (int f = 0; f < 6; f++)
            {
                int a = axisOfFace[f];
                int t1 = (a + 1) % 3, t2 = (a + 2) % 3;
                for (int k = 0; k < 4; k++)
                {
                    Vector3Int c = Corners[f * 4 + k];
                    Side1[f * 4 + k] = (c[t1] == 1 ? 1 : -1) * stride[t1];
                    Side2[f * 4 + k] = (c[t2] == 1 ? 1 : -1) * stride[t2];
                }
            }
        }

        /// <summary>neighbours: 3x3 chunks indexed [(dz + 1) * 3 + (dx + 1)], centre at 4. All must be generated.</summary>
        public void Build(Chunk[] neighbours, MeshData md)
        {
            Chunk c = neighbours[4];
            CopyVoxels(neighbours);
            ComputeSkyLight();
            ComputeBlockLight(neighbours);
            md.Clear();
            BuildMesh(c, md);
            WriteChunkLight(c);
        }

        void CopyVoxels(Chunk[] n)
        {
            for (int pz = 0; pz < W; pz++)
            {
                int lz = pz - P;
                int cz = lz < 0 ? 0 : (lz >= Chunk.Size ? 2 : 1);
                int iz = lz - (cz - 1) * Chunk.Size;
                for (int px = 0; px < W; px++)
                {
                    int lx = px - P;
                    int cx = lx < 0 ? 0 : (lx >= Chunk.Size ? 2 : 1);
                    int ix = lx - (cx - 1) * Chunk.Size;
                    byte[] src = n[cz * 3 + cx].Voxels;
                    int col = px + pz * W;
                    vox[col] = (byte)BlockId.Bedrock;
                    int si = ix | (iz << 4);
                    int di = col + WW;
                    for (int y = 0; y < Chunk.Height; y++)
                    {
                        vox[di] = src[si];
                        si += 256;
                        di += WW;
                    }
                    vox[col + (H - 1) * WW] = 0;
                }
            }
        }

        void Push(int i)
        {
            if (qTail == queue.Length)
            {
                if (qHead > queue.Length / 2)
                {
                    Array.Copy(queue, qHead, queue, 0, qTail - qHead);
                    qTail -= qHead;
                    qHead = 0;
                }
                else
                {
                    Array.Resize(ref queue, queue.Length * 2);
                }
            }
            queue[qTail++] = i;
        }

        void ComputeSkyLight()
        {
            bool[] stops = Blocks.StopsSky;
            qHead = qTail = 0;

            for (int col = 0; col < WW; col++)
            {
                int h = -1;
                int idx = col + Chunk.Height * WW; // y = 127
                for (int y = Chunk.Height - 1; y >= 0; y--, idx -= WW)
                {
                    if (stops[vox[idx]])
                    {
                        h = y;
                        break;
                    }
                }
                heights[col] = (short)h;
                sky[col] = 0;
                int di = col + WW;
                for (int y = 0; y < Chunk.Height; y++, di += WW) sky[di] = (byte)(y > h ? 15 : 0);
                sky[col + (H - 1) * WW] = 15;
            }

            for (int pz = 0; pz < W; pz++)
            for (int px = 0; px < W; px++)
            {
                int col = px + pz * W;
                int h = heights[col];
                int maxN = h;
                if (px > 0 && heights[col - 1] > maxN) maxN = heights[col - 1];
                if (px < W - 1 && heights[col + 1] > maxN) maxN = heights[col + 1];
                if (pz > 0 && heights[col - W] > maxN) maxN = heights[col - W];
                if (pz < W - 1 && heights[col + W] > maxN) maxN = heights[col + W];
                int top = Math.Min(Chunk.Height - 1, Math.Max(h + 1, maxN));
                for (int y = h + 1; y <= top; y++) Push(col + (y + 1) * WW);
            }

            Propagate(sky);
        }

        void ComputeBlockLight(Chunk[] n)
        {
            Array.Clear(blk, 0, Total);
            bool any = false;
            for (int i = 0; i < 9; i++)
            {
                if (n[i].EmitterCount > 0)
                {
                    any = true;
                    break;
                }
            }
            if (!any) return;

            qHead = qTail = 0;
            byte[] emission = Blocks.Emission;
            for (int i = 0; i < Total; i++)
            {
                byte e = emission[vox[i]];
                if (e > 0)
                {
                    blk[i] = e;
                    Push(i);
                }
            }
            Propagate(blk);
        }

        void Propagate(byte[] light)
        {
            bool[] opaque = Blocks.Opaque;
            while (qHead < qTail)
            {
                int i = queue[qHead++];
                int l = light[i] - 1;
                if (l <= 0) continue;
                int px = i % W;
                int t = i / W;
                int pz = t % W;
                int py = t / W;
                if (px > 0) Spread(i - 1, l, light, opaque);
                if (px < W - 1) Spread(i + 1, l, light, opaque);
                if (pz > 0) Spread(i - W, l, light, opaque);
                if (pz < W - 1) Spread(i + W, l, light, opaque);
                if (py > 0) Spread(i - WW, l, light, opaque);
                if (py < H - 1) Spread(i + WW, l, light, opaque);
            }
        }

        void Spread(int j, int l, byte[] light, bool[] opaque)
        {
            if (light[j] >= l || opaque[vox[j]]) return;
            light[j] = (byte)l;
            Push(j);
        }

        void BuildMesh(Chunk c, MeshData md)
        {
            bool[] opaque = Blocks.Opaque;
            byte[] shape = Blocks.Shape;
            int maxY = Math.Min(c.MaxY, Chunk.Height - 1);

            for (int y = 0; y <= maxY; y++)
            for (int z = 0; z < Chunk.Size; z++)
            {
                int row = P + (z + P) * W + (y + 1) * WW;
                for (int x = 0; x < Chunk.Size; x++)
                {
                    int i = row + x;
                    byte b = vox[i];
                    if (b == 0) continue;
                    byte s = shape[b];
                    if (s == (byte)BlockShape.Cross)
                    {
                        AddCross(md, x, y, z, i, b);
                        continue;
                    }
                    bool liquid = s == (byte)BlockShape.Liquid;
                    for (int f = 0; f < 6; f++)
                    {
                        byte nb = vox[i + FaceStep[f]];
                        if (nb == b || opaque[nb]) continue;
                        if (liquid) AddLiquidFace(md, x, y, z, i, f, b);
                        else AddFace(md, x, y, z, i, f, b);
                    }
                }
            }
        }

        void TrackY(MeshData md, float y0, float y1)
        {
            if (y0 < md.MinY) md.MinY = y0;
            if (y1 > md.MaxY) md.MaxY = y1;
        }

        void AddFace(MeshData md, int x, int y, int z, int i, int f, byte b)
        {
            bool[] ao = Blocks.CastsAO;
            int j = i + FaceStep[f];
            ushort tex = f == 2 ? Blocks.TexTop[b] : (f == 3 ? Blocks.TexBottom[b] : Blocks.TexSide[b]);
            float shade = FaceShade[f];
            int baseIndex = md.Vertices.Count;
            Vector2[] uvs = f == 3 ? BottomUV : QuadUV;

            float b0 = 0, b1 = 0, b2 = 0, b3 = 0;
            for (int k = 0; k < 4; k++)
            {
                int ci = f * 4 + k;
                int s1 = j + Side1[ci];
                int s2 = j + Side2[ci];
                int cc = s1 + Side2[ci];
                bool o1 = ao[vox[s1]];
                bool o2 = ao[vox[s2]];
                bool oc = ao[vox[cc]];
                int occ = (o1 && o2) ? 3 : (o1 ? 1 : 0) + (o2 ? 1 : 0) + (oc ? 1 : 0);

                int sSum = sky[j], bSum = blk[j], n = 1;
                if (!o1) { sSum += sky[s1]; bSum += blk[s1]; n++; }
                if (!o2) { sSum += sky[s2]; bSum += blk[s2]; n++; }
                if (!oc && !(o1 && o2)) { sSum += sky[cc]; bSum += blk[cc]; n++; }
                float skyL = sSum / (float)n;
                float blkL = bSum / (float)n;
                float aoV = AoCurve[occ] * shade;

                Vector3Int corner = Corners[ci];
                md.Vertices.Add(new Vector3(x + corner.x, y + corner.y, z + corner.z));
                Vector2 uv = uvs[k];
                md.UVs.Add(new Vector3(uv.x, uv.y, tex));
                md.Colors.Add(new Color32((byte)(skyL * 17f), (byte)(aoV * 255f), (byte)(blkL * 17f), 255));

                float brightness = aoV * (1f + Math.Max(skyL, blkL));
                switch (k)
                {
                    case 0: b0 = brightness; break;
                    case 1: b1 = brightness; break;
                    case 2: b2 = brightness; break;
                    default: b3 = brightness; break;
                }
            }

            List<int> ind = md.Indices[Blocks.Layer[b]];
            if (b0 + b2 >= b1 + b3)
            {
                ind.Add(baseIndex); ind.Add(baseIndex + 1); ind.Add(baseIndex + 2);
                ind.Add(baseIndex); ind.Add(baseIndex + 2); ind.Add(baseIndex + 3);
            }
            else
            {
                ind.Add(baseIndex + 1); ind.Add(baseIndex + 2); ind.Add(baseIndex + 3);
                ind.Add(baseIndex + 1); ind.Add(baseIndex + 3); ind.Add(baseIndex);
            }
            TrackY(md, y, y + 1);
        }

        void AddLiquidFace(MeshData md, int x, int y, int z, int i, int f, byte b)
        {
            int j = i + FaceStep[f];
            bool liquidAbove = vox[i + WW] == b;
            float top = liquidAbove ? 1f : 0.875f;
            float shade = f == 2 ? 1f : FaceShade[f];
            byte skyL = sky[j], blkL = blk[j];
            if (f == 3 || f == 2)
            {
                // Use the brighter of the two cells so the surface is not dark from below.
                if (sky[i] > skyL) skyL = sky[i];
            }
            ushort tex = Blocks.TexSide[b];
            Vector2[] uvs = f == 3 ? BottomUV : QuadUV;
            var color = new Color32((byte)(skyL * 17), (byte)(shade * 255f), (byte)(blkL * 17), 255);
            int baseIndex = md.Vertices.Count;
            for (int k = 0; k < 4; k++)
            {
                Vector3Int corner = Corners[f * 4 + k];
                float cy = corner.y == 1 ? top : 0f;
                md.Vertices.Add(new Vector3(x + corner.x, y + cy, z + corner.z));
                Vector2 uv = uvs[k];
                md.UVs.Add(new Vector3(uv.x, uv.y * (corner.y == 1 ? top : 1f), tex));
                md.Colors.Add(color);
            }
            List<int> ind = md.Indices[(int)RenderLayer.Water];
            ind.Add(baseIndex); ind.Add(baseIndex + 1); ind.Add(baseIndex + 2);
            ind.Add(baseIndex); ind.Add(baseIndex + 2); ind.Add(baseIndex + 3);
            TrackY(md, y, y + top);
        }

        void AddCross(MeshData md, int x, int y, int z, int i, byte b)
        {
            var color = new Color32((byte)(sky[i] * 17), (byte)(0.92f * 255f), (byte)(blk[i] * 17), 255);
            ushort tex = Blocks.TexSide[b];
            const float a = 0.15f, c = 0.85f;
            AddCrossQuad(md, new Vector3(x + a, y, z + a), new Vector3(x + c, y, z + c), tex, color);
            AddCrossQuad(md, new Vector3(x + a, y, z + c), new Vector3(x + c, y, z + a), tex, color);
            TrackY(md, y, y + 1);
        }

        static void AddCrossQuad(MeshData md, Vector3 p0, Vector3 p1, ushort tex, Color32 color)
        {
            int bi = md.Vertices.Count;
            Vector3 up = Vector3.up;
            md.Vertices.Add(p0);
            md.Vertices.Add(p0 + up);
            md.Vertices.Add(p1 + up);
            md.Vertices.Add(p1);
            md.UVs.Add(new Vector3(0, 0, tex));
            md.UVs.Add(new Vector3(0, 1, tex));
            md.UVs.Add(new Vector3(1, 1, tex));
            md.UVs.Add(new Vector3(1, 0, tex));
            for (int k = 0; k < 4; k++) md.Colors.Add(color);
            List<int> ind = md.Indices[(int)RenderLayer.Cutout];
            // front
            ind.Add(bi); ind.Add(bi + 1); ind.Add(bi + 2);
            ind.Add(bi); ind.Add(bi + 2); ind.Add(bi + 3);
            // back
            ind.Add(bi); ind.Add(bi + 2); ind.Add(bi + 1);
            ind.Add(bi); ind.Add(bi + 3); ind.Add(bi + 2);
        }

        void WriteChunkLight(Chunk c)
        {
            byte[] l = c.Light ?? new byte[Chunk.Volume];
            for (int y = 0; y < Chunk.Height; y++)
            for (int z = 0; z < Chunk.Size; z++)
            {
                int src = P + (z + P) * W + (y + 1) * WW;
                int dst = (z << 4) | (y << 8);
                for (int x = 0; x < Chunk.Size; x++)
                {
                    l[dst + x] = (byte)((sky[src + x] << 4) | blk[src + x]);
                }
            }
            c.Light = l;
        }
    }
}
