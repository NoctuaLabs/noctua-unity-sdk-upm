using System.Collections.Generic;
using NUnit.Framework;
using Tests.Runtime;
using UnityEngine;

namespace com.noctuagames.sdk.Tests.IAA
{
    /// <summary>
    /// Unit tests for <see cref="AdExperimentManager"/>.
    /// Covers: no experiments, control/treatment variants, deterministic assignment,
    /// 50/50 distribution, segment filtering, disabled experiments, chained merges,
    /// and ad_experiment_assigned event deduplication.
    /// </summary>
    [TestFixture]
    public class AdExperimentManagerTest
    {
        private const string PrefsPrefix = "NoctuaExp_";
        private const string SegPrefix   = "NoctuaSeg_";

        private MockEventSender     _eventSender;
        private UserSegmentManager  _segmentManager;

        [SetUp]
        public void SetUp()
        {
            _eventSender    = new MockEventSender();
            ClearPrefs();
            _segmentManager = new UserSegmentManager();
        }

        [TearDown]
        public void TearDown()
        {
            ClearPrefs();
        }

        private void ClearPrefs()
        {
            PlayerPrefs.DeleteKey($"{SegPrefix}install_ticks");
            PlayerPrefs.DeleteKey($"{SegPrefix}session_count");
            PlayerPrefs.DeleteKey($"{SegPrefix}purchase_count");

            // Clear experiment prefs for both test experiment IDs
            foreach (var id in new[] { "exp_test_a", "exp_test_b", "exp_seg_filter" })
            {
                PlayerPrefs.DeleteKey($"{PrefsPrefix}{id}_variant");
                PlayerPrefs.DeleteKey($"{PrefsPrefix}{id}_fired");
            }

            PlayerPrefs.Save();
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private static AdExperimentConfig MakeExperiment(
            string id,
            bool enabled = true,
            List<string> segmentFilters = null,
            List<AdVariantConfig> variants = null)
        {
            return new AdExperimentConfig
            {
                ExperimentId    = id,
                Enabled         = enabled,
                SegmentFilters  = segmentFilters,
                Variants        = variants ?? new List<AdVariantConfig>
                {
                    new AdVariantConfig { VariantId = "control",   Weight = 50, IaaOverride = null },
                    new AdVariantConfig { VariantId = "treatment", Weight = 50, IaaOverride = new IAA
                    {
                        CooldownSeconds = new CooldownConfig { Interstitial = 10 }
                    }}
                }
            };
        }

        private AdExperimentManager MakeManager(List<AdExperimentConfig> experiments)
        {
            return new AdExperimentManager(experiments, _segmentManager, _eventSender);
        }

        // ─── No experiments ────────────────────────────────────────────────────

        [Test]
        public void ApplyExperiments_NullExperiments_ReturnsBaseConfig()
        {
            var baseConfig = new IAA { Mediation = "admob" };
            var mgr        = MakeManager(null);

            var result = mgr.ApplyExperiments(baseConfig, "US");

            Assert.AreSame(baseConfig, result);
        }

        [Test]
        public void ApplyExperiments_EmptyExperiments_ReturnsBaseConfig()
        {
            var baseConfig = new IAA { Mediation = "admob" };
            var mgr        = MakeManager(new List<AdExperimentConfig>());

            var result = mgr.ApplyExperiments(baseConfig, "US");

            Assert.AreSame(baseConfig, result);
        }

        // ─── Control variant (no override) ────────────────────────────────────

        [Test]
        public void ApplyExperiments_ControlVariant_BaseConfigUnchanged()
        {
            // Force control assignment by persisting the variant
            PlayerPrefs.SetString($"{PrefsPrefix}exp_test_a_variant", "control");

            var baseConfig = new IAA { CooldownSeconds = new CooldownConfig { Interstitial = 30 } };
            var mgr        = MakeManager(new List<AdExperimentConfig> { MakeExperiment("exp_test_a") });

            var result = mgr.ApplyExperiments(baseConfig, "US");

            // Control variant has null IaaOverride → MergeWith not called → base unchanged
            Assert.AreSame(baseConfig, result);
            Assert.AreEqual(30, result.CooldownSeconds.Interstitial);
        }

        // ─── Treatment variant (with override) ────────────────────────────────

        [Test]
        public void ApplyExperiments_TreatmentVariant_MergesOverride()
        {
            PlayerPrefs.SetString($"{PrefsPrefix}exp_test_a_variant", "treatment");

            var baseConfig = new IAA { CooldownSeconds = new CooldownConfig { Interstitial = 30 } };
            var mgr        = MakeManager(new List<AdExperimentConfig> { MakeExperiment("exp_test_a") });

            var result = mgr.ApplyExperiments(baseConfig, "US");

            Assert.AreNotSame(baseConfig, result, "Treatment must produce a new config instance");
            Assert.AreEqual(10, result.CooldownSeconds.Interstitial, "Override should replace interstitial cooldown");
        }

        // ─── Deterministic assignment ──────────────────────────────────────────

        [Test]
        public void GetAssignedVariant_Deterministic_SameResultAcrossCalls()
        {
            var experiment = MakeExperiment("exp_test_a");
            var mgr        = MakeManager(new List<AdExperimentConfig> { experiment });

            string v1 = mgr.GetAssignedVariant(experiment, "t1_nonpayer_new_d0d1", "user-abc");
            // Clear persisted variant to simulate re-computation
            PlayerPrefs.DeleteKey($"{PrefsPrefix}exp_test_a_variant");

            string v2 = mgr.GetAssignedVariant(experiment, "t1_nonpayer_new_d0d1", "user-abc");

            Assert.AreEqual(v1, v2, "Same userId+experimentId must always produce the same variant");
        }

        [Test]
        public void GetAssignedVariant_PersistedVariantReturnedWithoutRecompute()
        {
            PlayerPrefs.SetString($"{PrefsPrefix}exp_test_a_variant", "treatment");
            var experiment = MakeExperiment("exp_test_a");
            var mgr        = MakeManager(new List<AdExperimentConfig> { experiment });

            string variant = mgr.GetAssignedVariant(experiment, "t1_nonpayer_new_d0d1", "user-xyz");

            Assert.AreEqual("treatment", variant, "Persisted variant must be returned as-is for session stability");
        }

        // ─── 50/50 distribution ────────────────────────────────────────────────

        [Test]
        public void GetAssignedVariant_Distribution_Approximately50_50()
        {
            var experiment = MakeExperiment("exp_test_a");
            var mgr        = MakeManager(new List<AdExperimentConfig> { experiment });

            int control   = 0;
            int treatment = 0;

            for (int i = 0; i < 1000; i++)
            {
                // Use a unique experiment ID per iteration to bypass PlayerPrefs caching
                var exp = MakeExperiment($"exp_dist_{i}");
                string v = mgr.GetAssignedVariant(exp, "t1_nonpayer_new_d0d1", $"user-{i:D4}");
                if (v == "control")   control++;
                else                  treatment++;
            }

            // Expect roughly 500/500 ± 10% tolerance
            Assert.Greater(control,   400, $"control={control} is too low — hash distribution skewed");
            Assert.Greater(treatment, 400, $"treatment={treatment} is too low — hash distribution skewed");
        }

        // ─── Segment filtering ─────────────────────────────────────────────────

        [Test]
        public void ApplyExperiments_SegmentFiltered_T3UserSkipsT1T2Experiment()
        {
            PlayerPrefs.SetString($"{PrefsPrefix}exp_seg_filter_variant", "treatment");

            var experiment = MakeExperiment("exp_seg_filter",
                segmentFilters: new List<string> { "t1", "t2" });

            var baseConfig = new IAA { CooldownSeconds = new CooldownConfig { Interstitial = 30 } };
            var mgr        = MakeManager(new List<AdExperimentConfig> { experiment });

            // T3 country → tier = "t3" → experiment filtered out
            var result = mgr.ApplyExperiments(baseConfig, "VN");

            Assert.AreSame(baseConfig, result, "T3 user must be excluded from a T1/T2-only experiment");
        }

        [Test]
        public void ApplyExperiments_EmptySegmentFilter_AllUsersIncluded()
        {
            PlayerPrefs.SetString($"{PrefsPrefix}exp_seg_filter_variant", "treatment");

            var experiment = MakeExperiment("exp_seg_filter",
                segmentFilters: new List<string>()); // empty = all tiers

            var baseConfig = new IAA { CooldownSeconds = new CooldownConfig { Interstitial = 30 } };
            var mgr        = MakeManager(new List<AdExperimentConfig> { experiment });

            var result = mgr.ApplyExperiments(baseConfig, "VN"); // T3 country

            Assert.AreNotSame(baseConfig, result, "Empty segment filter must include all users");
        }

        // ─── Disabled experiment ───────────────────────────────────────────────

        [Test]
        public void ApplyExperiments_DisabledExperiment_Skipped()
        {
            PlayerPrefs.SetString($"{PrefsPrefix}exp_test_a_variant", "treatment");

            var experiment = MakeExperiment("exp_test_a", enabled: false);
            var baseConfig = new IAA { CooldownSeconds = new CooldownConfig { Interstitial = 30 } };
            var mgr        = MakeManager(new List<AdExperimentConfig> { experiment });

            var result = mgr.ApplyExperiments(baseConfig, "US");

            Assert.AreSame(baseConfig, result, "Disabled experiment must not apply any override");
        }

        // ─── Chained experiments ───────────────────────────────────────────────

        [Test]
        public void ApplyExperiments_TwoExperiments_OverridesChained()
        {
            PlayerPrefs.SetString($"{PrefsPrefix}exp_test_a_variant", "treatment");
            PlayerPrefs.SetString($"{PrefsPrefix}exp_test_b_variant", "treatment");

            var expA = new AdExperimentConfig
            {
                ExperimentId   = "exp_test_a",
                Enabled        = true,
                SegmentFilters = null,
                Variants = new List<AdVariantConfig>
                {
                    new AdVariantConfig { VariantId = "control",   Weight = 50, IaaOverride = null },
                    new AdVariantConfig { VariantId = "treatment", Weight = 50, IaaOverride = new IAA
                    {
                        CooldownSeconds = new CooldownConfig { Interstitial = 10, Rewarded = 60, AppOpen = 30 }
                    }}
                }
            };

            var expB = new AdExperimentConfig
            {
                ExperimentId   = "exp_test_b",
                Enabled        = true,
                SegmentFilters = null,
                Variants = new List<AdVariantConfig>
                {
                    new AdVariantConfig { VariantId = "control",   Weight = 50, IaaOverride = null },
                    new AdVariantConfig { VariantId = "treatment", Weight = 50, IaaOverride = new IAA
                    {
                        FrequencyCaps = new FrequencyCapConfig
                        {
                            Interstitial = new FrequencyCapEntry { MaxImpressions = 5, WindowSeconds = 3600 },
                            AppOpen      = new FrequencyCapEntry { MaxImpressions = 3, WindowSeconds = 3600 }
                        }
                    }}
                }
            };

            var baseConfig = new IAA
            {
                CooldownSeconds = new CooldownConfig { Interstitial = 30 },
                FrequencyCaps   = new FrequencyCapConfig
                {
                    Interstitial = new FrequencyCapEntry { MaxImpressions = 10, WindowSeconds = 3600 }
                }
            };

            var mgr    = MakeManager(new List<AdExperimentConfig> { expA, expB });
            var result = mgr.ApplyExperiments(baseConfig, "US");

            Assert.AreEqual(10, result.CooldownSeconds.Interstitial,  "Exp A override: interstitial cooldown");
            Assert.AreEqual(5,  result.FrequencyCaps.Interstitial.MaxImpressions, "Exp B override: interstitial freq cap");
        }

        // ─── ad_experiment_assigned event deduplication ────────────────────────

        [Test]
        public void TrackAssignment_FiredOnlyOnce_NotOnSubsequentCalls()
        {
            var experiment = MakeExperiment("exp_test_a");
            var mgr        = MakeManager(new List<AdExperimentConfig> { experiment });
            var baseConfig = new IAA();

            // First call — should track the event
            mgr.ApplyExperiments(baseConfig, "US");
            int countAfterFirst = _eventSender.GetEventsByName("ad_experiment_assigned").Count;

            // Second call — should NOT fire again
            mgr.ApplyExperiments(baseConfig, "US");
            int countAfterSecond = _eventSender.GetEventsByName("ad_experiment_assigned").Count;

            Assert.AreEqual(countAfterFirst, countAfterSecond,
                "ad_experiment_assigned must fire only once per experiment install");
        }

        [Test]
        public void TrackAssignment_EventContainsCorrectFields()
        {
            PlayerPrefs.SetString($"{PrefsPrefix}exp_test_a_variant", "treatment");
            var experiment = MakeExperiment("exp_test_a");
            var mgr        = MakeManager(new List<AdExperimentConfig> { experiment });

            mgr.ApplyExperiments(new IAA(), "US");

            var events = _eventSender.GetEventsByName("ad_experiment_assigned");
            Assert.AreEqual(1, events.Count);

            var data = events[0].Data;
            Assert.AreEqual("exp_test_a", data["experiment_id"].ToString());
            Assert.AreEqual("treatment",  data["variant_id"].ToString());
            Assert.IsTrue(data.ContainsKey("segment_key"), "segment_key must be present in event payload");
        }
    }
}
