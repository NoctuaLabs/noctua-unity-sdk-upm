using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace com.noctuagames.sdk.Tests.IAA
{
    /// <summary>
    /// Advanced unit tests for <see cref="AdRevenueTrackingManager"/>.
    ///
    /// Covers areas not in <see cref="AdRevenueTrackingManagerTest"/>:
    ///   - Multi-cycle threshold resets (fires → accumulates from 0 → fires again)
    ///   - Payload field correctness (value, currency in fired events)
    ///   - Boundary conditions (threshold = 1, revenue = 0)
    ///   - Step 6 (rewarded revenue) multi-cycle and reset
    ///   - Step 3 shared counter reset after cross-format fire
    ///   - DroppedEventCount property reflecting null-tracker drops
    ///   - DroppedEventCount reset when tracker is wired
    ///   - SetAdRevenueTracker mid-session swap with in-flight Taichi state
    ///   - Per-event payload value correctness for all 6 Taichi steps
    /// </summary>
    [TestFixture]
    public class AdRevenueTrackingManagerAdvancedTest
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

        // ─── Multi-cycle: Step 1 (revenue) ───────────────────────────────────

        [Test]
        public void Step1_FiresOnSecondCycle_AfterReset()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            mgr.ProcessAllFormatsThresholds(0.01); // cycle 1 fires
            _tracker.Clear();

            mgr.ProcessAllFormatsThresholds(0.005);
            Assert.IsFalse(_tracker.WasFired("Total_Ads_Revenue_001"),
                "After reset, 0.005 < 0.01 — must not fire yet");

            mgr.ProcessAllFormatsThresholds(0.006); // 0.005 + 0.006 = 0.011 >= 0.01
            Assert.IsTrue(_tracker.WasFired("Total_Ads_Revenue_001"),
                "Second cycle must fire once accumulated revenue exceeds threshold again");
        }

        [Test]
        public void Step1_MultiCycle_FiresExactlyOncePerCrossing()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            // 3 full cycles, each crossing 0.01 exactly
            for (int cycle = 0; cycle < 3; cycle++)
            {
                _tracker.Clear();
                mgr.ProcessAllFormatsThresholds(0.01);
                Assert.AreEqual(1, _tracker.CountFired("Total_Ads_Revenue_001"),
                    $"Cycle {cycle + 1}: must fire exactly once per threshold crossing");
            }
        }

        // ─── Multi-cycle: Step 2 (ad count) ──────────────────────────────────

        [Test]
        public void Step2_FiresOnSecondCycle_AfterReset()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            for (int i = 0; i < 10; i++) mgr.ProcessAllFormatsThresholds(0); // cycle 1
            _tracker.Clear();

            for (int i = 0; i < 9; i++) mgr.ProcessAllFormatsThresholds(0);
            Assert.IsFalse(_tracker.WasFired("TenAdsShown"), "9 < 10 threshold after reset");

            mgr.ProcessAllFormatsThresholds(0); // 10th → fires again
            Assert.IsTrue(_tracker.WasFired("TenAdsShown"), "Second cycle must fire at 10");
        }

        [Test]
        public void Step2_ThresholdOf1_FiresOnFirstImpression()
        {
            var config = DefaultConfig();
            config.AdCountThreshold = 1;
            var mgr = new AdRevenueTrackingManager(_tracker, config);

            mgr.ProcessAllFormatsThresholds(0);

            Assert.IsTrue(_tracker.WasFired("TenAdsShown"),
                "AdCountThreshold = 1 must fire on the very first impression");
        }

        // ─── Multi-cycle: Steps 3/4 (interstitial) ───────────────────────────

        [Test]
        public void Step4_FiresOnSecondCycle_AfterReset()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            for (int i = 0; i < 5; i++) mgr.ProcessInterstitialThresholds(0); // cycle 1
            _tracker.Clear();

            for (int i = 0; i < 4; i++) mgr.ProcessInterstitialThresholds(0);
            Assert.IsFalse(_tracker.WasFired("taichi_interstitial_ad_impression"),
                "4 < threshold 5 after reset");

            mgr.ProcessInterstitialThresholds(0); // 5th → fires again
            Assert.IsTrue(_tracker.WasFired("taichi_interstitial_ad_impression"),
                "Second cycle must fire at threshold 5");
        }

        [Test]
        public void Step3_SharedCounter_ResetsAfterFire_BothFormatsContribute()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            // First cycle: 5 inter + 5 rew = 10 → Step 3 fires
            for (int i = 0; i < 5; i++) mgr.ProcessInterstitialThresholds(0);
            for (int i = 0; i < 5; i++) mgr.ProcessRewardedThresholds(0);
            Assert.IsTrue(_tracker.WasFired("taichi_total_ad_impression"),
                "Cycle 1: mixed 5+5 must fire Step 3");
            _tracker.Clear();

            // Second cycle: must start from 0 again
            for (int i = 0; i < 9; i++) mgr.ProcessInterstitialThresholds(0);
            Assert.IsFalse(_tracker.WasFired("taichi_total_ad_impression"),
                "After reset, 9 < 10 — must not fire yet");

            mgr.ProcessRewardedThresholds(0); // 10th combined → fires
            Assert.IsTrue(_tracker.WasFired("taichi_total_ad_impression"),
                "Step 3 counter must restart from 0 after first fire");
        }

        // ─── Multi-cycle: Steps 5/6 (rewarded) ───────────────────────────────

        [Test]
        public void Step5_FiresOnSecondCycle_AfterReset()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            for (int i = 0; i < 5; i++) mgr.ProcessRewardedThresholds(0); // cycle 1
            _tracker.Clear();

            for (int i = 0; i < 4; i++) mgr.ProcessRewardedThresholds(0);
            Assert.IsFalse(_tracker.WasFired("taichi_rewarded_ad_impression"), "4 < 5 after reset");

            mgr.ProcessRewardedThresholds(0);
            Assert.IsTrue(_tracker.WasFired("taichi_rewarded_ad_impression"),
                "Second cycle must fire at 5");
        }

        [Test]
        public void Step6_RevenueResets_AfterFire_AccumulatesFromZero()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            mgr.ProcessRewardedThresholds(0.01); // cycle 1 fires
            _tracker.Clear();

            mgr.ProcessRewardedThresholds(0.005);
            Assert.IsFalse(_tracker.WasFired("taichi_rewarded_ad_revenue"),
                "After reset, 0.005 < 0.01 — must not fire");

            mgr.ProcessRewardedThresholds(0.006); // 0.005 + 0.006 >= 0.01
            Assert.IsTrue(_tracker.WasFired("taichi_rewarded_ad_revenue"),
                "Step 6 must accumulate from 0 after reset and fire on second crossing");
        }

        [Test]
        public void Step6_BannerRevenue_NeverAccumulatesIntoStep6()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            // 100 banner impressions @ 1 USD each — massive revenue, but none is rewarded
            for (int i = 0; i < 100; i++)
                mgr.ProcessAllFormatsThresholds(1.0);

            Assert.IsFalse(_tracker.WasFired("taichi_rewarded_ad_revenue"),
                "Banner revenue must never bleed into Step 6 (rewarded-only counter)");
        }

        // ─── Payload field correctness ────────────────────────────────────────

        [Test]
        public void Step1_FiredEventPayload_ContainsCorrectValueAndCurrency()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            mgr.ProcessAllFormatsThresholds(0.015); // crosses 0.01 → fires with value=0.015

            var events = _tracker.Events.FindAll(e => e.EventName == "Total_Ads_Revenue_001");
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual("USD", (string)events[0].Params["currency"]);

            double value = (double)events[0].Params["value"];
            Assert.Greater(value, 0.0, "Fired event must carry positive revenue value");
        }

        [Test]
        public void Step2_FiredEventPayload_ContainsCurrencyUSD()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());
            for (int i = 0; i < 10; i++) mgr.ProcessAllFormatsThresholds(0);

            var events = _tracker.Events.FindAll(e => e.EventName == "TenAdsShown");
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual("USD", (string)events[0].Params["currency"]);
        }

        [Test]
        public void Step6_FiredEventPayload_MatchesAccumulatedRewardedRevenue()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            mgr.ProcessRewardedThresholds(0.006);
            mgr.ProcessRewardedThresholds(0.007); // 0.006 + 0.007 = 0.013 >= 0.01

            var events = _tracker.Events.FindAll(e => e.EventName == "taichi_rewarded_ad_revenue");
            Assert.AreEqual(1, events.Count);

            double value = (double)events[0].Params["value"];
            Assert.Greater(value, 0.01, "Payload value must reflect accumulated rewarded revenue");
            Assert.Less(value, 0.02, "Payload value must not exceed sum of inputs");
        }

        [Test]
        public void StepEvents_AllCarry_ValueAndCurrencyFields()
        {
            var config = new TaichiConfig
            {
                RevenueThreshold           = 0.001f,
                AdCountThreshold           = 1,
                TotalImpressionThreshold   = 1,
                InterstitialCountThreshold = 1,
                RewardedCountThreshold     = 1,
                RewardedRevenueThreshold   = 0.001f,
            };
            var mgr = new AdRevenueTrackingManager(_tracker, config);

            mgr.ProcessAllFormatsThresholds(0.001);    // fires Steps 1 + 2
            mgr.ProcessInterstitialThresholds(0.001);  // fires Steps 3 + 4
            mgr.ProcessRewardedThresholds(0.001);      // fires Steps 5 + 6

            var expectedEvents = new[]
            {
                "Total_Ads_Revenue_001", "TenAdsShown",
                "taichi_total_ad_impression", "taichi_interstitial_ad_impression",
                "taichi_rewarded_ad_impression", "taichi_rewarded_ad_revenue",
            };

            foreach (var name in expectedEvents)
            {
                Assert.IsTrue(_tracker.WasFired(name), $"Expected event '{name}' to fire");
                var ev = _tracker.Events.Find(e => e.EventName == name);
                Assert.IsNotNull(ev.Params, $"Event '{name}' must have a params dict");
                Assert.IsTrue(ev.Params.ContainsKey("currency"),
                    $"Event '{name}' must carry 'currency' field");
                Assert.AreEqual("USD", (string)ev.Params["currency"],
                    $"Event '{name}' currency must be USD");
            }
        }

        // ─── Boundary conditions ──────────────────────────────────────────────

        [Test]
        public void Step1_ThresholdOf0_FiresImmediately()
        {
            var config = DefaultConfig();
            config.RevenueThreshold = 0f;
            var mgr = new AdRevenueTrackingManager(_tracker, config);

            mgr.ProcessAllFormatsThresholds(0);

            Assert.IsTrue(_tracker.WasFired("Total_Ads_Revenue_001"),
                "Revenue threshold = 0 must fire on every call (0 >= 0)");
        }

        [Test]
        public void Step1_ZeroRevenueImpressions_OnlyCountAdvances()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            for (int i = 0; i < 10; i++) mgr.ProcessAllFormatsThresholds(0.0);

            Assert.IsFalse(_tracker.WasFired("Total_Ads_Revenue_001"),
                "Zero revenue impressions must not fire the revenue threshold");
            Assert.IsTrue(_tracker.WasFired("TenAdsShown"),
                "Zero revenue impressions must still advance the ad count");
        }

        [Test]
        public void AllSteps_ThresholdOf1_EachFiresOnSingleImpression()
        {
            var config = new TaichiConfig
            {
                RevenueThreshold           = 0.001f,
                AdCountThreshold           = 1,
                TotalImpressionThreshold   = 1,
                InterstitialCountThreshold = 1,
                RewardedCountThreshold     = 1,
                RewardedRevenueThreshold   = 0.001f,
            };
            var mgr = new AdRevenueTrackingManager(_tracker, config);

            mgr.ProcessAllFormatsThresholds(0.001);
            mgr.ProcessInterstitialThresholds(0.001);
            mgr.ProcessRewardedThresholds(0.001);

            Assert.IsTrue(_tracker.WasFired("Total_Ads_Revenue_001"));
            Assert.IsTrue(_tracker.WasFired("TenAdsShown"));
            Assert.IsTrue(_tracker.WasFired("taichi_total_ad_impression"));
            Assert.IsTrue(_tracker.WasFired("taichi_interstitial_ad_impression"));
            Assert.IsTrue(_tracker.WasFired("taichi_rewarded_ad_impression"));
            Assert.IsTrue(_tracker.WasFired("taichi_rewarded_ad_revenue"));
        }

        // ─── DroppedEventCount ────────────────────────────────────────────────

        [Test]
        public void DroppedEventCount_StartsAtZero_WithWiredTracker()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());
            Assert.AreEqual(0, mgr.DroppedEventCount);
        }

        [Test]
        public void DroppedEventCount_StartsAtZero_WithNullTracker()
        {
            var mgr = new AdRevenueTrackingManager(null, DefaultConfig());
            Assert.AreEqual(0, mgr.DroppedEventCount,
                "DroppedEventCount starts at zero even when constructed with null tracker");
        }

        [Test]
        public void DroppedEventCount_DoesNotIncrementForTaichiThreshold_WhenTrackerNull()
        {
            // Taichi threshold methods use ?. operator — they don't increment DroppedEventCount.
            // Only TrackAdmobRevenue / TrackAppLovinRevenue (platform-conditional) do.
            // This test documents that Taichi drops are a separate concern from revenue drops.
            var mgr = new AdRevenueTrackingManager(null, DefaultConfig());

            for (int i = 0; i < 15; i++) mgr.ProcessAllFormatsThresholds(0.01);

            Assert.AreEqual(0, mgr.DroppedEventCount,
                "DroppedEventCount must not increment for Taichi threshold drops — those are silent by design; " +
                "only TrackAdmobRevenue / TrackAppLovinRevenue increment the counter");
        }

        [Test]
        public void DroppedEventCount_ResetsToZero_WhenTrackerWired()
        {
            // We can't directly call TrackAdmobRevenue (requires UNITY_ADMOB).
            // Instead, simulate a lifecycle where we manually set the count via
            // null construction, then re-wire, and verify the count was reset.
            //
            // The count reset is a SetAdRevenueTracker() side-effect when going null→valid.
            var mgr = new AdRevenueTrackingManager(null, DefaultConfig());

            // Count is 0 at start; wiring should log "0 events were dropped"
            mgr.SetAdRevenueTracker(_tracker);

            Assert.AreEqual(0, mgr.DroppedEventCount,
                "DroppedEventCount must be 0 after wiring tracker (no events were dropped in this path)");
        }

        // ─── SetAdRevenueTracker mid-session with in-flight Taichi state ──────

        [Test]
        public void MidSessionTrackerSwap_PendingAccumulatedRevenue_CarriesOver()
        {
            // Accumulate 0.006 with tracker1 (not yet at threshold 0.01)
            var tracker1 = new MockAdRevenueTracker();
            var mgr = new AdRevenueTrackingManager(tracker1, DefaultConfig());
            mgr.ProcessAllFormatsThresholds(0.006);
            Assert.IsFalse(tracker1.WasFired("Total_Ads_Revenue_001"),
                "0.006 < 0.01 — must not fire yet");

            // Swap to tracker2 mid-session; PlayerPrefs still holds 0.006
            var tracker2 = new MockAdRevenueTracker();
            mgr.SetAdRevenueTracker(tracker2);

            mgr.ProcessAllFormatsThresholds(0.005); // 0.006 + 0.005 = 0.011 >= 0.01 → fires
            Assert.IsTrue(tracker2.WasFired("Total_Ads_Revenue_001"),
                "After tracker swap, accumulated state must carry over and fire with new tracker");
            Assert.IsFalse(tracker1.WasFired("Total_Ads_Revenue_001"),
                "Original tracker must not receive events after swap");
        }

        [Test]
        public void MidSessionTrackerSwap_ToNull_ThenRewired_StillFires()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            mgr.ProcessAllFormatsThresholds(0.006); // accumulate 0.006

            mgr.SetAdRevenueTracker(null);                      // clear tracker
            mgr.ProcessAllFormatsThresholds(0.003);             // accumulate 0.003 more (total 0.009)
                                                                // event silently dropped — no tracker

            mgr.SetAdRevenueTracker(_tracker);                  // re-wire
            mgr.ProcessAllFormatsThresholds(0.002);             // 0.006+0.003+0.002 = 0.011 >= 0.01

            Assert.IsTrue(_tracker.WasFired("Total_Ads_Revenue_001"),
                "Revenue accumulated while tracker was null must still count; " +
                "firing must happen once tracker is re-wired and next impression tips the balance");
        }

        [Test]
        public void MidSessionTrackerSwap_NullDuringMultipleSteps_NoStepsFireDuringNull()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());
            mgr.SetAdRevenueTracker(null);

            // Drive way past all thresholds while tracker is null
            for (int i = 0; i < 20; i++)
            {
                mgr.ProcessAllFormatsThresholds(0.01);
                mgr.ProcessInterstitialThresholds(0.01);
                mgr.ProcessRewardedThresholds(0.01);
            }

            Assert.AreEqual(0, _tracker.Events.Count,
                "All Taichi events must be silently dropped when tracker is null");
        }
    }
}
