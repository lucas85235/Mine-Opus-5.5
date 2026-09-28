namespace Mine
{
    /// <summary>
    /// Seeded gradient (Perlin "improved") noise in 2D and 3D. Immutable after construction, so it can be
    /// shared by all generator threads.
    /// </summary>
    public sealed class Perlin
    {
        readonly int[] p = new int[512];

        public Perlin(int seed)
        {
            var rng = new System.Random(seed);
            var perm = new int[256];
            for (int i = 0; i < 256; i++) perm[i] = i;
            for (int i = 255; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int t = perm[i];
                perm[i] = perm[j];
                perm[j] = t;
            }
            for (int i = 0; i < 512; i++) p[i] = perm[i & 255];
        }

        static float Fade(float t)
        {
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        static float Lerp(float a, float b, float t)
        {
            return a + t * (b - a);
        }

        static float Grad2(int hash, float x, float y)
        {
            switch (hash & 7)
            {
                case 0: return x + y;
                case 1: return -x + y;
                case 2: return x - y;
                case 3: return -x - y;
                case 4: return x * 1.41421f;
                case 5: return -x * 1.41421f;
                case 6: return y * 1.41421f;
                default: return -y * 1.41421f;
            }
        }

        static float Grad3(int hash, float x, float y, float z)
        {
            int h = hash & 15;
            float u = h < 8 ? x : y;
            float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        /// <summary>2D noise, roughly in [-1, 1].</summary>
        public float Noise(float x, float y)
        {
            int xi = MathUtil.FastFloor(x);
            int yi = MathUtil.FastFloor(y);
            float xf = x - xi;
            float yf = y - yi;
            xi &= 255;
            yi &= 255;
            float u = Fade(xf);
            float v = Fade(yf);
            int aa = p[p[xi] + yi];
            int ab = p[p[xi] + yi + 1];
            int ba = p[p[xi + 1] + yi];
            int bb = p[p[xi + 1] + yi + 1];
            float x1 = Lerp(Grad2(aa, xf, yf), Grad2(ba, xf - 1f, yf), u);
            float x2 = Lerp(Grad2(ab, xf, yf - 1f), Grad2(bb, xf - 1f, yf - 1f), u);
            return Lerp(x1, x2, v);
        }

        /// <summary>3D noise, roughly in [-1, 1].</summary>
        public float Noise(float x, float y, float z)
        {
            int X = MathUtil.FastFloor(x);
            int Y = MathUtil.FastFloor(y);
            int Z = MathUtil.FastFloor(z);
            x -= X;
            y -= Y;
            z -= Z;
            X &= 255;
            Y &= 255;
            Z &= 255;
            float u = Fade(x);
            float v = Fade(y);
            float w = Fade(z);
            int A = p[X] + Y, AA = p[A] + Z, AB = p[A + 1] + Z;
            int B = p[X + 1] + Y, BA = p[B] + Z, BB = p[B + 1] + Z;
            return Lerp(
                Lerp(Lerp(Grad3(p[AA], x, y, z), Grad3(p[BA], x - 1f, y, z), u),
                     Lerp(Grad3(p[AB], x, y - 1f, z), Grad3(p[BB], x - 1f, y - 1f, z), u), v),
                Lerp(Lerp(Grad3(p[AA + 1], x, y, z - 1f), Grad3(p[BA + 1], x - 1f, y, z - 1f), u),
                     Lerp(Grad3(p[AB + 1], x, y - 1f, z - 1f), Grad3(p[BB + 1], x - 1f, y - 1f, z - 1f), u), v),
                w);
        }

        /// <summary>Fractal sum of 2D octaves, normalised by the amplitude sum.</summary>
        public float Fbm(float x, float y, int octaves, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Noise(x, y) * amp;
                norm += amp;
                x = x * lacunarity + 17.13f;
                y = y * lacunarity + 31.71f;
                amp *= gain;
            }
            return sum / norm;
        }

        /// <summary>Fractal sum of 3D octaves, normalised by the amplitude sum.</summary>
        public float Fbm(float x, float y, float z, int octaves, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Noise(x, y, z) * amp;
                norm += amp;
                x = x * lacunarity + 17.13f;
                y = y * lacunarity + 31.71f;
                z = z * lacunarity + 9.37f;
                amp *= gain;
            }
            return sum / norm;
        }
    }
}
