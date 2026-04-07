using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Tracks per-network, per-format fill rates and average revenue to enable
    /// dynamic performance-based ad network routing.
    /// </summary>
    public class AdNetworkPerformanceTracker
    {
        private readonly NoctuaLogger _log = new(typeof(AdNetworkPerformanceTracker));

        private const int MaxFillSamples = 100;
        private const int MaxRevenueSamples = 50;
        private const string PrefsPrefix = "NoctuaAdPerf_";

        // Key: "{network}_{format}" → fill tracking
        private readonly Dictionary<string, Queue<bool>> _fillHistory = new();

        // Key: "{network}_{format}" → revenue tracking
        private readonly Dictionary<string, Queue<double>> _revenueHistory = new();

        /// <summary>
        /// Records a fill attempt result for the given network and format.
        /// </summary>
        /// <param name="networkName">The ad network name (e.g., "admob", "applovin").</param>
        /// <param name="format">The ad format (e.g., "interstitial", "rewarded").</param>
        /// <param name="filled">True if the ad was filled, false if it failed to fill.</param>
        public void RecordFillAttempt(string networkName, string format, bool filled)
        {
            string key = $"{networkName}_{format}";

            if (!_fillHistory.ContainsKey(key))
            {
                _fillHistory[key] = new Queue<bool>();
            }

            var queue = _fillHistory[key];
            if (queue.Count >= MaxFillSamples)
            {
                queue.Dequeue();
            }

            queue.Enqueue(filled);

            PersistFillRate(key, GetFillRate(networkName, format));
        }

        /// <summary>
        /// Records a revenue impression for the given network and format.
        /// </summary>
        /// <param name="networkName">The ad network name.</param>
        /// <param name="format">The ad format.</param>
        /// <param name="revenue">The revenue amount for this impression.</param>
        public void RecordRevenue(string networkName, string format, double revenue)
        {
            string key = $"{networkName}_{format}";

            if (!_revenueHistory.ContainsKey(key))
            {
                _revenueHistory[key] = new Queue<double>();
            }

            var queue = _revenueHistory[key];
            if (queue.Count >= MaxRevenueSamples)
            {
                queue.Dequeue();
            }

            queue.Enqueue(revenue);

            PersistAvgRevenue(key, GetAverageRevenue(networkName, format));
        }

        /// <summary>
        /// Returns the fill rate for the given network and format (0.0 to 1.0).
        /// Returns persisted value if no in-memory data is available.
        /// </summary>
        public double GetFillRate(string networkName, string format)
        {
            string key = $"{networkName}_{format}";

            if (_fillHistory.TryGetValue(key, out var queue) && queue.Count > 0)
            {
                int filled = 0;
                foreach (var entry in queue)
                {
                    if (entry) filled++;
                }
                return (double)filled / queue.Count;
            }

            return PlayerPrefs.GetFloat($"{PrefsPrefix}fill_{key}", 0.5f);
        }

        /// <summary>
        /// Returns the average revenue for the given network and format.
        /// Returns persisted value if no in-memory data is available.
        /// </summary>
        public double GetAverageRevenue(string networkName, string format)
        {
            string key = $"{networkName}_{format}";

            if (_revenueHistory.TryGetValue(key, out var queue) && queue.Count > 0)
            {
                double sum = 0;
                foreach (var entry in queue)
                {
                    sum += entry;
                }
                return sum / queue.Count;
            }

            return PlayerPrefs.GetFloat($"{PrefsPrefix}rev_{key}", 0f);
        }

        /// <summary>
        /// Returns the preferred network name for the given format based on
        /// the composite score (fillRate * avgRevenue). Returns null if insufficient data.
        /// </summary>
        /// <param name="format">The ad format to evaluate.</param>
        /// <returns>Network name with the highest composite score, or null.</returns>
        public string GetPreferredNetwork(string format)
        {
            string bestNetwork = null;
            double bestScore = -1;

            foreach (var key in _fillHistory.Keys)
            {
                if (!key.EndsWith($"_{format}")) continue;

                string networkName = key.Substring(0, key.Length - format.Length - 1);
                double fillRate = GetFillRate(networkName, format);
                double avgRevenue = GetAverageRevenue(networkName, format);
                double score = fillRate * avgRevenue;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestNetwork = networkName;
                }
            }

            if (bestNetwork != null)
            {
                _log.Debug($"Preferred network for '{format}': {bestNetwork} (score: {bestScore:F6})");
            }

            return bestNetwork;
        }

        private void PersistFillRate(string key, double fillRate)
        {
            PlayerPrefs.SetFloat($"{PrefsPrefix}fill_{key}", (float)fillRate);
            PlayerPrefs.Save();
        }

        private void PersistAvgRevenue(string key, double avgRevenue)
        {
            PlayerPrefs.SetFloat($"{PrefsPrefix}rev_{key}", (float)avgRevenue);
            PlayerPrefs.Save();
        }
    }
}
