using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace com.noctuagames.sdk.Tests.IAA
{
    /// <summary>
    /// Unit tests for <see cref="AdWatchMilestoneTracker"/>.
    ///
    /// Verifies the milestone semantics laid out in the canonical IAA spec:
    /// <list type="bullet">
    ///   <item>5x / 10x / 25x / 50x fire exactly once each per install</item>
    ///   <item>Only <see cref="AdFormatKey.Rewarded"/> and <see cref="AdFormatKey.Interstitial"/> contribute</item>
    ///   <item>Banner / RewardedInterstitial / AppOpen are silently ignored</item>
    ///   <item>Counts persist via <see cref="PlayerPrefs"/></item>
    /// </list>
    /// </summary>
    [TestFixture]
    public class AdWatchMilestoneTrackerTest
    {
        private List<(string Event, Dictionary<string, IConvertible> Payload)> _emitted;
        private AdWatchMilestoneTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            // Reset persisted state for every eligible + ineligible ad type so tests are isolated.
            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.Rewarded);
            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.Interstitial);
            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.Banner);
            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.RewardedInterstitial);
            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.AppOpen);

            _emitted = new List<(string, Dictionary<string, IConvertible>)>();
            _tracker = new AdWatchMilestoneTracker((name, payload) => _emitted.Add((name, payload)));
        }

        [TearDown]
        public void TearDown()
        {
            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.Rewarded);
            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.Interstitial);
            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.Banner);
            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.RewardedInterstitial);
            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.AppOpen);
        }

        // ─── Threshold firing ────────────────────────────────────────────────

        [Test]
        public void RecordWatch_BelowFirstThreshold_EmitsNothing()
        {
            for (int i = 0; i < 4; i++) _tracker.RecordWatch(AdFormatKey.Rewarded);

            Assert.AreEqual(0, _emitted.Count);
            Assert.AreEqual(4, AdWatchMilestoneTracker.GetCount(AdFormatKey.Rewarded));
        }

        [Test]
        public void RecordWatch_HittingFifth_EmitsWatchAds5xOnce()
        {
            for (int i = 0; i < 5; i++) _tracker.RecordWatch(AdFormatKey.Rewarded);

            Assert.AreEqual(1, _emitted.Count);
            Assert.AreEqual(IAAEventNames.WatchAds5x, _emitted[0].Event);
            Assert.AreEqual(AdFormatKey.Rewarded, _emitted[0].Payload[IAAPayloadKey.AdType]);
            Assert.AreEqual(5, _emitted[0].Payload[IAAPayloadKey.Count]);
        }

        [Test]
        public void RecordWatch_AllFourThresholds_EachFiresExactlyOnce()
        {
            for (int i = 0; i < 50; i++) _tracker.RecordWatch(AdFormatKey.Interstitial);

            Assert.AreEqual(4, _emitted.Count);
            Assert.AreEqual(IAAEventNames.WatchAds5x,  _emitted[0].Event);
            Assert.AreEqual(IAAEventNames.WatchAds10x, _emitted[1].Event);
            Assert.AreEqual(IAAEventNames.WatchAds25x, _emitted[2].Event);
            Assert.AreEqual(IAAEventNames.WatchAds50x, _emitted[3].Event);

            Assert.AreEqual( 5, _emitted[0].Payload[IAAPayloadKey.Count]);
            Assert.AreEqual(10, _emitted[1].Payload[IAAPayloadKey.Count]);
            Assert.AreEqual(25, _emitted[2].Payload[IAAPayloadKey.Count]);
            Assert.AreEqual(50, _emitted[3].Payload[IAAPayloadKey.Count]);
        }

        [Test]
        public void RecordWatch_PastFifty_DoesNotRefireAnyMilestone()
        {
            for (int i = 0; i < 60; i++) _tracker.RecordWatch(AdFormatKey.Rewarded);

            Assert.AreEqual(4, _emitted.Count, "Only the four canonical milestones should fire even past 50.");
            Assert.AreEqual(60, AdWatchMilestoneTracker.GetCount(AdFormatKey.Rewarded));
        }

        // ─── Once-per-install semantics ──────────────────────────────────────

        [Test]
        public void RecordWatch_SecondInstanceWithExistingState_DoesNotRefireAlreadyFiredMilestone()
        {
            for (int i = 0; i < 5; i++) _tracker.RecordWatch(AdFormatKey.Rewarded);
            Assert.AreEqual(1, _emitted.Count);

            // Simulate process restart by constructing a fresh tracker (PlayerPrefs persists).
            var emittedAfter = new List<(string, Dictionary<string, IConvertible>)>();
            var freshTracker = new AdWatchMilestoneTracker((n, p) => emittedAfter.Add((n, p)));

            for (int i = 0; i < 4; i++) freshTracker.RecordWatch(AdFormatKey.Rewarded);  // count: 6,7,8,9

            Assert.AreEqual(0, emittedAfter.Count, "5x must not refire after install.");
            Assert.AreEqual(9, AdWatchMilestoneTracker.GetCount(AdFormatKey.Rewarded));
        }

        // ─── Per-ad-type isolation ───────────────────────────────────────────

        [Test]
        public void RecordWatch_RewardedAndInterstitial_AreCountedSeparately()
        {
            for (int i = 0; i < 5; i++) _tracker.RecordWatch(AdFormatKey.Rewarded);
            for (int i = 0; i < 5; i++) _tracker.RecordWatch(AdFormatKey.Interstitial);

            Assert.AreEqual(2, _emitted.Count);
            Assert.AreEqual(IAAEventNames.WatchAds5x, _emitted[0].Event);
            Assert.AreEqual(AdFormatKey.Rewarded, _emitted[0].Payload[IAAPayloadKey.AdType]);
            Assert.AreEqual(IAAEventNames.WatchAds5x, _emitted[1].Event);
            Assert.AreEqual(AdFormatKey.Interstitial, _emitted[1].Payload[IAAPayloadKey.AdType]);
        }

        // ─── Ineligible ad types ─────────────────────────────────────────────

        [Test]
        public void RecordWatch_Banner_IsIgnored()
        {
            for (int i = 0; i < 100; i++) _tracker.RecordWatch(AdFormatKey.Banner);

            Assert.AreEqual(0, _emitted.Count);
            Assert.AreEqual(0, AdWatchMilestoneTracker.GetCount(AdFormatKey.Banner));
        }

        [Test]
        public void RecordWatch_RewardedInterstitial_IsIgnored()
        {
            for (int i = 0; i < 100; i++) _tracker.RecordWatch(AdFormatKey.RewardedInterstitial);

            Assert.AreEqual(0, _emitted.Count);
            Assert.AreEqual(0, AdWatchMilestoneTracker.GetCount(AdFormatKey.RewardedInterstitial));
        }

        [Test]
        public void RecordWatch_AppOpen_IsIgnored()
        {
            for (int i = 0; i < 100; i++) _tracker.RecordWatch(AdFormatKey.AppOpen);

            Assert.AreEqual(0, _emitted.Count);
            Assert.AreEqual(0, AdWatchMilestoneTracker.GetCount(AdFormatKey.AppOpen));
        }

        [Test]
        public void RecordWatch_NullOrEmptyAdType_IsIgnored()
        {
            _tracker.RecordWatch(null);
            _tracker.RecordWatch(string.Empty);

            Assert.AreEqual(0, _emitted.Count);
        }

        // ─── InstallAsDefault ────────────────────────────────────────────────

        [Test]
        public void InstallAsDefault_SetsStaticDefault()
        {
            _tracker.InstallAsDefault();
            Assert.AreSame(_tracker, AdWatchMilestoneTracker.Default);
        }

        [Test]
        public void InstallAsDefault_SecondCall_ReplacesDefault()
        {
            _tracker.InstallAsDefault();
            var second = new AdWatchMilestoneTracker((_, __) => { });
            second.InstallAsDefault();

            Assert.AreSame(second, AdWatchMilestoneTracker.Default);
        }
    }

    /// <summary>
    /// Extended edge-case tests for <see cref="AdWatchMilestoneTracker"/>.
    /// Covers: individual milestone thresholds, GetFiredMask semantics,
    /// null-emit-delegate guard, ResetForAdType(null), counter accumulation,
    /// per-format payload correctness, and threshold-skipping scenarios.
    /// </summary>
    [TestFixture]
    public class AdWatchMilestoneTrackerEdgeCaseTest
    {
        private List<(string Event, Dictionary<string, IConvertible> Payload)> _emitted;
        private AdWatchMilestoneTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.Rewarded);
            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.Interstitial);
            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.Banner);
            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.RewardedInterstitial);
            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.AppOpen);

            _emitted = new List<(string, Dictionary<string, IConvertible>)>();
            _tracker = new AdWatchMilestoneTracker((name, payload) => _emitted.Add((name, payload)));
        }

        [TearDown]
        public void TearDown()
        {
            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.Rewarded);
            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.Interstitial);
            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.Banner);
            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.RewardedInterstitial);
            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.AppOpen);
        }

        // ─── Constructor guard ────────────────────────────────────────────────

        [Test]
        public void Constructor_NullEmitDelegate_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new AdWatchMilestoneTracker(null));
        }

        // ─── Individual milestone thresholds ──────────────────────────────────

        [Test]
        public void RecordWatch_ExactlyFive_Fires5xOnly()
        {
            for (int i = 0; i < 5; i++) _tracker.RecordWatch(AdFormatKey.Interstitial);

            Assert.AreEqual(1, _emitted.Count);
            Assert.AreEqual(IAAEventNames.WatchAds5x, _emitted[0].Event);
        }

        [Test]
        public void RecordWatch_ExactlyTen_Fires10xOnly()
        {
            // Pre-fill 5 already-fired so we start just after the 5x milestone
            for (int i = 0; i < 5; i++) _tracker.RecordWatch(AdFormatKey.Rewarded);
            _emitted.Clear();

            for (int i = 0; i < 5; i++) _tracker.RecordWatch(AdFormatKey.Rewarded); // count → 10

            Assert.AreEqual(1, _emitted.Count);
            Assert.AreEqual(IAAEventNames.WatchAds10x, _emitted[0].Event);
        }

        [Test]
        public void RecordWatch_ExactlyTwentyFive_Fires25xOnly()
        {
            for (int i = 0; i < 10; i++) _tracker.RecordWatch(AdFormatKey.Rewarded);
            _emitted.Clear();

            for (int i = 0; i < 15; i++) _tracker.RecordWatch(AdFormatKey.Rewarded); // count → 25

            Assert.AreEqual(1, _emitted.Count);
            Assert.AreEqual(IAAEventNames.WatchAds25x, _emitted[0].Event);
        }

        [Test]
        public void RecordWatch_ExactlyFifty_Fires50xOnly()
        {
            for (int i = 0; i < 25; i++) _tracker.RecordWatch(AdFormatKey.Rewarded);
            _emitted.Clear();

            for (int i = 0; i < 25; i++) _tracker.RecordWatch(AdFormatKey.Rewarded); // count → 50

            Assert.AreEqual(1, _emitted.Count);
            Assert.AreEqual(IAAEventNames.WatchAds50x, _emitted[0].Event);
        }

        // ─── GetFiredMask semantics ────────────────────────────────────────────

        [Test]
        public void GetFiredMask_BeforeAnyWatch_IsZero()
        {
            Assert.AreEqual(0, AdWatchMilestoneTracker.GetFiredMask(AdFormatKey.Rewarded));
        }

        [Test]
        public void GetFiredMask_After5xFires_HasBit0Set()
        {
            for (int i = 0; i < 5; i++) _tracker.RecordWatch(AdFormatKey.Rewarded);

            int mask = AdWatchMilestoneTracker.GetFiredMask(AdFormatKey.Rewarded);
            Assert.IsTrue((mask & (1 << 0)) != 0, "Bit0 (5x) should be set after 5 watches");
        }

        [Test]
        public void GetFiredMask_AllFourMilestones_AllFourBitsSet()
        {
            for (int i = 0; i < 50; i++) _tracker.RecordWatch(AdFormatKey.Interstitial);

            int mask = AdWatchMilestoneTracker.GetFiredMask(AdFormatKey.Interstitial);
            // Bits 0,1,2,3 all set → mask == 0b1111 == 15
            Assert.AreEqual(15, mask, "After 50 watches, all four milestone bits should be set");
        }

        [Test]
        public void GetFiredMask_UnknownAdType_ReturnsZero()
        {
            // Non-eligible type was never incremented → mask should be 0
            int mask = AdWatchMilestoneTracker.GetFiredMask(AdFormatKey.Banner);
            Assert.AreEqual(0, mask);
        }

        // ─── GetCount helpers ─────────────────────────────────────────────────

        [Test]
        public void GetCount_NullAdType_ReturnsZeroWithoutThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                int count = AdWatchMilestoneTracker.GetCount(null);
                Assert.AreEqual(0, count);
            });
        }

        [Test]
        public void GetCount_AccumulatesCorrectlyAcrossManyWatches()
        {
            for (int i = 0; i < 37; i++) _tracker.RecordWatch(AdFormatKey.Rewarded);

            Assert.AreEqual(37, AdWatchMilestoneTracker.GetCount(AdFormatKey.Rewarded));
        }

        // ─── ResetForAdType edge cases ────────────────────────────────────────

        [Test]
        public void ResetForAdType_Null_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => AdWatchMilestoneTracker.ResetForAdType(null));
        }

        [Test]
        public void ResetForAdType_EmptyString_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => AdWatchMilestoneTracker.ResetForAdType(string.Empty));
        }

        [Test]
        public void ResetForAdType_ClearsCountAndMask()
        {
            for (int i = 0; i < 50; i++) _tracker.RecordWatch(AdFormatKey.Rewarded);

            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.Rewarded);

            Assert.AreEqual(0, AdWatchMilestoneTracker.GetCount(AdFormatKey.Rewarded));
            Assert.AreEqual(0, AdWatchMilestoneTracker.GetFiredMask(AdFormatKey.Rewarded));
        }

        [Test]
        public void ResetForAdType_AfterReset_MilestonesFireAgain()
        {
            for (int i = 0; i < 5; i++) _tracker.RecordWatch(AdFormatKey.Rewarded);
            Assert.AreEqual(1, _emitted.Count);
            _emitted.Clear();

            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.Rewarded);

            for (int i = 0; i < 5; i++) _tracker.RecordWatch(AdFormatKey.Rewarded);

            Assert.AreEqual(1, _emitted.Count, "After reset, 5x milestone should fire again");
            Assert.AreEqual(IAAEventNames.WatchAds5x, _emitted[0].Event);
        }

        // ─── Payload correctness per format ───────────────────────────────────

        [Test]
        public void RecordWatch_InterstitialMilestone_PayloadContainsInterstitialAdType()
        {
            for (int i = 0; i < 5; i++) _tracker.RecordWatch(AdFormatKey.Interstitial);

            Assert.AreEqual(1, _emitted.Count);
            Assert.AreEqual(AdFormatKey.Interstitial, _emitted[0].Payload[IAAPayloadKey.AdType]);
        }

        [Test]
        public void RecordWatch_RewardedMilestone_PayloadContainsRewardedAdType()
        {
            for (int i = 0; i < 5; i++) _tracker.RecordWatch(AdFormatKey.Rewarded);

            Assert.AreEqual(1, _emitted.Count);
            Assert.AreEqual(AdFormatKey.Rewarded, _emitted[0].Payload[IAAPayloadKey.AdType]);
        }

        [Test]
        public void RecordWatch_Milestone_PayloadCountMatchesThreshold()
        {
            for (int i = 0; i < 10; i++) _tracker.RecordWatch(AdFormatKey.Rewarded);

            // Two events: 5x and 10x
            Assert.AreEqual(2, _emitted.Count);
            Assert.AreEqual(5,  _emitted[0].Payload[IAAPayloadKey.Count]);
            Assert.AreEqual(10, _emitted[1].Payload[IAAPayloadKey.Count]);
        }

        // ─── Threshold skipping — single increment crosses multiple milestones ─

        [Test]
        public void RecordWatch_SingleIncrementCrossesMultipleThresholds_AllFire()
        {
            // Pre-set count to 4 via direct PlayerPrefs manipulation (bypassing tracker)
            // so the next RecordWatch hits count=5 (crossing 5x threshold)
            // This tests the increment from 4 → 5 crossing 5x only.
            // To test skip: start at 9 → next watch hits 10 (crosses both if 5x not fired yet)
            // Set count to 4 (fired none), then watch once → count=5 → 5x fires
            // Set count to 9 (fired 5x), then watch once → count=10 → 10x fires only (5x already fired)

            // Actually test genuine skip: set count=24, firedMask=0b0001 (5x already fired),
            // then watch once → count=25 → only 25x would normally fire, but 10x is also unfired...
            // The spec says "each fires exactly once" — test the edge where we're at 24 with only
            // 5x fired (10x and 25x still unfired), then jump to 25 in one step.
            PlayerPrefs.SetInt("noctua.ads.watch.count." + AdFormatKey.Rewarded, 24);
            PlayerPrefs.SetInt("noctua.ads.watch.fired." + AdFormatKey.Rewarded, 1 << 0); // 5x already fired
            PlayerPrefs.Save();

            _tracker.RecordWatch(AdFormatKey.Rewarded); // count → 25; 10x and 25x both unfired → both fire

            Assert.AreEqual(2, _emitted.Count, "Both 10x and 25x should fire in one increment when count crosses both thresholds");
            Assert.AreEqual(IAAEventNames.WatchAds10x, _emitted[0].Event);
            Assert.AreEqual(IAAEventNames.WatchAds25x, _emitted[1].Event);
        }

        [Test]
        public void RecordWatch_FromZeroSkippingToFifty_AllFourMilestonesFire()
        {
            // Set count to 49 with no milestones fired yet
            PlayerPrefs.SetInt("noctua.ads.watch.count." + AdFormatKey.Interstitial, 49);
            PlayerPrefs.SetInt("noctua.ads.watch.fired." + AdFormatKey.Interstitial, 0);
            PlayerPrefs.Save();

            _tracker.RecordWatch(AdFormatKey.Interstitial); // count → 50; all four thresholds passed

            Assert.AreEqual(4, _emitted.Count, "All four milestones should fire when count crosses 50 with none previously fired");
            Assert.AreEqual(IAAEventNames.WatchAds5x,  _emitted[0].Event);
            Assert.AreEqual(IAAEventNames.WatchAds10x, _emitted[1].Event);
            Assert.AreEqual(IAAEventNames.WatchAds25x, _emitted[2].Event);
            Assert.AreEqual(IAAEventNames.WatchAds50x, _emitted[3].Event);
        }

        // ─── Counter isolation between ad types ───────────────────────────────

        [Test]
        public void GetCount_RewardedAndInterstitial_IndependentCounters()
        {
            for (int i = 0; i < 7; i++)  _tracker.RecordWatch(AdFormatKey.Rewarded);
            for (int i = 0; i < 12; i++) _tracker.RecordWatch(AdFormatKey.Interstitial);

            Assert.AreEqual(7,  AdWatchMilestoneTracker.GetCount(AdFormatKey.Rewarded));
            Assert.AreEqual(12, AdWatchMilestoneTracker.GetCount(AdFormatKey.Interstitial));
        }

        [Test]
        public void GetFiredMask_RewardedAndInterstitial_IndependentMasks()
        {
            for (int i = 0; i < 5; i++) _tracker.RecordWatch(AdFormatKey.Rewarded);      // 5x fires for Rewarded
            // Interstitial untouched → mask should still be 0

            int rewardedMask      = AdWatchMilestoneTracker.GetFiredMask(AdFormatKey.Rewarded);
            int interstitialMask  = AdWatchMilestoneTracker.GetFiredMask(AdFormatKey.Interstitial);

            Assert.AreEqual(1, rewardedMask,     "Rewarded should have bit0 set");
            Assert.AreEqual(0, interstitialMask,  "Interstitial mask should still be 0");
        }
    }
}
