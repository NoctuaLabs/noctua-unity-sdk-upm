using System.Diagnostics;
using com.noctuagames.sdk.LiveOpsCampaign;
using NUnit.Framework;

namespace Tests.Runtime.Campaign
{
    /// <summary>
    /// Payloads of <c>live_ops_campaign_impression</c> / <c>live_ops_campaign_dismiss</c>.
    /// The new keys are additive: a campaign with no engagement type must still produce exactly
    /// the old one-key payload, so existing dashboards and queries keep matching.
    /// </summary>
    [TestFixture]
    public class CampaignEventPayloadTest
    {
        [Test]
        public void CampaignPayload_NoEngagementType_IsTheOldOneKeyPayload()
        {
            var payload = NoctuaLiveOpsCampaign.CampaignPayload(CampaignFactory.Item("cmp", null, null));

            Assert.AreEqual(1, payload.Count);
            Assert.AreEqual("cmp", payload["campaign_id"]);
        }

        [Test]
        public void CampaignPayload_AddsEngagementType()
        {
            var payload = NoctuaLiveOpsCampaign.CampaignPayload(
                CampaignFactory.Item("cmp", CampaignItem.EngagementEvent, null));

            Assert.AreEqual("cmp", payload["campaign_id"]);
            Assert.AreEqual(CampaignItem.EngagementEvent, payload["engagement_type"]);
        }

        [Test]
        public void CampaignPayload_NullItem_KeepsEmptyCampaignId()
        {
            var payload = NoctuaLiveOpsCampaign.CampaignPayload(null);

            Assert.AreEqual(string.Empty, payload["campaign_id"]);
        }

        [Test]
        public void DismissPayload_CarriesReasonAndVisibleMs()
        {
            var timer = Stopwatch.StartNew();
            var payload = NoctuaLiveOpsCampaign.DismissPayload(
                CampaignFactory.Item("cmp", CampaignItem.EngagementPurchase, null),
                CampaignActionDispatcher.DismissReasonCloseButton,
                timer);

            Assert.AreEqual("cmp", payload["campaign_id"]);
            Assert.AreEqual(CampaignItem.EngagementPurchase, payload["engagement_type"]);
            Assert.AreEqual(CampaignActionDispatcher.DismissReasonCloseButton, payload["reason"]);
            Assert.GreaterOrEqual((long)payload["visible_ms"], 0L);
        }

        [Test]
        public void DismissPayload_NeverShown_OmitsVisibleMs()
        {
            var payload = NoctuaLiveOpsCampaign.DismissPayload(
                CampaignFactory.Item("cmp", null, null),
                CampaignActionDispatcher.DismissReasonProgrammatic,
                visibleTimer: null);

            Assert.IsFalse(payload.ContainsKey("visible_ms"));
            Assert.AreEqual(CampaignActionDispatcher.DismissReasonProgrammatic, payload["reason"]);
        }
    }
}
