using System.Collections.Generic;
using com.noctuagames.sdk;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// Extended unit tests for <see cref="NoctuaConfig"/> covering all public constants,
    /// default field values, JSON serialisation keys, and round-trip fidelity for fields
    /// not yet exercised by <c>ModelSerializationExtendedTests</c>:
    ///   — the five remaining <c>Default*</c> URL constants
    ///   — <c>AnnouncementBaseUrl</c>, <c>RewardBaseUrl</c>, <c>SocialMediaBaseUrl</c>,
    ///     <c>CustomerServiceBaseUrl</c>
    ///   — <c>TrackerBatchPeriodMs</c>, <c>SessionHeartbeatPeriodMs</c>, <c>SessionTimeoutMs</c>
    ///   — all four client-side feature flags
    ///   — <c>Region</c> and <c>RemoteFeatureFlags</c>
    /// </summary>
    [TestFixture]
    public class NoctuaConfigExtendedTest
    {
        // ─── Default URL constants ────────────────────────────────────────────

        [Test]
        public void DefaultSandboxBaseUrl_IsHttpsAndContainsSandbox()
        {
            StringAssert.StartsWith("https://", NoctuaConfig.DefaultSandboxBaseUrl);
            StringAssert.Contains("sandbox", NoctuaConfig.DefaultSandboxBaseUrl);
        }

        [Test]
        public void DefaultAnnouncementBaseUrl_IsHttpsAndContainsAnnouncements()
        {
            StringAssert.StartsWith("https://", NoctuaConfig.DefaultAnnouncementBaseUrl);
            StringAssert.Contains("announcements", NoctuaConfig.DefaultAnnouncementBaseUrl);
        }

        [Test]
        public void DefaultRewardBaseUrl_IsHttpsAndContainsRewards()
        {
            StringAssert.StartsWith("https://", NoctuaConfig.DefaultRewardBaseUrl);
            StringAssert.Contains("rewards", NoctuaConfig.DefaultRewardBaseUrl);
        }

        [Test]
        public void DefaultCustomerServiceBaseUrl_IsHttpsAndContainsCs()
        {
            StringAssert.StartsWith("https://", NoctuaConfig.DefaultCustomerServiceBaseUrl);
            StringAssert.Contains("cs", NoctuaConfig.DefaultCustomerServiceBaseUrl);
        }

        [Test]
        public void DefaultSocialMediaBaseUrl_IsHttpsAndContainsSocialMedia()
        {
            StringAssert.StartsWith("https://", NoctuaConfig.DefaultSocialMediaBaseUrl);
            StringAssert.Contains("social-media", NoctuaConfig.DefaultSocialMediaBaseUrl);
        }

        // ─── Default instance field values ────────────────────────────────────

        [Test]
        public void NewInstance_AnnouncementBaseUrl_EqualsDefaultConstant()
        {
            var config = new NoctuaConfig();
            Assert.AreEqual(NoctuaConfig.DefaultAnnouncementBaseUrl, config.AnnouncementBaseUrl);
        }

        [Test]
        public void NewInstance_RewardBaseUrl_EqualsDefaultConstant()
        {
            var config = new NoctuaConfig();
            Assert.AreEqual(NoctuaConfig.DefaultRewardBaseUrl, config.RewardBaseUrl);
        }

        [Test]
        public void NewInstance_SocialMediaBaseUrl_EqualsDefaultConstant()
        {
            var config = new NoctuaConfig();
            Assert.AreEqual(NoctuaConfig.DefaultSocialMediaBaseUrl, config.SocialMediaBaseUrl);
        }

        [Test]
        public void NewInstance_CustomerServiceBaseUrl_EqualsDefaultConstant()
        {
            var config = new NoctuaConfig();
            Assert.AreEqual(NoctuaConfig.DefaultCustomerServiceBaseUrl, config.CustomerServiceBaseUrl);
        }

        [Test]
        public void NewInstance_TrackerBatchPeriodMs_Is60000()
        {
            var config = new NoctuaConfig();
            Assert.AreEqual(60_000u, config.TrackerBatchPeriodMs);
        }

        [Test]
        public void NewInstance_SessionHeartbeatPeriodMs_Is60000()
        {
            var config = new NoctuaConfig();
            Assert.AreEqual(60_000u, config.SessionHeartbeatPeriodMs);
        }

        [Test]
        public void NewInstance_SessionTimeoutMs_Is900000()
        {
            var config = new NoctuaConfig();
            Assert.AreEqual(900_000u, config.SessionTimeoutMs);
        }

        [Test]
        public void NewInstance_Region_IsNull()
        {
            var config = new NoctuaConfig();
            Assert.IsNull(config.Region);
        }

        [Test]
        public void NewInstance_RemoteFeatureFlags_IsNull()
        {
            var config = new NoctuaConfig();
            Assert.IsNull(config.RemoteFeatureFlags);
        }

        // ─── Feature flag defaults ─────────────────────────────────────────────

        [Test]
        public void NewInstance_WelcomeToastDisabled_IsFalse()
        {
            var config = new NoctuaConfig();
            Assert.IsFalse(config.welcomeToastDisabled);
        }

        [Test]
        public void NewInstance_IsIAAEnabled_IsFalse()
        {
            var config = new NoctuaConfig();
            Assert.IsFalse(config.isIAAEnabled);
        }

        [Test]
        public void NewInstance_IsIAPDisabled_IsFalse()
        {
            var config = new NoctuaConfig();
            Assert.IsFalse(config.isIAPDisabled);
        }

        [Test]
        public void NewInstance_IsOfflineFirst_IsFalse()
        {
            var config = new NoctuaConfig();
            Assert.IsFalse(config.IsOfflineFirst);
        }

        // ─── JSON round-trip — URL fields ─────────────────────────────────────

        [Test]
        public void JsonRoundTrip_AnnouncementBaseUrl_Preserved()
        {
            const string url = "https://custom.ann.test/api/v1/games/announcements";
            var config = new NoctuaConfig { AnnouncementBaseUrl = url };
            var back = JsonConvert.DeserializeObject<NoctuaConfig>(JsonConvert.SerializeObject(config));
            Assert.AreEqual(url, back.AnnouncementBaseUrl);
        }

        [Test]
        public void JsonRoundTrip_RewardBaseUrl_Preserved()
        {
            const string url = "https://custom.rewards.test/api/v1/games/rewards";
            var config = new NoctuaConfig { RewardBaseUrl = url };
            var back = JsonConvert.DeserializeObject<NoctuaConfig>(JsonConvert.SerializeObject(config));
            Assert.AreEqual(url, back.RewardBaseUrl);
        }

        [Test]
        public void JsonRoundTrip_SocialMediaBaseUrl_Preserved()
        {
            const string url = "https://custom.social.test/api/v1/games/social-media";
            var config = new NoctuaConfig { SocialMediaBaseUrl = url };
            var back = JsonConvert.DeserializeObject<NoctuaConfig>(JsonConvert.SerializeObject(config));
            Assert.AreEqual(url, back.SocialMediaBaseUrl);
        }

        [Test]
        public void JsonRoundTrip_CustomerServiceBaseUrl_Preserved()
        {
            const string url = "https://custom.cs.test/api/v1/games/cs";
            var config = new NoctuaConfig { CustomerServiceBaseUrl = url };
            var back = JsonConvert.DeserializeObject<NoctuaConfig>(JsonConvert.SerializeObject(config));
            Assert.AreEqual(url, back.CustomerServiceBaseUrl);
        }

        // ─── JSON round-trip — numeric / timing fields ─────────────────────────

        [Test]
        public void JsonRoundTrip_TrackerBatchPeriodMs_Preserved()
        {
            var config = new NoctuaConfig { TrackerBatchPeriodMs = 30_000u };
            var back = JsonConvert.DeserializeObject<NoctuaConfig>(JsonConvert.SerializeObject(config));
            Assert.AreEqual(30_000u, back.TrackerBatchPeriodMs);
        }

        [Test]
        public void JsonRoundTrip_SessionHeartbeatPeriodMs_Preserved()
        {
            var config = new NoctuaConfig { SessionHeartbeatPeriodMs = 120_000u };
            var back = JsonConvert.DeserializeObject<NoctuaConfig>(JsonConvert.SerializeObject(config));
            Assert.AreEqual(120_000u, back.SessionHeartbeatPeriodMs);
        }

        [Test]
        public void JsonRoundTrip_SessionTimeoutMs_Preserved()
        {
            var config = new NoctuaConfig { SessionTimeoutMs = 1_800_000u };
            var back = JsonConvert.DeserializeObject<NoctuaConfig>(JsonConvert.SerializeObject(config));
            Assert.AreEqual(1_800_000u, back.SessionTimeoutMs);
        }

        // ─── JSON round-trip — region and feature flags ───────────────────────

        [Test]
        public void JsonRoundTrip_Region_Preserved()
        {
            var config = new NoctuaConfig { Region = "ID" };
            var back = JsonConvert.DeserializeObject<NoctuaConfig>(JsonConvert.SerializeObject(config));
            Assert.AreEqual("ID", back.Region);
        }

        [Test]
        public void JsonRoundTrip_WelcomeToastDisabled_True_Preserved()
        {
            var config = new NoctuaConfig { welcomeToastDisabled = true };
            var back = JsonConvert.DeserializeObject<NoctuaConfig>(JsonConvert.SerializeObject(config));
            Assert.IsTrue(back.welcomeToastDisabled);
        }

        [Test]
        public void JsonRoundTrip_IsIAAEnabled_True_Preserved()
        {
            var config = new NoctuaConfig { isIAAEnabled = true };
            var back = JsonConvert.DeserializeObject<NoctuaConfig>(JsonConvert.SerializeObject(config));
            Assert.IsTrue(back.isIAAEnabled);
        }

        [Test]
        public void JsonRoundTrip_IsIAPDisabled_True_Preserved()
        {
            var config = new NoctuaConfig { isIAPDisabled = true };
            var back = JsonConvert.DeserializeObject<NoctuaConfig>(JsonConvert.SerializeObject(config));
            Assert.IsTrue(back.isIAPDisabled);
        }

        [Test]
        public void JsonRoundTrip_IsOfflineFirst_True_Preserved()
        {
            var config = new NoctuaConfig { IsOfflineFirst = true };
            var back = JsonConvert.DeserializeObject<NoctuaConfig>(JsonConvert.SerializeObject(config));
            Assert.IsTrue(back.IsOfflineFirst);
        }

        // ─── JSON round-trip — RemoteFeatureFlags ────────────────────────────

        [Test]
        public void JsonRoundTrip_RemoteFeatureFlags_NonNull_Preserved()
        {
            var flags = new Dictionary<string, bool> { { "newUi", true }, { "legacyFlow", false } };
            var config = new NoctuaConfig { RemoteFeatureFlags = flags };
            var back = JsonConvert.DeserializeObject<NoctuaConfig>(JsonConvert.SerializeObject(config));

            Assert.IsNotNull(back.RemoteFeatureFlags);
            Assert.AreEqual(2, back.RemoteFeatureFlags.Count);
            Assert.IsTrue(back.RemoteFeatureFlags["newUi"]);
            Assert.IsFalse(back.RemoteFeatureFlags["legacyFlow"]);
        }

        [Test]
        public void JsonRoundTrip_RemoteFeatureFlags_Empty_PreservesEmptyDict()
        {
            var config = new NoctuaConfig { RemoteFeatureFlags = new Dictionary<string, bool>() };
            var back = JsonConvert.DeserializeObject<NoctuaConfig>(JsonConvert.SerializeObject(config));

            Assert.IsNotNull(back.RemoteFeatureFlags);
            Assert.AreEqual(0, back.RemoteFeatureFlags.Count);
        }

        // ─── JSON key names ───────────────────────────────────────────────────

        [Test]
        public void Json_AnnouncementBaseUrl_UsesSnakeCaseKey()
        {
            var json = JsonConvert.SerializeObject(new NoctuaConfig());
            StringAssert.Contains("\"announcementBaseUrl\"", json);
        }

        [Test]
        public void Json_RewardBaseUrl_UsesSnakeCaseKey()
        {
            var json = JsonConvert.SerializeObject(new NoctuaConfig());
            StringAssert.Contains("\"rewardBaseUrl\"", json);
        }

        [Test]
        public void Json_WelcomeToastDisabled_UsesCorrectJsonKey()
        {
            var json = JsonConvert.SerializeObject(new NoctuaConfig());
            StringAssert.Contains("\"welcomeToastDisabled\"", json);
        }

        [Test]
        public void Json_IaaEnabled_UsesCorrectJsonKey()
        {
            var json = JsonConvert.SerializeObject(new NoctuaConfig());
            StringAssert.Contains("\"iaaEnabled\"", json);
        }

        [Test]
        public void Json_IapDisabled_UsesCorrectJsonKey()
        {
            var json = JsonConvert.SerializeObject(new NoctuaConfig());
            StringAssert.Contains("\"iapDisabled\"", json);
        }

        [Test]
        public void Json_OfflineFirstEnabled_UsesCorrectJsonKey()
        {
            var json = JsonConvert.SerializeObject(new NoctuaConfig());
            StringAssert.Contains("\"offlineFirstEnabled\"", json);
        }

        // ─── Deserialization from noctuagg.json-style snippet ─────────────────

        [Test]
        public void Deserialize_JsonSnippet_SetsAllFields()
        {
            const string json = @"{
                ""trackerUrl"":              ""https://t.test/api/v1"",
                ""baseUrl"":                ""https://b.test/api/v1"",
                ""announcementBaseUrl"":     ""https://ann.test/api"",
                ""rewardBaseUrl"":           ""https://rew.test/api"",
                ""socialMediaBaseUrl"":      ""https://soc.test/api"",
                ""customerServiceBaseUrl"":  ""https://cs.test/api"",
                ""trackerBatchSize"":        50,
                ""trackerBatchPeriodMs"":    30000,
                ""sessionHeartbeatPeriodMs"":45000,
                ""sessionTimeoutMs"":        600000,
                ""sandboxEnabled"":          true,
                ""region"":                  ""VN"",
                ""welcomeToastDisabled"":    true,
                ""iaaEnabled"":             true,
                ""iapDisabled"":            true,
                ""offlineFirstEnabled"":    true,
                ""remoteFeatureFlags"":     { ""ff_a"": true }
            }";

            var config = JsonConvert.DeserializeObject<NoctuaConfig>(json);

            Assert.AreEqual("https://t.test/api/v1",   config.TrackerUrl);
            Assert.AreEqual("https://b.test/api/v1",   config.BaseUrl);
            Assert.AreEqual("https://ann.test/api",    config.AnnouncementBaseUrl);
            Assert.AreEqual("https://rew.test/api",    config.RewardBaseUrl);
            Assert.AreEqual("https://soc.test/api",    config.SocialMediaBaseUrl);
            Assert.AreEqual("https://cs.test/api",     config.CustomerServiceBaseUrl);
            Assert.AreEqual(50u,                        config.TrackerBatchSize);
            Assert.AreEqual(30_000u,                    config.TrackerBatchPeriodMs);
            Assert.AreEqual(45_000u,                    config.SessionHeartbeatPeriodMs);
            Assert.AreEqual(600_000u,                   config.SessionTimeoutMs);
            Assert.IsTrue(config.IsSandbox);
            Assert.AreEqual("VN",                      config.Region);
            Assert.IsTrue(config.welcomeToastDisabled);
            Assert.IsTrue(config.isIAAEnabled);
            Assert.IsTrue(config.isIAPDisabled);
            Assert.IsTrue(config.IsOfflineFirst);
            Assert.IsNotNull(config.RemoteFeatureFlags);
            Assert.IsTrue(config.RemoteFeatureFlags["ff_a"]);
        }
    }
}
