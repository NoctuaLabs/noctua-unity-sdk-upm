using System.Linq;
using com.noctuagames.sdk.Campaign;
using NUnit.Framework;

namespace Tests.Runtime.Campaign
{
    [TestFixture]
    public class CampaignSchemaGateTest
    {
        private static CampaignItem PopupItem(string id, int schemaVersion)
        {
            var item = CampaignFactory.Item(id, CampaignItem.EngagementPurchase,
                CampaignFactory.Node(CampaignNode.TypeContainer));
            item.SchemaVersion = schemaVersion;
            return item;
        }

        [Test]
        public void Renderer_TooNewSchema_ReturnsNull()
        {
            var renderer = new CampaignRenderer(null, null);
            var item = PopupItem("c", CampaignRenderer.SupportedSchemaVersion + 1);

            using var controller = new CampaignRuntimeController();
            Assert.IsNull(renderer.RenderCampaign(item, controller));
        }

        [Test]
        public void Renderer_SupportedSchema_Renders()
        {
            var renderer = new CampaignRenderer(null, null);
            var item = PopupItem("c", CampaignRenderer.SupportedSchemaVersion);

            using var controller = new CampaignRuntimeController();
            Assert.IsNotNull(renderer.RenderCampaign(item, controller));
        }

        [Test]
        public void Manager_TooNewSchema_SkippedWithReason()
        {
            var config = new CampaignConfig
            {
                SchemaVersion = 1,
                Campaigns = new System.Collections.Generic.List<CampaignItem>
                {
                    PopupItem("ok", 1),
                    PopupItem("future", CampaignRenderer.SupportedSchemaVersion + 5),
                },
            };

            var manager = new CampaignManager(config, new FakeEnv(),
                new CampaignFrequencyGate(prefs: new FakePrefsStore()));

            var active = manager.GetActiveCampaigns(CampaignItem.EngagementPurchase);

            Assert.AreEqual(1, active.Count);
            Assert.AreEqual("ok", active[0].Id);

            var futureRes = manager.LastResolutions.First(r => r.Id == "future");
            Assert.IsFalse(futureRes.Eligible);
            StringAssert.Contains("schema", futureRes.Reason);
        }

        [Test]
        public void Manager_ItemInheritsConfigSchemaVersion_WhenUnset()
        {
            var config = new CampaignConfig
            {
                SchemaVersion = CampaignRenderer.SupportedSchemaVersion + 3,
                Campaigns = new System.Collections.Generic.List<CampaignItem> { PopupItem("inherits", 0) },
            };

            var manager = new CampaignManager(config, new FakeEnv(),
                new CampaignFrequencyGate(prefs: new FakePrefsStore()));

            Assert.AreEqual(0, manager.GetActiveCampaigns(CampaignItem.EngagementPurchase).Count);
        }
    }
}
