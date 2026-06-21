using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace EasyStateful.Samples.Showcase
{
    /// <summary>Shared color palette for the showcase.</summary>
    public static class Palette
    {
        public static readonly Color Bg        = Hex("#0D1117");
        public static readonly Color Panel     = Hex("#161B22");
        public static readonly Color PanelAlt  = Hex("#1C2230");
        public static readonly Color Track      = Hex("#2A313C");
        public static readonly Color Border    = Hex("#30363D");
        public static readonly Color Text      = Hex("#E6EDF3");
        public static readonly Color TextDim    = Hex("#8B949E");
        public static readonly Color Accent     = Hex("#4DA3FF");
        public static readonly Color Green       = Hex("#3FB950");
        public static readonly Color Purple      = Hex("#A371F7");
        public static readonly Color Pink        = Hex("#F778BA");

        public static Color Hex(string s) { ColorUtility.TryParseHtmlString(s, out var c); return c; }
    }

    /// <summary>Immediate helpers to assemble a clean, rounded uGUI hierarchy from code.</summary>
    public static class UI
    {
        static Sprite _round, _circle, _triangle;

        /// <summary>9-sliced rounded-rect sprite (border = radius), generated procedurally.</summary>
        public static Sprite Round => _round != null ? _round : (_round = MakeRounded(48, 16f));
        public static Sprite Circle => _circle != null ? _circle : (_circle = MakeCircle(64));
        /// <summary>Downward-pointing triangle (chevron); rotate 180° to point up.</summary>
        public static Sprite Triangle => _triangle != null ? _triangle : (_triangle = MakeTriangle(40));

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        public static Image Panel(string name, Transform parent, Color color, bool rounded = true, bool circle = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (circle) { img.sprite = Circle; img.type = Image.Type.Simple; }
            else if (rounded) { img.sprite = Round; img.type = Image.Type.Sliced; }
            return img;
        }

        public static TextMeshProUGUI Label(string name, Transform parent, string text, float size,
            Color color, TextAlignmentOptions align = TextAlignmentOptions.Left, FontStyles style = FontStyles.Normal)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = style;
            t.raycastTarget = false; t.richText = true;
            return t;
        }

        /// <summary>Adds a Button to an existing Image with a subtle hover/press tint.</summary>
        public static Button MakeButton(Image target, System.Action onClick)
        {
            var btn = target.gameObject.AddComponent<Button>();
            btn.targetGraphic = target;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            cb.selectedColor = Color.white;
            cb.fadeDuration = 0.1f;
            btn.colors = cb;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        public static void Stretch(RectTransform rt, float pad = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, pad); rt.offsetMax = new Vector2(-pad, -pad);
        }

        public static RectTransform At(RectTransform rt, float x, float y, float w, float h,
            Vector2? anchor = null, Vector2? pivot = null)
        {
            var a = anchor ?? new Vector2(0.5f, 0.5f);
            rt.anchorMin = rt.anchorMax = a;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
            return rt;
        }

        static Sprite MakeRounded(int size, float radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - half, dy = y + 0.5f - half;
                float qx = Mathf.Abs(dx) - (half - radius);
                float qy = Mathf.Abs(dy) - (half - radius);
                float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                float dist = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - dist) * 255));
            }
            tex.SetPixels32(px); tex.Apply(false);
            int b = Mathf.RoundToInt(radius);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        static Sprite MakeCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - half, dy = y + 0.5f - half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy) - (half - 1f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - dist) * 255));
            }
            tex.SetPixels32(px); tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite MakeTriangle(int size)
        {
            // Apex at bottom-center, base across the top → points down.
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                float halfW = (y / (float)(size - 1)) * (half - 1f); // 0 at bottom, full at top
                for (int x = 0; x < size; x++)
                {
                    float d = halfW - Mathf.Abs(x + 0.5f - half);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(d) * 255));
                }
            }
            tex.SetPixels32(px); tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        public static Sprite VerticalGradient(Color top, Color bottom, int h = 256)
        {
            var tex = new Texture2D(2, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[2 * h];
            for (int y = 0; y < h; y++)
            {
                Color c = Color.Lerp(bottom, top, y / (float)(h - 1));
                px[y * 2] = c; px[y * 2 + 1] = c;
            }
            tex.SetPixels32(px); tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, 2, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
