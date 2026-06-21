using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EasyStateful.Runtime;

namespace EasyStateful.Samples.Showcase
{
    // Motion page — an easing lab: the same move played through six easings at once.
    public partial class Showcase
    {
        static readonly (Ease ease, string name, string hex)[] EaseRows =
        {
            (Ease.Linear,    "Linear",    "#8B949E"),
            (Ease.OutQuad,   "OutQuad",   "#4DA3FF"),
            (Ease.OutCubic,  "OutCubic",  "#3FB950"),
            (Ease.OutBack,   "OutBack",   "#E3B341"),
            (Ease.OutElastic,"OutElastic","#A371F7"),
            (Ease.OutBounce, "OutBounce", "#F778BA"),
        };
        readonly List<StatefulRoot> _easeDots = new List<StatefulRoot>();
        bool _easeAtEnd;

        const float EaseStartX = 16f;
        const float EaseEndX = 540f;

        void BuildMotionPage(RectTransform page)
        {
            var card = PageCard(page, 30, -22, 740, 470, "EASING — ONE MOVE, SIX CURVES").transform;

            for (int i = 0; i < EaseRows.Length; i++)
            {
                float rowY = -50 - i * 58;
                // colour lives on the label, so the moving tiles stay identical (not slider-like)
                var name = UI.Label($"N{i}", card, EaseRows[i].name, 14, Palette.Hex(EaseRows[i].hex), TextAlignmentOptions.Left, FontStyles.Bold);
                UI.At(name.rectTransform, 20, rowY, 120, 24, new Vector2(0, 1), new Vector2(0, 1));

                // faint guide showing the travel path + a start/end marker (not a filled slider track)
                var guide = UI.Panel($"Guide{i}", card, new Color(1, 1, 1, 0.05f), rounded: false);
                UI.At(guide.rectTransform, 150, rowY - 19, EaseEndX + 26, 2, new Vector2(0, 1), new Vector2(0, 1));
                guide.raycastTarget = false;
                var endm = UI.Panel($"End{i}", card, new Color(1, 1, 1, 0.12f), circle: true);
                UI.At(endm.rectTransform, 150 + EaseEndX + 13, rowY - 18, 9, 9, new Vector2(0, 1), new Vector2(0.5f, 0.5f));
                endm.raycastTarget = false;

                // the travelling tile — its own StatefulRoot so each gets its own ease
                var holder = UI.Rect($"Lane{i}", card);
                UI.At(holder, 150, rowY - 6, EaseEndX + 40, 36, new Vector2(0, 1), new Vector2(0, 1));
                var tile = UI.Panel("Tile", holder, Palette.Accent);
                UI.At(tile.rectTransform, EaseStartX, 0, 26, 26, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f));
                tile.raycastTarget = false;
                var sr = tile.gameObject.AddComponent<StatefulRoot>();
                sr.statefulDataAsset = Data(new List<State>
                {
                    St("Start", P("", RECT, "m_AnchoredPosition.x", EaseStartX)),
                    St("End",   P("", RECT, "m_AnchoredPosition.x", EaseEndX)),
                });
                sr.LoadFromAsset(sr.statefulDataAsset);
                sr.SnapToState("Start");
                SetEase(sr, EaseRows[i].ease); // each tile animates with its own curve
                _easeDots.Add(sr);
            }

            var play = UI.Panel("Play", card, Palette.Accent);
            UI.At(play.rectTransform, 20, 30, 150, 46, new Vector2(0, 0), new Vector2(0, 0));
            UI.MakeButton(play, PlayEasings);
            var pl = UI.Label("L", play.transform, "Play / Reset", 16, Palette.Hex("#0D1117"), TextAlignmentOptions.Center, FontStyles.Bold);
            UI.Stretch(pl.rectTransform);

            var hint = UI.Label("H", card, "Same duration, same distance — only the curve differs.", 13, Palette.TextDim, TextAlignmentOptions.Left);
            UI.At(hint.rectTransform, 186, 42, 520, 22, new Vector2(0, 0), new Vector2(0, 0));
        }

        void PlayEasings()
        {
            _easeAtEnd = !_easeAtEnd;
            for (int i = 0; i < _easeDots.Count; i++)
            {
                if (_easeAtEnd) _easeDots[i].TweenToState("End", 1.15f, EaseRows[i].ease);
                else _easeDots[i].TweenToState("Start", 1.15f, EaseRows[i].ease);
            }
        }
    }
}
