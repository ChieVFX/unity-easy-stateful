using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EasyStateful.Runtime;

namespace EasyStateful.Samples.Showcase
{
    // Controls page — a grid of inputs, each an animated StatefulRoot.
    public partial class Showcase
    {
        static readonly string[] SegTabs = { "Daily", "Weekly", "Yearly" };
        static readonly Color[] SegColors = { Palette.Accent, Palette.Green, Palette.Purple };
        StatefulRoot _seg;
        TextMeshProUGUI _segValue;
        TextMeshProUGUI[] _segLabels;
        int _segTab;

        StatefulRoot _ctlToggle; TextMeshProUGUI _ctlToggleVal; bool _ctlToggleOn;
        StatefulRoot _check; bool _checkOn;
        StatefulRoot _stars; int _rating = 3;
        StatefulRoot _stepper; TextMeshProUGUI _stepLabel; int _stepValue = 2;

        Image Card(RectTransform page, float x, float y, float w, float h, string caption)
        {
            var card = UI.Panel("Card", page, Palette.PanelAlt);
            UI.At(card.rectTransform, x, y, w, h, new Vector2(0, 1), new Vector2(0, 1));
            var cap = UI.Label("Cap", card.transform, caption, 11, Palette.TextDim, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UI.At(cap.rectTransform, 18, -14, w - 36, 16, new Vector2(0, 1), new Vector2(0, 1));
            return card;
        }

        void BuildControlsPage(RectTransform page)
        {
            BuildSegmentedCard(Card(page, 30, -22, 740, 150, "SEGMENTED CONTROL").transform);
            BuildToggleCard(Card(page, 30, -188, 360, 150, "TOGGLE").transform);
            BuildCheckboxCard(Card(page, 410, -188, 360, 150, "ANIMATED CHECKBOX").transform);
            BuildStarsCard(Card(page, 30, -354, 360, 150, "STAR RATING").transform);
            BuildStepperCard(Card(page, 410, -354, 360, 150, "STEPPER").transform);
        }

        // ---------------- segmented ----------------
        void BuildSegmentedCard(Transform card)
        {
            var root = UI.Rect("Segmented", card);
            UI.Stretch(root);
            _seg = root.gameObject.AddComponent<StatefulRoot>();

            var bar = UI.Panel("Bar", root, Palette.Track);
            UI.At(bar.rectTransform, 18, -40, 360, 46, new Vector2(0, 1), new Vector2(0, 1));
            var hi = UI.Panel("Highlight", bar.transform, Palette.Accent);
            UI.At(hi.rectTransform, -120, 0, 112, 38);
            _segLabels = new TextMeshProUGUI[3];
            float[] sx = { -120, 0, 120 };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var hit = UI.Panel($"Seg{i}", bar.transform, new Color(0, 0, 0, 0), rounded: false);
                UI.At(hit.rectTransform, sx[i], 0, 120, 46);
                UI.MakeButton(hit, () => SelectSeg(idx));
                var l = UI.Label("L", hit.transform, SegTabs[i], 15, Palette.TextDim, TextAlignmentOptions.Center, FontStyles.Bold);
                UI.Stretch(l.rectTransform);
                _segLabels[i] = l;
            }

            // morphing emblem on the right
            var emblem = UI.Panel("Emblem", root, Palette.Accent);
            UI.At(emblem.rectTransform, -58, -62, 78, 78, new Vector2(1, 1), new Vector2(0.5f, 0.5f));
            ApplyMat(emblem, "EasyStateful/UIShimmer");
            var pip = UI.Panel("Pip", emblem.transform, new Color(1, 1, 1, 0.9f), circle: true);
            UI.At(pip.rectTransform, 20, 20, 16, 16); pip.raycastTarget = false;

            _segValue = UI.Label("Val", root, "Daily summary", 22, Palette.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            UI.At(_segValue.rectTransform, 18, -104, 460, 30, new Vector2(0, 1), new Vector2(0, 1));

            var states = new List<State>();
            for (int i = 0; i < 3; i++)
            {
                var c = SegColors[i];
                states.Add(St($"Seg{i}",
                    P("Bar/Highlight", RECT, "m_AnchoredPosition.x", sx[i]),
                    P("Bar/Highlight", IMAGE, "m_Color.r", c.r), P("Bar/Highlight", IMAGE, "m_Color.g", c.g), P("Bar/Highlight", IMAGE, "m_Color.b", c.b),
                    P("Emblem", IMAGE, "m_Color.r", c.r), P("Emblem", IMAGE, "m_Color.g", c.g), P("Emblem", IMAGE, "m_Color.b", c.b),
                    P("Emblem", TRANSFORM, "m_LocalScale.x", 0.84f + i * 0.12f), P("Emblem", TRANSFORM, "m_LocalScale.y", 0.84f + i * 0.12f),
                    P("Emblem", TRANSFORM, "localEulerAngles.z", i * 90f)));
            }
            _seg.statefulDataAsset = Data(states);
            _seg.LoadFromAsset(_seg.statefulDataAsset);
            _seg.SnapToState("Seg0");
            SetEase(_seg, Ease.OutCubic);
            SelectSeg(0, true);
        }

        void SelectSeg(int i, bool instant = false)
        {
            _segTab = i;
            _segValue.text = SegTabs[i] + " summary";
            for (int s = 0; s < 3; s++) _segLabels[s].color = s == i ? Palette.Hex("#0D1117") : Palette.TextDim;
            if (instant) _seg.SnapToState($"Seg{i}");
            else _seg.TweenToState($"Seg{i}", 0.4f, Ease.OutCubic);
        }

        // ---------------- toggle ----------------
        void BuildToggleCard(Transform card)
        {
            var label = UI.Label("TL", card, "Wi-Fi", 17, Palette.Text, TextAlignmentOptions.Left);
            UI.At(label.rectTransform, 18, -52, 200, 26, new Vector2(0, 1), new Vector2(0, 1));
            _ctlToggleVal = UI.Label("TV", card, "Off", 13, Palette.TextDim, TextAlignmentOptions.Left);
            UI.At(_ctlToggleVal.rectTransform, 18, -80, 200, 20, new Vector2(0, 1), new Vector2(0, 1));

            var track = UI.Panel("Toggle", card, Palette.Track);
            UI.At(track.rectTransform, -24, -64, 72, 38, new Vector2(1, 1), new Vector2(1, 1));
            _ctlToggle = track.gameObject.AddComponent<StatefulRoot>();
            UI.MakeButton(track, ToggleWifi);
            var knob = UI.Panel("Knob", track.transform, Color.white, circle: true);
            UI.At(knob.rectTransform, -16, 0, 28, 28); knob.raycastTarget = false;

            _ctlToggle.statefulDataAsset = Data(new List<State>
            {
                St("Off", P("", IMAGE, "m_Color.r", Palette.Track.r), P("", IMAGE, "m_Color.g", Palette.Track.g), P("", IMAGE, "m_Color.b", Palette.Track.b),
                    P("Knob", RECT, "m_AnchoredPosition.x", -16f)),
                St("On", P("", IMAGE, "m_Color.r", Palette.Green.r), P("", IMAGE, "m_Color.g", Palette.Green.g), P("", IMAGE, "m_Color.b", Palette.Green.b),
                    P("Knob", RECT, "m_AnchoredPosition.x", 16f)),
            });
            _ctlToggle.LoadFromAsset(_ctlToggle.statefulDataAsset);
            _ctlToggle.SnapToState("Off");
            SetEase(_ctlToggle, Ease.OutBack);
        }

        void ToggleWifi()
        {
            _ctlToggleOn = !_ctlToggleOn;
            _ctlToggleVal.text = _ctlToggleOn ? "Connected" : "Off";
            _ctlToggleVal.color = _ctlToggleOn ? Palette.Green : Palette.TextDim;
            _ctlToggle.TweenToState(_ctlToggleOn ? "On" : "Off", 0.28f, Ease.OutBack);
        }

        // ---------------- checkbox ----------------
        void BuildCheckboxCard(Transform card)
        {
            var label = UI.Label("CL", card, "I agree to the terms", 16, Palette.Text, TextAlignmentOptions.Left);
            UI.At(label.rectTransform, 70, -64, 260, 26, new Vector2(0, 1), new Vector2(0, 1));

            var box = UI.Panel("Check", card, Palette.Track);
            UI.At(box.rectTransform, 22, -52, 40, 40, new Vector2(0, 1), new Vector2(0, 1));
            _check = box.gameObject.AddComponent<StatefulRoot>();
            UI.MakeButton(box, ToggleCheck);
            var mark = UI.Panel("Mark", box.transform, Color.white, rounded: false);
            mark.sprite = UI.Check; mark.type = Image.Type.Simple; mark.raycastTarget = false;
            UI.At(mark.rectTransform, 0, 0, 34, 34);
            mark.transform.localScale = Vector3.zero;

            _check.statefulDataAsset = Data(new List<State>
            {
                St("Off", P("", IMAGE, "m_Color.r", Palette.Track.r), P("", IMAGE, "m_Color.g", Palette.Track.g), P("", IMAGE, "m_Color.b", Palette.Track.b),
                    P("Mark", TRANSFORM, "m_LocalScale.x", 0f), P("Mark", TRANSFORM, "m_LocalScale.y", 0f)),
                St("On", P("", IMAGE, "m_Color.r", Palette.Accent.r), P("", IMAGE, "m_Color.g", Palette.Accent.g), P("", IMAGE, "m_Color.b", Palette.Accent.b),
                    P("Mark", TRANSFORM, "m_LocalScale.x", 1f), P("Mark", TRANSFORM, "m_LocalScale.y", 1f)),
            });
            _check.LoadFromAsset(_check.statefulDataAsset);
            _check.SnapToState("Off");
            SetEase(_check, Ease.OutBack);
        }

        void ToggleCheck()
        {
            _checkOn = !_checkOn;
            _check.TweenToState(_checkOn ? "On" : "Off", 0.3f, _checkOn ? Ease.OutBack : Ease.InCubic);
        }

        // ---------------- star rating ----------------
        void BuildStarsCard(Transform card)
        {
            var root = UI.Rect("Stars", card);
            UI.Stretch(root);
            _stars = root.gameObject.AddComponent<StatefulRoot>();

            for (int i = 0; i < 5; i++)
            {
                int idx = i;
                var star = UI.Panel($"Star{i}", root, Palette.Track, rounded: false);
                star.sprite = UI.Star; star.type = Image.Type.Simple;
                UI.At(star.rectTransform, 28 + i * 58, -76, 46, 46, new Vector2(0, 1), new Vector2(0, 1));
                UI.MakeButton(star, () => SetRating(idx + 1));
            }

            var states = new List<State>();
            for (int r = 0; r <= 5; r++)
            {
                var props = new List<Property>();
                for (int i = 0; i < 5; i++)
                {
                    bool on = i < r;
                    var c = on ? Palette.Gold : Palette.Track;
                    float sc = on ? 1f : 0.8f;
                    props.Add(P($"Star{i}", IMAGE, "m_Color.r", c.r));
                    props.Add(P($"Star{i}", IMAGE, "m_Color.g", c.g));
                    props.Add(P($"Star{i}", IMAGE, "m_Color.b", c.b));
                    props.Add(P($"Star{i}", TRANSFORM, "m_LocalScale.x", sc));
                    props.Add(P($"Star{i}", TRANSFORM, "m_LocalScale.y", sc));
                }
                states.Add(St($"R{r}", props.ToArray()));
            }
            _stars.statefulDataAsset = Data(states);
            _stars.LoadFromAsset(_stars.statefulDataAsset);
            _stars.SnapToState($"R{_rating}");
            _stars.currentStateIndex = _rating; // states are R0..R5 in order
            SetEase(_stars, Ease.OutBack);
        }

        void SetRating(int r)
        {
            _rating = r;
            _stars.TweenToState($"R{r}", 0.32f, Ease.OutBack);
        }

        // ---------------- stepper ----------------
        void BuildStepperCard(Transform card)
        {
            var root = UI.Rect("Stepper", card);
            UI.Stretch(root);
            _stepper = root.gameObject.AddComponent<StatefulRoot>();

            var minus = UI.Panel("Minus", root, Palette.Track);
            UI.At(minus.rectTransform, 24, -64, 48, 48, new Vector2(0, 1), new Vector2(0, 1));
            UI.MakeButton(minus, () => Step(-1));
            var ml = UI.Label("L", minus.transform, "–", 26, Palette.Text, TextAlignmentOptions.Center, FontStyles.Bold); UI.Stretch(ml.rectTransform);

            var chip = UI.Panel("Chip", root, Palette.Panel);
            UI.At(chip.rectTransform, 0, -64, 96, 56, new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            chip.raycastTarget = false;
            _stepLabel = UI.Label("V", chip.transform, _stepValue.ToString(), 26, Palette.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            UI.Stretch(_stepLabel.rectTransform);

            var plus = UI.Panel("Plus", root, Palette.Accent);
            UI.At(plus.rectTransform, -24, -64, 48, 48, new Vector2(1, 1), new Vector2(1, 1));
            UI.MakeButton(plus, () => Step(1));
            var pl = UI.Label("L", plus.transform, "+", 26, Palette.Hex("#0D1117"), TextAlignmentOptions.Center, FontStyles.Bold); UI.Stretch(pl.rectTransform);

            _stepper.statefulDataAsset = Data(new List<State>
            {
                St("Pulse", P("Chip", TRANSFORM, "m_LocalScale.x", 1.18f), P("Chip", TRANSFORM, "m_LocalScale.y", 1.18f)),
                St("Rest",  P("Chip", TRANSFORM, "m_LocalScale.x", 1f),    P("Chip", TRANSFORM, "m_LocalScale.y", 1f)),
            });
            _stepper.LoadFromAsset(_stepper.statefulDataAsset);
            _stepper.SnapToState("Rest");
            _stepper.currentStateIndex = 1; // Rest
            SetEase(_stepper, Ease.OutBack);
        }

        void Step(int d)
        {
            _stepValue = Mathf.Clamp(_stepValue + d, 0, 99);
            _stepLabel.text = _stepValue.ToString();
            _stepper.SnapToState("Pulse");
            _stepper.TweenToState("Rest", 0.32f, Ease.OutBack); // pop down with overshoot
        }
    }
}
