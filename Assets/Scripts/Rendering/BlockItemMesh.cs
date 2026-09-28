using System.Collections.Generic;
using UnityEngine;

namespace Mine
{
    /// <summary>
    /// Builds a small stand-alone mesh of one block (a unit cube centred on the origin, or a flat card for plants)
    /// using the voxel material conventions: UV.z = texture array slice, vertex colour = (sky light, face shade,
    /// block light). Used for the first-person held item and the block in the character's hand.
    /// </summary>
    public sealed class BlockItemMesh
    {
        static readonly float[] FaceShade = { 0.6f, 0.6f, 1.0f, 0.5f, 0.8f, 0.8f };

        static readonly Vector3[][] Corners =
        {
            new[] { new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(1, 0, 1) },
            new[] { new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(0, 1, 0), new Vector3(0, 0, 0) },
            new[] { new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0) },
            new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(0, 0, 1) },
            new[] { new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(0, 1, 1), new Vector3(0, 0, 1) },
            new[] { new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 0) },
        };

        readonly List<Vector3> verts = new List<Vector3>(24);
        readonly List<Vector3> uvs = new List<Vector3>(24);
        readonly List<Color32> colors = new List<Color32>(24);
        readonly List<int> tris = new List<int>(36);

        public Mesh Mesh { get; }
        public BlockId Shown { get; private set; } = (BlockId)255;
        public int ShownLight { get; private set; } = -1;

        public BlockItemMesh(string name)
        {
            Mesh = new Mesh { name = name };
            Mesh.MarkDynamic();
        }

        /// <summary>Rebuilds only when the block or the light level changed. Returns true when rebuilt.</summary>
        public bool Update(BlockId id, int packedLight)
        {
            if (id == Shown && packedLight == ShownLight) return false;
            Build(id, packedLight);
            return true;
        }

        public static Material MaterialFor(BlockId id, Material opaque, Material cutout)
        {
            return Blocks.Get(id).Layer == RenderLayer.Opaque ? opaque : cutout;
        }

        void Build(BlockId id, int packedLight)
        {
            Shown = id;
            ShownLight = packedLight;
            verts.Clear();
            uvs.Clear();
            colors.Clear();
            tris.Clear();
            BlockDef def = Blocks.Get(id);
            byte sky = (byte)((packedLight >> 4) * 17), blk = (byte)((packedLight & 15) * 17);

            if (def.Shape == BlockShape.Cross)
            {
                ushort tex = (ushort)def.Side;
                verts.Add(new Vector3(-0.5f, -0.5f, 0f));
                verts.Add(new Vector3(-0.5f, 0.5f, 0f));
                verts.Add(new Vector3(0.5f, 0.5f, 0f));
                verts.Add(new Vector3(0.5f, -0.5f, 0f));
                uvs.Add(new Vector3(0, 0, tex));
                uvs.Add(new Vector3(0, 1, tex));
                uvs.Add(new Vector3(1, 1, tex));
                uvs.Add(new Vector3(1, 0, tex));
                for (int k = 0; k < 4; k++) colors.Add(new Color32(sky, 235, blk, 255));
                tris.AddRange(new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 });
            }
            else if (id != BlockId.Air)
            {
                var offset = new Vector3(-0.5f, -0.5f, -0.5f);
                for (int f = 0; f < 6; f++)
                {
                    int bi = verts.Count;
                    ushort tex = (ushort)(f == 2 ? def.Top : (f == 3 ? def.Bottom : def.Side));
                    for (int k = 0; k < 4; k++)
                    {
                        verts.Add(Corners[f][k] + offset);
                        Vector2 uv = f == 3
                            ? new Vector2(k == 1 || k == 2 ? 1 : 0, k >= 2 ? 1 : 0)
                            : new Vector2(k >= 2 ? 1 : 0, k == 1 || k == 2 ? 1 : 0);
                        uvs.Add(new Vector3(uv.x, uv.y, tex));
                        colors.Add(new Color32(sky, (byte)(FaceShade[f] * 255f), blk, 255));
                    }
                    tris.Add(bi); tris.Add(bi + 1); tris.Add(bi + 2);
                    tris.Add(bi); tris.Add(bi + 2); tris.Add(bi + 3);
                }
            }

            Mesh.Clear();
            Mesh.SetVertices(verts);
            Mesh.SetUVs(0, uvs);
            Mesh.SetColors(colors);
            Mesh.SetTriangles(tris, 0);
            Mesh.RecalculateBounds();
        }
    }
}
