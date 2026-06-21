using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace EasyStateful.Samples.Showcase
{
    // Shared page helper. The four page builders live in Showcase.<Page>.cs.
    public partial class Showcase
    {
        const float HeaderH = 34f;

        Image PageCard(RectTransform page, float x, float y, float w, float h, string caption)
        {
            var card = UI.Panel("Card", page, Palette.PanelAlt);
            UI.At(card.rectTransform, x, y, w, h, new Vector2(0, 1), new Vector2(0, 1));
            if (!string.IsNullOrEmpty(caption)) CardHeader(card.transform, w, caption);
            return card;
        }

        /// <summary>
        /// Shared card header: a slightly darker band (rounded only at the top, to meet the card's
        /// top corners) with the caption on it. Drawn before the card content so content sits on top.
        /// </summary>
        void CardHeader(Transform card, float w, string caption)
        {
            // Cool steel-blue band, a step lighter than the card body (PanelAlt #1C2230) with a
            // bluer tint — so it separates clearly from the body instead of sinking into the dark bg.
            var header = UI.Panel("Header", card, Palette.Hex("#26314A"), rounded: false);
            header.sprite = UI.RoundedRectTop((int)w, (int)HeaderH, 16f);
            UI.At(header.rectTransform, 0, 0, w, HeaderH, new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            header.raycastTarget = false;

            var cap = UI.Label("Cap", card, caption, 11, Palette.TextDim, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UI.At(cap.rectTransform, 18, -14, w - 36, 16, new Vector2(0, 1), new Vector2(0, 1));
        }
    }
}
