using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using EasyStateful.Runtime;

namespace EasyStateful.Samples.Showcase
{
    /// <summary>
    /// Easy Stateful Showcase — a small multi-page app built entirely from
    /// <see cref="StatefulRoot"/> widgets. Every animation in here is just an
    /// authored state plus a TweenToState() call; no hand-written tween code.
    ///
    /// Shell (this file): aurora background, window, left nav rail with a sliding
    /// selector, top bar, sliding page host, and the global overlays — drawer,
    /// toast, modal and FAB speed-dial. The four pages live in the Showcase.*.cs
    /// partials.
    /// </summary>
    [AddComponentMenu("Easy Stateful/Showcase")]
    public partial class Showcase : MonoBehaviour
    {
        // Component type strings for code-authored Property bindings.
        internal const string RECT      = "UnityEngine.RectTransform, UnityEngine.CoreModule";
        internal const string TRANSFORM = "UnityEngine.Transform, UnityEngine.CoreModule";
        internal const string IMAGE     = "UnityEngine.UI.Image, UnityEngine.UI";

        // ---- window geometry ----
        const float WIN_W = 1000, WIN_H = 648;
        const float RAIL_W = 200, TOP_H = 76;
        const float CONTENT_W = WIN_W - RAIL_W;       // 800
        const float PAGE_W = CONTENT_W, PAGE_H = WIN_H - TOP_H; // 800 x 572
        const float SLIDE = PAGE_W + 60;

        static readonly string[] NavTitles = { "Controls", "Motion", "Layout", "Effects", "Custom" };
        static readonly string[] NavSubs =
        {
            "Inputs that animate themselves",
            "Compare easings, side by side",
            "Reveal, expand and overlay",
            "Custom uGUI shaders & loaders",
            "Animate your own script values",
        };

        // ---- shell refs ----
        RectTransform _pageHost;
        readonly List<StatefulRoot> _pages = new List<StatefulRoot>();
        StatefulRoot _navSel;
        TextMeshProUGUI _topTitle, _topSub;
        TextMeshProUGUI[] _navLabels;
        int _page;

        // ---- overlays ----
        StatefulRoot _drawer, _toast, _modal, _fab;
        TextMeshProUGUI _toastLabel;
        bool _drawerOpen, _modalOpen, _fabOpen, _toastShown;
        float _toastHideAt;
        readonly StatefulRoot[] _drawerToggles = new StatefulRoot[4];
        readonly bool[] _drawerOn = { true, false, true, false };

        void Awake()
        {
            QualitySettings.vSyncCount = 1;
            Application.runInBackground = true;
        }

        void Start()
        {
            BuildUI(EnsureCanvas().transform);
        }

        // ============================================================ shell
        void BuildUI(Transform canvas)
        {
            var bg = UI.Panel("Showcase_BG", canvas, Color.white, rounded: false);
            bg.sprite = UI.Round; bg.type = Image.Type.Simple;
            var aurora = Mat("EasyStateful/UIAurora");
            if (aurora != null) { bg.material = aurora; bg.gameObject.AddComponent<ShaderTime>(); }
            else bg.sprite = UI.VerticalGradient(Palette.Hex("#141A23"), Palette.Hex("#070A0E"));
            bg.raycastTarget = false;
            UI.Stretch(bg.rectTransform);

            var window = UI.Panel("Window", canvas, Palette.Panel);
            UI.At(window.rectTransform, 0, 0, WIN_W, WIN_H);
            var outline = window.gameObject.AddComponent<Outline>();
            outline.effectColor = Palette.Border; outline.effectDistance = new Vector2(1, -1);

            BuildRail(window.transform);

            var content = UI.Rect("Content", window.transform);
            content.anchorMin = new Vector2(1, 0); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(1, 0.5f); content.sizeDelta = new Vector2(CONTENT_W, 0);
            content.anchoredPosition = Vector2.zero;

            BuildTopBar(content);

            _pageHost = UI.Rect("PageHost", content);
            _pageHost.anchorMin = Vector2.zero; _pageHost.anchorMax = Vector2.one;
            _pageHost.offsetMin = Vector2.zero; _pageHost.offsetMax = new Vector2(0, -TOP_H);
            _pageHost.gameObject.AddComponent<RectMask2D>();

            BuildControlsPage(MakePage(0));
            BuildMotionPage(MakePage(1));
            BuildLayoutPage(MakePage(2));
            BuildEffectsPage(MakePage(3));
            BuildCustomPage(MakePage(4));

            BuildFab(window.transform);
            BuildModal(window.transform);
            BuildToast(window.transform);
            BuildDrawer(window.transform);

            SelectPage(0, instant: true);
        }

        RectTransform MakePage(int index)
        {
            var page = UI.Rect($"Page{index}", _pageHost);
            UI.At(page, index == 0 ? 0 : SLIDE, 0, PAGE_W, PAGE_H);
            var sr = page.gameObject.AddComponent<StatefulRoot>();
            sr.statefulDataAsset = Data(new List<State>
            {
                St("Center", P("", RECT, "m_AnchoredPosition.x", 0f)),
                St("Left",   P("", RECT, "m_AnchoredPosition.x", -SLIDE)),
                St("Right",  P("", RECT, "m_AnchoredPosition.x", SLIDE)),
            });
            sr.LoadFromAsset(sr.statefulDataAsset);
            sr.SnapToState(index == 0 ? "Center" : "Right");
            // Keep currentStateIndex in sync (Center=0, Right=2) so StatefulRoot's own
            // first-frame inspector-sync doesn't yank every page back to state 0.
            sr.currentStateIndex = index == 0 ? 0 : 2;
            SetEase(sr, Ease.OutCubic);
            _pages.Add(sr);
            return page;
        }

        // ---------------- nav rail ----------------
        static float ItemY(int i) => -150 - i * 54;

        void BuildRail(Transform window)
        {
            // Rail rounded on the LEFT only (to meet the window's rounded left corners); its right
            // edge is a straight divider against the content, not a rounded 9-slice.
            var rail = UI.Panel("Rail", window, Palette.PanelAlt, rounded: false);
            rail.sprite = UI.RoundedRectLeft((int)RAIL_W, (int)WIN_H, 16f);
            rail.rectTransform.anchorMin = new Vector2(0, 0); rail.rectTransform.anchorMax = new Vector2(0, 1);
            rail.rectTransform.pivot = new Vector2(0, 0.5f); rail.rectTransform.sizeDelta = new Vector2(RAIL_W, 0);
            rail.rectTransform.anchoredPosition = Vector2.zero;
            _navSel = rail.gameObject.AddComponent<StatefulRoot>();

            var logo = UI.Panel("Logo", rail.transform, Palette.Accent, circle: true); // Simple sprite → smooth shimmer (9-slice breaks the sweep)
            UI.At(logo.rectTransform, 24, -34, 26, 26, new Vector2(0, 1), new Vector2(0, 1));
            ApplyMat(logo, "EasyStateful/UIShimmer");
            var word = UI.Label("Word", rail.transform, "Easy\nStateful", 19, Palette.Text,
                TextAlignmentOptions.TopLeft, FontStyles.Bold);
            word.lineSpacing = -8;
            UI.At(word.rectTransform, 60, -28, 130, 50, new Vector2(0, 1), new Vector2(0, 1));

            // selection pill (behind the items) — rounded on the LEFT only and run flush to the
            // rail's right edge, so the selected tab reads as connected to the content area.
            const float pillLeft = 12f, pillW = RAIL_W - pillLeft;
            var pill = UI.Panel("NavPill", rail.transform, new Color(Palette.Accent.r, Palette.Accent.g, Palette.Accent.b, 0.16f), rounded: false);
            pill.sprite = UI.RoundedRectLeft((int)pillW, 44, 14f);
            UI.At(pill.rectTransform, pillLeft, ItemY(0), pillW, 44, new Vector2(0, 1), new Vector2(0, 1));
            var bar = UI.Panel("Bar", pill.transform, Palette.Accent);
            UI.At(bar.rectTransform, 4, 0, 3, 22, new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            bar.raycastTarget = false;

            _navLabels = new TextMeshProUGUI[NavTitles.Length];
            for (int i = 0; i < NavTitles.Length; i++)
            {
                int idx = i;
                var hit = UI.Panel($"Nav{i}", rail.transform, new Color(0, 0, 0, 0), rounded: false);
                UI.At(hit.rectTransform, 0, ItemY(i), RAIL_W - 24, 44, new Vector2(0.5f, 1), new Vector2(0.5f, 1));
                UI.MakeButton(hit, () => SelectPage(idx));
                var lab = UI.Label("L", hit.transform, NavTitles[i], 16, Palette.TextDim,
                    TextAlignmentOptions.Left, FontStyles.Bold);
                UI.At(lab.rectTransform, 22, 0, 130, 44, new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                _navLabels[i] = lab;
            }

            var states = new List<State>();
            for (int i = 0; i < NavTitles.Length; i++)
                states.Add(St($"Nav{i}", P("NavPill", RECT, "m_AnchoredPosition.y", ItemY(i))));
            _navSel.statefulDataAsset = Data(states);
            _navSel.LoadFromAsset(_navSel.statefulDataAsset);
            _navSel.SnapToState("Nav0");
            SetEase(_navSel, Ease.OutCubic);

            var foot = UI.Label("RailFoot", rail.transform, "v1 · states + tweens", 11, Palette.TextDim,
                TextAlignmentOptions.Left);
            UI.At(foot.rectTransform, 24, 22, 160, 16, new Vector2(0, 0), new Vector2(0, 0));
        }

        void BuildTopBar(Transform content)
        {
            var bar = UI.Rect("TopBar", content);
            bar.anchorMin = new Vector2(0, 1); bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(0.5f, 1); bar.sizeDelta = new Vector2(0, TOP_H);
            bar.anchoredPosition = Vector2.zero;

            _topTitle = UI.Label("Title", bar, "Controls", 26, Palette.Text, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UI.At(_topTitle.rectTransform, 30, -16, 480, 34, new Vector2(0, 1), new Vector2(0, 1));
            _topSub = UI.Label("Sub", bar, "", 14, Palette.TextDim, TextAlignmentOptions.TopLeft);
            UI.At(_topSub.rectTransform, 32, -50, 480, 22, new Vector2(0, 1), new Vector2(0, 1));

            var menu = UI.Panel("MenuBtn", bar, Palette.PanelAlt);
            UI.At(menu.rectTransform, -28, -18, 44, 40, new Vector2(1, 1), new Vector2(1, 1));
            UI.MakeButton(menu, OpenDrawer);
            for (int i = 0; i < 3; i++)
            {
                var line = UI.Panel($"Bar{i}", menu.transform, Palette.Text, rounded: false);
                UI.At(line.rectTransform, 0, 7 - i * 7, 20, 2.5f);
                line.raycastTarget = false;
            }
        }

        void SelectPage(int to, bool instant = false)
        {
            if (!instant && to == _page) return;
            if (!instant)
            {
                bool fwd = to > _page;
                _pages[_page].TweenToState(fwd ? "Left" : "Right", 0.42f, Ease.InOutCubic);
                _pages[to].SnapToState(fwd ? "Right" : "Left");
                _pages[to].TweenToState("Center", 0.5f, Ease.OutCubic);
            }
            _page = to;
            _topTitle.text = NavTitles[to];
            _topSub.text = NavSubs[to];
            for (int i = 0; i < _navLabels.Length; i++)
                _navLabels[i].color = i == to ? Palette.Text : Palette.TextDim;
            if (instant) _navSel.SnapToState($"Nav{to}");
            else _navSel.TweenToState($"Nav{to}", 0.34f, Ease.OutCubic);
        }

        // ============================================================ FAB speed-dial
        static readonly (string label, Color color)[] FabActions =
        {
            ("T", Palette.Accent), ("D", Palette.Purple), ("M", Palette.Green),
        };
        static readonly string[] FabLabels = { "Show toast", "Open dialog", "Open menu" };

        void BuildFab(Transform parent)
        {
            // Full-window root so the speed-dial gets its own dimming backdrop.
            var root = UI.Rect("Fab", parent);
            UI.Stretch(root);
            _fab = root.gameObject.AddComponent<StatefulRoot>();

            var scrim = UI.Panel("Scrim", root, new Color(0, 0, 0, 0.66f), rounded: false);
            UI.Stretch(scrim.rectTransform);
            UI.MakeButton(scrim, CloseFab);

            System.Action[] acts = { () => { ShowToast("Hello from the FAB"); CloseFab(); },
                                     () => { OpenModal(); CloseFab(); },
                                     () => { OpenDrawer(); CloseFab(); } };
            var closed = new List<Property> { P("Main/Plus", TRANSFORM, "localEulerAngles.z", 0f),
                                              P("Scrim", "", "m_IsActive", 0f), P("Scrim", IMAGE, "m_Color.a", 0f) };
            var open = new List<Property> { P("Main/Plus", TRANSFORM, "localEulerAngles.z", 135f),
                                            P("Scrim", "", "m_IsActive", 1f), P("Scrim", IMAGE, "m_Color.a", 0.66f) };
            for (int i = 0; i < 3; i++)
            {
                var mini = UI.Panel($"Mini{i}", root, FabActions[i].color, circle: true);
                UI.At(mini.rectTransform, -58, 56, 46, 46, new Vector2(1, 0), new Vector2(0.5f, 0.5f));
                mini.transform.localScale = Vector3.zero;
                UI.MakeButton(mini, acts[i]);
                var ml = UI.Label("L", mini.transform, FabActions[i].label, 18, Palette.Hex("#0D1117"), TextAlignmentOptions.Center, FontStyles.Bold);
                UI.Stretch(ml.rectTransform);
                // descriptive label pill to the left (child of the mini → inherits its pop-in scale)
                var pill = UI.Panel("Pill", mini.transform, Palette.Hex("#161B22"));
                UI.At(pill.rectTransform, -86, 0, 112, 32);
                pill.raycastTarget = false;
                var pl = UI.Label("PL", pill.transform, FabLabels[i], 13, Palette.Text, TextAlignmentOptions.Center, FontStyles.Bold);
                UI.Stretch(pl.rectTransform);

                closed.Add(P($"Mini{i}", RECT, "m_AnchoredPosition.y", 56f));
                closed.Add(P($"Mini{i}", TRANSFORM, "m_LocalScale.x", 0f));
                closed.Add(P($"Mini{i}", TRANSFORM, "m_LocalScale.y", 0f));
                open.Add(P($"Mini{i}", RECT, "m_AnchoredPosition.y", 130f + i * 64f));
                open.Add(P($"Mini{i}", TRANSFORM, "m_LocalScale.x", 1f));
                open.Add(P($"Mini{i}", TRANSFORM, "m_LocalScale.y", 1f));
            }

            var main = UI.Panel("Main", root, Palette.Accent, circle: true);
            UI.At(main.rectTransform, -58, 56, 60, 60, new Vector2(1, 0), new Vector2(0.5f, 0.5f));
            UI.MakeButton(main, ToggleFab);
            var plus = UI.Rect("Plus", main.transform);
            UI.At(plus, 0, 0, 30, 30);
            var h = UI.Panel("H", plus, Palette.Hex("#0D1117"), rounded: false); UI.At(h.rectTransform, 0, 0, 22, 3); h.raycastTarget = false;
            var v = UI.Panel("V", plus, Palette.Hex("#0D1117"), rounded: false); UI.At(v.rectTransform, 0, 0, 3, 22); v.raycastTarget = false;

            _fab.statefulDataAsset = Data(new List<State> { St("Closed", closed.ToArray()), St("Open", open.ToArray()) });
            _fab.LoadFromAsset(_fab.statefulDataAsset);
            _fab.SnapToState("Closed");
            SetEase(_fab, Ease.OutBack);
        }

        void ToggleFab()
        {
            _fabOpen = !_fabOpen;
            _fab.TweenToState(_fabOpen ? "Open" : "Closed", 0.3f, _fabOpen ? Ease.OutBack : Ease.InCubic);
        }
        void CloseFab() { if (!_fabOpen) return; _fabOpen = false; _fab.TweenToState("Closed", 0.22f, Ease.InCubic); }

        // ============================================================ modal
        void BuildModal(Transform parent)
        {
            var root = UI.Rect("Modal", parent);
            UI.Stretch(root);
            _modal = root.gameObject.AddComponent<StatefulRoot>();

            var scrim = UI.Panel("Scrim", root, new Color(0, 0, 0, 0.6f), rounded: false);
            UI.Stretch(scrim.rectTransform);
            UI.MakeButton(scrim, CloseModal);

            var dialog = UI.Panel("Dialog", root, Palette.Panel);
            UI.At(dialog.rectTransform, 0, 0, 380, 200);
            dialog.gameObject.AddComponent<CanvasGroup>();
            var o = dialog.gameObject.AddComponent<Outline>();
            o.effectColor = Palette.Border; o.effectDistance = new Vector2(1, -1);

            var t = UI.Label("T", dialog.transform, "Publish your changes?", 21, Palette.Text, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UI.At(t.rectTransform, 28, -26, 324, 30, new Vector2(0, 1), new Vector2(0, 1));
            var b = UI.Label("B", dialog.transform, "Your followers get notified right away.\nDon’t worry — you can undo it anytime.", 15, Palette.TextDim, TextAlignmentOptions.TopLeft);
            UI.At(b.rectTransform, 28, -66, 324, 60, new Vector2(0, 1), new Vector2(0, 1));

            var cancel = UI.Panel("Cancel", dialog.transform, Palette.Track);
            UI.At(cancel.rectTransform, -150, 28, 120, 44, new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            UI.MakeButton(cancel, CloseModal);
            var cl = UI.Label("L", cancel.transform, "Maybe later", 15, Palette.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            UI.Stretch(cl.rectTransform);

            var ok = UI.Panel("Publish", dialog.transform, Palette.Green);
            UI.At(ok.rectTransform, 150, 28, 120, 44, new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            UI.MakeButton(ok, () => { CloseModal(); ShowToast("Changes published"); });
            var okl = UI.Label("L", ok.transform, "Publish", 15, Palette.Hex("#0D1117"), TextAlignmentOptions.Center, FontStyles.Bold);
            UI.Stretch(okl.rectTransform);

            _modal.statefulDataAsset = Data(new List<State>
            {
                St("Closed",
                    P("Scrim", "", "m_IsActive", 0f), P("Scrim", IMAGE, "m_Color.a", 0f),
                    P("Dialog", "", "m_IsActive", 0f), P("Dialog", CANVASGROUP, "alpha", 0f),
                    P("Dialog", TRANSFORM, "m_LocalScale.x", 0.92f), P("Dialog", TRANSFORM, "m_LocalScale.y", 0.92f)),
                St("Open",
                    P("Scrim", "", "m_IsActive", 1f), P("Scrim", IMAGE, "m_Color.a", 0.6f),
                    P("Dialog", "", "m_IsActive", 1f), P("Dialog", CANVASGROUP, "alpha", 1f),
                    P("Dialog", TRANSFORM, "m_LocalScale.x", 1f), P("Dialog", TRANSFORM, "m_LocalScale.y", 1f)),
            });
            _modal.LoadFromAsset(_modal.statefulDataAsset);
            _modal.SnapToState("Closed");
            SetEase(_modal, Ease.OutBack);
        }

        // NOTE: per-call ease is baked over by the property cache, so we pick the ease via SetEase
        // before each transition — OutBack to pop open, OutCubic to fade/shrink shut without overshoot.
        void OpenModal() { _modalOpen = true; SetEase(_modal, Ease.OutBack); _modal.TweenToState("Open", 0.3f); }
        void CloseModal() { if (!_modalOpen) return; _modalOpen = false; SetEase(_modal, Ease.OutCubic); _modal.TweenToState("Closed", 0.2f); }

        // ============================================================ drawer
        void BuildDrawer(Transform parent)
        {
            var root = UI.Rect("Drawer", parent);
            UI.Stretch(root);
            root.gameObject.AddComponent<RectMask2D>();
            _drawer = root.gameObject.AddComponent<StatefulRoot>();

            var scrim = UI.Panel("Scrim", root, new Color(0, 0, 0, 0.55f), rounded: false);
            UI.Stretch(scrim.rectTransform);
            UI.MakeButton(scrim, CloseDrawer);

            var sheet = UI.Panel("Sheet", root, Palette.Hex("#1B2230"));
            var srt = sheet.rectTransform;
            srt.anchorMin = new Vector2(1, 0); srt.anchorMax = new Vector2(1, 1);
            srt.pivot = new Vector2(1, 0.5f); srt.sizeDelta = new Vector2(300, 0);
            srt.anchoredPosition = new Vector2(320, 0);

            var st = UI.Label("ST", sheet.transform, "Quick settings", 20, Palette.Text, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UI.At(st.rectTransform, 24, -28, 252, 30, new Vector2(0, 1), new Vector2(0, 1));
            string[] items = { "Sound effects", "Haptics", "Auto-sync", "Reduced motion" };
            for (int i = 0; i < items.Length; i++)
            {
                int idx = i;
                bool on = _drawerOn[i];
                var row = UI.Panel($"Row{i}", sheet.transform, Palette.PanelAlt);
                UI.At(row.rectTransform, 0, -78 - i * 52, 252, 44, new Vector2(0.5f, 1), new Vector2(0.5f, 1));
                var l = UI.Label("L", row.transform, items[i], 15, Palette.Text, TextAlignmentOptions.Left);
                UI.At(l.rectTransform, 16, 0, 160, 44, new Vector2(0, 0.5f), new Vector2(0, 0.5f));

                var tog = UI.Panel("Tog", row.transform, on ? Palette.Green : Palette.Track);
                UI.At(tog.rectTransform, -16, 0, 46, 24, new Vector2(1, 0.5f), new Vector2(1, 0.5f));
                var tr = tog.gameObject.AddComponent<StatefulRoot>();
                UI.MakeButton(tog, () => ToggleDrawerRow(idx));
                var knob = UI.Panel("Knob", tog.transform, Color.white, circle: true);
                UI.At(knob.rectTransform, on ? 10 : -10, 0, 18, 18); knob.raycastTarget = false;
                tr.statefulDataAsset = Data(new List<State>
                {
                    St("Off", P("", IMAGE, "m_Color.r", Palette.Track.r), P("", IMAGE, "m_Color.g", Palette.Track.g), P("", IMAGE, "m_Color.b", Palette.Track.b),
                        P("Knob", RECT, "m_AnchoredPosition.x", -10f)),
                    St("On", P("", IMAGE, "m_Color.r", Palette.Green.r), P("", IMAGE, "m_Color.g", Palette.Green.g), P("", IMAGE, "m_Color.b", Palette.Green.b),
                        P("Knob", RECT, "m_AnchoredPosition.x", 10f)),
                });
                tr.LoadFromAsset(tr.statefulDataAsset);
                tr.SnapToState(on ? "On" : "Off");
                tr.currentStateIndex = on ? 1 : 0;
                SetEase(tr, Ease.OutBack);
                _drawerToggles[i] = tr;
            }
            var hint = UI.Label("Hint", sheet.transform, "Tap outside to close", 12, Palette.TextDim, TextAlignmentOptions.Center);
            UI.At(hint.rectTransform, 0, 24, 252, 18, new Vector2(0.5f, 0), new Vector2(0.5f, 0));

            _drawer.statefulDataAsset = Data(new List<State>
            {
                St("Closed", P("Scrim", "", "m_IsActive", 0f), P("Scrim", IMAGE, "m_Color.a", 0f),
                    P("Sheet", RECT, "m_AnchoredPosition.x", 320f)),
                St("Open", P("Scrim", "", "m_IsActive", 1f), P("Scrim", IMAGE, "m_Color.a", 0.55f),
                    P("Sheet", RECT, "m_AnchoredPosition.x", 0f)),
            });
            _drawer.LoadFromAsset(_drawer.statefulDataAsset);
            _drawer.SnapToState("Closed");
            SetEase(_drawer, Ease.OutCubic);
        }

        void ToggleDrawerRow(int i)
        {
            _drawerOn[i] = !_drawerOn[i];
            _drawerToggles[i].TweenToState(_drawerOn[i] ? "On" : "Off", 0.25f, Ease.OutBack);
        }

        void OpenDrawer() { _drawerOpen = true; _drawer.TweenToState("Open", 0.34f, Ease.OutCubic); }
        void CloseDrawer() { if (!_drawerOpen) return; _drawerOpen = false; _drawer.TweenToState("Closed", 0.3f, Ease.InOutCubic); }

        // ============================================================ toast
        void BuildToast(Transform parent)
        {
            var toast = UI.Panel("Toast", parent, Palette.Hex("#13283F"));
            UI.At(toast.rectTransform, 0, 40, 290, 46, new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            toast.raycastTarget = false;
            var o = toast.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.30f, 0.64f, 1f, 0.5f); o.effectDistance = new Vector2(1, -1);
            _toast = toast.gameObject.AddComponent<StatefulRoot>();
            var tcg = toast.gameObject.AddComponent<CanvasGroup>(); // fade bg + dot + label together
            tcg.blocksRaycasts = false;

            var dot = UI.Panel("Dot", toast.transform, Palette.Accent, circle: true);
            UI.At(dot.rectTransform, 22, 0, 12, 12);
            dot.raycastTarget = false;
            _toastLabel = UI.Label("L", toast.transform, "", 15, Palette.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            UI.At(_toastLabel.rectTransform, 44, 0, 230, 46, new Vector2(0, 0.5f), new Vector2(0, 0.5f));

            _toast.statefulDataAsset = Data(new List<State>
            {
                St("Hidden", P("", "", "m_IsActive", 0f), P("", CANVASGROUP, "alpha", 0f),
                    P("", RECT, "m_AnchoredPosition.y", 36f)),
                St("Shown", P("", "", "m_IsActive", 1f), P("", CANVASGROUP, "alpha", 1f),
                    P("", RECT, "m_AnchoredPosition.y", 86f)),
            });
            _toast.LoadFromAsset(_toast.statefulDataAsset);
            _toast.SnapToState("Hidden");
            SetEase(_toast, Ease.OutBack);
        }

        public void ShowToast(string text)
        {
            _toastLabel.text = text;
            _toastShown = true;
            _toastHideAt = Time.time + 1.9f;
            SetEase(_toast, Ease.OutBack);           // pop up
            _toast.TweenToState("Shown", 0.34f);
        }

        void Update()
        {
            if (_toastShown && Time.time >= _toastHideAt)
            {
                _toastShown = false;
                SetEase(_toast, Ease.OutCubic);       // slide down + fade, no overshoot
                _toast.TweenToState("Hidden", 0.3f);
            }
            PageUpdate();
        }

        // Pages may hook per-frame work (e.g. progress). Defined in a partial; keep a safe default.
        partial void PageUpdate();

        // ============================================================ data + canvas helpers
        internal static Property P(string path, string comp, string prop, float val) =>
            new Property { path = path, componentType = comp, propertyName = prop, value = val, objectReference = "" };

        internal static State St(string name, params Property[] props) =>
            new State { name = name, time = 0, properties = new List<Property>(props) };

        internal static StatefulDataAsset Data(List<State> states)
        {
            var a = ScriptableObject.CreateInstance<StatefulDataAsset>();
            a.stateMachine = new UIStateMachine { states = states };
            return a;
        }

        internal static Material Mat(string shader)
        {
            var sh = Shader.Find(shader);
            return sh != null ? new Material(sh) { hideFlags = HideFlags.DontSave } : null;
        }

        /// <summary>
        /// The per-call ease passed to TweenToState is baked over by the property cache, so the
        /// supported way to choose an ease is the instance override. We set it and rebuild the cache.
        /// (Color stays Linear via the global property-override rule, so this only affects motion.)
        /// </summary>
        internal static void SetEase(StatefulRoot sr, Ease e)
        {
            sr.overrideDefaultEase = true;
            sr.customDefaultEase = e;
            sr.InvalidatePropertyTransitionCache();
        }

        /// <summary>Assign a custom UI shader material and drive its animated time uniform.</summary>
        internal static Material ApplyMat(Image img, string shader)
        {
            var m = Mat(shader);
            if (m == null) return null;
            img.material = m;
            // Custom shaders sweep/gradient in UV space; a 9-sliced sprite breaks UVs into
            // segments, so force Simple (continuous 0-1 UVs) on anything shader-driven.
            img.type = Image.Type.Simple;
            img.gameObject.AddComponent<ShaderTime>();
            return m;
        }

        Canvas EnsureCanvas()
        {
            var canvas = FindFirstObjectByType<Canvas>();
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
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
            return canvas;
        }
    }
}
