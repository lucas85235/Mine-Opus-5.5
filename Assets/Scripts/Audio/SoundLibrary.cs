using System.Collections.Generic;
using UnityEngine;

namespace Mine
{
    public enum MobKind { Pig, Sheep, Cow }

    /// <summary>Builds every sound effect clip at startup from the synthesis functions.</summary>
    public sealed class SoundLibrary
    {
        readonly Dictionary<SoundMaterial, AudioClip[]> breakClips = new Dictionary<SoundMaterial, AudioClip[]>();
        readonly Dictionary<SoundMaterial, AudioClip[]> stepClips = new Dictionary<SoundMaterial, AudioClip[]>();
        readonly Dictionary<SoundMaterial, AudioClip[]> hitClips = new Dictionary<SoundMaterial, AudioClip[]>();
        readonly Dictionary<MobKind, AudioClip[]> mobIdle = new Dictionary<MobKind, AudioClip[]>();
        readonly Dictionary<MobKind, AudioClip> mobHurt = new Dictionary<MobKind, AudioClip>();

        public AudioClip Pop { get; private set; }
        public AudioClip Click { get; private set; }
        public AudioClip Splash { get; private set; }
        public AudioClip Poof { get; private set; }
        public AudioClip Wind { get; private set; }
        public AudioClip Crickets { get; private set; }

        public void Build()
        {
            int seed = 1;
            foreach (SoundMaterial m in System.Enum.GetValues(typeof(SoundMaterial)))
            {
                Synth.Material mat = Synth.ForSound(m);
                var brk = new AudioClip[3];
                var step = new AudioClip[4];
                var hit = new AudioClip[2];
                for (int i = 0; i < brk.Length; i++)
                {
                    float[] data = m == SoundMaterial.Glass ? Synth.Shatter(seed++) : Synth.Impact(mat, 0.4f, 1.6f, seed++, 0.9f);
                    brk[i] = Clip("break_" + m + i, data);
                }
                for (int i = 0; i < step.Length; i++) step[i] = Clip("step_" + m + i, Synth.Impact(mat, 0.16f, 0.7f, seed++, 0.55f));
                for (int i = 0; i < hit.Length; i++) hit[i] = Clip("hit_" + m + i, Synth.Impact(mat, 0.12f, 0.45f, seed++, 0.5f));
                breakClips[m] = brk;
                stepClips[m] = step;
                hitClips[m] = hit;
            }

            Pop = Clip("pop", Synth.Pop());
            Click = Clip("click", Synth.Click());
            Splash = Clip("splash", Synth.Splash(77));
            Poof = Clip("poof", Synth.Impact(new Synth.Material { Low = 200, High = 1400, Q = 0.7f, Decay = 0.18f, Grain = 0.3f }, 0.5f, 1f, 91, 0.6f));

            // Pig: two short nasal grunts.
            mobIdle[MobKind.Pig] = new[]
            {
                Clip("pig0", Synth.Concat(Synth.Voice(0.16f, 190, 160, 700, 1150, 0.03f, 0.35f, 5), Synth.Silence(0.06f), Synth.Voice(0.2f, 175, 140, 650, 1100, 0.03f, 0.35f, 6))),
                Clip("pig1", Synth.Voice(0.28f, 200, 150, 720, 1200, 0.04f, 0.4f, 7)),
            };
            mobHurt[MobKind.Pig] = Clip("pigHurt", Synth.Voice(0.22f, 320, 260, 900, 1500, 0.05f, 0.4f, 8));

            // Sheep: vibrato "baa".
            mobIdle[MobKind.Sheep] = new[]
            {
                Clip("sheep0", Synth.Voice(0.7f, 300, 270, 800, 1250, 0.07f, 0.12f, 9)),
                Clip("sheep1", Synth.Voice(0.55f, 330, 280, 850, 1300, 0.08f, 0.12f, 10)),
            };
            mobHurt[MobKind.Sheep] = Clip("sheepHurt", Synth.Voice(0.3f, 420, 380, 900, 1400, 0.06f, 0.2f, 11));

            // Cow: long low "moo".
            mobIdle[MobKind.Cow] = new[]
            {
                Clip("cow0", Synth.Voice(1.1f, 110, 95, 420, 750, 0.02f, 0.08f, 12)),
                Clip("cow1", Synth.Voice(0.9f, 120, 100, 450, 800, 0.02f, 0.08f, 13)),
            };
            mobHurt[MobKind.Cow] = Clip("cowHurt", Synth.Voice(0.35f, 170, 150, 500, 900, 0.03f, 0.2f, 14));

            Wind = Clip("wind", Synth.Wind(14f, 21));
            Crickets = Clip("crickets", Synth.Crickets(9f, 22));
        }

        public static AudioClip Clip(string name, float[] data)
        {
            AudioClip c = AudioClip.Create(name, data.Length, 1, Synth.SampleRate, false);
            c.SetData(data, 0);
            return c;
        }

        static AudioClip Pick(AudioClip[] clips)
        {
            return clips[Random.Range(0, clips.Length)];
        }

        public AudioClip Break(SoundMaterial m) => Pick(breakClips[m]);
        public AudioClip Step(SoundMaterial m) => Pick(stepClips[m]);
        public AudioClip Hit(SoundMaterial m) => Pick(hitClips[m]);
        public AudioClip MobIdle(MobKind k) => Pick(mobIdle[k]);
        public AudioClip MobHurt(MobKind k) => mobHurt[k];
    }
}
