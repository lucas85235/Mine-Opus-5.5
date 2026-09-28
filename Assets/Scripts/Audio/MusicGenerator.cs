using System;

namespace Mine
{
    /// <summary>
    /// Generative calm piano pieces: a chord progression with soft pads, occasional arpeggios and a pentatonic
    /// random-walk melody, rendered additively and passed through a small Schroeder reverb. Thread-safe (pure).
    /// </summary>
    public static class MusicGenerator
    {
        const int TableSize = 4096;
        static readonly float[] SineTable = BuildTable();

        static float[] BuildTable()
        {
            var t = new float[TableSize];
            for (int i = 0; i < TableSize; i++) t[i] = (float)Math.Sin(2 * Math.PI * i / TableSize);
            return t;
        }

        static float Sin(double phase01)
        {
            double f = phase01 - Math.Floor(phase01);
            return SineTable[(int)(f * TableSize) & (TableSize - 1)];
        }

        static float MidiToFreq(int note)
        {
            return 440f * (float)Math.Pow(2.0, (note - 69) / 12.0);
        }

        public static float[] Generate(int seed, float seconds)
        {
            int sr = Synth.SampleRate;
            var rng = new Random(seed);
            var buf = new float[(int)(seconds * sr)];

            float bpm = 62 + rng.Next(14);
            float beat = 60f / bpm;
            int[][] progressions =
            {
                new[] { 0, 9, 5, 7 },
                new[] { 9, 5, 0, 7 },
                new[] { 0, 4, 9, 5 },
                new[] { 5, 0, 7, 9 },
                new[] { 0, 5, 9, 7 },
            };
            int[] prog = progressions[rng.Next(progressions.Length)];
            int key = rng.Next(-3, 4);
            int[] penta = { 0, 2, 4, 7, 9 };
            int melodyIndex = 5 + rng.Next(3);

            float barLength = beat * 4f;
            int bars = (int)((seconds - 4f) / barLength);
            for (int bar = 0; bar < bars; bar++)
            {
                int root = prog[bar % prog.Length];
                bool minor = root == 9 || root == 4 || root == 2;
                int[] chord = { root, root + (minor ? 3 : 4), root + 7 };
                float barStart = 1f + bar * barLength;

                // Soft pad in the low register.
                for (int c = 0; c < chord.Length; c++)
                {
                    int note = 48 + key + chord[c];
                    AddNote(buf, barStart + c * 0.02f, MidiToFreq(note), barLength * 1.1f, 0.10f, 0.9f);
                }
                if (bar % 2 == 0) AddNote(buf, barStart, MidiToFreq(36 + key + root), barLength * 1.8f, 0.12f, 0.6f);

                // Arpeggio on some bars.
                if (rng.NextDouble() < 0.45)
                {
                    for (int k = 0; k < 8; k++)
                    {
                        int note = 60 + key + chord[k % 3] + (k >= 3 && k < 6 ? 12 : 0);
                        AddNote(buf, barStart + k * beat * 0.5f, MidiToFreq(note), beat, 0.07f, 1.6f);
                    }
                }

                // Melody: sparse pentatonic random walk.
                for (int b = 0; b < 4; b++)
                {
                    if (rng.NextDouble() > 0.55) continue;
                    melodyIndex = Math.Max(0, Math.Min(12, melodyIndex + rng.Next(-2, 3)));
                    int octave = melodyIndex / 5, degree = melodyIndex % 5;
                    int note = 67 + key + octave * 12 + penta[degree];
                    float offset = rng.NextDouble() < 0.25 ? beat * 0.5f : 0f;
                    AddNote(buf, barStart + b * beat + offset, MidiToFreq(note), beat * (1 + rng.Next(2)), 0.16f, 1.3f);
                }
            }

            buf = Reverb(buf, 0.28f);
            Synth.Normalize(buf, 0.55f);
            Synth.FadeEdges(buf, 1.5f, 3f);
            return buf;
        }

        static void AddNote(float[] buf, float startSeconds, float freq, float duration, float velocity, float decayRate)
        {
            int sr = Synth.SampleRate;
            int start = (int)(startSeconds * sr);
            int len = (int)((duration + 1.2f) * sr);
            double inc = freq / sr;
            float decay = decayRate + freq / 900f;
            for (int i = 0; i < len; i++)
            {
                int idx = start + i;
                if (idx >= buf.Length) break;
                float t = i / (float)sr;
                float env = Math.Min(1f, t / 0.008f) * (float)Math.Exp(-t * decay);
                if (t > duration) env *= (float)Math.Exp(-(t - duration) * 6f);
                if (env < 0.0005f && t > 0.05f) break;
                double ph = inc * i;
                float v = Sin(ph)
                          + 0.42f * Sin(ph * 2.0) * (float)Math.Exp(-t * 2.2f)
                          + 0.18f * Sin(ph * 3.0) * (float)Math.Exp(-t * 3.5f)
                          + 0.07f * Sin(ph * 4.01) * (float)Math.Exp(-t * 5f);
                buf[idx] += v * env * velocity;
            }
        }

        static float[] Reverb(float[] input, float wet)
        {
            int[] combDelays = { 1557, 1617, 1491, 1422 };
            int[] allDelays = { 225, 556 };
            var combs = new float[combDelays.Length][];
            var combIdx = new int[combDelays.Length];
            var combLp = new float[combDelays.Length];
            for (int c = 0; c < combs.Length; c++) combs[c] = new float[combDelays[c]];
            var alls = new float[allDelays.Length][];
            var allIdx = new int[allDelays.Length];
            for (int a = 0; a < alls.Length; a++) alls[a] = new float[allDelays[a]];

            var output = new float[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                float x = input[i];
                float sum = 0f;
                for (int c = 0; c < combs.Length; c++)
                {
                    float[] line = combs[c];
                    float y = line[combIdx[c]];
                    combLp[c] = y * 0.7f + combLp[c] * 0.3f;
                    line[combIdx[c]] = x + combLp[c] * 0.83f;
                    combIdx[c] = (combIdx[c] + 1) % line.Length;
                    sum += y;
                }
                float v = sum * 0.25f;
                for (int a = 0; a < alls.Length; a++)
                {
                    float[] line = alls[a];
                    float delayed = line[allIdx[a]];
                    float y = -v + delayed;
                    line[allIdx[a]] = v + delayed * 0.5f;
                    allIdx[a] = (allIdx[a] + 1) % line.Length;
                    v = y;
                }
                output[i] = x * (1f - wet) + v * wet;
            }
            return output;
        }
    }
}
