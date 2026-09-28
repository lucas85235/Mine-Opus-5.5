using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Mine
{
    /// <summary>
    /// Inventory screen (E): 27 + 9 slots with click/right-click/shift-click stack handling, a crafting list
    /// (survival) or a block palette (creative). Opening it from a crafting table unlocks table recipes.
    /// </summary>
    public sealed class InventoryView : MonoBehaviour
    {
        const float SlotSize = 64f;
        const float SlotStep = 70f;

        GameManager gm;
        Inventory inv;
        RectTransform root;
        RectTransform panel;
        Text title;
        Text sideTitle;
        readonly SlotView[] slots = new SlotView[Inventory.Size];
        ItemStack cursor;
        SlotView cursorView;
        Text tooltip;
        RectTransform tooltipRect;
        int hoveredSlot = -1;
        BlockId hoveredPalette = BlockId.Air;
        bool atTable;

        RectTransform recipesRoot;
        RectTransform paletteRoot;
        readonly List<Recipe> recipeList = new List<Recipe>();
        readonly List<Button> recipeButtons = new List<Button>();
        readonly List<Text> recipeLabels = new List<Text>();

        public bool IsOpen => gameObject.activeSelf;

        public void Build(GameManager manager, RectTransform canvasRoot)
        {
            gm = manager;
            inv = manager.Inventory;
            root = canvasRoot;

            RectTransform self = (RectTransform)transform;
            UIFactory.Stretch(self);

            Image dim = UIFactory.Image(self, "Dim", TextureFactory.White, new Color(0f, 0f, 0f, 0.55f), true);
            UIFactory.Stretch(dim.rectTransform);

            Image panelImg = UIFactory.Image(self, "Panel", TextureFactory.PanelSprite, Color.white, true);
            panel = panelImg.rectTransform;
            UIFactory.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 680f));

            title = UIFactory.Text(panel, "Title", "Inventário", 30, TextAnchor.UpperLeft, new Color(0.25f, 0.25f, 0.25f), false);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(-570f, 315f), new Vector2(560f, 40f));

            float x0 = -570f, y0 = 255f;
            for (int r = 0; r < 3; r++)
            for (int c = 0; c < 9; c++)
            {
                int index = 9 + r * 9 + c;
                CreateSlot(index, new Vector2(x0 + c * SlotStep + SlotSize * 0.5f, y0 - r * SlotStep - SlotSize * 0.5f));
            }
            Text hotbarLabel = UIFactory.Text(panel, "HotbarLabel", "Barra rápida", 22, TextAnchor.UpperLeft, new Color(0.3f, 0.3f, 0.3f), false);
            UIFactory.Place(hotbarLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(x0, y0 - 3 * SlotStep - 6f), new Vector2(400f, 30f));
            for (int c = 0; c < 9; c++)
            {
                CreateSlot(c, new Vector2(x0 + c * SlotStep + SlotSize * 0.5f, y0 - 3 * SlotStep - 40f - SlotSize * 0.5f));
            }

            Text help = UIFactory.Text(panel, "Help",
                "Clique: pegar/soltar pilha   Botão direito: metade / um   Shift+clique: mover rápido",
                18, TextAnchor.UpperLeft, new Color(0.32f, 0.32f, 0.32f), false);
            UIFactory.Place(help.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(x0, -250f), new Vector2(640f, 60f));

            sideTitle = UIFactory.Text(panel, "SideTitle", "Criação", 30, TextAnchor.UpperLeft, new Color(0.25f, 0.25f, 0.25f), false);
            UIFactory.Place(sideTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(90f, 315f), new Vector2(480f, 40f));

            recipesRoot = UIFactory.Rect("Recipes", panel);
            UIFactory.Place(recipesRoot, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(90f, 262f), new Vector2(480f, 560f));
            BuildRecipes();

            paletteRoot = UIFactory.Rect("Palette", panel);
            UIFactory.Place(paletteRoot, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(90f, 262f), new Vector2(480f, 560f));
            BuildPalette();

            cursorView = SlotView.Create(self, "Cursor", null, SlotSize, 52f);
            cursorView.Background.color = new Color(0, 0, 0, 0);
            cursorView.Background.raycastTarget = false;
            cursorView.Root.pivot = new Vector2(0.5f, 0.5f);

            tooltip = UIFactory.Text(self, "Tooltip", "", 22, TextAnchor.MiddleLeft, Color.white);
            tooltip.horizontalOverflow = HorizontalWrapMode.Overflow;
            tooltipRect = tooltip.rectTransform;
            tooltipRect.pivot = new Vector2(0f, 0f);
            tooltipRect.sizeDelta = new Vector2(400f, 30f);

            inv.Changed += () =>
            {
                if (IsOpen) Refresh();
            };
            gameObject.SetActive(false);
        }

        void CreateSlot(int index, Vector2 pos)
        {
            SlotView v = SlotView.Create(panel, "Slot" + index, TextureFactory.InventorySlot, SlotSize, 50f);
            UIFactory.Place(v.Root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(SlotSize, SlotSize));
            var h = v.Root.gameObject.AddComponent<SlotHandler>();
            h.Index = index;
            h.Clicked = OnSlotClicked;
            h.Entered = i => hoveredSlot = i;
            h.Exited = i =>
            {
                if (hoveredSlot == i) hoveredSlot = -1;
            };
            slots[index] = v;
        }

        void BuildRecipes()
        {
            float y = 0f;
            foreach (Recipe r in Recipes.All)
            {
                Recipe recipe = r;
                Button b = UIFactory.Button(recipesRoot, "", new Vector2(480f, 46f), () => Craft(recipe), 20);
                var rt = (RectTransform)b.transform;
                UIFactory.Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(480f, 46f));
                Image icon = UIFactory.Image(rt, "Icon", TextureFactory.Icons[(int)r.Result], Color.white);
                UIFactory.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(36f, 36f));
                Text label = b.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.fontSize = 18;
                label.rectTransform.offsetMin = new Vector2(54f, 0f);
                label.rectTransform.offsetMax = new Vector2(-6f, 0f);
                recipeList.Add(r);
                recipeButtons.Add(b);
                recipeLabels.Add(label);
                y -= 50f;
            }
        }

        void BuildPalette()
        {
            int i = 0;
            for (int b = 1; b < Blocks.Count; b++)
            {
                BlockDef def = Blocks.Defs[b];
                if (!def.Obtainable) continue;
                int col = i % 7, row = i / 7;
                SlotView v = SlotView.Create(paletteRoot, "Palette" + def.Id, TextureFactory.InventorySlot, 60f, 46f);
                UIFactory.Place(v.Root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(col * 66f, -row * 66f), new Vector2(60f, 60f));
                v.Set(new ItemStack(def.Id, 1), false);
                var h = v.Root.gameObject.AddComponent<SlotHandler>();
                BlockId id = def.Id;
                h.Index = b;
                h.Clicked = (_, button) => OnPaletteClicked(id, button);
                h.Entered = _ => hoveredPalette = id;
                h.Exited = _ =>
                {
                    if (hoveredPalette == id) hoveredPalette = BlockId.Air;
                };
                i++;
            }
        }

        public void Open(bool fromTable)
        {
            atTable = fromTable;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            hoveredSlot = -1;
            hoveredPalette = BlockId.Air;
            title.text = fromTable ? "Bancada de Trabalho" : "Inventário";
            bool creative = gm.Creative;
            recipesRoot.gameObject.SetActive(!creative);
            paletteRoot.gameObject.SetActive(creative);
            sideTitle.text = creative ? "Blocos (criativo)" : (fromTable ? "Criação (bancada)" : "Criação");
            Refresh();
        }

        public void Close()
        {
            if (!cursor.IsEmpty)
            {
                inv.Add(cursor.Id, cursor.Count);
                cursor = ItemStack.Empty;
            }
            gameObject.SetActive(false);
        }

        void Refresh()
        {
            for (int i = 0; i < Inventory.Size; i++) slots[i].Set(inv.Slots[i], true);
            for (int i = 0; i < recipeList.Count; i++)
            {
                Recipe r = recipeList[i];
                bool can = Recipes.CanCraft(r, inv, atTable);
                recipeButtons[i].interactable = can;
                string need = r.NeedsTable && !atTable ? "  <color=#ff9a9a>(requer bancada)</color>" : "";
                recipeLabels[i].text = r.ResultCount + "x " + Blocks.NameOf(r.Result) + "  <color=#d8d8d8>= " + r.IngredientText + "</color>" + need;
                recipeLabels[i].color = can ? new Color(1f, 1f, 0.75f) : new Color(0.7f, 0.7f, 0.7f);
            }
            cursorView.Set(cursor, true);
        }

        void Craft(Recipe r)
        {
            bool shift = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
            int times = shift ? 64 : 1;
            bool any = false;
            for (int i = 0; i < times; i++)
            {
                if (!Recipes.Craft(r, inv, atTable)) break;
                any = true;
            }
            if (any) AudioManager.Instance?.PlayPop();
            Refresh();
        }

        void OnPaletteClicked(BlockId id, PointerEventData.InputButton button)
        {
            if (!cursor.IsEmpty && cursor.Id != id)
            {
                cursor = ItemStack.Empty;
            }
            else if (cursor.IsEmpty)
            {
                cursor = new ItemStack(id, button == PointerEventData.InputButton.Right ? 1 : Inventory.MaxStack);
            }
            else
            {
                cursor.Count = Mathf.Min(Inventory.MaxStack, cursor.Count + (button == PointerEventData.InputButton.Right ? 1 : Inventory.MaxStack));
            }
            AudioManager.Instance?.PlayClick();
            Refresh();
        }

        void OnSlotClicked(int i, PointerEventData.InputButton button)
        {
            bool shift = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
            ItemStack slot = inv.Slots[i];

            if (button == PointerEventData.InputButton.Left)
            {
                if (shift && cursor.IsEmpty && !slot.IsEmpty)
                {
                    QuickMove(i);
                    return;
                }
                if (cursor.IsEmpty)
                {
                    cursor = slot;
                    slot = ItemStack.Empty;
                }
                else if (slot.IsEmpty)
                {
                    slot = cursor;
                    cursor = ItemStack.Empty;
                }
                else if (slot.Id == cursor.Id)
                {
                    int move = Mathf.Min(Inventory.MaxStack - slot.Count, cursor.Count);
                    slot.Count += move;
                    cursor.Count -= move;
                    if (cursor.Count <= 0) cursor = ItemStack.Empty;
                }
                else
                {
                    ItemStack t = slot;
                    slot = cursor;
                    cursor = t;
                }
            }
            else if (button == PointerEventData.InputButton.Right)
            {
                if (cursor.IsEmpty)
                {
                    if (!slot.IsEmpty)
                    {
                        int half = (slot.Count + 1) / 2;
                        cursor = new ItemStack(slot.Id, half);
                        slot.Count -= half;
                        if (slot.Count <= 0) slot = ItemStack.Empty;
                    }
                }
                else if (slot.IsEmpty || (slot.Id == cursor.Id && slot.Count < Inventory.MaxStack))
                {
                    slot = new ItemStack(cursor.Id, (slot.IsEmpty ? 0 : slot.Count) + 1);
                    cursor.Count--;
                    if (cursor.Count <= 0) cursor = ItemStack.Empty;
                }
                else
                {
                    ItemStack t = slot;
                    slot = cursor;
                    cursor = t;
                }
            }
            else
            {
                return;
            }

            inv.Slots[i] = slot;
            AudioManager.Instance?.PlayClick();
            inv.NotifyChanged();
            Refresh();
        }

        void QuickMove(int i)
        {
            ItemStack s = inv.Slots[i];
            int from = i < Inventory.HotbarSize ? Inventory.HotbarSize : 0;
            int to = i < Inventory.HotbarSize ? Inventory.Size : Inventory.HotbarSize;
            for (int k = from; k < to && s.Count > 0; k++)
            {
                if (inv.Slots[k].Id == s.Id && inv.Slots[k].Count > 0 && inv.Slots[k].Count < Inventory.MaxStack)
                {
                    int move = Mathf.Min(Inventory.MaxStack - inv.Slots[k].Count, s.Count);
                    inv.Slots[k].Count += move;
                    s.Count -= move;
                }
            }
            for (int k = from; k < to && s.Count > 0; k++)
            {
                if (inv.Slots[k].IsEmpty)
                {
                    inv.Slots[k] = s;
                    s.Count = 0;
                }
            }
            inv.Slots[i] = s.Count > 0 ? s : ItemStack.Empty;
            AudioManager.Instance?.PlayClick();
            inv.NotifyChanged();
            Refresh();
        }

        void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 screen = mouse.position.ReadValue();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local);

            cursorView.Root.anchorMin = cursorView.Root.anchorMax = new Vector2(0.5f, 0.5f);
            cursorView.Root.anchoredPosition = local;
            cursorView.Root.SetAsLastSibling();

            string tip = null;
            if (cursor.IsEmpty)
            {
                if (hoveredSlot >= 0 && !inv.Slots[hoveredSlot].IsEmpty) tip = Blocks.NameOf(inv.Slots[hoveredSlot].Id);
                else if (hoveredPalette != BlockId.Air) tip = Blocks.NameOf(hoveredPalette);
            }
            tooltip.gameObject.SetActive(tip != null);
            if (tip != null)
            {
                tooltip.text = tip;
                tooltipRect.anchorMin = tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
                tooltipRect.anchoredPosition = local + new Vector2(18f, 12f);
                tooltipRect.SetAsLastSibling();
            }
        }
    }
}
