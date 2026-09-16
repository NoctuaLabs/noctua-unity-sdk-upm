using com.noctuagames.sdk.LiveOpsCampaign;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime.Campaign
{
    [TestFixture]
    public class CampaignBackdropTest
    {
        private static CampaignItem Parse(string extra) =>
            JsonConvert.DeserializeObject<CampaignItem>(
                "{ \"id\": \"c\", \"engagement_type\": \"event\"" + extra + " }");

        [Test]
        public void Absent_backdrop_color_DeserializesToNull()
        {
            Assert.IsNull(Parse("").BackdropColor);
        }

        [Test]
        public void TryResolve_Absent_ReturnsFalse()
        {
            Assert.IsFalse(CampaignBackdropStyle.TryResolve(Parse(""), out _));
        }

        [Test]
        public void TryResolve_RgbHex_IsOpaque()
        {
            Assert.IsTrue(CampaignBackdropStyle.TryResolve(Parse(", \"backdrop_color\": \"#102030\""), out var color));
            Assert.AreEqual(1f, color.a);
        }

        [Test]
        public void TryResolve_RgbaHex_CarriesOpacity()
        {
            Assert.IsTrue(CampaignBackdropStyle.TryResolve(Parse(", \"backdrop_color\": \"#FF000080\""), out var color));
            Assert.AreEqual(1f, color.r);
            Assert.AreEqual(128f / 255f, color.a, 0.001f);
        }

        [Test]
        public void TryResolve_FullyTransparent_IsAllowed()
        {
            // A campaign may deliberately drop the dim entirely.
            Assert.IsTrue(CampaignBackdropStyle.TryResolve(Parse(", \"backdrop_color\": \"#00000000\""), out var color));
            Assert.AreEqual(0f, color.a);
        }

        [TestCase("dark")]
        [TestCase("#12")]
        [TestCase("   ")]
        public void TryResolve_Invalid_ReturnsFalse(string raw)
        {
            Assert.IsFalse(CampaignBackdropStyle.TryResolve(Parse($", \"backdrop_color\": \"{raw}\""), out _));
        }

        [Test]
        public void InvalidBackdrop_DoesNotCancelCampaign()
        {
            var item = CampaignFactory.Item("c", CampaignItem.EngagementEvent,
                new CampaignNode { Type = CampaignNode.TypeSpacer });
            item.BackdropColor = "nope";

            Assert.IsTrue(CampaignValidator.TryValidate(item, out var error), error);
        }
    }
}
