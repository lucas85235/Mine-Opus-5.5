using UnityEngine;
using UnityEngine.UI;

namespace Mine
{
    /// <summary>Builds and drives every screen: HUD (crosshair, hotbar, info), menus, loading and inventory.</summary>
    public sealed class UIManager : MonoBehaviour
    {
        GameManager gm;
        Canvas canvas;
        RectTransform root;

        // HUD
        RectTransform hud;
        readonly SlotView[] hotbar = new SlotView[Inventory.HotbarSize];
        RectTransform selectedFrame;
        Text itemName;
        float itemNameTimer;
        Text infoText;
        Text debugText;
        Text modeText;
        RectTransform helpPanel;
        Image underwater;
        Text toast;
        float toastTimer;
        bool debugVisible;
        float infoTimer;
        int lastSelected = -1;
        BlockId lastSelectedId = BlockId.Air;
        float fpsAccum;
        int fpsFrames;
        float fps;

        // Menus
        RectTransform mainMenu;
        RectTransform pauseMenu;
        RectTransform loading;
        InputField seedField;
        Button loadButton;
        Text saveInfo;
        Image loadingFill;
        Text loadingText;
        Button renderButton;
        Button sensButton;
        Button modeButton;
        Button cameraButton;
        Button musicButton;
        Image[] crosshair;

        public InventoryView InventoryScreen { get; private set; }
        public bool HelpVisible => helpPanel != null && helpPanel.gameObject.activeSelf;

        public void Build(GameManager manager)
        {
            gm = manager;
            UIFactory.EnsureEventSystem();
            canvas = UIFactory.Canvas("UI", 10);
            canvas.transform.SetParent(transform, false);
            root = (RectTransform)canvas.transform;

            BuildHud();
            BuildInventory();
            BuildPause();
            BuildLoading();
            BuildMainMenu();
            BuildToast();

            gm.Inventory.Changed += RefreshHotbar;
            RefreshHotbar();
        }

        // ------------------------------------------------------------------ HUD

        void BuildHud()
        {
            hud = UIFactory.Rect("HUD", root);
            UIFactory.Stretch(hud);

            underwater = UIFactory.Image(hud, "Underwater", TextureFactory.White, new Color(0.08f, 0.22f, 0.6f, 0.38f));
            UIFactory.Stretch(underwater.rectTransform);
            underwater.enabled = false;

            Image ch = UIFactory.Image(hud, "CrosshairH", TextureFactory.White, new Color(1f, 1f, 1f, 0.9f));
            UIFactory.Place(ch.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 4f));
            Image cv = UIFactory.Image(hud, "CrosshairV", TextureFactory.White, new Color(1f, 1f, 1f, 0.9f));
            UIFactory.Place(cv.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, 30f));
            foreach (Image img in new[] { ch, cv })
            {
                var o = img.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.6f);
                o.effectDistance = new Vector2(1.5f, -1.5f);
            }
            crosshair = new[] { ch, cv };

            const float slot = 80f;
            RectTransform bar = UIFactory.Rect("Hotbar", hud);
            UIFactory.Place(bar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(slot * 9f, slot));
            for (int i = 0; i < Inventory.HotbarSize; i++)
            {
                SlotView v = SlotView.Create(bar, "Slot" + i, TextureFactory.HotbarSlot, slot, 54f);
                v.Background.raycastTarget = false;
                UIFactory.Place(v.Root, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(slot * (i + 0.5f), 0f), new Vector2(slot, slot));
                Text num = UIFactory.Text(v.Root, "Key", (i + 1).ToString(), 16, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.55f));
                UIFactory.Stretch(num.rectTransform, 6f);
                hotbar[i] = v;
            }
            Image frame = UIFactory.Image(bar, "Selected", TextureFactory.SlotSelected, Color.white);
            selectedFrame = frame.rectTransform;
            UIFactory.Place(selectedFrame, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(slot * 0.5f, 0f), new Vector2(slot + 12f, slot + 12f));

            itemName = UIFactory.Text(hud, "ItemName", "", 28, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Place(itemName.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 112f), new Vector2(800f, 40f));

            infoText = UIFactory.Text(hud, "Info", "", 20, TextAnchor.UpperLeft, Color.white);
            UIFactory.Place(infoText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -10f), new Vector2(900f, 400f));

            modeText = UIFactory.Text(hud, "Mode", "", 22, TextAnchor.UpperRight, Color.white);
            UIFactory.Place(modeText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -10f), new Vector2(600f, 80f));

            debugText = infoText;

            Image helpBg = UIFactory.Image(hud, "Help", TextureFactory.White, new Color(0f, 0f, 0f, 0.6f));
            helpPanel = helpBg.rectTransform;
            UIFactory.Place(helpPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(980f, 600f));
            Text helpText = UIFactory.Text(helpPanel, "Text", HelpText, 24, TextAnchor.UpperLeft, Color.white);
            UIFactory.Stretch(helpText.rectTransform, 24f);
            helpPanel.gameObject.SetActive(false);
        }

        const string HelpText =
            "<b><size=30>CONTROLES</size></b>\n\n" +
            "<b>W A S D</b>  mover        <b>Mouse</b>  olhar\n" +
            "<b>Espaço</b>  pular / nadar / subir voando\n" +
            "<b>Shift</b>  agachar (não cai das bordas) / descer voando\n" +
            "<b>Ctrl</b> ou <b>W duas vezes</b>  correr\n" +
            "<b>Botão esquerdo</b>  quebrar bloco / atacar animal\n" +
            "<b>Botão direito</b>  colocar bloco / abrir bancada\n" +
            "<b>Botão do meio</b>  selecionar o bloco mirado\n" +
            "<b>1 - 9</b> / <b>Scroll</b>  escolher bloco da barra\n" +
            "<b>E</b>  inventário e criação (crafting)\n" +
            "<b>G</b>  alternar modo Criativo / Sobrevivência\n" +
            "<b>V</b>  câmera: 1ª pessoa / 3ª pessoa (costas) / 3ª pessoa (frente)\n" +
            "<b>F</b> ou <b>Espaço duas vezes</b>  voar (modo criativo)\n" +
            "<b>T</b> (segurar)  acelerar o tempo (dia / noite)\n" +
            "<b>F3</b>  informações de debug      <b>F1</b>  esta ajuda\n" +
            "<b>F5</b>  salvar      <b>F9</b>  carregar      <b>Esc</b>  pausa / menu";

        public void ToggleHelp()
        {
            helpPanel.gameObject.SetActive(!helpPanel.gameObject.activeSelf);
        }

        public void ToggleDebug()
        {
            debugVisible = !debugVisible;
            infoTimer = 0f;
        }

        public void ShowHud(bool visible)
        {
            hud.gameObject.SetActive(visible);
        }

        public void RefreshHotbar()
        {
            Inventory inv = gm.Inventory;
            for (int i = 0; i < Inventory.HotbarSize; i++) hotbar[i].Set(inv.Slots[i], !gm.Creative);
            selectedFrame.anchoredPosition = new Vector2(80f * (inv.Selected + 0.5f), 0f);
            ItemStack sel = inv.SelectedStack;
            BlockId id = sel.IsEmpty ? BlockId.Air : sel.Id;
            if (inv.Selected != lastSelected || id != lastSelectedId)
            {
                lastSelected = inv.Selected;
                lastSelectedId = id;
                if (id != BlockId.Air)
                {
                    itemName.text = Blocks.NameOf(id);
                    itemNameTimer = 2.2f;
                }
            }
        }

        public void SetCrosshairVisible(bool value)
        {
            if (crosshair == null || crosshair[0].enabled == value) return;
            for (int i = 0; i < crosshair.Length; i++) crosshair[i].enabled = value;
        }

        public void SetUnderwater(bool value)
        {
            if (underwater.enabled != value) underwater.enabled = value;
        }

        void UpdateHud()
        {
            itemNameTimer -= Time.unscaledDeltaTime;
            Color c = itemName.color;
            c.a = Mathf.Clamp01(itemNameTimer);
            itemName.color = c;

            fpsAccum += Time.unscaledDeltaTime;
            fpsFrames++;
            if (fpsAccum >= 0.5f)
            {
                fps = fpsFrames / fpsAccum;
                fpsAccum = 0f;
                fpsFrames = 0;
            }

            infoTimer -= Time.unscaledDeltaTime;
            if (infoTimer > 0f) return;
            infoTimer = 0.2f;

            string mode = gm.Creative ? "Criativo" : "Sobrevivência";
            if (gm.Player != null && gm.Player.Flying) mode += " (voando)";
            modeText.text = mode + "\n" + (gm.DayNight != null ? gm.DayNight.ClockText : "");

            if (!debugVisible)
            {
                infoText.text = Mathf.RoundToInt(fps) + " FPS\n<size=18>F1: ajuda   F3: debug</size>";
                return;
            }
            VoxelWorld w = gm.World;
            PlayerController p = gm.Player;
            if (w == null || p == null)
            {
                infoText.text = Mathf.RoundToInt(fps) + " FPS";
                return;
            }
            Vector3 pos = p.Position;
            int bx = MathUtil.FastFloor(pos.x), by = MathUtil.FastFloor(pos.y), bz = MathUtil.FastFloor(pos.z);
            int light = w.GetPackedLight(bx, MathUtil.FastFloor(pos.y + 1f), bz);
            string facing = FacingName(p.Yaw);
            string target = gm.Interaction != null && gm.Interaction.HasTarget
                ? Blocks.NameOf(gm.Interaction.Target.Id) + " @ " + gm.Interaction.Target.Block
                : "-";
            infoText.text =
                "<b>Mine</b> - " + Mathf.RoundToInt(fps) + " FPS\n" +
                "XYZ: " + pos.x.ToString("0.0") + " / " + pos.y.ToString("0.0") + " / " + pos.z.ToString("0.0") + "\n" +
                "Bloco: " + bx + " " + by + " " + bz + "   Chunk: " + (bx >> 4) + " " + (bz >> 4) + "\n" +
                "Olhando: " + facing + " (" + p.Yaw.ToString("0") + " / " + p.Pitch.ToString("0") + ")\n" +
                "Bioma: " + w.Generator.BiomeAt(bx, bz) + "   Luz céu/bloco: " + (light >> 4) + "/" + (light & 15) + "\n" +
                "Seed: " + w.Seed + "\n" +
                "Chunks: " + w.LoadedChunks + " carregados, " + w.MeshedChunks + " com malha, fila " + w.PendingJobs + " (" + w.WorkerThreads + " threads)\n" +
                "Distância: " + w.RenderDistance + " chunks   Mobs: " + (MobManager.Instance != null ? MobManager.Instance.Count : 0) + "\n" +
                "Hora: " + gm.DayNight.ClockText + "\n" +
                "Alvo: " + target;
        }

        static string FacingName(float yaw)
        {
            string[] names = { "Norte (+Z)", "Leste (+X)", "Sul (-Z)", "Oeste (-X)" };
            int i = Mathf.RoundToInt(Mathf.Repeat(yaw, 360f) / 90f) % 4;
            return names[i];
        }

        // ------------------------------------------------------------------ inventory

        void BuildInventory()
        {
            RectTransform rt = UIFactory.Rect("Inventory", root);
            InventoryScreen = rt.gameObject.AddComponent<InventoryView>();
            InventoryScreen.Build(gm, root);
        }

        // ------------------------------------------------------------------ pause

        void BuildPause()
        {
            pauseMenu = UIFactory.Rect("Pause", root);
            UIFactory.Stretch(pauseMenu);
            Image dim = UIFactory.Image(pauseMenu, "Dim", TextureFactory.White, new Color(0f, 0f, 0f, 0.6f), true);
            UIFactory.Stretch(dim.rectTransform);

            Text title = UIFactory.Text(pauseMenu, "Title", "Jogo pausado", 48, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), new Vector2(800f, 70f));

            var size = new Vector2(560f, 58f);
            float y = 240f;
            AddButton(pauseMenu, "Voltar ao jogo", size, ref y, () => gm.Resume());
            AddButton(pauseMenu, "Salvar mundo (F5)", size, ref y, () => gm.SaveWorld());
            renderButton = AddButton(pauseMenu, "", size, ref y, () =>
            {
                gm.CycleRenderDistance();
                RefreshPauseLabels();
            });
            sensButton = AddButton(pauseMenu, "", size, ref y, () =>
            {
                gm.CycleSensitivity();
                RefreshPauseLabels();
            });
            modeButton = AddButton(pauseMenu, "", size, ref y, () =>
            {
                gm.ToggleCreative();
                RefreshPauseLabels();
            });
            cameraButton = AddButton(pauseMenu, "", size, ref y, () =>
            {
                gm.CycleCamera();
                RefreshPauseLabels();
            });
            musicButton = AddButton(pauseMenu, "", size, ref y, () =>
            {
                gm.ToggleMusic();
                RefreshPauseLabels();
            });
            AddButton(pauseMenu, "Salvar e voltar ao menu", size, ref y, () => gm.SaveAndQuitToMenu());
            AddButton(pauseMenu, "Sair do jogo", size, ref y, () => gm.QuitGame());
            pauseMenu.gameObject.SetActive(false);
        }

        Button AddButton(RectTransform parent, string label, Vector2 size, ref float y, System.Action action)
        {
            Button b = UIFactory.Button(parent, label, size, action);
            UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), size);
            y -= size.y + 12f;
            return b;
        }

        void RefreshPauseLabels()
        {
            UIFactory.SetButtonLabel(renderButton, "Distância de visão: " + gm.RenderDistance + " chunks");
            UIFactory.SetButtonLabel(sensButton, "Sensibilidade do mouse: " + gm.Sensitivity.ToString("0.00"));
            UIFactory.SetButtonLabel(modeButton, "Modo: " + (gm.Creative ? "Criativo" : "Sobrevivência") + " (G)");
            UIFactory.SetButtonLabel(cameraButton, "Câmera: " + gm.CameraModes.ModeName + " (V)");
            UIFactory.SetButtonLabel(musicButton, "Música: " + (gm.MusicOn ? "ligada" : "desligada"));
        }

        public void ShowPause(bool visible)
        {
            if (visible) RefreshPauseLabels();
            pauseMenu.gameObject.SetActive(visible);
            if (visible) pauseMenu.SetAsLastSibling();
        }

        // ------------------------------------------------------------------ main menu & loading

        RectTransform DirtScreen(string name)
        {
            RectTransform rt = UIFactory.Rect(name, root);
            UIFactory.Stretch(rt);
            Image bg = UIFactory.Image(rt, "Background", TextureFactory.DirtBackground, Color.white, true);
            bg.type = Image.Type.Tiled;
            UIFactory.Stretch(bg.rectTransform);
            return rt;
        }

        void BuildMainMenu()
        {
            mainMenu = DirtScreen("MainMenu");

            Text title = UIFactory.Text(mainMenu, "Title", "MINE", 150, TextAnchor.MiddleCenter, new Color(0.86f, 0.86f, 0.86f));
            title.fontStyle = FontStyle.Bold;
            var s = title.GetComponent<Shadow>();
            s.effectDistance = new Vector2(7f, -7f);
            s.effectColor = new Color(0.15f, 0.15f, 0.15f, 1f);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(1200f, 180f));

            Text subtitle = UIFactory.Text(mainMenu, "Subtitle", "um mundo de blocos feito do zero", 30, TextAnchor.MiddleCenter, new Color(1f, 1f, 0.35f));
            UIFactory.Place(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(1200f, 50f));

            Text seedLabel = UIFactory.Text(mainMenu, "SeedLabel", "Seed do mundo (vazio = aleatória)", 24, TextAnchor.MiddleLeft, new Color(0.8f, 0.8f, 0.8f));
            UIFactory.Place(seedLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(600f, 34f));
            seedField = UIFactory.InputField(mainMenu, "Seed...", new Vector2(600f, 56f));
            UIFactory.Place((RectTransform)seedField.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 72f), new Vector2(600f, 56f));

            var size = new Vector2(600f, 62f);
            float y = -10f;
            AddButton(mainMenu, "Novo mundo", size, ref y, () => gm.StartNewWorld(seedField.text));
            loadButton = AddButton(mainMenu, "Carregar mundo salvo", size, ref y, () => gm.LoadWorld());
            saveInfo = UIFactory.Text(mainMenu, "SaveInfo", "", 20, TextAnchor.MiddleCenter, new Color(0.75f, 0.75f, 0.75f));
            UIFactory.Place(saveInfo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y + 18f), new Vector2(800f, 30f));
            y -= 30f;
            AddButton(mainMenu, "Sair", size, ref y, () => gm.QuitGame());

            Text footer = UIFactory.Text(mainMenu, "Footer", "Texturas, sons e música gerados por código  -  Sardinha Studios", 20, TextAnchor.LowerCenter, new Color(0.7f, 0.7f, 0.7f));
            UIFactory.Place(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(1400f, 30f));
        }

        void BuildLoading()
        {
            loading = DirtScreen("Loading");
            loadingText = UIFactory.Text(loading, "Text", "Gerando mundo...", 36, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Place(loadingText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(1200f, 60f));
            Image barBg = UIFactory.Image(loading, "Bar", TextureFactory.White, new Color(0.25f, 0.25f, 0.25f, 1f));
            UIFactory.Place(barBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(600f, 14f));
            loadingFill = UIFactory.Image(barBg.rectTransform, "Fill", TextureFactory.White, new Color(0.45f, 0.85f, 0.35f, 1f));
            RectTransform fr = loadingFill.rectTransform;
            fr.anchorMin = new Vector2(0f, 0f);
            fr.anchorMax = new Vector2(0f, 1f);
            fr.pivot = new Vector2(0f, 0.5f);
            fr.offsetMin = Vector2.zero;
            fr.offsetMax = Vector2.zero;
            loading.gameObject.SetActive(false);
        }

        public void ShowMainMenu(bool visible)
        {
            mainMenu.gameObject.SetActive(visible);
            if (!visible) return;
            mainMenu.SetAsLastSibling();
            bool has = SaveSystem.HasSave();
            loadButton.interactable = has;
            saveInfo.text = has ? SaveSystem.DescribeSave() : "Nenhum mundo salvo ainda";
        }

        public void ShowLoading(bool visible)
        {
            loading.gameObject.SetActive(visible);
            if (visible) loading.SetAsLastSibling();
        }

        public void SetLoading(float progress, string text)
        {
            loadingText.text = text;
            loadingFill.rectTransform.sizeDelta = new Vector2(600f * Mathf.Clamp01(progress), 0f);
        }

        // ------------------------------------------------------------------ toast

        void BuildToast()
        {
            toast = UIFactory.Text(root, "Toast", "", 30, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Place(toast.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1200f, 50f));
        }

        public void Toast(string message)
        {
            toast.text = message;
            toastTimer = 2.5f;
            toast.transform.SetAsLastSibling();
        }

        void Update()
        {
            if (gm == null) return;
            toastTimer -= Time.unscaledDeltaTime;
            Color c = toast.color;
            c.a = Mathf.Clamp01(toastTimer);
            toast.color = c;
            if (hud.gameObject.activeSelf) UpdateHud();
        }
    }
}
