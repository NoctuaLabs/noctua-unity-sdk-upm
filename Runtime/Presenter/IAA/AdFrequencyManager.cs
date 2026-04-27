using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Manages per-format ad frequency caps and cooldown periods.
    /// Tracks impression counts within time windows and enforces minimum intervals between ads.
    /// Impression history and last-impression timestamps are persisted to PlayerPrefs so
    /// frequency caps survive app restarts.
    /// </summary>
    public class AdFrequencyManager
    {
        private readonly NoctuaLogger _log = new(typeof(AdFrequencyManager));

        private readonly FrequencyCapConfig _frequencyCaps;
        private readonly CooldownConfig _cooldowns;
        private readonly EnabledFormatsConfig _enabledFormats;

        // Impression tracking: format -> list of impression timestamps (UTC ticks)
        private readonly Dictionary<string, List<DateTime>> _impressionHistory = new();

        // Cooldown tracking: format -> last impression time
        private readonly Dictionary<string, DateTime> _lastImpressionTime = new();

        // PlayerPrefs key prefix — namespaced to avoid collisions
        private const string PrefsPrefix = "NoctuaFreq_";

        /// <summary>
        /// Creates a new AdFrequencyManager with the given configuration.
        /// Restores persisted impression history from PlayerPrefs on construction.
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

            LoadFromPrefs();
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
        /// Persists the updated history to PlayerPrefs.
        /// </summary>
        /// <param name="format">Ad format name.</param>
        public void RecordImpression(string format)
        {
            var now = DateTime.UtcNow;

            _lastImpressionTime[format] = now;

            if (!_impressionHistory.ContainsKey(format))
                _impressionHistory[format] = new List<DateTime>();

            _impressionHistory[format].Add(now);

            _log.Debug($"Recorded impression for '{format}'. Total in window: {_impressionHistory[format].Count}");

            SaveToPrefs(format);
        }

        // ─────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────

        private bool IsFormatEnabled(string format)
        {
            if (_enabledFormats == null) return true;

            return format switch
            {
                AdFormatKey.Interstitial => _enabledFormats.Interstitial ?? true,
                AdFormatKey.Rewarded => _enabledFormats.Rewarded ?? true,
                AdFormatKey.RewardedInterstitial => _enabledFormats.RewardedInterstitial ?? true,
                AdFormatKey.Banner => _enabledFormats.Banner ?? true,
                AdFormatKey.AppOpen => _enabledFormats.AppOpen ?? true,
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

            // Prune expired entries outside the rolling window
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

        // ─────────────────────────────────────────────────────────
        // PlayerPrefs persistence
        // Format: NoctuaFreq_{format}_last  = UTC ticks of last impression (long as string)
        //         NoctuaFreq_{format}_hist  = comma-separated UTC ticks of impression history
        // ─────────────────────────────────────────────────────────

        private void SaveToPrefs(string format)
        {
            try
            {
                // Save last impression time
                if (_lastImpressionTime.TryGetValue(format, out var last))
                    PlayerPrefs.SetString(PrefsPrefix + format + "_last", last.Ticks.ToString());

                // Save impression history as comma-separated ticks
                if (_impressionHistory.TryGetValue(format, out var history))
                {
                    var ticks = string.Join(",", history.ConvertAll(t => t.Ticks.ToString()));
                    PlayerPrefs.SetString(PrefsPrefix + format + "_hist", ticks);
                }

                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                _log.Warning($"Failed to persist frequency cap data for '{format}': {ex.Message}");
            }
        }

        private void LoadFromPrefs()
        {
            var formats = new[]
            {
                AdFormatKey.Interstitial,
                AdFormatKey.Rewarded,
                AdFormatKey.RewardedInterstitial,
                AdFormatKey.Banner,
                AdFormatKey.AppOpen
            };

            foreach (var format in formats)
            {
                try
                {
                    // Restore last impression time
                    var lastKey = PrefsPrefix + format + "_last";
                    if (PlayerPrefs.HasKey(lastKey) &&
                        long.TryParse(PlayerPrefs.GetString(lastKey), out var lastTicks))
                    {
                        _lastImpressionTime[format] = new DateTime(lastTicks, DateTimeKind.Utc);
                    }

                    // Restore impression history
                    var histKey = PrefsPrefix + format + "_hist";
                    if (PlayerPrefs.HasKey(histKey))
                    {
                        var raw = PlayerPrefs.GetString(histKey);
                        if (!string.IsNullOrEmpty(raw))
                        {
                            var history = new List<DateTime>();
                            foreach (var part in raw.Split(','))
                            {
                                if (long.TryParse(part, out var ticks))
                                    history.Add(new DateTime(ticks, DateTimeKind.Utc));
                            }
                            _impressionHistory[format] = history;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.Warning($"Failed to restore frequency cap data for '{format}': {ex.Message}");
                }
            }

            _log.Debug("Frequency cap history restored from PlayerPrefs.");
        }
    }
}
