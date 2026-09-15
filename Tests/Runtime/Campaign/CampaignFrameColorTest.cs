using com.noctuagames.sdk.LiveOpsCampaign;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime.Campaign
{
    [TestFixture]
    public class CampaignFrameColorTest
    {
        private static CampaignItem Parse(string extra) =>
            JsonConvert.DeserializeObject<CampaignItem>(
                "{ \"id\": \"c\", \"engagement_type\": \"event\"" + extra + " }");

        [Test]
        public void Absent_frame_color_DeserializesToNull()
        {
            Assert.IsNull(Parse("").FrameColor);
        }

        [Test]
        public void Present_frame_color_Maps()
        {
            Assert.AreEqual("#3B82F6", Parse(", \"frame_color\": \"#3B82F6\"").FrameColor);
        }

        [Test]
        public void TryResolve_ValidHex_OnCardChrome_ReturnsColor()
        {
            var item = Parse(", \"frame_color\": \"#FF0000\", \"borderless\": false");

            Assert.IsTrue(CampaignFrameStyle.TryResolve(item, out var color));
            Assert.AreEqual(Color.red, color);
        }

        [Test]
        public void TryResolve_Absent_ReturnsFalse()
        {
            Assert.IsFalse(CampaignFrameStyle.TryResolve(Parse(", \"borderless\": false"), out _));
        }

        [TestCase("not-a-color")]
        [TestCase("#12")]
        [TestCase("")]
        public void TryResolve_InvalidHex_ReturnsFalse(string raw)
        {
            var item = Parse($", \"frame_color\": \"{raw}\", \"borderless\": false");
            Assert.IsFalse(CampaignFrameStyle.TryResolve(item, out _));
        }

        [Test]
        public void TryResolve_Borderless_ReturnsFalse()
        {
            // Borderless creatives draw their own frame — the SDK card has no chrome to colour.
            var item = Parse(", \"frame_color\": \"#FF0000\"");
            Assert.IsFalse(CampaignFrameStyle.TryResolve(item, out _));
        }

        [Test]
        public void TryResolve_Fullscreen_ReturnsFalse()
        {
            var item = Parse(", \"frame_color\": \"#FF0000\", \"borderless\": false, \"fullscreen\": true");
            Assert.IsFalse(CampaignFrameStyle.TryResolve(item, out _));
        }

        [Test]
        public void InvalidHex_DoesNotCancelCampaign()
        {
            var item = CampaignFactory.Item("c", CampaignItem.EngagementEvent,
                new CampaignNode { Type = CampaignNode.TypeSpacer });
            item.FrameColor = "garbage";

            Assert.IsTrue(CampaignValidator.TryValidate(item, out var error), error);
        }
    }
}
