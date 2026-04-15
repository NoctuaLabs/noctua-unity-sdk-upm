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
        public const string WatchAds5x    = "watch_ads_5x";
        public const string WatchAds10x   = "watch_ads_10x";
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

    /// <summary>
    /// Builds canonical IAA event payloads. Every method returns a <see cref="Dictionary{TKey,TValue}"/>
    /// containing the full canonical key set — callers cannot omit keys, only supply values.
    /// </summary>
    public static class IAAPayloadBuilder
    {
        private const string CurrencyUsd = "USD";

        /// <summary>
        /// Build payload for <see cref="IAAEventNames.AdImpression"/>.
        /// All revenue values must already be in USD (callers pre-convert if the network
        /// reports a different currency).
        /// </summary>
        public static Dictionary<string, IConvertible> BuildAdImpression(
            string placement,
            string adType,
            string adUnitId,
            string adUnitName,
            double value,
            double valueUsd,
            string adSize,
            string adSource,
            string adPlatform,
            long engagementTimeMs)
        {
            return new Dictionary<string, IConvertible>
            {
                { IAAPayloadKey.Placement,      placement      ?? "unknown" },
                { IAAPayloadKey.AdType,         adType         ?? "unknown" },
                { IAAPayloadKey.AdUnitId,       adUnitId       ?? "unknown" },
                { IAAPayloadKey.AdUnitName,     adUnitName     ?? "unknown" },
                { IAAPayloadKey.Value,          value          },
                { IAAPayloadKey.Currency,       CurrencyUsd    },
                { IAAPayloadKey.ValueUsd,       valueUsd       },
                { IAAPayloadKey.AdFormat,       adType         ?? "unknown" },
                { IAAPayloadKey.AdSize,         adSize         ?? IAAAdSize.Unknown },
                { IAAPayloadKey.AdSource,       adSource       ?? "unknown" },
                { IAAPayloadKey.AdPlatform,     adPlatform     ?? "unknown" },
                { IAAPayloadKey.EngagementTime, engagementTimeMs },
            };
        }

        /// <summary>
        /// Build payload for <see cref="IAAEventNames.AdLoaded"/>. Same shape as
        /// <see cref="BuildAdImpression"/> minus revenue + engagement (no impression yet).
        /// </summary>
        public static Dictionary<string, IConvertible> BuildAdLoaded(
            string placement,
            string adType,
            string adUnitId,
            string adUnitName,
            string adSize,
            string adSource,
            string adPlatform)
        {
            return new Dictionary<string, IConvertible>
            {
                { IAAPayloadKey.Placement,  placement  ?? "unknown" },
                { IAAPayloadKey.AdType,     adType     ?? "unknown" },
                { IAAPayloadKey.AdUnitId,   adUnitId   ?? "unknown" },
                { IAAPayloadKey.AdUnitName, adUnitName ?? "unknown" },
                { IAAPayloadKey.AdFormat,   adType     ?? "unknown" },
                { IAAPayloadKey.AdSize,     adSize     ?? IAAAdSize.Unknown },
                { IAAPayloadKey.AdSource,   adSource   ?? "unknown" },
                { IAAPayloadKey.AdPlatform, adPlatform ?? "unknown" },
            };
        }

        /// <summary>
        /// Build payload for <see cref="IAAEventNames.AdLoadFailed"/>. Per canonical spec:
        /// only <c>ad_format</c>, <c>ad_platform</c>, <c>ad_unit_name</c>, <c>error</c>.
        /// </summary>
        public static Dictionary<string, IConvertible> BuildAdLoadFailed(
            string adFormat, string adPlatform, string adUnitName, string error)
        {
            return new Dictionary<string, IConvertible>
            {
                { IAAPayloadKey.AdFormat,   adFormat   ?? "unknown" },
                { IAAPayloadKey.AdPlatform, adPlatform ?? "unknown" },
                { IAAPayloadKey.AdUnitName, adUnitName ?? "unknown" },
                { IAAPayloadKey.Error,      error      ?? "unknown" },
            };
        }

        /// <summary>
        /// Build payload for <see cref="IAAEventNames.AdShowFailed"/>. Same canonical shape
        /// as <see cref="BuildAdLoadFailed"/>.
        /// </summary>
        public static Dictionary<string, IConvertible> BuildAdShowFailed(
            string adFormat, string adPlatform, string adUnitName, string error)
            => BuildAdLoadFailed(adFormat, adPlatform, adUnitName, error);

        /// <summary>
        /// Build payload for <see cref="IAAEventNames.AdClicked"/>. Same shape as
        /// <see cref="BuildAdLoaded"/> (no engagement time, no revenue).
        /// </summary>
        public static Dictionary<string, IConvertible> BuildAdClicked(
            string placement,
            string adType,
            string adUnitId,
            string adUnitName,
            string adSize,
            string adSource,
            string adPlatform)
            => BuildAdLoaded(placement, adType, adUnitId, adUnitName, adSize, adSource, adPlatform);

        /// <summary>
        /// Build payload for the canonical watch-count milestone events
        /// (<c>watch_ads_5x</c>, <c>_10x</c>, <c>_25x</c>, <c>_50x</c>).
        /// </summary>
        public static Dictionary<string, IConvertible> BuildWatchMilestone(string adType, int count)
        {
            return new Dictionary<string, IConvertible>
            {
                { IAAPayloadKey.AdType, adType ?? "unknown" },
                { IAAPayloadKey.Count,  count  },
            };
        }

        /// <summary>
        /// Combine error code/message and (optional) mediator-level error code/message into the
        /// single canonical <c>error</c> string consumed by <see cref="BuildAdLoadFailed"/>
        /// and <see cref="BuildAdShowFailed"/>.
        /// </summary>
        public static string FormatError(int code, string message, int? mediatorCode = null, string mediatorMessage = null)
        {
            var msg = string.IsNullOrEmpty(message) ? "unknown" : message;
            if (mediatorCode.HasValue)
            {
                var medMsg = string.IsNullOrEmpty(mediatorMessage) ? "unknown" : mediatorMessage;
                return $"[{code}] {msg} | mediator [{mediatorCode.Value}] {medMsg}";
            }
            return $"[{code}] {msg}";
        }

        /// <summary>
        /// Convenience overload — combine error code/message with an optional <c>domain</c>
        /// (AdMob exposes <c>GetDomain()</c>).
        /// </summary>
        public static string FormatError(int code, string message, string domain)
        {
            var msg = string.IsNullOrEmpty(message) ? "unknown" : message;
            var dom = string.IsNullOrEmpty(domain) ? "unknown" : domain;
            return $"[{code}] {msg} (domain={dom})";
        }
    }
}
