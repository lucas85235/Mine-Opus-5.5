using System.Runtime.CompilerServices;

namespace Mine
{
    /// <summary>Small deterministic math helpers that are safe to call from worker threads.</summary>
    public static class MathUtil
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u + 0x7F4A7C15u;
                h ^= (uint)x * 0x85EBCA77u; h = (h << 13) | (h >> 19); h *= 0xC2B2AE3Du;
                h ^= (uint)y * 0x27D4EB2Fu; h = (h << 13) | (h >> 19); h *= 0x165667B1u;
                h ^= (uint)z * 0x9E3779B1u; h = (h << 13) | (h >> 19); h *= 0x85EBCA77u;
                h ^= h >> 15; h *= 0x2C1B3C6Du;
                h ^= h >> 12; h *= 0x297A2D39u;
                h ^= h >> 15;
                return h;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Hash01(int x, int y, int z, int seed)
        {
            return (Hash(x, y, z, seed) & 0xFFFFFF) / 16777216f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FloorDiv(int a, int b)
        {
            int q = a / b;
            if ((a % b != 0) && ((a < 0) != (b < 0))) q--;
            return q;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FastFloor(float f)
        {
            int i = (int)f;
            return f < i ? i - 1 : i;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Stable string hash (FNV-1a) used to turn typed seeds into numbers.</summary>
        public static int StableHash(string s)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < s.Length; i++)
                {
                    h ^= s[i];
                    h *= 16777619u;
                }
                return (int)h;
            }
        }
    }
}
