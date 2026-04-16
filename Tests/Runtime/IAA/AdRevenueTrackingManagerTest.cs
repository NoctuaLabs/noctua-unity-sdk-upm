using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace com.noctuagames.sdk.Tests.IAA
{
    /// <summary>
    /// Unit tests for <see cref="AdRevenueTrackingManager"/>.
    /// Covers: Taichi 6-step threshold logic, PlayerPrefs persistence, null-config guard.
    /// Note: AdMob/AppLovin SDK methods are conditional (#if UNITY_ADMOB / UNITY_APPLOVIN)
    /// and are tested through the public threshold methods available in all configurations.
    /// </summary>
    [TestFixture]
    public class AdRevenueTrackingManagerTest
    {
        private const string KeyTotalRevenue      = "Noctua_Taichi_TotalRevenue";
        private const string KeyTotalAdCount      = "Noctua_Taichi_TotalAdCount";
        private const string KeyTotalImpressions  = "Noctua_Taichi_TotalImpressions";
        private const string KeyInterstitialCount = "Noctua_Taichi_InterstitialCount";
        private const string KeyRewardedCount     = "Noctua_Taichi_RewardedCount";
        private const string KeyRewardedRevenue   = "Noctua_Taichi_RewardedRevenue";

        private MockAdRevenueTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            _tracker = new MockAdRevenueTracker();

            // Clear all Taichi PlayerPrefs
            PlayerPrefs.DeleteKey(KeyTotalRevenue);
            PlayerPrefs.DeleteKey(KeyTotalAdCount);
            PlayerPrefs.DeleteKey(KeyTotalImpressions);
            PlayerPrefs.DeleteKey(KeyInterstitialCount);
            PlayerPrefs.DeleteKey(KeyRewardedCount);
            PlayerPrefs.DeleteKey(KeyRewardedRevenue);
            PlayerPrefs.Save();
        }

        private TaichiConfig DefaultConfig() => new TaichiConfig
        {
            RevenueThreshold           = 0.01f,
            AdCountThreshold           = 10,
            TotalImpressionThreshold   = 10,
            InterstitialCountThreshold = 5,
            RewardedCountThreshold     = 5,
            RewardedRevenueThreshold   = 0.01f,
        };

        // ─── SetAdRevenueTracker ──────────────────────────────────────────────

        [Test]
        public void SetAdRevenueTracker_NewTrackerReceivesEvents()
        {
            var tracker1 = new MockAdRevenueTracker();
            var tracker2 = new MockAdRevenueTracker();
            var mgr      = new AdRevenueTrackingManager(tracker1, DefaultConfig());

            mgr.SetAdRevenueTracker(tracker2);
            mgr.ProcessAllFormatsThresholds(0.01); // crosses threshold → fires event

            Assert.AreEqual(0, tracker1.Events.Count, "Original tracker must not receive events after replacement");
            Assert.IsTrue(tracker2.WasFired("Total_Ads_Revenue_001"), "New tracker must receive events");
        }

        [Test]
        public void SetAdRevenueTracker_NullTrackerSilentlyDropsEvents()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());
            mgr.SetAdRevenueTracker(null);

            Assert.DoesNotThrow(() => mgr.ProcessAllFormatsThresholds(0.01),
                "Should not throw when tracker is null");
        }

        // ─── No config guard ──────────────────────────────────────────────────

        [Test]
        public void ProcessAllFormatsThresholds_NullConfig_FiresNoEvents()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, taichiConfig: null);
            mgr.ProcessAllFormatsThresholds(1.0);

            Assert.AreEqual(0, _tracker.Events.Count,
                "Taichi tracking disabled when config is null");
        }

        [Test]
        public void SetTaichiConfig_Null_DisablesTracking()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());
            mgr.SetTaichiConfig(null);

            mgr.ProcessAllFormatsThresholds(1.0);

            Assert.AreEqual(0, _tracker.Events.Count,
                "After SetTaichiConfig(null), no events should fire");
        }

        // ─── Step 1: Total_Ads_Revenue_001 ───────────────────────────────────

        [Test]
        public void ProcessAllFormatsThresholds_BelowRevenueThreshold_NoEvent()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            // Threshold = 0.01 USD; add 0.005 → should not fire
            mgr.ProcessAllFormatsThresholds(0.005);

            Assert.IsFalse(_tracker.WasFired("Total_Ads_Revenue_001"));
        }

        [Test]
        public void ProcessAllFormatsThresholds_CrossesRevenueThreshold_FiresEvent()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            // 0.01 >= threshold (0.01) → fires
            mgr.ProcessAllFormatsThresholds(0.01);

            Assert.IsTrue(_tracker.WasFired("Total_Ads_Revenue_001"));
        }

        [Test]
        public void ProcessAllFormatsThresholds_CrossesRevenueThreshold_ResetsCounter()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());
            mgr.ProcessAllFormatsThresholds(0.01); // fires and resets to 0
            _tracker.Clear();

            // Next impression alone (0.005) should NOT fire again
            mgr.ProcessAllFormatsThresholds(0.005);
            Assert.IsFalse(_tracker.WasFired("Total_Ads_Revenue_001"),
                "Counter should have reset after first threshold crossing");
        }

        [Test]
        public void ProcessAllFormatsThresholds_AccumulatesAcrossCalls()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            mgr.ProcessAllFormatsThresholds(0.004);
            mgr.ProcessAllFormatsThresholds(0.003);
            mgr.ProcessAllFormatsThresholds(0.004); // 0.004+0.003+0.004 = 0.011 >= 0.01

            Assert.IsTrue(_tracker.WasFired("Total_Ads_Revenue_001"),
                "Revenue should accumulate across calls until threshold reached");
        }

        // ─── Step 2: TenAdsShown ──────────────────────────────────────────────

        [Test]
        public void ProcessAllFormatsThresholds_BelowAdCountThreshold_NoEvent()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            for (int i = 0; i < 9; i++)
                mgr.ProcessAllFormatsThresholds(0); // count = 9, threshold = 10

            Assert.IsFalse(_tracker.WasFired("TenAdsShown"));
        }

        [Test]
        public void ProcessAllFormatsThresholds_ReachesAdCountThreshold_FiresEvent()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            for (int i = 0; i < 10; i++)
                mgr.ProcessAllFormatsThresholds(0);

            Assert.IsTrue(_tracker.WasFired("TenAdsShown"));
        }

        [Test]
        public void ProcessAllFormatsThresholds_AdCountFires_ResetsCounter()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            for (int i = 0; i < 10; i++)
                mgr.ProcessAllFormatsThresholds(0);

            _tracker.Clear();

            // 9 more should not re-fire
            for (int i = 0; i < 9; i++)
                mgr.ProcessAllFormatsThresholds(0);

            Assert.IsFalse(_tracker.WasFired("TenAdsShown"),
                "Counter should have reset after firing");
        }

        // ─── Step 3: taichi_total_ad_impression ───────────────────────────────

        [Test]
        public void ProcessInterstitialThresholds_BelowTotalImpressionThreshold_NoEvent()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            for (int i = 0; i < 9; i++)
                mgr.ProcessInterstitialThresholds(0);

            Assert.IsFalse(_tracker.WasFired("taichi_total_ad_impression"));
        }

        [Test]
        public void ProcessInterstitialThresholds_CrossesTotalImpressionThreshold_FiresEvent()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            for (int i = 0; i < 10; i++)
                mgr.ProcessInterstitialThresholds(0);

            Assert.IsTrue(_tracker.WasFired("taichi_total_ad_impression"));
        }

        // ─── Step 4: taichi_interstitial_ad_impression ────────────────────────

        [Test]
        public void ProcessInterstitialThresholds_CrossesInterstitialCountThreshold_FiresEvent()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            for (int i = 0; i < 5; i++)
                mgr.ProcessInterstitialThresholds(0); // threshold = 5

            Assert.IsTrue(_tracker.WasFired("taichi_interstitial_ad_impression"));
        }

        [Test]
        public void ProcessInterstitialThresholds_BelowInterstitialCount_NoSpecificEvent()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            for (int i = 0; i < 4; i++)
                mgr.ProcessInterstitialThresholds(0);

            Assert.IsFalse(_tracker.WasFired("taichi_interstitial_ad_impression"));
        }

        // ─── Step 5: taichi_rewarded_ad_impression & Step 6: taichi_rewarded_ad_revenue ──

        [Test]
        public void ProcessRewardedThresholds_CrossesRewardedCountThreshold_FiresEvent()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            for (int i = 0; i < 5; i++)
                mgr.ProcessRewardedThresholds(0); // threshold = 5

            Assert.IsTrue(_tracker.WasFired("taichi_rewarded_ad_impression"));
        }

        [Test]
        public void ProcessRewardedThresholds_CrossesRewardedRevenueThreshold_FiresEvent()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            mgr.ProcessRewardedThresholds(0.01); // single call crosses 0.01 threshold

            Assert.IsTrue(_tracker.WasFired("taichi_rewarded_ad_revenue"));
        }

        [Test]
        public void ProcessRewardedThresholds_BelowRewardedRevenueThreshold_NoEvent()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            mgr.ProcessRewardedThresholds(0.005);

            Assert.IsFalse(_tracker.WasFired("taichi_rewarded_ad_revenue"));
        }

        [Test]
        public void ProcessRewardedThresholds_RewardedRevenue_AccumulatesAcrossCalls()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            mgr.ProcessRewardedThresholds(0.006);
            mgr.ProcessRewardedThresholds(0.005); // 0.006 + 0.005 = 0.011 >= 0.01

            Assert.IsTrue(_tracker.WasFired("taichi_rewarded_ad_revenue"));
        }

        // ─── Step 3 shared by both interstitial and rewarded ─────────────────

        [Test]
        public void ProcessBothThresholds_MixedCalls_TotalImpressionCountsMixed()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            // 5 interstitial + 5 rewarded = 10 → triggers taichi_total_ad_impression
            for (int i = 0; i < 5; i++)
                mgr.ProcessInterstitialThresholds(0);
            for (int i = 0; i < 5; i++)
                mgr.ProcessRewardedThresholds(0);

            Assert.IsTrue(_tracker.WasFired("taichi_total_ad_impression"),
                "Total impression counter should be shared between interstitial and rewarded");
        }

        // ─── SetTaichiConfig (late update) ────────────────────────────────────

        [Test]
        public void SetTaichiConfig_UpdatesThresholdMidSession()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, new TaichiConfig
            {
                RevenueThreshold = 1.0f, // high threshold — won't fire
                AdCountThreshold = 1000,
                TotalImpressionThreshold   = 1000,
                InterstitialCountThreshold = 1000,
                RewardedCountThreshold     = 1000,
                RewardedRevenueThreshold   = 1.0f,
            });

            mgr.ProcessAllFormatsThresholds(0.005);
            Assert.IsFalse(_tracker.WasFired("Total_Ads_Revenue_001"));

            // Lower the threshold
            mgr.SetTaichiConfig(new TaichiConfig
            {
                RevenueThreshold           = 0.001f,
                AdCountThreshold           = 1000,
                TotalImpressionThreshold   = 1000,
                InterstitialCountThreshold = 1000,
                RewardedCountThreshold     = 1000,
                RewardedRevenueThreshold   = 0.001f,
            });

            // Accumulated value (0.005) exceeds new threshold (0.001)
            mgr.ProcessAllFormatsThresholds(0.001);
            Assert.IsTrue(_tracker.WasFired("Total_Ads_Revenue_001"),
                "After threshold lowered, next call should trigger event");
        }

        // ─── PlayerPrefs persistence ──────────────────────────────────────────

        [Test]
        public void ProcessAllFormatsThresholds_StatePersisted_RestoredAcrossInstances()
        {
            var config = DefaultConfig(); // revenue threshold = 0.01
            var mgr1   = new AdRevenueTrackingManager(_tracker, config);

            // Accumulate 0.006 — not enough to fire
            mgr1.ProcessAllFormatsThresholds(0.006);
            Assert.IsFalse(_tracker.WasFired("Total_Ads_Revenue_001"));

            // New instance — should restore 0.006 from PlayerPrefs
            _tracker.Clear();
            var mgr2 = new AdRevenueTrackingManager(_tracker, config);
            mgr2.ProcessAllFormatsThresholds(0.005); // 0.006 + 0.005 = 0.011 >= 0.01

            Assert.IsTrue(_tracker.WasFired("Total_Ads_Revenue_001"),
                "Accumulated revenue should persist and trigger event in new instance");
        }

        // ─── Null tracker — initialization race regression tests ─────────────

        [Test]
        public void Constructor_NullTracker_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => new AdRevenueTrackingManager(null, DefaultConfig()),
                "Constructor with null tracker must not throw");
        }

        [Test]
        public void Constructor_NullTracker_NullConfig_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => new AdRevenueTrackingManager(null, null),
                "Constructor with both null tracker and null config must not throw");
        }

        [Test]
        public void ProcessAllFormatsThresholds_NullTracker_DoesNotThrow()
        {
            var mgr = new AdRevenueTrackingManager(null, DefaultConfig());

            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 15; i++)
                    mgr.ProcessAllFormatsThresholds(0.002);
            }, "Null tracker must not throw on threshold processing");
        }

        [Test]
        public void ProcessAllFormatsThresholds_NullTracker_NoEventsTracked()
        {
            var mgr = new AdRevenueTrackingManager(null, DefaultConfig());

            // Drive well past both thresholds
            for (int i = 0; i < 15; i++)
                mgr.ProcessAllFormatsThresholds(0.005);

            // No tracker → no events recorded anywhere we can assert on
            // The test verifies no exception and no interaction with a null reference.
            // Real verification comes from the [REVENUE LOST] error log in production.
            Assert.Pass("No exception thrown with null tracker");
        }

        [Test]
        public void ProcessInterstitialThresholds_NullTracker_DoesNotThrow()
        {
            var mgr = new AdRevenueTrackingManager(null, DefaultConfig());

            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 12; i++)
                    mgr.ProcessInterstitialThresholds(0.001);
            });
        }

        [Test]
        public void ProcessRewardedThresholds_NullTracker_DoesNotThrow()
        {
            var mgr = new AdRevenueTrackingManager(null, DefaultConfig());

            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 12; i++)
                    mgr.ProcessRewardedThresholds(0.001);
            });
        }

        [Test]
        public void SetAdRevenueTracker_AfterNullConstruction_EnablesTracking()
        {
            // Simulates the initialization race fix: tracker is wired after construction
            var mgr = new AdRevenueTrackingManager(null, DefaultConfig());

            // Wire tracker (mirrors what Noctua.Initialization.cs now does before Initialize())
            mgr.SetAdRevenueTracker(_tracker);

            // Now impress enough to cross both thresholds
            for (int i = 0; i < 10; i++)
                mgr.ProcessAllFormatsThresholds(0.002); // total = 0.02 >= 0.01

            Assert.IsTrue(_tracker.WasFired("Total_Ads_Revenue_001"),
                "Tracker wired after construction must receive threshold events");
            Assert.IsTrue(_tracker.WasFired("TenAdsShown"),
                "Tracker wired after construction must receive TenAdsShown event");
        }

        [Test]
        public void SetAdRevenueTracker_NullToValidToNull_CyclesCorrectly()
        {
            var mgr = new AdRevenueTrackingManager(null, DefaultConfig());

            // Phase 1: null → wired → events tracked
            mgr.SetAdRevenueTracker(_tracker);
            mgr.ProcessAllFormatsThresholds(0.01); // fires Total_Ads_Revenue_001
            Assert.IsTrue(_tracker.WasFired("Total_Ads_Revenue_001"), "Phase 1: event must fire");

            // Phase 2: cleared → null → events silently dropped
            _tracker.Clear();
            mgr.SetAdRevenueTracker(null);
            Assert.DoesNotThrow(() => mgr.ProcessAllFormatsThresholds(0.01),
                "Phase 2: null tracker must not throw");
            Assert.AreEqual(0, _tracker.Events.Count,
                "Phase 2: cleared tracker must not receive events when manager tracker is null");

            // Phase 3: re-wired → events tracked again
            mgr.SetAdRevenueTracker(_tracker);
            mgr.ProcessAllFormatsThresholds(0.01); // fires again after reset
            Assert.IsTrue(_tracker.WasFired("Total_Ads_Revenue_001"),
                "Phase 3: re-wired tracker must receive events");
        }
    }
}
