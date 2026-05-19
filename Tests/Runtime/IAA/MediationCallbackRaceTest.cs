using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime.IAA
{
    /// <summary>
    /// Race-condition tests for <see cref="AdRevenueTrackingManager"/> when both AppLovin
    /// and AdMob revenue callbacks fire simultaneously inside <c>MediationManager</c>.
    ///
    /// <b>Architecture contract (enforced by tests below):</b>
    ///
    /// <c>MediationManager.SubscribeAdmobRevenueEvents</c> and
    /// <c>SubscribeAppLovinRevenueEvents</c> both wrap revenue callbacks in
    /// <c>PostToMainThread()</c>. This serialises concurrent OS-thread callbacks
    /// (AppLovin MAX background thread, AdMob GMA JNI thread) onto Unity's main
    /// thread before touching <see cref="AdRevenueTrackingManager"/>. The design
    /// depends on this serialisation to avoid two known races:
    ///
    ///   Race A — PlayerPrefs threading:
    ///     <c>ProcessAllFormatsThresholds</c> calls <c>PlayerPrefs.GetFloat/SetFloat</c>,
    ///     which Unity restricts to the main thread. Calling it from a background thread
    ///     throws <see cref="UnityException"/>. Without <c>PostToMainThread</c> the
    ///     entire revenue event is silently lost (exception is swallowed by the ad SDK).
    ///
    ///   Race B — non-volatile tracker field:
    ///     <c>_adRevenueTracker</c> is a plain field. <c>SetAdRevenueTracker</c> writes
    ///     it on the main thread; revenue callbacks read it. Without <c>volatile</c> or
    ///     locking, a background-thread reader may see a stale value. A null read causes
    ///     the event to be silently dropped and <c>_droppedEventCount</c> incremented.
    ///
    /// These tests cover:
    ///   Group S — Sequential dispatch simulation (PostToMainThread guarantee)
    ///   Group C — Concurrent SetAdRevenueTracker while processing on main thread
    ///   Group P — PlayerPrefs threading constraint (background-thread calls throw)
    ///   Group T — Thread-safe tracking invariants
    /// </summary>
    [TestFixture]
    public class MediationCallbackRaceTest
    {
        private const string KeyTotalRevenue      = "Noctua_Taichi_TotalRevenue";
        private const string KeyTotalAdCount      = "Noctua_Taichi_TotalAdCount";
        private const string KeyTotalImpressions  = "Noctua_Taichi_TotalImpressions";
        private const string KeyInterstitialCount = "Noctua_Taichi_InterstitialCount";
        private const string KeyRewardedCount     = "Noctua_Taichi_RewardedCount";
        private const string KeyRewardedRevenue   = "Noctua_Taichi_RewardedRevenue";

        private ConcurrentMockTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            _tracker = new ConcurrentMockTracker();

            PlayerPrefs.DeleteKey(KeyTotalRevenue);
            PlayerPrefs.DeleteKey(KeyTotalAdCount);
            PlayerPrefs.DeleteKey(KeyTotalImpressions);
            PlayerPrefs.DeleteKey(KeyInterstitialCount);
            PlayerPrefs.DeleteKey(KeyRewardedCount);
            PlayerPrefs.DeleteKey(KeyRewardedRevenue);
            PlayerPrefs.Save();

            // Suppress expected warning/error logs for tests that deliberately pass
            // null trackers or trigger threshold-dropped conditions.
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private TaichiConfig DefaultConfig() => new TaichiConfig
        {
            RevenueThreshold           = 0.01f,
            AdCountThreshold           = 10,
            TotalImpressionThreshold   = 10,
            InterstitialCountThreshold = 5,
            RewardedCountThreshold     = 5,
            RewardedRevenueThreshold   = 0.01f,
        };

        private TaichiConfig HighThresholdConfig() => new TaichiConfig
        {
            RevenueThreshold           = 1000f,
            AdCountThreshold           = 100000,
            TotalImpressionThreshold   = 100000,
            InterstitialCountThreshold = 100000,
            RewardedCountThreshold     = 100000,
            RewardedRevenueThreshold   = 1000f,
        };

        // ─── Group S: Sequential dispatch simulation ──────────────────────────
        //
        // PostToMainThread serialises concurrent OS callbacks onto the Unity
        // main thread. These tests simulate that sequential dispatch and pin
        // the correctness invariants when AppLovin and AdMob fire interleaved.

        [Test]
        public void S1_AppLovinThenAdMob_BothImpressions_AreCounted()
        {
            // Simulate PostToMainThread executing sequentially:
            // 1. AppLovin revenue callback arrives first
            // 2. AdMob revenue callback arrives second
            // Both should increment the Taichi count (AdCountThreshold = 100000, we send 2).
            var mgr = new AdRevenueTrackingManager(_tracker, HighThresholdConfig());

            // AppLovin impression — sequential on main thread (what PostToMainThread does)
            mgr.ProcessAllFormatsThresholds(0.001);

            // AdMob impression — sequential on main thread
            mgr.ProcessAllFormatsThresholds(0.001);

            int stored = PlayerPrefs.GetInt(KeyTotalAdCount, 0);
            Assert.AreEqual(2, stored,
                "Both AppLovin and AdMob impressions must each increment the total ad count");
        }

        [Test]
        public void S2_AdMobThenAppLovin_BothImpressions_AreCounted()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, HighThresholdConfig());

            // AdMob first, AppLovin second (reversed order)
            mgr.ProcessAllFormatsThresholds(0.001);
            mgr.ProcessAllFormatsThresholds(0.001);

            int stored = PlayerPrefs.GetInt(KeyTotalAdCount, 0);
            Assert.AreEqual(2, stored,
                "Reversed callback order must still produce count = 2");
        }

        [Test]
        public void S3_HighVolume_AlternatingCallbacks_TotalCountMatchesInput()
        {
            // Simulate 100 alternating AppLovin / AdMob impressions.
            // With HighThresholdConfig no thresholds are crossed so no
            // reset happens — the raw stored count reflects all 100 calls.
            const int impressions = 100;
            var mgr = new AdRevenueTrackingManager(_tracker, HighThresholdConfig());

            for (int i = 0; i < impressions; i++)
                mgr.ProcessAllFormatsThresholds(0.0001);

            int stored = PlayerPrefs.GetInt(KeyTotalAdCount, 0);
            Assert.AreEqual(impressions, stored,
                $"All {impressions} sequential impressions must be counted");
        }

        [Test]
        public void S4_ThresholdCrossing_AppLovinAndAdMob_FiresExactlyOnce()
        {
            // Threshold = 10 impressions. AppLovin sends 5, AdMob sends 5.
            // Combined = 10 → TenAdsShown fires exactly once.
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            // 5 AppLovin callbacks
            for (int i = 0; i < 5; i++)
                mgr.ProcessAllFormatsThresholds(0);

            // 5 AdMob callbacks — the 10th crosses the threshold
            for (int i = 0; i < 5; i++)
                mgr.ProcessAllFormatsThresholds(0);

            Assert.AreEqual(1, _tracker.CountFired("TenAdsShown"),
                "Threshold must fire exactly once when AppLovin + AdMob combined = 10");
        }

        [Test]
        public void S5_InterstitialFromBothNetworks_Step4_FiresAtCombinedThreshold()
        {
            // Step 4 fires when the interstitial-only counter reaches its threshold (5).
            // AppLovin sends 3 interstitials, AdMob sends 2 → total = 5 = threshold.
            // Step 3 shares a separate total-impression counter (KeyTotalImpressions),
            // also incremented here; with threshold = 10 it does NOT fire yet.
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            // AppLovin interstitials (simulated via ProcessInterstitialThresholds)
            for (int i = 0; i < 3; i++)
                mgr.ProcessInterstitialThresholds(0);

            // AdMob interstitials
            for (int i = 0; i < 2; i++)
                mgr.ProcessInterstitialThresholds(0);

            Assert.IsTrue(_tracker.WasFired("taichi_interstitial_ad_impression"),
                "Step 4 must fire when both networks combined reach interstitial threshold (5)");
        }

        [Test]
        public void S6_RewardedFromBothNetworks_Step5_FiresAtCombinedThreshold()
        {
            // AppLovin sends 2 rewarded, AdMob sends 3 rewarded — total = 5 = threshold.
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            for (int i = 0; i < 2; i++)
                mgr.ProcessRewardedThresholds(0.001);

            for (int i = 0; i < 3; i++)
                mgr.ProcessRewardedThresholds(0.001);

            Assert.IsTrue(_tracker.WasFired("taichi_rewarded_ad_impression"),
                "Step 5 must fire when both networks combined reach rewarded threshold (5)");
        }

        // ─── Group C: Concurrent SetAdRevenueTracker ─────────────────────────
        //
        // Race B: _adRevenueTracker is not volatile. A background thread calling
        // SetAdRevenueTracker races with the main thread reading _adRevenueTracker
        // inside ProcessAllFormatsThresholds. The invariant is: no unhandled
        // exception. An event may go to either the old or new tracker, or be
        // silently dropped if the field reads null — all outcomes are safe.

        [Test]
        public void C1_SetAdRevenueTracker_FromBackground_WhileMainThreadProcesses_NoException()
        {
            // Background thread continuously swaps tracker between valid and null.
            // Main thread processes impressions. Neither must throw or deadlock.
            var tracker = new ConcurrentMockTracker();
            var mgr = new AdRevenueTrackingManager(tracker, HighThresholdConfig());

            using var cts = new CancellationTokenSource();

            var bgTask = Task.Run(() =>
            {
                while (!cts.IsCancellationRequested)
                {
                    mgr.SetAdRevenueTracker(tracker);
                    Thread.Sleep(0); // give scheduler a chance to interleave
                    mgr.SetAdRevenueTracker(null);
                    Thread.Sleep(0);
                    mgr.SetAdRevenueTracker(tracker);
                }
            }, cts.Token);

            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 80; i++)
                    mgr.ProcessAllFormatsThresholds(0.0001);
            }, "Main-thread processing must not throw even when background swaps tracker");

            cts.Cancel();
            bool stopped = bgTask.Wait(1000); // deadlock guard
            Assert.IsTrue(stopped, "Background task must stop within 1000ms after cancellation");
        }

        [Test]
        public void C2_SetAdRevenueTracker_NullMidFlight_NoCrash_EventsLostAtMost()
        {
            // Tracker is valid, accumulate to just below threshold.
            // Set tracker to null — next threshold crossing finds null → event lost.
            // No exception must be thrown; the drop is documented behaviour.
            var tracker = new ConcurrentMockTracker();
            var mgr = new AdRevenueTrackingManager(tracker, DefaultConfig());

            // 9 impressions — threshold = 10, so TenAdsShown not yet fired
            for (int i = 0; i < 9; i++)
                mgr.ProcessAllFormatsThresholds(0);

            // Swap to null from a background thread (simulating late SetAdRevenueTracker)
            Task.Run(() => mgr.SetAdRevenueTracker(null)).Wait(200);

            // 10th impression — crosses threshold but _adRevenueTracker may be null
            Assert.DoesNotThrow(() => mgr.ProcessAllFormatsThresholds(0),
                "Crossing threshold with null tracker must not throw");

            // The event was either tracked (race won by main thread) or lost (race won by bg).
            // Either is acceptable — no crash is the invariant.
            int fired = tracker.CountFired("TenAdsShown");
            Assert.IsTrue(fired == 0 || fired == 1,
                "TenAdsShown must fire 0 or 1 times — never more than once regardless of race");
        }

        [Test]
        public void C3_RewireTrackerAfterNull_SubsequentCallsReachNewTracker()
        {
            // Classic lifecycle: tracker → null → new tracker.
            // Revenue must reach the new tracker after rewiring.
            var tracker1 = new ConcurrentMockTracker();
            var tracker2 = new ConcurrentMockTracker();
            var mgr = new AdRevenueTrackingManager(tracker1, DefaultConfig());

            mgr.ProcessAllFormatsThresholds(0.01); // crosses revenue threshold on tracker1
            Assert.IsTrue(tracker1.WasFired("Total_Ads_Revenue_001"), "tracker1 must receive first threshold event");

            mgr.SetAdRevenueTracker(null);
            mgr.ProcessAllFormatsThresholds(0.01); // crossed but null → silently lost

            mgr.SetAdRevenueTracker(tracker2);
            mgr.ProcessAllFormatsThresholds(0.01); // tracker2 now active → receives this event

            Assert.IsTrue(tracker2.WasFired("Total_Ads_Revenue_001"),
                "tracker2 must receive threshold event after rewiring");
            Assert.AreEqual(1, tracker1.CountFired("Total_Ads_Revenue_001"),
                "tracker1 must not receive additional events after being replaced");
        }

        [Test]
        public void C4_SetAdRevenueTracker_LoopFromBackground_ManagerAlwaysStable()
        {
            // Stress: background thread cycles tracker 200 times while main thread
            // processes 200 impressions. No deadlock, no exception.
            var tracker = new ConcurrentMockTracker();
            var mgr = new AdRevenueTrackingManager(tracker, HighThresholdConfig());

            const int iterations = 200;
            using var cts = new CancellationTokenSource();

            var bgTask = Task.Run(() =>
            {
                for (int i = 0; i < iterations && !cts.IsCancellationRequested; i++)
                {
                    mgr.SetAdRevenueTracker(i % 2 == 0 ? (IAdRevenueTracker)tracker : null);
                    Thread.Sleep(0);
                }
            }, cts.Token);

            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < iterations; i++)
                    mgr.ProcessAllFormatsThresholds(0.0001);
            });

            cts.Cancel();
            bool stopped = bgTask.Wait(1000); // deadlock guard
            Assert.IsTrue(stopped, "Background task must stop within 1000ms after cancellation");
        }

        [Test]
        public void C5_SetAdRevenueTrackerCalledFromMultipleBackgroundThreads_NoException()
        {
            // Multiple background threads simultaneously call SetAdRevenueTracker.
            // Only one thread's value wins — no crash is the invariant.
            var tracker1 = new ConcurrentMockTracker();
            var tracker2 = new ConcurrentMockTracker();
            var tracker3 = new ConcurrentMockTracker();
            var mgr = new AdRevenueTrackingManager(tracker1, HighThresholdConfig());

            var tasks = new Task[]
            {
                Task.Run(() => { for (int i = 0; i < 100; i++) { mgr.SetAdRevenueTracker(tracker1); Thread.Yield(); } }),
                Task.Run(() => { for (int i = 0; i < 100; i++) { mgr.SetAdRevenueTracker(tracker2); Thread.Yield(); } }),
                Task.Run(() => { for (int i = 0; i < 100; i++) { mgr.SetAdRevenueTracker(tracker3); Thread.Yield(); } }),
            };

            // Main thread processes impressions concurrently
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 50; i++)
                    mgr.ProcessAllFormatsThresholds(0.0001);
            });

            Assert.DoesNotThrow(() => Task.WaitAll(tasks),
                "Concurrent SetAdRevenueTracker from multiple threads must not throw");
        }

        // ─── Group P: PlayerPrefs threading constraint ─────────────────────────
        //
        // ProcessAllFormatsThresholds reads/writes PlayerPrefs which Unity
        // restricts to the main thread. When MediationManager's PostToMainThread
        // has a null context (Editor / tests), it falls back to inline execution
        // on the calling thread. A background-thread caller therefore throws
        // UnityException. This is the expected behaviour — PostToMainThread
        // guarantees main-thread execution in production.

        [Test]
        public void P1_SingleBackgroundThread_ThrowsUnityException_MainThreadConstraint()
        {
            // Regression: AppLovin MAX callback arrives on background thread.
            // Without PostToMainThread → inline fallback → background thread
            // → PlayerPrefs.GetFloat throws UnityException.
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            var ex = Assert.Throws<AggregateException>(() =>
                Task.Run(() => mgr.ProcessAllFormatsThresholds(0.01)).Wait());

            Assert.IsInstanceOf<UnityException>(ex.InnerException,
                "Background-thread ProcessAllFormatsThresholds must throw UnityException (PlayerPrefs main-thread-only)");
        }

        [Test]
        public void P2_TwoSimultaneousBackgroundThreads_BothThrowUnityException_NoDeadlock()
        {
            // Simulates both AppLovin and AdMob callbacks arriving simultaneously
            // when _mainThreadContext == null (inline fallback path in PostToMainThread).
            // Both must throw UnityException — neither should deadlock or swallow the other.
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            var t1 = Task.Run(() => mgr.ProcessAllFormatsThresholds(0.01));
            var t2 = Task.Run(() => mgr.ProcessAllFormatsThresholds(0.01));

            // Task.Wait() throws AggregateException when the task is faulted, so we cannot
            // use its bool return value to check for deadlock. Catch the expected exception
            // and separately verify both tasks reached a terminal state within the timeout.
            var whenAll = Task.WhenAll(t1, t2);
            try
            {
                whenAll.Wait(3000); // deadlock guard — faulted tasks complete (and throw) immediately
            }
            catch (AggregateException)
            {
                // expected — both tasks threw UnityException from PlayerPrefs
            }

            Assert.IsTrue(whenAll.IsCompleted,
                "Both background threads must reach a terminal state within 3 seconds (no deadlock)");
            Assert.IsTrue(t1.IsFaulted, "t1 (AppLovin callback sim) must have faulted with UnityException");
            Assert.IsTrue(t2.IsFaulted, "t2 (AdMob callback sim) must have faulted with UnityException");

            bool t1IsUnity = t1.Exception?.InnerException is UnityException ||
                             (t1.Exception?.InnerExceptions.Count > 0 &&
                              t1.Exception.InnerExceptions[0] is UnityException);
            bool t2IsUnity = t2.Exception?.InnerException is UnityException ||
                             (t2.Exception?.InnerExceptions.Count > 0 &&
                              t2.Exception.InnerExceptions[0] is UnityException);

            Assert.IsTrue(t1IsUnity, $"t1 exception must be UnityException, got: {t1.Exception?.InnerException?.GetType().Name}");
            Assert.IsTrue(t2IsUnity, $"t2 exception must be UnityException, got: {t2.Exception?.InnerException?.GetType().Name}");
        }

        [Test]
        public void P3_MainThreadProcessing_NeverThrows_SameCallThatFailsOnBackground()
        {
            // Contrast with P1/P2: the exact same call on the main thread must succeed.
            // This is what PostToMainThread guarantees in production.
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            Assert.DoesNotThrow(() =>
            {
                mgr.ProcessAllFormatsThresholds(0.01);
                mgr.ProcessInterstitialThresholds(0.01);
                mgr.ProcessRewardedThresholds(0.01);
            }, "All threshold methods must succeed on the Unity main thread");
        }

        [Test]
        public void P4_InterstitialThresholds_BackgroundThread_ThrowsUnityException()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            var ex = Assert.Throws<AggregateException>(() =>
                Task.Run(() => mgr.ProcessInterstitialThresholds(0.01)).Wait());

            Assert.IsInstanceOf<UnityException>(ex.InnerException,
                "ProcessInterstitialThresholds on background thread must throw UnityException");
        }

        [Test]
        public void P5_RewardedThresholds_BackgroundThread_ThrowsUnityException()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            var ex = Assert.Throws<AggregateException>(() =>
                Task.Run(() => mgr.ProcessRewardedThresholds(0.01)).Wait());

            Assert.IsInstanceOf<UnityException>(ex.InnerException,
                "ProcessRewardedThresholds on background thread must throw UnityException");
        }

        // ─── Group T: Thread-safe tracking invariants ─────────────────────────

        [Test]
        public void T1_SequentialMixedCallbacks_ConcurrentMockTracker_ExactEventCount()
        {
            // With a thread-safe tracker and sequential main-thread dispatch,
            // every threshold crossing must be recorded exactly once.
            // AdCountThreshold = 1 so every impression fires TenAdsShown.
            var config = new TaichiConfig
            {
                RevenueThreshold           = 1000f,
                AdCountThreshold           = 1,      // fires on every impression
                TotalImpressionThreshold   = 100000,
                InterstitialCountThreshold = 100000,
                RewardedCountThreshold     = 100000,
                RewardedRevenueThreshold   = 1000f,
            };
            var mgr = new AdRevenueTrackingManager(_tracker, config);

            const int appLovinImpressions = 5;
            const int admobImpressions    = 5;

            for (int i = 0; i < appLovinImpressions; i++)
                mgr.ProcessAllFormatsThresholds(0);

            for (int i = 0; i < admobImpressions; i++)
                mgr.ProcessAllFormatsThresholds(0);

            int total = _tracker.CountFired("TenAdsShown");
            Assert.AreEqual(appLovinImpressions + admobImpressions, total,
                $"ConcurrentMockTracker must record exactly {appLovinImpressions + admobImpressions} TenAdsShown events");
        }

        [Test]
        public void T2_NullTrackerThenRewire_ConcurrentMockTracker_NoLostCallAfterRewire()
        {
            // After rewiring, every impression must reach the tracker.
            var mgr = new AdRevenueTrackingManager(null, DefaultConfig());
            mgr.SetAdRevenueTracker(_tracker);

            for (int i = 0; i < 10; i++)
                mgr.ProcessAllFormatsThresholds(0);

            Assert.IsTrue(_tracker.WasFired("TenAdsShown"),
                "ConcurrentMockTracker must receive TenAdsShown after rewire from null");
        }

        [Test]
        public void T3_DoubleFirePrevention_ThresholdFiredOnce_EvenWithLargeRevenue()
        {
            // A very large single impression (100× revenue threshold) must fire
            // Total_Ads_Revenue_001 exactly once — there is no loop in the threshold logic.
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            mgr.ProcessAllFormatsThresholds(100.0); // threshold = 0.01 → massively over

            Assert.AreEqual(1, _tracker.CountFired("Total_Ads_Revenue_001"),
                "Revenue threshold event must fire exactly once regardless of how far revenue exceeds threshold");
        }

        [Test]
        public void T4_SharedTotalImpressionCounter_AppLovinPlusAdMob_FiresOnce()
        {
            // Step 3 (taichi_total_ad_impression) uses a counter shared between
            // interstitial and rewarded impressions from any network.
            // 5 interstitials from AppLovin + 5 from AdMob = 10 = threshold.
            // Must fire exactly once — not twice (one per network).
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            for (int i = 0; i < 5; i++) mgr.ProcessInterstitialThresholds(0);
            for (int i = 0; i < 5; i++) mgr.ProcessInterstitialThresholds(0);

            Assert.AreEqual(1, _tracker.CountFired("taichi_total_ad_impression"),
                "Shared total-impression counter must fire Step 3 exactly once across networks");
        }

        [Test]
        public void T5_RevenueAccumulation_AppLovinAndAdMob_AccumulatesCorrectly()
        {
            // Revenue threshold = 0.01.
            // AppLovin contributes 0.006, AdMob contributes 0.005.
            // Combined 0.011 must fire Total_Ads_Revenue_001.
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            mgr.ProcessAllFormatsThresholds(0.006); // AppLovin impression
            mgr.ProcessAllFormatsThresholds(0.005); // AdMob impression

            Assert.IsTrue(_tracker.WasFired("Total_Ads_Revenue_001"),
                "Revenue accumulated across AppLovin and AdMob impressions must trigger threshold");
        }

        [Test]
        public void T6_NullConfig_BothNetworkCallbacks_NoEvents_NoException()
        {
            // Taichi disabled (null config) — both callbacks fire many times with no crash.
            var mgr = new AdRevenueTrackingManager(_tracker, taichiConfig: null);

            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 20; i++)
                {
                    mgr.ProcessAllFormatsThresholds(0.01);
                    mgr.ProcessInterstitialThresholds(0.01);
                    mgr.ProcessRewardedThresholds(0.01);
                }
            }, "Null TaichiConfig must suppress all threshold processing without throwing");

            Assert.AreEqual(0, _tracker.TotalEventCount,
                "No tracker events must be recorded when Taichi config is null");
        }

        [Test]
        public void T7_DroppedEventCount_OnlyIncrementsInPlatformGatedCode_NotInTaichiPaths()
        {
            // DroppedEventCount is incremented only inside the platform-conditional
            // TrackAdmobRevenue (#if UNITY_ADMOB) and TrackAppLovinRevenue (#if UNITY_APPLOVIN)
            // methods. The Taichi threshold paths (ProcessAllFormatsThresholds etc.) use the
            // null-conditional ?. operator and never increment DroppedEventCount.
            //
            // This test documents that contract: firing many impressions through Taichi
            // paths with a null tracker must not increment DroppedEventCount.
            var mgr = new AdRevenueTrackingManager(null, DefaultConfig());

            for (int i = 0; i < 15; i++)
            {
                mgr.ProcessAllFormatsThresholds(0.001);
                mgr.ProcessInterstitialThresholds(0.001);
                mgr.ProcessRewardedThresholds(0.001);
            }

            Assert.AreEqual(0, mgr.DroppedEventCount,
                "Taichi threshold paths must not increment DroppedEventCount — only platform-gated revenue methods do");
        }

        [Test]
        public void T8_MultipleThresholdCycles_BothNetworks_CorrectFireCount()
        {
            // AdCountThreshold = 10. Send 30 impressions (alternating AppLovin/AdMob).
            // TenAdsShown must fire exactly 3 times (one per 10-impression cycle).
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            for (int i = 0; i < 30; i++)
                mgr.ProcessAllFormatsThresholds(0);

            Assert.AreEqual(3, _tracker.CountFired("TenAdsShown"),
                "30 impressions with threshold=10 must fire TenAdsShown exactly 3 times");
        }
    }
}
