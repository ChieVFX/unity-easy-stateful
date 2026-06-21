using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace EasyStateful.Samples.PerformanceLab
{
    /// <summary>
    /// Centralized color / sizing theme for the Performance Lab window so the
    /// whole UI stays visually consistent.
    /// </summary>
    public static class PerfTheme
    {
        public static readonly Color Bg        = Hex("#0D1117");
        public static readonly Color Panel     = Hex("#161B22");
        public static readonly Color PanelAlt  = Hex("#1C2330");
        public static readonly Color Border    = Hex("#30363D");
        public static readonly Color Track      = Hex("#262C36");
        public static readonly Color Text      = Hex("#E6EDF3");
        public static readonly Color TextDim    = Hex("#8B949E");
        public static readonly Color Accent     = Hex("#4DA3FF");
        public static readonly Color AccentDim  = Hex("#1F6FEB");
        public static readonly Color Good       = Hex("#3FB950");
        public static readonly Color Warn       = Hex("#D29922");
        public static readonly Color Bad        = Hex("#F85149");

        public static Color Hex(string s)
        {
            ColorUtility.TryParseHtmlString(s, out var c);
            return c;
        }
    }

    /// <summary>
    /// Small immediate helpers for assembling a clean uGUI hierarchy from code.
    /// Keeps PerformanceLab readable and avoids fragile serialized references.
    /// </summary>
    public static class PerfUI
    {
        static Sprite _round;
        static Sprite _knob;

        /// <summary>
        /// Procedurally generated 9-sliced rounded-rect sprite (border = radius).
        /// Built-in UI sprites aren't reliably loadable at runtime, so we make our own.
        /// </summary>
        public static Sprite Round => _round != null ? _round : (_round = MakeRounded(48, 14f));

        /// <summary>Solid circle sprite (slider handle).</summary>
        public static Sprite Knob => _knob != null ? _knob : (_knob = MakeCircle(48));

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
                float a = Mathf.Clamp01(0.5f - dist);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            int b = Mathf.RoundToInt(radius);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        /// <summary>Tall vertical-gradient sprite, stretched across the backdrop for a little depth.</summary>
        public static Sprite MakeVerticalGradient(Color top, Color bottom, int h = 256)
        {
            var tex = new Texture2D(2, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[2 * h];
            for (int y = 0; y < h; y++)
            {
                Color c = Color.Lerp(bottom, top, y / (float)(h - 1));
                px[y * 2] = c;
                px[y * 2 + 1] = c;
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, 2, h), new Vector2(0.5f, 0.5f), 100f);
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
                float a = Mathf.Clamp01(0.5f - dist);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>A filled, optionally rounded panel.</summary>
        public static Image Panel(string name, Transform parent, Color color, bool rounded = true)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (rounded)
            {
                img.sprite = Round;
                img.type = Image.Type.Sliced;
            }
            return img;
        }

        public static VerticalLayoutGroup VLayout(GameObject go, int pad, float spacing,
            bool expandW = true, bool expandH = false)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(pad, pad, pad, pad);
            v.spacing = spacing;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = expandW;
            v.childForceExpandHeight = expandH;
            v.childAlignment = TextAnchor.UpperLeft;
            return v;
        }

        public static HorizontalLayoutGroup HLayout(GameObject go, float spacing, bool expandW = false)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = expandW;
            h.childForceExpandHeight = true;
            h.childAlignment = TextAnchor.MiddleLeft;
            return h;
        }

        public static LayoutElement Layout(GameObject go, float prefH = -1, float prefW = -1,
            float flexW = -1, float minW = -1)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (prefH >= 0) le.preferredHeight = prefH;
            if (prefW >= 0) le.preferredWidth = prefW;
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (minW >= 0) le.minWidth = minW;
            return le;
        }

        public static TextMeshProUGUI Label(string name, Transform parent, string text,
            float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Left,
            FontStyles style = FontStyles.Normal)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        public static UnityEngine.UI.Button Button(string label, Transform parent, Color baseColor, Color textColor,
            System.Action onClick, float fontSize = 16f)
        {
            var img = Panel("Btn_" + label, parent, baseColor);
            var btn = img.gameObject.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            cb.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            cb.selectedColor = Color.white;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var t = Label("Text", img.transform, label, fontSize, textColor, TextAlignmentOptions.Center,
                FontStyles.Bold);
            Stretch(t.rectTransform);
            return btn;
        }

        /// <summary>Builds a standard uGUI Slider with track / fill / knob, themed.</summary>
        public static UnityEngine.UI.Slider Slider(string name, Transform parent, float min, float max, float value,
            bool wholeNumbers, UnityEngine.Events.UnityAction<float> onChanged)
        {
            var root = Rect(name, parent);
            var slider = root.gameObject.AddComponent<UnityEngine.UI.Slider>();

            var bg = Panel("Track", root, PerfTheme.Track);
            Anchor(bg.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f));
            bg.rectTransform.sizeDelta = new Vector2(0, 8);

            var fillArea = Rect("Fill Area", root);
            Anchor(fillArea, new Vector2(0, 0.5f), new Vector2(1, 0.5f));
            fillArea.sizeDelta = new Vector2(-20, 8);
            fillArea.anchoredPosition = Vector2.zero;
            var fill = Panel("Fill", fillArea, PerfTheme.Accent);
            fill.rectTransform.sizeDelta = new Vector2(20, 0);

            var handleArea = Rect("Handle Slide Area", root);
            Anchor(handleArea, new Vector2(0, 0), new Vector2(1, 1));
            handleArea.sizeDelta = new Vector2(-20, 0);
            handleArea.anchoredPosition = Vector2.zero;
            var handle = Rect("Handle", handleArea);
            var hImg = handle.gameObject.AddComponent<Image>();
            hImg.sprite = Knob;
            hImg.color = PerfTheme.Text;
            handle.sizeDelta = new Vector2(20, 20);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle;
            slider.targetGraphic = hImg;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            slider.value = value;
            if (onChanged != null) slider.onValueChanged.AddListener(onChanged);
            return slider;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static void Anchor(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
