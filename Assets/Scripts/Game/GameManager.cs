using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Mine
{
    public enum GameState { Menu, Loading, Playing, Paused, Inventory }

    /// <summary>
    /// Entry point. Lives on a single scene object and builds everything else at runtime: textures, materials,
    /// world, player, UI, audio, sky, clouds and mobs. Also owns the game state machine and global hotkeys.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; } = GameState.Menu;
        public VoxelWorld World { get; private set; }
        public PlayerController Player { get; private set; }
        public PlayerInteraction Interaction { get; private set; }
        public Inventory Inventory { get; } = new Inventory();
        public DayNightCycle DayNight { get; private set; }
        public UIManager UI { get; private set; }
        public BlockParticles Particles { get; private set; }
        public MobManager Mobs { get; private set; }
        public CameraModeController CameraModes { get; private set; }
        public bool Creative { get; private set; }
        public int RenderDistance { get; private set; } = 8;
        public float Sensitivity { get; private set; } = 1f;
        public bool MusicOn { get; private set; } = true;

        static readonly int[] RenderDistances = { 4, 6, 8, 10, 12, 16 };
        static readonly float[] Sensitivities = { 0.4f, 0.6f, 0.8f, 1f, 1.25f, 1.5f, 2f, 3f };

        Camera cam;
        Light sun;
        BlockOutline outline;
        HeldItemView held;
        PlayerModel playerModel;
        CloudLayer clouds;
        AudioManager audioManager;
        bool snapToSurface;
        float loadingTime;

        // Command line helpers for quick testing: -autostart [seed] -autoshot <file.png> -autoquit
        string autoShotPath;
        bool autoQuit;
        float autoTimer;
        bool autoShotTaken;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // Frame limiter instead of VSync: VSync waits can stall badly on some laptop/DWM setups
            // (measured ~7 FPS in WaitForTargetFPS with an idle CPU and GPU).
            QualitySettings.vSyncCount = 0;
            int hz = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            Application.targetFrameRate = Mathf.Clamp(hz > 0 ? hz : 60, 60, 240);

            RenderDistance = PlayerPrefs.GetInt("mine.renderDistance", 8);
            Sensitivity = PlayerPrefs.GetFloat("mine.sensitivity", 1f);
            MusicOn = PlayerPrefs.GetInt("mine.music", 1) == 1;

            TextureFactory.Build();
            MaterialLibrary.Build();
            SetupCameraAndLighting();

            DayNight = gameObject.AddComponent<DayNightCycle>();
            DayNight.Sun = sun;
            DayNight.ViewDistance = RenderDistance * Chunk.Size;

            var audioGo = new GameObject("Audio");
            audioGo.transform.SetParent(transform, false);
            audioManager = audioGo.AddComponent<AudioManager>();
            audioManager.Init();
            audioManager.MusicVolume = MusicOn ? 0.55f : 0f;

            var fx = new GameObject("Effects");
            fx.transform.SetParent(transform, false);
            Particles = fx.AddComponent<BlockParticles>();
            Particles.Init();
            outline = fx.AddComponent<BlockOutline>();
            outline.Init();

            var cloudGo = new GameObject("Sky");
            cloudGo.transform.SetParent(transform, false);
            clouds = cloudGo.AddComponent<CloudLayer>();
            clouds.Init();

            var mobGo = new GameObject("Mobs");
            Mobs = mobGo.AddComponent<MobManager>();

            var playerGo = new GameObject("Player");
            Player = playerGo.AddComponent<PlayerController>();
            Player.Init(cam);
            Player.Sensitivity = Sensitivity;
            Interaction = playerGo.AddComponent<PlayerInteraction>();
            Interaction.Init(this, Player, outline);
            held = playerGo.AddComponent<HeldItemView>();
            held.Init(Player, Interaction, Inventory);
            playerModel = playerGo.AddComponent<PlayerModel>();
            playerModel.Init(Player, Interaction, Inventory);
            CameraModes = playerGo.AddComponent<CameraModeController>();
            CameraModes.Init(Player, playerModel, held);

            UI = gameObject.AddComponent<UIManager>();
            UI.Build(this);

            EnterMenu();
            ParseCommandLine();
        }

        void ParseCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            string autoStartSeed = null;
            for (int i = 0; i < args.Length; i++)
            {
                string next = i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : null;
                switch (args[i])
                {
                    case "-autostart":
                        autoStartSeed = next ?? "";
                        break;
                    case "-autoshot":
                        autoShotPath = next;
                        break;
                    case "-autoquit":
                        autoQuit = true;
                        break;
                    case "-runinbackground":
                        Application.runInBackground = true;
                        break;
                    case "-perfprobe":
                        gameObject.AddComponent<PerfProbe>();
                        break;
                }
            }
            if (autoStartSeed != null) StartNewWorld(autoStartSeed);
        }

        void UpdateAutomation()
        {
            if (State != GameState.Playing || (autoShotPath == null && !autoQuit)) return;
            float dt = Time.unscaledDeltaTime;
            autoTimer += dt;
            autoFrames++;
            autoWorst = Mathf.Max(autoWorst, dt);
            if (autoTimer - autoLastLog >= 1f)
            {
                Debug.Log("[Mine] t=" + autoTimer.ToString("0.0") + "s fps=" + (autoFrames / (autoTimer - autoLastLog)).ToString("0") +
                          " worstFrame=" + (autoWorst * 1000f).ToString("0.0") + "ms meshed=" + World.MeshedChunks + " pending=" + World.PendingJobs +
                          " focused=" + Application.isFocused + " quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()] +
                          " gcMB=" + (System.GC.GetTotalMemory(false) / 1048576));
                autoLastLog = autoTimer;
                autoFrames = 0;
                autoWorst = 0f;
            }
            if (autoShotPath != null && !autoShotTaken && autoTimer > 4f)
            {
                ScreenCapture.CaptureScreenshot(autoShotPath);
                autoShotTaken = true;
                Debug.Log("[Mine] Screenshot saved to " + autoShotPath);
            }
            if (autoQuit && autoTimer > 8f)
            {
                Debug.Log("[Mine] Auto quit. Chunks meshed: " + World.MeshedChunks + ", CPU threads: " + SystemInfo.processorCount);
                Application.Quit();
            }
        }

        int autoFrames;
        float autoWorst;
        float autoLastLog;

        void SetupCameraAndLighting()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                go.tag = "MainCamera";
                cam = go.GetComponent<Camera>();
            }
            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = FarClip();
            cam.fieldOfView = 72f;
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                data.renderShadows = false;
            }

            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional)
                {
                    sun = l;
                    break;
                }
            }
            if (sun == null)
            {
                var go = new GameObject("Sun");
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.shadows = LightShadows.None;
            RenderSettings.sun = sun;
            RenderSettings.skybox = MaterialLibrary.Sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
        }

        float FarClip()
        {
            return Mathf.Max(400f, RenderDistance * Chunk.Size + 200f);
        }

        // ------------------------------------------------------------------ state transitions

        void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void EnterMenu()
        {
            State = GameState.Menu;
            Time.timeScale = 1f;
            Player.InputEnabled = false;
            Player.PhysicsEnabled = false;
            Player.Teleport(new Vector3(0f, 90f, 0f), 0f, -15f);
            if (UI.InventoryScreen.IsOpen) UI.InventoryScreen.Close();
            UI.ShowHud(false);
            UI.ShowPause(false);
            UI.ShowLoading(false);
            UI.ShowMainMenu(true);
            SetCursor(false);
        }

        void EnterLoading()
        {
            State = GameState.Loading;
            Time.timeScale = 1f;
            loadingTime = 0f;
            if (UI.InventoryScreen.IsOpen) UI.InventoryScreen.Close();
            UI.ShowMainMenu(false);
            UI.ShowPause(false);
            UI.ShowHud(false);
            UI.ShowLoading(true);
            UI.SetLoading(0f, "Gerando mundo...");
            Player.PhysicsEnabled = false;
            Player.InputEnabled = false;
            SetCursor(false);
        }

        void EnterPlaying()
        {
            State = GameState.Playing;
            Time.timeScale = 1f;
            UI.ShowLoading(false);
            UI.ShowMainMenu(false);
            UI.ShowPause(false);
            UI.ShowHud(true);
            UI.RefreshHotbar();
            Player.PhysicsEnabled = true;
            Player.InputEnabled = true;
            Mobs.Active = true;
            SetCursor(true);
        }

        public void Pause()
        {
            if (State != GameState.Playing) return;
            State = GameState.Paused;
            Player.InputEnabled = false;
            Time.timeScale = 0f;
            UI.ShowPause(true);
            SetCursor(false);
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;
            State = GameState.Playing;
            Time.timeScale = 1f;
            UI.ShowPause(false);
            Player.InputEnabled = true;
            SetCursor(true);
        }

        public void OpenInventory(bool atTable)
        {
            if (State != GameState.Playing) return;
            State = GameState.Inventory;
            Player.InputEnabled = false;
            UI.InventoryScreen.Open(atTable);
            SetCursor(false);
        }

        public void CloseInventory()
        {
            if (State != GameState.Inventory) return;
            UI.InventoryScreen.Close();
            State = GameState.Playing;
            Player.InputEnabled = true;
            SetCursor(true);
        }

        // ------------------------------------------------------------------ world lifecycle

        void CreateWorld(int seed, Dictionary<long, byte[]> storedChunks)
        {
            DestroyWorld();
            var go = new GameObject("World");
            World = go.AddComponent<VoxelWorld>();
            World.Init(seed, storedChunks, RenderDistance, MaterialLibrary.ChunkMaterials);
            World.SetViewer(Player.transform);
            Player.SetWorld(World);
            DayNight.ViewDistance = RenderDistance * Chunk.Size;
            cam.farClipPlane = FarClip();
        }

        void DestroyWorld()
        {
            Mobs.Active = false;
            Mobs.Clear();
            if (World != null)
            {
                Destroy(World.gameObject);
                World = null;
            }
            Player.SetWorld(null);
        }

        public void StartNewWorld(string seedText)
        {
            int seed;
            string s = seedText != null ? seedText.Trim() : "";
            if (s.Length == 0) seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            else if (!int.TryParse(s, out seed)) seed = MathUtil.StableHash(s);

            CreateWorld(seed, null);
            Creative = false;
            GiveStarterKit();
            DayNight.Time01 = 0.02f;

            Vector2Int col = World.Generator.FindSpawnColumn();
            int h = World.Generator.Height(col.x, col.y);
            Player.Flying = false;
            Player.Teleport(new Vector3(col.x + 0.5f, h + 1f, col.y + 0.5f), UnityEngine.Random.Range(0f, 360f), 0f);
            snapToSurface = true;
            EnterLoading();
        }

        void GiveStarterKit()
        {
            Inventory.Clear();
            BlockId[] hotbarKit =
            {
                BlockId.Grass, BlockId.Dirt, BlockId.Stone, BlockId.Planks, BlockId.OakLog,
                BlockId.Glass, BlockId.Sand, BlockId.OakLeaves, BlockId.Lamp
            };
            for (int i = 0; i < hotbarKit.Length; i++)
                Inventory.Slots[i] = new ItemStack(hotbarKit[i], hotbarKit[i] == BlockId.Lamp ? 16 : Inventory.MaxStack);
            Inventory.Slots[9] = new ItemStack(BlockId.Cobblestone, 64);
            Inventory.Slots[10] = new ItemStack(BlockId.StoneBricks, 32);
            Inventory.Slots[11] = new ItemStack(BlockId.Bricks, 32);
            Inventory.Slots[12] = new ItemStack(BlockId.Wool, 16);
            Inventory.Slots[13] = new ItemStack(BlockId.CraftingTable, 1);
            Inventory.Slots[14] = new ItemStack(BlockId.FlowerRed, 8);
            Inventory.Slots[15] = new ItemStack(BlockId.FlowerYellow, 8);
            Inventory.Selected = 0;
            Inventory.NotifyChanged();
        }

        public void SaveWorld()
        {
            if (World == null || State == GameState.Menu || State == GameState.Loading) return;
            var data = new SaveData
            {
                Seed = World.Seed,
                TimeOfDay = DayNight.Time01,
                PlayerX = Player.Position.x,
                PlayerY = Player.Position.y,
                PlayerZ = Player.Position.z,
                Yaw = Player.Yaw,
                Pitch = Player.Pitch,
                Flying = Player.Flying,
                Creative = Creative,
                Selected = Inventory.Selected,
                SlotIds = new int[Inventory.Size],
                SlotCounts = new int[Inventory.Size]
            };
            for (int i = 0; i < Inventory.Size; i++)
            {
                data.SlotIds[i] = (int)Inventory.Slots[i].Id;
                data.SlotCounts[i] = Inventory.Slots[i].Count;
            }
            try
            {
                SaveSystem.Save(data, World.CollectModifiedChunks());
                UI.Toast("Mundo salvo!");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                UI.Toast("Erro ao salvar: " + e.Message);
            }
        }

        public void LoadWorld()
        {
            if (!SaveSystem.TryLoad(out SaveData data, out Dictionary<long, byte[]> chunks, out string error))
            {
                UI.Toast("Falha ao carregar: " + error);
                return;
            }
            CreateWorld(data.Seed, chunks);
            Inventory.Clear();
            if (data.SlotIds != null && data.SlotCounts != null)
            {
                for (int i = 0; i < Inventory.Size && i < data.SlotIds.Length && i < data.SlotCounts.Length; i++)
                {
                    int id = data.SlotIds[i];
                    if (id > 0 && id < Blocks.Count && data.SlotCounts[i] > 0)
                        Inventory.Slots[i] = new ItemStack((BlockId)id, Mathf.Min(data.SlotCounts[i], Inventory.MaxStack));
                }
            }
            Inventory.Selected = data.Selected;
            Inventory.NotifyChanged();
            Creative = data.Creative;
            Player.Flying = data.Flying && Creative;
            DayNight.Time01 = Mathf.Repeat(data.TimeOfDay, 1f);
            Player.Teleport(new Vector3(data.PlayerX, data.PlayerY, data.PlayerZ), data.Yaw, data.Pitch);
            snapToSurface = false;
            EnterLoading();
            UI.Toast("Mundo carregado (seed " + data.Seed + ")");
        }

        public void SaveAndQuitToMenu()
        {
            Time.timeScale = 1f;
            SaveWorld();
            DestroyWorld();
            EnterMenu();
        }

        public void QuitGame()
        {
            if (World != null && State != GameState.Menu && State != GameState.Loading) SaveWorld();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ settings

        public void CycleRenderDistance()
        {
            int idx = Array.IndexOf(RenderDistances, RenderDistance);
            RenderDistance = RenderDistances[(idx + 1) % RenderDistances.Length];
            PlayerPrefs.SetInt("mine.renderDistance", RenderDistance);
            World?.SetRenderDistance(RenderDistance);
            DayNight.ViewDistance = RenderDistance * Chunk.Size;
            cam.farClipPlane = FarClip();
        }

        public void CycleSensitivity()
        {
            int idx = Array.IndexOf(Sensitivities, Sensitivity);
            Sensitivity = Sensitivities[(idx + 1) % Sensitivities.Length];
            Player.Sensitivity = Sensitivity;
            PlayerPrefs.SetFloat("mine.sensitivity", Sensitivity);
        }

        public void ToggleCreative()
        {
            Creative = !Creative;
            if (!Creative) Player.Flying = false;
            UI.RefreshHotbar();
            UI.Toast(Creative ? "Modo criativo: blocos infinitos, quebra instantânea, F para voar" : "Modo sobrevivência");
        }

        public void CycleCamera()
        {
            CameraModes.Cycle();
            UI.Toast("Câmera: " + CameraModes.ModeName + "  (V)");
        }

        public void ToggleMusic()
        {
            MusicOn = !MusicOn;
            audioManager.MusicVolume = MusicOn ? 0.55f : 0f;
            PlayerPrefs.SetInt("mine.music", MusicOn ? 1 : 0);
        }

        // ------------------------------------------------------------------ per-frame

        void Update()
        {
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;

            switch (State)
            {
                case GameState.Loading:
                    UpdateLoading();
                    break;
                case GameState.Playing:
                    HandlePlayingInput(kb, mouse);
                    break;
                case GameState.Paused:
                    if (kb != null && kb.escapeKey.wasPressedThisFrame) Resume();
                    break;
                case GameState.Inventory:
                    if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) CloseInventory();
                    break;
            }
            UpdateAutomation();
        }

        void UpdateLoading()
        {
            if (World == null) return;
            loadingTime += Time.unscaledDeltaTime;
            float progress = World.AreaProgress(Player.Position, 3);
            UI.SetLoading(progress, "Gerando mundo... " + Mathf.RoundToInt(progress * 100f) + "%");
            if (loadingTime < 0.25f || !World.IsAreaReady(Player.Position, 3)) return;

            int x = MathUtil.FastFloor(Player.Position.x), z = MathUtil.FastFloor(Player.Position.z);
            if (snapToSurface)
            {
                int top = World.GetTopBlockY(x, z, true);
                if (top >= 0) Player.Position.y = top + 1f;
                snapToSurface = false;
            }
            // Never start inside blocks (e.g. something was built where the save put the player).
            for (int i = 0; i < 128; i++)
            {
                Player.GetBounds(out Vector3 min, out Vector3 max);
                if (!VoxelPhysics.BoxOverlapsSolid(World, min, max)) break;
                Player.Position.y += 1f;
            }
            Player.Teleport(Player.Position, Player.Yaw, Player.Pitch);
            EnterPlaying();
        }

        void HandlePlayingInput(Keyboard kb, Mouse mouse)
        {
            if (kb == null) return;

            if (kb.escapeKey.wasPressedThisFrame)
            {
                Pause();
                return;
            }
            if (Cursor.lockState != CursorLockMode.Locked && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                SetCursor(true);
            }
            if (kb.eKey.wasPressedThisFrame)
            {
                OpenInventory(false);
                return;
            }

            for (int i = 0; i < Inventory.HotbarSize; i++)
            {
                if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) Inventory.Selected = i;
            }
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll > 0.01f) Inventory.Selected -= 1;
                else if (scroll < -0.01f) Inventory.Selected += 1;
            }

            if (kb.f1Key.wasPressedThisFrame) UI.ToggleHelp();
            if (kb.f3Key.wasPressedThisFrame) UI.ToggleDebug();
            if (kb.f5Key.wasPressedThisFrame) SaveWorld();
            if (kb.f9Key.wasPressedThisFrame)
            {
                LoadWorld();
                return;
            }
            if (kb.gKey.wasPressedThisFrame) ToggleCreative();
            if (kb.vKey.wasPressedThisFrame) CycleCamera();
            if (kb.fKey.wasPressedThisFrame)
            {
                if (Creative)
                {
                    Player.Flying = !Player.Flying;
                    Player.Velocity.y = 0f;
                }
                else
                {
                    UI.Toast("Voar só no modo criativo (G)");
                }
            }
        }

        void LateUpdate()
        {
            bool inWorld = World != null && State != GameState.Menu && State != GameState.Loading;
            bool underwater = inWorld && CameraModes.CameraUnderwater(World);
            DayNight.Underwater = underwater;
            UI.SetUnderwater(underwater);
            // Like Minecraft, no crosshair when looking at the character's face.
            UI.SetCrosshairVisible(CameraModes.Mode != CameraMode.ThirdPersonFront);
            Keyboard kb = Keyboard.current;
            DayNight.FastForward = State == GameState.Playing && kb != null && kb.tKey.isPressed;

            audioManager.InWorld = inWorld;
            audioManager.Daylight = DayNight.Daylight;
            audioManager.Underwater = underwater;
            audioManager.Altitude = Player.Position.y;
        }

        void OnApplicationQuit()
        {
            Time.timeScale = 1f;
            // Autosave when the window is closed mid-game (Alt+F4, closing the editor play session...).
            if (World != null && (State == GameState.Playing || State == GameState.Paused || State == GameState.Inventory))
            {
                if (State == GameState.Inventory) UI.InventoryScreen.Close();
                SaveWorld();
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }
    }
}
