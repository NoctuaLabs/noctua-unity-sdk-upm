using System.Collections.Generic;
using NUnit.Framework;

namespace com.noctuagames.sdk.Tests.IAA
{
    /// <summary>
    /// Unit tests for <see cref="IAA.MergeWith"/> and <see cref="EnabledFormatsConfig"/> merging.
    /// Verifies field-level merge semantics: remote non-null fields override local,
    /// local fields are preserved when remote is null.
    /// </summary>
    [TestFixture]
    public class IAAConfigTest
    {
        // ─── MergeWith(null) ──────────────────────────────────────────────────

        [Test]
        public void MergeWith_NullRemote_ReturnsSelf()
        {
            var local = new IAA { Mediation = "admob" };
            var result = local.MergeWith(null);

            Assert.AreSame(local, result);
        }

        // ─── Top-level field merge ────────────────────────────────────────────

        [Test]
        public void MergeWith_RemoteMediation_OverridesLocal()
        {
            var local  = new IAA { Mediation = "admob" };
            var remote = new IAA { Mediation = "applovin" };

            var result = local.MergeWith(remote);

            Assert.AreEqual("applovin", result.Mediation);
        }

        [Test]
        public void MergeWith_RemoteMediationNull_LocalPreserved()
        {
            var local  = new IAA { Mediation = "admob" };
            var remote = new IAA { Mediation = null };

            var result = local.MergeWith(remote);

            Assert.AreEqual("admob", result.Mediation,
                "Local mediation should be preserved when remote has null");
        }

        [Test]
        public void MergeWith_RemoteSecondaryMediation_OverridesLocal()
        {
            var local  = new IAA { SecondaryMediation = "applovin" };
            var remote = new IAA { SecondaryMediation = "chartboost" };

            var result = local.MergeWith(remote);

            Assert.AreEqual("chartboost", result.SecondaryMediation);
        }

        [Test]
        public void MergeWith_RemoteAdFormatOverrides_OverridesLocal()
        {
            var local  = new IAA { AdFormatOverrides = new Dictionary<string, string> { ["interstitial"] = "admob" } };
            var remote = new IAA { AdFormatOverrides = new Dictionary<string, string> { ["interstitial"] = "applovin" } };

            var result = local.MergeWith(remote);

            Assert.AreEqual("applovin", result.AdFormatOverrides["interstitial"]);
        }

        [Test]
        public void MergeWith_RemoteFrequencyCaps_OverridesLocal()
        {
            var local  = new IAA { FrequencyCaps = new FrequencyCapConfig
            {
                Interstitial = new FrequencyCapEntry { MaxImpressions = 5, WindowSeconds = 3600 }
            }};
            var remote = new IAA { FrequencyCaps = new FrequencyCapConfig
            {
                Interstitial = new FrequencyCapEntry { MaxImpressions = 10, WindowSeconds = 7200 }
            }};

            var result = local.MergeWith(remote);

            Assert.AreEqual(10,   result.FrequencyCaps.Interstitial.MaxImpressions);
            Assert.AreEqual(7200, result.FrequencyCaps.Interstitial.WindowSeconds);
        }

        [Test]
        public void MergeWith_RemoteFrequencyCapsNull_LocalPreserved()
        {
            var local  = new IAA { FrequencyCaps = new FrequencyCapConfig
            {
                AppOpen = new FrequencyCapEntry { MaxImpressions = 3, WindowSeconds = 3600 }
            }};
            var remote = new IAA { FrequencyCaps = null };

            var result = local.MergeWith(remote);

            Assert.IsNotNull(result.FrequencyCaps);
            Assert.AreEqual(3, result.FrequencyCaps.AppOpen.MaxImpressions,
                "Local frequency caps should be preserved when remote is null");
        }

        [Test]
        public void MergeWith_RemoteCooldownSeconds_OverridesLocal()
        {
            var local  = new IAA { CooldownSeconds = new CooldownConfig { Interstitial = 15 } };
            var remote = new IAA { CooldownSeconds = new CooldownConfig { Interstitial = 30 } };

            var result = local.MergeWith(remote);

            Assert.AreEqual(30, result.CooldownSeconds.Interstitial);
        }

        [Test]
        public void MergeWith_RemoteDynamicOptimization_OverridesLocal()
        {
            var local  = new IAA { DynamicOptimization = false };
            var remote = new IAA { DynamicOptimization = true };

            var result = local.MergeWith(remote);

            Assert.IsTrue(result.DynamicOptimization);
        }

        [Test]
        public void MergeWith_RemoteDynamicOptimizationNull_LocalPreserved()
        {
            var local  = new IAA { DynamicOptimization = true };
            var remote = new IAA { DynamicOptimization = null };

            var result = local.MergeWith(remote);

            Assert.IsTrue(result.DynamicOptimization);
        }

        [Test]
        public void MergeWith_RemoteAppOpenAutoShow_OverridesLocal()
        {
            var local  = new IAA { AppOpenAutoShow = false };
            var remote = new IAA { AppOpenAutoShow = true };

            var result = local.MergeWith(remote);

            Assert.IsTrue(result.AppOpenAutoShow);
        }

        [Test]
        public void MergeWith_RemoteTaichi_OverridesLocal()
        {
            var local  = new IAA { Taichi = new TaichiConfig { RevenueThreshold = 0.01f } };
            var remote = new IAA { Taichi = new TaichiConfig { RevenueThreshold = 0.05f } };

            var result = local.MergeWith(remote);

            Assert.AreEqual(0.05f, result.Taichi.RevenueThreshold);
        }

        [Test]
        public void MergeWith_ProducesNewInstance_DoesNotMutateLocal()
        {
            var local  = new IAA { Mediation = "admob" };
            var remote = new IAA { Mediation = "applovin" };

            var result = local.MergeWith(remote);

            Assert.AreNotSame(local, result,  "MergeWith must return a new object");
            Assert.AreEqual("admob", local.Mediation, "Local should remain unchanged");
        }

        // ─── EnabledFormats field-level merge ─────────────────────────────────

        [Test]
        public void MergeWith_EnabledFormats_FieldLevelMerge_RemoteFieldOverridesLocal()
        {
            var local  = new IAA
            {
                EnabledFormats = new EnabledFormatsConfig
                {
                    Interstitial         = true,
                    Rewarded             = true,
                    RewardedInterstitial = false,
                    Banner               = true,
                    AppOpen              = true,
                }
            };

            var remote = new IAA
            {
                EnabledFormats = new EnabledFormatsConfig
                {
                    RewardedInterstitial = null,  // absent → keep local false
                    AppOpen              = false,  // override local true
                }
            };

            var result = local.MergeWith(remote);

            Assert.IsTrue(result.EnabledFormats.Interstitial,          "Local true kept");
            Assert.IsTrue(result.EnabledFormats.Rewarded,              "Local true kept");
            Assert.IsFalse(result.EnabledFormats.RewardedInterstitial, "Local false kept (remote null)");
            Assert.IsTrue(result.EnabledFormats.Banner,                "Local true kept");
            Assert.IsFalse(result.EnabledFormats.AppOpen,              "Remote false overrides local true");
        }

        [Test]
        public void MergeWith_EnabledFormatsRemoteNull_LocalPreserved()
        {
            var local = new IAA
            {
                EnabledFormats = new EnabledFormatsConfig
                {
                    Interstitial = false,
                    Rewarded     = true,
                }
            };
            var remote = new IAA { EnabledFormats = null };

            var result = local.MergeWith(remote);

            Assert.IsFalse(result.EnabledFormats.Interstitial, "Local false preserved");
            Assert.IsTrue(result.EnabledFormats.Rewarded,       "Local true preserved");
        }

        [Test]
        public void MergeWith_EnabledFormatsLocalNull_RemoteApplied()
        {
            var local  = new IAA { EnabledFormats = null };
            var remote = new IAA
            {
                EnabledFormats = new EnabledFormatsConfig { Interstitial = false }
            };

            var result = local.MergeWith(remote);

            Assert.IsNotNull(result.EnabledFormats);
            Assert.IsFalse(result.EnabledFormats.Interstitial,
                "Remote value should apply when local EnabledFormats is null");
        }

        [Test]
        public void MergeWith_BothEnabledFormatsNull_ResultIsEmptyNotNull()
        {
            var local  = new IAA { EnabledFormats = null };
            var remote = new IAA { EnabledFormats = null };

            var result = local.MergeWith(remote);

            // MergeEnabledFormats: remote==null → return local ?? new EnabledFormatsConfig()
            Assert.IsNotNull(result.EnabledFormats,
                "Result should be an empty EnabledFormatsConfig, not null");
        }

        // ─── Full merge scenario: simulated server partial override ───────────

        [Test]
        public void MergeWith_ServerSendsPartialConfig_LocalDefaultsPreserved()
        {
            var local = new IAA
            {
                Mediation          = "admob",
                SecondaryMediation = "applovin",
                AppOpenAutoShow    = true,
                EnabledFormats     = new EnabledFormatsConfig
                {
                    Interstitial         = true,
                    Rewarded             = true,
                    RewardedInterstitial = false,
                    Banner               = true,
                    AppOpen              = true,
                },
                FrequencyCaps = new FrequencyCapConfig
                {
                    Interstitial = new FrequencyCapEntry { MaxImpressions = 10, WindowSeconds = 3600 },
                    AppOpen      = new FrequencyCapEntry { MaxImpressions = 3, WindowSeconds = 3600 },
                },
                CooldownSeconds = new CooldownConfig { Interstitial = 15, AppOpen = 30 },
            };

            // Server only overrides AppOpen auto-show and disables rewarded_interstitial
            var remote = new IAA
            {
                AppOpenAutoShow = false,
                EnabledFormats  = new EnabledFormatsConfig
                {
                    RewardedInterstitial = true,  // re-enable on server
                }
            };

            var result = local.MergeWith(remote);

            // Server-provided fields
            Assert.IsFalse(result.AppOpenAutoShow,                    "Server override applied");
            Assert.IsTrue(result.EnabledFormats.RewardedInterstitial, "Server re-enabled RI");

            // Local fields preserved
            Assert.AreEqual("admob",    result.Mediation);
            Assert.AreEqual("applovin", result.SecondaryMediation);
            Assert.IsTrue(result.EnabledFormats.Interstitial);
            Assert.IsTrue(result.EnabledFormats.Rewarded);
            Assert.IsTrue(result.EnabledFormats.Banner);
            Assert.IsTrue(result.EnabledFormats.AppOpen);
            Assert.AreEqual(10, result.FrequencyCaps.Interstitial.MaxImpressions);
            Assert.AreEqual(15, result.CooldownSeconds.Interstitial);
            Assert.AreEqual(30, result.CooldownSeconds.AppOpen);
        }
    }
}
