using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace EasyStateful.Samples.Showcase
{
    // Shared page helper. The four page builders live in Showcase.<Page>.cs.
    public partial class Showcase
    {
        Image PageCard(RectTransform page, float x, float y, float w, float h, string caption)
        {
            var card = UI.Panel("Card", page, Palette.PanelAlt);
            UI.At(card.rectTransform, x, y, w, h, new Vector2(0, 1), new Vector2(0, 1));
            if (!string.IsNullOrEmpty(caption))
            {
                var cap = UI.Label("Cap", card.transform, caption, 11, Palette.TextDim, TextAlignmentOptions.TopLeft, FontStyles.Bold);
                UI.At(cap.rectTransform, 18, -14, w - 36, 16, new Vector2(0, 1), new Vector2(0, 1));
            }
            return card;
        }
    }
}
