using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace EasyStateful.Samples.Showcase
{
    /// <summary>
    /// Feeds the current time into a UI material's <c>_AnimTime</c> uniform every frame.
    /// uGUI doesn't reliably re-evaluate <c>_Time</c> on a static canvas, so the custom
    /// shaders read <c>_AnimTime</c> instead and we drive it from here.
    /// </summary>
    public class ShaderTime : MonoBehaviour
    {
        static readonly int ID = Shader.PropertyToID("_AnimTime");
        Graphic _g;
        void Awake() { _g = GetComponent<Graphic>(); }
        void Update()
        {
            var m = _g != null ? _g.material : null;
            if (m != null) m.SetFloat(ID, Time.unscaledTime);
        }
    }

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
        public static readonly Color Gold        = Hex("#E3B341");
        public static readonly Color Orange      = Hex("#F0883E");

        public static Color Hex(string s) { ColorUtility.TryParseHtmlString(s, out var c); return c; }
    }

    /// <summary>Immediate helpers to assemble a clean, rounded uGUI hierarchy from code.</summary>
    public static class UI
    {
        static Sprite _round, _circle, _triangle, _star, _check;
        public static Sprite Check => _check != null ? _check : (_check = MakeCheck(48));

        /// <summary>9-sliced rounded-rect sprite (border = radius), generated procedurally.</summary>
        public static Sprite Round => _round != null ? _round : (_round = MakeRounded(48, 16f));
        public static Sprite Circle => _circle != null ? _circle : (_circle = MakeCircle(64));
        /// <summary>Downward-pointing triangle (chevron); rotate 180° to point up.</summary>
        public static Sprite Triangle => _triangle != null ? _triangle : (_triangle = MakeTriangle(40));
        /// <summary>Five-point star (points up).</summary>
        public static Sprite Star => _star != null ? _star : (_star = MakeStar(64));

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

        static readonly Dictionary<string, Sprite> _roundedRects = new Dictionary<string, Sprite>();

        /// <summary>
        /// Full-rect rounded-rectangle sprite generated at an exact pixel size, for shader-driven
        /// tiles drawn with <see cref="Image.Type.Simple"/>. A 9-sliced sprite would segment the
        /// UVs (breaking the sweep/gradient), while stretching the shared square <see cref="Round"/>
        /// sprite balloons the corner radius into an ellipse — so generate the shape at the right
        /// aspect instead.
        /// </summary>
        public static Sprite RoundedRect(int w, int h, float radius)
        {
            string key = w + "x" + h + "r" + radius;
            if (_roundedRects.TryGetValue(key, out var cached) && cached != null) return cached;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            float hw = w * 0.5f, hh = h * 0.5f;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = x + 0.5f - hw, dy = y + 0.5f - hh;
                float qx = Mathf.Abs(dx) - (hw - radius);
                float qy = Mathf.Abs(dy) - (hh - radius);
                float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                float dist = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - dist) * 255));
            }
            tex.SetPixels32(px); tex.Apply(false);
            var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f); // FullRect → Simple
            _roundedRects[key] = s;
            return s;
        }

        /// <summary>
        /// Rectangle with only the TOP two corners rounded (flat bottom and sides) — for a card
        /// header that meets the card's rounded top corners but stays a flat-bottomed band instead
        /// of curling into a pill. Drawn with <see cref="Image.Type.Simple"/> at its own size.
        /// </summary>
        public static Sprite RoundedRectTop(int w, int h, float radius)
        {
            string key = "T" + w + "x" + h + "r" + radius;
            if (_roundedRects.TryGetValue(key, out var cached) && cached != null) return cached;

            float r = Mathf.Min(radius, Mathf.Min(w * 0.5f, h));
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)        // y = 0 is the bottom row, y = h-1 the top
            for (int x = 0; x < w; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float a = 1f;
                float cy = h - r;              // arc centre height for the top corners
                if (fy > cy && (fx < r || fx > w - r))
                {
                    float cx = (fx < w * 0.5f) ? r : (w - r);
                    float dist = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy)) - r;
                    a = Mathf.Clamp01(0.5f - dist);
                }
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px); tex.Apply(false);
            var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            _roundedRects[key] = s;
            return s;
        }

        /// <summary>
        /// Rectangle with only the LEFT two corners rounded (flat right edge) — for a side-nav
        /// selection pill that runs flush to the rail's right edge. Drawn Simple at its own size.
        /// </summary>
        public static Sprite RoundedRectLeft(int w, int h, float radius)
        {
            string key = "L" + w + "x" + h + "r" + radius;
            if (_roundedRects.TryGetValue(key, out var cached) && cached != null) return cached;

            float r = Mathf.Min(radius, Mathf.Min(w, h * 0.5f));
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float a = 1f;
                if (fx < r && (fy < r || fy > h - r))   // only the two left corners
                {
                    float cx = r;
                    float cy = (fy < h * 0.5f) ? r : (h - r);
                    float dist = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy)) - r;
                    a = Mathf.Clamp01(0.5f - dist);
                }
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px); tex.Apply(false);
            var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            _roundedRects[key] = s;
            return s;
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

        static Sprite MakeStar(int size)
        {
            // 10 vertices alternating outer/inner radius, pointing up. 2x2 supersampled for AA.
            var pts = new Vector2[10];
            float cx = size * 0.5f, cy = size * 0.5f;
            float outer = size * 0.48f, inner = outer * 0.42f;
            for (int i = 0; i < 10; i++)
            {
                float ang = Mathf.PI / 2f + i * Mathf.PI / 5f; // start at top
                float r = (i % 2 == 0) ? outer : inner;
                pts[i] = new Vector2(cx + Mathf.Cos(ang) * r, cy + Mathf.Sin(ang) * r);
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < 2; sy++)
                for (int sx = 0; sx < 2; sx++)
                    if (InPoly(pts, x + 0.25f + sx * 0.5f, y + 0.25f + sy * 0.5f)) hits++;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(hits * 63));
            }
            tex.SetPixels32(px); tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite MakeCheck(int size)
        {
            // Two thick segments forming a check mark.
            Vector2 a = new Vector2(0.22f, 0.50f) * size;
            Vector2 b = new Vector2(0.42f, 0.30f) * size;
            Vector2 c = new Vector2(0.80f, 0.74f) * size;
            float t = size * 0.085f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Mathf.Min(SegDist(p, a, b), SegDist(p, b, c));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(t - d) * 255));
            }
            tex.SetPixels32(px); tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a, ap = p - a;
            float h = Mathf.Clamp01(Vector2.Dot(ap, ab) / Vector2.Dot(ab, ab));
            return (ap - ab * h).magnitude;
        }

        static bool InPoly(Vector2[] p, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                if ((p[i].y > y) != (p[j].y > y) &&
                    x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x)
                    inside = !inside;
            return inside;
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
