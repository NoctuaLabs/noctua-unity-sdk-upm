using System;
using System.Collections.Generic;
using System.Linq;
using com.noctuagames.sdk.LiveOpsCampaign;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Runtime.Campaign
{
    /// <summary>
    /// Dynamic products: a Buy button with <c>product_key</c> points at a JSON bundle in the
    /// campaign's data, and the game's resolver turns it into the SKU to sell.
    /// </summary>
    [TestFixture]
    public class CampaignDynamicProductTest
    {
        private const string Bundle =
            "{\"tier\":\"t3\",\"items\":[{\"id\":\"gem\",\"qty\":500},{\"id\":\"gold\",\"qty\":20000}]}";

        [SetUp]
        public void SetUp() => LogAssert.ignoreFailingMessages = true;

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            LiveOpsProgressTestKit.DestroyUiRoots();
        }

        private static CampaignNode DynamicBuy(string key, string id = "offer_1_btn_0") =>
            CampaignFactory.Node(CampaignNode.TypeButton,
                props: new Dictionary<string, object> { { "text", "Buy" } },
                action: new CampaignAction { TypeRaw = "purchase", ProductKey = key, Id = id });

        private static CampaignItem Offer(string bundle, params CampaignNode[] buttons) =>
            CampaignFactory.Item("dyn", CampaignItem.EngagementPurchase,
                CampaignFactory.Node(CampaignNode.TypeContainer, children: buttons),
                new Dictionary<string, string> { { "offer_main", bundle } });

        private static NoctuaLiveOpsCampaign Facade(CampaignItem item) =>
            new(new CampaignConfig { SchemaVersion = 2, Campaigns = new List<CampaignItem> { item } },
                null, null, new MockEventSender(), () => Array.Empty<string>());

        private static string TierToSku(CampaignItem _, string __, JToken bundle) =>
            bundle["tier"]?.ToString() == "t3" ? "pack3" : null;

        private static CampaignAction FirstAction(CampaignItem item) => item.View.Children[0].Action;

        [Test]
        public void ProductKey_DeserializesFromJson()
        {
            var action = Newtonsoft.Json.JsonConvert.DeserializeObject<CampaignAction>(
                "{ \"type\": \"purchase\", \"product_key\": \"offer_main\" }");
            Assert.AreEqual("offer_main", action.ProductKey);
            Assert.IsNull(action.ProductId);
        }

        [Test]
        public void GetProductBundle_ParsesObjectsAndArrays()
        {
            var facade = Facade(Offer(Bundle, DynamicBuy("offer_main")));
            var item = Offer(Bundle);

            Assert.AreEqual("t3", facade.GetProductBundle(item, "offer_main")["tier"].ToString());

            var array = Offer("[{\"sku_hint\":\"a\"},{\"sku_hint\":\"b\"}]");
            array.Id = "dyn_array";
            Assert.AreEqual(2, ((JArray)facade.GetProductBundle(array, "offer_main")).Count);
        }

        [TestCase("not json")]
        [TestCase("\"just a string\"")]
        [TestCase("[1,2,3]")]
        [TestCase("[]")]
        [TestCase("")]
        public void GetProductBundle_RejectsAnythingButObjects(string raw)
        {
            var item = Offer(raw);
            Assert.IsNull(Facade(item).GetProductBundle(item, "offer_main"));
        }

        [Test]
        public void GetProductBundle_RejectsOversizedBundles()
        {
            var big = "{\"pad\":\"" + new string('x', NoctuaLiveOpsCampaign.MaxBundleBytes) + "\"}";
            var item = Offer(big);
            Assert.IsNull(Facade(item).GetProductBundle(item, "offer_main"));
        }

        [Test]
        public void GetProductBundle_ReturnsACopy()
        {
            var item = Offer(Bundle);
            var facade = Facade(item);

            facade.GetProductBundle(item, "offer_main")["tier"] = "tampered";

            Assert.AreEqual("t3", facade.GetProductBundle(item, "offer_main")["tier"].ToString());
        }

        [Test]
        public void WithDynamicProducts_FillsTheResolvedSku_WithoutTouchingTheStoredItem()
        {
            var item = Offer(Bundle, DynamicBuy("offer_main"));
            var facade = Facade(item);
            facade.SetProductResolver(TierToSku);

            var resolved = facade.WithDynamicProducts(item);

            Assert.AreEqual("pack3", FirstAction(resolved).ProductId);
            Assert.AreEqual("offer_1_btn_0", FirstAction(resolved).Id);
            Assert.IsNull(FirstAction(item).ProductId, "the stored campaign is never mutated");
            CollectionAssert.AreEqual(new[] { "pack3" }, NoctuaLiveOpsCampaign.PurchaseSkus(resolved));
        }

        [Test]
        public void WithDynamicProducts_HidesButtonsTheGameCannotSell()
        {
            var item = Offer("{\"tier\":\"t9\"}", DynamicBuy("offer_main"));
            var facade = Facade(item);
            facade.SetProductResolver(TierToSku);

            var node = facade.WithDynamicProducts(item).View.Children[0];

            Assert.IsNull(node.Action);
            Assert.IsFalse(CampaignConditions.Evaluate(node.VisibleIf, item.Data), "hidden by a never-true condition");
        }

        [Test]
        public void WithDynamicProducts_HidesEverythingWithoutAResolver()
        {
            var item = Offer(Bundle, DynamicBuy("offer_main"));
            var node = Facade(item).WithDynamicProducts(item).View.Children[0];
            Assert.IsNull(node.Action);
            Assert.IsNotNull(node.VisibleIf);
        }

        [Test]
        public void WithDynamicProducts_SurvivesAThrowingResolver()
        {
            var item = Offer(Bundle, DynamicBuy("offer_main"));
            var facade = Facade(item);
            facade.SetProductResolver((_, __, ___) => throw new InvalidOperationException("boom"));

            Assert.IsNull(facade.WithDynamicProducts(item).View.Children[0].Action);
        }

        [Test]
        public void WithDynamicProducts_LeavesFixedSkuCampaignsAlone()
        {
            var item = CampaignFactory.Item("fixed", CampaignItem.EngagementPurchase,
                CampaignFactory.Node(CampaignNode.TypeButton,
                    action: new CampaignAction { TypeRaw = "purchase", ProductId = "pack1" }));
            Assert.AreSame(item, Facade(item).WithDynamicProducts(item));
        }

        [Test]
        public void ResolverSeesTheKeyAndBundle_AndTheSkuMapsBackToTheBundle()
        {
            var item = Offer(Bundle, DynamicBuy("offer_main"));
            var facade = Facade(item);
            string seenKey = null;
            facade.SetProductResolver((_, key, bundle) =>
            {
                seenKey = key;
                return TierToSku(null, key, bundle);
            });

            facade.WithDynamicProducts(item);

            Assert.AreEqual("offer_main", seenKey);
            Assert.AreEqual(500, (int)facade.GetProductBundleForSku(item, "pack3")["items"][0]["qty"]);
            Assert.IsNull(facade.GetProductBundleForSku(item, "pack1"), "a fixed SKU has no bundle");
        }

        [Test]
        public void PurchaseExtra_CarriesCampaignAndBundle_AndKeepsTheGamesOwnKeys()
        {
            var item = Offer(Bundle, DynamicBuy("offer_main"));
            var facade = Facade(item);
            facade.SetProductResolver(TierToSku);
            facade.WithDynamicProducts(item);
            var gameExtra = new Dictionary<string, string> { { "source", "popup" } };

            var extra = facade.PurchaseExtra(item, "pack3", gameExtra);

            Assert.AreEqual("dyn", extra[NoctuaLiveOpsCampaign.ExtraCampaignId]);
            Assert.AreEqual("offer_main", extra[NoctuaLiveOpsCampaign.ExtraBundleKey]);
            Assert.AreEqual("t3", JObject.Parse(extra[NoctuaLiveOpsCampaign.ExtraBundle])["tier"].ToString());
            Assert.AreEqual("popup", extra["source"]);
            Assert.AreEqual(1, gameExtra.Count, "the game's dictionary is not modified");
        }

        [Test]
        public void PurchaseExtra_ForAFixedSku_HasOnlyTheCampaign()
        {
            var item = Offer(Bundle);
            var extra = Facade(item).PurchaseExtra(item, "pack1");
            CollectionAssert.AreEquivalent(new[] { NoctuaLiveOpsCampaign.ExtraCampaignId }, extra.Keys.ToList());
        }

        [Test]
        public void Validator_AcceptsAProductKeyWithItsBundle_RejectsAMissingOne()
        {
            var ok = Offer(Bundle, DynamicBuy("offer_main"));
            Assert.IsTrue(CampaignValidator.TryValidate(ok, out var error), error);

            var missing = Offer(Bundle, DynamicBuy("offer_other"));
            Assert.IsFalse(CampaignValidator.TryValidate(missing, out error));
            StringAssert.Contains("offer_other", error);
        }
    }
}
