using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EasyStateful.Runtime;

namespace EasyStateful.Samples.Showcase
{
    /// <summary>
    /// Drives a hologram shader's <c>_Progress</c> from a plain C# property so a
    /// StatefulRoot can tween it — i.e. animating a CUSTOM script value, not a
    /// built-in component field.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class HologramController : MonoBehaviour
    {
        static readonly int PID = Shader.PropertyToID("_Progress");
        static readonly int TID = Shader.PropertyToID("_AnimTime");

        public TMP_Text progressLabel;
        Image _img;
        float _progress = 1f;

        // This is what StatefulRoot tweens.
        public float Progress
        {
            get => _progress;
            set { _progress = value; Apply(); }
        }

        void Awake() { _img = GetComponent<Image>(); }

        void Apply()
        {
            var m = _img != null ? _img.material : null;
            if (m != null) m.SetFloat(PID, _progress);
        }

        void Update()
        {
            var m = _img != null ? _img.material : null;
            if (m != null) { m.SetFloat(PID, _progress); m.SetFloat(TID, Time.unscaledTime); }
            if (progressLabel != null) progressLabel.text = "Progress  <b><color=#39D0FF>" + _progress.ToString("0.00") + "</color></b>";
        }
    }

    /// <summary>A trivial script value (a float field) for the count-up demo.</summary>
    public class CountUp : MonoBehaviour
    {
        public float Value;
        public TMP_Text label;
        void Update() { if (label != null) label.text = Mathf.RoundToInt(Value).ToString("N0"); }
    }

    public partial class Showcase
    {
        const string HOLO  = "EasyStateful.Samples.Showcase.HologramController, Assembly-CSharp";
        const string COUNT = "EasyStateful.Samples.Showcase.CountUp, Assembly-CSharp";

        StatefulRoot _holo; bool _holoOn = true;
        StatefulRoot _count; bool _counted;

        void BuildCustomPage(RectTransform page)
        {
            BuildHologramCard(PageCard(page, 30, -22, 740, 366, "MATERIALIZE — TWEEN A CUSTOM SHADER VALUE").transform);
            BuildCountCard(PageCard(page, 30, -402, 740, 142, "COUNT-UP — TWEEN A SCRIPT FIELD").transform);
        }

        // ---------------- hologram materialize ----------------
        void BuildHologramCard(Transform card)
        {
            // stage
            var stage = UI.Panel("Stage", card, Palette.Hex("#090C12"));
            UI.At(stage.rectTransform, 20, -42, 300, 300, new Vector2(0, 1), new Vector2(0, 1));
            var so = stage.gameObject.AddComponent<Outline>();
            so.effectColor = new Color(0.22f, 0.82f, 1f, 0.25f); so.effectDistance = new Vector2(1, -1);

            // the holographic subject (a star) — its own StatefulRoot + HologramController
            var subject = UI.Panel("Subject", stage.transform, Color.white);
            subject.sprite = UI.Star; subject.type = Image.Type.Simple; subject.raycastTarget = false;
            UI.At(subject.rectTransform, 0, 0, 180, 180);
            var holoMat = Mat("EasyStateful/UIHologram");
            if (holoMat != null) subject.material = holoMat;
            var ctrl = subject.gameObject.AddComponent<HologramController>();
            _holo = subject.gameObject.AddComponent<StatefulRoot>();

            // explanation + live readout on the right
            var desc = UI.Label("Desc", card, "A StatefulRoot tweens <b>HologramController.Progress</b>\nfrom 0 → 1. The script feeds that value into the\nhologram shader’s <color=#39D0FF>_Progress</color> uniform each frame.", 15, Palette.TextDim, TextAlignmentOptions.TopLeft);
            UI.At(desc.rectTransform, 350, -52, 372, 90, new Vector2(0, 1), new Vector2(0, 1));

            var readout = UI.Label("Readout", card, "Progress 1.00", 26, Palette.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            UI.At(readout.rectTransform, 350, -150, 372, 34, new Vector2(0, 1), new Vector2(0, 1));
            ctrl.progressLabel = readout;

            var matBtn = UI.Panel("Materialize", card, Palette.Hex("#1F6FEB"));
            UI.At(matBtn.rectTransform, 350, -206, 176, 46, new Vector2(0, 1), new Vector2(0, 1));
            UI.MakeButton(matBtn, () => SetHologram(true));
            var mbl = UI.Label("L", matBtn.transform, "Materialize", 15, Palette.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            UI.Stretch(mbl.rectTransform);

            var deBtn = UI.Panel("Dematerialize", card, Palette.Track);
            UI.At(deBtn.rectTransform, 538, -206, 176, 46, new Vector2(0, 1), new Vector2(0, 1));
            UI.MakeButton(deBtn, () => SetHologram(false));
            var dbl = UI.Label("L", deBtn.transform, "Dematerialize", 15, Palette.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            UI.Stretch(dbl.rectTransform);

            _holo.statefulDataAsset = Data(new List<State>
            {
                St("Dematerialized", P("", HOLO, "Progress", 0f)),
                St("Materialized",   P("", HOLO, "Progress", 1f)),
            });
            _holo.LoadFromAsset(_holo.statefulDataAsset);
            _holo.SnapToState("Materialized");
            _holo.currentStateIndex = 1; // Materialized is index 1
            SetEase(_holo, Ease.InOutCubic);
        }

        void SetHologram(bool on)
        {
            _holoOn = on;
            _holo.TweenToState(on ? "Materialized" : "Dematerialized", 1.1f, Ease.InOutCubic);
        }

        // ---------------- count-up ----------------
        void BuildCountCard(Transform card)
        {
            var root = UI.Rect("Count", card);
            UI.Stretch(root);
            _count = root.gameObject.AddComponent<StatefulRoot>();
            var cu = root.gameObject.AddComponent<CountUp>();

            var number = UI.Label("Number", root, "0", 46, Palette.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            UI.At(number.rectTransform, 24, -44, 320, 56, new Vector2(0, 1), new Vector2(0, 1));
            cu.label = number;
            var sub = UI.Label("Sub", root, "CountUp.Value, tweened by the same engine", 14, Palette.TextDim, TextAlignmentOptions.Left);
            UI.At(sub.rectTransform, 26, -98, 360, 20, new Vector2(0, 1), new Vector2(0, 1));

            var countBtn = UI.Panel("CountBtn", root, Palette.Hex("#1F6FEB"));
            UI.At(countBtn.rectTransform, -200, 0, 170, 46, new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            UI.MakeButton(countBtn, () => SetCount(true));
            var cbl = UI.Label("L", countBtn.transform, "Count to 2,500", 15, Palette.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            UI.Stretch(cbl.rectTransform);

            var resetBtn = UI.Panel("ResetBtn", root, Palette.Track);
            UI.At(resetBtn.rectTransform, -24, 0, 150, 46, new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            UI.MakeButton(resetBtn, () => SetCount(false));
            var rbl = UI.Label("L", resetBtn.transform, "Reset", 15, Palette.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            UI.Stretch(rbl.rectTransform);

            _count.statefulDataAsset = Data(new List<State>
            {
                St("Zero", P("", COUNT, "Value", 0f)),
                St("Full", P("", COUNT, "Value", 2500f)),
            });
            _count.LoadFromAsset(_count.statefulDataAsset);
            _count.SnapToState("Zero");
            SetEase(_count, Ease.OutCubic);
        }

        void SetCount(bool full)
        {
            _counted = full;
            _count.TweenToState(full ? "Full" : "Zero", full ? 1.5f : 0.5f, Ease.OutCubic);
        }
    }
}
