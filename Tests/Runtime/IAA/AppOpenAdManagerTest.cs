using NUnit.Framework;
using UnityEngine;

namespace com.noctuagames.sdk.Tests.IAA
{
    /// <summary>
    /// Unit tests for <see cref="AppOpenAdManager"/>.
    /// Covers: Configure, ConfigureSecondary, foreground auto-show, manual show,
    /// preferred-network routing, frequency/cooldown gating, OnAdNotAvailable callback.
    /// </summary>
    [TestFixture]
    public class AppOpenAdManagerTest
    {
        private MockAdNetwork _primary;
        private MockAdNetwork _secondary;

        [SetUp]
        public void SetUp()
        {
            _primary   = new MockAdNetwork { NetworkName = "admob" };
            _secondary = new MockAdNetwork { NetworkName = "applovin" };

            PlayerPrefs.DeleteKey("NoctuaFreq_app_open_last");
            PlayerPrefs.DeleteKey("NoctuaFreq_app_open_hist");
            PlayerPrefs.Save();
        }

        // ─── Configure ────────────────────────────────────────────────────────

        [Test]
        public void Configure_ValidUnit_SetsAndLoadsOnPrimary()
        {
            var mgr = new AppOpenAdManager(_primary);
            mgr.Configure("unit-primary");

            Assert.AreEqual("unit-primary", _primary.LastAppOpenAdUnitId);
            Assert.AreEqual(1, _primary.LoadAppOpenCallCount);
        }

        [Test]
        public void Configure_NullUnit_DoesNothing()
        {
            var mgr = new AppOpenAdManager(_primary);
            mgr.Configure(null);

            Assert.IsNull(_primary.LastAppOpenAdUnitId);
            Assert.AreEqual(0, _primary.LoadAppOpenCallCount);
        }

        [Test]
        public void Configure_EmptyUnit_DoesNothing()
        {
            var mgr = new AppOpenAdManager(_primary);
            mgr.Configure(string.Empty);

            Assert.IsNull(_primary.LastAppOpenAdUnitId);
            Assert.AreEqual(0, _primary.LoadAppOpenCallCount);
        }

        [Test]
        public void Configure_UnknownUnit_DoesNothing()
        {
            var mgr = new AppOpenAdManager(_primary);
            mgr.Configure("unknown");

            Assert.AreEqual(0, _primary.LoadAppOpenCallCount);
        }

        [Test]
        public void Configure_WithSecondaryUnit_LoadsOnBothNetworks()
        {
            var mgr = new AppOpenAdManager(_primary, _secondary);
            mgr.Configure("unit-primary", "unit-secondary");

            Assert.AreEqual("unit-primary",   _primary.LastAppOpenAdUnitId);
            Assert.AreEqual("unit-secondary", _secondary.LastAppOpenAdUnitId);
            Assert.AreEqual(1, _primary.LoadAppOpenCallCount);
            Assert.AreEqual(1, _secondary.LoadAppOpenCallCount);
        }

        [Test]
        public void Configure_SecondaryUnitUnknown_OnlyLoadsPrimary()
        {
            var mgr = new AppOpenAdManager(_primary, _secondary);
            mgr.Configure("unit-primary", "unknown");

            Assert.AreEqual(1, _primary.LoadAppOpenCallCount);
            Assert.AreEqual(0, _secondary.LoadAppOpenCallCount);
        }

        // ─── ConfigureSecondary ────────────────────────────────────────────────

        [Test]
        public void ConfigureSecondary_NoSecondaryNetwork_Ignored()
        {
            // Primary-only manager — ConfigureSecondary should be a no-op
            var mgr = new AppOpenAdManager(_primary);
            mgr.Configure("unit-primary");

            Assert.DoesNotThrow(() => mgr.ConfigureSecondary("unit-secondary"));
            Assert.AreEqual(0, _secondary.LoadAppOpenCallCount);
        }

        [Test]
        public void ConfigureSecondary_ValidUnit_SetsAndLoadsOnSecondary()
        {
            var mgr = new AppOpenAdManager(_primary, _secondary);
            mgr.Configure("unit-primary");

            mgr.ConfigureSecondary("unit-secondary");

            Assert.AreEqual("unit-secondary", _secondary.LastAppOpenAdUnitId);
            Assert.AreEqual(1, _secondary.LoadAppOpenCallCount);
        }

        [Test]
        public void ConfigureSecondary_EmptyUnit_Ignored()
        {
            var mgr = new AppOpenAdManager(_primary, _secondary);
            mgr.Configure("unit-primary");
            mgr.ConfigureSecondary(string.Empty);

            Assert.AreEqual(0, _secondary.LoadAppOpenCallCount);
        }

        // ─── IsAppOpenAdReady ─────────────────────────────────────────────────

        [Test]
        public void IsAppOpenAdReady_PrimaryReady_ReturnsTrue()
        {
            _primary.AppOpenReady = true;
            var mgr = new AppOpenAdManager(_primary);

            Assert.IsTrue(mgr.IsAppOpenAdReady());
        }

        [Test]
        public void IsAppOpenAdReady_PrimaryNotReady_SecondaryReady_ReturnsTrue()
        {
            _primary.AppOpenReady   = false;
            _secondary.AppOpenReady = true;

            var mgr = new AppOpenAdManager(_primary, _secondary);

            Assert.IsTrue(mgr.IsAppOpenAdReady());
        }

        [Test]
        public void IsAppOpenAdReady_NeitherReady_ReturnsFalse()
        {
            _primary.AppOpenReady   = false;
            _secondary.AppOpenReady = false;

            var mgr = new AppOpenAdManager(_primary, _secondary);

            Assert.IsFalse(mgr.IsAppOpenAdReady());
        }

        [Test]
        public void IsAppOpenAdReady_FrequencyManagerBlocks_ReturnsFalse()
        {
            _primary.AppOpenReady = true;

            var freqMgr = new AdFrequencyManager(
                cooldowns: new CooldownConfig { AppOpen = 30 }
            );
            freqMgr.RecordImpression(AdFormatKey.AppOpen); // puts in cooldown

            var mgr = new AppOpenAdManager(_primary, frequencyManager: freqMgr);

            Assert.IsFalse(mgr.IsAppOpenAdReady());
        }

        // ─── ShowAppOpenAd ────────────────────────────────────────────────────

        [Test]
        public void ShowAppOpenAd_PrimaryReady_ShowsFromPrimary()
        {
            _primary.AppOpenReady = true;
            var mgr = new AppOpenAdManager(_primary, _secondary);
            mgr.Configure("unit");

            mgr.ShowAppOpenAd();

            Assert.AreEqual(1, _primary.ShowAppOpenCallCount);
            Assert.AreEqual(0, _secondary.ShowAppOpenCallCount);
        }

        [Test]
        public void ShowAppOpenAd_PrimaryNotReady_FallsBackToSecondary()
        {
            _primary.AppOpenReady   = false;
            _secondary.AppOpenReady = true;

            var mgr = new AppOpenAdManager(_primary, _secondary);
            mgr.ShowAppOpenAd();

            Assert.AreEqual(0, _primary.ShowAppOpenCallCount);
            Assert.AreEqual(1, _secondary.ShowAppOpenCallCount);
        }

        [Test]
        public void ShowAppOpenAd_NeitherReady_FiresOnAdNotAvailable()
        {
            _primary.AppOpenReady   = false;
            _secondary.AppOpenReady = false;

            string notAvailableFormat = null;
            var mgr = new AppOpenAdManager(
                _primary, _secondary,
                onAdNotAvailable: fmt => notAvailableFormat = fmt
            );

            mgr.ShowAppOpenAd();

            Assert.AreEqual(0, _primary.ShowAppOpenCallCount);
            Assert.AreEqual(AdFormatKey.AppOpen, notAvailableFormat);
        }

        [Test]
        public void ShowAppOpenAd_FrequencyManagerBlocks_FiresOnAdNotAvailable()
        {
            _primary.AppOpenReady = true;

            var freqMgr = new AdFrequencyManager(
                cooldowns: new CooldownConfig { AppOpen = 60 }
            );
            freqMgr.RecordImpression(AdFormatKey.AppOpen);

            string notAvailableFormat = null;
            var mgr = new AppOpenAdManager(
                _primary,
                frequencyManager: freqMgr,
                onAdNotAvailable: fmt => notAvailableFormat = fmt
            );

            mgr.ShowAppOpenAd();

            Assert.AreEqual(0, _primary.ShowAppOpenCallCount, "Should not show during cooldown");
            Assert.AreEqual(AdFormatKey.AppOpen, notAvailableFormat);
        }

        [Test]
        public void ShowAppOpenAd_NoFrequencyManager_AlwaysAllows()
        {
            _primary.AppOpenReady = true;
            var mgr = new AppOpenAdManager(_primary, frequencyManager: null);

            mgr.ShowAppOpenAd();
            mgr.ShowAppOpenAd();

            Assert.AreEqual(2, _primary.ShowAppOpenCallCount);
        }

        [Test]
        public void ShowAppOpenAd_PreferredSecondary_ShowsFromSecondary()
        {
            _primary.AppOpenReady   = true;
            _secondary.AppOpenReady = true;

            var mgr = new AppOpenAdManager(
                _primary, _secondary,
                preferredNetworkName: "applovin"
            );

            mgr.ShowAppOpenAd();

            Assert.AreEqual(0, _primary.ShowAppOpenCallCount,   "Primary should not be used");
            Assert.AreEqual(1, _secondary.ShowAppOpenCallCount, "Preferred secondary should show");
        }

        [Test]
        public void ShowAppOpenAd_PreferredSecondaryNotReady_FallsBackToPrimary()
        {
            _primary.AppOpenReady   = true;
            _secondary.AppOpenReady = false;

            var mgr = new AppOpenAdManager(
                _primary, _secondary,
                preferredNetworkName: "applovin"
            );

            mgr.ShowAppOpenAd();

            Assert.AreEqual(1, _primary.ShowAppOpenCallCount,   "Should fall back to primary");
            Assert.AreEqual(0, _secondary.ShowAppOpenCallCount);
        }

        [Test]
        public void ShowAppOpenAd_RecordsImpressionInFrequencyManager()
        {
            _primary.AppOpenReady = true;

            var freqMgr = new AdFrequencyManager(
                cooldowns: new CooldownConfig { AppOpen = 60 }
            );

            var mgr = new AppOpenAdManager(_primary, frequencyManager: freqMgr);
            mgr.ShowAppOpenAd();

            // Now in cooldown — next show should be blocked
            Assert.IsFalse(freqMgr.CanShowAd(AdFormatKey.AppOpen),
                "FrequencyManager should have recorded the impression");
        }

        // ─── OnApplicationForeground ───────────────────────────────────────────

        [Test]
        public void OnApplicationForeground_AutoShowDisabled_DoesNotShow()
        {
            _primary.AppOpenReady = true;
            var mgr = new AppOpenAdManager(_primary, autoShowOnForeground: false);
            mgr.Configure("unit");

            mgr.OnApplicationForeground();

            Assert.AreEqual(0, _primary.ShowAppOpenCallCount);
        }

        [Test]
        public void OnApplicationForeground_AutoShowEnabled_NotConfigured_DoesNotShow()
        {
            _primary.AppOpenReady = true;
            // Don't call Configure() → _appOpenAdUnitConfigured = false
            var mgr = new AppOpenAdManager(_primary, autoShowOnForeground: true);

            mgr.OnApplicationForeground();

            Assert.AreEqual(0, _primary.ShowAppOpenCallCount);
        }

        [Test]
        public void OnApplicationForeground_AutoShowEnabled_Configured_Shows()
        {
            _primary.AppOpenReady = true;
            var mgr = new AppOpenAdManager(_primary, autoShowOnForeground: true);
            mgr.Configure("unit");

            mgr.OnApplicationForeground();

            Assert.AreEqual(1, _primary.ShowAppOpenCallCount);
        }

        [Test]
        public void OnApplicationForeground_FullscreenAdShowing_DoesNotShow()
        {
            _primary.AppOpenReady = true;
            var mgr = new AppOpenAdManager(_primary, autoShowOnForeground: true);
            mgr.Configure("unit");

            mgr.SetFullscreenAdShowing(true);
            mgr.OnApplicationForeground();

            Assert.AreEqual(0, _primary.ShowAppOpenCallCount);
        }

        [Test]
        public void OnApplicationForeground_NotReady_DoesNotShow()
        {
            _primary.AppOpenReady = false;
            var mgr = new AppOpenAdManager(_primary, autoShowOnForeground: true);
            mgr.Configure("unit");

            mgr.OnApplicationForeground();

            Assert.AreEqual(0, _primary.ShowAppOpenCallCount);
        }

        // ─── SetFullscreenAdShowing ────────────────────────────────────────────

        [Test]
        public void SetFullscreenAdShowing_True_ThenFalse_AutoShowResumed()
        {
            _primary.AppOpenReady = true;
            var mgr = new AppOpenAdManager(_primary, autoShowOnForeground: true);
            mgr.Configure("unit");

            mgr.SetFullscreenAdShowing(true);
            mgr.OnApplicationForeground();
            Assert.AreEqual(0, _primary.ShowAppOpenCallCount, "Should be blocked");

            mgr.SetFullscreenAdShowing(false);
            mgr.OnApplicationForeground();
            Assert.AreEqual(1, _primary.ShowAppOpenCallCount, "Should show after fullscreen released");
        }

        // ─── LoadAppOpenAd ─────────────────────────────────────────────────────

        [Test]
        public void LoadAppOpenAd_AfterConfigure_TriggersLoad()
        {
            var mgr = new AppOpenAdManager(_primary, _secondary);
            mgr.Configure("unit-primary", "unit-secondary");

            // Clear counts from Configure()
            _primary   = new MockAdNetwork { NetworkName = "admob",    AppOpenReady = true };
            _secondary = new MockAdNetwork { NetworkName = "applovin", AppOpenReady = true };
            var mgr2 = new AppOpenAdManager(_primary, _secondary);
            mgr2.Configure("unit-primary", "unit-secondary");

            mgr2.LoadAppOpenAd();

            Assert.AreEqual(2, _primary.LoadAppOpenCallCount,   "Load during Configure + LoadAppOpenAd");
            Assert.AreEqual(2, _secondary.LoadAppOpenCallCount, "Load during Configure + LoadAppOpenAd");
        }

        [Test]
        public void LoadAppOpenAd_BeforeConfigure_DoesNothing()
        {
            var mgr = new AppOpenAdManager(_primary, _secondary);
            mgr.LoadAppOpenAd(); // not configured

            Assert.AreEqual(0, _primary.LoadAppOpenCallCount);
            Assert.AreEqual(0, _secondary.LoadAppOpenCallCount);
        }
    }
}
