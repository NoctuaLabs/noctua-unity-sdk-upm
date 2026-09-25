using UnityEngine;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// Resolves a campaign's popup chrome colours: the card frame (<see cref="CampaignItem.FrameColor"/>)
    /// and the backdrop dimming the game behind it (<see cref="CampaignItem.BackdropColor"/>).
    /// Pure — kept out of the presenter so it is unit-testable without a live UI panel.
    /// </summary>
    public static class CampaignPopupStyle
    {
        /// <summary>Border thickness applied when a frame colour is set.</summary>
        public const float FrameWidthPx = 2f;

        /// <summary>
        /// Returns <c>true</c> with the parsed colour when <paramref name="item"/> has a valid
        /// <c>frame_color</c> and renders with SDK card chrome. Borderless and fullscreen
        /// campaigns have no card frame, so they always return <c>false</c>.
        /// </summary>
        public static bool TryResolveFrame(CampaignItem item, out Color color)
        {
            color = default;
            if (item == null || item.Fullscreen || item.Borderless) return false;
            return TryParse(item.FrameColor, out color);
        }

        /// <summary>
        /// Returns <c>true</c> with the parsed colour when <paramref name="item"/> sets a valid
        /// <c>backdrop_color</c>. The alpha channel (<c>#RRGGBBAA</c>) is the opacity; a colour
        /// without one is opaque.
        /// </summary>
        public static bool TryResolveBackdrop(CampaignItem item, out Color color)
        {
            color = default;
            return item != null && TryParse(item.BackdropColor, out color);
        }

        private static bool TryParse(string raw, out Color color)
        {
            color = default;
            return !string.IsNullOrWhiteSpace(raw) && ColorUtility.TryParseHtmlString(raw.Trim(), out color);
        }
    }
}
