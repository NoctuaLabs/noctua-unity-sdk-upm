using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace com.noctuagames.sdk.Tests.IAA
{
    /// <summary>
    /// Unit tests for <see cref="CpmFloorManager"/>.
    /// Covers: Allow/SoftFail/HardFail results, cold-start guard, segment overrides,
    /// disabled config, and unknown country tier fallback.
    /// </summary>
    [TestFixture]
    public class CpmFloorManagerTest
    {
        // Minimal config with T1/T2/T3 floors for interstitial and rewarded
        private CpmFloorConfig DefaultConfig() => new CpmFloorConfig
        {
            Enabled    = true,
            MinSamples = 10,
            Floors = new Dictionary<string, Dictionary<string, CpmFloorEntry>>
            {
                ["interstitial"] = new Dictionary<string, CpmFloorEntry>
                {
                    ["t1"] = new CpmFloorEntry { Soft = 0.50, Hard = 0.20 },
                    ["t2"] = new CpmFloorEntry { Soft = 0.20, Hard = 0.08 },
                    ["t3"] = new CpmFloorEntry { Soft = 0.05, Hard = 0.02 },
                },
                ["rewarded"] = new Dictionary<string, CpmFloorEntry>
                {
                    ["t1"] = new CpmFloorEntry { Soft = 1.00, Hard = 0.40 },
                    ["t2"] = new CpmFloorEntry { Soft = 0.40, Hard = 0.15 },
                    ["t3"] = new CpmFloorEntry { Soft = 0.10, Hard = 0.04 },
                },
            },
            SegmentOverrides = new Dictionary<string, Dictionary<string, CpmFloorEntry>>()
        };

        // ─── Allow (above soft floor) ──────────────────────────────────────────

        [Test]
        public void EvaluateFloor_AboveSoft_ReturnsAllow()
        {
            var mgr = new CpmFloorManager(DefaultConfig());

            var result = mgr.EvaluateFloor("admob", "interstitial", avgCpm: 0.60, sampleCount: 20, segmentKey: "t1_nonpayer_new_d0d1");

            Assert.AreEqual(CpmFloorResult.Allow, result);
        }

        [Test]
        public void EvaluateFloor_EqualToSoft_ReturnsAllow()
        {
            var mgr = new CpmFloorManager(DefaultConfig());

            // avgCpm == soft (0.50) → Allow
            var result = mgr.EvaluateFloor("admob", "interstitial", avgCpm: 0.50, sampleCount: 15, segmentKey: "t1_nonpayer_new_d0d1");

            Assert.AreEqual(CpmFloorResult.Allow, result);
        }

        // ─── SoftFail (between hard and soft) ─────────────────────────────────

        [Test]
        public void EvaluateFloor_BelowSoftAboveHard_ReturnsSoftFail()
        {
            var mgr = new CpmFloorManager(DefaultConfig());

            // t1 interstitial: soft=0.50, hard=0.20 → avgCpm=0.30 → SoftFail
            var result = mgr.EvaluateFloor("admob", "interstitial", avgCpm: 0.30, sampleCount: 15, segmentKey: "t1_nonpayer_new_d0d1");

            Assert.AreEqual(CpmFloorResult.SoftFail, result);
        }

        [Test]
        public void EvaluateFloor_EqualToHard_ReturnsSoftFail()
        {
            var mgr = new CpmFloorManager(DefaultConfig());

            // avgCpm == hard (0.20) → SoftFail (hard is exclusive lower bound)
            var result = mgr.EvaluateFloor("admob", "interstitial", avgCpm: 0.20, sampleCount: 15, segmentKey: "t1_nonpayer_new_d0d1");

            Assert.AreEqual(CpmFloorResult.SoftFail, result);
        }

        // ─── HardFail (below hard floor) ──────────────────────────────────────

        [Test]
        public void EvaluateFloor_BelowHard_ReturnsHardFail()
        {
            var mgr = new CpmFloorManager(DefaultConfig());

            // t1 interstitial: hard=0.20 → avgCpm=0.10 → HardFail
            var result = mgr.EvaluateFloor("admob", "interstitial", avgCpm: 0.10, sampleCount: 20, segmentKey: "t1_nonpayer_new_d0d1");

            Assert.AreEqual(CpmFloorResult.HardFail, result);
        }

        // ─── Cold-start guard (insufficient samples) ──────────────────────────

        [Test]
        public void EvaluateFloor_InsufficientSamples_ReturnsAllow()
        {
            var mgr = new CpmFloorManager(DefaultConfig());

            // Only 5 samples < MinSamples(10) → Allow regardless of CPM
            var result = mgr.EvaluateFloor("admob", "interstitial", avgCpm: 0.00, sampleCount: 5, segmentKey: "t1_nonpayer_new_d0d1");

            Assert.AreEqual(CpmFloorResult.Allow, result, "Cold-start guard must prevent hard-blocking with insufficient data");
        }

        [Test]
        public void EvaluateFloor_ZeroSamples_ReturnsAllow()
        {
            var mgr = new CpmFloorManager(DefaultConfig());

            var result = mgr.EvaluateFloor("admob", "rewarded", avgCpm: 0.00, sampleCount: 0, segmentKey: "t1_nonpayer_new_d0d1");

            Assert.AreEqual(CpmFloorResult.Allow, result);
        }

        // ─── No floor configured for format ───────────────────────────────────

        [Test]
        public void EvaluateFloor_NoFloorForFormat_ReturnsAllow()
        {
            var mgr = new CpmFloorManager(DefaultConfig());

            // "banner" is not in the floors dict → Allow
            var result = mgr.EvaluateFloor("admob", "banner", avgCpm: 0.00, sampleCount: 50, segmentKey: "t1_nonpayer_new_d0d1");

            Assert.AreEqual(CpmFloorResult.Allow, result);
        }

        // ─── Disabled config ───────────────────────────────────────────────────

        [Test]
        public void EvaluateFloor_FloorDisabled_ReturnsAllow()
        {
            var config = DefaultConfig();
            config.Enabled = false;
            var mgr = new CpmFloorManager(config);

            var result = mgr.EvaluateFloor("admob", "interstitial", avgCpm: 0.00, sampleCount: 100, segmentKey: "t1_nonpayer_new_d0d1");

            Assert.AreEqual(CpmFloorResult.Allow, result, "Disabled floor config must always return Allow");
        }

        [Test]
        public void EvaluateFloor_FloorEnabledNull_ReturnsAllow()
        {
            var config = DefaultConfig();
            config.Enabled = null;
            var mgr = new CpmFloorManager(config);

            var result = mgr.EvaluateFloor("admob", "interstitial", avgCpm: 0.00, sampleCount: 100, segmentKey: "t1_nonpayer_new_d0d1");

            Assert.AreEqual(CpmFloorResult.Allow, result);
        }

        // ─── Segment overrides ─────────────────────────────────────────────────

        [Test]
        public void EvaluateFloor_SegmentOverride_TakesPrecedenceOverTierFloor()
        {
            var config = DefaultConfig();
            // Override for a specific composite segment with a much higher floor
            config.SegmentOverrides["t1_highspender_loyal_d30plus"] =
                new Dictionary<string, CpmFloorEntry>
                {
                    ["rewarded"] = new CpmFloorEntry { Soft = 2.00, Hard = 1.00 }
                };
            var mgr = new CpmFloorManager(config);

            // avgCpm=1.20 is above t1 tier floor (soft=1.00) but below segment override soft=2.00
            // Without override: Allow. With override: SoftFail.
            var result = mgr.EvaluateFloor("admob", "rewarded", avgCpm: 1.20, sampleCount: 20,
                segmentKey: "t1_highspender_loyal_d30plus");

            Assert.AreEqual(CpmFloorResult.SoftFail, result,
                "Segment override should take precedence over default tier floor");
        }

        [Test]
        public void EvaluateFloor_SegmentOverride_OtherSegmentUsesDefaultTier()
        {
            var config = DefaultConfig();
            config.SegmentOverrides["t1_highspender_loyal_d30plus"] =
                new Dictionary<string, CpmFloorEntry>
                {
                    ["rewarded"] = new CpmFloorEntry { Soft = 2.00, Hard = 1.00 }
                };
            var mgr = new CpmFloorManager(config);

            // A different segment → uses default t1 tier floor (soft=1.00, hard=0.40)
            // avgCpm=0.80 > soft=1.00 → wait, 0.80 < 1.00 → SoftFail under t1 tier
            // Let's use 1.10 to confirm Allow from tier floor
            var result = mgr.EvaluateFloor("admob", "rewarded", avgCpm: 1.10, sampleCount: 20,
                segmentKey: "t1_nonpayer_new_d0d1");

            Assert.AreEqual(CpmFloorResult.Allow, result);
        }

        // ─── Unknown country tier ──────────────────────────────────────────────

        [Test]
        public void EvaluateFloor_UnknownCountryTier_FallsBackToT3()
        {
            var mgr = new CpmFloorManager(DefaultConfig());

            // segmentKey starts with "t3" (unknown country) → uses t3 floor (soft=0.05, hard=0.02)
            // avgCpm=0.03 is between hard(0.02) and soft(0.05) → SoftFail
            var result = mgr.EvaluateFloor("admob", "interstitial", avgCpm: 0.03, sampleCount: 20,
                segmentKey: "t3_nonpayer_new_d0d1");

            Assert.AreEqual(CpmFloorResult.SoftFail, result);
        }

        // ─── T2 tier ──────────────────────────────────────────────────────────

        [Test]
        public void EvaluateFloor_T2Tier_UsesT2Floors()
        {
            var mgr = new CpmFloorManager(DefaultConfig());

            // t2 interstitial: soft=0.20, hard=0.08 → avgCpm=0.05 < hard(0.08) → HardFail
            var result = mgr.EvaluateFloor("admob", "interstitial", avgCpm: 0.05, sampleCount: 15,
                segmentKey: "t2_nonpayer_returning_d2d7");

            Assert.AreEqual(CpmFloorResult.HardFail, result);
        }

        // ─── Network name is ignored in floor lookup ───────────────────────────

        [Test]
        public void EvaluateFloor_DifferentNetworkSameFormat_SameFloorApplied()
        {
            var mgr = new CpmFloorManager(DefaultConfig());

            // Floor lookup is by format+tier, not by network name
            var admob     = mgr.EvaluateFloor("admob",     "interstitial", 0.10, 20, "t1_nonpayer_new_d0d1");
            var applovin  = mgr.EvaluateFloor("applovin",  "interstitial", 0.10, 20, "t1_nonpayer_new_d0d1");

            Assert.AreEqual(admob, applovin, "Floor result should be the same regardless of network name");
        }
    }
}
