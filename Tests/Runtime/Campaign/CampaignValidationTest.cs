using System.Collections.Generic;
using com.noctuagames.sdk.LiveOpsCampaign;
using NUnit.Framework;

namespace Tests.Runtime.Campaign
{
    [TestFixture]
    public class CampaignValidationTest
    {
        private static CampaignItem Popup(CampaignNode view, Dictionary<string, string> data = null)
            => CampaignFactory.Item("c", CampaignItem.EngagementPurchase, view, data);

        private static bool Valid(CampaignItem item, out string error)
            => CampaignValidator.TryValidate(item, out error);

        // ---- campaign level ------------------------------------------------

        [Test]
        public void BareContainer_IsValid()
        {
            Assert.IsTrue(Valid(Popup(CampaignFactory.Node(CampaignNode.TypeContainer)), out _));
        }

        [Test]
        public void MissingId_Fails()
        {
            var item = CampaignFactory.Item(null, CampaignItem.EngagementPurchase,
                CampaignFactory.Node(CampaignNode.TypeContainer));
            Assert.IsFalse(Valid(item, out var err));
            StringAssert.Contains("id", err);
        }

        [Test]
        public void UnknownEngagementType_Fails()
        {
            var item = CampaignFactory.Item("c", "banner", CampaignFactory.Node(CampaignNode.TypeContainer));
            Assert.IsFalse(Valid(item, out var err));
            StringAssert.Contains("engagement_type", err);
        }

        [Test]
        public void EventEngagementType_IsValid()
        {
            var item = CampaignFactory.Item("c", CampaignItem.EngagementEvent,
                CampaignFactory.Node(CampaignNode.TypeContainer));
            Assert.IsTrue(Valid(item, out _));
        }

        [Test]
        public void NoView_Fails()
        {
            Assert.IsFalse(Valid(CampaignFactory.Item("c", CampaignItem.EngagementPurchase, null), out var err));
            StringAssert.Contains("view", err);
        }

        // ---- node level --------------------------------------------------

        [Test]
        public void Image_UnresolvedUrlToken_Fails()
        {
            var view = CampaignFactory.Node(CampaignNode.TypeImage,
                new Dictionary<string, object> { { "url", "{{bg}}" } });
            Assert.IsFalse(Valid(Popup(view, new Dictionary<string, string>()), out var err));
            StringAssert.Contains("url", err);
        }

        [Test]
        public void Image_TokenResolved_Passes()
        {
            var view = CampaignFactory.Node(CampaignNode.TypeImage,
                new Dictionary<string, object> { { "url", "{{bg}}" } });
            Assert.IsTrue(Valid(Popup(view, new Dictionary<string, string> { { "bg", "https://cdn/x.png" } }), out _));
        }

        [Test]
        public void Image_MissingUrl_Fails()
        {
            var view = CampaignFactory.Node(CampaignNode.TypeImage);
            Assert.IsFalse(Valid(Popup(view), out var err));
            StringAssert.Contains("url", err);
        }

        [Test]
        public void Text_EmptyLabel_Fails()
        {
            var view = CampaignFactory.Node(CampaignNode.TypeText);
            Assert.IsFalse(Valid(Popup(view), out var err));
            StringAssert.Contains("label", err);
        }

        [Test]
        public void Button_UnresolvedLabelToken_Fails()
        {
            var view = CampaignFactory.Node(CampaignNode.TypeButton,
                new Dictionary<string, object> { { "text", "Buy {{price}}" } });
            Assert.IsFalse(Valid(Popup(view, new Dictionary<string, string>()), out var err));
            StringAssert.Contains("label", err);
        }

        [Test]
        public void Text_LocKeyResolved_Passes()
        {
            var view = CampaignFactory.Node(CampaignNode.TypeText,
                new Dictionary<string, object> { { "loc_key", "greeting" } });
            Assert.IsTrue(Valid(Popup(view, new Dictionary<string, string> { { "greeting", "Hello" } }), out _));
        }

        [Test]
        public void Countdown_UnparseableEndTs_Fails()
        {
            var view = CampaignFactory.Node(CampaignNode.TypeCountdown,
                new Dictionary<string, object> { { "end_ts", "soon" } });
            Assert.IsFalse(Valid(Popup(view), out var err));
            StringAssert.Contains("end_ts", err);
        }

        [Test]
        public void Countdown_MissingEndTs_Fails()
        {
            var view = CampaignFactory.Node(CampaignNode.TypeCountdown);
            Assert.IsFalse(Valid(Popup(view), out var err));
            StringAssert.Contains("end_ts", err);
        }

        [Test]
        public void Countdown_IsoAndUnix_Pass()
        {
            var iso = CampaignFactory.Node(CampaignNode.TypeCountdown,
                new Dictionary<string, object> { { "end_ts", "2026-09-01T11:42:27Z" } });
            Assert.IsTrue(Valid(Popup(iso), out _));

            var unix = CampaignFactory.Node(CampaignNode.TypeCountdown,
                new Dictionary<string, object> { { "end_ts", "1790000000" } });
            Assert.IsTrue(Valid(Popup(unix), out _));
        }

        [Test]
        public void Carousel_NoChildren_Fails()
        {
            var view = CampaignFactory.Node(CampaignNode.TypeCarousel);
            Assert.IsFalse(Valid(Popup(view), out var err));
            StringAssert.Contains("carousel", err);
        }

        [Test]
        public void Carousel_WithChild_Passes()
        {
            var view = CampaignFactory.Node(CampaignNode.TypeCarousel, children: new[]
            {
                CampaignFactory.Node(CampaignNode.TypeImage,
                    new Dictionary<string, object> { { "url", "https://cdn/x.png" } }),
            });
            Assert.IsTrue(Valid(Popup(view), out _));
        }

        [Test]
        public void UnknownNodeType_IsNotFatal()
        {
            var view = CampaignFactory.Node(CampaignNode.TypeContainer, children: new[]
            {
                CampaignFactory.Node("hologram"),
            });
            Assert.IsTrue(Valid(Popup(view), out _));
        }

        [Test]
        public void DeepTree_DoesNotThrow()
        {
            var root = CampaignFactory.Node(CampaignNode.TypeContainer);
            var cur = root;
            for (var i = 0; i < 500; i++)
            {
                var child = CampaignFactory.Node(CampaignNode.TypeContainer);
                cur.Children = new List<CampaignNode> { child };
                cur = child;
            }
            Assert.DoesNotThrow(() => CampaignValidator.TryValidate(Popup(root), out _));
        }

        // ---- action level ----------------------------------------------

        [Test]
        public void Purchase_MissingProductId_Fails()
        {
            var view = CampaignFactory.Node(CampaignNode.TypeButton,
                new Dictionary<string, object> { { "text", "Buy" } },
                action: new CampaignAction { TypeRaw = "purchase" });
            Assert.IsFalse(Valid(Popup(view), out var err));
            StringAssert.Contains("product_id", err);
        }

        [Test]
        public void Purchase_TokenResolvedProductId_Passes()
        {
            var view = CampaignFactory.Node(CampaignNode.TypeButton,
                new Dictionary<string, object> { { "text", "Buy" } },
                action: new CampaignAction { TypeRaw = "purchase", ProductId = "{{sku}}" });
            Assert.IsTrue(Valid(Popup(view, new Dictionary<string, string> { { "sku", "noctua.pack7" } }), out _));
        }

        [Test]
        public void Deeplink_Missing_Fails()
        {
            var view = CampaignFactory.Node(CampaignNode.TypeButton,
                new Dictionary<string, object> { { "text", "Go" } },
                action: new CampaignAction { TypeRaw = "deeplink" });
            Assert.IsFalse(Valid(Popup(view), out var err));
            StringAssert.Contains("deeplink", err);
        }

        [Test]
        public void Deeplink_Present_Passes()
        {
            var view = CampaignFactory.Node(CampaignNode.TypeButton,
                new Dictionary<string, object> { { "text", "Go" } },
                action: new CampaignAction { TypeRaw = "deeplink", Deeplink = "event/summer" });
            Assert.IsTrue(Valid(Popup(view), out _));
        }

        [Test]
        public void Dismiss_NeedsNoFields()
        {
            var view = CampaignFactory.Node(CampaignNode.TypeButton,
                new Dictionary<string, object> { { "text", "Close" } },
                action: new CampaignAction { TypeRaw = "dismiss" });
            Assert.IsTrue(Valid(Popup(view), out _));
        }

        [Test]
        public void UnknownAction_IsNotFatal()
        {
            var view = CampaignFactory.Node(CampaignNode.TypeButton,
                new Dictionary<string, object> { { "text", "?" } },
                action: new CampaignAction { TypeRaw = "track_event" }); // not in the trimmed enum → None → no requirement
            Assert.IsTrue(Valid(Popup(view), out _));
        }

        // ---- manager integration --------------------------------------

        [Test]
        public void Manager_InvalidCampaign_Excluded_WithInvalidReason()
        {
            var bad = CampaignFactory.Item("bad", CampaignItem.EngagementPurchase,
                CampaignFactory.Node(CampaignNode.TypeImage,
                    new Dictionary<string, object> { { "url", "{{bg}}" } }),
                new Dictionary<string, string>()); // no 'bg' → unresolved

            var config = new CampaignConfig { SchemaVersion = 1, Campaigns = new List<CampaignItem> { bad } };
            var mgr = new CampaignManager(config, new FakeEnv(),
                new CampaignFrequencyGate(prefs: new FakePrefsStore()));

            Assert.AreEqual(0, mgr.GetActiveCampaigns(CampaignItem.EngagementPurchase).Count);
            StringAssert.StartsWith("invalid:", mgr.LastResolutions[0].Reason);
        }
    }
}
