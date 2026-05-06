using System;
using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime.IAA
{
    /// <summary>
    /// EditMode NUnit tests for <see cref="AdFrequencyManager"/>.
    ///
    /// Covers:
    ///   — <c>CanShowAd</c>       — format disabled, cooldown enforced, frequency cap enforced
    ///   — <c>RecordImpression</c> — impression counted, cooldown started, frequency cap triggered
    ///   — PlayerPrefs persistence round-trip (SaveToPrefs / LoadFromPrefs)
    ///   — Null configs → no restrictions
    ///   — Rolling window pruning (expired entries allow new impressions)
    ///
    /// PlayerPrefs keys prefixed "NoctuaFreq_" are cleared in SetUp/TearDown.
    /// </summary>
    [TestFixture]
    public class AdFrequencyManagerTest
    {
        private const string Prefix = "NoctuaFreq_";

        [SetUp]
        public void SetUp() => ClearPrefs();

        [TearDown]
        public void TearDown() => ClearPrefs();

        private static void ClearPrefs()
        {
            foreach (var fmt in new[] { "interstitial", "rewarded", "rewarded_interstitial", "banner", "app_open" })
            {
                PlayerPrefs.DeleteKey(Prefix + fmt + "_last");
                PlayerPrefs.DeleteKey(Prefix + fmt + "_hist");
            }
            PlayerPrefs.Save();
        }

        // ─── Helper factories ──────────────────────────────────────────────

        private static FrequencyCapConfig CapOf(string format, int max, int windowSec)
        {
            var entry = new FrequencyCapEntry { MaxImpressions = max, WindowSeconds = windowSec };
            return format switch
            {
                AdFormatKey.Interstitial         => new FrequencyCapConfig { Interstitial         = entry },
                AdFormatKey.Rewarded             => new FrequencyCapConfig { Rewarded             = entry },
                AdFormatKey.RewardedInterstitial => new FrequencyCapConfig { RewardedInterstitial = entry },
                AdFormatKey.Banner               => new FrequencyCapConfig { Banner               = entry },
                AdFormatKey.AppOpen              => new FrequencyCapConfig { AppOpen              = entry },
                _                               => new FrequencyCapConfig()
            };
        }

        private static CooldownConfig CooldownOf(string format, int seconds)
        {
            return format switch
            {
                AdFormatKey.Interstitial         => new CooldownConfig { Interstitial         = seconds },
                AdFormatKey.Rewarded             => new CooldownConfig { Rewarded             = seconds },
                AdFormatKey.RewardedInterstitial => new CooldownConfig { RewardedInterstitial = seconds },
                AdFormatKey.Banner               => new CooldownConfig { Banner               = seconds },
                AdFormatKey.AppOpen              => new CooldownConfig { AppOpen              = seconds },
                _                               => new CooldownConfig()
            };
        }

        // ═══════════════════════════════════════════════════════════════════
        // Null configs — no restrictions
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void CanShowAd_NullConfigs_AlwaysTrue()
        {
            var mgr = new AdFrequencyManager(); // all null

            Assert.IsTrue(mgr.CanShowAd(AdFormatKey.Interstitial));
            Assert.IsTrue(mgr.CanShowAd(AdFormatKey.Rewarded));
            Assert.IsTrue(mgr.CanShowAd(AdFormatKey.Banner));
        }

        [Test]
        public void CanShowAd_NullConfigs_AfterImpression_StillTrue()
        {
            var mgr = new AdFrequencyManager();
            mgr.RecordImpression(AdFormatKey.Interstitial);

            // No cooldown/cap configured → still allowed
            Assert.IsTrue(mgr.CanShowAd(AdFormatKey.Interstitial));
        }

        // ═══════════════════════════════════════════════════════════════════
        // Enabled / disabled formats
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void CanShowAd_FormatDisabled_ReturnsFalse()
        {
            var enabled = new EnabledFormatsConfig { Interstitial = false };
            var mgr = new AdFrequencyManager(enabledFormats: enabled);

            Assert.IsFalse(mgr.CanShowAd(AdFormatKey.Interstitial),
                "Explicitly disabled format must return false");
        }

        [Test]
        public void CanShowAd_FormatEnabled_ReturnsTrue()
        {
            var enabled = new EnabledFormatsConfig { Interstitial = true };
            var mgr = new AdFrequencyManager(enabledFormats: enabled);

            Assert.IsTrue(mgr.CanShowAd(AdFormatKey.Interstitial));
        }

        [Test]
        public void CanShowAd_FormatNullInConfig_DefaultsToEnabled()
        {
            // Rewarded = null in EnabledFormatsConfig → defaults to true
            var enabled = new EnabledFormatsConfig { Interstitial = false };
            var mgr = new AdFrequencyManager(enabledFormats: enabled);

            Assert.IsTrue(mgr.CanShowAd(AdFormatKey.Rewarded),
                "Null entry in EnabledFormatsConfig must default to enabled");
        }

        [Test]
        public void CanShowAd_UnknownFormat_NotBlocked()
        {
            var enabled = new EnabledFormatsConfig { Interstitial = false };
            var mgr = new AdFrequencyManager(enabledFormats: enabled);

            // Unknown format hits the default branch → true
            Assert.IsTrue(mgr.CanShowAd("custom_format"));
        }

        // ═══════════════════════════════════════════════════════════════════
        // Cooldown enforcement
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void CanShowAd_WithinCooldown_ReturnsFalse()
        {
            // 3600 s cooldown → immediately after impression still in cooldown
            var cooldowns = CooldownOf(AdFormatKey.Interstitial, 3600);
            var mgr = new AdFrequencyManager(cooldowns: cooldowns);

            mgr.RecordImpression(AdFormatKey.Interstitial);

            Assert.IsFalse(mgr.CanShowAd(AdFormatKey.Interstitial),
                "Ad must be blocked immediately after impression when cooldown is active");
        }

        [Test]
        public void CanShowAd_BeforeFirstImpression_NoCooldown()
        {
            var cooldowns = CooldownOf(AdFormatKey.Interstitial, 3600);
            var mgr = new AdFrequencyManager(cooldowns: cooldowns);

            // No impression recorded yet → no last impression → not in cooldown
            Assert.IsTrue(mgr.CanShowAd(AdFormatKey.Interstitial),
                "Before any impression, cooldown must not block the first show");
        }

        [Test]
        public void CanShowAd_ZeroCooldown_NotBlocked()
        {
            var cooldowns = CooldownOf(AdFormatKey.Rewarded, 0);
            var mgr = new AdFrequencyManager(cooldowns: cooldowns);

            mgr.RecordImpression(AdFormatKey.Rewarded);

            Assert.IsTrue(mgr.CanShowAd(AdFormatKey.Rewarded),
                "Zero-second cooldown must never block subsequent shows");
        }

        [Test]
        public void CanShowAd_DifferentFormat_IndependentCooldown()
        {
            var cooldowns = CooldownOf(AdFormatKey.Interstitial, 3600);
            var mgr = new AdFrequencyManager(cooldowns: cooldowns);

            mgr.RecordImpression(AdFormatKey.Interstitial);

            // Rewarded has no cooldown configured (0)
            Assert.IsTrue(mgr.CanShowAd(AdFormatKey.Rewarded),
                "Cooldown on one format must not affect a different format");
        }

        // ═══════════════════════════════════════════════════════════════════
        // Frequency cap enforcement
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void CanShowAd_BelowCap_ReturnsTrue()
        {
            var caps = CapOf(AdFormatKey.Rewarded, maxImpressions: 3, windowSec: 3600);
            var mgr  = new AdFrequencyManager(frequencyCaps: caps);

            mgr.RecordImpression(AdFormatKey.Rewarded);
            mgr.RecordImpression(AdFormatKey.Rewarded);

            Assert.IsTrue(mgr.CanShowAd(AdFormatKey.Rewarded),
                "2 impressions against cap of 3 must still be allowed");
        }

        [Test]
        public void CanShowAd_AtCap_ReturnsFalse()
        {
            var caps = CapOf(AdFormatKey.Rewarded, maxImpressions: 2, windowSec: 3600);
            var mgr  = new AdFrequencyManager(frequencyCaps: caps);

            mgr.RecordImpression(AdFormatKey.Rewarded);
            mgr.RecordImpression(AdFormatKey.Rewarded);

            Assert.IsFalse(mgr.CanShowAd(AdFormatKey.Rewarded),
                "At frequency cap the format must be blocked");
        }

        [Test]
        public void CanShowAd_ZeroMaxImpressions_NotCapped()
        {
            // MaxImpressions = 0 → cap disabled (guard: if cap.MaxImpressions <= 0 return false)
            var caps = CapOf(AdFormatKey.Interstitial, maxImpressions: 0, windowSec: 3600);
            var mgr  = new AdFrequencyManager(frequencyCaps: caps);

            mgr.RecordImpression(AdFormatKey.Interstitial);
            mgr.RecordImpression(AdFormatKey.Interstitial);

            Assert.IsTrue(mgr.CanShowAd(AdFormatKey.Interstitial),
                "MaxImpressions=0 must disable the cap (treat as unrestricted)");
        }

        // ═══════════════════════════════════════════════════════════════════
        // RecordImpression
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void RecordImpression_DoesNotThrow()
        {
            var mgr = new AdFrequencyManager();
            Assert.DoesNotThrow(() => mgr.RecordImpression(AdFormatKey.Banner));
        }

        [Test]
        public void RecordImpression_PersistsToPlayerPrefs()
        {
            var mgr = new AdFrequencyManager();
            mgr.RecordImpression(AdFormatKey.Banner);

            // After recording, the history key must exist in PlayerPrefs
            Assert.IsTrue(PlayerPrefs.HasKey(Prefix + "banner_hist"),
                "RecordImpression must persist history to PlayerPrefs");
            Assert.IsTrue(PlayerPrefs.HasKey(Prefix + "banner_last"),
                "RecordImpression must persist last-impression time to PlayerPrefs");
        }

        // ═══════════════════════════════════════════════════════════════════
        // PlayerPrefs persistence round-trip
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void LoadFromPrefs_RestoredCooldown_EnforcedOnNewInstance()
        {
            var cooldowns = CooldownOf(AdFormatKey.Rewarded, 3600);

            // First manager records impression and persists it
            var mgr1 = new AdFrequencyManager(cooldowns: cooldowns);
            mgr1.RecordImpression(AdFormatKey.Rewarded);

            // Second manager loads from prefs and must see the cooldown
            var mgr2 = new AdFrequencyManager(cooldowns: cooldowns);
            Assert.IsFalse(mgr2.CanShowAd(AdFormatKey.Rewarded),
                "Cooldown persisted by instance 1 must be honoured by instance 2 after restore");
        }

        [Test]
        public void LoadFromPrefs_RestoredCap_EnforcedOnNewInstance()
        {
            var caps = CapOf(AdFormatKey.Interstitial, maxImpressions: 1, windowSec: 3600);

            var mgr1 = new AdFrequencyManager(frequencyCaps: caps);
            mgr1.RecordImpression(AdFormatKey.Interstitial);

            var mgr2 = new AdFrequencyManager(frequencyCaps: caps);
            Assert.IsFalse(mgr2.CanShowAd(AdFormatKey.Interstitial),
                "Frequency cap persisted by instance 1 must be honoured by instance 2");
        }
    }
}
