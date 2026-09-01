using System.Collections.Generic;
using com.noctuagames.sdk.Campaign;
using NUnit.Framework;

namespace Tests.Runtime.Campaign
{
    [TestFixture]
    public class CampaignActionDispatcherTest
    {
        private MockEventSender _events;
        private CampaignActionHandlers _handlers;
        private CampaignActionDispatcher _dispatcher;

        private string _deeplink;
        private string _purchasedProduct;

        [SetUp]
        public void SetUp()
        {
            _events = new MockEventSender();
            _deeplink = _purchasedProduct = null;

            _handlers = new CampaignActionHandlers
            {
                Deeplink = d => _deeplink = d,
                Purchase = (p, _) => _purchasedProduct = p,
            };

            _dispatcher = new CampaignActionDispatcher(_handlers, _events);
        }

        private static CampaignItem Ctx => CampaignFactory.Item("cmp", CampaignItem.EngagementPurchase, null);

        private void Dispatch(CampaignAction a) => _dispatcher.Dispatch(a, Ctx);

        [Test]
        public void Deeplink_ForwardsRoute()
        {
            Dispatch(new CampaignAction { TypeRaw = "deeplink", Deeplink = "shop/bundle/7" });
            Assert.AreEqual("shop/bundle/7", _deeplink);
        }

        [Test]
        public void Deeplink_AlsoClosesThePopup()
        {
            var closed = false;
            _dispatcher.CurrentDismiss = () => closed = true;
            Dispatch(new CampaignAction { TypeRaw = "deeplink", Deeplink = "event/x" });
            Assert.AreEqual("event/x", _deeplink);
            Assert.IsTrue(closed, "deeplink should invoke CurrentDismiss");
        }

        [Test]
        public void Purchase_ForwardsProductId()
        {
            Dispatch(new CampaignAction { TypeRaw = "purchase", ProductId = "gold_pack_1" });
            Assert.AreEqual("gold_pack_1", _purchasedProduct);
        }

        [Test]
        public void Purchase_DoesNotCloseThePopup()
        {
            var closed = false;
            _dispatcher.CurrentDismiss = () => closed = true;
            Dispatch(new CampaignAction { TypeRaw = "purchase", ProductId = "p1" });
            Assert.IsFalse(closed, "purchase leaves the popup open");
        }

        [Test]
        public void Purchase_MissingProductId_NoThrow_NoCall()
        {
            Assert.DoesNotThrow(() => Dispatch(new CampaignAction { TypeRaw = "purchase" }));
            Assert.IsNull(_purchasedProduct);
        }

        [Test]
        public void Dismiss_InvokesCurrentDismiss()
        {
            var dismissed = false;
            _dispatcher.CurrentDismiss = () => dismissed = true;
            Dispatch(new CampaignAction { TypeRaw = "dismiss" });
            Assert.IsTrue(dismissed);
        }

        [Test]
        public void EveryDispatch_EmitsCampaignClick()
        {
            Dispatch(new CampaignAction { TypeRaw = "deeplink", Deeplink = "x" });
            Dispatch(new CampaignAction { TypeRaw = "totally_unknown" });

            var clicks = _events.GetEventsByName(CampaignActionDispatcher.ClickEvent);
            Assert.AreEqual(2, clicks.Count);
            Assert.AreEqual("cmp", clicks[0].Data["campaign_id"]);
        }

        [Test]
        public void UnknownAction_NoHandlerCalled_NoThrow()
        {
            Assert.DoesNotThrow(() => Dispatch(new CampaignAction { TypeRaw = "frobnicate" }));
            Assert.IsNull(_deeplink);
            Assert.IsNull(_purchasedProduct);
        }

        [Test]
        public void MissingHandler_NoThrow()
        {
            var bare = new CampaignActionDispatcher(new CampaignActionHandlers(), _events);
            Assert.DoesNotThrow(() =>
                bare.Dispatch(new CampaignAction { TypeRaw = "deeplink", Deeplink = "a/b" }, Ctx));
        }
    }
}
