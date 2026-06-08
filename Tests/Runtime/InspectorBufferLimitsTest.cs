using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// EditMode tests for <see cref="InspectorBufferLimits"/> — verifies the RAM-tiered
    /// Inspector buffer capacities are monotonic and never regress below the historical defaults.
    /// </summary>
    [TestFixture]
    public class InspectorBufferLimitsTest
    {
        [Test]
        public void ForDevice_LowRam_UsesHistoricalDefaults()
        {
            var l = InspectorBufferLimits.ForDevice(2048); // 2 GB
            Assert.AreEqual(5000, l.Logs);
            Assert.AreEqual(200,  l.Trackers);
            Assert.AreEqual(100,  l.Http);
        }

        [Test]
        public void ForDevice_BelowTwoGb_StillDefaults()
        {
            var l = InspectorBufferLimits.ForDevice(1024); // 1 GB
            Assert.AreEqual(5000, l.Logs);
            Assert.AreEqual(200,  l.Trackers);
            Assert.AreEqual(100,  l.Http);
        }

        [Test]
        public void ForDevice_HigherRam_IncreasesCapacity()
        {
            var low  = InspectorBufferLimits.ForDevice(2048);
            var mid  = InspectorBufferLimits.ForDevice(3072);
            var high = InspectorBufferLimits.ForDevice(4096);
            var top  = InspectorBufferLimits.ForDevice(8192);

            Assert.Greater(mid.Logs,  low.Logs);
            Assert.Greater(high.Logs, mid.Logs);
            Assert.GreaterOrEqual(top.Logs, high.Logs);

            Assert.Greater(mid.Trackers,  low.Trackers);
            Assert.Greater(high.Trackers, mid.Trackers);

            Assert.Greater(mid.Http,  low.Http);
            Assert.Greater(high.Http, mid.Http);
        }

        [Test]
        public void ForDevice_Monotonic_NeverDecreasesAsRamGrows()
        {
            int prevLogs = 0, prevTrackers = 0, prevHttp = 0;
            foreach (var mb in new[] { 512, 1024, 2048, 3072, 4096, 6144, 8192, 12288 })
            {
                var l = InspectorBufferLimits.ForDevice(mb);
                Assert.GreaterOrEqual(l.Logs,     prevLogs,     $"Logs decreased at {mb}MB");
                Assert.GreaterOrEqual(l.Trackers, prevTrackers, $"Trackers decreased at {mb}MB");
                Assert.GreaterOrEqual(l.Http,     prevHttp,     $"Http decreased at {mb}MB");
                prevLogs = l.Logs; prevTrackers = l.Trackers; prevHttp = l.Http;
            }
        }

        [Test]
        public void ForDevice_NeverBelowDefaults()
        {
            foreach (var mb in new[] { 256, 512, 1024, 2048, 3072, 4096, 6144, 16384 })
            {
                var l = InspectorBufferLimits.ForDevice(mb);
                Assert.GreaterOrEqual(l.Logs,     5000);
                Assert.GreaterOrEqual(l.Trackers, 200);
                Assert.GreaterOrEqual(l.Http,     100);
            }
        }
    }
}
