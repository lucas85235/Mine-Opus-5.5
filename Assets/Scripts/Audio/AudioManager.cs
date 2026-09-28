using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Mine
{
    /// <summary>Plays the synthesized sounds: pooled 3D one-shots, ambient loops and generative music.</summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        public SoundLibrary Library { get; private set; }
        public float SfxVolume = 1f;
        public float MusicVolume = 0.55f;

        // Set every frame by GameManager.
        public bool InWorld;
        public float Daylight = 1f;
        public bool Underwater;
        public float Altitude = 64f;

        AudioSource[] pool;
        int next;
        AudioSource ui;
        AudioSource wind;
        AudioSource crickets;
        AudioSource musicSource;
        readonly List<AudioClip> music = new List<AudioClip>();
        readonly List<Task<float[]>> musicTasks = new List<Task<float[]>>();
        float musicTimer = 20f;

        public void Init()
        {
            Instance = this;
            Library = new SoundLibrary();
            Library.Build();

            pool = new AudioSource[16];
            for (int i = 0; i < pool.Length; i++)
            {
                var go = new GameObject("Sfx" + i);
                go.transform.SetParent(transform, false);
                AudioSource s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 2f;
                s.maxDistance = 32f;
                s.dopplerLevel = 0f;
                pool[i] = s;
            }

            ui = Create2D("UI", false);
            wind = Create2D("Wind", true);
            wind.clip = Library.Wind;
            wind.volume = 0f;
            wind.Play();
            crickets = Create2D("Crickets", true);
            crickets.clip = Library.Crickets;
            crickets.volume = 0f;
            crickets.Play();
            musicSource = Create2D("Music", false);

            int[] seeds = { 1789, 4242, 9001 };
            foreach (int seed in seeds)
            {
                int s = seed;
                musicTasks.Add(Task.Run(() => MusicGenerator.Generate(s, 100f)));
            }
        }

        AudioSource Create2D(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            AudioSource s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.loop = loop;
            return s;
        }

        void Update()
        {
            for (int i = musicTasks.Count - 1; i >= 0; i--)
            {
                Task<float[]> t = musicTasks[i];
                if (!t.IsCompleted) continue;
                musicTasks.RemoveAt(i);
                if (t.Status == TaskStatus.RanToCompletion) music.Add(SoundLibrary.Clip("music" + music.Count, t.Result));
                else if (t.Exception != null) Debug.LogException(t.Exception);
            }

            float dt = Time.unscaledDeltaTime;
            float windTarget = 0f, cricketTarget = 0f;
            if (InWorld && !Underwater)
            {
                windTarget = Mathf.Lerp(0.05f, 0.35f, Mathf.InverseLerp(60f, 110f, Altitude)) * SfxVolume;
                cricketTarget = (1f - Daylight) * 0.22f * SfxVolume * Mathf.InverseLerp(30f, 50f, Altitude);
            }
            wind.volume = Mathf.MoveTowards(wind.volume, windTarget, dt * 0.3f);
            crickets.volume = Mathf.MoveTowards(crickets.volume, cricketTarget, dt * 0.2f);

            musicSource.volume = MusicVolume * 0.6f;
            if (!musicSource.isPlaying && music.Count > 0)
            {
                musicTimer -= dt;
                if (musicTimer <= 0f)
                {
                    musicSource.clip = music[Random.Range(0, music.Count)];
                    musicSource.Play();
                    musicTimer = Random.Range(90f, 200f);
                }
            }
        }

        public void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (clip == null || pool == null) return;
            AudioSource s = pool[next];
            next = (next + 1) % pool.Length;
            s.transform.position = position;
            s.pitch = pitch;
            s.volume = volume * SfxVolume;
            s.clip = clip;
            s.Play();
        }

        public void Play2D(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null || ui == null) return;
            ui.pitch = pitch;
            ui.PlayOneShot(clip, volume * SfxVolume);
        }

        public void PlayBreak(BlockId id, Vector3 pos)
        {
            PlayAt(Library.Break(Blocks.Get(id).Sound), pos, 0.9f, Random.Range(0.85f, 1.1f));
        }

        public void PlayPlace(BlockId id, Vector3 pos)
        {
            PlayAt(Library.Step(Blocks.Get(id).Sound), pos, 0.9f, Random.Range(0.75f, 0.9f));
        }

        public void PlayHit(BlockId id, Vector3 pos)
        {
            PlayAt(Library.Hit(Blocks.Get(id).Sound), pos, 0.45f, Random.Range(0.9f, 1.1f));
        }

        public void PlayStep(BlockId id, Vector3 pos, float volume)
        {
            PlayAt(Library.Step(Blocks.Get(id).Sound), pos, volume, Random.Range(0.9f, 1.12f));
        }

        public void PlayClick()
        {
            Play2D(Library.Click, 0.6f, Random.Range(0.95f, 1.05f));
        }

        public void PlayPop()
        {
            Play2D(Library.Pop, 0.35f, Random.Range(0.9f, 1.3f));
        }
    }
}
