using System.Collections.Generic;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Evaluates CPM floor thresholds against a network's tracked average CPM to determine
    /// whether an ad network should be used for a given ad format.
    ///
    /// Floors are resolved in priority order:
    ///   1. Segment overrides keyed by composite segment (e.g. "t1_highspender_loyal_d30plus")
    ///   2. Tier floors keyed by country tier ("t1", "t2", "t3")
    ///   3. Allow (no floor configured)
    ///
    /// Results:
    ///   <see cref="CpmFloorResult.Allow"/>    — avg CPM is at or above the soft floor, or no floor applies.
    ///   <see cref="CpmFloorResult.SoftFail"/> — avg CPM is below the soft floor but at or above the hard floor.
    ///                                           The network is tried anyway; log a warning.
    ///   <see cref="CpmFloorResult.HardFail"/> — avg CPM is below the hard floor. Skip this network.
    /// </summary>
    public class CpmFloorManager
    {
        private readonly NoctuaLogger _log = new(typeof(CpmFloorManager));
        private readonly CpmFloorConfig _config;
        private const int DefaultMinSamples = 10;

        /// <summary>
        /// Creates a new <see cref="CpmFloorManager"/> with the provided configuration.
        /// </summary>
        /// <param name="config">The CPM floor configuration from the IAA config.</param>
        public CpmFloorManager(CpmFloorConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// Evaluates whether a network's average CPM meets the floor requirements for the given format.
        /// </summary>
        /// <param name="networkName">The ad network name (used only for log messages).</param>
        /// <param name="format">The ad format ("interstitial", "rewarded", "banner", "app_open").</param>
        /// <param name="avgCpm">The network's historical average CPM in USD (from AdNetworkPerformanceTracker).</param>
        /// <param name="sampleCount">Number of revenue impressions tracked. Below MinSamples → Allow (cold-start safe).</param>
        /// <param name="segmentKey">Composite segment key from UserSegmentManager (e.g. "t1_nonpayer_loyal_d30plus").</param>
        /// <returns>
        /// <see cref="CpmFloorResult.Allow"/>, <see cref="CpmFloorResult.SoftFail"/>,
        /// or <see cref="CpmFloorResult.HardFail"/>.
        /// </returns>
        public CpmFloorResult EvaluateFloor(
            string networkName,
            string format,
            double avgCpm,
            int sampleCount,
            string segmentKey)
        {
            if (_config?.Enabled != true)
                return CpmFloorResult.Allow;

            int minSamples = _config.MinSamples ?? DefaultMinSamples;
            if (sampleCount < minSamples)
            {
                _log.Debug($"Floor skipped for {networkName}/{format}: only {sampleCount}/{minSamples} samples.");
                return CpmFloorResult.Allow;
            }

            var floor = ResolveFloor(format, segmentKey);
            if (floor == null)
                return CpmFloorResult.Allow;

            if (avgCpm >= floor.Soft)
                return CpmFloorResult.Allow;

            if (avgCpm >= floor.Hard)
            {
                _log.Warning($"SoftFail: {networkName}/{format} avgCPM ${avgCpm:F4} < soft ${floor.Soft:F4}. Proceeding anyway.");
                return CpmFloorResult.SoftFail;
            }

            _log.Warning($"HardFail: {networkName}/{format} avgCPM ${avgCpm:F4} < hard ${floor.Hard:F4}. Skipping network.");
            return CpmFloorResult.HardFail;
        }

        /// <summary>
        /// Returns true if a floor is configured for the given ad format.
        /// </summary>
        public bool HasFloor(string format)
        {
            if (_config?.Enabled != true) return false;
            if (_config.Floors == null) return false;
            return _config.Floors.ContainsKey(format);
        }

        // ── Private helpers ────────────────────────────────────────────────────────

        /// <summary>
        /// Resolves the applicable CpmFloorEntry for the given format and segment key.
        /// Segment overrides take priority over country-tier floors.
        /// </summary>
        private CpmFloorEntry ResolveFloor(string format, string segmentKey)
        {
            // 1. Check segment overrides
            if (_config.SegmentOverrides != null &&
                !string.IsNullOrEmpty(segmentKey) &&
                _config.SegmentOverrides.TryGetValue(segmentKey, out var segOverride) &&
                segOverride != null &&
                segOverride.TryGetValue(format, out var segFloor))
            {
                return segFloor;
            }

            // 2. Fall back to country-tier floor
            if (_config.Floors == null ||
                !_config.Floors.TryGetValue(format, out var tierFloors) ||
                tierFloors == null)
            {
                return null;
            }

            string tier = ExtractCountryTier(segmentKey);
            if (tierFloors.TryGetValue(tier, out var tierFloor))
                return tierFloor;

            // If the exact tier is missing, try T3 as the most permissive fallback
            if (tier != "t3" && tierFloors.TryGetValue("t3", out var t3Floor))
                return t3Floor;

            return null;
        }

        /// <summary>
        /// Extracts the country-tier prefix from a composite segment key.
        /// "t1_nonpayer_loyal_d30plus" → "t1". Returns "t3" for unknown/empty input.
        /// </summary>
        private static string ExtractCountryTier(string segmentKey)
        {
            if (string.IsNullOrEmpty(segmentKey)) return "t3";

            int underscore = segmentKey.IndexOf('_');
            if (underscore <= 0) return "t3";

            string tier = segmentKey.Substring(0, underscore);
            return (tier == "t1" || tier == "t2" || tier == "t3") ? tier : "t3";
        }
    }

    /// <summary>
    /// Result of a CPM floor evaluation.
    /// </summary>
    public enum CpmFloorResult
    {
        /// <summary>The network's avg CPM meets or exceeds the soft floor, or no floor applies. Proceed normally.</summary>
        Allow,

        /// <summary>Avg CPM is below the soft floor but above the hard floor. Proceed with a warning.</summary>
        SoftFail,

        /// <summary>Avg CPM is below the hard floor. Skip this network.</summary>
        HardFail
    }
}
