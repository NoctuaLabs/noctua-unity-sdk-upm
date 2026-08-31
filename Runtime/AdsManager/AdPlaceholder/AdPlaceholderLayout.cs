using UnityEngine;

namespace com.noctuagames.sdk.AdPlaceholder
{
    /// <summary>
    /// Size of the banner cross-promotion placeholder box. Mirrors the standard mobile banner
    /// presets exposed by the real ad networks (<c>GoogleMobileAds.Api.AdSize</c>,
    /// AppLovin's <c>MaxSdkBase.BannerAdSize</c>) so switching between a real network banner and
    /// the cross-promo placeholder needs no separate layout tuning.
    /// </summary>
    public readonly struct AdPlaceholderSize
    {
        /// <summary>Width in pixels.</summary>
        public int Width { get; }

        /// <summary>Height in pixels.</summary>
        public int Height { get; }

        /// <summary>Creates a custom placeholder size in pixels.</summary>
        public AdPlaceholderSize(int width, int height)
        {
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
        }

        /// <summary>Standard mobile banner, 320×50 — matches <c>AdSize.Banner</c> / AppLovin's default banner.</summary>
        public static AdPlaceholderSize Banner => new AdPlaceholderSize(320, 50);

        /// <summary>Large banner, 320×100.</summary>
        public static AdPlaceholderSize LargeBanner => new AdPlaceholderSize(320, 100);

        /// <summary>Medium rectangle (MREC), 300×250.</summary>
        public static AdPlaceholderSize MediumRectangle => new AdPlaceholderSize(300, 250);

        /// <summary>Full banner, 468×60.</summary>
        public static AdPlaceholderSize FullBanner => new AdPlaceholderSize(468, 60);

        /// <summary>Leaderboard, 728×90 — tablets / landscape.</summary>
        public static AdPlaceholderSize Leaderboard => new AdPlaceholderSize(728, 90);
    }

    /// <summary>
    /// On-screen anchor for the banner cross-promotion placeholder box. Mirrors
    /// <c>GoogleMobileAds.Api.AdPosition</c> so banner-placeholder placement follows the same
    /// convention as a real AdMob/AppLovin banner.
    /// </summary>
    public enum AdPlaceholderPosition
    {
        /// <summary>Top edge, horizontally centered.</summary>
        Top,

        /// <summary>Bottom edge, horizontally centered.</summary>
        Bottom,

        /// <summary>Top-left corner.</summary>
        TopLeft,

        /// <summary>Top-right corner.</summary>
        TopRight,

        /// <summary>Bottom-left corner.</summary>
        BottomLeft,

        /// <summary>Bottom-right corner.</summary>
        BottomRight,

        /// <summary>Screen center.</summary>
        Center
    }
}
