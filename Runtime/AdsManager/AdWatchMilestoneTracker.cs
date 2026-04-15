using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Tracks cumulative ad-view counts per ad type and fires the canonical
    /// <c>watch_ads_5x</c> / <c>watch_ads_10x</c> / <c>watch_ads_25x</c> / <c>watch_ads_50x</c>
    /// events exactly once each per install.
    ///
    /// Spec: only <see cref="AdFormatKey.Rewarded"/> and <see cref="AdFormatKey.Interstitial"/>
    /// contribute. Banner / RewardedInterstitial / AppOpen calls are silently ignored.
    ///
    /// State is persisted in <see cref="PlayerPrefs"/>:
    /// <list type="bullet">
    ///   <item><c>noctua.ads.watch.count.&lt;adType&gt;</c> — int counter</item>
    ///   <item><c>noctua.ads.watch.fired.&lt;adType&gt;</c> — int bitmask: bit0=5x bit1=10x bit2=25x bit3=50x</item>
    /// </list>
    ///
    /// Lives in the AdsManager layer (same as mediations) — emits via the global
    /// <see cref="Noctua.Event"/> facade. The single instance is created by
    /// <c>Noctua.Initialization</c> and exposed as <see cref="Default"/>.
    /// </summary>
    public class AdWatchMilestoneTracker
    {
        private static readonly NoctuaLogger _log = new(typeof(AdWatchMilestoneTracker));

        private const string CountKeyPrefix = "noctua.ads.watch.count.";
        private const string FiredKeyPrefix = "noctua.ads.watch.fired.";

        // Bit positions within the per-ad-type "fired" bitmask
        private const int Bit5x  = 0;
        private const int Bit10x = 1;
        private const int Bit25x = 2;
        private const int Bit50x = 3;

        // Threshold → (bit, eventName). Order matters: smallest first so that a single
        // increment crossing two thresholds still fires both, lowest first.
        private static readonly (int Threshold, int Bit, string EventName)[] Milestones =
        {
            ( 5, Bit5x,  IAAEventNames.WatchAds5x  ),
            (10, Bit10x, IAAEventNames.WatchAds10x ),
            (25, Bit25x, IAAEventNames.WatchAds25x ),
            (50, Bit50x, IAAEventNames.WatchAds50x ),
        };

        private static readonly HashSet<string> EligibleAdTypes = new()
        {
            AdFormatKey.Rewarded,
            AdFormatKey.Interstitial,
        };

        private readonly Action<string, Dictionary<string, IConvertible>> _emit;

        /// <summary>
        /// Singleton accessor — set by <c>Noctua.Initialization</c>. Mediations call
        /// <c>AdWatchMilestoneTracker.Default?.RecordWatch(adType)</c>.
        /// Nullable until SDK init completes.
        /// </summary>
        public static AdWatchMilestoneTracker Default { get; private set; }

        /// <summary>
        /// Construct with an event-emit delegate. Defaulting to <c>Noctua.Event.TrackCustomEvent</c>
        /// happens at the call site (composition root) — keep this class testable by injecting.
        /// </summary>
        public AdWatchMilestoneTracker(Action<string, Dictionary<string, IConvertible>> emit)
        {
            _emit = emit ?? throw new ArgumentNullException(nameof(emit));
        }

        /// <summary>
        /// Install this instance as the process-wide <see cref="Default"/>. Idempotent — a second
        /// call replaces the previous instance (used in tests + on SDK re-init).
        /// </summary>
        public void InstallAsDefault() => Default = this;

        /// <summary>
        /// Record one successful ad view of the given type. If the new cumulative count
        /// crosses a 5/10/25/50 threshold for the first time, the matching
        /// <c>watch_ads_Nx</c> event fires once.
        /// </summary>
        /// <param name="adType">Must be <see cref="AdFormatKey.Rewarded"/> or
        /// <see cref="AdFormatKey.Interstitial"/>. Other values are ignored.</param>
        public void RecordWatch(string adType)
        {
            try
            {
                if (string.IsNullOrEmpty(adType) || !EligibleAdTypes.Contains(adType))
                {
                    return;
                }

                var countKey = CountKeyPrefix + adType;
                var firedKey = FiredKeyPrefix + adType;

                var newCount = PlayerPrefs.GetInt(countKey, 0) + 1;
                var firedMask = PlayerPrefs.GetInt(firedKey, 0);

                PlayerPrefs.SetInt(countKey, newCount);

                foreach (var (threshold, bit, eventName) in Milestones)
                {
                    if (newCount < threshold) continue;
                    if ((firedMask & (1 << bit)) != 0) continue;

                    firedMask |= 1 << bit;
                    PlayerPrefs.SetInt(firedKey, firedMask);

                    var payload = IAAPayloadBuilder.BuildWatchMilestone(adType, newCount);
                    _emit(eventName, payload);
                    _log.Info($"Fired ad-watch milestone {eventName} for ad_type={adType} count={newCount}");
                }

                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                _log.Error($"AdWatchMilestoneTracker.RecordWatch failed for ad_type={adType}: {ex.Message}");
            }
        }

        /// <summary>Test helper — current count for an ad type (no side effects).</summary>
        public static int GetCount(string adType) => PlayerPrefs.GetInt(CountKeyPrefix + (adType ?? ""), 0);

        /// <summary>Test helper — current fired bitmask for an ad type.</summary>
        public static int GetFiredMask(string adType) => PlayerPrefs.GetInt(FiredKeyPrefix + (adType ?? ""), 0);

        /// <summary>Test helper — clear all milestone state for an ad type.</summary>
        public static void ResetForAdType(string adType)
        {
            if (string.IsNullOrEmpty(adType)) return;
            PlayerPrefs.DeleteKey(CountKeyPrefix + adType);
            PlayerPrefs.DeleteKey(FiredKeyPrefix + adType);
            PlayerPrefs.Save();
        }
    }
}
