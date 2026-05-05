using System;
using com.noctuagames.sdk;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// Unit tests for miscellaneous SDK model types:
    ///   * <see cref="NoctuaAdjustAttribution.FromJson"/> — null / empty / valid / invalid inputs
    ///   * <see cref="NativeEvent"/> — JSON round-trip via <see cref="RawJsonStringConverter"/>
    ///   * <see cref="RefundTrackingEntry"/> — JSON round-trip
    ///   * <see cref="AppUpdateInfo"/> — property accessors
    ///   * <see cref="AppUpdateResult"/> — enum ordinal values
    ///   * <see cref="FirebaseConfig"/> — JSON round-trip
    ///   * <see cref="HttpExchange"/> / <see cref="HttpExchangeState"/> — field access and enum values
    ///   * <see cref="NoctuaConsumableType"/> / <see cref="NoctuaProductType"/> — enum ordinal values
    /// </summary>
    [TestFixture]
    public class MiscModelTest
    {
        // ─── NoctuaAdjustAttribution.FromJson ─────────────────────────────────

        [Test]
        public void AdjustAttribution_FromJson_Null_ReturnsEmptyInstance()
        {
            var result = NoctuaAdjustAttribution.FromJson(null);
            Assert.IsNotNull(result);
            Assert.IsNull(result.TrackerToken);
            Assert.IsNull(result.Network);
        }

        [Test]
        public void AdjustAttribution_FromJson_EmptyString_ReturnsEmptyInstance()
        {
            var result = NoctuaAdjustAttribution.FromJson("");
            Assert.IsNotNull(result);
            Assert.IsNull(result.Network);
        }

        [Test]
        public void AdjustAttribution_FromJson_EmptyObject_ReturnsEmptyInstance()
        {
            var result = NoctuaAdjustAttribution.FromJson("{}");
            Assert.IsNotNull(result);
            Assert.IsNull(result.Network);
            Assert.IsNull(result.Campaign);
        }

        [Test]
        public void AdjustAttribution_FromJson_ValidJson_PopulatesFields()
        {
            const string json = @"{
                ""trackerToken"": ""abc123"",
                ""trackerName"": ""Organic"",
                ""network"": ""Facebook"",
                ""campaign"": ""campaign_01"",
                ""adgroup"": ""group_01"",
                ""creative"": ""creative_01"",
                ""clickLabel"": ""click_01"",
                ""adid"": ""device-adid"",
                ""costType"": ""CPI"",
                ""costAmount"": 0.45,
                ""costCurrency"": ""USD""
            }";

            var result = NoctuaAdjustAttribution.FromJson(json);

            Assert.AreEqual("abc123",      result.TrackerToken);
            Assert.AreEqual("Organic",     result.TrackerName);
            Assert.AreEqual("Facebook",    result.Network);
            Assert.AreEqual("campaign_01", result.Campaign);
            Assert.AreEqual("group_01",    result.Adgroup);
            Assert.AreEqual("creative_01", result.Creative);
            Assert.AreEqual("click_01",    result.ClickLabel);
            Assert.AreEqual("device-adid", result.Adid);
            Assert.AreEqual("CPI",         result.CostType);
            Assert.AreEqual(0.45,          result.CostAmount, delta: 0.0001);
            Assert.AreEqual("USD",         result.CostCurrency);
        }

        [Test]
        public void AdjustAttribution_FromJson_InvalidJson_ReturnsEmptyInstance()
        {
            var result = NoctuaAdjustAttribution.FromJson("not-valid-json{{");
            Assert.IsNotNull(result);
            Assert.IsNull(result.TrackerToken);
        }

        [Test]
        public void AdjustAttribution_FromJson_PartialJson_PopulatesAvailableFields()
        {
            const string json = @"{""network"": ""Google Ads""}";
            var result = NoctuaAdjustAttribution.FromJson(json);

            Assert.AreEqual("Google Ads", result.Network);
            Assert.IsNull(result.Campaign,  "Missing fields should remain null");
        }

        [Test]
        public void AdjustAttribution_FromJson_UnknownKeys_AreIgnored()
        {
            const string json = @"{""network"": ""TikTok"", ""unknown_field"": ""value""}";
            Assert.DoesNotThrow(() => NoctuaAdjustAttribution.FromJson(json));
            var result = NoctuaAdjustAttribution.FromJson(json);
            Assert.AreEqual("TikTok", result.Network);
        }

        // ─── NativeEvent ──────────────────────────────────────────────────────

        [Test]
        public void NativeEvent_FieldAccess_SetsAndGets()
        {
            var evt = new NativeEvent
            {
                Id        = 42L,
                EventJson = "{\"event_name\":\"level_up\"}",
                CreatedAt = 1700000000000L,
            };

            Assert.AreEqual(42L,                       evt.Id);
            Assert.AreEqual("{\"event_name\":\"level_up\"}", evt.EventJson);
            Assert.AreEqual(1700000000000L,            evt.CreatedAt);
        }

        [Test]
        public void NativeEvent_JsonDeserialization_InlinesEventJsonAsString()
        {
            // When the outer JSON contains eventJson as an inlined object, RawJsonStringConverter
            // captures it as a raw JSON string rather than deserializing it.
            const string outerJson = @"{
                ""id"": 7,
                ""eventJson"": {""event_name"": ""purchase"", ""revenue"": 9.99},
                ""createdAt"": 1234567890000
            }";

            var evt = JsonConvert.DeserializeObject<NativeEvent>(outerJson);

            Assert.AreEqual(7L,             evt.Id);
            Assert.AreEqual(1234567890000L, evt.CreatedAt);
            // EventJson should contain the raw inner JSON string
            Assert.IsNotNull(evt.EventJson);
            StringAssert.Contains("event_name", evt.EventJson);
            StringAssert.Contains("purchase",   evt.EventJson);
        }

        // ─── RefundTrackingEntry ──────────────────────────────────────────────

        [Test]
        public void RefundTrackingEntry_JsonRoundTrip_PreservesFields()
        {
            var timestamp = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
            var entry = new RefundTrackingEntry
            {
                ProductId   = "com.example.gold500",
                PaymentType = PaymentType.PlayStore,
                Timestamp   = timestamp,
            };

            var json = JsonConvert.SerializeObject(entry);
            var back = JsonConvert.DeserializeObject<RefundTrackingEntry>(json);

            Assert.AreEqual("com.example.gold500", back.ProductId);
            Assert.AreEqual(PaymentType.PlayStore,  back.PaymentType);
            Assert.AreEqual(timestamp,              back.Timestamp);
        }

        [Test]
        public void RefundTrackingEntry_JsonKeys_AreSnakeCase()
        {
            var entry = new RefundTrackingEntry { ProductId = "x", PaymentType = PaymentType.None };
            var json  = JsonConvert.SerializeObject(entry);

            StringAssert.Contains("\"product_id\"",   json);
            StringAssert.Contains("\"payment_type\"", json);
            StringAssert.Contains("\"timestamp\"",    json);
        }

        // ─── AppUpdateInfo ────────────────────────────────────────────────────

        [Test]
        public void AppUpdateInfo_Properties_RoundTrip()
        {
            var info = new AppUpdateInfo
            {
                IsUpdateAvailable   = true,
                IsImmediateAllowed  = true,
                IsFlexibleAllowed   = false,
                AvailableVersionCode = 210,
                StalenessDays        = 7,
            };

            Assert.IsTrue(info.IsUpdateAvailable);
            Assert.IsTrue(info.IsImmediateAllowed);
            Assert.IsFalse(info.IsFlexibleAllowed);
            Assert.AreEqual(210, info.AvailableVersionCode);
            Assert.AreEqual(7,   info.StalenessDays);
        }

        [Test]
        public void AppUpdateInfo_DefaultCtor_AllFalseAndZero()
        {
            var info = new AppUpdateInfo();

            Assert.IsFalse(info.IsUpdateAvailable);
            Assert.IsFalse(info.IsImmediateAllowed);
            Assert.IsFalse(info.IsFlexibleAllowed);
            Assert.AreEqual(0, info.AvailableVersionCode);
            Assert.AreEqual(0, info.StalenessDays);
        }

        // ─── AppUpdateResult enum ─────────────────────────────────────────────

        [Test]
        public void AppUpdateResult_Ordinals_AreCorrect()
        {
            Assert.AreEqual(0, (int)AppUpdateResult.Success);
            Assert.AreEqual(1, (int)AppUpdateResult.UserCancelled);
            Assert.AreEqual(2, (int)AppUpdateResult.Failed);
            Assert.AreEqual(3, (int)AppUpdateResult.NotAvailable);
        }

        [Test]
        public void AppUpdateResult_Count_IsFour()
        {
            Assert.AreEqual(4, Enum.GetValues(typeof(AppUpdateResult)).Length);
        }

        // ─── FirebaseConfig ───────────────────────────────────────────────────

        [Test]
        public void FirebaseConfig_JsonRoundTrip_PreservesFlags()
        {
            var config = new FirebaseConfig
            {
                Android = new FirebaseAndroidConfig { CustomEventDisabled = true  },
                Ios     = new FirebaseIosConfig     { CustomEventDisabled = false },
            };

            var json = JsonConvert.SerializeObject(config);
            var back = JsonConvert.DeserializeObject<FirebaseConfig>(json);

            Assert.IsTrue(back.Android.CustomEventDisabled);
            Assert.IsFalse(back.Ios.CustomEventDisabled);
        }

        [Test]
        public void FirebaseConfig_JsonKeys_AreCamelCase()
        {
            var config = new FirebaseConfig
            {
                Android = new FirebaseAndroidConfig { CustomEventDisabled = false },
                Ios     = new FirebaseIosConfig     { CustomEventDisabled = false },
            };
            var json = JsonConvert.SerializeObject(config);

            StringAssert.Contains("\"android\"",             json);
            StringAssert.Contains("\"ios\"",                 json);
            StringAssert.Contains("\"customEventDisabled\"", json);
        }

        // ─── HttpExchangeState enum ───────────────────────────────────────────

        [Test]
        public void HttpExchangeState_Ordinals_AreCorrect()
        {
            Assert.AreEqual(0, (int)HttpExchangeState.Building);
            Assert.AreEqual(1, (int)HttpExchangeState.Sending);
            Assert.AreEqual(2, (int)HttpExchangeState.Receiving);
            Assert.AreEqual(3, (int)HttpExchangeState.Complete);
            Assert.AreEqual(4, (int)HttpExchangeState.Failed);
            Assert.AreEqual(5, (int)HttpExchangeState.Aborted);
        }

        [Test]
        public void HttpExchangeState_Count_IsSix()
        {
            Assert.AreEqual(6, Enum.GetValues(typeof(HttpExchangeState)).Length);
        }

        // ─── HttpExchange ─────────────────────────────────────────────────────

        [Test]
        public void HttpExchange_DefaultCtor_InitializesHeaderDictionaries()
        {
            var exchange = new HttpExchange();

            // Request/response header dictionaries should be initialized (non-null)
            Assert.IsNotNull(exchange.RequestHeaders);
            Assert.IsNotNull(exchange.ResponseHeaders);
        }

        [Test]
        public void HttpExchange_Properties_SetAndGet()
        {
            var id    = Guid.NewGuid();
            var start = DateTime.UtcNow;

            var exchange = new HttpExchange
            {
                Id              = id,
                Method          = "POST",
                Url             = "https://api.example.com/track",
                RequestBody     = "{\"event\":\"level_up\"}",
                Status          = 200,
                ResponseBody    = "{\"success\":true}",
                StartUtc        = start,
                ElapsedMs       = 123L,
                Error           = null,
                State           = HttpExchangeState.Complete,
            };

            Assert.AreEqual(id,                           exchange.Id);
            Assert.AreEqual("POST",                       exchange.Method);
            Assert.AreEqual("https://api.example.com/track", exchange.Url);
            Assert.AreEqual("{\"event\":\"level_up\"}",   exchange.RequestBody);
            Assert.AreEqual(200,                          exchange.Status);
            Assert.AreEqual("{\"success\":true}",         exchange.ResponseBody);
            Assert.AreEqual(start,                        exchange.StartUtc);
            Assert.AreEqual(123L,                         exchange.ElapsedMs);
            Assert.IsNull(exchange.Error);
            Assert.AreEqual(HttpExchangeState.Complete,   exchange.State);
        }

        [Test]
        public void HttpExchange_ErrorState_HasErrorString()
        {
            var exchange = new HttpExchange
            {
                Method = "GET",
                Url    = "https://api.example.com/data",
                Status = 500,
                Error  = "Internal Server Error",
                State  = HttpExchangeState.Failed,
            };

            Assert.AreEqual(HttpExchangeState.Failed, exchange.State);
            Assert.AreEqual("Internal Server Error",  exchange.Error);
        }

        // ─── NoctuaConsumableType enum ────────────────────────────────────────

        [Test]
        public void NoctuaConsumableType_Ordinals_AreCorrect()
        {
            Assert.AreEqual(0, (int)NoctuaConsumableType.Consumable);
            Assert.AreEqual(1, (int)NoctuaConsumableType.NonConsumable);
            Assert.AreEqual(2, (int)NoctuaConsumableType.Subscription);
        }

        [Test]
        public void NoctuaConsumableType_Count_IsThree()
        {
            Assert.AreEqual(3, Enum.GetValues(typeof(NoctuaConsumableType)).Length);
        }

        // ─── NoctuaProductType enum ───────────────────────────────────────────

        [Test]
        public void NoctuaProductType_Ordinals_AreCorrect()
        {
            Assert.AreEqual(0, (int)NoctuaProductType.InApp);
            Assert.AreEqual(1, (int)NoctuaProductType.Subs);
        }

        [Test]
        public void NoctuaProductType_Count_IsTwo()
        {
            Assert.AreEqual(2, Enum.GetValues(typeof(NoctuaProductType)).Length);
        }

        // ─── GeoIPData ────────────────────────────────────────────────────────

        [Test]
        public void GeoIPData_DefaultValues_AreNull()
        {
            var geo = new GeoIPData();
            Assert.IsNull(geo.Country);
            Assert.IsNull(geo.IpAddress);
        }

        [Test]
        public void GeoIPData_FieldAssignment_RoundTrips()
        {
            var geo = new GeoIPData { Country = "ID", IpAddress = "1.2.3.4" };
            Assert.AreEqual("ID",      geo.Country);
            Assert.AreEqual("1.2.3.4", geo.IpAddress);
        }

        [Test]
        public void GeoIPData_ShallowCopy_ReturnsNewInstance()
        {
            var original = new GeoIPData { Country = "SG", IpAddress = "5.6.7.8" };
            var copy = original.ShallowCopy();

            Assert.AreNotSame(original, copy, "ShallowCopy must return a new object");
        }

        [Test]
        public void GeoIPData_ShallowCopy_PreservesFieldValues()
        {
            var original = new GeoIPData { Country = "VN", IpAddress = "10.0.0.1" };
            var copy = original.ShallowCopy();

            Assert.AreEqual(original.Country,   copy.Country);
            Assert.AreEqual(original.IpAddress, copy.IpAddress);
        }

        [Test]
        public void GeoIPData_ShallowCopy_IsIndependent()
        {
            var original = new GeoIPData { Country = "TH", IpAddress = "192.168.1.1" };
            var copy = original.ShallowCopy();
            copy.Country = "MY";

            // Mutating the copy must not affect the original
            Assert.AreEqual("TH", original.Country,
                "ShallowCopy must be independent — mutating copy must not affect original");
        }
    }
}
