using System;

namespace Mine
{
    /// <summary>RBJ-cookbook biquad filter.</summary>
    public struct Biquad
    {
        float b0, b1, b2, a1, a2;
        float x1, x2, y1, y2;

        static Biquad Make(float b0, float b1, float b2, float a0, float a1, float a2)
        {
            return new Biquad { b0 = b0 / a0, b1 = b1 / a0, b2 = b2 / a0, a1 = a1 / a0, a2 = a2 / a0 };
        }

        public static Biquad LowPass(float freq, float q, int sr)
        {
            float w = 2f * (float)Math.PI * Math.Min(freq, sr * 0.45f) / sr;
            float cs = (float)Math.Cos(w), alpha = (float)Math.Sin(w) / (2f * q);
            return Make((1f - cs) / 2f, 1f - cs, (1f - cs) / 2f, 1f + alpha, -2f * cs, 1f - alpha);
        }

        public static Biquad HighPass(float freq, float q, int sr)
        {
            float w = 2f * (float)Math.PI * Math.Min(freq, sr * 0.45f) / sr;
            float cs = (float)Math.Cos(w), alpha = (float)Math.Sin(w) / (2f * q);
            return Make((1f + cs) / 2f, -(1f + cs), (1f + cs) / 2f, 1f + alpha, -2f * cs, 1f - alpha);
        }

        public static Biquad BandPass(float freq, float q, int sr)
        {
            float w = 2f * (float)Math.PI * Math.Min(freq, sr * 0.45f) / sr;
            float cs = (float)Math.Cos(w), alpha = (float)Math.Sin(w) / (2f * q);
            return Make(alpha, 0f, -alpha, 1f + alpha, -2f * cs, 1f - alpha);
        }

        /// <summary>Takes the coefficients of another filter while keeping this filter's history (no clicks).</summary>
        public void Retune(Biquad other)
        {
            b0 = other.b0;
            b1 = other.b1;
            b2 = other.b2;
            a1 = other.a1;
            a2 = other.a2;
        }

        public float Process(float x)
        {
            float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1;
            x1 = x;
            y2 = y1;
            y1 = y;
            return y;
        }
    }

    /// <summary>Sound synthesis building blocks. Everything returns mono float sample buffers.</summary>
    public static class Synth
    {
        public const int SampleRate = 44100;

        public static float[] Buffer(float seconds)
        {
            return new float[Math.Max(1, (int)(seconds * SampleRate))];
        }

        public static void Normalize(float[] s, float peak)
        {
            float max = 0f;
            for (int i = 0; i < s.Length; i++) max = Math.Max(max, Math.Abs(s[i]));
            if (max < 1e-6f) return;
            float k = peak / max;
            for (int i = 0; i < s.Length; i++) s[i] *= k;
        }

        public static void FadeEdges(float[] s, float inSeconds, float outSeconds)
        {
            int fi = Math.Min(s.Length, (int)(inSeconds * SampleRate));
            int fo = Math.Min(s.Length, (int)(outSeconds * SampleRate));
            for (int i = 0; i < fi; i++) s[i] *= i / (float)fi;
            for (int i = 0; i < fo; i++) s[s.Length - 1 - i] *= i / (float)fo;
        }

        public struct Material
        {
            public float Low, High;          // band limits of the noise body
            public float Q;
            public float Decay;              // seconds (exp time constant)
            public float Grain;              // 0 smooth .. 1 crunchy
            public float ThumpFreq, Thump;   // low sine knock
            public float Ring, RingFreq;     // resonant tonal part (wood, glass)
        }

        public static Material ForSound(SoundMaterial m)
        {
            switch (m)
            {
                case SoundMaterial.Stone: return new Material { Low = 900, High = 3200, Q = 0.8f, Decay = 0.07f, Grain = 0.75f, ThumpFreq = 150, Thump = 0.45f };
                case SoundMaterial.Wood: return new Material { Low = 350, High = 1600, Q = 1.6f, Decay = 0.08f, Grain = 0.25f, ThumpFreq = 190, Thump = 0.5f, Ring = 0.35f, RingFreq = 420 };
                case SoundMaterial.Dirt: return new Material { Low = 120, High = 900, Q = 0.7f, Decay = 0.1f, Grain = 0.65f, ThumpFreq = 95, Thump = 0.4f };
                case SoundMaterial.Grass: return new Material { Low = 1500, High = 6000, Q = 0.6f, Decay = 0.11f, Grain = 0.95f, ThumpFreq = 110, Thump = 0.15f };
                case SoundMaterial.Sand: return new Material { Low = 300, High = 2600, Q = 0.6f, Decay = 0.13f, Grain = 1f, ThumpFreq = 90, Thump = 0.15f };
                case SoundMaterial.Gravel: return new Material { Low = 600, High = 2400, Q = 0.8f, Decay = 0.11f, Grain = 0.95f, ThumpFreq = 120, Thump = 0.25f };
                case SoundMaterial.Glass: return new Material { Low = 2500, High = 9000, Q = 0.7f, Decay = 0.09f, Grain = 0.5f, ThumpFreq = 300, Thump = 0.1f, Ring = 0.6f, RingFreq = 2600 };
                case SoundMaterial.Snow: return new Material { Low = 500, High = 3000, Q = 0.6f, Decay = 0.12f, Grain = 0.9f, ThumpFreq = 100, Thump = 0.12f };
                case SoundMaterial.Leaves: return new Material { Low = 2000, High = 7000, Q = 0.6f, Decay = 0.1f, Grain = 1f, ThumpFreq = 0, Thump = 0f };
                default: return new Material { Low = 150, High = 900, Q = 0.7f, Decay = 0.09f, Grain = 0.3f, ThumpFreq = 90, Thump = 0.35f };
            }
        }

        /// <summary>Noise burst shaped like an impact on the given material.</summary>
        public static float[] Impact(Material m, float seconds, float decayScale, int seed, float gain)
        {
            var s = Buffer(seconds);
            var rng = new Random(seed);
            var hp = Biquad.HighPass(m.Low, m.Q, SampleRate);
            var lp = Biquad.LowPass(m.High, m.Q, SampleRate);
            var lp2 = Biquad.LowPass(m.High, m.Q, SampleRate);
            float decay = m.Decay * decayScale;
            int grainLen = SampleRate / 450;
            float grainAmp = 1f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                if (i % grainLen == 0)
                {
                    float r = (float)rng.NextDouble();
                    grainAmp = 1f - m.Grain + m.Grain * r * r * 2.2f;
                }
                float env = (float)Math.Exp(-t / decay) * Math.Min(1f, t / 0.002f);
                float n = (float)(rng.NextDouble() * 2.0 - 1.0);
                float body = lp2.Process(lp.Process(hp.Process(n))) * grainAmp * env;
                float thump = m.Thump > 0f ? (float)Math.Sin(2 * Math.PI * m.ThumpFreq * t) * (float)Math.Exp(-t / 0.035f) * m.Thump : 0f;
                float ring = 0f;
                if (m.Ring > 0f)
                {
                    float f = m.RingFreq;
                    ring = ((float)Math.Sin(2 * Math.PI * f * t) + 0.5f * (float)Math.Sin(2 * Math.PI * f * 2.76 * t) + 0.3f * (float)Math.Sin(2 * Math.PI * f * 5.4 * t))
                           * (float)Math.Exp(-t / (decay * 1.4f)) * m.Ring;
                }
                s[i] = body * 2.5f + thump + ring;
            }
            Normalize(s, gain);
            FadeEdges(s, 0.001f, 0.01f);
            return s;
        }

        /// <summary>Bright shattering sound for glass.</summary>
        public static float[] Shatter(int seed)
        {
            var s = Buffer(0.45f);
            var rng = new Random(seed);
            var hp = Biquad.HighPass(2500, 0.7f, SampleRate);
            int partials = 14;
            var freqs = new float[partials];
            var starts = new float[partials];
            for (int p = 0; p < partials; p++)
            {
                freqs[p] = 2200f + (float)rng.NextDouble() * 5500f;
                starts[p] = (float)rng.NextDouble() * 0.12f;
            }
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                float v = hp.Process((float)(rng.NextDouble() * 2 - 1)) * (float)Math.Exp(-t / 0.05f) * 1.5f;
                for (int p = 0; p < partials; p++)
                {
                    float lt = t - starts[p];
                    if (lt < 0f) continue;
                    v += (float)Math.Sin(2 * Math.PI * freqs[p] * lt) * (float)Math.Exp(-lt / 0.06f) * 0.25f;
                }
                s[i] = v;
            }
            Normalize(s, 0.8f);
            FadeEdges(s, 0.001f, 0.02f);
            return s;
        }

        public static float[] Pop()
        {
            var s = Buffer(0.09f);
            double phase = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                float f = 500f + 900f * (t / 0.09f);
                phase += 2 * Math.PI * f / SampleRate;
                s[i] = (float)Math.Sin(phase) * (float)Math.Exp(-t / 0.03f) * Math.Min(1f, t / 0.003f);
            }
            Normalize(s, 0.6f);
            return s;
        }

        public static float[] Click()
        {
            var s = Buffer(0.05f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                float sq = Math.Sign(Math.Sin(2 * Math.PI * 1100 * t));
                s[i] = sq * (float)Math.Exp(-t / 0.012f) * 0.4f + (float)Math.Sin(2 * Math.PI * 2200 * t) * (float)Math.Exp(-t / 0.006f) * 0.3f;
            }
            var lp = Biquad.LowPass(5000, 0.7f, SampleRate);
            for (int i = 0; i < s.Length; i++) s[i] = lp.Process(s[i]);
            Normalize(s, 0.5f);
            FadeEdges(s, 0.0005f, 0.005f);
            return s;
        }

        public static float[] Splash(int seed)
        {
            var s = Buffer(0.7f);
            var rng = new Random(seed);
            var lp = Biquad.LowPass(3000, 0.7f, SampleRate);
            int bubbles = 10;
            var bStart = new float[bubbles];
            var bFreq = new float[bubbles];
            for (int b = 0; b < bubbles; b++)
            {
                bStart[b] = 0.05f + (float)rng.NextDouble() * 0.5f;
                bFreq[b] = 500f + (float)rng.NextDouble() * 900f;
            }
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                if (i % 256 == 0) lp.Retune(Biquad.LowPass(3500f * (float)Math.Exp(-t / 0.2f) + 400f, 0.7f, SampleRate));
                float v = lp.Process((float)(rng.NextDouble() * 2 - 1)) * (float)Math.Exp(-t / 0.18f) * Math.Min(1f, t / 0.01f);
                for (int b = 0; b < bubbles; b++)
                {
                    float lt = t - bStart[b];
                    if (lt < 0f || lt > 0.06f) continue;
                    float f = bFreq[b] * (1f + lt * 8f);
                    v += (float)Math.Sin(2 * Math.PI * f * lt) * (float)Math.Exp(-lt / 0.02f) * 0.25f;
                }
                s[i] = v;
            }
            Normalize(s, 0.7f);
            FadeEdges(s, 0.002f, 0.05f);
            return s;
        }

        /// <summary>Animal voice: sawtooth through two formant filters with a pitch contour.</summary>
        public static float[] Voice(float seconds, float f0Start, float f0End, float formant1, float formant2, float vibrato, float breath, int seed)
        {
            var s = Buffer(seconds);
            var rng = new Random(seed);
            var bp1 = Biquad.BandPass(formant1, 3f, SampleRate);
            var bp2 = Biquad.BandPass(formant2, 4f, SampleRate);
            var lp = Biquad.LowPass(2500, 0.7f, SampleRate);
            double phase = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                float k = t / seconds;
                float f0 = f0Start + (f0End - f0Start) * k;
                f0 *= 1f + vibrato * (float)Math.Sin(2 * Math.PI * 6.5 * t);
                phase += f0 / SampleRate;
                float saw = (float)(2.0 * (phase - Math.Floor(phase)) - 1.0);
                float noise = (float)(rng.NextDouble() * 2 - 1) * breath;
                float src = saw + noise;
                float v = bp1.Process(src) * 1.2f + bp2.Process(src) * 0.8f;
                float env = Math.Min(1f, t / 0.03f) * Math.Min(1f, (seconds - t) / 0.08f);
                s[i] = lp.Process(v) * env;
            }
            Normalize(s, 0.8f);
            return s;
        }

        public static float[] Concat(params float[][] parts)
        {
            int len = 0;
            foreach (float[] p in parts) len += p.Length;
            var s = new float[len];
            int o = 0;
            foreach (float[] p in parts)
            {
                Array.Copy(p, 0, s, o, p.Length);
                o += p.Length;
            }
            return s;
        }

        public static float[] Silence(float seconds)
        {
            return Buffer(seconds);
        }

        /// <summary>Seamless looping wind: filtered noise with slowly wandering cutoff and gain.</summary>
        public static float[] Wind(float seconds, int seed)
        {
            var s = Buffer(seconds);
            var rng = new Random(seed);
            var lp = Biquad.LowPass(500, 0.9f, SampleRate);
            var lp2 = Biquad.LowPass(500, 0.9f, SampleRate);
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                float lfo = 0.5f + 0.5f * (float)Math.Sin(2 * Math.PI * t / seconds * 3) * (float)Math.Sin(2 * Math.PI * t / seconds * 2 + 1.3);
                if (i % 256 == 0) lp.Retune(Biquad.LowPass(250f + 650f * lfo, 0.9f, SampleRate));
                float n = (float)(rng.NextDouble() * 2 - 1);
                s[i] = lp2.Process(lp.Process(n)) * (0.35f + 0.65f * lfo);
            }
            s = MakeLoop(s, 1.5f);
            Normalize(s, 0.6f);
            return s;
        }

        /// <summary>Night-time crickets: short chirp trains at ~4.3 kHz.</summary>
        public static float[] Crickets(float seconds, int seed)
        {
            var s = Buffer(seconds);
            var rng = new Random(seed);
            float t0 = 0.2f;
            while (t0 < seconds - 0.5f)
            {
                float f = 4000f + (float)rng.NextDouble() * 700f;
                int pulses = 3 + rng.Next(3);
                float amp = 0.3f + (float)rng.NextDouble() * 0.7f;
                for (int p = 0; p < pulses; p++)
                {
                    float start = t0 + p * 0.045f;
                    int si = (int)(start * SampleRate);
                    int len = (int)(0.025f * SampleRate);
                    for (int k = 0; k < len && si + k < s.Length; k++)
                    {
                        float lt = k / (float)SampleRate;
                        float env = (float)Math.Sin(Math.PI * k / len);
                        s[si + k] += (float)Math.Sin(2 * Math.PI * f * lt) * env * amp;
                    }
                }
                t0 += 0.35f + (float)rng.NextDouble() * 0.9f;
            }
            Normalize(s, 0.35f);
            return s;
        }

        /// <summary>Crossfades the tail into the head so the (shortened) buffer loops without a click.</summary>
        public static float[] MakeLoop(float[] s, float fadeSeconds)
        {
            int n = Math.Min(s.Length / 2, (int)(fadeSeconds * SampleRate));
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                int tail = s.Length - n + i;
                s[i] = s[i] * k + s[tail] * (1f - k);
            }
            var result = new float[s.Length - n];
            Array.Copy(s, result, result.Length);
            return result;
        }
    }
}
