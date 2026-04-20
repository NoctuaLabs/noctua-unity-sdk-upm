using com.noctuagames.sdk;
using com.noctuagames.sdk.Events;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Tests.Runtime.Model
{
    /// <summary>Verifies DTO construction and JSON round-trips for small 0%-coverage model types.</summary>
    public class DtoTest
    {
        // ─── NoctuaAdjustAttribution ──────────────────────────────────────────

        [Test]
        public void AdjustAttribution_FromJson_PopulatesFields()
        {
            const string json = @"{
                ""trackerToken"": ""abc123"",
                ""trackerName"": ""MyTracker"",
                ""network"": ""Facebook"",
                ""campaign"": ""summer"",
                ""adgroup"": ""group1"",
                ""creative"": ""banner"",
                ""clickLabel"": ""lbl"",
                ""adid"": ""device-id"",
                ""costType"": ""CPI"",
                ""costAmount"": 0.42,
                ""costCurrency"": ""USD"",
                ""fbInstallReferrer"": ""ref""
            }";

            var attr = NoctuaAdjustAttribution.FromJson(json);

            Assert.AreEqual("abc123", attr.TrackerToken);
            Assert.AreEqual("Facebook", attr.Network);
            Assert.AreEqual("summer", attr.Campaign);
            Assert.AreEqual(0.42, attr.CostAmount, 0.0001);
            Assert.AreEqual("USD", attr.CostCurrency);
        }

        [Test]
        public void AdjustAttribution_FromJson_NullOrEmpty_ReturnsEmpty()
        {
            var a1 = NoctuaAdjustAttribution.FromJson(null);
            var a2 = NoctuaAdjustAttribution.FromJson("");
            var a3 = NoctuaAdjustAttribution.FromJson("{}");

            Assert.IsNotNull(a1);
            Assert.IsNull(a1.TrackerToken);
            Assert.IsNotNull(a2);
            Assert.IsNotNull(a3);
        }

        [Test]
        public void AdjustAttribution_FromJson_InvalidJson_ReturnsEmpty()
        {
            var attr = NoctuaAdjustAttribution.FromJson("not-json{{{");
            Assert.IsNotNull(attr);
            Assert.IsNull(attr.Network);
        }

        [Test]
        public void AdjustAttribution_FromJson_IgnoresUnknownFields()
        {
            const string json = @"{""trackerToken"":""tok"",""unknownField"":""ignored""}";
            var attr = NoctuaAdjustAttribution.FromJson(json);
            Assert.AreEqual("tok", attr.TrackerToken);
        }

        // ─── AppUpdateInfo ────────────────────────────────────────────────────

        [Test]
        public void AppUpdateInfo_DefaultValues_AllFalseOrZero()
        {
            var info = new AppUpdateInfo();
            Assert.IsFalse(info.IsUpdateAvailable);
            Assert.IsFalse(info.IsImmediateAllowed);
            Assert.IsFalse(info.IsFlexibleAllowed);
            Assert.AreEqual(0, info.AvailableVersionCode);
            Assert.AreEqual(0, info.StalenessDays);
        }

        [Test]
        public void AppUpdateInfo_PropertyAssignment_RoundTrips()
        {
            var info = new AppUpdateInfo
            {
                IsUpdateAvailable = true,
                IsImmediateAllowed = true,
                AvailableVersionCode = 42,
                StalenessDays = 7
            };

            Assert.IsTrue(info.IsUpdateAvailable);
            Assert.IsTrue(info.IsImmediateAllowed);
            Assert.AreEqual(42, info.AvailableVersionCode);
            Assert.AreEqual(7, info.StalenessDays);
        }

        [Test]
        public void AppUpdateResult_EnumValues_MatchExpected()
        {
            Assert.AreEqual(0, (int)AppUpdateResult.Success);
            Assert.AreEqual(1, (int)AppUpdateResult.UserCancelled);
            Assert.AreEqual(2, (int)AppUpdateResult.Failed);
            Assert.AreEqual(3, (int)AppUpdateResult.NotAvailable);
        }

        // ─── GeoIPData ────────────────────────────────────────────────────────

        [Test]
        public void GeoIPData_ShallowCopy_IsIndependent()
        {
            var original = new GeoIPData { Country = "ID", IpAddress = "1.2.3.4" };
            var copy = original.ShallowCopy();

            Assert.AreEqual("ID", copy.Country);
            Assert.AreEqual("1.2.3.4", copy.IpAddress);
            Assert.AreNotSame(original, copy);

            copy.Country = "SG";
            Assert.AreEqual("ID", original.Country, "original must not be mutated by copy change");
        }

        [Test]
        public void GeoIPData_JsonRoundTrip_PreservesFields()
        {
            var data = new GeoIPData { Country = "US", IpAddress = "8.8.8.8" };
            var json = JsonConvert.SerializeObject(data);
            var back = JsonConvert.DeserializeObject<GeoIPData>(json);

            Assert.AreEqual("US", back.Country);
            Assert.AreEqual("8.8.8.8", back.IpAddress);
        }
    }
}
