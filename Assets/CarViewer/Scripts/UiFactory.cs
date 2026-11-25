using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CarViewer
{
    /// <summary>Shared palette: midnight / graphite panels, amber accent, cyan AR accent.</summary>
    public static class Palette
    {
        public static readonly Color Midnight = new Color32(0x0B, 0x14, 0x26, 0xE6);
        public static readonly Color MidnightSolid = new Color32(0x0B, 0x14, 0x26, 0xFF);
        public static readonly Color Graphite = new Color32(0x1C, 0x22, 0x30, 0xE0);
        public static readonly Color GraphiteLight = new Color32(0x2A, 0x32, 0x45, 0xFF);
        public static readonly Color Amber = new Color32(0xF5, 0xA5, 0x24, 0xFF);
        public static readonly Color Cyan = new Color32(0x22, 0xD3, 0xEE, 0xFF);
        public static readonly Color Text = new Color32(0xE8, 0xEE, 0xF7, 0xFF);
        public static readonly Color Muted = new Color32(0x8F, 0xA0, 0xB8, 0xFF);
        public static readonly Color Scrim = new Color32(0x00, 0x00, 0x00, 0xA0);
    }

    /// <summary>Small helpers to build uGUI hierarchies from code.</summary>
    public static class UiFactory
    {
        public static TMP_FontAsset Font;
        static Sprite rounded, circle;
        public static Sprite Rounded => rounded != null ? rounded : (rounded = UiIcons.RoundedRect());
        public static Sprite CircleSprite => circle != null ? circle : (circle = UiIcons.Circle());

        public static RectTransform NewRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public static void Stretch(RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
        }

        public static Image Panel(Transform parent, string name, Color color, bool round = true)
        {
            var rt = NewRect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (round) { img.sprite = Rounded; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f; }
            return img;
        }

        public static TextMeshProUGUI Label(Transform parent, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, string name = "Text")
        {
            var rt = NewRect(parent, name);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null) t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        public static Image Icon(Transform parent, Sprite sprite, Color color, string name = "Icon")
        {
            var rt = NewRect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        /// <summary>A dock-style button: icon on top, small label below.</summary>
        public class DockButton
        {
            public Button button;
            public Image icon;
            public TextMeshProUGUI label;
            public Image background;
            public void SetActive(bool active)
            {
                icon.color = active ? Palette.Amber : Palette.Text;
                label.color = active ? Palette.Amber : Palette.Muted;
            }
        }

        public static DockButton MakeDockButton(Transform parent, Sprite icon, string label)
        {
            var bg = Panel(parent, label + "Button", new Color(1, 1, 1, 0.001f));
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.pressedColor = new Color(1, 1, 1, 0.7f);
            btn.colors = colors;

            var ic = Icon(bg.transform, icon, Palette.Text);
            Anchor(ic.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -18), new Vector2(84, 84));
            var lb = Label(bg.transform, label, 28, Palette.Muted);
            Anchor(lb.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(170, 40));
            return new DockButton { button = btn, icon = ic, label = lb, background = bg };
        }

        /// <summary>Round button with a sprite icon or a text glyph.</summary>
        public static Button MakeRoundButton(Transform parent, string name, Sprite icon, string glyph, Color fill, Color fg)
        {
            var bg = Panel(parent, name, fill, false);
            bg.sprite = CircleSprite;
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            if (icon != null)
            {
                var ic = Icon(bg.transform, icon, fg);
                Anchor(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60));
            }
            else
            {
                var t = Label(bg.transform, glyph, 54, fg);
                Stretch(t.rectTransform);
            }
            return btn;
        }

        /// <summary>Pill button with a text label (primary action).</summary>
        public static Button MakeTextButton(Transform parent, string text, Color fill, Color fg, Vector2 size)
        {
            var bg = Panel(parent, text + "Button", fill);
            bg.rectTransform.sizeDelta = size;
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var t = Label(bg.transform, text, 48, fg);
            t.fontStyle = FontStyles.Bold;
            Stretch(t.rectTransform);
            return btn;
        }
    }
}

