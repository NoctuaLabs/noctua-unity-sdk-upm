using NUnit.Framework;
using UnityEngine;

namespace com.noctuagames.sdk.Tests.IAA
{
    /// <summary>
    /// Unit tests for <see cref="AdNetworkPerformanceTracker"/>.
    /// Covers: fill rate recording, revenue averaging, preferred network scoring,
    /// history queue overflow, and PlayerPrefs persistence fallback.
    /// </summary>
    [TestFixture]
    public class AdNetworkPerformanceTrackerTest
    {
        private const string Admob    = "admob";
        private const string AppLovin = "applovin";
        private const string Inter    = AdFormatKey.Interstitial;
        private const string Rewarded = AdFormatKey.Rewarded;

        [SetUp]
        public void SetUp()
        {
            // Clear all performance prefs before each test
            foreach (var network in new[] { Admob, AppLovin })
            foreach (var format  in new[] { Inter, Rewarded, AdFormatKey.Banner, AdFormatKey.AppOpen })
            {
                PlayerPrefs.DeleteKey($"NoctuaAdPerf_fill_{network}_{format}");
                PlayerPrefs.DeleteKey($"NoctuaAdPerf_rev_{network}_{format}");
            }
            PlayerPrefs.Save();
        }

        // ─── RecordFillAttempt / GetFillRate ─────────────────────────────────

        [Test]
        public void GetFillRate_AllFilled_Returns1()
        {
            var tracker = new AdNetworkPerformanceTracker();
            for (int i = 0; i < 5; i++)
                tracker.RecordFillAttempt(Admob, Inter, filled: true);

            Assert.AreEqual(1.0, tracker.GetFillRate(Admob, Inter), delta: 0.001);
        }

        [Test]
        public void GetFillRate_NoneFilled_Returns0()
        {
            var tracker = new AdNetworkPerformanceTracker();
            for (int i = 0; i < 5; i++)
                tracker.RecordFillAttempt(Admob, Inter, filled: false);

            Assert.AreEqual(0.0, tracker.GetFillRate(Admob, Inter), delta: 0.001);
        }

        [Test]
        public void GetFillRate_HalfFilled_Returns05()
        {
            var tracker = new AdNetworkPerformanceTracker();
            for (int i = 0; i < 4; i++)
                tracker.RecordFillAttempt(Admob, Inter, filled: i % 2 == 0);

            Assert.AreEqual(0.5, tracker.GetFillRate(Admob, Inter), delta: 0.001);
        }

        [Test]
        public void GetFillRate_NoDataInMemory_ReturnsPersistedDefault()
        {
            // When no in-memory data, falls back to PlayerPrefs (default 0.5f)
            var tracker = new AdNetworkPerformanceTracker();
            double rate = tracker.GetFillRate(Admob, Inter);

            Assert.AreEqual(0.5, rate, delta: 0.001, "Default from PlayerPrefs.GetFloat(..., 0.5f)");
        }

        [Test]
        public void RecordFillAttempt_QueueOverflow_OldestEvicted()
        {
            // MaxFillSamples = 100 — after 100 fills add 1 more non-fill
            var tracker = new AdNetworkPerformanceTracker();
            for (int i = 0; i < 100; i++)
                tracker.RecordFillAttempt(Admob, Inter, filled: true);

            // Add one failed fill — after eviction: 99 filled + 1 failed = 99%
            tracker.RecordFillAttempt(Admob, Inter, filled: false);

            double rate = tracker.GetFillRate(Admob, Inter);
            Assert.AreEqual(0.99, rate, delta: 0.001, "Queue should cap at 100 and evict oldest");
        }

        [Test]
        public void RecordFillAttempt_DifferentFormatsTrackedSeparately()
        {
            var tracker = new AdNetworkPerformanceTracker();
            for (int i = 0; i < 4; i++)
                tracker.RecordFillAttempt(Admob, Inter,    filled: true);
            for (int i = 0; i < 4; i++)
                tracker.RecordFillAttempt(Admob, Rewarded, filled: false);

            Assert.AreEqual(1.0, tracker.GetFillRate(Admob, Inter),    delta: 0.001);
            Assert.AreEqual(0.0, tracker.GetFillRate(Admob, Rewarded), delta: 0.001);
        }

        [Test]
        public void RecordFillAttempt_DifferentNetworksTrackedSeparately()
        {
            var tracker = new AdNetworkPerformanceTracker();
            for (int i = 0; i < 4; i++)
                tracker.RecordFillAttempt(Admob,    Inter, filled: true);
            for (int i = 0; i < 4; i++)
                tracker.RecordFillAttempt(AppLovin, Inter, filled: false);

            Assert.AreEqual(1.0, tracker.GetFillRate(Admob,    Inter), delta: 0.001);
            Assert.AreEqual(0.0, tracker.GetFillRate(AppLovin, Inter), delta: 0.001);
        }

        // ─── RecordRevenue / GetAverageRevenue ────────────────────────────────

        [Test]
        public void GetAverageRevenue_SingleSample_ReturnsExact()
        {
            var tracker = new AdNetworkPerformanceTracker();
            tracker.RecordRevenue(Admob, Inter, 0.05);

            Assert.AreEqual(0.05, tracker.GetAverageRevenue(Admob, Inter), delta: 0.0001);
        }

        [Test]
        public void GetAverageRevenue_MultipleSamples_ReturnsAverage()
        {
            var tracker = new AdNetworkPerformanceTracker();
            tracker.RecordRevenue(Admob, Inter, 0.01);
            tracker.RecordRevenue(Admob, Inter, 0.03);

            Assert.AreEqual(0.02, tracker.GetAverageRevenue(Admob, Inter), delta: 0.0001);
        }

        [Test]
        public void GetAverageRevenue_NoDataInMemory_ReturnsPersistedDefault()
        {
            var tracker = new AdNetworkPerformanceTracker();
            // Default PlayerPrefs.GetFloat = 0f
            Assert.AreEqual(0.0, tracker.GetAverageRevenue(Admob, Inter), delta: 0.0001);
        }

        [Test]
        public void RecordRevenue_QueueOverflow_OldestEvicted()
        {
            // MaxRevenueSamples = 50 — fill 50 with 0.02 then add 0.00
            var tracker = new AdNetworkPerformanceTracker();
            for (int i = 0; i < 50; i++)
                tracker.RecordRevenue(Admob, Inter, 0.02);

            // 51st sample → evicts first 0.02, adds 0.00
            tracker.RecordRevenue(Admob, Inter, 0.00);

            // 49 × 0.02 + 1 × 0.00 = 0.98 / 50 = 0.0196
            double avg = tracker.GetAverageRevenue(Admob, Inter);
            Assert.AreEqual(49 * 0.02 / 50, avg, delta: 0.0001);
        }

        // ─── GetPreferredNetwork ──────────────────────────────────────────────

        [Test]
        public void GetPreferredNetwork_NoData_ReturnsNull()
        {
            var tracker = new AdNetworkPerformanceTracker();
            Assert.IsNull(tracker.GetPreferredNetwork(Inter));
        }

        [Test]
        public void GetPreferredNetwork_OnlyOneNetwork_ReturnsThatNetwork()
        {
            var tracker = new AdNetworkPerformanceTracker();
            tracker.RecordFillAttempt(Admob, Inter, true);
            tracker.RecordRevenue(Admob, Inter, 0.01);

            Assert.AreEqual(Admob, tracker.GetPreferredNetwork(Inter));
        }

        [Test]
        public void GetPreferredNetwork_HigherScoreWins()
        {
            var tracker = new AdNetworkPerformanceTracker();

            // admob: fillRate=0.5, avgRevenue=0.01 → score=0.005
            tracker.RecordFillAttempt(Admob, Inter, true);
            tracker.RecordFillAttempt(Admob, Inter, false);
            tracker.RecordRevenue(Admob, Inter, 0.01);

            // applovin: fillRate=1.0, avgRevenue=0.02 → score=0.02
            tracker.RecordFillAttempt(AppLovin, Inter, true);
            tracker.RecordRevenue(AppLovin, Inter, 0.02);

            Assert.AreEqual(AppLovin, tracker.GetPreferredNetwork(Inter),
                "AppLovin has higher composite score (fillRate * avgRevenue)");
        }

        [Test]
        public void GetPreferredNetwork_ZeroRevenueBoth_ReturnsANetworkOrNull()
        {
            var tracker = new AdNetworkPerformanceTracker();

            // Both have fill but zero revenue → score = 0
            tracker.RecordFillAttempt(Admob,    Inter, true);
            tracker.RecordFillAttempt(AppLovin, Inter, true);

            // Score = 0 for both; bestScore stays -1 for the second if both are 0
            // (first one found wins when score ties at 0 > -1)
            string preferred = tracker.GetPreferredNetwork(Inter);
            // Just verify it doesn't throw and returns one of the known networks
            Assert.IsTrue(preferred == Admob || preferred == AppLovin || preferred == null);
        }

        [Test]
        public void GetPreferredNetwork_DataOnlyForAnotherFormat_ReturnsNull()
        {
            var tracker = new AdNetworkPerformanceTracker();
            tracker.RecordFillAttempt(Admob, Rewarded, true);

            // Querying Interstitial when only Rewarded has data → null
            Assert.IsNull(tracker.GetPreferredNetwork(Inter));
        }
    }
}
