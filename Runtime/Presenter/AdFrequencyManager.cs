using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Manages per-format ad frequency caps and cooldown periods.
    /// Tracks impression counts within time windows and enforces minimum intervals between ads.
    /// </summary>
    public class AdFrequencyManager
    {
        private readonly NoctuaLogger _log = new(typeof(AdFrequencyManager));

        private readonly FrequencyCapConfig _frequencyCaps;
        private readonly CooldownConfig _cooldowns;
        private readonly EnabledFormatsConfig _enabledFormats;

        // Impression tracking: format -> list of impression timestamps
        private readonly Dictionary<string, List<DateTime>> _impressionHistory = new();

        // Cooldown tracking: format -> last impression time
        private readonly Dictionary<string, DateTime> _lastImpressionTime = new();

        /// <summary>
        /// Creates a new AdFrequencyManager with the given configuration.
        /// All parameters are optional — null means no restrictions.
        /// </summary>
        public AdFrequencyManager(
            FrequencyCapConfig frequencyCaps = null,
            CooldownConfig cooldowns = null,
            EnabledFormatsConfig enabledFormats = null)
        {
            _frequencyCaps = frequencyCaps;
            _cooldowns = cooldowns;
            _enabledFormats = enabledFormats;
        }

        /// <summary>
        /// Checks whether an ad of the given format is allowed to show right now.
        /// Returns false if the format is disabled, frequency capped, or in cooldown.
        /// </summary>
        /// <param name="format">Ad format name: "interstitial", "rewarded", "rewarded_interstitial", "banner", "app_open".</param>
        public bool CanShowAd(string format)
        {
            if (!IsFormatEnabled(format))
            {
                _log.Debug($"Ad format '{format}' is disabled by config.");
                return false;
            }

            if (IsInCooldown(format))
            {
                _log.Debug($"Ad format '{format}' is in cooldown period.");
                return false;
            }

            if (IsFrequencyCapped(format))
            {
                _log.Debug($"Ad format '{format}' has reached its frequency cap.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Records that an ad impression occurred for the given format.
        /// Must be called after each successful ad display.
        /// </summary>
        /// <param name="format">Ad format name.</param>
        public void RecordImpression(string format)
        {
            var now = DateTime.UtcNow;

            _lastImpressionTime[format] = now;

            if (!_impressionHistory.ContainsKey(format))
            {
                _impressionHistory[format] = new List<DateTime>();
            }

            _impressionHistory[format].Add(now);

            _log.Debug($"Recorded impression for '{format}'. Total in window: {_impressionHistory[format].Count}");
        }

        private bool IsFormatEnabled(string format)
        {
            if (_enabledFormats == null) return true;

            return format switch
            {
                AdFormatKey.Interstitial => _enabledFormats.Interstitial,
                AdFormatKey.Rewarded => _enabledFormats.Rewarded,
                AdFormatKey.RewardedInterstitial => _enabledFormats.RewardedInterstitial,
                AdFormatKey.Banner => _enabledFormats.Banner,
                AdFormatKey.AppOpen => _enabledFormats.AppOpen,
                _ => true
            };
        }

        private bool IsInCooldown(string format)
        {
            if (_cooldowns == null) return false;

            int cooldownSeconds = GetCooldownSeconds(format);
            if (cooldownSeconds <= 0) return false;

            if (!_lastImpressionTime.TryGetValue(format, out var lastTime)) return false;

            double elapsed = (DateTime.UtcNow - lastTime).TotalSeconds;
            return elapsed < cooldownSeconds;
        }

        private bool IsFrequencyCapped(string format)
        {
            if (_frequencyCaps == null) return false;

            var cap = GetFrequencyCapEntry(format);
            if (cap == null || cap.MaxImpressions <= 0) return false;

            if (!_impressionHistory.TryGetValue(format, out var history)) return false;

            // Prune expired entries outside the window
            var windowStart = DateTime.UtcNow.AddSeconds(-cap.WindowSeconds);
            history.RemoveAll(t => t < windowStart);

            return history.Count >= cap.MaxImpressions;
        }

        private int GetCooldownSeconds(string format)
        {
            if (_cooldowns == null) return 0;

            return format switch
            {
                AdFormatKey.Interstitial => _cooldowns.Interstitial,
                AdFormatKey.Rewarded => _cooldowns.Rewarded,
                AdFormatKey.RewardedInterstitial => _cooldowns.RewardedInterstitial,
                AdFormatKey.Banner => _cooldowns.Banner,
                AdFormatKey.AppOpen => _cooldowns.AppOpen,
                _ => 0
            };
        }

        private FrequencyCapEntry GetFrequencyCapEntry(string format)
        {
            if (_frequencyCaps == null) return null;

            return format switch
            {
                AdFormatKey.Interstitial => _frequencyCaps.Interstitial,
                AdFormatKey.Rewarded => _frequencyCaps.Rewarded,
                AdFormatKey.RewardedInterstitial => _frequencyCaps.RewardedInterstitial,
                AdFormatKey.Banner => _frequencyCaps.Banner,
                AdFormatKey.AppOpen => _frequencyCaps.AppOpen,
                _ => null
            };
        }
    }
}
