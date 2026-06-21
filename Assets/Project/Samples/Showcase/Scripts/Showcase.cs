using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using EasyStateful.Runtime;

namespace EasyStateful.Samples.Showcase
{
    /// <summary>
    /// Easy Stateful Showcase.
    ///
    /// A small, living UI built entirely from <see cref="StatefulRoot"/> widgets whose
    /// visual states are authored in code and driven by button clicks via TweenToState:
    ///   • a segmented control with a sliding highlight and a morphing hero emblem,
    ///   • an iOS-style toggle (knob slides, track recolors),
    ///   • an expand / collapse card (height grows, chevron rotates).
    ///
    /// The point: rich, smooth UI motion without a line of animation code — just states + one call.
    /// </summary>
    [AddComponentMenu("Easy Stateful/Showcase")]
    public class Showcase : MonoBehaviour
    {
        const string RECT      = "UnityEngine.RectTransform, UnityEngine.CoreModule";
        const string TRANSFORM = "UnityEngine.Transform, UnityEngine.CoreModule";
        const string IMAGE     = "UnityEngine.UI.Image, UnityEngine.UI";

        // Segmented control content per tab.
        static readonly string[] TabTitles = { "Overview", "Activity", "Settings" };
        static readonly string[] TabDesc =
        {
            "A calm place to start.\nGlanceable and clean.",
            "Your recent motion.\nEverything updates live.",
            "Tune it to taste.\nThe states do the rest.",
        };
        static readonly Color[] TabColors = { Palette.Accent, Palette.Green, Palette.Purple };

        StatefulRoot _segmented, _toggle, _card, _drawer, _toast;
        TextMeshProUGUI _heroTitle, _heroDesc, _toggleValue, _toastLabel;
        Image[] _segLabBg;
        TextMeshProUGUI[] _segLab;
        int _tab;
        bool _toggleOn, _cardOpen, _drawerOpen, _toastShown;
        float _toastHideAt;

        void Awake()
        {
            QualitySettings.vSyncCount = 1;
            Application.runInBackground = true;
        }

        void Start()
        {
            var canvas = EnsureCanvas();
            BuildUI(canvas.transform);
        }

        // ============================================================ UI root
        void BuildUI(Transform canvas)
        {
            var bg = UI.Panel("Showcase_BG", canvas, Color.white, rounded: false);
            bg.sprite = UI.Round; bg.type = Image.Type.Simple;
            var aurora = Mat("EasyStateful/UIAurora");          // custom uGUI shader (animated)
            if (aurora != null) bg.material = aurora;
            else { bg.sprite = UI.VerticalGradient(Palette.Hex("#141A23"), Palette.Hex("#070A0E")); }
            bg.raycastTarget = false;
            UI.Stretch(bg.rectTransform);

            // Centered "app window".
            var panel = UI.Panel("Window", canvas, Palette.Panel);
            UI.At(panel.rectTransform, 0, 0, 720, 880);
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = Palette.Border;
            outline.effectDistance = new Vector2(1, -1);

            var p = panel.transform;

            // Header.
            var title = UI.Label("Title", p, "Easy Stateful", 30, Palette.Text, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UI.At(title.rectTransform, 40, -26, 500, 40, new Vector2(0, 1), new Vector2(0, 1));
            var sub = UI.Label("Sub", p, "Live UI states — one method call away", 15, Palette.TextDim, TextAlignmentOptions.TopLeft);
            UI.At(sub.rectTransform, 42, -64, 500, 24, new Vector2(0, 1), new Vector2(0, 1));

            // Hamburger button (opens the slide-in drawer).
            var menu = UI.Panel("MenuBtn", p, Palette.PanelAlt);
            UI.At(menu.rectTransform, -34, -36, 46, 40, new Vector2(1, 1), new Vector2(1, 1));
            UI.MakeButton(menu, OpenDrawer);
            for (int i = 0; i < 3; i++)
            {
                var bar = UI.Panel($"Bar{i}", menu.transform, Palette.Text, rounded: false);
                UI.At(bar.rectTransform, 0, 8 - i * 8, 22, 2.5f);
                bar.raycastTarget = false;
            }

            BuildSegmented(p);
            Divider(p, -470);
            BuildToggleRow(p);
            Divider(p, -596);
            BuildCard(p);

            var foot = UI.Label("Foot", p, "Switch tabs · flip the toggle · open the menu (top-right)", 13, Palette.TextDim,
                TextAlignmentOptions.Center);
            UI.At(foot.rectTransform, 0, 28, 640, 20, new Vector2(0.5f, 0), new Vector2(0.5f, 0));

            BuildToast(p);
            BuildDrawer(p); // last → renders above everything

            SelectTab(0, instant: true);
        }

        void Divider(Transform parent, float y)
        {
            var d = UI.Panel("Divider", parent, new Color(1, 1, 1, 0.06f), rounded: false);
            UI.At(d.rectTransform, 0, y, 640, 1, new Vector2(0.5f, 1), new Vector2(0.5f, 1));
        }

        // ============================================================ Segmented control + hero
        void BuildSegmented(Transform parent)
        {
            var root = UI.Rect("Segmented", parent);
            UI.At(root, 0, -90, 640, 360, new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            _segmented = root.gameObject.AddComponent<StatefulRoot>();

            // --- segment bar ---
            var seg = UI.Panel("Seg", root, Palette.PanelAlt);
            UI.At(seg.rectTransform, 0, -28, 600, 56, new Vector2(0.5f, 1), new Vector2(0.5f, 1));

            // sliding highlight pill (animated)
            var hi = UI.Panel("Highlight", seg.transform, Palette.Accent);
            UI.At(hi.rectTransform, -200, 0, 188, 44);

            _segLab = new TextMeshProUGUI[3];
            _segLabBg = new Image[3];
            float[] segX = { -200, 0, 200 };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var hit = UI.Panel($"Seg{i}", seg.transform, new Color(0, 0, 0, 0), rounded: false);
                UI.At(hit.rectTransform, segX[i], 0, 196, 50);
                UI.MakeButton(hit, () => SelectTab(idx));
                _segLabBg[i] = hit;
                var lab = UI.Label("Lab", hit.transform, TabTitles[i], 17, Palette.TextDim,
                    TextAlignmentOptions.Center, FontStyles.Bold);
                UI.Stretch(lab.rectTransform);
                _segLab[i] = lab;
            }

            // --- hero ---
            var hero = UI.Rect("Hero", root);
            UI.At(hero, 0, -96, 600, 220, new Vector2(0.5f, 1), new Vector2(0.5f, 1));

            // emblem: a rounded square with a corner pip so rotation reads clearly
            var emblem = UI.Panel("Emblem", hero, Palette.Accent);
            UI.At(emblem.rectTransform, -210, 6, 120, 120);
            var shimmer = Mat("EasyStateful/UIShimmer");        // custom uGUI shader (sheen sweep)
            if (shimmer != null) emblem.material = shimmer;
            var pip = UI.Panel("Pip", emblem.transform, new Color(1, 1, 1, 0.9f), circle: true);
            UI.At(pip.rectTransform, 32, 32, 22, 22);
            pip.raycastTarget = false;

            _heroTitle = UI.Label("HeroTitle", hero, "Overview", 32, Palette.Text, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UI.At(_heroTitle.rectTransform, -130, 56, 360, 44, new Vector2(0.5f, 0.5f), new Vector2(0, 1));
            _heroDesc = UI.Label("HeroDesc", hero, "", 17, Palette.TextDim, TextAlignmentOptions.TopLeft);
            UI.At(_heroDesc.rectTransform, -130, 4, 360, 80, new Vector2(0.5f, 0.5f), new Vector2(0, 1));

            var accent = UI.Panel("Accent", hero, Palette.Accent);
            UI.At(accent.rectTransform, -130, -76, 150, 6, new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f));

            _segmented.statefulDataAsset = BuildSegmentedData();
            _segmented.LoadFromAsset(_segmented.statefulDataAsset);
            _segmented.SnapToState("Tab0");
        }

        StatefulDataAsset BuildSegmentedData()
        {
            var states = new List<State>();
            float[] hiX = { -200, 0, 200 };
            float[] scale = { 1.0f, 1.18f, 0.86f };
            float[] rot = { 0f, 120f, 240f };
            for (int i = 0; i < 3; i++)
            {
                var c = TabColors[i];
                states.Add(St($"Tab{i}",
                    P("Seg/Highlight", RECT, "m_AnchoredPosition.x", hiX[i]),
                    P("Seg/Highlight", IMAGE, "m_Color.r", c.r), P("Seg/Highlight", IMAGE, "m_Color.g", c.g), P("Seg/Highlight", IMAGE, "m_Color.b", c.b),
                    P("Hero/Emblem", TRANSFORM, "m_LocalScale.x", scale[i]), P("Hero/Emblem", TRANSFORM, "m_LocalScale.y", scale[i]),
                    P("Hero/Emblem", TRANSFORM, "localEulerAngles.z", rot[i]),
                    P("Hero/Emblem", IMAGE, "m_Color.r", c.r), P("Hero/Emblem", IMAGE, "m_Color.g", c.g), P("Hero/Emblem", IMAGE, "m_Color.b", c.b),
                    P("Hero/Accent", IMAGE, "m_Color.r", c.r), P("Hero/Accent", IMAGE, "m_Color.g", c.g), P("Hero/Accent", IMAGE, "m_Color.b", c.b)
                ));
            }
            return Data(states);
        }

        void SelectTab(int i, bool instant = false)
        {
            _tab = i;
            _heroTitle.text = TabTitles[i];
            _heroDesc.text = TabDesc[i];
            for (int s = 0; s < 3; s++)
                _segLab[s].color = s == i ? Palette.Hex("#0D1117") : Palette.TextDim;

            if (instant) _segmented.SnapToState($"Tab{i}");
            else _segmented.TweenToState($"Tab{i}", 0.45f, Ease.OutCubic);
        }

        // ============================================================ Toggle
        void BuildToggleRow(Transform parent)
        {
            var label = UI.Label("ToggleLabel", parent, "Smooth notifications", 18, Palette.Text,
                TextAlignmentOptions.Left);
            UI.At(label.rectTransform, 40, -508, 360, 28, new Vector2(0, 1), new Vector2(0, 1));
            _toggleValue = UI.Label("ToggleValue", parent, "Off", 14, Palette.TextDim, TextAlignmentOptions.Left);
            UI.At(_toggleValue.rectTransform, 42, -534, 360, 20, new Vector2(0, 1), new Vector2(0, 1));

            var track = UI.Panel("Toggle", parent, Palette.Track);
            UI.At(track.rectTransform, -40, -520, 72, 38, new Vector2(1, 1), new Vector2(1, 1));
            _toggle = track.gameObject.AddComponent<StatefulRoot>();
            UI.MakeButton(track, ToggleSwitch);

            var knob = UI.Panel("Knob", track.transform, Color.white, circle: true);
            UI.At(knob.rectTransform, -16, 0, 28, 28);
            knob.raycastTarget = false;

            _toggle.statefulDataAsset = BuildToggleData();
            _toggle.LoadFromAsset(_toggle.statefulDataAsset);
            _toggle.SnapToState("Off");
        }

        StatefulDataAsset BuildToggleData()
        {
            var off = Palette.Track;
            var on = Palette.Green;
            return Data(new List<State>
            {
                St("Off",
                    P("", IMAGE, "m_Color.r", off.r), P("", IMAGE, "m_Color.g", off.g), P("", IMAGE, "m_Color.b", off.b),
                    P("Knob", RECT, "m_AnchoredPosition.x", -16f)),
                St("On",
                    P("", IMAGE, "m_Color.r", on.r), P("", IMAGE, "m_Color.g", on.g), P("", IMAGE, "m_Color.b", on.b),
                    P("Knob", RECT, "m_AnchoredPosition.x", 16f)),
            });
        }

        void ToggleSwitch()
        {
            _toggleOn = !_toggleOn;
            _toggleValue.text = _toggleOn ? "On" : "Off";
            _toggleValue.color = _toggleOn ? Palette.Green : Palette.TextDim;
            _toggle.TweenToState(_toggleOn ? "On" : "Off", 0.28f, Ease.OutBack);
            ShowToast(_toggleOn ? "Notifications on" : "Notifications off");
        }

        // ============================================================ Expand / collapse card
        const float CardCollapsed = 64f;
        const float CardExpanded = 188f;

        void BuildCard(Transform parent)
        {
            var card = UI.Panel("Card", parent, Palette.PanelAlt);
            UI.At(card.rectTransform, 0, -636, 640, CardCollapsed, new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            card.gameObject.AddComponent<RectMask2D>(); // clip the body while collapsed
            _card = card.gameObject.AddComponent<StatefulRoot>();

            var header = UI.Panel("Header", card.transform, new Color(0, 0, 0, 0), rounded: false);
            UI.At(header.rectTransform, 0, 0, 640, CardCollapsed, new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            UI.MakeButton(header, ToggleCard);

            var htitle = UI.Label("HTitle", header.transform, "What just happened?", 18, Palette.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            UI.At(htitle.rectTransform, 24, 0, 420, CardCollapsed, new Vector2(0, 0.5f), new Vector2(0, 0.5f));

            var chevron = UI.Panel("Chevron", header.transform, Palette.TextDim, rounded: false);
            chevron.sprite = UI.Triangle;
            chevron.type = Image.Type.Simple;
            chevron.raycastTarget = false;
            UI.At(chevron.rectTransform, -28, 0, 18, 12, new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f));

            var body = UI.Label("Body", card.transform,
                "Every transition you just saw is a <b>state</b> on a StatefulRoot.\n" +
                "You author the look of each state once, then call\n" +
                "<color=#4DA3FF>TweenToState(\"Name\")</color> — easing and timing are handled for you.",
                15, Palette.TextDim, TextAlignmentOptions.TopLeft);
            UI.At(body.rectTransform, 24, -CardCollapsed, 590, 110, new Vector2(0, 1), new Vector2(0, 1));

            _card.statefulDataAsset = BuildCardData();
            _card.LoadFromAsset(_card.statefulDataAsset);
            _card.SnapToState("Collapsed");
        }

        StatefulDataAsset BuildCardData()
        {
            return Data(new List<State>
            {
                St("Collapsed",
                    P("", RECT, "m_SizeDelta.y", CardCollapsed),
                    P("Header/Chevron", TRANSFORM, "localEulerAngles.z", 0f)),
                St("Expanded",
                    P("", RECT, "m_SizeDelta.y", CardExpanded),
                    P("Header/Chevron", TRANSFORM, "localEulerAngles.z", 180f)),
            });
        }

        void ToggleCard()
        {
            _cardOpen = !_cardOpen;
            _card.TweenToState(_cardOpen ? "Expanded" : "Collapsed", 0.35f, Ease.OutCubic);
        }

        // ============================================================ slide-in drawer
        void BuildDrawer(Transform parent)
        {
            var root = UI.Rect("Drawer", parent);
            UI.Stretch(root);
            root.gameObject.AddComponent<RectMask2D>();   // clips the parked sheet off-screen
            _drawer = root.gameObject.AddComponent<StatefulRoot>();

            var scrim = UI.Panel("Scrim", root, new Color(0, 0, 0, 0.55f), rounded: false);
            UI.Stretch(scrim.rectTransform);
            UI.MakeButton(scrim, CloseDrawer);            // tap outside to close

            var sheet = UI.Panel("Sheet", root, Palette.Hex("#1B2230"));
            var srt = sheet.rectTransform;
            srt.anchorMin = new Vector2(1, 0); srt.anchorMax = new Vector2(1, 1);
            srt.pivot = new Vector2(1, 0.5f);
            srt.sizeDelta = new Vector2(300, 0);
            srt.anchoredPosition = new Vector2(320, 0);   // closed (off-screen right)
            var so = sheet.gameObject.AddComponent<Outline>();
            so.effectColor = Palette.Border; so.effectDistance = new Vector2(-1, -1);

            var st = UI.Label("SheetTitle", sheet.transform, "Quick settings", 20, Palette.Text,
                TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UI.At(st.rectTransform, 24, -28, 252, 30, new Vector2(0, 1), new Vector2(0, 1));

            string[] items = { "Sound effects", "Haptics", "Auto-sync", "Reduced motion" };
            Color[] dots = { Palette.Accent, Palette.Green, Palette.Purple, Palette.Pink };
            for (int i = 0; i < items.Length; i++)
            {
                var row = UI.Panel($"Row{i}", sheet.transform, Palette.PanelAlt);
                UI.At(row.rectTransform, 0, -78 - i * 52, 252, 44, new Vector2(0.5f, 1), new Vector2(0.5f, 1));
                var lab = UI.Label("L", row.transform, items[i], 15, Palette.Text, TextAlignmentOptions.Left);
                UI.At(lab.rectTransform, 16, 0, 180, 44, new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                var dot = UI.Panel("Dot", row.transform, dots[i], circle: true);
                UI.At(dot.rectTransform, -18, 0, 12, 12, new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f));
                dot.raycastTarget = false;
            }

            var hint = UI.Label("Hint", sheet.transform, "Tap outside to close", 12, Palette.TextDim,
                TextAlignmentOptions.Center);
            UI.At(hint.rectTransform, 0, 24, 252, 18, new Vector2(0.5f, 0), new Vector2(0.5f, 0));

            _drawer.statefulDataAsset = Data(new List<State>
            {
                St("Closed",
                    P("Scrim", "", "m_IsActive", 0f),
                    P("Scrim", IMAGE, "m_Color.a", 0f),
                    P("Sheet", RECT, "m_AnchoredPosition.x", 320f)),
                St("Open",
                    P("Scrim", "", "m_IsActive", 1f),
                    P("Scrim", IMAGE, "m_Color.a", 0.55f),
                    P("Sheet", RECT, "m_AnchoredPosition.x", 0f)),
            });
            _drawer.LoadFromAsset(_drawer.statefulDataAsset);
            _drawer.SnapToState("Closed");
        }

        void OpenDrawer()  { _drawerOpen = true;  _drawer.TweenToState("Open", 0.34f, Ease.OutCubic); }
        void CloseDrawer() { if (!_drawerOpen) return; _drawerOpen = false; _drawer.TweenToState("Closed", 0.30f, Ease.InOutCubic); }

        // ============================================================ toast (slide-in + auto hide)
        void BuildToast(Transform parent)
        {
            var toast = UI.Panel("Toast", parent, Palette.Hex("#13283F"));
            UI.At(toast.rectTransform, 0, 40, 280, 46, new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            toast.raycastTarget = false;
            var to = toast.gameObject.AddComponent<Outline>();
            to.effectColor = new Color(0.30f, 0.64f, 1f, 0.5f); to.effectDistance = new Vector2(1, -1);
            _toast = toast.gameObject.AddComponent<StatefulRoot>();

            var dot = UI.Panel("Dot", toast.transform, Palette.Accent, circle: true);
            UI.At(dot.rectTransform, 22, 0, 12, 12);
            dot.raycastTarget = false;
            _toastLabel = UI.Label("L", toast.transform, "", 15, Palette.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            UI.At(_toastLabel.rectTransform, 44, 0, 220, 46, new Vector2(0, 0.5f), new Vector2(0, 0.5f));

            _toast.statefulDataAsset = Data(new List<State>
            {
                St("Hidden",
                    P("", "", "m_IsActive", 0f),
                    P("", IMAGE, "m_Color.a", 0f),
                    P("", RECT, "m_AnchoredPosition.y", 40f)),
                St("Shown",
                    P("", "", "m_IsActive", 1f),
                    P("", IMAGE, "m_Color.a", 1f),
                    P("", RECT, "m_AnchoredPosition.y", 76f)),
            });
            _toast.LoadFromAsset(_toast.statefulDataAsset);
            _toast.SnapToState("Hidden");
        }

        void ShowToast(string text)
        {
            _toastLabel.text = text;
            _toastShown = true;
            _toastHideAt = Time.time + 1.8f;
            _toast.TweenToState("Shown", 0.3f, Ease.OutBack);
        }

        void Update()
        {
            if (_toastShown && Time.time >= _toastHideAt)
            {
                _toastShown = false;
                _toast.TweenToState("Hidden", 0.3f, Ease.InCubic);
            }
        }

        static Material Mat(string shader)
        {
            var sh = Shader.Find(shader);
            return sh != null ? new Material(sh) { hideFlags = HideFlags.DontSave } : null;
        }

        // ============================================================ data helpers
        static Property P(string path, string comp, string prop, float val) =>
            new Property { path = path, componentType = comp, propertyName = prop, value = val, objectReference = "" };

        static State St(string name, params Property[] props) =>
            new State { name = name, time = 0, properties = new List<Property>(props) };

        static StatefulDataAsset Data(List<State> states)
        {
            var a = ScriptableObject.CreateInstance<StatefulDataAsset>();
            a.stateMachine = new UIStateMachine { states = states };
            return a;
        }

        // ============================================================ canvas
        Canvas EnsureCanvas()
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }
            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
            return canvas;
        }
    }
}
