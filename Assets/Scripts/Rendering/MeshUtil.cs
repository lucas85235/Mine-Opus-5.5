using System.Collections.Generic;
using UnityEngine;

namespace Mine
{
    /// <summary>Procedural helper meshes (boxes with per-face UV rectangles, wire cubes).</summary>
    public static class MeshUtil
    {
        // Face order: +X, -X, +Y, -Y, +Z (front), -Z (back)
        static readonly Vector3[] Normals =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
        };

        static readonly Vector3[][] FaceCorners =
        {
            new[] { new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(1, 0, 1) },
            new[] { new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(0, 1, 0), new Vector3(0, 0, 0) },
            new[] { new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0) },
            new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(0, 0, 1) },
            new[] { new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(0, 1, 1), new Vector3(0, 0, 1) },
            new[] { new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 0) },
        };

        /// <summary>
        /// Axis-aligned box centred at <paramref name="center"/>. uvRects: 6 rectangles (normalised) in face order
        /// +X, -X, +Y, -Y, +Z, -Z; pass null for a full 0..1 mapping on every face.
        /// </summary>
        public static void AddBox(List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int> tris,
            Vector3 center, Vector3 size, Rect[] uvRects)
        {
            Vector3 min = center - size * 0.5f;
            for (int f = 0; f < 6; f++)
            {
                int bi = verts.Count;
                Rect r = uvRects != null ? uvRects[f] : new Rect(0, 0, 1, 1);
                for (int k = 0; k < 4; k++)
                {
                    Vector3 c = FaceCorners[f][k];
                    verts.Add(min + Vector3.Scale(c, size));
                    normals.Add(Normals[f]);
                }
                uvs.Add(new Vector2(r.xMin, r.yMin));
                uvs.Add(new Vector2(r.xMin, r.yMax));
                uvs.Add(new Vector2(r.xMax, r.yMax));
                uvs.Add(new Vector2(r.xMax, r.yMin));
                tris.Add(bi); tris.Add(bi + 1); tris.Add(bi + 2);
                tris.Add(bi); tris.Add(bi + 2); tris.Add(bi + 3);
            }
        }

        public static Mesh Box(Vector3 center, Vector3 size, Rect[] uvRects, string name = "Box")
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            AddBox(v, n, uv, t, center, size, uvRects);
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Unit-cube outline made of 12 thin boxes (so it stays visible at any resolution).</summary>
        public static Mesh WireCube(float thickness)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            float th = thickness;
            for (int a = 0; a < 2; a++)
            for (int b = 0; b < 2; b++)
            {
                // Edges along X
                AddBox(v, n, uv, t, new Vector3(0.5f, a, b), new Vector3(1f + th, th, th), null);
                // Edges along Y
                AddBox(v, n, uv, t, new Vector3(a, 0.5f, b), new Vector3(th, 1f + th, th), null);
                // Edges along Z
                AddBox(v, n, uv, t, new Vector3(a, b, 0.5f), new Vector3(th, th, 1f + th), null);
            }
            var mesh = new Mesh { name = "WireCube" };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
