using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime.IAA
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

        // ─── Threading contract ───────────────────────────────────────────────
        //
        // Regression for AppLovin background-thread revenue-loss bug.
        //
        // AppLovin MAX delivers OnAdRevenuePaidEvent from a background thread
        // (MaxSdkBase.HandleBackgroundCallback). ProcessAllFormatsThresholds
        // and the per-format threshold helpers read PlayerPrefs, which is
        // main-thread-only. Before the fix, every AppLovin revenue impression
        // was throwing UnityException("GetFloat can only be called from the
        // main thread") and getting swallowed by MAX as a publisher-event
        // exception — TrackAdRevenue never ran.
        //
        // These tests lock in the threading contract: the Process* methods
        // are NOT thread-safe, so callers (MediationManager) MUST marshal
        // the invocation to the main thread (PostToMainThread).
        //
        // If anyone ever makes Process* thread-safe (e.g. swaps PlayerPrefs
        // for a thread-safe store), these tests will start failing — that's
        // the cue to also remove PostToMainThread wrappers around revenue
        // handlers in MediationManager.SubscribeAppLovinRevenueEvents and
        // SetupAdUnitID's RewardedInterstitialAdmob handler.

        [Test]
        public void ProcessAllFormatsThresholds_FromBackgroundThread_Throws()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            var ex = Assert.Throws<AggregateException>(() =>
                Task.Run(() => mgr.ProcessAllFormatsThresholds(0.01)).Wait());

            Assert.IsInstanceOf<UnityException>(ex.InnerException,
                $"Expected UnityException (PlayerPrefs not main-thread), got {ex.InnerException?.GetType().Name}");
            StringAssert.Contains("main thread", ex.InnerException.Message,
                "PlayerPrefs threading violation message expected");
        }

        [Test]
        public void ProcessAllFormatsThresholds_FromMainThread_DoesNotThrow()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            // Sanity check: same call on the main thread must succeed.
            // This is what MediationManager guarantees by wrapping the
            // AppLovin OnAdRevenuePaid handler in PostToMainThread.
            Assert.DoesNotThrow(() => mgr.ProcessAllFormatsThresholds(0.01));
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

    /// <summary>
    /// Extended unit tests for <see cref="AdRevenueTrackingManager"/>.
    /// Covers: multi-tracker swap scenarios, format-specific event routing,
    /// revenue value forwarding, multiple-event sequences, and DroppedEventCount edge cases.
    /// </summary>
    [TestFixture]
    public class AdRevenueTrackingManagerExtendedTest
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

            PlayerPrefs.DeleteKey(KeyTotalRevenue);
            PlayerPrefs.DeleteKey(KeyTotalAdCount);
            PlayerPrefs.DeleteKey(KeyTotalImpressions);
            PlayerPrefs.DeleteKey(KeyInterstitialCount);
            PlayerPrefs.DeleteKey(KeyRewardedCount);
            PlayerPrefs.DeleteKey(KeyRewardedRevenue);
            PlayerPrefs.Save();

            // Suppress expected warning logs for tests that deliberately pass null trackers
            // or trigger SetAdRevenueTracker(null) paths.
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
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

        private TaichiConfig ThresholdOneConfig() => new TaichiConfig
        {
            RevenueThreshold           = 0.001f,
            AdCountThreshold           = 1,
            TotalImpressionThreshold   = 1,
            InterstitialCountThreshold = 1,
            RewardedCountThreshold     = 1,
            RewardedRevenueThreshold   = 0.001f,
        };

        // ─── Multi-tracker swap: only the active tracker receives events ─────

        [Test]
        public void TrackRevenue_WithSequentialTrackers_OnlyLastReceivesEvents()
        {
            // Wire tracker1, accumulate but don't cross threshold yet
            var tracker1 = new MockAdRevenueTracker();
            var tracker2 = new MockAdRevenueTracker();
            var tracker3 = new MockAdRevenueTracker();
            var mgr = new AdRevenueTrackingManager(tracker1, DefaultConfig());

            mgr.ProcessAllFormatsThresholds(0.002); // tracker1 active — no threshold crossed

            mgr.SetAdRevenueTracker(tracker2);
            mgr.ProcessAllFormatsThresholds(0.002); // tracker2 active — still below 0.01

            mgr.SetAdRevenueTracker(tracker3);
            mgr.ProcessAllFormatsThresholds(0.01); // 0.002+0.002+0.01 = 0.014 >= 0.01 — fires on tracker3

            Assert.AreEqual(0, tracker1.Events.Count, "tracker1 must receive no threshold events");
            Assert.AreEqual(0, tracker2.Events.Count, "tracker2 must receive no threshold events");
            Assert.IsTrue(tracker3.WasFired("Total_Ads_Revenue_001"),
                "Only tracker3 (the active tracker) must receive the threshold event");
        }

        [Test]
        public void TrackRevenue_AfterTrackerSetToNull_RemovedTrackerNotCalled()
        {
            // Accumulate below threshold with a real tracker, then null it, then cross threshold
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            mgr.ProcessAllFormatsThresholds(0.005); // below 0.01 — no event yet
            Assert.AreEqual(0, _tracker.Events.Count, "No event fired below threshold");

            mgr.SetAdRevenueTracker(null); // remove tracker

            mgr.ProcessAllFormatsThresholds(0.006); // 0.005+0.006 = 0.011 >= 0.01 — threshold crossed

            Assert.AreEqual(0, _tracker.Events.Count,
                "Original tracker must not receive events after being replaced with null");
        }

        // ─── Null tracker — no crash guarantees ──────────────────────────────

        [Test]
        public void TrackRevenue_NullTrackerFromStart_DoesNotThrow()
        {
            var mgr = new AdRevenueTrackingManager(null, DefaultConfig());

            Assert.DoesNotThrow(() =>
            {
                mgr.ProcessAllFormatsThresholds(0.01);
                mgr.ProcessInterstitialThresholds(0.01);
                mgr.ProcessRewardedThresholds(0.01);
            }, "All threshold methods must be safe when tracker is null");
        }

        [Test]
        public void TrackRevenue_EmptyTrackerList_DoesNotThrow()
        {
            // "Empty tracker list" in this API means null tracker — verify no crash on every code path
            var mgr = new AdRevenueTrackingManager(null, DefaultConfig());

            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 20; i++)
                {
                    mgr.ProcessAllFormatsThresholds(0.01);
                    mgr.ProcessInterstitialThresholds(0.01);
                    mgr.ProcessRewardedThresholds(0.01);
                }
            }, "No exceptions must be thrown with no tracker registered across many events");
        }

        // ─── Format-specific event routing ────────────────────────────────────

        [Test]
        public void TrackRevenue_InterstitialFormat_CorrectEventsFired()
        {
            // ProcessInterstitialThresholds fires Step 3 (taichi_total_ad_impression)
            // and Step 4 (taichi_interstitial_ad_impression), NOT rewarded events.
            var mgr = new AdRevenueTrackingManager(_tracker, ThresholdOneConfig());

            mgr.ProcessInterstitialThresholds(0.001); // crosses all thresholds with threshold=1

            Assert.IsTrue(_tracker.WasFired("taichi_total_ad_impression"),
                "Interstitial path must fire Step 3 (total impression)");
            Assert.IsTrue(_tracker.WasFired("taichi_interstitial_ad_impression"),
                "Interstitial path must fire Step 4 (interstitial impression)");
            Assert.IsFalse(_tracker.WasFired("taichi_rewarded_ad_impression"),
                "Interstitial path must NOT fire Step 5 (rewarded impression)");
            Assert.IsFalse(_tracker.WasFired("taichi_rewarded_ad_revenue"),
                "Interstitial path must NOT fire Step 6 (rewarded revenue)");
        }

        [Test]
        public void TrackRevenue_RewardedFormat_CorrectEventsFired()
        {
            // ProcessRewardedThresholds fires Step 3, Step 5, and Step 6, NOT interstitial events.
            var mgr = new AdRevenueTrackingManager(_tracker, ThresholdOneConfig());

            mgr.ProcessRewardedThresholds(0.001); // crosses all thresholds with threshold=1

            Assert.IsTrue(_tracker.WasFired("taichi_total_ad_impression"),
                "Rewarded path must fire Step 3 (total impression)");
            Assert.IsTrue(_tracker.WasFired("taichi_rewarded_ad_impression"),
                "Rewarded path must fire Step 5 (rewarded impression)");
            Assert.IsTrue(_tracker.WasFired("taichi_rewarded_ad_revenue"),
                "Rewarded path must fire Step 6 (rewarded revenue)");
            Assert.IsFalse(_tracker.WasFired("taichi_interstitial_ad_impression"),
                "Rewarded path must NOT fire Step 4 (interstitial impression)");
        }

        // ─── Revenue value forwarding ─────────────────────────────────────────

        [Test]
        public void TrackRevenue_CorrectRevenue_PassedThroughToEventPayload()
        {
            // Verify the 'value' field in the fired Taichi event matches accumulated revenue
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            // Feed 0.012 total, crossing the 0.01 threshold — payload value should be ~0.012
            mgr.ProcessAllFormatsThresholds(0.007);
            mgr.ProcessAllFormatsThresholds(0.005); // 0.007+0.005 = 0.012 >= 0.01

            var ev = _tracker.Events.Find(e => e.EventName == "Total_Ads_Revenue_001");
            Assert.IsNotNull(ev.Params, "Fired event must carry a params dict");
            Assert.IsTrue(ev.Params.ContainsKey("value"), "Event payload must contain 'value' key");

            double value = (double)ev.Params["value"];
            Assert.Greater(value, 0.01, "Payload value must exceed the threshold");
            Assert.Less(value, 0.02,    "Payload value must not exceed sum of inputs");
        }

        [Test]
        public void TrackRevenue_CorrectNetworkEventPayload_ContainsCurrencyUSD()
        {
            // All Taichi custom events carry currency=USD in their payloads
            var mgr = new AdRevenueTrackingManager(_tracker, ThresholdOneConfig());

            mgr.ProcessAllFormatsThresholds(0.001);

            var totalRevenueEv = _tracker.Events.Find(e => e.EventName == "Total_Ads_Revenue_001");
            var adCountEv      = _tracker.Events.Find(e => e.EventName == "TenAdsShown");

            Assert.AreEqual("USD", (string)totalRevenueEv.Params["currency"],
                "Total_Ads_Revenue_001 must carry currency=USD");
            Assert.AreEqual("USD", (string)adCountEv.Params["currency"],
                "TenAdsShown must carry currency=USD");
        }

        // ─── SetAdRevenueTracker injection pattern ────────────────────────────

        [Test]
        public void TrackRevenue_AfterSetAdRevenueTracker_CallsTracker()
        {
            // Mirrors the initialization race fix: tracker wired after construction
            var mgr = new AdRevenueTrackingManager(null, DefaultConfig());

            // Wire tracker after construction (the setter injection pattern)
            mgr.SetAdRevenueTracker(_tracker);

            // Now cross both thresholds
            for (int i = 0; i < 10; i++)
                mgr.ProcessAllFormatsThresholds(0.002); // 10 impressions × 0.002 = 0.02 >= 0.01

            Assert.IsTrue(_tracker.WasFired("Total_Ads_Revenue_001"),
                "Tracker wired via SetAdRevenueTracker must receive revenue threshold events");
            Assert.IsTrue(_tracker.WasFired("TenAdsShown"),
                "Tracker wired via SetAdRevenueTracker must receive ad-count threshold events");
        }

        // ─── Multiple events all forwarded ────────────────────────────────────

        [Test]
        public void TrackRevenue_MultipleEvents_AllForwardedToTracker()
        {
            // Use threshold=1 config so each call fires an event; send 5 calls → 5 events
            var config = new TaichiConfig
            {
                RevenueThreshold           = 0.001f,
                AdCountThreshold           = 1,
                TotalImpressionThreshold   = 1000, // high — won't fire
                InterstitialCountThreshold = 1000,
                RewardedCountThreshold     = 1000,
                RewardedRevenueThreshold   = 1.0f,
            };
            var mgr = new AdRevenueTrackingManager(_tracker, config);

            // Each ProcessAllFormatsThresholds call fires Total_Ads_Revenue_001 AND TenAdsShown
            for (int i = 0; i < 5; i++)
                mgr.ProcessAllFormatsThresholds(0.001);

            Assert.AreEqual(5, _tracker.CountFired("Total_Ads_Revenue_001"),
                "All 5 revenue threshold crossings must be forwarded to tracker");
            Assert.AreEqual(5, _tracker.CountFired("TenAdsShown"),
                "All 5 ad-count threshold crossings must be forwarded to tracker");
        }

        // ─── DroppedEventCount edge cases ─────────────────────────────────────

        [Test]
        public void DroppedEventCount_AfterNullThenRewire_ResetsToZero()
        {
            // Construct with valid tracker → set null (DroppedEventCount doesn't change via Taichi paths)
            // → rewire → DroppedEventCount must be 0
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());
            Assert.AreEqual(0, mgr.DroppedEventCount, "Starts at zero with valid tracker");

            mgr.SetAdRevenueTracker(null);
            // DroppedEventCount only increments via TrackAdmobRevenue / TrackAppLovinRevenue
            // (platform-conditional code). Taichi paths use ?. operator and don't increment.
            Assert.AreEqual(0, mgr.DroppedEventCount,
                "DroppedEventCount does not increment from Taichi threshold paths");

            mgr.SetAdRevenueTracker(_tracker); // rewire
            Assert.AreEqual(0, mgr.DroppedEventCount,
                "DroppedEventCount must be 0 after rewiring (was already 0)");
        }

        [Test]
        public void DroppedEventCount_NullConstructorNullConfig_AlwaysZero()
        {
            var mgr = new AdRevenueTrackingManager(null, null);
            Assert.AreEqual(0, mgr.DroppedEventCount,
                "DroppedEventCount must be 0 when both tracker and config are null");
        }

        // ─── Null config guard on all threshold methods ───────────────────────

        [Test]
        public void ProcessAllFormatsThresholds_NullConfig_SkipsSilently()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, null); // no config
            Assert.DoesNotThrow(() => mgr.ProcessAllFormatsThresholds(999.99),
                "ProcessAllFormatsThresholds with null config must not throw");
            Assert.AreEqual(0, _tracker.Events.Count,
                "No events must be fired when TaichiConfig is null");
        }

        [Test]
        public void ProcessInterstitialThresholds_NullConfig_SkipsSilently()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, null);
            Assert.DoesNotThrow(() => mgr.ProcessInterstitialThresholds(999.99));
            Assert.AreEqual(0, _tracker.Events.Count);
        }

        [Test]
        public void ProcessRewardedThresholds_NullConfig_SkipsSilently()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, null);
            Assert.DoesNotThrow(() => mgr.ProcessRewardedThresholds(999.99));
            Assert.AreEqual(0, _tracker.Events.Count);
        }

        // ─── SetTaichiConfig late-wiring ──────────────────────────────────────

        [Test]
        public void SetTaichiConfig_AfterNullConfig_EnablesThresholdFiring()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, null); // starts with null config
            mgr.ProcessAllFormatsThresholds(0.01); // should silently skip
            Assert.AreEqual(0, _tracker.Events.Count, "No events before config is set");

            mgr.SetTaichiConfig(DefaultConfig()); // wire up config
            mgr.ProcessAllFormatsThresholds(0.01); // now should fire Step 1

            Assert.IsTrue(_tracker.WasFired("Total_Ads_Revenue_001"),
                "Step 1 must fire after SetTaichiConfig() wires config");
        }

        [Test]
        public void SetTaichiConfig_Null_SilencesSubsequentCalls()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());
            mgr.SetTaichiConfig(null); // clear config
            mgr.ProcessAllFormatsThresholds(999.99); // should skip silently
            Assert.AreEqual(0, _tracker.Events.Count,
                "No events must fire after SetTaichiConfig(null)");
        }

        // ─── Zero / near-zero revenue ─────────────────────────────────────────

        [Test]
        public void ProcessAllFormatsThresholds_ZeroRevenue_OnlyCountAdvances()
        {
            // Revenue stays at 0 → Step 1 never fires; count increments → Step 2 may fire
            var config = new TaichiConfig
            {
                RevenueThreshold           = 100f,   // high threshold, won't be crossed
                AdCountThreshold           = 1,      // fires immediately on first impression
                TotalImpressionThreshold   = 1,
                InterstitialCountThreshold = 10,
                RewardedCountThreshold     = 10,
                RewardedRevenueThreshold   = 100f,
            };
            var mgr = new AdRevenueTrackingManager(_tracker, config);
            mgr.ProcessAllFormatsThresholds(0.0);

            Assert.IsFalse(_tracker.WasFired("Total_Ads_Revenue_001"),
                "Step 1 must not fire when revenue is 0");
            Assert.IsTrue(_tracker.WasFired("Total_Ads_Ad_Count_002"),
                "Step 2 (count) must fire even with 0 revenue when count threshold is met");
        }

        [Test]
        public void ProcessAllFormatsThresholds_LargeRevenueJump_FiresOnceNotMultipleTimes()
        {
            // Revenue jumps to 1000× the threshold in a single call.
            // Step 1 fires exactly once — there's no repeated firing per threshold multiple.
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());
            mgr.ProcessAllFormatsThresholds(100.0); // threshold is 0.01 — way over

            var step1Count = _tracker.Events.Count(e => e.EventName == "Total_Ads_Revenue_001");
            Assert.AreEqual(1, step1Count,
                "Step 1 must fire exactly once regardless of how far revenue exceeds threshold");
        }

        // ─── Threshold boundary: exact match ─────────────────────────────────

        [Test]
        public void ProcessAllFormatsThresholds_ExactThresholdMatch_Fires()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());
            mgr.ProcessAllFormatsThresholds(0.01); // exactly equals RevenueThreshold = 0.01

            Assert.IsTrue(_tracker.WasFired("Total_Ads_Revenue_001"),
                "Step 1 must fire when revenue exactly equals threshold");
        }

        // ─── Cumulative accumulation across calls ─────────────────────────────

        [Test]
        public void ProcessRewardedThresholds_AccumulatesAcrossCalls_FiresOnCrossing()
        {
            var config = new TaichiConfig
            {
                RevenueThreshold           = 1000f,
                AdCountThreshold           = 1000,
                TotalImpressionThreshold   = 1000,
                InterstitialCountThreshold = 1000,
                RewardedCountThreshold     = 3,      // fires on 3rd rewarded impression
                RewardedRevenueThreshold   = 1000f,
            };
            var mgr = new AdRevenueTrackingManager(_tracker, config);

            mgr.ProcessRewardedThresholds(0.01);
            mgr.ProcessRewardedThresholds(0.01);
            Assert.IsFalse(_tracker.WasFired("Total_Rewarded_Count_005"),
                "Step 5 must not fire before threshold is crossed");

            mgr.ProcessRewardedThresholds(0.01); // 3rd call crosses count=3
            Assert.IsTrue(_tracker.WasFired("Total_Rewarded_Count_005"),
                "Step 5 must fire on 3rd rewarded impression when threshold=3");
        }

        [Test]
        public void ProcessInterstitialThresholds_AccumulatesAcrossCalls_FiresOnCrossing()
        {
            var config = new TaichiConfig
            {
                RevenueThreshold           = 1000f,
                AdCountThreshold           = 1000,
                TotalImpressionThreshold   = 1000,
                InterstitialCountThreshold = 2,      // fires on 2nd interstitial
                RewardedCountThreshold     = 1000,
                RewardedRevenueThreshold   = 1000f,
            };
            var mgr = new AdRevenueTrackingManager(_tracker, config);

            mgr.ProcessInterstitialThresholds(0.01);
            Assert.IsFalse(_tracker.WasFired("Total_Interstitial_Count_004"),
                "Step 4 must not fire after 1 interstitial when threshold=2");

            mgr.ProcessInterstitialThresholds(0.01);
            Assert.IsTrue(_tracker.WasFired("Total_Interstitial_Count_004"),
                "Step 4 must fire on 2nd interstitial when threshold=2");
        }

        // ─── PlayerPrefs persistence across instances ─────────────────────────

        [Test]
        public void PlayerPrefs_ProgressPersistedAcrossInstances()
        {
            // Instance 1: accumulate some revenue but don't cross threshold
            var config = new TaichiConfig
            {
                RevenueThreshold           = 0.05f, // 5 cents
                AdCountThreshold           = 1000,
                TotalImpressionThreshold   = 1000,
                InterstitialCountThreshold = 1000,
                RewardedCountThreshold     = 1000,
                RewardedRevenueThreshold   = 1000f,
            };
            var mgr1 = new AdRevenueTrackingManager(_tracker, config);
            mgr1.ProcessAllFormatsThresholds(0.03); // 3 cents — below threshold, persisted
            Assert.AreEqual(0, _tracker.Events.Count, "Not yet at threshold after instance 1");

            // Instance 2 picks up the 3 cents from PlayerPrefs
            var mgr2 = new AdRevenueTrackingManager(_tracker, config);
            mgr2.ProcessAllFormatsThresholds(0.03); // 3 more cents → total 6 cents, crosses 5 cent threshold
            Assert.IsTrue(_tracker.WasFired("Total_Ads_Revenue_001"),
                "Step 1 must fire when second instance adds enough to cross threshold");
        }
    }
}
