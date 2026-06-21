using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EasyStateful.Runtime;

namespace EasyStateful.Samples.Showcase
{
    // Layout page — reveal & overlay patterns: a true accordion (items push each
    // other), plus buttons that open the global modal and drawer.
    public partial class Showcase
    {
        static readonly string[] AccTitles = { "What is a state?", "How do transitions work?", "Can I nest them?" };
        static readonly string[] AccBodies =
        {
            "A snapshot of property values on this hierarchy —\nposition, scale, color, visibility, anything.",
            "Call TweenToState(\"Name\") and the engine interpolates\nevery property from where it is now to the target.",
            "Yes — every widget on these pages is its own\nStatefulRoot, nested freely inside the layout.",
        };
        StatefulRoot _acc;
        int _accOpen = -1;

        const float AccHeaderH = 54f, AccBodyH = 86f, AccGap = 10f;

        void BuildLayoutPage(RectTransform page)
        {
            var card = PageCard(page, 30, -22, 740, 432, "ACCORDION").transform;

            var root = UI.Rect("Accordion", card);
            UI.At(root, 18, -42, 704, 300, new Vector2(0, 1), new Vector2(0, 1));
            _acc = root.gameObject.AddComponent<StatefulRoot>();

            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var item = UI.Panel($"Item{i}", root, Palette.Panel);
                UI.At(item.rectTransform, 0, 0, 704, AccHeaderH, new Vector2(0.5f, 1), new Vector2(0.5f, 1));
                item.gameObject.AddComponent<RectMask2D>();

                var header = UI.Panel($"H{i}", item.transform, new Color(0, 0, 0, 0), rounded: false);
                UI.At(header.rectTransform, 0, 0, 704, AccHeaderH, new Vector2(0.5f, 1), new Vector2(0.5f, 1));
                UI.MakeButton(header, () => ToggleAcc(idx));
                var t = UI.Label("T", header.transform, AccTitles[i], 16, Palette.Text, TextAlignmentOptions.Left, FontStyles.Bold);
                UI.At(t.rectTransform, 20, 0, 560, AccHeaderH, new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                var chev = UI.Panel("Chevron", header.transform, Palette.TextDim, rounded: false);
                chev.sprite = UI.Triangle; chev.type = Image.Type.Simple; chev.raycastTarget = false;
                UI.At(chev.rectTransform, -24, 0, 16, 11, new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f));

                var body = UI.Label($"B{i}", item.transform, AccBodies[i], 14, Palette.TextDim, TextAlignmentOptions.TopLeft);
                UI.At(body.rectTransform, 20, -AccHeaderH, 660, AccBodyH, new Vector2(0, 1), new Vector2(0, 1));
            }

            _acc.statefulDataAsset = Data(BuildAccStates());
            _acc.LoadFromAsset(_acc.statefulDataAsset);
            _acc.SnapToState("Closed");

            // overlay triggers
            var dialogBtn = UI.Panel("DialogBtn", card, Palette.Accent);
            UI.At(dialogBtn.rectTransform, 18, 26, 200, 48, new Vector2(0, 0), new Vector2(0, 0));
            UI.MakeButton(dialogBtn, OpenModal);
            var dl = UI.Label("L", dialogBtn.transform, "Open dialog", 15, Palette.Hex("#0D1117"), TextAlignmentOptions.Center, FontStyles.Bold);
            UI.Stretch(dl.rectTransform);

            var drawerBtn = UI.Panel("DrawerBtn", card, Palette.Track);
            UI.At(drawerBtn.rectTransform, 230, 26, 200, 48, new Vector2(0, 0), new Vector2(0, 0));
            UI.MakeButton(drawerBtn, OpenDrawer);
            var dr = UI.Label("L", drawerBtn.transform, "Open drawer", 15, Palette.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            UI.Stretch(dr.rectTransform);
        }

        // y of item i's top (pivot top) given which item is open
        float AccItemTop(int i, int open)
        {
            float y = 0f;
            for (int k = 0; k < i; k++)
                y -= (k == open ? AccHeaderH + AccBodyH : AccHeaderH) + AccGap;
            return y;
        }

        List<State> BuildAccStates()
        {
            var states = new List<State>();
            for (int open = -1; open < 3; open++)
            {
                var props = new List<Property>();
                for (int i = 0; i < 3; i++)
                {
                    bool o = i == open;
                    props.Add(P($"Item{i}", RECT, "m_AnchoredPosition.y", AccItemTop(i, open)));
                    props.Add(P($"Item{i}", RECT, "m_SizeDelta.y", o ? AccHeaderH + AccBodyH : AccHeaderH));
                    props.Add(P($"Item{i}/H{i}/Chevron", TRANSFORM, "localEulerAngles.z", o ? 180f : 0f));
                }
                states.Add(St(open < 0 ? "Closed" : $"Open{open}", props.ToArray()));
            }
            return states;
        }

        void ToggleAcc(int i)
        {
            _accOpen = (_accOpen == i) ? -1 : i;
            _acc.TweenToState(_accOpen < 0 ? "Closed" : $"Open{_accOpen}", 0.4f, Ease.OutCubic);
        }
    }
}
