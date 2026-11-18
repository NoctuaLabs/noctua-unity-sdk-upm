using System;
using System.Collections.Generic;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Canonical event names emitted by every IAA mediation. Both AppLovin and AdMob MUST
    /// reference these constants — never hard-code event-name strings inside per-file helpers.
    /// This is enforced at test time by <c>IAAEventParityTest</c>.
    /// </summary>
    public static class IAAEventNames
    {
        // ---------------- User-facing events (canonical schema applies) ----------------
        public const string AdImpression  = "ad_impression";
        public const string AdLoaded      = "ad_loaded";
        public const string AdLoadFailed  = "ad_load_failed";
        public const string AdShowFailed  = "ad_show_failed";
        public const string AdClicked     = "ad_clicked";
        public const string AdShown       = "ad_shown";
        public const string AdClosed      = "ad_closed";
        public const string RewardEarned  = "reward_earned";

        // Banner-only lifecycle (parity required across AppLovin + AdMob)
        public const string AdCollapsed   = "ad_collapsed";
        public const string AdExpanded    = "ad_expanded";

        // Watch-count milestones — fire once per install for rewarded + interstitial only.
        public const string WatchAds1x    = "watch_ads_1x";
        public const string WatchAds5x    = "watch_ads_5x";
        public const string WatchAds10x   = "watch_ads_10x";
        public const string WatchAds15x   = "watch_ads_15x";
        public const string WatchAds20x   = "watch_ads_20x";
        public const string WatchAds25x   = "watch_ads_25x";
        public const string WatchAds50x   = "watch_ads_50x";

        // ---------------- Deprecated aliases — kept one release for dashboard back-compat ----
        /// <summary>Deprecated. Use <see cref="AdShowFailed"/>. Removed in next major release.</summary>
        public const string AdShownFailedLegacy = "ad_shown_failed";
    }

    /// <summary>
    /// Canonical payload-key constants. Every IAA event payload MUST use these strings
    /// for keys — no synonyms (e.g. <c>mediation_service</c>, <c>ad_network</c>) are allowed
    /// in canonical events.
    /// </summary>
    public static class IAAPayloadKey
    {
        public const string Placement      = "placement";
        public const string AdType         = "ad_type";
        public const string AdUnitId       = "ad_unit_id";
        public const string AdUnitName     = "ad_unit_name";
        public const string Value          = "value";
        public const string Currency       = "currency";
        public const string ValueUsd       = "value_usd";
        public const string AdFormat       = "ad_format";
        public const string AdSize         = "ad_size";
        public const string AdSource       = "ad_source";
        public const string AdPlatform     = "ad_platform";
        public const string EngagementTime = "engagement_time";
        public const string Error          = "error";
        public const string Count          = "count";
    }

    /// <summary>
    /// Canonical ad-size strings used in the <c>ad_size</c> payload key.
    /// </summary>
    public static class IAAAdSize
    {
        public const string Fullscreen = "fullscreen";
        public const string Banner320  = "320x50";
        public const string Banner728  = "728x90";
        public const string MRec300    = "300x250";
        public const string Unknown    = "unknown";
    }
}
