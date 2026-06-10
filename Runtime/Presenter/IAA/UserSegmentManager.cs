using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Classifies the current user into a composite segment key used for CPM floor selection
    /// and A/B experiment assignment. All dimension values are persisted to PlayerPrefs so
    /// that the segment remains stable across sessions.
    /// </summary>
    public class UserSegmentManager
    {
        private readonly NoctuaLogger _log = new(typeof(UserSegmentManager));
        private const string PrefsPrefix = "NoctuaSeg_";

        /// <summary>
        /// PlayerPrefs key holding the UTC install timestamp (in <see cref="DateTime.Ticks"/>,
        /// stored as a string). Written once on first launch. Exposed so other trackers
        /// (e.g. login retention milestones) read the same single source of truth.
        /// </summary>
        public const string InstallTicksKey = PrefsPrefix + "install_ticks";

        // ── Country tier tables ────────────────────────────────────────────────────
        private static readonly HashSet<string> Tier1Countries = new(StringComparer.OrdinalIgnoreCase)
        {
            "US", "CA", "AU", "JP", "KR", "DE", "FR", "GB",
            "NL", "SE", "NO", "DK", "FI", "CH", "AT", "SG",
            "HK", "NZ", "IE", "BE", "IT", "ES", "PT", "LU",
            "IS", "AE" // UAE treated as T1 for premium ad rates
        };

        private static readonly HashSet<string> Tier2Countries = new(StringComparer.OrdinalIgnoreCase)
        {
            "BR", "MX", "TR", "AR", "CO", "CL", "PE", "PL",
            "CZ", "HU", "RO", "SA", "ZA", "TH", "MY", "PH",
            "ID", "UA", "RU", "GR", "IL", "QA", "KW", "EG",
            "NG", "MA", "TW", "MO", "BH"
        };

        // Payer tier thresholds
        private const int LowSpenderMin = 1;
        private const int HighSpenderMin = 5;

        // Session tier thresholds
        private const int NewSessionMax = 3;
        private const int ReturningSessionMax = 20;

        // Install cohort day boundaries
        private const int D0D1Max = 1;
        private const int D2D7Max = 7;
        private const int D8D30Max = 30;

        /// <summary>
        /// Initializes a new <see cref="UserSegmentManager"/>.
        /// Writes the install timestamp on first launch and increments the session counter.
        /// </summary>
        // NOTE: every construction increments the session counter (behavior pinned by
        // UserSegmentManagerTest). Session count drives GetSessionTier(), so this type
        // must be constructed at most once per launch in production — MediationManager
        // guarantees that via `_segmentManager ??= new UserSegmentManager()`. Do not
        // construct additional instances outside tests.
        public UserSegmentManager()
        {
            InitializeInstallTimestamp();
            IncrementSessionCount();
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the country tier for the given ISO-3166-1 alpha-2 country code.
        /// </summary>
        /// <returns>"t1", "t2", or "t3". Returns "t3" for null, empty, or unknown codes.</returns>
        public static string GetCountryTier(string isoCountryCode)
        {
            if (string.IsNullOrEmpty(isoCountryCode)) return "t3";

            if (Tier1Countries.Contains(isoCountryCode)) return "t1";
            if (Tier2Countries.Contains(isoCountryCode)) return "t2";
            return "t3";
        }

        /// <summary>
        /// Returns the user's payer tier based on the persisted purchase count.
        /// "nonpayer" (0 purchases), "lowspender" (1–4), or "highspender" (5+).
        /// </summary>
        public string GetPayerTier()
        {
            int count = GetPurchaseCount();
            if (count >= HighSpenderMin) return "highspender";
            if (count >= LowSpenderMin) return "lowspender";
            return "nonpayer";
        }

        /// <summary>
        /// Returns the user's session tier based on lifetime session count.
        /// "new" (1–3), "returning" (4–20), or "loyal" (21+).
        /// </summary>
        public string GetSessionTier()
        {
            int sessions = PlayerPrefs.GetInt($"{PrefsPrefix}session_count", 1);
            if (sessions <= NewSessionMax) return "new";
            if (sessions <= ReturningSessionMax) return "returning";
            return "loyal";
        }

        /// <summary>
        /// Returns the user's install cohort based on days since first launch.
        /// "d0d1" (0–1 days), "d2d7" (2–7), "d8d30" (8–30), or "d30plus" (31+).
        /// </summary>
        public string GetInstallCohort()
        {
            int days = GetDaysSinceInstall();
            if (days <= D0D1Max) return "d0d1";
            if (days <= D2D7Max) return "d2d7";
            if (days <= D8D30Max) return "d8d30";
            return "d30plus";
        }

        /// <summary>
        /// Returns the composite segment key combining all dimensions.
        /// Format: "{countryTier}_{payerTier}_{sessionTier}_{installCohort}".
        /// Example: "t1_nonpayer_loyal_d30plus".
        /// </summary>
        public string GetCompositeSegment(string isoCountryCode)
        {
            string tier    = GetCountryTier(isoCountryCode);
            string payer   = GetPayerTier();
            string session = GetSessionTier();
            string cohort  = GetInstallCohort();
            return $"{tier}_{payer}_{session}_{cohort}";
        }

        /// <summary>
        /// Returns only the country-tier component of the composite segment ("t1", "t2", "t3").
        /// </summary>
        public string GetCountryTierKey(string isoCountryCode) => GetCountryTier(isoCountryCode);

        /// <summary>
        /// Records a completed purchase. Increments the persisted purchase count used
        /// to compute payer tier in subsequent sessions.
        /// </summary>
        public void RecordPurchase()
        {
            int current = PlayerPrefs.GetInt($"{PrefsPrefix}purchase_count", 0);
            PlayerPrefs.SetInt($"{PrefsPrefix}purchase_count", current + 1);
            PlayerPrefs.Save();
            _log.Debug($"Purchase recorded. Total: {current + 1}");
        }

        // ── Private helpers ────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the persisted UTC install timestamp ticks, or <c>null</c> if it has not
        /// been written yet. Side-effect free.
        /// </summary>
        public static long? GetInstallTicks()
        {
            string stored = PlayerPrefs.GetString(InstallTicksKey, "");
            if (string.IsNullOrEmpty(stored) || !long.TryParse(stored, out long ticks))
            {
                return null;
            }
            return ticks;
        }

        /// <summary>
        /// Writes the UTC install timestamp on first call (no-op if already present). Static so the
        /// SDK composition root can anchor the install date at init time, independent of whether the
        /// ads/mediation path (which constructs <see cref="UserSegmentManager"/>) ever runs. Other
        /// consumers (e.g. login retention milestones) depend on this key existing.
        /// </summary>
        public static void EnsureInstallTimestamp()
        {
            if (PlayerPrefs.HasKey(InstallTicksKey))
            {
                return;
            }
            PlayerPrefs.SetString(InstallTicksKey, DateTime.UtcNow.Ticks.ToString());
            PlayerPrefs.Save();
        }

        private void InitializeInstallTimestamp()
        {
            bool firstLaunch = !PlayerPrefs.HasKey(InstallTicksKey);
            EnsureInstallTimestamp();
            if (firstLaunch)
            {
                _log.Debug($"Install timestamp recorded: {PlayerPrefs.GetString(InstallTicksKey, "")}");
            }
        }

        private void IncrementSessionCount()
        {
            int current = PlayerPrefs.GetInt($"{PrefsPrefix}session_count", 0);
            PlayerPrefs.SetInt($"{PrefsPrefix}session_count", current + 1);
            PlayerPrefs.Save();
        }

        private int GetDaysSinceInstall()
        {
            long? installTicks = GetInstallTicks();
            if (installTicks == null)
            {
                return 0;
            }

            var installDate = new DateTime(installTicks.Value, DateTimeKind.Utc);
            return (int)(DateTime.UtcNow - installDate).TotalDays;
        }

        private int GetPurchaseCount()
        {
            return PlayerPrefs.GetInt($"{PrefsPrefix}purchase_count", 0);
        }
    }
}
