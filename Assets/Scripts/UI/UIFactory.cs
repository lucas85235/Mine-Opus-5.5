using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mine
{
    /// <summary>Helpers to build uGUI hierarchies from code (no prefabs or imported assets).</summary>
    public static class UIFactory
    {
        static Font font;

        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            RectTransform rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static Text Text(Transform parent, string name, string content, int size, TextAnchor anchor, Color color, bool shadow = true)
        {
            RectTransform rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = content;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            if (shadow)
            {
                var s = rt.gameObject.AddComponent<Shadow>();
                s.effectColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
                s.effectDistance = new Vector2(2f, -2f);
            }
            return t;
        }

        public static Button Button(Transform parent, string label, Vector2 size, Action onClick, int fontSize = 26)
        {
            Image bg = Image(parent, "Button " + label, TextureFactory.ButtonSprite, Color.white, true);
            bg.rectTransform.sizeDelta = size;
            var button = bg.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.72f, 0.8f, 1.15f, 1f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.6f, 0.6f, 0.65f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            var nav = new Navigation { mode = Navigation.Mode.None };
            button.navigation = nav;

            Text t = Text(bg.transform, "Label", label, fontSize, TextAnchor.MiddleCenter, new Color(0.94f, 0.94f, 0.94f));
            Stretch(t.rectTransform);
            button.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                onClick?.Invoke();
            });
            return button;
        }

        public static void SetButtonLabel(Button b, string label)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = label;
        }

        public static InputField InputField(Transform parent, string placeholder, Vector2 size)
        {
            Image bg = Image(parent, "InputField", TextureFactory.White, new Color(0f, 0f, 0f, 0.85f), true);
            bg.rectTransform.sizeDelta = size;
            Image border = Image(bg.transform, "Border", TextureFactory.SlotSelected, new Color(0.63f, 0.63f, 0.63f, 1f));
            Stretch(border.rectTransform, -2f);

            var field = bg.gameObject.AddComponent<InputField>();
            Text ph = Text(bg.transform, "Placeholder", placeholder, 24, TextAnchor.MiddleLeft, new Color(0.55f, 0.55f, 0.55f), false);
            ph.fontStyle = FontStyle.Italic;
            Stretch(ph.rectTransform, 12f);
            Text txt = Text(bg.transform, "Text", "", 24, TextAnchor.MiddleLeft, Color.white, false);
            Stretch(txt.rectTransform, 12f);
            txt.supportRichText = false;
            field.textComponent = txt;
            field.placeholder = ph;
            field.characterLimit = 32;
            field.caretColor = Color.white;
            return field;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        public static Canvas Canvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = 5;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            canvas.pixelPerfect = false;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }
    }

    /// <summary>Icon + count display used by the hotbar and the inventory grid.</summary>
    public sealed class SlotView
    {
        public RectTransform Root;
        public Image Background;
        public Image Icon;
        public Text Count;

        public static SlotView Create(Transform parent, string name, Sprite background, float size, float iconSize)
        {
            var v = new SlotView();
            v.Background = UIFactory.Image(parent, name, background, Color.white, true);
            v.Root = v.Background.rectTransform;
            v.Root.sizeDelta = new Vector2(size, size);
            v.Icon = UIFactory.Image(v.Root, "Icon", null, Color.white);
            v.Icon.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
            v.Icon.preserveAspect = true;
            v.Icon.enabled = false;
            v.Count = UIFactory.Text(v.Root, "Count", "", Mathf.RoundToInt(size * 0.32f), TextAnchor.LowerRight, Color.white);
            UIFactory.Stretch(v.Count.rectTransform, size * 0.06f);
            return v;
        }

        public void Set(ItemStack stack, bool showCount)
        {
            if (stack.IsEmpty)
            {
                Icon.enabled = false;
                Count.text = "";
                return;
            }
            Icon.enabled = true;
            Icon.sprite = TextureFactory.Icons[(int)stack.Id];
            Count.text = showCount && stack.Count > 1 ? stack.Count.ToString() : "";
        }
    }

    /// <summary>Forwards pointer clicks/hover on a slot to callbacks (left, right, shift).</summary>
    public sealed class SlotHandler : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public int Index;
        public Action<int, PointerEventData.InputButton> Clicked;
        public Action<int> Entered;
        public Action<int> Exited;

        public void OnPointerDown(PointerEventData eventData)
        {
            Clicked?.Invoke(Index, eventData.button);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Entered?.Invoke(Index);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Exited?.Invoke(Index);
        }
    }
}
