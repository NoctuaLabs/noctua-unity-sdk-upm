using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace com.noctuagames.sdk.Tests
{
    /// <summary>
    /// Unit and integration tests for GlobalConfig JSON deserialization.
    ///
    /// Covers:
    /// - Required and optional field parsing
    /// - Default URL fallbacks (BaseUrl, TrackerUrl)
    /// - Sandbox URL override when IsSandbox = true
    /// - UTF-8 BOM stripping before deserialization
    /// - IAA section including cpm_floors and ad_experiments
    /// - The actual noctuagg.json file (integration smoke test)
    /// </summary>
    [TestFixture]
    public class ConfigLoadTest
    {
        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Mimics the post-load defaults applied in Noctua.Initialization.cs constructor.
        /// </summary>
        private static GlobalConfig ParseAndApplyDefaults(string json)
        {
            var config = JsonConvert.DeserializeObject<GlobalConfig>(json);
            if (config == null) return null;

            config.Noctua ??= new NoctuaConfig();
            config.Adjust  ??= new AdjustConfig();

            if (string.IsNullOrEmpty(config.Noctua.BaseUrl))
                config.Noctua.BaseUrl = NoctuaConfig.DefaultBaseUrl;

            if (string.IsNullOrEmpty(config.Noctua.TrackerUrl))
                config.Noctua.TrackerUrl = NoctuaConfig.DefaultTrackerUrl;

            if (config.Noctua.IsSandbox)
                config.Noctua.BaseUrl = NoctuaConfig.DefaultSandboxBaseUrl;

            return config;
        }

        /// <summary>
        /// Strips a UTF-8 BOM prefix from bytes (matches the logic in Noctua.Initialization.cs).
        /// </summary>
        private static string StripBomAndDecode(byte[] bytes)
        {
            ReadOnlySpan<byte> raw = bytes;
            if (Encoding.UTF8.Preamble.SequenceEqual(raw[..3]))
                raw = raw[3..];
            return Encoding.UTF8.GetString(raw);
        }

        private const string MinimalValidJson = @"{
            ""clientId"": ""test-client-id"",
            ""noctua"": {}
        }";

        // ── JSON deserialization: required fields ─────────────────────────────────

        [Test]
        public void Parse_ValidMinimalJson_ClientIdPresent()
        {
            var config = ParseAndApplyDefaults(MinimalValidJson);

            Assert.IsNotNull(config);
            Assert.AreEqual("test-client-id", config.ClientId);
        }

        [Test]
        public void Parse_MissingOptionalSections_NullFields()
        {
            var config = ParseAndApplyDefaults(MinimalValidJson);

            Assert.IsNull(config.Adjust?.AppToken,    "Adjust should be null when absent");
            Assert.IsNull(config.Facebook,             "Facebook should be null when absent");
            Assert.IsNull(config.Firebase,             "Firebase should be null when absent");
            Assert.IsNull(config.IAA,                  "IAA should be null when absent");
        }

        [Test]
        public void Parse_GameIdPresent_ParsedAsLong()
        {
            const string json = @"{
                ""clientId"": ""cid"",
                ""gameId"": 42,
                ""noctua"": {}
            }";

            var config = ParseAndApplyDefaults(json);
            Assert.AreEqual(42L, config.GameID);
        }

        [Test]
        public void Parse_NullJson_ReturnsNull()
        {
            var config = JsonConvert.DeserializeObject<GlobalConfig>("null");
            Assert.IsNull(config);
        }

        [Test]
        public void Parse_InvalidJson_ThrowsException()
        {
            Assert.Throws<JsonException>(() =>
                JsonConvert.DeserializeObject<GlobalConfig>("{ invalid json"));
        }

        // ── Post-load URL defaults ─────────────────────────────────────────────────

        [Test]
        public void Defaults_BaseUrl_AbsentInJson_UsesDefault()
        {
            const string json = @"{ ""clientId"": ""cid"", ""noctua"": {} }";

            var config = ParseAndApplyDefaults(json);

            Assert.AreEqual(NoctuaConfig.DefaultBaseUrl, config.Noctua.BaseUrl);
        }

        [Test]
        public void Defaults_TrackerUrl_AbsentInJson_UsesDefault()
        {
            const string json = @"{ ""clientId"": ""cid"", ""noctua"": {} }";

            var config = ParseAndApplyDefaults(json);

            Assert.AreEqual(NoctuaConfig.DefaultTrackerUrl, config.Noctua.TrackerUrl);
        }

        [Test]
        public void Defaults_BaseUrl_PresentInJson_Preserved()
        {
            const string json = @"{
                ""clientId"": ""cid"",
                ""noctua"": { ""baseUrl"": ""https://custom.example.com/api"" }
            }";

            var config = ParseAndApplyDefaults(json);

            Assert.AreEqual("https://custom.example.com/api", config.Noctua.BaseUrl);
        }

        [Test]
        public void Defaults_SandboxEnabled_True_OverridesBaseUrlWithSandboxUrl()
        {
            const string json = @"{
                ""clientId"": ""cid"",
                ""noctua"": { ""sandboxEnabled"": true }
            }";

            var config = ParseAndApplyDefaults(json);

            Assert.AreEqual(NoctuaConfig.DefaultSandboxBaseUrl, config.Noctua.BaseUrl,
                "IsSandbox=true must always redirect BaseUrl to the sandbox endpoint");
        }

        [Test]
        public void Defaults_SandboxEnabled_False_UsesProductionUrl()
        {
            const string json = @"{
                ""clientId"": ""cid"",
                ""noctua"": { ""sandboxEnabled"": false }
            }";

            var config = ParseAndApplyDefaults(json);

            Assert.AreEqual(NoctuaConfig.DefaultBaseUrl, config.Noctua.BaseUrl);
            Assert.AreNotEqual(NoctuaConfig.DefaultSandboxBaseUrl, config.Noctua.BaseUrl);
        }

        [Test]
        public void Defaults_SandboxEnabled_OverridesCustomBaseUrl()
        {
            // Even if a custom baseUrl is set, sandbox=true must win
            const string json = @"{
                ""clientId"": ""cid"",
                ""noctua"": {
                    ""baseUrl"": ""https://custom.example.com/api"",
                    ""sandboxEnabled"": true
                }
            }";

            var config = ParseAndApplyDefaults(json);

            Assert.AreEqual(NoctuaConfig.DefaultSandboxBaseUrl, config.Noctua.BaseUrl,
                "sandboxEnabled must override any custom baseUrl");
        }

        // ── Feature flags ──────────────────────────────────────────────────────────

        [Test]
        public void Parse_FeatureFlags_ParsedCorrectly()
        {
            const string json = @"{
                ""clientId"": ""cid"",
                ""noctua"": {
                    ""iaaEnabled"": true,
                    ""iapDisabled"": false,
                    ""offlineFirstEnabled"": true,
                    ""welcomeToastDisabled"": true
                }
            }";

            var config = ParseAndApplyDefaults(json);

            Assert.IsTrue(config.Noctua.isIAAEnabled);
            Assert.IsFalse(config.Noctua.isIAPDisabled);
            Assert.IsTrue(config.Noctua.IsOfflineFirst);
            Assert.IsTrue(config.Noctua.welcomeToastDisabled);
        }

        [Test]
        public void Parse_RemoteFeatureFlags_ParsedAsDictionary()
        {
            const string json = @"{
                ""clientId"": ""cid"",
                ""noctua"": {
                    ""remoteFeatureFlags"": {
                        ""vnLegalPurposeEnabled"": true,
                        ""ssoDisabled"": false
                    }
                }
            }";

            var config = ParseAndApplyDefaults(json);

            Assert.IsNotNull(config.Noctua.RemoteFeatureFlags);
            Assert.IsTrue(config.Noctua.RemoteFeatureFlags["vnLegalPurposeEnabled"]);
            Assert.IsFalse(config.Noctua.RemoteFeatureFlags["ssoDisabled"]);
        }

        // ── UTF-8 BOM stripping ────────────────────────────────────────────────────

        [Test]
        public void BomStrip_FileWithBom_ParsedCorrectly()
        {
            const string rawJson = @"{ ""clientId"": ""bom-test"", ""noctua"": {} }";
            byte[] bom     = Encoding.UTF8.GetPreamble();   // EF BB BF
            byte[] content = Encoding.UTF8.GetBytes(rawJson);
            byte[] withBom = new byte[bom.Length + content.Length];
            Array.Copy(bom,     0, withBom, 0,           bom.Length);
            Array.Copy(content, 0, withBom, bom.Length, content.Length);

            string stripped = StripBomAndDecode(withBom);
            var config = ParseAndApplyDefaults(stripped);

            Assert.IsNotNull(config);
            Assert.AreEqual("bom-test", config.ClientId);
        }

        [Test]
        public void BomStrip_FileWithoutBom_ParsedCorrectly()
        {
            const string rawJson = @"{ ""clientId"": ""no-bom"", ""noctua"": {} }";
            byte[] noBom = Encoding.UTF8.GetBytes(rawJson);

            string decoded = StripBomAndDecode(noBom);
            var config = ParseAndApplyDefaults(decoded);

            Assert.AreEqual("no-bom", config.ClientId);
        }

        // ── IAA section deserialization ────────────────────────────────────────────

        [Test]
        public void Parse_IAASection_MediationAndNetworks()
        {
            const string json = @"{
                ""clientId"": ""cid"",
                ""noctua"": {},
                ""iaa"": {
                    ""mediation"": ""admob"",
                    ""secondary_mediation"": ""applovin""
                }
            }";

            var config = ParseAndApplyDefaults(json);

            Assert.IsNotNull(config.IAA);
            Assert.AreEqual("admob",    config.IAA.Mediation);
            Assert.AreEqual("applovin", config.IAA.SecondaryMediation);
        }

        [Test]
        public void Parse_IAASection_CpmFloors_ParsedCorrectly()
        {
            const string json = @"{
                ""clientId"": ""cid"",
                ""noctua"": {},
                ""iaa"": {
                    ""mediation"": ""admob"",
                    ""cpm_floors"": {
                        ""enabled"": true,
                        ""min_samples"": 10,
                        ""floors"": {
                            ""interstitial"": {
                                ""t1"": { ""soft"": 0.50, ""hard"": 0.20 },
                                ""t2"": { ""soft"": 0.20, ""hard"": 0.08 }
                            }
                        },
                        ""segment_overrides"": {}
                    }
                }
            }";

            var config = ParseAndApplyDefaults(json);

            Assert.IsNotNull(config.IAA.CpmFloors);
            Assert.IsTrue(config.IAA.CpmFloors.Enabled == true);
            Assert.AreEqual(10, config.IAA.CpmFloors.MinSamples);
            Assert.AreEqual(0.50, config.IAA.CpmFloors.Floors["interstitial"]["t1"].Soft, 0.001);
            Assert.AreEqual(0.20, config.IAA.CpmFloors.Floors["interstitial"]["t1"].Hard, 0.001);
            Assert.AreEqual(0.08, config.IAA.CpmFloors.Floors["interstitial"]["t2"].Hard, 0.001);
        }

        [Test]
        public void Parse_IAASection_CpmFloors_Disabled_ParsedCorrectly()
        {
            const string json = @"{
                ""clientId"": ""cid"",
                ""noctua"": {},
                ""iaa"": {
                    ""cpm_floors"": { ""enabled"": false }
                }
            }";

            var config = ParseAndApplyDefaults(json);

            Assert.IsFalse(config.IAA.CpmFloors.Enabled == true);
        }

        [Test]
        public void Parse_IAASection_AdExperiments_ParsedCorrectly()
        {
            const string json = @"{
                ""clientId"": ""cid"",
                ""noctua"": {},
                ""iaa"": {
                    ""ad_experiments"": [
                        {
                            ""experiment_id"": ""exp_test_q2"",
                            ""enabled"": true,
                            ""segment_filters"": [""t1"", ""t2""],
                            ""variants"": [
                                { ""variant_id"": ""control"",   ""weight"": 50, ""iaa_override"": null },
                                { ""variant_id"": ""high_cap"",  ""weight"": 50, ""iaa_override"": {
                                    ""cooldown_seconds"": { ""interstitial"": 10 }
                                }}
                            ]
                        }
                    ]
                }
            }";

            var config = ParseAndApplyDefaults(json);

            Assert.IsNotNull(config.IAA.AdExperiments);
            Assert.AreEqual(1, config.IAA.AdExperiments.Count);

            var exp = config.IAA.AdExperiments[0];
            Assert.AreEqual("exp_test_q2", exp.ExperimentId);
            Assert.IsTrue(exp.Enabled);
            Assert.AreEqual(2, exp.SegmentFilters.Count);
            Assert.AreEqual("t1", exp.SegmentFilters[0]);
            Assert.AreEqual(2, exp.Variants.Count);
            Assert.IsNull(exp.Variants[0].IaaOverride,  "Control variant must have null override");
            Assert.IsNotNull(exp.Variants[1].IaaOverride, "Treatment variant must have override");
            Assert.AreEqual(10, exp.Variants[1].IaaOverride.CooldownSeconds.Interstitial);
        }

        [Test]
        public void Parse_IAASection_MultipleAdExperiments_AllParsed()
        {
            const string json = @"{
                ""clientId"": ""cid"",
                ""noctua"": {},
                ""iaa"": {
                    ""ad_experiments"": [
                        { ""experiment_id"": ""exp_a"", ""enabled"": true,  ""variants"": [] },
                        { ""experiment_id"": ""exp_b"", ""enabled"": false, ""variants"": [] }
                    ]
                }
            }";

            var config = ParseAndApplyDefaults(json);

            Assert.AreEqual(2, config.IAA.AdExperiments.Count);
            Assert.AreEqual("exp_a", config.IAA.AdExperiments[0].ExperimentId);
            Assert.IsTrue(config.IAA.AdExperiments[0].Enabled);
            Assert.AreEqual("exp_b", config.IAA.AdExperiments[1].ExperimentId);
            Assert.IsFalse(config.IAA.AdExperiments[1].Enabled);
        }

        [Test]
        public void Parse_IAASection_FrequencyCapsAndCooldowns()
        {
            const string json = @"{
                ""clientId"": ""cid"",
                ""noctua"": {},
                ""iaa"": {
                    ""frequency_caps"": {
                        ""interstitial"": { ""max_impressions"": 10, ""window_seconds"": 3600 },
                        ""app_open"":     { ""max_impressions"": 3,  ""window_seconds"": 3600 }
                    },
                    ""cooldown_seconds"": {
                        ""interstitial"": 15,
                        ""app_open"":     30
                    }
                }
            }";

            var config = ParseAndApplyDefaults(json);

            Assert.AreEqual(10,   config.IAA.FrequencyCaps.Interstitial.MaxImpressions);
            Assert.AreEqual(3600, config.IAA.FrequencyCaps.Interstitial.WindowSeconds);
            Assert.AreEqual(3,    config.IAA.FrequencyCaps.AppOpen.MaxImpressions);
            Assert.AreEqual(15,   config.IAA.CooldownSeconds.Interstitial);
            Assert.AreEqual(30,   config.IAA.CooldownSeconds.AppOpen);
        }

        // ── noctuagg.json integration smoke tests ──────────────────────────────────

        [Test]
        public void ActualConfig_NoctuaggJson_ParsesWithoutError()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "noctuagg.json");
            Assume.That(File.Exists(path), $"noctuagg.json not found at: {path}");

            string json   = File.ReadAllText(path, Encoding.UTF8);
            var    config = ParseAndApplyDefaults(json);

            Assert.IsNotNull(config, "noctuagg.json must deserialize to a non-null GlobalConfig");
        }

        [Test]
        public void ActualConfig_NoctuaggJson_HasRequiredFields()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "noctuagg.json");
            Assume.That(File.Exists(path), $"noctuagg.json not found at: {path}");

            string json   = File.ReadAllText(path, Encoding.UTF8);
            var    config = ParseAndApplyDefaults(json);

            Assert.IsNotNull(config.ClientId,    "clientId is required");
            Assert.IsNotNull(config.Noctua,      "noctua section is required");
            Assert.IsNotEmpty(config.Noctua.BaseUrl,    "BaseUrl must be set after defaults applied");
            Assert.IsNotEmpty(config.Noctua.TrackerUrl, "TrackerUrl must be set after defaults applied");
        }

        [Test]
        public void ActualConfig_NoctuaggJson_IAASection_Valid()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "noctuagg.json");
            Assume.That(File.Exists(path), $"noctuagg.json not found at: {path}");

            string json   = File.ReadAllText(path, Encoding.UTF8);
            var    config = ParseAndApplyDefaults(json);

            Assume.That(config.IAA != null, "IAA section not present in noctuagg.json — skipping");

            Assert.IsNotEmpty(config.IAA.Mediation, "IAA mediation must be set");
        }

        [Test]
        public void ActualConfig_NoctuaggJson_CpmFloors_Valid()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "noctuagg.json");
            Assume.That(File.Exists(path), $"noctuagg.json not found at: {path}");

            string json   = File.ReadAllText(path, Encoding.UTF8);
            var    config = ParseAndApplyDefaults(json);

            Assume.That(config.IAA?.CpmFloors != null, "cpm_floors not present — skipping");

            Assert.IsNotNull(config.IAA.CpmFloors.Floors,
                "cpm_floors.floors must not be null when section is present");
            Assert.Greater(config.IAA.CpmFloors.Floors.Count, 0,
                "At least one format floor must be configured");
            Assert.IsTrue(config.IAA.CpmFloors.MinSamples >= 0,
                "min_samples must be non-negative");
        }

        [Test]
        public void ActualConfig_NoctuaggJson_AdExperiments_AllHaveUniqueIds()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "noctuagg.json");
            Assume.That(File.Exists(path), $"noctuagg.json not found at: {path}");

            string json   = File.ReadAllText(path, Encoding.UTF8);
            var    config = ParseAndApplyDefaults(json);

            Assume.That(config.IAA?.AdExperiments != null && config.IAA.AdExperiments.Count > 0,
                "ad_experiments not present — skipping");

            var ids = new HashSet<string>();
            foreach (var exp in config.IAA.AdExperiments)
            {
                Assert.IsNotEmpty(exp.ExperimentId, "Every experiment must have a non-empty experiment_id");
                Assert.IsTrue(ids.Add(exp.ExperimentId),
                    $"Duplicate experiment_id found: '{exp.ExperimentId}'");
                Assert.IsNotNull(exp.Variants, $"Experiment '{exp.ExperimentId}' must have a variants array");
            }
        }

        [Test]
        public void ActualConfig_NoctuaggJson_AdExperiment_VariantWeightsSumTo100()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "noctuagg.json");
            Assume.That(File.Exists(path), $"noctuagg.json not found at: {path}");

            string json   = File.ReadAllText(path, Encoding.UTF8);
            var    config = ParseAndApplyDefaults(json);

            Assume.That(config.IAA?.AdExperiments != null && config.IAA.AdExperiments.Count > 0,
                "ad_experiments not present — skipping");

            foreach (var exp in config.IAA.AdExperiments)
            {
                if (exp.Variants == null || exp.Variants.Count == 0) continue;

                int total = 0;
                foreach (var v in exp.Variants) total += v.Weight;

                Assert.AreEqual(100, total,
                    $"Variant weights in experiment '{exp.ExperimentId}' must sum to 100, got {total}");
            }
        }
    }
}
