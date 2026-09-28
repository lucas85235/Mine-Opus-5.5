using System;
using UnityEngine;

namespace Mine
{
    public enum Biome : byte { Ocean, Beach, Plains, Forest, BirchForest, Desert, Mountains, SnowyPeaks }

    /// <summary>
    /// Deterministic procedural terrain. Every method is a pure function of (seed, position), so chunks can be
    /// generated in any order on any thread and trees crossing chunk borders come out identical on both sides.
    /// </summary>
    public sealed class TerrainGenerator
    {
        public const int SeaLevel = 48;
        public const int MaxTerrain = 116;

        public readonly int Seed;

        readonly Perlin continental;
        readonly Perlin hills;
        readonly Perlin detail;
        readonly Perlin mountainMask;
        readonly Perlin ridge;
        readonly Perlin river;
        readonly Perlin temperature;
        readonly Perlin humidity;
        readonly Perlin patches;
        readonly Perlin cave1;
        readonly Perlin cave2;
        readonly Perlin cave3;

        public struct Column
        {
            public int Height;
            public BlockId Top;
            public BlockId Filler;
            public int FillerDepth;
            public bool SandstoneBelow;
            public Biome Biome;
        }

        enum TreeKind { Oak, Birch, Spruce }

        public TerrainGenerator(int seed)
        {
            Seed = seed;
            continental = new Perlin(seed ^ 0x1B873593);
            hills = new Perlin(seed ^ 0x2C1B3C6D);
            detail = new Perlin(seed ^ 0x297A2D39);
            mountainMask = new Perlin(seed ^ 0x3C6EF372);
            ridge = new Perlin(seed ^ 0x5A827999);
            river = new Perlin(seed ^ 0x6ED9EBA1);
            temperature = new Perlin(seed ^ 0x7F4A7C15);
            humidity = new Perlin(seed ^ 0x0F1BBCDC);
            patches = new Perlin(seed ^ 0x4B0F3B58);
            cave1 = new Perlin(seed ^ 0x61C88647);
            cave2 = new Perlin(seed ^ 0x3243F6A8);
            cave3 = new Perlin(seed ^ 0x13198A2E);
        }

        // ------------------------------------------------------------------ height & biomes

        public float HeightF(int x, int z)
        {
            float c = continental.Fbm(x * 0.0016f, z * 0.0016f, 4);
            float hl = hills.Fbm(x * 0.0085f, z * 0.0085f, 4);
            float d = detail.Fbm(x * 0.04f, z * 0.04f, 2);

            float mm = mountainMask.Fbm(x * 0.0021f + 31.4f, z * 0.0021f - 17.2f, 3);
            float mask = MathUtil.SmoothStep(0.02f, 0.36f, mm);
            float rg = 1f - Math.Abs(ridge.Fbm(x * 0.0048f, z * 0.0048f, 4));
            rg = rg * rg * rg;

            float h = SeaLevel + 5f + c * 20f + hl * 9f + d * 1.6f;
            h += (rg * 70f + 6f) * mask;

            // Rivers carve valleys (and canyons through mountains).
            float rv = Math.Abs(river.Fbm(x * 0.0019f + 100f, z * 0.0019f - 100f, 3));
            // Wider valleys where the river crosses mountains, so the walls are slopes rather than sheer cliffs.
            float rf = MathUtil.SmoothStep(0.012f, 0.075f + 0.09f * mask, rv);
            float bed = SeaLevel - 3f + d;
            if (h > bed) h = bed + (h - bed) * rf;

            if (h < 4f) h = 4f;
            if (h > MaxTerrain) h = MaxTerrain;
            return h;
        }

        public int Height(int x, int z)
        {
            return MathUtil.FastFloor(HeightF(x, z));
        }

        public int Slope(int x, int z)
        {
            return Math.Max(Math.Abs(Height(x + 1, z) - Height(x - 1, z)), Math.Abs(Height(x, z + 1) - Height(x, z - 1)));
        }

        public Column GetColumn(int x, int z, int h, int slope)
        {
            float temp = temperature.Fbm(x * 0.0011f + 50f, z * 0.0011f + 50f, 3);
            float hum = humidity.Fbm(x * 0.0013f - 70f, z * 0.0013f + 20f, 3);
            float pn = patches.Noise(x * 0.07f, z * 0.07f);
            uint hs = MathUtil.Hash(x, 0, z, Seed);

            var col = new Column
            {
                Height = h,
                FillerDepth = 3 + (int)(hs & 1),
                Top = BlockId.Grass,
                Filler = BlockId.Dirt,
                Biome = Biome.Plains
            };

            float snowLine = SeaLevel + 44 + pn * 5f;

            if (h < SeaLevel - 1)
            {
                col.Biome = Biome.Ocean;
                if (h < SeaLevel - 6 && pn < -0.15f)
                {
                    col.Top = BlockId.Gravel;
                    col.Filler = BlockId.Gravel;
                }
                else if (pn > 0.35f && h < SeaLevel - 3)
                {
                    col.Top = BlockId.Dirt;
                    col.Filler = BlockId.Dirt;
                }
                else
                {
                    col.Top = BlockId.Sand;
                    col.Filler = BlockId.Sand;
                }
            }
            else if (temp > 0.26f && h < SeaLevel + 26)
            {
                col.Biome = Biome.Desert;
                col.Top = BlockId.Sand;
                col.Filler = BlockId.Sand;
                col.FillerDepth = 4;
                col.SandstoneBelow = true;
            }
            else if (h <= SeaLevel + 1)
            {
                col.Biome = Biome.Beach;
                col.Top = BlockId.Sand;
                col.Filler = BlockId.Sand;
            }
            else if (h >= snowLine)
            {
                col.Biome = Biome.SnowyPeaks;
                col.Top = slope >= 4 ? BlockId.Stone : BlockId.Snow;
                col.Filler = BlockId.Stone;
            }
            else if (slope >= 3 && h > SeaLevel + 16)
            {
                col.Biome = Biome.Mountains;
                col.Top = (pn > 0.45f && slope < 5) ? BlockId.Gravel : BlockId.Stone;
                col.Filler = BlockId.Stone;
            }
            else
            {
                if (h > SeaLevel + 28) col.Biome = Biome.Mountains;
                else if (hum > 0.28f) col.Biome = Biome.BirchForest;
                else if (hum > 0.06f) col.Biome = Biome.Forest;
                else col.Biome = Biome.Plains;
            }
            return col;
        }

        public Biome BiomeAt(int x, int z)
        {
            int h = Height(x, z);
            return GetColumn(x, z, h, Slope(x, z)).Biome;
        }

        static float TreeChance(Column c)
        {
            switch (c.Biome)
            {
                case Biome.Plains: return 0.03f;
                case Biome.Forest: return 0.42f;
                case Biome.BirchForest: return 0.42f;
                case Biome.Mountains: return c.Top == BlockId.Grass ? 0.14f : 0f;
                case Biome.SnowyPeaks: return c.Top == BlockId.Snow ? 0.07f : 0f;
                default: return 0f;
            }
        }

        static float GrassChance(Biome b)
        {
            switch (b)
            {
                case Biome.Plains: return 0.30f;
                case Biome.Forest: return 0.14f;
                case Biome.BirchForest: return 0.16f;
                case Biome.Mountains: return 0.06f;
                default: return 0f;
            }
        }

        // ------------------------------------------------------------------ caves

        void CaveNoise(int x, int y, int z, out float a, out float b, out float c)
        {
            a = cave1.Noise(x * 0.016f, y * 0.028f, z * 0.016f);
            b = cave2.Noise(x * 0.016f + 37.1f, y * 0.028f - 11.3f, z * 0.016f + 5.7f);
            c = cave3.Noise(x * 0.022f, y * 0.04f, z * 0.022f);
        }

        static bool IsCave(float a, float b, float c, int y)
        {
            // "Spaghetti" tunnels: where two independent noise fields are both close to zero.
            float t = a * a + b * b;
            float width = y < 24 ? 0.0095f : 0.0070f;
            if (t < width) return true;
            // Larger caverns deep down.
            if (y < 40 && c > 0.56f - (40 - y) * 0.004f) return true;
            return false;
        }

        static bool CaveAllowed(int y, int h)
        {
            if (y < 5 || y > h) return false;
            // Keep lake and beach floors sealed so water never pours into caves.
            if (h < SeaLevel + 3 && y > h - 7) return false;
            return true;
        }

        static float Trilerp(float c000, float c100, float c010, float c110, float c001, float c101, float c011, float c111,
            float fx, float fy, float fz)
        {
            float x00 = c000 + (c100 - c000) * fx;
            float x10 = c010 + (c110 - c010) * fx;
            float x01 = c001 + (c101 - c001) * fx;
            float x11 = c011 + (c111 - c011) * fx;
            float y0 = x00 + (x10 - x00) * fy;
            float y1 = x01 + (x11 - x01) * fy;
            return y0 + (y1 - y0) * fz;
        }

        const int LatX = 5;              // 16 / 4 + 1
        const int LatY = Chunk.Height / 4 + 1;
        const int LatStrideY = LatX * LatX;

        static float Tri(float[] g, int x, int y, int z)
        {
            int ix = x >> 2, iy = y >> 2, iz = z >> 2;
            float fx = (x & 3) * 0.25f, fy = (y & 3) * 0.25f, fz = (z & 3) * 0.25f;
            int i = ix + iz * LatX + iy * LatStrideY;
            return Trilerp(g[i], g[i + 1], g[i + LatStrideY], g[i + LatStrideY + 1],
                g[i + LatX], g[i + LatX + 1], g[i + LatStrideY + LatX], g[i + LatStrideY + LatX + 1], fx, fy, fz);
        }

        /// <summary>Same result as the per-chunk interpolated lattice, but for a single world position.</summary>
        public bool CaveAt(int wx, int wy, int wz, int h)
        {
            if (!CaveAllowed(wy, h)) return false;
            int x0 = (wx >> 2) << 2, y0 = (wy >> 2) << 2, z0 = (wz >> 2) << 2;
            float fx = (wx - x0) * 0.25f, fy = (wy - y0) * 0.25f, fz = (wz - z0) * 0.25f;
            var a = new float[8];
            var b = new float[8];
            var c = new float[8];
            for (int k = 0; k < 8; k++)
            {
                int dx = k & 1, dy = (k >> 1) & 1, dz = (k >> 2) & 1;
                CaveNoise(x0 + dx * 4, y0 + dy * 4, z0 + dz * 4, out a[k], out b[k], out c[k]);
            }
            // k bit layout: x = 1, y = 2, z = 4
            float na = Trilerp(a[0], a[1], a[2], a[3], a[4], a[5], a[6], a[7], fx, fy, fz);
            float nb = Trilerp(b[0], b[1], b[2], b[3], b[4], b[5], b[6], b[7], fx, fy, fz);
            float nc = Trilerp(c[0], c[1], c[2], c[3], c[4], c[5], c[6], c[7], fx, fy, fz);
            return IsCave(na, nb, nc, wy);
        }

        // ------------------------------------------------------------------ chunk generation

        public void Generate(Chunk chunk)
        {
            byte[] v = chunk.Voxels;
            int bx = chunk.X * Chunk.Size;
            int bz = chunk.Z * Chunk.Size;

            var hm = new int[18 * 18];
            for (int j = 0; j < 18; j++)
            for (int i = 0; i < 18; i++)
                hm[i + j * 18] = Height(bx + i - 1, bz + j - 1);

            var n1 = new float[LatX * LatY * LatX];
            var n2 = new float[n1.Length];
            var n3 = new float[n1.Length];
            for (int ly = 0; ly < LatY; ly++)
            for (int lz = 0; lz < LatX; lz++)
            for (int lx = 0; lx < LatX; lx++)
            {
                int idx = lx + lz * LatX + ly * LatStrideY;
                CaveNoise(bx + lx * 4, ly * 4, bz + lz * 4, out n1[idx], out n2[idx], out n3[idx]);
            }

            var columns = new Column[256];
            int maxY = 0;

            for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++)
            {
                int h = hm[(x + 1) + (z + 1) * 18];
                int slope = Math.Max(
                    Math.Abs(hm[(x + 2) + (z + 1) * 18] - hm[x + (z + 1) * 18]),
                    Math.Abs(hm[(x + 1) + (z + 2) * 18] - hm[(x + 1) + z * 18]));
                int wx = bx + x, wz = bz + z;
                Column col = GetColumn(wx, wz, h, slope);
                columns[x + z * 16] = col;

                int top = Math.Max(h, SeaLevel);
                for (int y = 0; y <= top; y++)
                {
                    BlockId id;
                    if (y == 0) id = BlockId.Bedrock;
                    else if (y < 4 && (MathUtil.Hash(wx, y, wz, Seed + 7) & 255) < (4 - y) * 64) id = BlockId.Bedrock;
                    else if (y > h) id = BlockId.Water;
                    else if (y == h) id = col.Top;
                    else if (y > h - col.FillerDepth) id = col.Filler;
                    else if (col.SandstoneBelow && y > h - col.FillerDepth - 4) id = BlockId.Sandstone;
                    else id = BlockId.Stone;

                    if (id != BlockId.Bedrock && id != BlockId.Water && CaveAllowed(y, h))
                    {
                        if (IsCave(Tri(n1, x, y, z), Tri(n2, x, y, z), Tri(n3, x, y, z), y)) id = BlockId.Air;
                    }
                    v[Chunk.Index(x, y, z)] = (byte)id;
                }
                if (top > maxY) maxY = top;
            }

            // Ground cover: tall grass and flowers.
            for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++)
            {
                Column col = columns[x + z * 16];
                int h = col.Height;
                if (col.Top != BlockId.Grass || h + 1 >= Chunk.Height) continue;
                if (v[Chunk.Index(x, h, z)] != (byte)BlockId.Grass || v[Chunk.Index(x, h + 1, z)] != 0) continue;
                int wx = bx + x, wz = bz + z;
                float r = MathUtil.Hash01(wx, h, wz, Seed + 99);
                BlockId plant = BlockId.Air;
                if (r < 0.012f) plant = (MathUtil.Hash(wx, 1, wz, Seed + 5) & 1) == 0 ? BlockId.FlowerRed : BlockId.FlowerYellow;
                else if (r < GrassChance(col.Biome)) plant = BlockId.TallGrass;
                if (plant != BlockId.Air)
                {
                    v[Chunk.Index(x, h + 1, z)] = (byte)plant;
                    if (h + 1 > maxY) maxY = h + 1;
                }
            }

            PlaceOres(chunk, v);
            PlaceTrees(v, bx, bz, ref maxY);

            chunk.MaxY = Math.Min(maxY, Chunk.Height - 1);
            chunk.EmitterCount = 0;
        }

        void PlaceOres(Chunk chunk, byte[] v)
        {
            var rng = new System.Random((int)MathUtil.Hash(chunk.X, 0, chunk.Z, Seed + 555));
            Vein(v, rng, BlockId.CoalOre, 16, 5, 100, 8);
            Vein(v, rng, BlockId.IronOre, 9, 5, 56, 6);
        }

        static void Vein(byte[] v, System.Random rng, BlockId ore, int count, int minY, int maxY, int size)
        {
            for (int i = 0; i < count; i++)
            {
                float x = rng.Next(16), y = rng.Next(minY, maxY), z = rng.Next(16);
                int n = size / 2 + rng.Next(size);
                for (int s = 0; s < n; s++)
                {
                    int ix = (int)x, iy = (int)y, iz = (int)z;
                    if (ix >= 0 && ix < 16 && iz >= 0 && iz < 16 && iy > 0 && iy < Chunk.Height)
                    {
                        int idx = Chunk.Index(ix, iy, iz);
                        if (v[idx] == (byte)BlockId.Stone) v[idx] = (byte)ore;
                    }
                    x += rng.Next(3) - 1;
                    y += (rng.Next(3) - 1) * 0.5f;
                    z += rng.Next(3) - 1;
                }
            }
        }

        // ------------------------------------------------------------------ trees

        void PlaceTrees(byte[] v, int bx, int bz, ref int maxY)
        {
            const int Cell = 5;
            const int Margin = 3;
            int gx0 = MathUtil.FloorDiv(bx - Margin, Cell), gx1 = MathUtil.FloorDiv(bx + 15 + Margin, Cell);
            int gz0 = MathUtil.FloorDiv(bz - Margin, Cell), gz1 = MathUtil.FloorDiv(bz + 15 + Margin, Cell);

            for (int gz = gz0; gz <= gz1; gz++)
            for (int gx = gx0; gx <= gx1; gx++)
            {
                uint hs = MathUtil.Hash(gx, 7, gz, Seed + 1013);
                int tx = gx * Cell + (int)(hs % Cell);
                int tz = gz * Cell + (int)((hs >> 8) % Cell);
                if (tx < bx - Margin || tx > bx + 15 + Margin || tz < bz - Margin || tz > bz + 15 + Margin) continue;

                int h = Height(tx, tz);
                if (h <= SeaLevel || h > MaxTerrain - 3) continue;
                Column col = GetColumn(tx, tz, h, Slope(tx, tz));
                float roll = ((hs >> 16) & 0xFFFF) / 65536f;
                if (roll >= TreeChance(col)) continue;
                if (col.Top != BlockId.Grass && col.Top != BlockId.Snow) continue;
                if (CaveAt(tx, h, tz, h) || CaveAt(tx, h - 1, tz, h)) continue;

                uint hs2 = MathUtil.Hash(tx, h, tz, Seed + 2027);
                float kindRoll = (hs2 & 0xFF) / 256f;
                TreeKind kind;
                switch (col.Biome)
                {
                    case Biome.Mountains:
                    case Biome.SnowyPeaks:
                        kind = TreeKind.Spruce;
                        break;
                    case Biome.BirchForest:
                        kind = kindRoll < 0.8f ? TreeKind.Birch : TreeKind.Oak;
                        break;
                    case Biome.Forest:
                        kind = kindRoll < 0.2f ? TreeKind.Birch : TreeKind.Oak;
                        break;
                    default:
                        kind = kindRoll < 0.1f ? TreeKind.Birch : TreeKind.Oak;
                        break;
                }

                int extra = (int)((hs2 >> 8) % 3);
                switch (kind)
                {
                    case TreeKind.Oak:
                        GrowRound(v, bx, bz, tx, h, tz, 4 + extra, BlockId.OakLog, BlockId.OakLeaves, ref maxY);
                        break;
                    case TreeKind.Birch:
                        GrowRound(v, bx, bz, tx, h, tz, 5 + extra, BlockId.BirchLog, BlockId.BirchLeaves, ref maxY);
                        break;
                    default:
                        GrowSpruce(v, bx, bz, tx, h, tz, 6 + extra + (int)((hs2 >> 12) % 2), ref maxY);
                        break;
                }
            }
        }

        void GrowRound(byte[] v, int bx, int bz, int x, int h, int z, int trunk, BlockId log, BlockId leaves, ref int maxY)
        {
            int top = h + trunk;
            for (int y = top - 2; y <= top + 1; y++)
            {
                int r = y <= top - 1 ? 2 : 1;
                for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    bool corner = Math.Abs(dx) == r && Math.Abs(dz) == r;
                    if (corner)
                    {
                        if (y == top + 1) continue;
                        if ((MathUtil.Hash(x + dx, y, z + dz, Seed + 31) & 1) == 0) continue;
                    }
                    SetLeaf(v, bx, bz, x + dx, y, z + dz, leaves, ref maxY);
                }
            }
            for (int y = h + 1; y <= top; y++) SetLog(v, bx, bz, x, y, z, log, ref maxY);
            ReplaceIfInside(v, bx, bz, x, h, z, BlockId.Grass, BlockId.Dirt);
        }

        void GrowSpruce(byte[] v, int bx, int bz, int x, int h, int z, int trunk, ref int maxY)
        {
            int top = h + trunk;
            SetLeaf(v, bx, bz, x, top + 1, z, BlockId.SpruceLeaves, ref maxY);
            for (int y = top; y >= h + 2; y--)
            {
                int d = top - y;
                int r;
                if (d == 0) r = 1;
                else if (d % 2 == 1) r = Math.Min(1 + d / 2, 3);
                else r = Math.Max(1, Math.Min(1 + d / 2, 3) - 1);
                int limit = d == 0 ? 1 : r * r + 1;
                for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx * dx + dz * dz > limit) continue;
                    SetLeaf(v, bx, bz, x + dx, y, z + dz, BlockId.SpruceLeaves, ref maxY);
                }
            }
            for (int y = h + 1; y <= top; y++) SetLog(v, bx, bz, x, y, z, BlockId.SpruceLog, ref maxY);
            ReplaceIfInside(v, bx, bz, x, h, z, BlockId.Grass, BlockId.Dirt);
        }

        static bool Inside(int bx, int bz, int x, int y, int z)
        {
            return x >= bx && x < bx + 16 && z >= bz && z < bz + 16 && y >= 0 && y < Chunk.Height;
        }

        static void SetLeaf(byte[] v, int bx, int bz, int x, int y, int z, BlockId leaves, ref int maxY)
        {
            if (!Inside(bx, bz, x, y, z)) return;
            int idx = Chunk.Index(x - bx, y, z - bz);
            byte cur = v[idx];
            if (cur != 0 && Blocks.Shape[cur] != (byte)BlockShape.Cross) return;
            v[idx] = (byte)leaves;
            if (y > maxY) maxY = y;
        }

        static void SetLog(byte[] v, int bx, int bz, int x, int y, int z, BlockId log, ref int maxY)
        {
            if (!Inside(bx, bz, x, y, z)) return;
            int idx = Chunk.Index(x - bx, y, z - bz);
            byte cur = v[idx];
            if (cur != 0 && Blocks.Shape[cur] != (byte)BlockShape.Cross && Blocks.Layer[cur] != (byte)RenderLayer.Cutout) return;
            v[idx] = (byte)log;
            if (y > maxY) maxY = y;
        }

        static void ReplaceIfInside(byte[] v, int bx, int bz, int x, int y, int z, BlockId from, BlockId to)
        {
            if (!Inside(bx, bz, x, y, z)) return;
            int idx = Chunk.Index(x - bx, y, z - bz);
            if (v[idx] == (byte)from) v[idx] = (byte)to;
        }

        // ------------------------------------------------------------------ spawn

        /// <summary>Finds a dry, grassy column near the origin to spawn the player on.</summary>
        public Vector2Int FindSpawnColumn()
        {
            for (int ring = 0; ring < 80; ring++)
            {
                int r = ring * 8;
                int steps = ring == 0 ? 1 : ring * 8;
                for (int s = 0; s < steps; s++)
                {
                    float a = s / (float)steps * Mathf.PI * 2f;
                    int x = Mathf.RoundToInt(Mathf.Cos(a) * r);
                    int z = Mathf.RoundToInt(Mathf.Sin(a) * r);
                    int h = Height(x, z);
                    if (h <= SeaLevel + 2 || h > SeaLevel + 30) continue;
                    Column c = GetColumn(x, z, h, Slope(x, z));
                    if (c.Top == BlockId.Grass) return new Vector2Int(x, z);
                }
            }
            return Vector2Int.zero;
        }
    }
}
