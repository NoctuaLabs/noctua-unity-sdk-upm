using System.Collections.Generic;
using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime.IAA
{
    /// <summary>
    /// EditMode NUnit tests for <see cref="AdWatchMilestoneTracker"/>.
    ///
    /// Covers:
    ///   — Constructor rejects null emit delegate
    ///   — <c>RecordWatch</c> ignores ineligible formats (banner, rewarded_interstitial)
    ///   — Milestone events fire at 5 / 10 / 25 / 50 cumulative views
    ///   — Each milestone fires only once (bitmask dedup)
    ///   — Persisted counts/masks survive across instances (PlayerPrefs round-trip)
    ///   — <c>GetCount</c> / <c>GetFiredMask</c> / <c>ResetForAdType</c> test helpers
    ///   — <c>InstallAsDefault</c> sets the static <c>Default</c> property
    ///
    /// PlayerPrefs state is cleared before and after each test via
    /// <c>AdWatchMilestoneTracker.ResetForAdType</c>.
    /// </summary>
    [TestFixture]
    public class AdWatchMilestoneTrackerTest
    {
        private List<string> _emittedEvents;
        private AdWatchMilestoneTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            _emittedEvents = new List<string>();
            _tracker = new AdWatchMilestoneTracker((name, _) => _emittedEvents.Add(name));

            // Clear any leftover PlayerPrefs state
            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.Rewarded);
            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.Interstitial);
            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.Banner);
            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.AppOpen);
        }

        [TearDown]
        public void TearDown()
        {
            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.Rewarded);
            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.Interstitial);
        }

        // ═══════════════════════════════════════════════════════════════════
        // Constructor
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void Constructor_NullEmit_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new AdWatchMilestoneTracker(null));
        }

        // ═══════════════════════════════════════════════════════════════════
        // Eligibility filtering
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void RecordWatch_BannerFormat_Ignored()
        {
            for (int i = 0; i < 10; i++) _tracker.RecordWatch(AdFormatKey.Banner);

            Assert.AreEqual(0, _emittedEvents.Count, "Banner views must not trigger milestones");
            Assert.AreEqual(0, AdWatchMilestoneTracker.GetCount(AdFormatKey.Banner));
        }

        [Test]
        public void RecordWatch_AppOpenFormat_Ignored()
        {
            for (int i = 0; i < 10; i++) _tracker.RecordWatch(AdFormatKey.AppOpen);

            Assert.AreEqual(0, _emittedEvents.Count, "App open views must not trigger milestones");
        }

        [Test]
        public void RecordWatch_NullAdType_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _tracker.RecordWatch(null));
        }

        [Test]
        public void RecordWatch_EmptyAdType_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _tracker.RecordWatch(""));
        }

        // ═══════════════════════════════════════════════════════════════════
        // Milestone firing — rewarded ads
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void RecordWatch_FiveRewarded_Fires5xEvent()
        {
            for (int i = 0; i < 5; i++) _tracker.RecordWatch(AdFormatKey.Rewarded);

            Assert.Contains(IAAEventNames.WatchAds5x, _emittedEvents);
        }

        [Test]
        public void RecordWatch_TenRewarded_Fires5xAnd10xEvents()
        {
            for (int i = 0; i < 10; i++) _tracker.RecordWatch(AdFormatKey.Rewarded);

            Assert.Contains(IAAEventNames.WatchAds5x,  _emittedEvents);
            Assert.Contains(IAAEventNames.WatchAds10x, _emittedEvents);
        }

        [Test]
        public void RecordWatch_TwentyFiveRewarded_FiresAllThreeBelow()
        {
            for (int i = 0; i < 25; i++) _tracker.RecordWatch(AdFormatKey.Rewarded);

            Assert.Contains(IAAEventNames.WatchAds5x,  _emittedEvents);
            Assert.Contains(IAAEventNames.WatchAds10x, _emittedEvents);
            Assert.Contains(IAAEventNames.WatchAds25x, _emittedEvents);
            Assert.IsFalse(_emittedEvents.Contains(IAAEventNames.WatchAds50x),
                "50x milestone must not fire at 25 views");
        }

        [Test]
        public void RecordWatch_FiftyRewarded_FiresAllFourMilestones()
        {
            for (int i = 0; i < 50; i++) _tracker.RecordWatch(AdFormatKey.Rewarded);

            Assert.Contains(IAAEventNames.WatchAds5x,  _emittedEvents);
            Assert.Contains(IAAEventNames.WatchAds10x, _emittedEvents);
            Assert.Contains(IAAEventNames.WatchAds25x, _emittedEvents);
            Assert.Contains(IAAEventNames.WatchAds50x, _emittedEvents);
        }

        // ═══════════════════════════════════════════════════════════════════
        // Once-only dedup via bitmask
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void RecordWatch_5xMilestoneFiredOnce_EvenAfterMoreViews()
        {
            // First 5 views → fires watch_ads_5x
            for (int i = 0; i < 5; i++) _tracker.RecordWatch(AdFormatKey.Rewarded);
            int countAfterFirst = _emittedEvents.FindAll(e => e == IAAEventNames.WatchAds5x).Count;
            Assert.AreEqual(1, countAfterFirst);

            // 5 more views → must NOT fire watch_ads_5x again
            for (int i = 0; i < 5; i++) _tracker.RecordWatch(AdFormatKey.Rewarded);
            int countAfterSecond = _emittedEvents.FindAll(e => e == IAAEventNames.WatchAds5x).Count;
            Assert.AreEqual(1, countAfterSecond, "5x milestone must fire at most once per install");
        }

        // ═══════════════════════════════════════════════════════════════════
        // Interstitial format — independent counter
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void RecordWatch_InterstitialFiveViews_Fires5xEvent()
        {
            for (int i = 0; i < 5; i++) _tracker.RecordWatch(AdFormatKey.Interstitial);

            Assert.Contains(IAAEventNames.WatchAds5x, _emittedEvents);
            Assert.AreEqual(5, AdWatchMilestoneTracker.GetCount(AdFormatKey.Interstitial));
        }

        [Test]
        public void RecordWatch_RewardedAndInterstitial_IndependentCounters()
        {
            for (int i = 0; i < 3; i++) _tracker.RecordWatch(AdFormatKey.Rewarded);
            for (int i = 0; i < 3; i++) _tracker.RecordWatch(AdFormatKey.Interstitial);

            // Neither has reached 5 yet
            Assert.AreEqual(0, _emittedEvents.Count,
                "3+3 views (different types) must not yet trigger 5x milestone");
        }

        // ═══════════════════════════════════════════════════════════════════
        // GetCount / GetFiredMask test helpers
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void GetCount_AfterThreeViews_ReturnsThree()
        {
            _tracker.RecordWatch(AdFormatKey.Rewarded);
            _tracker.RecordWatch(AdFormatKey.Rewarded);
            _tracker.RecordWatch(AdFormatKey.Rewarded);

            Assert.AreEqual(3, AdWatchMilestoneTracker.GetCount(AdFormatKey.Rewarded));
        }

        [Test]
        public void GetFiredMask_AfterFiveViews_Bit0Set()
        {
            for (int i = 0; i < 5; i++) _tracker.RecordWatch(AdFormatKey.Rewarded);

            int mask = AdWatchMilestoneTracker.GetFiredMask(AdFormatKey.Rewarded);
            Assert.AreEqual(1, mask & 1, "Bit 0 (5x) must be set after 5 views");
        }

        // ═══════════════════════════════════════════════════════════════════
        // PlayerPrefs persistence round-trip
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void RecordWatch_PersistedCount_RestoredByNewInstance()
        {
            // First tracker records 4 views
            for (int i = 0; i < 4; i++) _tracker.RecordWatch(AdFormatKey.Rewarded);

            // New tracker instance reads from PlayerPrefs — one more view should fire 5x
            var events2 = new List<string>();
            var tracker2 = new AdWatchMilestoneTracker((name, _) => events2.Add(name));
            tracker2.RecordWatch(AdFormatKey.Rewarded);

            Assert.Contains(IAAEventNames.WatchAds5x, events2,
                "Persisted count must be restored and milestone must fire on the 5th view");
        }

        [Test]
        public void RecordWatch_PersistedFiredMask_PreventsRefiring()
        {
            // First tracker fires the 5x milestone
            for (int i = 0; i < 5; i++) _tracker.RecordWatch(AdFormatKey.Rewarded);

            // New tracker reads the bitmask — milestone must not re-fire
            var events2 = new List<string>();
            var tracker2 = new AdWatchMilestoneTracker((name, _) => events2.Add(name));
            tracker2.RecordWatch(AdFormatKey.Rewarded);  // view 6

            Assert.IsFalse(events2.Contains(IAAEventNames.WatchAds5x),
                "5x milestone must not re-fire when bitmask is already set in PlayerPrefs");
        }

        // ═══════════════════════════════════════════════════════════════════
        // ResetForAdType
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void ResetForAdType_ClearsCountAndMask()
        {
            for (int i = 0; i < 5; i++) _tracker.RecordWatch(AdFormatKey.Rewarded);

            AdWatchMilestoneTracker.ResetForAdType(AdFormatKey.Rewarded);

            Assert.AreEqual(0, AdWatchMilestoneTracker.GetCount(AdFormatKey.Rewarded));
            Assert.AreEqual(0, AdWatchMilestoneTracker.GetFiredMask(AdFormatKey.Rewarded));
        }

        // ═══════════════════════════════════════════════════════════════════
        // InstallAsDefault
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void InstallAsDefault_SetsDotDefaultProperty()
        {
            _tracker.InstallAsDefault();
            Assert.AreSame(_tracker, AdWatchMilestoneTracker.Default);
        }
    }
}
