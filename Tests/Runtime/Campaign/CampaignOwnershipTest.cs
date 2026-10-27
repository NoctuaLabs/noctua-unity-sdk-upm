using System;
using System.Collections.Generic;
using System.Linq;
using com.noctuagames.sdk.LiveOpsCampaign;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime.Campaign
{
    /// <summary>
    /// Purchase state on <see cref="NoctuaLiveOpsCampaign"/>: which SKUs a campaign sells, the
    /// pure <c>owned.&lt;sku&gt;</c> swap, the "fully owned" rule, the manager's <c>owned</c> gate and
    /// the check / cache / fail-open rules.
    /// </summary>
    [TestFixture]
    public class CampaignOwnershipTest
    {
        private const string Gold = "pass.gold";
        private const string Normal = "pass.normal";

        [TearDown]
        public void TearDown()
        {
            // Each facade parks an empty UI root in DontDestroyOnLoad.
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go.name == "NoctuaLiveOpsCampaignUI") UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static CampaignNode Buy(string productId) =>
            CampaignFactory.Node(CampaignNode.TypeButton,
                props: new Dictionary<string, object> { { "text", "Buy" } },
                action: new CampaignAction { TypeRaw = "purchase", ProductId = productId });

        private static CampaignItem Offer(string id, Dictionary<string, string> data, params CampaignNode[] buttons) =>
            CampaignFactory.Item(id, CampaignItem.EngagementPurchase,
                CampaignFactory.Node(CampaignNode.TypeContainer, children: buttons), data);

        private static CampaignItem MultiOffer() =>
            Offer("monthly_pass", new Dictionary<string, string>
                {
                    { "owned." + Gold, "0" },
                    { "owned." + Normal, "0" },
                },
                Buy(Gold), Buy(Normal));

        private static Dictionary<string, bool> Owns(params (string sku, bool owned)[] answers) =>
            answers.ToDictionary(a => a.sku, a => a.owned);

        private static NoctuaLiveOpsCampaign Facade(Func<string, UniTask<bool>> isOwned, params CampaignItem[] campaigns) =>
            new NoctuaLiveOpsCampaign(
                new CampaignConfig { SchemaVersion = 2, Campaigns = campaigns.ToList() },
                null, null, new MockEventSender(), () => Array.Empty<string>(),
                isProductOwned: isOwned);

        private static bool Prefetch(NoctuaLiveOpsCampaign campaign, CampaignItem item) =>
            campaign.PrefetchOwnershipAsync(item).GetAwaiter().GetResult();

        // ---- which SKUs a campaign sells --------------------------------

        [Test]
        public void PurchaseSkus_WalksNestedButtons_ResolvesTokens_SkipsDuplicates()
        {
            var item = Offer("starter", new Dictionary<string, string> { { "product_id", Gold } },
                CampaignFactory.Node(CampaignNode.TypeContainer, children: Buy("{{product_id}}")),
                Buy(Normal),
                Buy(Gold));

            CollectionAssert.AreEqual(new[] { Gold, Normal }, NoctuaLiveOpsCampaign.PurchaseSkus(item));
        }

        [Test]
        public void PurchaseSkus_IgnoresDeeplinksAndEmptyProductIds()
        {
            var deeplink = CampaignFactory.Node(CampaignNode.TypeButton,
                props: new Dictionary<string, object> { { "text", "Details" } },
                action: new CampaignAction { TypeRaw = "deeplink", Deeplink = "shop/gold" });
            var item = Offer("news", null, deeplink, Buy("{{missing}}"));

            Assert.IsEmpty(NoctuaLiveOpsCampaign.PurchaseSkus(item));
            Assert.IsEmpty(NoctuaLiveOpsCampaign.PurchaseSkus(null));
        }

        // ---- the pure swap ------------------------------------------------

        [Test]
        public void ApplyOwnership_FlipsKnownSkus_KeepsDefaultForUnknown_NeverMutates()
        {
            var item = MultiOffer();
            var applied = NoctuaLiveOpsCampaign.ApplyOwnership(item, Owns((Gold, true), ("other", true)));

            Assert.AreEqual("1", applied.Data["owned." + Gold]);
            Assert.AreEqual("0", applied.Data["owned." + Normal], "unknown SKU keeps the shipped default");
            Assert.IsFalse(applied.Data.ContainsKey("owned.other"), "never adds keys the campaign doesn't ship");
            Assert.AreEqual("0", item.Data["owned." + Gold], "the stored item is untouched");
        }

        [Test]
        public void ApplyOwnership_NothingToChange_ReturnsTheSameItem()
        {
            var item = MultiOffer();
            Assert.AreSame(item, NoctuaLiveOpsCampaign.ApplyOwnership(item, null));
            Assert.AreSame(item, NoctuaLiveOpsCampaign.ApplyOwnership(item, Owns((Gold, false))));
        }

        // ---- fully owned ----------------------------------------------------

        [Test]
        public void FullyOwned_OnlyWhenEverySkuIsKnownOwned()
        {
            var item = MultiOffer();

            Assert.IsTrue(NoctuaLiveOpsCampaign.IsFullyOwned(item, Owns((Gold, true), (Normal, true))));
            Assert.IsFalse(NoctuaLiveOpsCampaign.IsFullyOwned(item, Owns((Gold, true), (Normal, false))), "one card still buyable");
            Assert.IsFalse(NoctuaLiveOpsCampaign.IsFullyOwned(item, Owns((Gold, true))), "unknown counts as not owned");
            Assert.IsFalse(NoctuaLiveOpsCampaign.IsFullyOwned(item, null));
        }

        [Test]
        public void FullyOwned_NeverForACampaignThatSellsNothing()
        {
            var news = Offer("news", null, CampaignFactory.Node(CampaignNode.TypeText,
                props: new Dictionary<string, object> { { "text", "Hi" } }));
            Assert.IsFalse(NoctuaLiveOpsCampaign.IsFullyOwned(news, Owns((Gold, true))));
        }

        // ---- the manager gate ----------------------------------------------

        [Test]
        public void Manager_OwnedCampaign_IsNotEligible_WithReasonOwned()
        {
            var owned = MultiOffer();
            var other = Offer("starter", null, Buy(Gold));
            var manager = new CampaignManager(
                new CampaignConfig { SchemaVersion = 2, Campaigns = new List<CampaignItem> { owned, other } },
                new FakeEnv(), new CampaignFrequencyGate(prefs: new FakePrefsStore()),
                isOwned: c => c.Id == owned.Id);

            var active = manager.GetActiveCampaigns();

            CollectionAssert.AreEqual(new[] { "starter" }, active.Select(c => c.Id));
            Assert.AreEqual("owned", manager.LastResolutions.Single(r => r.Id == owned.Id).Reason);
        }

        // ---- automatic purchased state -------------------------------------

        private static CampaignItem StarterPack(Dictionary<string, string> data = null) =>
            Offer("starter_pack_1", data,
                CampaignFactory.Node(CampaignNode.TypeContainer, children: Buy(Gold)),
                Buy(Normal));

        private static CampaignNode GoldButton(CampaignItem item) => item.View.Children[0].Children[0];
        private static CampaignNode NormalButton(CampaignItem item) => item.View.Children[1];

        [Test]
        public void MarkOwned_OwnedButton_BecomesAnUntappablePurchasedLabel()
        {
            var item = StarterPack();
            var marked = NoctuaLiveOpsCampaign.MarkOwnedButtons(item, Owns((Gold, true), (Normal, false)), "Purchased");

            var gold = GoldButton(marked);
            Assert.IsNull(gold.Action, "an owned product can't be bought again");
            Assert.AreEqual("Purchased", gold.PropString("text"));
            Assert.AreEqual(NoctuaLiveOpsCampaign.PurchasedButtonColor, gold.Style.BackgroundColor);

            Assert.AreSame(NormalButton(item), NormalButton(marked), "a buyable button is untouched");
        }

        [Test]
        public void MarkOwned_NeverMutatesTheStoredItem()
        {
            var item = StarterPack();
            NoctuaLiveOpsCampaign.MarkOwnedButtons(item, Owns((Gold, true)), "Purchased");

            var gold = GoldButton(item);
            Assert.IsNotNull(gold.Action);
            Assert.AreEqual("Buy", gold.PropString("text"));
            Assert.IsNull(gold.Style);
        }

        [Test]
        public void MarkOwned_NothingOwned_ReturnsTheSameItem()
        {
            var item = StarterPack();
            Assert.AreSame(item, NoctuaLiveOpsCampaign.MarkOwnedButtons(item, Owns((Gold, false)), "Purchased"));
            Assert.AreSame(item, NoctuaLiveOpsCampaign.MarkOwnedButtons(item, null, "Purchased"));
        }

        [Test]
        public void MarkOwned_CampaignLabelWins_ThenTheTranslation_ThenEnglish()
        {
            var custom = StarterPack(new Dictionary<string, string> { { "purchased_label", "Owned!" } });
            Assert.AreEqual("Owned!", GoldButton(NoctuaLiveOpsCampaign.MarkOwnedButtons(custom, Owns((Gold, true)), "Sudah Dibeli")).PropString("text"));

            Assert.AreEqual("Sudah Dibeli", GoldButton(NoctuaLiveOpsCampaign.MarkOwnedButtons(StarterPack(), Owns((Gold, true)), "Sudah Dibeli")).PropString("text"));
            Assert.AreEqual(NoctuaLiveOpsCampaign.DefaultPurchasedLabel, GoldButton(NoctuaLiveOpsCampaign.MarkOwnedButtons(StarterPack(), Owns((Gold, true)), null)).PropString("text"));
        }

        [Test]
        public void MarkOwned_SkuTheCampaignStylesItself_IsLeftToItsVisibleIf()
        {
            var item = StarterPack(new Dictionary<string, string> { { "owned." + Gold, "0" } });
            var marked = NoctuaLiveOpsCampaign.MarkOwnedButtons(item, Owns((Gold, true)), "Purchased");
            Assert.AreSame(item, marked);
        }

        [Test]
        public void MarkOwned_TokenizedProductId_LocKeyDropped_ResponsiveColourOverridden()
        {
            var button = Buy("{{product_id}}");
            button.Props["loc_key"] = "buy_label";
            button.Responsive = new Dictionary<string, CampaignStyleProps>
            {
                { "landscape", new CampaignStyleProps { BackgroundColor = "#17A34A", Height = "40" } },
                { "compact", new CampaignStyleProps { Height = "32" } },
            };
            var item = Offer("starter", new Dictionary<string, string> { { "product_id", Gold }, { "buy_label", "Buy" } }, button);

            var marked = NoctuaLiveOpsCampaign.MarkOwnedButtons(item, Owns((Gold, true)), "Purchased").View.Children[0];

            Assert.AreEqual("Purchased", marked.PropString("text"));
            Assert.IsNull(marked.PropString("loc_key"), "loc_key would put the Buy label back");
            Assert.AreEqual(NoctuaLiveOpsCampaign.PurchasedButtonColor, marked.Responsive["landscape"].BackgroundColor);
            Assert.AreEqual("40", marked.Responsive["landscape"].Height);
            Assert.AreSame(button.Responsive["compact"], marked.Responsive["compact"]);
            Assert.AreEqual("#17A34A", button.Responsive["landscape"].BackgroundColor, "the stored style is untouched");
        }

        [Test]
        public void MarkOwned_TappableNonButton_OnlyLosesItsAction()
        {
            var image = CampaignFactory.Node(CampaignNode.TypeImage,
                props: new Dictionary<string, object> { { "url", "https://example.com/pack.png" } },
                action: new CampaignAction { TypeRaw = "purchase", ProductId = Gold });
            var item = Offer("starter", null, image);

            var marked = NoctuaLiveOpsCampaign.MarkOwnedButtons(item, Owns((Gold, true)), "Purchased").View.Children[0];

            Assert.IsNull(marked.Action);
            Assert.AreEqual("https://example.com/pack.png", marked.PropString("url"));
            Assert.IsNull(marked.PropString("text"));
        }

        // ---- check / cache / fail open --------------------------------------

        [Test]
        public void Prefetch_CachesAnswers_AndHidesAFullyOwnedOffer()
        {
            var item = MultiOffer();
            var asked = new List<string>();
            var campaign = Facade(sku => { asked.Add(sku); return UniTask.FromResult(true); }, item);

            Assert.IsTrue(Prefetch(campaign, item));
            Assert.IsFalse(Prefetch(campaign, item), "cached — nothing new to learn");

            CollectionAssert.AreEquivalent(new[] { Gold, Normal }, asked, "each SKU asked once");
            Assert.IsTrue(campaign.IsFullyOwned(item));
            Assert.IsEmpty(campaign.GetActiveCampaigns(), "the owned offer drops out");
        }

        [Test]
        public void Prefetch_PartlyOwned_StaysEligible()
        {
            var item = MultiOffer();
            var campaign = Facade(sku => UniTask.FromResult(sku == Gold), item);

            Prefetch(campaign, item);

            Assert.IsTrue(campaign.Ownership[Gold]);
            Assert.IsFalse(campaign.Ownership[Normal]);
            CollectionAssert.AreEqual(new[] { item.Id }, campaign.GetActiveCampaigns().Select(c => c.Id));
        }

        [Test]
        public void Prefetch_ProviderThrows_StaysUnknown_AndTheOfferStillShows()
        {
            var item = MultiOffer();
            var campaign = Facade(_ => throw new InvalidOperationException("store down"), item);

            Assert.IsFalse(Prefetch(campaign, item));
            Assert.IsEmpty(campaign.Ownership);
            Assert.AreEqual(1, campaign.GetActiveCampaigns().Count);
        }

        [Test]
        public void Prefetch_AccountSwitchMidCheck_DropsTheAnswers()
        {
            var item = MultiOffer();
            NoctuaLiveOpsCampaign campaign = null;
            campaign = Facade(_ =>
            {
                campaign.ClearOwnership();
                return UniTask.FromResult(true);
            }, item);

            Assert.IsFalse(Prefetch(campaign, item));
            Assert.IsEmpty(campaign.Ownership);
        }

        [Test]
        public void NoProvider_NothingIsCheckedOrHidden()
        {
            var item = MultiOffer();
            var campaign = Facade(sku => UniTask.FromResult(true), item);
            campaign.SetOwnershipProvider(null);

            Assert.IsFalse(Prefetch(campaign, item));
            Assert.AreEqual(1, campaign.GetActiveCampaigns().Count);
        }

        [Test]
        public void SetOwnershipProvider_ClearsWhatTheOldOneSaid()
        {
            var item = MultiOffer();
            var campaign = Facade(sku => UniTask.FromResult(true), item);
            Prefetch(campaign, item);
            Assert.IsTrue(campaign.IsFullyOwned(item));

            campaign.SetOwnershipProvider(sku => UniTask.FromResult(false));

            Assert.IsEmpty(campaign.Ownership);
            Assert.IsFalse(campaign.IsFullyOwned(item));
        }

        [Test]
        public void RefreshOwnership_ReasksOneSku()
        {
            var item = MultiOffer();
            var owned = false;
            var campaign = Facade(_ => UniTask.FromResult(owned), item);
            Prefetch(campaign, item);
            Assert.IsFalse(campaign.Ownership[Gold]);

            owned = true;
            campaign.RefreshOwnershipAsync(Gold).GetAwaiter().GetResult();

            Assert.IsTrue(campaign.Ownership[Gold]);
            Assert.IsFalse(campaign.Ownership[Normal], "other SKUs keep their answer");
        }
    }
}
