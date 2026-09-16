using UnityEngine;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// Resolves a campaign's <see cref="CampaignItem.BackdropColor"/> — the dim painted over the
    /// game behind the popup. Pure, so it is unit-testable without a live UI panel.
    /// </summary>
    public static class CampaignBackdropStyle
    {
        /// <summary>
        /// Returns <c>true</c> with the parsed colour when <paramref name="item"/> sets a valid
        /// <c>backdrop_color</c>. The alpha channel (<c>#RRGGBBAA</c>) is the opacity; a colour
        /// without one is opaque.
        /// </summary>
        public static bool TryResolve(CampaignItem item, out Color color)
        {
            color = default;
            if (item == null || string.IsNullOrWhiteSpace(item.BackdropColor)) return false;
            return ColorUtility.TryParseHtmlString(item.BackdropColor.Trim(), out color);
        }
    }
}
