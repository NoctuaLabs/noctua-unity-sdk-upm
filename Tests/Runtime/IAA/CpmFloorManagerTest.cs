using System.Collections.Generic;
using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime.IAA
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

    /// <summary>
    /// Extended edge-case tests for <see cref="CpmFloorManager"/>.
    /// Covers: HasFloor, null/empty configs, null floors dict, MinSamples default,
    /// app_open format, unknown-tier segmentKey, t3 fallback when exact tier missing,
    /// segment override without matching format, and zero-value floor fields.
    /// </summary>
    [TestFixture]
    public class CpmFloorManagerEdgeCaseTest
    {
        private const string Interstitial = "interstitial";
        private const string Rewarded     = "rewarded";
        private const string AppOpen      = "app_open";
        private const string Banner       = "banner";

        private CpmFloorConfig DefaultConfig() => new CpmFloorConfig
        {
            Enabled    = true,
            MinSamples = 10,
            Floors = new Dictionary<string, Dictionary<string, CpmFloorEntry>>
            {
                [Interstitial] = new Dictionary<string, CpmFloorEntry>
                {
                    ["t1"] = new CpmFloorEntry { Soft = 0.50, Hard = 0.20 },
                    ["t2"] = new CpmFloorEntry { Soft = 0.20, Hard = 0.08 },
                    ["t3"] = new CpmFloorEntry { Soft = 0.05, Hard = 0.02 },
                },
                [Rewarded] = new Dictionary<string, CpmFloorEntry>
                {
                    ["t1"] = new CpmFloorEntry { Soft = 1.00, Hard = 0.40 },
                    ["t2"] = new CpmFloorEntry { Soft = 0.40, Hard = 0.15 },
                    ["t3"] = new CpmFloorEntry { Soft = 0.10, Hard = 0.04 },
                },
            },
            SegmentOverrides = new Dictionary<string, Dictionary<string, CpmFloorEntry>>()
        };

        // ─── HasFloor ──────────────────────────────────────────────────────────

        [Test]
        public void HasFloor_ConfiguredFormat_ReturnsTrue()
        {
            var mgr = new CpmFloorManager(DefaultConfig());

            Assert.IsTrue(mgr.HasFloor(Interstitial));
            Assert.IsTrue(mgr.HasFloor(Rewarded));
        }

        [Test]
        public void HasFloor_UnknownFormat_ReturnsFalse()
        {
            var mgr = new CpmFloorManager(DefaultConfig());

            Assert.IsFalse(mgr.HasFloor(Banner));
            Assert.IsFalse(mgr.HasFloor(AppOpen));
        }

        [Test]
        public void HasFloor_DisabledConfig_ReturnsFalse()
        {
            var config = DefaultConfig();
            config.Enabled = false;
            var mgr = new CpmFloorManager(config);

            Assert.IsFalse(mgr.HasFloor(Interstitial));
        }

        [Test]
        public void HasFloor_NullEnabledFlag_ReturnsFalse()
        {
            var config = DefaultConfig();
            config.Enabled = null;
            var mgr = new CpmFloorManager(config);

            Assert.IsFalse(mgr.HasFloor(Interstitial));
        }

        [Test]
        public void HasFloor_NullFloorsDict_ReturnsFalse()
        {
            var config = DefaultConfig();
            config.Floors = null;
            var mgr = new CpmFloorManager(config);

            Assert.IsFalse(mgr.HasFloor(Interstitial));
        }

        // ─── Null / empty config guards ────────────────────────────────────────

        [Test]
        public void EvaluateFloor_NullConfig_ReturnsAllow()
        {
            var mgr = new CpmFloorManager(null);

            Assert.DoesNotThrow(() =>
            {
                var result = mgr.EvaluateFloor("admob", Interstitial, avgCpm: 0.00, sampleCount: 100, segmentKey: "t1_nonpayer_new_d0d1");
                Assert.AreEqual(CpmFloorResult.Allow, result);
            });
        }

        [Test]
        public void EvaluateFloor_NullFloorsDict_ReturnsAllow()
        {
            var config = DefaultConfig();
            config.Floors = null;
            var mgr = new CpmFloorManager(config);

            var result = mgr.EvaluateFloor("admob", Interstitial, avgCpm: 0.00, sampleCount: 100, segmentKey: "t1_nonpayer_new_d0d1");

            Assert.AreEqual(CpmFloorResult.Allow, result);
        }

        [Test]
        public void EvaluateFloor_NullSegmentKey_DoesNotThrow()
        {
            var mgr = new CpmFloorManager(DefaultConfig());

            Assert.DoesNotThrow(() =>
            {
                // null segmentKey → ExtractCountryTier returns "t3"
                var result = mgr.EvaluateFloor("admob", Interstitial, avgCpm: 0.10, sampleCount: 20, segmentKey: null);
                // t3: soft=0.05, hard=0.02. avgCpm=0.10 > soft=0.05 → Allow
                Assert.AreEqual(CpmFloorResult.Allow, result, "Null segmentKey falls back to t3; avgCpm 0.10 > soft 0.05 → Allow");
            });
        }

        [Test]
        public void EvaluateFloor_NullSegmentKey_FallsBackToT3Floor()
        {
            var mgr = new CpmFloorManager(DefaultConfig());

            // t3 interstitial: soft=0.05, hard=0.02
            // avgCpm=0.03: between hard(0.02) and soft(0.05) → SoftFail
            var result = mgr.EvaluateFloor("admob", Interstitial, avgCpm: 0.03, sampleCount: 20, segmentKey: null);

            Assert.AreEqual(CpmFloorResult.SoftFail, result, "Null segmentKey must fall back to t3 floors");
        }

        [Test]
        public void EvaluateFloor_EmptySegmentKey_FallsBackToT3Floor()
        {
            var mgr = new CpmFloorManager(DefaultConfig());

            // t3 interstitial: hard=0.02 → avgCpm=0.01 < hard → HardFail
            var result = mgr.EvaluateFloor("admob", Interstitial, avgCpm: 0.01, sampleCount: 20, segmentKey: "");

            Assert.AreEqual(CpmFloorResult.HardFail, result, "Empty segmentKey must fall back to t3 floors");
        }

        // ─── MinSamples default (null MinSamples uses DefaultMinSamples=10) ────

        [Test]
        public void EvaluateFloor_NullMinSamples_UsesDefaultOf10()
        {
            var config = DefaultConfig();
            config.MinSamples = null;  // forces default of 10
            var mgr = new CpmFloorManager(config);

            // 9 samples < default 10 → cold-start Allow
            var result = mgr.EvaluateFloor("admob", Interstitial, avgCpm: 0.00, sampleCount: 9, segmentKey: "t1_nonpayer_new_d0d1");

            Assert.AreEqual(CpmFloorResult.Allow, result, "Null MinSamples must default to 10; 9 samples < 10 → cold-start Allow");
        }

        [Test]
        public void EvaluateFloor_NullMinSamples_ExactlyAtDefault_AppliesToFloor()
        {
            var config = DefaultConfig();
            config.MinSamples = null;
            var mgr = new CpmFloorManager(config);

            // 10 samples == default 10 → floor applies; t1 interstitial soft=0.50, avgCpm=0.10 → HardFail
            var result = mgr.EvaluateFloor("admob", Interstitial, avgCpm: 0.10, sampleCount: 10, segmentKey: "t1_nonpayer_new_d0d1");

            Assert.AreEqual(CpmFloorResult.HardFail, result, "Exactly 10 samples with null MinSamples must apply the floor");
        }

        // ─── App open and banner formats ───────────────────────────────────────

        [Test]
        public void EvaluateFloor_AppOpenFormat_NoFloorConfigured_ReturnsAllow()
        {
            var mgr = new CpmFloorManager(DefaultConfig());

            var result = mgr.EvaluateFloor("admob", AppOpen, avgCpm: 0.00, sampleCount: 100, segmentKey: "t1_nonpayer_new_d0d1");

            Assert.AreEqual(CpmFloorResult.Allow, result, "app_open not in floors → Allow");
        }

        [Test]
        public void EvaluateFloor_AppOpenFormat_WithFloorConfigured_EvaluatesCorrectly()
        {
            var config = DefaultConfig();
            config.Floors[AppOpen] = new Dictionary<string, CpmFloorEntry>
            {
                ["t1"] = new CpmFloorEntry { Soft = 0.80, Hard = 0.30 }
            };
            var mgr = new CpmFloorManager(config);

            // avgCpm=0.50: above hard(0.30), below soft(0.80) → SoftFail
            var result = mgr.EvaluateFloor("admob", AppOpen, avgCpm: 0.50, sampleCount: 20, segmentKey: "t1_nonpayer_new_d0d1");

            Assert.AreEqual(CpmFloorResult.SoftFail, result);
        }

        // ─── SegmentKey with no underscore / invalid tier prefix ───────────────

        [Test]
        public void EvaluateFloor_SegmentKeyNoUnderscore_FallsBackToT3()
        {
            var mgr = new CpmFloorManager(DefaultConfig());

            // "justplain" has no underscore → ExtractCountryTier returns "t3"
            // t3 interstitial: soft=0.05, hard=0.02; avgCpm=0.10 >= 0.05 → Allow
            var result = mgr.EvaluateFloor("admob", Interstitial, avgCpm: 0.10, sampleCount: 20, segmentKey: "justplain");

            Assert.AreEqual(CpmFloorResult.Allow, result);
        }

        [Test]
        public void EvaluateFloor_SegmentKeyUnknownTierPrefix_FallsBackToT3()
        {
            var mgr = new CpmFloorManager(DefaultConfig());

            // "t4_nonpayer_new_d0d1" → tier="t4" → not t1/t2/t3 → ExtractCountryTier returns "t3"
            // t3 interstitial: hard=0.02, avgCpm=0.01 < 0.02 → HardFail
            var result = mgr.EvaluateFloor("admob", Interstitial, avgCpm: 0.01, sampleCount: 20, segmentKey: "t4_nonpayer_new_d0d1");

            Assert.AreEqual(CpmFloorResult.HardFail, result);
        }

        // ─── T1 floor missing — fallback to t3 ────────────────────────────────

        [Test]
        public void EvaluateFloor_T1FloorMissing_FallsBackToT3()
        {
            var config = DefaultConfig();
            // Remove t1 from interstitial floors; keep t2 and t3 only
            config.Floors[Interstitial].Remove("t1");
            var mgr = new CpmFloorManager(config);

            // segmentKey starts with t1, but t1 floor missing → falls back to t3 (soft=0.05, hard=0.02)
            // avgCpm=0.03: SoftFail under t3 floor
            var result = mgr.EvaluateFloor("admob", Interstitial, avgCpm: 0.03, sampleCount: 20, segmentKey: "t1_nonpayer_new_d0d1");

            Assert.AreEqual(CpmFloorResult.SoftFail, result, "Missing t1 floor should fall back to t3");
        }

        [Test]
        public void EvaluateFloor_T2FloorMissing_FallsBackToT3()
        {
            var config = DefaultConfig();
            config.Floors[Interstitial].Remove("t2");
            var mgr = new CpmFloorManager(config);

            // t2 missing → falls back to t3 floor (soft=0.05, hard=0.02); avgCpm=0.03 → SoftFail
            var result = mgr.EvaluateFloor("admob", Interstitial, avgCpm: 0.03, sampleCount: 20, segmentKey: "t2_nonpayer_new_d0d1");

            Assert.AreEqual(CpmFloorResult.SoftFail, result, "Missing t2 floor should fall back to t3");
        }

        [Test]
        public void EvaluateFloor_BothT1AndT3Missing_ReturnsAllow()
        {
            var config = DefaultConfig();
            config.Floors[Interstitial].Remove("t1");
            config.Floors[Interstitial].Remove("t3");
            var mgr = new CpmFloorManager(config);

            // t1 missing, t3 also missing → ResolveFloor returns null → Allow
            var result = mgr.EvaluateFloor("admob", Interstitial, avgCpm: 0.00, sampleCount: 20, segmentKey: "t1_nonpayer_new_d0d1");

            Assert.AreEqual(CpmFloorResult.Allow, result);
        }

        // ─── Segment override does not cover the requested format ──────────────

        [Test]
        public void EvaluateFloor_SegmentOverrideForOtherFormat_UsesDefaultTierFloor()
        {
            var config = DefaultConfig();
            // Override exists for segment key but only covers "rewarded", not "interstitial"
            config.SegmentOverrides["t1_nonpayer_new_d0d1"] = new Dictionary<string, CpmFloorEntry>
            {
                [Rewarded] = new CpmFloorEntry { Soft = 2.00, Hard = 1.00 }
            };
            var mgr = new CpmFloorManager(config);

            // Requesting "interstitial" → override doesn't match format → falls through to t1 tier
            // t1 interstitial: soft=0.50, hard=0.20; avgCpm=0.30 → SoftFail
            var result = mgr.EvaluateFloor("admob", Interstitial, avgCpm: 0.30, sampleCount: 20, segmentKey: "t1_nonpayer_new_d0d1");

            Assert.AreEqual(CpmFloorResult.SoftFail, result, "Override for different format should not apply; tier floor used instead");
        }

        // ─── Multiple formats configured — correct floor returned per format ───

        [Test]
        public void EvaluateFloor_MultipleFormats_EachUsesItsOwnFloor()
        {
            var mgr = new CpmFloorManager(DefaultConfig());

            // t1 interstitial soft=0.50: avgCpm=0.60 → Allow
            var intResult = mgr.EvaluateFloor("admob", Interstitial, avgCpm: 0.60, sampleCount: 20, segmentKey: "t1_nonpayer_new_d0d1");
            // t1 rewarded soft=1.00: avgCpm=0.60 → SoftFail (below soft=1.00, above hard=0.40)
            var rewResult = mgr.EvaluateFloor("admob", Rewarded, avgCpm: 0.60, sampleCount: 20, segmentKey: "t1_nonpayer_new_d0d1");

            Assert.AreEqual(CpmFloorResult.Allow,    intResult, "Interstitial: 0.60 >= soft 0.50 → Allow");
            Assert.AreEqual(CpmFloorResult.SoftFail, rewResult, "Rewarded: 0.60 < soft 1.00 but >= hard 0.40 → SoftFail");
        }

        // ─── Zero-value floor fields ───────────────────────────────────────────

        [Test]
        public void EvaluateFloor_ZeroSoftFloor_NonZeroAvgCpm_ReturnsAllow()
        {
            var config = DefaultConfig();
            config.Floors[Interstitial]["t1"] = new CpmFloorEntry { Soft = 0.0, Hard = 0.0 };
            var mgr = new CpmFloorManager(config);

            // avgCpm=0.01 >= soft=0.0 → Allow
            var result = mgr.EvaluateFloor("admob", Interstitial, avgCpm: 0.01, sampleCount: 20, segmentKey: "t1_nonpayer_new_d0d1");

            Assert.AreEqual(CpmFloorResult.Allow, result, "Zero soft floor: any non-negative CPM should be Allow");
        }

        [Test]
        public void EvaluateFloor_ZeroHardFloor_ZeroAvgCpm_ReturnsSoftFail()
        {
            var config = DefaultConfig();
            // Soft > 0, Hard = 0; avgCpm = 0.0
            config.Floors[Interstitial]["t1"] = new CpmFloorEntry { Soft = 0.50, Hard = 0.0 };
            var mgr = new CpmFloorManager(config);

            // avgCpm=0.0 < soft=0.50, avgCpm=0.0 >= hard=0.0 → SoftFail
            var result = mgr.EvaluateFloor("admob", Interstitial, avgCpm: 0.0, sampleCount: 20, segmentKey: "t1_nonpayer_new_d0d1");

            Assert.AreEqual(CpmFloorResult.SoftFail, result, "Zero hard floor with zero avgCpm and positive soft floor → SoftFail");
        }

        // ─── Empty SegmentOverrides dict ──────────────────────────────────────

        [Test]
        public void EvaluateFloor_EmptySegmentOverrides_UsesFloorNormally()
        {
            var config = DefaultConfig();
            config.SegmentOverrides = new Dictionary<string, Dictionary<string, CpmFloorEntry>>();
            var mgr = new CpmFloorManager(config);

            // Should behave exactly like no overrides: t1 interstitial soft=0.50; avgCpm=0.60 → Allow
            var result = mgr.EvaluateFloor("admob", Interstitial, avgCpm: 0.60, sampleCount: 20, segmentKey: "t1_nonpayer_new_d0d1");

            Assert.AreEqual(CpmFloorResult.Allow, result);
        }

        [Test]
        public void EvaluateFloor_NullSegmentOverrides_DoesNotThrow()
        {
            var config = DefaultConfig();
            config.SegmentOverrides = null;
            var mgr = new CpmFloorManager(config);

            Assert.DoesNotThrow(() =>
            {
                var result = mgr.EvaluateFloor("admob", Interstitial, avgCpm: 0.60, sampleCount: 20, segmentKey: "t1_nonpayer_new_d0d1");
                Assert.AreEqual(CpmFloorResult.Allow, result);
            });
        }
    }
}
