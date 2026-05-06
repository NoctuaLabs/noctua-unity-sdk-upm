using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime.IAA
{
    /// <summary>
    /// EditMode NUnit tests for <see cref="AdNetworkPerformanceTracker"/>.
    ///
    /// Covers:
    ///   — <c>RecordFillAttempt</c> / <c>GetFillRate</c>  — rolling average, 100-sample cap
    ///   — <c>RecordRevenue</c>     / <c>GetAverageRevenue</c> — rolling average, 50-sample cap
    ///   — <c>GetAverageCpm</c>     — alias for GetAverageRevenue
    ///   — <c>GetSampleCount</c>    — cold-start guard
    ///   — <c>GetPreferredNetwork</c> — highest score (fillRate × avgRevenue) wins
    ///   — PlayerPrefs fallback when no in-memory data
    ///
    /// PlayerPrefs keys prefixed "NoctuaAdPerf_" are cleared in SetUp/TearDown.
    /// </summary>
    [TestFixture]
    public class AdNetworkPerformanceTrackerTest
    {
        private const string Prefix  = "NoctuaAdPerf_";
        private const string Network = "admob";
        private const string Format  = AdFormatKey.Rewarded;

        [SetUp]
        public void SetUp() => PlayerPrefs.DeleteAll();

        [TearDown]
        public void TearDown() => PlayerPrefs.DeleteAll();

        // ═══════════════════════════════════════════════════════════════════
        // GetFillRate
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void GetFillRate_NoData_ReturnsPersisted_DefaultHalf()
        {
            var tracker = new AdNetworkPerformanceTracker();

            // No PlayerPrefs set → default 0.5f
            double rate = tracker.GetFillRate(Network, Format);

            Assert.AreEqual(0.5, rate, delta: 0.001,
                "Without data or persisted value, GetFillRate must return 0.5 (PlayerPrefs default)");
        }

        [Test]
        public void GetFillRate_AllFilled_ReturnsOne()
        {
            var tracker = new AdNetworkPerformanceTracker();
            for (int i = 0; i < 10; i++)
                tracker.RecordFillAttempt(Network, Format, filled: true);

            Assert.AreEqual(1.0, tracker.GetFillRate(Network, Format), delta: 0.001);
        }

        [Test]
        public void GetFillRate_NoneFilled_ReturnsZero()
        {
            var tracker = new AdNetworkPerformanceTracker();
            for (int i = 0; i < 10; i++)
                tracker.RecordFillAttempt(Network, Format, filled: false);

            Assert.AreEqual(0.0, tracker.GetFillRate(Network, Format), delta: 0.001);
        }

        [Test]
        public void GetFillRate_HalfFilled_ReturnsHalf()
        {
            var tracker = new AdNetworkPerformanceTracker();
            for (int i = 0; i < 5; i++) tracker.RecordFillAttempt(Network, Format, true);
            for (int i = 0; i < 5; i++) tracker.RecordFillAttempt(Network, Format, false);

            Assert.AreEqual(0.5, tracker.GetFillRate(Network, Format), delta: 0.01);
        }

        [Test]
        public void GetFillRate_ExceedsMaxSamples_OldestDropped()
        {
            var tracker = new AdNetworkPerformanceTracker();

            // Fill 100 failures
            for (int i = 0; i < 100; i++)
                tracker.RecordFillAttempt(Network, Format, filled: false);

            // Add 100 successes — rolling window keeps only last 100 (all successes)
            for (int i = 0; i < 100; i++)
                tracker.RecordFillAttempt(Network, Format, filled: true);

            Assert.AreEqual(1.0, tracker.GetFillRate(Network, Format), delta: 0.001,
                "After rolling 100 successes, rate must converge to 1.0");
        }

        // ═══════════════════════════════════════════════════════════════════
        // GetAverageRevenue / GetAverageCpm
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void GetAverageRevenue_NoData_ReturnsPersisted_DefaultZero()
        {
            var tracker = new AdNetworkPerformanceTracker();

            double rev = tracker.GetAverageRevenue(Network, Format);

            Assert.AreEqual(0.0, rev, delta: 0.001,
                "Without data or persisted value, GetAverageRevenue must return 0.0");
        }

        [Test]
        public void GetAverageRevenue_TwoSamples_ReturnsAverage()
        {
            var tracker = new AdNetworkPerformanceTracker();
            tracker.RecordRevenue(Network, Format, 1.0);
            tracker.RecordRevenue(Network, Format, 3.0);

            Assert.AreEqual(2.0, tracker.GetAverageRevenue(Network, Format), delta: 0.001);
        }

        [Test]
        public void GetAverageCpm_DelegatesToGetAverageRevenue()
        {
            var tracker = new AdNetworkPerformanceTracker();
            tracker.RecordRevenue(Network, Format, 4.0);

            Assert.AreEqual(
                tracker.GetAverageRevenue(Network, Format),
                tracker.GetAverageCpm(Network, Format),
                delta: 0.001,
                "GetAverageCpm must return the same value as GetAverageRevenue");
        }

        [Test]
        public void GetAverageRevenue_ExceedsMaxRevenueSamples_OldDropped()
        {
            var tracker = new AdNetworkPerformanceTracker();

            // Record 50 zeros — fills the 50-sample cap
            for (int i = 0; i < 50; i++)
                tracker.RecordRevenue(Network, Format, 0.0);

            // Add one more: oldest zero drops, replaced by 5.0
            tracker.RecordRevenue(Network, Format, 5.0);

            // Window = [0×49 + 5×1] → avg = 5/50 = 0.1
            double avg = tracker.GetAverageRevenue(Network, Format);
            Assert.AreEqual(0.1, avg, delta: 0.01,
                "After overflow, oldest entry must be dequeued and average must update");
        }

        // ═══════════════════════════════════════════════════════════════════
        // GetSampleCount
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void GetSampleCount_NoRecords_ReturnsZero()
        {
            var tracker = new AdNetworkPerformanceTracker();
            Assert.AreEqual(0, tracker.GetSampleCount(Network, Format));
        }

        [Test]
        public void GetSampleCount_AfterThreeRecords_ReturnsThree()
        {
            var tracker = new AdNetworkPerformanceTracker();
            tracker.RecordRevenue(Network, Format, 1.0);
            tracker.RecordRevenue(Network, Format, 2.0);
            tracker.RecordRevenue(Network, Format, 3.0);

            Assert.AreEqual(3, tracker.GetSampleCount(Network, Format));
        }

        [Test]
        public void GetSampleCount_CappedAt50()
        {
            var tracker = new AdNetworkPerformanceTracker();
            for (int i = 0; i < 75; i++)
                tracker.RecordRevenue(Network, Format, 1.0);

            Assert.AreEqual(50, tracker.GetSampleCount(Network, Format),
                "Revenue sample count must be capped at 50");
        }

        // ═══════════════════════════════════════════════════════════════════
        // GetPreferredNetwork
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void GetPreferredNetwork_NoData_ReturnsNull()
        {
            var tracker = new AdNetworkPerformanceTracker();

            Assert.IsNull(tracker.GetPreferredNetwork(Format),
                "With no fill data, GetPreferredNetwork must return null");
        }

        [Test]
        public void GetPreferredNetwork_OneNetwork_ReturnsThat()
        {
            var tracker = new AdNetworkPerformanceTracker();
            tracker.RecordFillAttempt("applovin", Format, true);
            tracker.RecordRevenue("applovin", Format, 2.0);

            Assert.AreEqual("applovin", tracker.GetPreferredNetwork(Format));
        }

        [Test]
        public void GetPreferredNetwork_TwoNetworks_HigherScoreWins()
        {
            var tracker = new AdNetworkPerformanceTracker();

            // admob: fillRate=1.0, avgRevenue=1.0 → score=1.0
            tracker.RecordFillAttempt("admob", Format, true);
            tracker.RecordRevenue("admob", Format, 1.0);

            // applovin: fillRate=1.0, avgRevenue=2.0 → score=2.0
            tracker.RecordFillAttempt("applovin", Format, true);
            tracker.RecordRevenue("applovin", Format, 2.0);

            Assert.AreEqual("applovin", tracker.GetPreferredNetwork(Format),
                "Network with higher fillRate × avgRevenue must be preferred");
        }

        [Test]
        public void GetPreferredNetwork_HighFillLowRevenue_VsLowFillHighRevenue()
        {
            var tracker = new AdNetworkPerformanceTracker();

            // admob: fillRate=0.1, avgRevenue=10.0 → score=1.0
            for (int i = 0; i < 9; i++)  tracker.RecordFillAttempt("admob", Format, false);
            tracker.RecordFillAttempt("admob", Format, true);
            tracker.RecordRevenue("admob", Format, 10.0);

            // applovin: fillRate=1.0, avgRevenue=0.5 → score=0.5
            tracker.RecordFillAttempt("applovin", Format, true);
            tracker.RecordRevenue("applovin", Format, 0.5);

            Assert.AreEqual("admob", tracker.GetPreferredNetwork(Format),
                "admob score 1.0 must beat applovin score 0.5");
        }

        [Test]
        public void GetPreferredNetwork_DifferentFormat_NotInfluenced()
        {
            var tracker = new AdNetworkPerformanceTracker();

            // Record interstitial data for admob
            tracker.RecordFillAttempt("admob", AdFormatKey.Interstitial, true);
            tracker.RecordRevenue("admob", AdFormatKey.Interstitial, 5.0);

            // Querying rewarded format — no fill data → null
            Assert.IsNull(tracker.GetPreferredNetwork(AdFormatKey.Rewarded),
                "Fill history for interstitial must not influence rewarded queries");
        }

        // ═══════════════════════════════════════════════════════════════════
        // PlayerPrefs persistence
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void RecordFillAttempt_PersistsFillRateToPlayerPrefs()
        {
            var tracker = new AdNetworkPerformanceTracker();
            tracker.RecordFillAttempt(Network, Format, true);

            string key = Prefix + $"fill_{Network}_{Format}";
            Assert.IsTrue(PlayerPrefs.HasKey(key),
                "RecordFillAttempt must persist fill rate to PlayerPrefs");
        }

        [Test]
        public void RecordRevenue_PersistsAvgRevenueToPlayerPrefs()
        {
            var tracker = new AdNetworkPerformanceTracker();
            tracker.RecordRevenue(Network, Format, 3.0);

            string key = Prefix + $"rev_{Network}_{Format}";
            Assert.IsTrue(PlayerPrefs.HasKey(key),
                "RecordRevenue must persist average revenue to PlayerPrefs");
        }
    }
}
