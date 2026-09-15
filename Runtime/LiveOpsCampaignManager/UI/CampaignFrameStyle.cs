using UnityEngine;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// Resolves a campaign's <see cref="CampaignItem.FrameColor"/> into the colour the popup
    /// card's border should use. Pure — kept out of the presenter so it is unit-testable
    /// without a live UI panel.
    /// </summary>
    public static class CampaignFrameStyle
    {
        /// <summary>Border thickness applied when a frame colour is set.</summary>
        public const float FrameWidthPx = 2f;

        /// <summary>
        /// Returns <c>true</c> with the parsed colour when <paramref name="item"/> has a valid
        /// <c>frame_color</c> and renders with SDK card chrome. Borderless and fullscreen
        /// campaigns have no card frame, so they always return <c>false</c>.
        /// </summary>
        public static bool TryResolve(CampaignItem item, out Color color)
        {
            color = default;
            if (item == null || item.Fullscreen || item.Borderless) return false;
            if (string.IsNullOrWhiteSpace(item.FrameColor)) return false;

            return ColorUtility.TryParseHtmlString(item.FrameColor.Trim(), out color);
        }
    }
}
