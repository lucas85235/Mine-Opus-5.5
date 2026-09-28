using UnityEngine;

namespace Mine
{
    /// <summary>
    /// A 16 x 128 x 16 column of blocks. Voxel data is a flat byte array (index = x | z &lt;&lt; 4 | y &lt;&lt; 8).
    /// Worker threads read/write the arrays; all bookkeeping flags except <see cref="Disposed"/> are main-thread only.
    /// </summary>
    public sealed class Chunk
    {
        public const int Size = 16;
        public const int Height = 128;
        public const int Volume = Size * Size * Height;

        public readonly int X;
        public readonly int Z;
        public readonly long Key;
        public readonly byte[] Voxels = new byte[Volume];

        /// <summary>Packed light per cell ((sky &lt;&lt; 4) | block), produced by the mesher. Null until first mesh.</summary>
        public byte[] Light;

        public bool Generated;
        public bool Modified;
        public bool NeedsMesh;
        public bool UrgentMesh;
        public bool MeshInFlight;
        public bool HasMesh;
        public volatile bool Disposed;

        /// <summary>Highest y that may contain a non-air block (meshing stops there).</summary>
        public int MaxY;
        public int EmitterCount;

        public GameObject Go;
        public MeshFilter Filter;
        public MeshRenderer Renderer;
        public Mesh Mesh;

        public Chunk(int x, int z)
        {
            X = x;
            Z = z;
            Key = VoxelWorld.Key(x, z);
        }

        public static int Index(int x, int y, int z)
        {
            return x | (z << 4) | (y << 8);
        }

        /// <summary>Recomputes <see cref="MaxY"/> and <see cref="EmitterCount"/> from the voxel data.</summary>
        public void RecountMeta()
        {
            MaxY = 0;
            for (int y = Height - 1; y >= 0 && MaxY == 0; y--)
            {
                int start = y << 8;
                for (int i = 0; i < 256; i++)
                {
                    if (Voxels[start + i] != 0)
                    {
                        MaxY = y;
                        break;
                    }
                }
            }

            int emitters = 0;
            var emission = Blocks.Emission;
            for (int i = 0; i < Volume; i++)
            {
                if (emission[Voxels[i]] > 0) emitters++;
            }
            EmitterCount = emitters;
        }
    }
}
