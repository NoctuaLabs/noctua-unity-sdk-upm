using System.Linq;
using com.noctuagames.sdk;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// Unit tests for configuration DTOs and the <see cref="CountryData"/> lookup table:
    ///   * <see cref="CountryData"/> / <see cref="Country"/> — list sanity, well-known entries
    ///   * <see cref="AdjustConfig"/> / <see cref="AdjustAndroidConfig"/> / <see cref="AdjustIosConfig"/>
    ///     — JSON round-trip, default values
    ///   * <see cref="FacebookConfig"/> / <see cref="FacebookAndroidConfig"/> / <see cref="FacebookIosConfig"/>
    ///     — JSON round-trip
    ///   * <see cref="GlobalConfig"/> — JSON deserialization with nested sections
    ///   * <see cref="CoPublisherConfig"/> — JSON round-trip
    ///   * <see cref="ProductPurchaseStatus"/> — field access and default values
    /// </summary>
    [TestFixture]
    public class ConfigAndCountryDataTest
    {
        // ─── CountryData ──────────────────────────────────────────────────────

        [Test]
        public void CountryData_Countries_IsNotNull()
        {
            Assert.IsNotNull(CountryData.Countries);
        }

        [Test]
        public void CountryData_Countries_HasAtLeast100Entries()
        {
            Assert.GreaterOrEqual(CountryData.Countries.Count, 100,
                "Should contain at least 100 countries");
        }

        [Test]
        public void CountryData_Countries_AllHaveNonEmptyFields()
        {
            foreach (var c in CountryData.Countries)
            {
                Assert.IsNotNull(c.Code,      $"Country Code should not be null");
                Assert.IsNotNull(c.Name,      $"Country Name should not be null");
                Assert.IsNotNull(c.PhoneCode, $"Country PhoneCode should not be null");
                Assert.IsNotEmpty(c.Code,     $"Country Code should not be empty");
                Assert.IsNotEmpty(c.Name,     $"Country Name should not be empty");
                Assert.IsNotEmpty(c.PhoneCode,$"Country PhoneCode should not be empty");
            }
        }

        [Test]
        public void CountryData_Countries_AllPhoneCodesStartWithPlus()
        {
            foreach (var c in CountryData.Countries)
            {
                StringAssert.StartsWith("+", c.PhoneCode,
                    $"PhoneCode for {c.Code} ({c.Name}) should start with '+'");
            }
        }

        [Test]
        public void CountryData_Countries_ContainsIndonesia()
        {
            var id = CountryData.Countries.FirstOrDefault(c => c.Code == "ID");
            Assert.IsNotNull(id, "Indonesia (ID) should be in the list");
            Assert.AreEqual("Indonesia", id.Name);
            Assert.AreEqual("+62", id.PhoneCode);
        }

        [Test]
        public void CountryData_Countries_ContainsSingapore()
        {
            var sg = CountryData.Countries.FirstOrDefault(c => c.Code == "SG");
            Assert.IsNotNull(sg, "Singapore (SG) should be in the list");
            Assert.AreEqual("+65", sg.PhoneCode);
        }

        [Test]
        public void CountryData_Countries_ContainsThailand()
        {
            var th = CountryData.Countries.FirstOrDefault(c => c.Code == "TH");
            Assert.IsNotNull(th, "Thailand (TH) should be in the list");
            Assert.AreEqual("+66", th.PhoneCode);
        }

        [Test]
        public void CountryData_Countries_ContainsUnitedStates()
        {
            var us = CountryData.Countries.FirstOrDefault(c => c.Code == "US");
            Assert.IsNotNull(us, "United States (US) should be in the list");
            Assert.AreEqual("+1", us.PhoneCode);
        }

        [Test]
        public void Country_Constructor_SetsAllFields()
        {
            var country = new Country("MY", "Malaysia", "+60");

            Assert.AreEqual("MY",       country.Code);
            Assert.AreEqual("Malaysia", country.Name);
            Assert.AreEqual("+60",      country.PhoneCode);
        }

        [Test]
        public void CountryData_Countries_NoDuplicateCodes()
        {
            var codes = CountryData.Countries.Select(c => c.Code).ToList();
            var uniqueCodes = codes.Distinct().Count();
            Assert.AreEqual(codes.Count, uniqueCodes, "No duplicate country codes should exist");
        }

        // ─── AdjustConfig ─────────────────────────────────────────────────────

        [Test]
        public void AdjustAndroidConfig_DefaultEnvironment_IsSandbox()
        {
            var cfg = new AdjustAndroidConfig { AppToken = "abc" };
            Assert.AreEqual("sandbox", cfg.Environment);
        }

        [Test]
        public void AdjustIosConfig_DefaultEnvironment_IsSandbox()
        {
            var cfg = new AdjustIosConfig { AppToken = "xyz" };
            Assert.AreEqual("sandbox", cfg.Environment);
        }

        [Test]
        public void AdjustAndroidConfig_DefaultEventMap_IsEmpty()
        {
            var cfg = new AdjustAndroidConfig { AppToken = "abc" };
            Assert.IsNotNull(cfg.EventMap);
            Assert.AreEqual(0, cfg.EventMap.Count);
        }

        [Test]
        public void AdjustConfig_JsonRoundTrip_PreservesTokens()
        {
            var config = new AdjustConfig
            {
                Android = new AdjustAndroidConfig { AppToken = "android-abc", Environment = "production" },
                Ios     = new AdjustIosConfig     { AppToken = "ios-xyz",     Environment = "production" }
            };

            var json = JsonConvert.SerializeObject(config);
            var back = JsonConvert.DeserializeObject<AdjustConfig>(json);

            Assert.AreEqual("android-abc", back.Android.AppToken);
            Assert.AreEqual("ios-xyz",     back.Ios.AppToken);
            Assert.AreEqual("production",  back.Android.Environment);
        }

        [Test]
        public void AdjustConfig_JsonRoundTrip_PreservesEventMap()
        {
            var config = new AdjustConfig
            {
                Android = new AdjustAndroidConfig
                {
                    AppToken = "tok",
                    EventMap = new System.Collections.Generic.Dictionary<string, string>
                    {
                        { "level_up", "adj_level_up_token" },
                        { "purchase", "adj_purchase_token" },
                    }
                },
                Ios = new AdjustIosConfig { AppToken = "ios" }
            };

            var json = JsonConvert.SerializeObject(config);
            var back = JsonConvert.DeserializeObject<AdjustConfig>(json);

            Assert.AreEqual(2, back.Android.EventMap.Count);
            Assert.AreEqual("adj_level_up_token", back.Android.EventMap["level_up"]);
        }

        // ─── FacebookConfig ───────────────────────────────────────────────────

        [Test]
        public void FacebookConfig_JsonRoundTrip_PreservesIds()
        {
            var config = new FacebookConfig
            {
                Android = new FacebookAndroidConfig { AppId = "123456789", ClientToken = "client_android" },
                Ios     = new FacebookIosConfig     { AppId = "987654321", ClientToken = "client_ios" }
            };

            var json = JsonConvert.SerializeObject(config);
            var back = JsonConvert.DeserializeObject<FacebookConfig>(json);

            Assert.AreEqual("123456789",     back.Android.AppId);
            Assert.AreEqual("client_android", back.Android.ClientToken);
            Assert.AreEqual("987654321",     back.Ios.AppId);
            Assert.AreEqual("client_ios",    back.Ios.ClientToken);
        }

        [Test]
        public void FacebookConfig_JsonKeys_AreCorrect()
        {
            var config = new FacebookConfig
            {
                Android = new FacebookAndroidConfig { AppId = "123", ClientToken = "tok" },
                Ios     = new FacebookIosConfig     { AppId = "456", ClientToken = "tok2" }
            };
            var json = JsonConvert.SerializeObject(config);

            StringAssert.Contains("\"android\"",     json);
            StringAssert.Contains("\"ios\"",         json);
            StringAssert.Contains("\"appId\"",       json);
            StringAssert.Contains("\"clientToken\"", json);
        }

        // ─── GlobalConfig ─────────────────────────────────────────────────────

        [Test]
        public void GlobalConfig_JsonDeserialization_ClientIdRequired()
        {
            const string json = @"{
                ""clientId"": ""game-client-123"",
                ""gameId"": 42,
                ""noctua"": { ""isSandbox"": true }
            }";

            var config = JsonConvert.DeserializeObject<GlobalConfig>(json);

            Assert.AreEqual("game-client-123", config.ClientId);
            Assert.AreEqual(42L,               config.GameID);
            Assert.IsTrue(config.Noctua.IsSandbox);
            Assert.IsNull(config.Adjust, "Adjust should be null when not in JSON");
        }

        [Test]
        public void GlobalConfig_DefaultGameId_IsZero()
        {
            var config = new GlobalConfig { ClientId = "test" };
            Assert.AreEqual(0L, config.GameID);
        }

        // ─── CoPublisherConfig ────────────────────────────────────────────────

        [Test]
        public void CoPublisherConfig_JsonRoundTrip_PreservesAllUrls()
        {
            var config = new CoPublisherConfig
            {
                CompanyName       = "Acme Games",
                CompanyWebsiteUrl = "https://acme.example.com",
                CompanyTermUrl    = "https://acme.example.com/terms",
                CompanyPrivacyUrl = "https://acme.example.com/privacy",
            };

            var json = JsonConvert.SerializeObject(config);
            var back = JsonConvert.DeserializeObject<CoPublisherConfig>(json);

            Assert.AreEqual("Acme Games",                    back.CompanyName);
            Assert.AreEqual("https://acme.example.com",      back.CompanyWebsiteUrl);
            Assert.AreEqual("https://acme.example.com/terms",  back.CompanyTermUrl);
            Assert.AreEqual("https://acme.example.com/privacy", back.CompanyPrivacyUrl);
        }

        // ─── ProductPurchaseStatus ────────────────────────────────────────────

        [Test]
        public void ProductPurchaseStatus_DefaultCtor_AllFalseNullAndZero()
        {
            var status = new ProductPurchaseStatus();

            Assert.IsNull(status.ProductId);
            Assert.IsFalse(status.IsPurchased);
            Assert.IsFalse(status.IsAcknowledged);
            Assert.IsFalse(status.IsAutoRenewing);
            Assert.AreEqual(0,    status.PurchaseState);
            Assert.IsNull(status.PurchaseToken);
            Assert.AreEqual(0L,   status.PurchaseTime);
            Assert.AreEqual(0L,   status.ExpiryTime);
            Assert.IsNull(status.OrderId);
            Assert.IsNull(status.OriginalJson);
            Assert.IsNull(status.TransactionJson);
        }

        [Test]
        public void ProductPurchaseStatus_Fields_SetAndGet()
        {
            var status = new ProductPurchaseStatus
            {
                ProductId      = "com.example.gold100",
                IsPurchased    = true,
                IsAcknowledged = true,
                IsAutoRenewing = false,
                PurchaseState  = 1,
                PurchaseToken  = "purchase-token-abc",
                PurchaseTime   = 1700000000000L,
                ExpiryTime     = 0L,
                OrderId        = "GPA.1234-5678",
                OriginalJson   = "{\"purchaseToken\":\"abc\"}",
                TransactionJson = "",
            };

            Assert.AreEqual("com.example.gold100",       status.ProductId);
            Assert.IsTrue(status.IsPurchased);
            Assert.IsTrue(status.IsAcknowledged);
            Assert.IsFalse(status.IsAutoRenewing);
            Assert.AreEqual(1,                           status.PurchaseState);
            Assert.AreEqual("purchase-token-abc",        status.PurchaseToken);
            Assert.AreEqual(1700000000000L,              status.PurchaseTime);
            Assert.AreEqual(0L,                          status.ExpiryTime);
            Assert.AreEqual("GPA.1234-5678",             status.OrderId);
            Assert.AreEqual("{\"purchaseToken\":\"abc\"}", status.OriginalJson);
        }
    }
}
