using System;
using NUnit.Framework;
using UnityEngine;

namespace com.noctuagames.sdk.Tests.IAA
{
    /// <summary>
    /// Unit tests for <see cref="AdFrequencyManager"/>.
    /// Covers: format-enabled checks, cooldown, frequency caps, PlayerPrefs persistence.
    /// </summary>
    [TestFixture]
    public class AdFrequencyManagerTest
    {
        private const string Interstitial = AdFormatKey.Interstitial;
        private const string Rewarded     = AdFormatKey.Rewarded;
        private const string AppOpen      = AdFormatKey.AppOpen;
        private const string Banner       = AdFormatKey.Banner;

        [SetUp]
        public void SetUp()
        {
            // Clear all persisted frequency data before each test
            foreach (var fmt in new[] { Interstitial, Rewarded, AppOpen, Banner,
                                         AdFormatKey.RewardedInterstitial })
            {
                PlayerPrefs.DeleteKey("NoctuaFreq_" + fmt + "_last");
                PlayerPrefs.DeleteKey("NoctuaFreq_" + fmt + "_hist");
            }

            PlayerPrefs.Save();
        }

        // ─── No config (all null) ──────────────────────────────────────────────

        [Test]
        public void CanShowAd_NoConfig_ReturnsTrue()
        {
            var mgr = new AdFrequencyManager();

            Assert.IsTrue(mgr.CanShowAd(Interstitial));
            Assert.IsTrue(mgr.CanShowAd(Rewarded));
            Assert.IsTrue(mgr.CanShowAd(AppOpen));
        }

        [Test]
        public void CanShowAd_UnknownFormat_ReturnsTrue()
        {
            // Unknown formats fall through to 'true' in all switch cases
            var mgr = new AdFrequencyManager(
                enabledFormats: new EnabledFormatsConfig { Interstitial = false }
            );

            Assert.IsTrue(mgr.CanShowAd("native"));
        }

        // ─── Enabled formats ───────────────────────────────────────────────────

        [Test]
        public void CanShowAd_DisabledFormat_ReturnsFalse()
        {
            var mgr = new AdFrequencyManager(
                enabledFormats: new EnabledFormatsConfig
                {
                    Interstitial = false,
                    Rewarded     = true,
                    AppOpen      = true,
                }
            );

            Assert.IsFalse(mgr.CanShowAd(Interstitial), "Interstitial should be disabled");
            Assert.IsTrue(mgr.CanShowAd(Rewarded),      "Rewarded should be enabled");
            Assert.IsTrue(mgr.CanShowAd(AppOpen),        "AppOpen should be enabled");
        }

        [Test]
        public void CanShowAd_AllFormatsDisabled_ReturnsFalse()
        {
            var mgr = new AdFrequencyManager(
                enabledFormats: new EnabledFormatsConfig
                {
                    Interstitial         = false,
                    Rewarded             = false,
                    RewardedInterstitial = false,
                    Banner               = false,
                    AppOpen              = false,
                }
            );

            Assert.IsFalse(mgr.CanShowAd(Interstitial));
            Assert.IsFalse(mgr.CanShowAd(Rewarded));
            Assert.IsFalse(mgr.CanShowAd(AdFormatKey.RewardedInterstitial));
            Assert.IsFalse(mgr.CanShowAd(Banner));
            Assert.IsFalse(mgr.CanShowAd(AppOpen));
        }

        [Test]
        public void CanShowAd_NullEnabledFlags_DefaultsToTrue()
        {
            // Nullable bool? null means "not configured" → default true
            var mgr = new AdFrequencyManager(
                enabledFormats: new EnabledFormatsConfig
                {
                    Interstitial = null,
                    Rewarded     = null,
                }
            );

            Assert.IsTrue(mgr.CanShowAd(Interstitial));
            Assert.IsTrue(mgr.CanShowAd(Rewarded));
        }

        // ─── Cooldown ─────────────────────────────────────────────────────────

        [Test]
        public void CanShowAd_NoCooldownConfig_ReturnsTrue()
        {
            var mgr = new AdFrequencyManager(cooldowns: null);
            mgr.RecordImpression(Interstitial);

            Assert.IsTrue(mgr.CanShowAd(Interstitial));
        }

        [Test]
        public void CanShowAd_WithinCooldown_ReturnsFalse()
        {
            var mgr = new AdFrequencyManager(
                cooldowns: new CooldownConfig { Interstitial = 30 }
            );
            mgr.RecordImpression(Interstitial);

            // Immediately after impression → still in 30s cooldown
            Assert.IsFalse(mgr.CanShowAd(Interstitial));
        }

        [Test]
        public void CanShowAd_CooldownZero_NeverBlocked()
        {
            var mgr = new AdFrequencyManager(
                cooldowns: new CooldownConfig { Interstitial = 0 }
            );
            mgr.RecordImpression(Interstitial);

            Assert.IsTrue(mgr.CanShowAd(Interstitial));
        }

        [Test]
        public void CanShowAd_BeforeFirstImpression_CooldownDoesNotBlock()
        {
            // No impression recorded yet → no cooldown
            var mgr = new AdFrequencyManager(
                cooldowns: new CooldownConfig { AppOpen = 30 }
            );

            Assert.IsTrue(mgr.CanShowAd(AppOpen));
        }

        [Test]
        public void CanShowAd_EachFormatCooldownIsIndependent()
        {
            var mgr = new AdFrequencyManager(
                cooldowns: new CooldownConfig { Interstitial = 30, Rewarded = 0 }
            );

            mgr.RecordImpression(Interstitial);
            mgr.RecordImpression(Rewarded);

            Assert.IsFalse(mgr.CanShowAd(Interstitial), "Interstitial should be in cooldown");
            Assert.IsTrue(mgr.CanShowAd(Rewarded),      "Rewarded has 0s cooldown, should not block");
        }

        // ─── Frequency caps ───────────────────────────────────────────────────

        [Test]
        public void CanShowAd_NoFrequencyCapConfig_ReturnsTrue()
        {
            var mgr = new AdFrequencyManager(frequencyCaps: null);

            for (int i = 0; i < 20; i++)
                mgr.RecordImpression(Interstitial);

            Assert.IsTrue(mgr.CanShowAd(Interstitial));
        }

        [Test]
        public void CanShowAd_BelowFrequencyCap_ReturnsTrue()
        {
            var mgr = new AdFrequencyManager(
                frequencyCaps: new FrequencyCapConfig
                {
                    Interstitial = new FrequencyCapEntry { MaxImpressions = 5, WindowSeconds = 3600 }
                }
            );

            for (int i = 0; i < 4; i++)
                mgr.RecordImpression(Interstitial);

            Assert.IsTrue(mgr.CanShowAd(Interstitial));
        }

        [Test]
        public void CanShowAd_AtFrequencyCap_ReturnsFalse()
        {
            var mgr = new AdFrequencyManager(
                frequencyCaps: new FrequencyCapConfig
                {
                    Interstitial = new FrequencyCapEntry { MaxImpressions = 3, WindowSeconds = 3600 }
                }
            );

            for (int i = 0; i < 3; i++)
                mgr.RecordImpression(Interstitial);

            Assert.IsFalse(mgr.CanShowAd(Interstitial));
        }

        [Test]
        public void CanShowAd_FrequencyCapMaxImpressionsZero_NeverBlocked()
        {
            // MaxImpressions = 0 is treated as "no cap"
            var mgr = new AdFrequencyManager(
                frequencyCaps: new FrequencyCapConfig
                {
                    Interstitial = new FrequencyCapEntry { MaxImpressions = 0, WindowSeconds = 3600 }
                }
            );

            for (int i = 0; i < 50; i++)
                mgr.RecordImpression(Interstitial);

            Assert.IsTrue(mgr.CanShowAd(Interstitial));
        }

        [Test]
        public void CanShowAd_FrequencyCapEachFormatIndependent()
        {
            var mgr = new AdFrequencyManager(
                frequencyCaps: new FrequencyCapConfig
                {
                    Interstitial = new FrequencyCapEntry { MaxImpressions = 2, WindowSeconds = 3600 },
                    Rewarded     = new FrequencyCapEntry { MaxImpressions = 10, WindowSeconds = 3600 }
                }
            );

            for (int i = 0; i < 2; i++)
                mgr.RecordImpression(Interstitial);

            Assert.IsFalse(mgr.CanShowAd(Interstitial), "Interstitial cap reached");
            Assert.IsTrue(mgr.CanShowAd(Rewarded),       "Rewarded cap not reached");
        }

        // ─── PlayerPrefs persistence ───────────────────────────────────────────

        [Test]
        public void RecordImpression_PersistsThenRestored()
        {
            var mgr1 = new AdFrequencyManager(
                cooldowns: new CooldownConfig { Interstitial = 60 }
            );
            mgr1.RecordImpression(Interstitial);

            // New instance should load from PlayerPrefs and be in cooldown
            var mgr2 = new AdFrequencyManager(
                cooldowns: new CooldownConfig { Interstitial = 60 }
            );

            Assert.IsFalse(mgr2.CanShowAd(Interstitial),
                "Second instance should restore cooldown state from PlayerPrefs");
        }

        [Test]
        public void FrequencyCap_PersistsThenRestored()
        {
            var mgr1 = new AdFrequencyManager(
                frequencyCaps: new FrequencyCapConfig
                {
                    AppOpen = new FrequencyCapEntry { MaxImpressions = 2, WindowSeconds = 3600 }
                }
            );

            mgr1.RecordImpression(AppOpen);
            mgr1.RecordImpression(AppOpen);

            // New instance restores history
            var mgr2 = new AdFrequencyManager(
                frequencyCaps: new FrequencyCapConfig
                {
                    AppOpen = new FrequencyCapEntry { MaxImpressions = 2, WindowSeconds = 3600 }
                }
            );

            Assert.IsFalse(mgr2.CanShowAd(AppOpen),
                "Second instance should restore impression history from PlayerPrefs");
        }

        [Test]
        public void CanShowAd_DisabledFormat_BlocksBeforeCooldownCheck()
        {
            // Disabled check happens first → even without any impressions
            var mgr = new AdFrequencyManager(
                enabledFormats: new EnabledFormatsConfig { Rewarded = false },
                cooldowns: new CooldownConfig { Rewarded = 0 }
            );

            Assert.IsFalse(mgr.CanShowAd(Rewarded));
        }

        [Test]
        public void RecordImpression_MultipleFormatsTrackedSeparately()
        {
            var mgr = new AdFrequencyManager(
                frequencyCaps: new FrequencyCapConfig
                {
                    Interstitial = new FrequencyCapEntry { MaxImpressions = 1, WindowSeconds = 3600 },
                    AppOpen      = new FrequencyCapEntry { MaxImpressions = 5, WindowSeconds = 3600 }
                }
            );

            mgr.RecordImpression(Interstitial);

            // Interstitial cap exhausted, app_open untouched
            Assert.IsFalse(mgr.CanShowAd(Interstitial));
            Assert.IsTrue(mgr.CanShowAd(AppOpen));
        }

        [Test]
        public void RecordImpression_CalledMultipleTimes_HistoryGrows()
        {
            var mgr = new AdFrequencyManager(
                frequencyCaps: new FrequencyCapConfig
                {
                    Rewarded = new FrequencyCapEntry { MaxImpressions = 10, WindowSeconds = 3600 }
                }
            );

            for (int i = 0; i < 9; i++)
                mgr.RecordImpression(Rewarded);

            Assert.IsTrue(mgr.CanShowAd(Rewarded), "9 < 10 cap, should allow");

            mgr.RecordImpression(Rewarded);
            Assert.IsFalse(mgr.CanShowAd(Rewarded), "10 == 10 cap, should block");
        }
    }
}
