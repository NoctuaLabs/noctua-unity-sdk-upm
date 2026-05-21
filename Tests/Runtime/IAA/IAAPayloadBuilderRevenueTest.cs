using System.Collections.Generic;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime.IAA
{
    /// <summary>
    /// Unit tests for the revenue-specific payload builders added in Plan 1.
    /// Verifies exact key sets — no device/SDK context required (pure, thread-free helpers).
    ///
    /// Note: BuildAdImpression (10-key) is covered by IAAPayloadBuilderTest and is not
    /// re-tested here.
    /// </summary>
    [TestFixture]
    public class IAAPayloadBuilderRevenueTest
    {
#if UNITY_APPLOVIN
        [Test]
        public void BuildAppLovinRevenuePayload_ContainsExactKeySet()
        {
            var payload = IAAPayloadBuilder.BuildAppLovinRevenuePayload(
                adInfo:      null,
                deviceId:    "test-device-id",
                countryCode: "US"
            );

            var expected = new HashSet<string>
            {
                "country_code",
                "network_name",
                "ad_unit_identifier",
                "placement",
                "network_placement",
                "revenue_precision",
                "ad_format",
                "dsp_name",
                "ad_user_id",
            };

            Assert.AreEqual(expected.Count, payload.Count,
                "BuildAppLovinRevenuePayload must produce exactly " + expected.Count + " keys");

            foreach (var key in expected)
                Assert.IsTrue(payload.ContainsKey(key), $"Missing expected key: '{key}'");
        }

        [Test]
        public void BuildAppLovinRevenuePayload_DeviceId_MappedToAdUserId()
        {
            const string deviceId = "device-abc-123";
            var payload = IAAPayloadBuilder.BuildAppLovinRevenuePayload(null, deviceId, "SG");
            Assert.AreEqual(deviceId, payload["ad_user_id"].ToString());
        }

        [Test]
        public void BuildAppLovinRevenuePayload_CountryCode_Preserved()
        {
            var payload = IAAPayloadBuilder.BuildAppLovinRevenuePayload(null, "", "JP");
            Assert.AreEqual("JP", payload["country_code"].ToString());
        }

        [Test]
        public void BuildAppLovinRevenuePayload_NullAdInfo_ReturnsEmptyStrings()
        {
            var payload = IAAPayloadBuilder.BuildAppLovinRevenuePayload(null, "", "");
            Assert.AreEqual("", payload["network_name"].ToString());
            Assert.AreEqual("", payload["ad_unit_identifier"].ToString());
            Assert.AreEqual("", payload["placement"].ToString());
            Assert.AreEqual("", payload["network_placement"].ToString());
            Assert.AreEqual("", payload["revenue_precision"].ToString());
            Assert.AreEqual("", payload["ad_format"].ToString());
            Assert.AreEqual("", payload["dsp_name"].ToString());
        }
#endif

#if UNITY_ADMOB
        [Test]
        public void BuildAdmobRevenuePayload_ContainsExactKeySet()
        {
            var payload = IAAPayloadBuilder.BuildAdmobRevenuePayload(
                adValue:      null,
                responseInfo: null,
                deviceId:     "test-device-id"
            );

            var expected = new HashSet<string>
            {
                "ad_source_id",
                "ad_source_instance_id",
                "ad_source_instance_name",
                "ad_source_name",
                "adapter_class_name",
                "latency_millis",
                "response_id",
                "mediation_group_name",
                "mediation_ab_test_name",
                "mediation_ab_test_variant",
                "ad_user_id",
            };

            Assert.AreEqual(expected.Count, payload.Count,
                "BuildAdmobRevenuePayload must produce exactly " + expected.Count + " keys");

            foreach (var key in expected)
                Assert.IsTrue(payload.ContainsKey(key), $"Missing expected key: '{key}'");
        }

        [Test]
        public void BuildAdmobRevenuePayload_DeviceId_MappedToAdUserId()
        {
            const string deviceId = "admob-device-xyz";
            var payload = IAAPayloadBuilder.BuildAdmobRevenuePayload(null, null, deviceId);
            Assert.AreEqual(deviceId, payload["ad_user_id"].ToString());
        }

        [Test]
        public void BuildAdmobRevenuePayload_NullResponseInfo_ReturnsEmptyPlaceholders()
        {
            var payload = IAAPayloadBuilder.BuildAdmobRevenuePayload(null, null, "");
            Assert.AreEqual("empty", payload["ad_source_id"].ToString());
            Assert.AreEqual("empty", payload["ad_source_name"].ToString());
            Assert.AreEqual("empty", payload["response_id"].ToString());
            Assert.AreEqual("empty", payload["mediation_group_name"].ToString());
            Assert.AreEqual("empty", payload["mediation_ab_test_name"].ToString());
            Assert.AreEqual("empty", payload["mediation_ab_test_variant"].ToString());
        }
#endif
    }
}
