using System.Collections.Generic;
using com.noctuagames.sdk;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// Unit tests for <see cref="GameServiceModels"/>:
    ///   * <see cref="IAA.MergeWith"/> — merge semantics (null-remote, field override, immutability)
    ///   * <see cref="TaichiConfig"/> default field values
    ///   * <see cref="InitGameResponse"/> / <see cref="RemoteConfigs"/> JSON deserialization
    ///   * Supporting value types: <see cref="FrequencyCapEntry"/>, <see cref="FrequencyCapConfig"/>,
    ///     <see cref="CooldownConfig"/>, <see cref="EnabledFormatsConfig"/>, <see cref="NetworkConfig"/>,
    ///     <see cref="CpmFloorEntry"/>, <see cref="AdExperimentConfig"/>, <see cref="AdVariantConfig"/>
    /// </summary>
    [TestFixture]
    public class GameServiceModelsTest
    {
        // ─── Helpers ──────────────────────────────────────────────────────────

        private static IAA BaseIaa(
            string mediation = "applovin",
            string secondary = null,
            int interstitialCooldown = 30,
            int rewardedCooldown = 10)
        {
            return new IAA
            {
                Mediation          = mediation,
                SecondaryMediation = secondary,
                CooldownSeconds    = new CooldownConfig
                {
                    Interstitial = interstitialCooldown,
                    Rewarded     = rewardedCooldown,
                },
                FrequencyCaps = new FrequencyCapConfig
                {
                    Interstitial = new FrequencyCapEntry { MaxImpressions = 3, WindowSeconds = 3600 }
                },
                EnabledFormats = new EnabledFormatsConfig
                {
                    Interstitial = true,
                    Rewarded     = true,
                },
                DynamicOptimization = false,
                AppOpenAutoShow     = false,
            };
        }

        // ─── IAA.MergeWith — null remote ─────────────────────────────────────

        [Test]
        public void MergeWith_NullRemote_ReturnsSameInstance()
        {
            var base_ = BaseIaa();
            var merged = base_.MergeWith(null);
            Assert.AreSame(base_, merged, "MergeWith(null) should return 'this'");
        }

        // ─── IAA.MergeWith — simple scalar override ───────────────────────────

        [Test]
        public void MergeWith_RemoteSetsMedation_OverridesLocal()
        {
            var base_  = BaseIaa(mediation: "applovin");
            var remote = new IAA { Mediation = "admob" };

            var merged = base_.MergeWith(remote);

            Assert.AreEqual("admob", merged.Mediation);
        }

        [Test]
        public void MergeWith_RemoteMediationNull_KeepsLocal()
        {
            var base_  = BaseIaa(mediation: "applovin");
            var remote = new IAA { Mediation = null };   // not specified

            var merged = base_.MergeWith(remote);

            Assert.AreEqual("applovin", merged.Mediation);
        }

        [Test]
        public void MergeWith_RemoteSetsSecondaryMediation_OverridesLocal()
        {
            var base_  = BaseIaa(secondary: null);
            var remote = new IAA { SecondaryMediation = "admob" };

            var merged = base_.MergeWith(remote);

            Assert.AreEqual("admob", merged.SecondaryMediation);
        }

        [Test]
        public void MergeWith_RemoteSetsDynamicOptimization_OverridesLocal()
        {
            var base_  = BaseIaa();
            base_.DynamicOptimization = false;
            var remote = new IAA { DynamicOptimization = true };

            var merged = base_.MergeWith(remote);

            Assert.IsTrue(merged.DynamicOptimization);
        }

        // ─── IAA.MergeWith — immutability ────────────────────────────────────

        [Test]
        public void MergeWith_NonNullRemote_ReturnsNewInstance()
        {
            var base_  = BaseIaa();
            var remote = new IAA { Mediation = "admob" };

            var merged = base_.MergeWith(remote);

            Assert.AreNotSame(base_,  merged, "MergeWith should return a new IAA, not mutate the base");
            Assert.AreNotSame(remote, merged);
        }

        [Test]
        public void MergeWith_NonNullRemote_DoesNotMutateBase()
        {
            var base_  = BaseIaa(mediation: "applovin");
            var remote = new IAA { Mediation = "admob" };

            base_.MergeWith(remote);

            Assert.AreEqual("applovin", base_.Mediation, "Base should be unmodified after MergeWith");
        }

        // ─── IAA.MergeWith — CooldownConfig (zero = not specified) ───────────

        [Test]
        public void MergeWith_CooldownRemotePositive_OverridesBase()
        {
            var base_  = BaseIaa(interstitialCooldown: 30, rewardedCooldown: 10);
            var remote = new IAA
            {
                CooldownSeconds = new CooldownConfig { Interstitial = 60, Rewarded = 20 }
            };

            var merged = base_.MergeWith(remote);

            Assert.AreEqual(60, merged.CooldownSeconds.Interstitial);
            Assert.AreEqual(20, merged.CooldownSeconds.Rewarded);
        }

        [Test]
        public void MergeWith_CooldownRemoteZero_KeepsBase()
        {
            // CooldownConfig int fields: 0 means "not specified", not "clear the cooldown"
            var base_  = BaseIaa(interstitialCooldown: 30, rewardedCooldown: 10);
            var remote = new IAA
            {
                CooldownSeconds = new CooldownConfig { Interstitial = 0, Rewarded = 0 }
            };

            var merged = base_.MergeWith(remote);

            Assert.AreEqual(30, merged.CooldownSeconds.Interstitial,
                "Zero in remote CooldownConfig should keep base value (0 = not specified)");
            Assert.AreEqual(10, merged.CooldownSeconds.Rewarded);
        }

        [Test]
        public void MergeWith_CooldownRemoteNull_ReturnsBaseConfig()
        {
            var baseCooldown = new CooldownConfig { Interstitial = 45 };
            var base_        = new IAA { Mediation = "applovin", CooldownSeconds = baseCooldown };
            var remote       = new IAA { CooldownSeconds = null };

            var merged = base_.MergeWith(remote);

            Assert.IsNotNull(merged.CooldownSeconds);
            Assert.AreEqual(45, merged.CooldownSeconds.Interstitial);
        }

        [Test]
        public void MergeWith_CooldownBaseNullRemotePositive_UsesRemoteValue()
        {
            var base_  = new IAA { Mediation = "applovin", CooldownSeconds = null };
            var remote = new IAA { CooldownSeconds = new CooldownConfig { Interstitial = 15 } };

            var merged = base_.MergeWith(remote);

            Assert.IsNotNull(merged.CooldownSeconds);
            Assert.AreEqual(15, merged.CooldownSeconds.Interstitial);
        }

        // ─── IAA.MergeWith — FrequencyCaps ───────────────────────────────────

        [Test]
        public void MergeWith_FrequencyCapsRemoteNull_KeepsBase()
        {
            var baseCap = new FrequencyCapConfig
            {
                Interstitial = new FrequencyCapEntry { MaxImpressions = 3, WindowSeconds = 3600 }
            };
            var base_  = new IAA { Mediation = "applovin", FrequencyCaps = baseCap };
            var remote = new IAA { FrequencyCaps = null };

            var merged = base_.MergeWith(remote);

            Assert.IsNotNull(merged.FrequencyCaps);
            Assert.IsNotNull(merged.FrequencyCaps.Interstitial);
            Assert.AreEqual(3, merged.FrequencyCaps.Interstitial.MaxImpressions);
        }

        [Test]
        public void MergeWith_FrequencyCapsRemoteEntry_OverridesBase()
        {
            var base_  = new IAA
            {
                Mediation     = "applovin",
                FrequencyCaps = new FrequencyCapConfig
                {
                    Interstitial = new FrequencyCapEntry { MaxImpressions = 3, WindowSeconds = 3600 }
                }
            };
            var remote = new IAA
            {
                FrequencyCaps = new FrequencyCapConfig
                {
                    Interstitial = new FrequencyCapEntry { MaxImpressions = 5, WindowSeconds = 7200 }
                }
            };

            var merged = base_.MergeWith(remote);

            Assert.AreEqual(5,    merged.FrequencyCaps.Interstitial.MaxImpressions);
            Assert.AreEqual(7200, merged.FrequencyCaps.Interstitial.WindowSeconds);
        }

        [Test]
        public void MergeWith_FrequencyCapsRemoteEntryNull_KeepsBaseEntry()
        {
            var base_  = new IAA
            {
                Mediation     = "applovin",
                FrequencyCaps = new FrequencyCapConfig
                {
                    Interstitial = new FrequencyCapEntry { MaxImpressions = 3, WindowSeconds = 3600 },
                    Rewarded     = new FrequencyCapEntry { MaxImpressions = 5, WindowSeconds = 1800 }
                }
            };
            // Remote only overrides Rewarded, leaves Interstitial null
            var remote = new IAA
            {
                FrequencyCaps = new FrequencyCapConfig
                {
                    Interstitial = null,
                    Rewarded     = new FrequencyCapEntry { MaxImpressions = 10, WindowSeconds = 3600 }
                }
            };

            var merged = base_.MergeWith(remote);

            // Base interstitial kept
            Assert.IsNotNull(merged.FrequencyCaps.Interstitial);
            Assert.AreEqual(3, merged.FrequencyCaps.Interstitial.MaxImpressions);
            // Rewarded overridden
            Assert.AreEqual(10, merged.FrequencyCaps.Rewarded.MaxImpressions);
        }

        // ─── IAA.MergeWith — EnabledFormats ───────────────────────────────────

        [Test]
        public void MergeWith_EnabledFormatsRemoteNull_KeepsBase()
        {
            var base_  = new IAA
            {
                Mediation      = "applovin",
                EnabledFormats = new EnabledFormatsConfig { Interstitial = true, Rewarded = false }
            };
            var remote = new IAA { EnabledFormats = null };

            var merged = base_.MergeWith(remote);

            Assert.IsNotNull(merged.EnabledFormats);
            Assert.IsTrue(merged.EnabledFormats.Interstitial);
            Assert.IsFalse(merged.EnabledFormats.Rewarded);
        }

        [Test]
        public void MergeWith_EnabledFormatsRemoteOverrides_NonNullFields()
        {
            var base_  = new IAA
            {
                Mediation      = "applovin",
                EnabledFormats = new EnabledFormatsConfig
                {
                    Interstitial = true,
                    Rewarded     = true,
                    Banner       = false,
                }
            };
            var remote = new IAA
            {
                EnabledFormats = new EnabledFormatsConfig
                {
                    Interstitial = false, // override
                    Rewarded     = null,  // keep base (null = not specified)
                    // Banner not set → null → keeps base
                }
            };

            var merged = base_.MergeWith(remote);

            Assert.IsFalse(merged.EnabledFormats.Interstitial, "Remote false should override base true");
            Assert.IsTrue(merged.EnabledFormats.Rewarded, "Null remote keeps base value");
            Assert.IsFalse(merged.EnabledFormats.Banner, "Null remote keeps base false");
        }

        [Test]
        public void MergeWith_EnabledFormatsBaseNullRemoteNonNull_UsesNewConfig()
        {
            var base_  = new IAA { Mediation = "applovin", EnabledFormats = null };
            var remote = new IAA
            {
                EnabledFormats = new EnabledFormatsConfig { AppOpen = false }
            };

            var merged = base_.MergeWith(remote);

            Assert.IsNotNull(merged.EnabledFormats);
            Assert.IsFalse(merged.EnabledFormats.AppOpen);
        }

        // ─── TaichiConfig — default values ───────────────────────────────────

        [Test]
        public void TaichiConfig_Defaults_AreSet()
        {
            var taichi = new TaichiConfig();

            Assert.AreEqual(0.01f, taichi.RevenueThreshold,          delta: 0.0001f);
            Assert.AreEqual(10,    taichi.AdCountThreshold);
            Assert.AreEqual(10,    taichi.TotalImpressionThreshold);
            Assert.AreEqual(10,    taichi.InterstitialCountThreshold);
            Assert.AreEqual(10,    taichi.RewardedCountThreshold);
            Assert.AreEqual(0.01f, taichi.RewardedRevenueThreshold,   delta: 0.0001f);
        }

        [Test]
        public void TaichiConfig_JsonRoundTrip_PreservesFields()
        {
            var taichi = new TaichiConfig
            {
                RevenueThreshold         = 0.05f,
                AdCountThreshold         = 20,
                TotalImpressionThreshold = 15,
            };
            var json  = JsonConvert.SerializeObject(taichi);
            var back  = JsonConvert.DeserializeObject<TaichiConfig>(json);

            Assert.AreEqual(0.05f, back.RevenueThreshold,         delta: 0.0001f);
            Assert.AreEqual(20,    back.AdCountThreshold);
            Assert.AreEqual(15,    back.TotalImpressionThreshold);
        }

        // ─── FrequencyCapEntry ────────────────────────────────────────────────

        [Test]
        public void FrequencyCapEntry_JsonRoundTrip_PreservesFields()
        {
            var entry = new FrequencyCapEntry { MaxImpressions = 4, WindowSeconds = 1800 };
            var json  = JsonConvert.SerializeObject(entry);
            var back  = JsonConvert.DeserializeObject<FrequencyCapEntry>(json);

            Assert.AreEqual(4,    back.MaxImpressions);
            Assert.AreEqual(1800, back.WindowSeconds);
        }

        [Test]
        public void FrequencyCapEntry_JsonKeys_AreLowerSnakeCase()
        {
            var entry = new FrequencyCapEntry { MaxImpressions = 2, WindowSeconds = 900 };
            var json  = JsonConvert.SerializeObject(entry);

            StringAssert.Contains("\"max_impressions\"", json);
            StringAssert.Contains("\"window_seconds\"", json);
        }

        // ─── CooldownConfig ───────────────────────────────────────────────────

        [Test]
        public void CooldownConfig_JsonRoundTrip_AllFormats()
        {
            var config = new CooldownConfig
            {
                Interstitial         = 30,
                Rewarded             = 10,
                RewardedInterstitial = 20,
                Banner               = 5,
                AppOpen              = 60,
            };
            var json = JsonConvert.SerializeObject(config);
            var back = JsonConvert.DeserializeObject<CooldownConfig>(json);

            Assert.AreEqual(30, back.Interstitial);
            Assert.AreEqual(10, back.Rewarded);
            Assert.AreEqual(20, back.RewardedInterstitial);
            Assert.AreEqual(5,  back.Banner);
            Assert.AreEqual(60, back.AppOpen);
        }

        // ─── EnabledFormatsConfig ─────────────────────────────────────────────

        [Test]
        public void EnabledFormatsConfig_JsonRoundTrip_NullableFields()
        {
            var config = new EnabledFormatsConfig
            {
                Interstitial         = true,
                Rewarded             = false,
                RewardedInterstitial = null,
                Banner               = true,
                AppOpen              = null,
            };
            var json = JsonConvert.SerializeObject(config);
            var back = JsonConvert.DeserializeObject<EnabledFormatsConfig>(json);

            Assert.IsTrue(back.Interstitial);
            Assert.IsFalse(back.Rewarded);
            Assert.IsNull(back.RewardedInterstitial);
            Assert.IsTrue(back.Banner);
            Assert.IsNull(back.AppOpen);
        }

        // ─── NetworkConfig ────────────────────────────────────────────────────

        [Test]
        public void NetworkConfig_JsonRoundTrip_WithAdFormats()
        {
            var config = new NetworkConfig
            {
                AdFormat = new AdFormatNoctua
                {
                    Interstitial = new AdUnit
                    {
                        Android = new AdUnitID { adUnitID = "ca-app-pub-android" },
                        IOS     = new AdUnitID { adUnitID = "ca-app-pub-ios" }
                    }
                }
            };
            var json = JsonConvert.SerializeObject(config);
            var back = JsonConvert.DeserializeObject<NetworkConfig>(json);

            Assert.IsNotNull(back.AdFormat);
            Assert.IsNotNull(back.AdFormat.Interstitial);
            Assert.AreEqual("ca-app-pub-android", back.AdFormat.Interstitial.Android.adUnitID);
            Assert.AreEqual("ca-app-pub-ios",     back.AdFormat.Interstitial.IOS.adUnitID);
        }

        // ─── CpmFloorEntry ────────────────────────────────────────────────────

        [Test]
        public void CpmFloorEntry_JsonRoundTrip_PreservesFloors()
        {
            var entry = new CpmFloorEntry { Soft = 0.5, Hard = 0.2 };
            var json  = JsonConvert.SerializeObject(entry);
            var back  = JsonConvert.DeserializeObject<CpmFloorEntry>(json);

            Assert.AreEqual(0.5, back.Soft, delta: 0.0001);
            Assert.AreEqual(0.2, back.Hard, delta: 0.0001);
        }

        // ─── AdExperimentConfig / AdVariantConfig ─────────────────────────────

        [Test]
        public void AdExperimentConfig_JsonRoundTrip_PreservesFields()
        {
            var experiment = new AdExperimentConfig
            {
                ExperimentId    = "exp_001",
                Enabled         = true,
                SegmentFilters  = new List<string> { "t1", "t2" },
                Variants        = new List<AdVariantConfig>
                {
                    new AdVariantConfig { VariantId = "control", Weight = 50,  IaaOverride = null },
                    new AdVariantConfig
                    {
                        VariantId   = "high_cap",
                        Weight      = 50,
                        IaaOverride = new IAA
                        {
                            FrequencyCaps = new FrequencyCapConfig
                            {
                                Interstitial = new FrequencyCapEntry { MaxImpressions = 5, WindowSeconds = 3600 }
                            }
                        }
                    }
                }
            };

            var json = JsonConvert.SerializeObject(experiment);
            var back = JsonConvert.DeserializeObject<AdExperimentConfig>(json);

            Assert.AreEqual("exp_001", back.ExperimentId);
            Assert.IsTrue(back.Enabled);
            Assert.AreEqual(2, back.SegmentFilters.Count);
            Assert.AreEqual(2, back.Variants.Count);
            Assert.AreEqual("control",  back.Variants[0].VariantId);
            Assert.AreEqual(50,          back.Variants[0].Weight);
            Assert.IsNull(back.Variants[0].IaaOverride);
            Assert.AreEqual("high_cap", back.Variants[1].VariantId);
            Assert.IsNotNull(back.Variants[1].IaaOverride);
            Assert.AreEqual(5,
                back.Variants[1].IaaOverride.FrequencyCaps.Interstitial.MaxImpressions);
        }

        // ─── InitGameResponse ─────────────────────────────────────────────────

        [Test]
        public void InitGameResponse_JsonDeserialization_AllFields()
        {
            const string json = @"{
                ""country"": ""ID"",
                ""ip_address"": ""1.2.3.4"",
                ""active_product_id"": ""prod_abc"",
                ""remote_configs"": null,
                ""active_bundle_ids"": [""com.example.game""],
                ""supported_currencies"": [""USD"", ""IDR""],
                ""country_to_currency_map"": {""ID"":""IDR"",""US"":""USD""},
                ""distribution_platform"": ""google"",
                ""offline_mode"": false
            }";

            var resp = JsonConvert.DeserializeObject<InitGameResponse>(json);

            Assert.AreEqual("ID",          resp.Country);
            Assert.AreEqual("1.2.3.4",     resp.IpAddress);
            Assert.AreEqual("prod_abc",    resp.ActiveProductId);
            Assert.AreEqual("google",      resp.DistributionPlatform);
            Assert.IsFalse(resp.OfflineMode);
            Assert.AreEqual(1, resp.ActiveBundleIds.Count);
            Assert.AreEqual("com.example.game", resp.ActiveBundleIds[0]);
            Assert.AreEqual(2, resp.SupportedCurrencies.Count);
            Assert.AreEqual("IDR", resp.CountryToCurrencyMap["ID"]);
        }

        [Test]
        public void InitGameResponse_OfflineModeTrue_Deserializes()
        {
            const string json = @"{""offline_mode"": true}";
            var resp = JsonConvert.DeserializeObject<InitGameResponse>(json);
            Assert.IsTrue(resp.OfflineMode);
        }

        // ─── RemoteConfigs ────────────────────────────────────────────────────

        [Test]
        public void RemoteConfigs_JsonDeserialization_EnabledPaymentTypes()
        {
            const string json = @"{
                ""enabled_payment_types"": [0, 1, 2],
                ""iaa"": null,
                ""feature_flags"": {""ssoDisabled"": ""true""}
            }";

            var cfg = JsonConvert.DeserializeObject<RemoteConfigs>(json);

            Assert.IsNotNull(cfg.EnabledPaymentTypes);
            Assert.AreEqual(3, cfg.EnabledPaymentTypes.Count);
            Assert.IsNotNull(cfg.RemoteFeatureFlags);
            Assert.AreEqual("true", cfg.RemoteFeatureFlags["ssoDisabled"]);
        }

        [Test]
        public void RemoteConfigs_WithIaa_DeserializesMediation()
        {
            const string json = @"{
                ""iaa"": { ""mediation"": ""admob"", ""secondary_mediation"": ""applovin"" }
            }";

            var cfg = JsonConvert.DeserializeObject<RemoteConfigs>(json);

            Assert.IsNotNull(cfg.IAA);
            Assert.AreEqual("admob",    cfg.IAA.Mediation);
            Assert.AreEqual("applovin", cfg.IAA.SecondaryMediation);
        }
    }
}
