using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Tracks the cumulative ad-view count across eligible formats and fires the canonical
    /// <c>watch_ads_1x</c> / <c>watch_ads_5x</c> / <c>watch_ads_10x</c> /
    /// <c>watch_ads_15x</c> / <c>watch_ads_20x</c> / <c>watch_ads_25x</c> / <c>watch_ads_50x</c>
    /// events exactly once each per install.
    ///
    /// Spec: only <see cref="AdFormatKey.Rewarded"/> and <see cref="AdFormatKey.Interstitial"/>
    /// contribute. Banner / RewardedInterstitial / AppOpen calls are silently ignored.
    ///
    /// <b>Combined counting:</b> rewarded and interstitial views accumulate into a single shared
    /// counter — watching 3 interstitials then 2 rewarded reaches the 5x milestone. The payload
    /// <c>ad_type</c> is <c>"combined"</c>. (Previously each format had its own independent counter.)
    ///
    /// State is persisted in <see cref="PlayerPrefs"/>:
    /// <list type="bullet">
    ///   <item><c>noctua.ads.watch.count.combined</c> — int counter (rewarded + interstitial)</item>
    ///   <item><c>noctua.ads.watch.fired.combined</c> — int bitmask: bit0=5x bit1=10x bit2=25x bit3=50x bit4=1x bit5=15x bit6=20x</item>
    ///   <item><c>noctua.ads.watch.migrated</c> — int flag: 1 once legacy per-format state has been folded in</item>
    /// </list>
    ///
    /// <b>Migration:</b> on the first <see cref="RecordWatch"/> after upgrading from the per-format
    /// model, the combined counter is seeded from the sum of the legacy per-format counts and the
    /// combined fired-mask is the OR of the legacy per-format masks. This preserves already-passed
    /// milestones (no duplicate events) and carries forward total progress.
    ///
    /// Lives in the AdsManager layer (same as mediations) — emits via the global
    /// <see cref="Noctua.Event"/> facade. The single instance is created by
    /// <c>Noctua.Initialization</c> and exposed as <see cref="Default"/>.
    /// </summary>
    public class AdWatchMilestoneTracker
    {
        private static readonly NoctuaLogger _log = new(typeof(AdWatchMilestoneTracker));

        /// <summary>Stable, greppable tag prefixed to every log line from this tracker.
        /// Search the logs for <c>[watch_ads_milestone]</c> to find all related output.</summary>
        private const string LogTag = "[watch_ads_milestone]";

        // Combined storage (current model).
        private const string CombinedCountKey = "noctua.ads.watch.count.combined";
        private const string CombinedFiredKey = "noctua.ads.watch.fired.combined";
        private const string MigratedKey      = "noctua.ads.watch.migrated";

        // Legacy per-ad-type prefixes — read once during migration, then never written again.
        private const string LegacyCountKeyPrefix = "noctua.ads.watch.count.";
        private const string LegacyFiredKeyPrefix = "noctua.ads.watch.fired.";

        // ad_type value reported in the combined milestone payload.
        private const string CombinedAdType = "combined";

        // Bit positions within the "fired" bitmask.
        // Bits 0-3 are reserved for the original thresholds and must not be reassigned
        // (existing installs have these bits persisted in PlayerPrefs).
        private const int Bit5x  = 0;
        private const int Bit10x = 1;
        private const int Bit25x = 2;
        private const int Bit50x = 3;
        private const int Bit1x  = 4;
        private const int Bit15x = 5;
        private const int Bit20x = 6;

        // Threshold → (bit, eventName). Order matters: smallest first so that a single
        // increment crossing two thresholds still fires both, lowest first.
        private static readonly (int Threshold, int Bit, string EventName)[] Milestones =
        {
            ( 1, Bit1x,  IAAEventNames.WatchAds1x  ),
            ( 5, Bit5x,  IAAEventNames.WatchAds5x  ),
            (10, Bit10x, IAAEventNames.WatchAds10x ),
            (15, Bit15x, IAAEventNames.WatchAds15x ),
            (20, Bit20x, IAAEventNames.WatchAds20x ),
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
        /// Record one successful ad view of the given type. Rewarded and interstitial views share a
        /// single combined counter. If the new combined count crosses a 1/5/10/15/20/25/50 threshold
        /// for the first time, the matching <c>watch_ads_Nx</c> event fires once.
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

                MigrateLegacyStateIfNeeded();

                var newCount = PlayerPrefs.GetInt(CombinedCountKey, 0) + 1;
                var firedMask = PlayerPrefs.GetInt(CombinedFiredKey, 0);

                PlayerPrefs.SetInt(CombinedCountKey, newCount);

                foreach (var (threshold, bit, eventName) in Milestones)
                {
                    if (newCount < threshold) continue;
                    if ((firedMask & (1 << bit)) != 0) continue;

                    firedMask |= 1 << bit;
                    PlayerPrefs.SetInt(CombinedFiredKey, firedMask);

                    var payload = IAAPayloadBuilder.BuildWatchMilestone(CombinedAdType, newCount);
                    _emit(eventName, payload);
                    _log.Info($"{LogTag} fired {eventName} (combined count={newCount}, last ad_type={adType})");
                }

                // Progress visibility: report the running count and the next milestone still to reach
                // (firedMask already reflects any milestone fired on this watch).
                var next = GetNextUnfiredMilestone(firedMask);
                if (next.HasValue)
                {
                    _log.Debug($"{LogTag} progress: combined count={newCount} (last ad_type={adType}), " +
                               $"next {next.Value.EventName} at {next.Value.Threshold} ({newCount}/{next.Value.Threshold})");
                }
                else
                {
                    _log.Debug($"{LogTag} progress: combined count={newCount} (last ad_type={adType}), all milestones reached");
                }

                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                _log.Error($"{LogTag} RecordWatch failed for ad_type={adType}: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns the next milestone (smallest threshold) whose bit is not yet set in
        /// <paramref name="firedMask"/>, or <c>null</c> if every milestone has already fired.
        /// <see cref="Milestones"/> is in ascending threshold order, so the first unfired entry
        /// is the next one to reach.
        /// </summary>
        private static (int Threshold, string EventName)? GetNextUnfiredMilestone(int firedMask)
        {
            foreach (var (threshold, bit, eventName) in Milestones)
            {
                if ((firedMask & (1 << bit)) == 0)
                {
                    return (threshold, eventName);
                }
            }
            return null;
        }

        /// <summary>
        /// One-time fold of the legacy per-format counters/masks into the combined store. Guarded by
        /// the <c>migrated</c> flag so it runs at most once per install. Combined count is seeded with
        /// the sum of legacy counts; combined fired-mask is the OR of legacy masks (so any milestone
        /// already fired in either format is considered fired and never re-fires).
        /// </summary>
        private void MigrateLegacyStateIfNeeded()
        {
            if (PlayerPrefs.GetInt(MigratedKey, 0) == 1)
            {
                return;
            }

            int legacyCount =
                PlayerPrefs.GetInt(LegacyCountKeyPrefix + AdFormatKey.Rewarded, 0) +
                PlayerPrefs.GetInt(LegacyCountKeyPrefix + AdFormatKey.Interstitial, 0);
            int legacyFired =
                PlayerPrefs.GetInt(LegacyFiredKeyPrefix + AdFormatKey.Rewarded, 0) |
                PlayerPrefs.GetInt(LegacyFiredKeyPrefix + AdFormatKey.Interstitial, 0);

            // Seed only when the combined store is empty so we never clobber existing combined progress.
            if (!PlayerPrefs.HasKey(CombinedCountKey) && legacyCount > 0)
            {
                PlayerPrefs.SetInt(CombinedCountKey, legacyCount);
            }
            if (!PlayerPrefs.HasKey(CombinedFiredKey) && legacyFired != 0)
            {
                PlayerPrefs.SetInt(CombinedFiredKey, legacyFired);
            }

            PlayerPrefs.SetInt(MigratedKey, 1);
            PlayerPrefs.Save();

            if (legacyCount > 0 || legacyFired != 0)
            {
                _log.Info($"{LogTag} migrated legacy state into combined counter (count={legacyCount}, mask={legacyFired})");
            }
        }

        /// <summary>Test helper — current combined count. The <paramref name="adType"/> argument is
        /// ignored (counting is combined); kept for call-site back-compat.</summary>
        public static int GetCount(string adType = null) => PlayerPrefs.GetInt(CombinedCountKey, 0);

        /// <summary>Test helper — current combined fired bitmask. The <paramref name="adType"/>
        /// argument is ignored; kept for call-site back-compat.</summary>
        public static int GetFiredMask(string adType = null) => PlayerPrefs.GetInt(CombinedFiredKey, 0);

        /// <summary>Test helper — clear all combined milestone state (and the migration flag).</summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(CombinedCountKey);
            PlayerPrefs.DeleteKey(CombinedFiredKey);
            PlayerPrefs.DeleteKey(MigratedKey);
            PlayerPrefs.Save();
        }

        /// <summary>Test helper — clear combined state plus the legacy per-format keys for the given
        /// ad type. Kept for back-compat with existing tests; also clears the migration flag.</summary>
        public static void ResetForAdType(string adType)
        {
            Reset();
            if (!string.IsNullOrEmpty(adType))
            {
                PlayerPrefs.DeleteKey(LegacyCountKeyPrefix + adType);
                PlayerPrefs.DeleteKey(LegacyFiredKeyPrefix + adType);
                PlayerPrefs.Save();
            }
        }
    }
}
