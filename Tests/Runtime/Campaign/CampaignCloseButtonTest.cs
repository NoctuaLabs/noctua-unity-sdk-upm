using com.noctuagames.sdk.LiveOpsCampaign;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Tests.Runtime.Campaign
{
    [TestFixture]
    public class CampaignCloseButtonTest
    {
        [Test]
        public void Absent_close_button_DeserializesToNull()
        {
            var item = JsonConvert.DeserializeObject<CampaignItem>(
                "{ \"id\": \"c\", \"engagement_type\": \"purchase\" }");

            Assert.IsNull(item.CloseButton);
        }

        [Test]
        public void Hidden_close_button_Maps()
        {
            var item = JsonConvert.DeserializeObject<CampaignItem>(
                "{ \"id\": \"c\", \"close_button\": { \"hidden\": true } }");

            Assert.IsNotNull(item.CloseButton);
            Assert.IsTrue(item.CloseButton.Hidden);
            Assert.IsNull(item.CloseButton.ImageUrl);
            Assert.IsNull(item.CloseButton.Size);
            Assert.IsNull(item.CloseButton.Inset);
        }

        [Test]
        public void Skinned_close_button_MapsAllFields()
        {
            var item = JsonConvert.DeserializeObject<CampaignItem>(
                "{ \"id\": \"c\", \"close_button\": { " +
                "\"image_url\": \"{{close_bg}}\", \"size\": 44, \"inset\": 10 } }");

            var cb = item.CloseButton;
            Assert.IsNotNull(cb);
            Assert.IsFalse(cb.Hidden);
            Assert.AreEqual("{{close_bg}}", cb.ImageUrl);
            Assert.AreEqual(44, cb.Size);
            Assert.AreEqual(10, cb.Inset);
        }
    }
}
