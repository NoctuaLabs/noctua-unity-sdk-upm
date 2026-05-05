using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// Unit tests for <see cref="NoctuaAdjustAttribution.FromJson"/>:
    ///   — null / empty string / empty object  → returns a fresh (all-null) instance
    ///   — invalid JSON → returns a fresh instance (exception swallowed)
    ///   — valid full payload → all mapped fields populated
    ///   — partial payload → only matching fields populated, missing ones remain null / 0
    ///   — unknown JSON keys → silently ignored (MissingMemberHandling.Ignore)
    ///   — CostAmount (double) → round-tripped correctly
    /// </summary>
    [TestFixture]
    public class NoctuaAdjustAttributionTest
    {
        // ─── Guard cases — returns empty instance ────────────────────────────

        [Test]
        public void FromJson_Null_ReturnsNonNullInstance()
        {
            var result = NoctuaAdjustAttribution.FromJson(null);
            Assert.IsNotNull(result);
        }

        [Test]
        public void FromJson_Null_AllFieldsAreNull()
        {
            var result = NoctuaAdjustAttribution.FromJson(null);
            Assert.IsNull(result.TrackerToken);
            Assert.IsNull(result.TrackerName);
            Assert.IsNull(result.Network);
            Assert.IsNull(result.Campaign);
            Assert.IsNull(result.Adgroup);
            Assert.IsNull(result.Creative);
            Assert.IsNull(result.ClickLabel);
            Assert.IsNull(result.Adid);
            Assert.IsNull(result.CostType);
            Assert.AreEqual(0d, result.CostAmount);
            Assert.IsNull(result.CostCurrency);
            Assert.IsNull(result.FbInstallReferrer);
        }

        [Test]
        public void FromJson_EmptyString_ReturnsNonNullInstance()
        {
            var result = NoctuaAdjustAttribution.FromJson("");
            Assert.IsNotNull(result);
        }

        [Test]
        public void FromJson_EmptyString_AllStringFieldsAreNull()
        {
            var result = NoctuaAdjustAttribution.FromJson("");
            Assert.IsNull(result.TrackerToken);
            Assert.IsNull(result.Network);
        }

        [Test]
        public void FromJson_EmptyObject_ReturnsNonNullInstance()
        {
            var result = NoctuaAdjustAttribution.FromJson("{}");
            Assert.IsNotNull(result);
        }

        [Test]
        public void FromJson_EmptyObject_AllStringFieldsAreNull()
        {
            var result = NoctuaAdjustAttribution.FromJson("{}");
            Assert.IsNull(result.TrackerToken);
            Assert.IsNull(result.Network);
            Assert.IsNull(result.Campaign);
        }

        [Test]
        public void FromJson_InvalidJson_ReturnsNonNullInstance()
        {
            var result = NoctuaAdjustAttribution.FromJson("not-valid-json{{{");
            Assert.IsNotNull(result);
        }

        [Test]
        public void FromJson_InvalidJson_AllStringFieldsAreNull()
        {
            var result = NoctuaAdjustAttribution.FromJson("not-valid-json{{{");
            Assert.IsNull(result.TrackerToken);
            Assert.IsNull(result.Network);
        }

        [Test]
        public void FromJson_InvalidJson_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => NoctuaAdjustAttribution.FromJson("{invalid"));
        }

        // ─── Full payload ────────────────────────────────────────────────────

        [Test]
        public void FromJson_FullPayload_PopulatesAllStringFields()
        {
            const string json = @"{
                ""trackerToken"":     ""abc123"",
                ""trackerName"":      ""My Campaign - Facebook"",
                ""network"":          ""Facebook"",
                ""campaign"":         ""summer_sale"",
                ""adgroup"":          ""mobile_users"",
                ""creative"":         ""banner_v2"",
                ""clickLabel"":       ""click_lbl"",
                ""adid"":             ""device-adid-xyz"",
                ""costType"":         ""CPI"",
                ""costAmount"":       1.25,
                ""costCurrency"":     ""USD"",
                ""fbInstallReferrer"":""fb_ref_token""
            }";

            var result = NoctuaAdjustAttribution.FromJson(json);

            Assert.AreEqual("abc123",          result.TrackerToken);
            Assert.AreEqual("My Campaign - Facebook", result.TrackerName);
            Assert.AreEqual("Facebook",        result.Network);
            Assert.AreEqual("summer_sale",     result.Campaign);
            Assert.AreEqual("mobile_users",    result.Adgroup);
            Assert.AreEqual("banner_v2",       result.Creative);
            Assert.AreEqual("click_lbl",       result.ClickLabel);
            Assert.AreEqual("device-adid-xyz", result.Adid);
            Assert.AreEqual("CPI",             result.CostType);
            Assert.AreEqual(1.25d,             result.CostAmount, 1e-9);
            Assert.AreEqual("USD",             result.CostCurrency);
            Assert.AreEqual("fb_ref_token",    result.FbInstallReferrer);
        }

        // ─── Partial payload ─────────────────────────────────────────────────

        [Test]
        public void FromJson_PartialPayload_OnlyMappedFieldsPopulated()
        {
            const string json = @"{
                ""trackerToken"": ""tok-partial"",
                ""network"":      ""Google Ads""
            }";

            var result = NoctuaAdjustAttribution.FromJson(json);

            Assert.AreEqual("tok-partial", result.TrackerToken);
            Assert.AreEqual("Google Ads",  result.Network);
            Assert.IsNull(result.Campaign);
            Assert.IsNull(result.Adgroup);
            Assert.IsNull(result.Creative);
            Assert.IsNull(result.Adid);
            Assert.AreEqual(0d, result.CostAmount);
        }

        [Test]
        public void FromJson_OnlyCostAmount_OtherFieldsAreNull()
        {
            const string json = @"{ ""costAmount"": 2.99 }";

            var result = NoctuaAdjustAttribution.FromJson(json);

            Assert.AreEqual(2.99d, result.CostAmount, 1e-9);
            Assert.IsNull(result.TrackerToken);
            Assert.IsNull(result.Network);
            Assert.IsNull(result.CostCurrency);
        }

        // ─── Extra / unknown keys ─────────────────────────────────────────────

        [Test]
        public void FromJson_UnknownKeys_DoNotThrowAndAreIgnored()
        {
            const string json = @"{
                ""trackerToken"":   ""tok-extra"",
                ""unknownField123"": ""should-be-ignored"",
                ""anotherExtra"":    42
            }";

            NoctuaAdjustAttribution result = null;
            Assert.DoesNotThrow(() => result = NoctuaAdjustAttribution.FromJson(json));
            Assert.IsNotNull(result);
            Assert.AreEqual("tok-extra", result.TrackerToken);
        }

        // ─── CostAmount edge cases ────────────────────────────────────────────

        [Test]
        public void FromJson_ZeroCostAmount_IsZero()
        {
            var result = NoctuaAdjustAttribution.FromJson(@"{ ""costAmount"": 0 }");
            Assert.AreEqual(0d, result.CostAmount, 1e-9);
        }

        [Test]
        public void FromJson_LargeCostAmount_Preserved()
        {
            var result = NoctuaAdjustAttribution.FromJson(@"{ ""costAmount"": 9999.999 }");
            Assert.AreEqual(9999.999d, result.CostAmount, 1e-9);
        }

        // ─── Network values ───────────────────────────────────────────────────

        [Test]
        public void FromJson_OrganicNetwork_EmptyStringPreserved()
        {
            var result = NoctuaAdjustAttribution.FromJson(@"{ ""network"": """" }");
            Assert.AreEqual("", result.Network);
        }

        [Test]
        public void FromJson_WhitespaceCampaign_PreservedAsIs()
        {
            var result = NoctuaAdjustAttribution.FromJson(@"{ ""campaign"": ""  "" }");
            Assert.AreEqual("  ", result.Campaign);
        }
    }
}
