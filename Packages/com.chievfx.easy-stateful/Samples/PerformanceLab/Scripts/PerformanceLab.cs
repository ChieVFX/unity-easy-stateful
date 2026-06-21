using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using EasyStateful.Runtime;

namespace EasyStateful.Samples.PerformanceLab
{
    /// <summary>
    /// Stateful Performance Lab.
    ///
    /// Spawns an adjustable swarm of <see cref="StatefulRoot"/> objects and lets you
    /// fire simultaneous state transitions through them, while a clean control window
    /// reports live FPS / frame-time so you can measure how the tween engine scales.
    ///
    /// Drop this on a GameObject under (or beside) a Canvas and press Play.
    /// The whole UI is built from code so the sample is a single, robust component.
    /// </summary>
    [AddComponentMenu("Easy Stateful/Performance Lab")]
    public class PerformanceLab : MonoBehaviour
    {
        [Header("Spawn range")]
        [SerializeField] int minObjects = 16;
        [SerializeField] int maxObjects = 4096;
        [SerializeField] int startObjects = 512;

        [Header("Transition")]
        [SerializeField, Range(0.05f, 2f)] float duration = 0.5f;
        [SerializeField, Range(0.05f, 2f)] float autoInterval = 0.4f;

        // ---- runtime data shared by every spawned card ----
        StatefulDataAsset _data;
        string[] _stateNames;
        int _stateIndex;

        readonly List<StatefulRoot> _cards = new List<StatefulRoot>();
        RectTransform _playField;
        int _targetCount;

        // ---- easing palette ----
        static readonly (Ease ease, string name)[] Eases =
        {
            (Ease.Linear, "Linear"), (Ease.OutQuad, "OutQuad"), (Ease.InOutQuad, "InOutQuad"),
            (Ease.OutCubic, "OutCubic"), (Ease.OutExpo, "OutExpo"), (Ease.OutBack, "OutBack"),
            (Ease.OutElastic, "OutElastic"), (Ease.OutBounce, "OutBounce"), (Ease.InOutSine, "InOutSine"),
        };
        int _easeIndex = 3;
        Ease CurrentEase => Eases[_easeIndex].ease;

        // ---- auto-cycle ----
        bool _auto;
        float _autoTimer;

        // ---- active-tween tracking (one batch shares start+duration) ----
        float _batchEnd;
        int _batchCount;
        int ActiveTweens => Time.time < _batchEnd ? _batchCount : 0;

        // ---- metrics ----
        float _smoothDelta = 0.016f;
        float _winMin = float.MaxValue, _winMax;
        float _winTimer;
        float _dispMin, _dispMax;

        // ---- UI references ----
        TextMeshProUGUI _fpsValue, _subStats, _objCounter, _tweenCounter;
        TextMeshProUGUI _objVal, _durVal, _intervalVal, _easeName, _autoLabel;
        FpsGraph _graph;
        Image _autoBtnImg;

        void Awake()
        {
            // Uncap so FPS reflects the real cost of the tween workload.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Application.runInBackground = true; // keep ticking even when the editor is unfocused

            _data = BuildStateData();
            _stateNames = new string[_data.stateMachine.states.Count];
            for (int i = 0; i < _stateNames.Length; i++)
                _stateNames[i] = _data.stateMachine.states[i].name;

            _targetCount = Mathf.Clamp(startObjects, minObjects, maxObjects);
        }

        void Start()
        {
            var canvas = EnsureCanvas();
            BuildUI(canvas.transform);
            Canvas.ForceUpdateCanvases();
            Spawn(_targetCount);
        }

        // -----------------------------------------------------------------
        //  Stateful data (built in code so the sample is self-contained)
        // -----------------------------------------------------------------
        const string TRANSFORM = "UnityEngine.Transform, UnityEngine.CoreModule";
        const string IMAGE = "UnityEngine.UI.Image, UnityEngine.UI";

        StatefulDataAsset BuildStateData()
        {
            var asset = ScriptableObject.CreateInstance<StatefulDataAsset>();
            var sm = new UIStateMachine();
            sm.states.Add(State("Calm",  0.55f, PerfTheme.Hex("#4DA3FF")));
            sm.states.Add(State("Grow",  1.00f, PerfTheme.Hex("#3FB950")));
            sm.states.Add(State("Alert", 0.80f, PerfTheme.Hex("#F85149")));
            sm.states.Add(State("Glow",  0.92f, PerfTheme.Hex("#D29922")));
            sm.states.Add(State("Cool",  0.45f, PerfTheme.Hex("#A371F7")));
            asset.stateMachine = sm;
            return asset;
        }

        static State State(string name, float scale, Color c)
        {
            return new State
            {
                name = name,
                time = 0,
                properties = new List<Property>
                {
                    Prop(TRANSFORM, "m_LocalScale.x", scale),
                    Prop(TRANSFORM, "m_LocalScale.y", scale),
                    Prop(IMAGE, "m_Color.r", c.r),
                    Prop(IMAGE, "m_Color.g", c.g),
                    Prop(IMAGE, "m_Color.b", c.b),
                    Prop(IMAGE, "m_Color.a", 1f),
                }
            };
        }

        static Property Prop(string comp, string name, float val) =>
            new Property { path = "", componentType = comp, propertyName = name, value = val, objectReference = "" };

        // -----------------------------------------------------------------
        //  Spawning
        // -----------------------------------------------------------------
        void Spawn(int count)
        {
            ClearCards();
            count = Mathf.Clamp(count, 0, maxObjects);
            if (count == 0) return;

            float w = _playField.rect.width;
            float h = _playField.rect.height;
            if (w <= 1f || h <= 1f) { w = 1400f; h = 900f; }

            int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count * (w / h))));
            int rows = Mathf.CeilToInt((float)count / cols);
            float cell = Mathf.Min(w / cols, h / rows);
            float size = Mathf.Max(2f, cell * 0.82f);
            float startX = -w * 0.5f + cell * 0.5f;
            float startY = h * 0.5f - cell * 0.5f;

            for (int i = 0; i < count; i++)
            {
                int col = i % cols;
                int row = i / cols;

                var go = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(StatefulRoot));
                var rt = (RectTransform)go.transform;
                rt.SetParent(_playField, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2(startX + col * cell, startY - row * cell);

                var img = go.GetComponent<Image>();
                img.sprite = PerfUI.Round;
                img.type = Image.Type.Simple; // 1 quad per card; corners stay softly rounded
                img.raycastTarget = false; // huge raycast cost otherwise; cards aren't clickable

                var root = go.GetComponent<StatefulRoot>();
                root.statefulDataAsset = _data;
                root.LoadFromAsset(_data);
                int initial = i % _stateNames.Length;
                // Keep currentStateIndex in sync so StatefulRoot's own inspector-driven
                // Update doesn't snap every card back to state 0 on the first frame.
                root.currentStateIndex = initial;
                root.SnapToState(_stateNames[initial]);

                _cards.Add(root);
            }
        }

        void ClearCards()
        {
            for (int i = 0; i < _cards.Count; i++)
                if (_cards[i] != null) Destroy(_cards[i].gameObject);
            _cards.Clear();
            _batchEnd = 0f;
        }

        // -----------------------------------------------------------------
        //  Driving transitions
        // -----------------------------------------------------------------
        void TransitionAll()
        {
            if (_cards.Count == 0 || _stateNames.Length == 0) return;
            _stateIndex = (_stateIndex + 1) % _stateNames.Length;
            string state = _stateNames[_stateIndex];
            var ease = CurrentEase;
            for (int i = 0; i < _cards.Count; i++)
                _cards[i].TweenToState(state, duration, ease);

            _batchEnd = Time.time + duration;
            _batchCount = _cards.Count;
        }

        // -----------------------------------------------------------------
        //  Per-frame metrics + auto cycle
        // -----------------------------------------------------------------
        void Update()
        {
            float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            _smoothDelta = Mathf.Lerp(_smoothDelta, dt, 0.1f);

            float inst = 1f / dt;
            _winMin = Mathf.Min(_winMin, inst);
            _winMax = Mathf.Max(_winMax, inst);
            _winTimer += dt;
            if (_winTimer >= 1f)
            {
                _dispMin = _winMin; _dispMax = _winMax;
                _winMin = float.MaxValue; _winMax = 0f; _winTimer = 0f;
            }

            if (_auto && _cards.Count > 0)
            {
                _autoTimer += dt;
                if (_autoTimer >= autoInterval)
                {
                    _autoTimer = 0f;
                    TransitionAll();
                }
            }

            UpdateReadouts(dt);
        }

        void UpdateReadouts(float dt)
        {
            float fps = 1f / _smoothDelta;
            float ms = _smoothDelta * 1000f;

            if (_fpsValue != null)
            {
                _fpsValue.text = Mathf.RoundToInt(fps).ToString();
                _fpsValue.color = fps >= 58f ? PerfTheme.Good : fps >= 30f ? PerfTheme.Warn : PerfTheme.Bad;
            }
            if (_subStats != null)
                _subStats.text = $"{ms:0.0} ms   ·   min {Mathf.RoundToInt(_dispMin)}   max {Mathf.RoundToInt(_dispMax)}";
            if (_objCounter != null)
                _objCounter.text = $"Objects <b><color=#E6EDF3>{_cards.Count}</color></b>";
            if (_tweenCounter != null)
                _tweenCounter.text = $"Active <b><color=#E6EDF3>{ActiveTweens}</color></b>";

            if (_graph != null) _graph.Push(dt * 1000f);
        }

        // -----------------------------------------------------------------
        //  UI construction
        // -----------------------------------------------------------------
        Canvas EnsureCanvas()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
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
                // New Input System backend.
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
            return canvas;
        }

        void BuildUI(Transform canvas)
        {
            // Full-screen gradient background for a bit of depth.
            var bg = PerfUI.Panel("PerfLab_BG", canvas, Color.white, rounded: false);
            bg.sprite = PerfUI.MakeVerticalGradient(PerfTheme.Hex("#11161D"), PerfTheme.Hex("#080A0E"));
            bg.type = Image.Type.Simple;
            bg.raycastTarget = false;
            PerfUI.Stretch(bg.rectTransform);

            // Play field (cards live here), inset to leave room for the window on the right.
            _playField = PerfUI.Rect("PlayField", canvas);
            PerfUI.Stretch(_playField);
            _playField.offsetMin = new Vector2(24, 24);
            _playField.offsetMax = new Vector2(-490, -24);

            BuildWindow(canvas);
        }

        void BuildWindow(Transform canvas)
        {
            var window = PerfUI.Panel("ControlWindow", canvas, PerfTheme.Panel);
            window.rectTransform.anchorMin = new Vector2(1, 0);
            window.rectTransform.anchorMax = new Vector2(1, 1);
            window.rectTransform.pivot = new Vector2(1, 0.5f);
            window.rectTransform.sizeDelta = new Vector2(440, -48);
            window.rectTransform.anchoredPosition = new Vector2(-24, 0);

            var outline = window.gameObject.AddComponent<Outline>();
            outline.effectColor = PerfTheme.Border;
            outline.effectDistance = new Vector2(1, -1);

            PerfUI.VLayout(window.gameObject, 18, 14);

            // ---- Header ----
            var header = PerfUI.Rect("Header", window.transform);
            PerfUI.VLayout(header.gameObject, 0, 2);
            PerfUI.Layout(header.gameObject, prefH: 50);
            PerfUI.Label("Title", header, "Stateful Performance Lab", 22, PerfTheme.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            PerfUI.Label("Sub", header, "Stress-test simultaneous state transitions", 13, PerfTheme.TextDim);

            var rule = PerfUI.Panel("Rule", window.transform, PerfTheme.Accent, rounded: false);
            PerfUI.Layout(rule.gameObject, prefH: 2);

            BuildMetrics(window.transform);
            BuildControls(window.transform);

            // Footer hint pinned to bottom via a flexible spacer.
            var spacer = PerfUI.Rect("Spacer", window.transform);
            PerfUI.Layout(spacer.gameObject, prefH: 0).flexibleHeight = 1;
            var foot = PerfUI.Label("Footer", window.transform,
                "Each card is a StatefulRoot · vSync off", 11, PerfTheme.TextDim);
            PerfUI.Layout(foot.gameObject, prefH: 16);
        }

        void BuildMetrics(Transform parent)
        {
            var panel = PerfUI.Panel("Metrics", parent, PerfTheme.PanelAlt);
            PerfUI.VLayout(panel.gameObject, 14, 6);

            PerfUI.Label("Cap", panel.transform, "PERFORMANCE", 11, PerfTheme.TextDim,
                TextAlignmentOptions.Left, FontStyles.Bold);

            _fpsValue = PerfUI.Label("FPS", panel.transform, "0", 58, PerfTheme.Good,
                TextAlignmentOptions.Left, FontStyles.Bold);
            PerfUI.Layout(_fpsValue.gameObject, prefH: 62);

            _subStats = PerfUI.Label("Sub", panel.transform, "0.0 ms", 13, PerfTheme.TextDim);
            PerfUI.Layout(_subStats.gameObject, prefH: 18);

            var graphGo = PerfUI.Rect("Graph", panel.transform);
            var raw = graphGo.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            _graph = graphGo.gameObject.AddComponent<FpsGraph>();
            PerfUI.Layout(graphGo.gameObject, prefH: 86);

            var counters = PerfUI.Rect("Counters", panel.transform);
            PerfUI.HLayout(counters.gameObject, 8);
            PerfUI.Layout(counters.gameObject, prefH: 20);
            _objCounter = PerfUI.Label("Obj", counters, "Objects 0", 14, PerfTheme.TextDim);
            PerfUI.Layout(_objCounter.gameObject, flexW: 1);
            _tweenCounter = PerfUI.Label("Tw", counters, "Active 0", 14, PerfTheme.TextDim,
                TextAlignmentOptions.Right);
            PerfUI.Layout(_tweenCounter.gameObject, flexW: 1);

            PerfUI.Layout(panel.gameObject, prefH: 240);
        }

        void BuildControls(Transform parent)
        {
            var panel = PerfUI.Panel("Controls", parent, PerfTheme.PanelAlt);
            PerfUI.VLayout(panel.gameObject, 14, 10);
            PerfUI.Layout(panel.gameObject, prefH: 300);

            PerfUI.Label("Cap", panel.transform, "CONTROLS", 11, PerfTheme.TextDim,
                TextAlignmentOptions.Left, FontStyles.Bold);

            // Objects slider.
            var objRow = Row(panel.transform, "Objects");
            var objSlider = PerfUI.Slider("ObjSlider", objRow, minObjects, maxObjects, _targetCount, true,
                v => { _targetCount = Mathf.RoundToInt(v); _objVal.text = _targetCount.ToString(); });
            PerfUI.Layout(objSlider.gameObject, flexW: 1, minW: 60);
            _objVal = PerfUI.Label("Val", objRow, _targetCount.ToString(), 14, PerfTheme.Text,
                TextAlignmentOptions.Right);
            PerfUI.Layout(_objVal.gameObject, prefW: 52, flexW: 0);

            // Respawn / Clear.
            var btnRow = Row(panel.transform, null);
            var respawn = PerfUI.Button("Respawn", btnRow, PerfTheme.AccentDim, PerfTheme.Text,
                () => Spawn(_targetCount));
            PerfUI.Layout(respawn.gameObject, flexW: 1);
            var clear = PerfUI.Button("Clear", btnRow, PerfTheme.Track, PerfTheme.Text, ClearCards);
            PerfUI.Layout(clear.gameObject, flexW: 1);

            // Duration slider.
            var durRow = Row(panel.transform, "Duration");
            var durSlider = PerfUI.Slider("DurSlider", durRow, 0.05f, 2f, duration, false,
                v => { duration = v; _durVal.text = $"{duration:0.00}s"; });
            PerfUI.Layout(durSlider.gameObject, flexW: 1, minW: 60);
            _durVal = PerfUI.Label("Val", durRow, $"{duration:0.00}s", 14, PerfTheme.Text,
                TextAlignmentOptions.Right);
            PerfUI.Layout(_durVal.gameObject, prefW: 52, flexW: 0);

            // Easing cycler.
            var easeRow = Row(panel.transform, "Easing");
            var prev = PerfUI.Button("<", easeRow, PerfTheme.Track, PerfTheme.Text, () => CycleEase(-1), 18);
            PerfUI.Layout(prev.gameObject, prefW: 34, flexW: 0);
            _easeName = PerfUI.Label("Name", easeRow, Eases[_easeIndex].name, 15, PerfTheme.Accent,
                TextAlignmentOptions.Center, FontStyles.Bold);
            PerfUI.Layout(_easeName.gameObject, flexW: 1);
            var next = PerfUI.Button(">", easeRow, PerfTheme.Track, PerfTheme.Text, () => CycleEase(1), 18);
            PerfUI.Layout(next.gameObject, prefW: 34, flexW: 0);

            // Interval slider.
            var intRow = Row(panel.transform, "Interval");
            var intSlider = PerfUI.Slider("IntSlider", intRow, 0.05f, 2f, autoInterval, false,
                v => { autoInterval = v; _intervalVal.text = $"{autoInterval:0.00}s"; });
            PerfUI.Layout(intSlider.gameObject, flexW: 1, minW: 60);
            _intervalVal = PerfUI.Label("Val", intRow, $"{autoInterval:0.00}s", 14, PerfTheme.Text,
                TextAlignmentOptions.Right);
            PerfUI.Layout(_intervalVal.gameObject, prefW: 52, flexW: 0);

            // Transition All + Auto toggle.
            var actionRow = Row(panel.transform, null);
            PerfUI.Layout(actionRow.gameObject, prefH: 44);
            var trigger = PerfUI.Button("Transition All", actionRow, PerfTheme.Accent, PerfTheme.Hex("#0D1117"),
                TransitionAll, 17);
            PerfUI.Layout(trigger.gameObject, flexW: 1);
            _autoBtnImg = PerfUI.Panel("AutoBtn", actionRow, PerfTheme.Track);
            var autoBtn = _autoBtnImg.gameObject.AddComponent<Button>();
            autoBtn.targetGraphic = _autoBtnImg;
            autoBtn.onClick.AddListener(ToggleAuto);
            PerfUI.Layout(_autoBtnImg.gameObject, prefW: 110, flexW: 0);
            _autoLabel = PerfUI.Label("Lbl", _autoBtnImg.transform, "Auto: Off", 14, PerfTheme.TextDim,
                TextAlignmentOptions.Center, FontStyles.Bold);
            PerfUI.Stretch(_autoLabel.rectTransform);
        }

        RectTransform Row(Transform parent, string label)
        {
            var row = PerfUI.Rect("Row", parent);
            PerfUI.HLayout(row.gameObject, 8);
            PerfUI.Layout(row.gameObject, prefH: 30);
            if (!string.IsNullOrEmpty(label))
            {
                var l = PerfUI.Label("Label", row, label, 14, PerfTheme.TextDim);
                PerfUI.Layout(l.gameObject, prefW: 78, flexW: 0);
            }
            return row;
        }

        void CycleEase(int dir)
        {
            _easeIndex = (_easeIndex + dir + Eases.Length) % Eases.Length;
            _easeName.text = Eases[_easeIndex].name;
        }

        void ToggleAuto()
        {
            _auto = !_auto;
            _autoTimer = 0f;
            _autoLabel.text = _auto ? "Auto: On" : "Auto: Off";
            _autoLabel.color = _auto ? PerfTheme.Hex("#0D1117") : PerfTheme.TextDim;
            _autoBtnImg.color = _auto ? PerfTheme.Good : PerfTheme.Track;
        }
    }
}
