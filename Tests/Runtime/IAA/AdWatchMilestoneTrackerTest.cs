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
}
