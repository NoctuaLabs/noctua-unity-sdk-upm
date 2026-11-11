using System;
using System.Collections.Generic;
using System.Linq;
using com.noctuagames.sdk;
using com.noctuagames.sdk.LiveOpsCampaign;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.Runtime.Campaign
{
    /// <summary>
    /// What a game no longer has to write: the built-in Claim All action, the default product
    /// resolver, campaign info on orders, and one setup call.
    /// </summary>
    [TestFixture]
    public class CampaignGameIntegrationTest
    {
        [SetUp]
        public void SetUp() => LogAssert.ignoreFailingMessages = true;

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            LiveOpsProgressTestKit.DestroyUiRoots();
        }

        private static CampaignItem Offer(string id, Dictionary<string, string> data, params CampaignNode[] buttons) =>
            CampaignFactory.Item(id, CampaignItem.EngagementPurchase,
                CampaignFactory.Node(CampaignNode.TypeContainer, children: buttons), data);

        private static CampaignNode DynamicBuy(string key) =>
            CampaignFactory.Node(CampaignNode.TypeButton,
                action: new CampaignAction { TypeRaw = "purchase", ProductKey = key });

        private static CampaignNode FixedBuy(string sku) =>
            CampaignFactory.Node(CampaignNode.TypeButton,
                action: new CampaignAction { TypeRaw = "purchase", ProductId = sku });

        private static NoctuaLiveOpsCampaign Facade(params CampaignItem[] campaigns) =>
            new(new CampaignConfig { SchemaVersion = 3, Campaigns = campaigns.ToList() },
                null, null, new MockEventSender(), () => Array.Empty<string>());

        // ---- Claim All as a built-in action ------------------------------

        [Test]
        public void ClaimAll_ActionType_DeserializesFromJson()
        {
            var action = JsonConvert.DeserializeObject<CampaignAction>("{\"type\":\"claim_all\",\"id\":\"claim_all\"}");
            Assert.AreEqual(CampaignActionType.ClaimAll, action.Type);
        }

        [Test]
        public void ClaimAll_RunsTheHandler_VeilsThePopup_AndKeepsItOpen()
        {
            var gate = new UniTaskCompletionSource();
            CampaignItem claimedFor = null;
            var busy = new List<bool>();
            var closed = false;
            var dispatcher = new CampaignActionDispatcher(new CampaignActionHandlers
            {
                ClaimAllAsync = item => { claimedFor = item; return gate.Task; },
            }, new MockEventSender())
            {
                CurrentBusy = busy.Add,
                CurrentDismiss = () => closed = true,
            };
            var missions = Offer("season", null);

            dispatcher.Dispatch(new CampaignAction { TypeRaw = "claim_all" }, missions);
            dispatcher.Dispatch(new CampaignAction { TypeRaw = "claim_all" }, missions); // double tap
            gate.TrySetResult();

            Assert.AreSame(missions, claimedFor);
            CollectionAssert.AreEqual(new[] { true, false }, busy, "one claim, veiled while it runs");
            Assert.IsFalse(closed, "the popup stays open to show the claimed state");
        }

        [Test]
        public void ClaimAll_WithoutAHandler_IsIgnored()
        {
            var dispatcher = new CampaignActionDispatcher(new CampaignActionHandlers(), new MockEventSender());
            Assert.DoesNotThrow(() => dispatcher.Dispatch(new CampaignAction { TypeRaw = "claim_all" }, Offer("x", null)));
        }

        // ---- Default product resolver ------------------------------------

        [TestCase("{\"" + NoctuaLiveOpsCampaign.PlatformSkuKey + "\":\"store.sku\",\"sku\":\"generic.sku\"}", "store.sku")]
        [TestCase("{\"sku\":\"generic.sku\"}", "generic.sku")]
        [TestCase("[{\"sku\":\"first.sku\"},{\"sku\":\"second.sku\"}]", "first.sku")]
        [TestCase("{\"sku\":\"  padded.sku  \"}", "padded.sku")]
        [TestCase("{\"tier\":\"t3\"}", null)]
        [TestCase("{\"sku\":42}", null)]
        [TestCase("{\"sku\":\"\"}", null)]
        public void DefaultBundleSku_ReadsTheStoreThenGenericKey(string bundle, string expected)
        {
            Assert.AreEqual(expected, NoctuaLiveOpsCampaign.DefaultBundleSku(JToken.Parse(bundle)));
        }

        [Test]
        public void WithoutAResolver_TheBundlesOwnSkuIsSold()
        {
            var item = Offer("dyn", new Dictionary<string, string> { { "offer", "{\"sku\":\"pack9\"}" } }, DynamicBuy("offer"));

            var resolved = Facade(item).WithDynamicProducts(item);

            Assert.AreEqual("pack9", resolved.View.Children[0].Action.ProductId);
        }

        [Test]
        public void AGameResolver_StillDecidesAlone()
        {
            var item = Offer("dyn", new Dictionary<string, string> { { "offer", "{\"sku\":\"pack9\"}" } }, DynamicBuy("offer"));
            var facade = Facade(item);
            facade.SetProductResolver((_, __, ___) => null);

            var node = facade.WithDynamicProducts(item).View.Children[0];

            Assert.IsNull(node.Action, "a resolver's null means 'do not sell', even if the bundle names a SKU");
        }

        // ---- Campaign info on orders -------------------------------------

        [Test]
        public void CampaignOrderExtra_OnlyForAProductJustTappedInACampaign()
        {
            var item = Offer("starter", null, FixedBuy("pack1"));
            var dispatcher = new CampaignActionDispatcher(new CampaignActionHandlers { Purchase = (_, __) => { } },
                new MockEventSender());
            var facade = Facade(item);

            Assert.IsNull(dispatcher.RecentPurchaseCampaign("pack1"), "no tap yet");
            dispatcher.Dispatch(new CampaignAction { TypeRaw = "purchase", ProductId = "pack1" }, item);
            Assert.AreEqual("starter", dispatcher.RecentPurchaseCampaign("pack1"));
            Assert.IsNull(dispatcher.RecentPurchaseCampaign("pack2"), "another product is not credited");
            Assert.IsNull(facade.CampaignOrderExtra("pack1"), "this facade's own dispatcher saw no tap");
        }

        // ---- Popup purchases never mix with the game's own purchases ---------

        private sealed class Clock
        {
            public DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            public void Advance(double seconds) => Now = Now.AddSeconds(seconds);
        }

        private static (CampaignActionDispatcher dispatcher, List<string> credited, MockEventSender events)
            AwaitableDispatcher(Clock clock, UniTaskCompletionSource gate)
        {
            var credited = new List<string>();
            var events = new MockEventSender();
            var dispatcher = new CampaignActionDispatcher(new CampaignActionHandlers
            {
                PurchaseAsync = (_, __) => gate.Task,
                PurchaseCredited = (campaignId, _) => credited.Add(campaignId),
            }, events, now: () => clock.Now);
            return (dispatcher, credited, events);
        }

        private static int ResultEvents(MockEventSender events) =>
            events.SentEvents.Count(e => e.Name == CampaignActionDispatcher.PurchaseResultEvent);

        [Test]
        public void APurchaseStartedFromThePopup_IsTheCampaigns()
        {
            var clock = new Clock();
            var gate = new UniTaskCompletionSource();
            var (dispatcher, credited, events) = AwaitableDispatcher(clock, gate);

            dispatcher.Dispatch(new CampaignAction { TypeRaw = "purchase", ProductId = "pack1" }, Offer("starter", null));
            clock.Advance(5); // the game's own confirm dialog
            Assert.AreEqual("starter", dispatcher.RecentPurchaseCampaign("pack1"), "the order gets the campaign");
            dispatcher.HandlePurchaseSettled(new PurchaseOutcome("pack1", clock.Now, "completed", orderId: 1));
            gate.TrySetResult();

            CollectionAssert.AreEqual(new[] { "starter" }, credited);
            Assert.AreEqual(1, ResultEvents(events));
        }

        [Test]
        public void AShopPurchaseAfterThePopupPurchaseEnded_IsNeverTheCampaigns()
        {
            var clock = new Clock();
            var gate = new UniTaskCompletionSource();
            var (dispatcher, credited, events) = AwaitableDispatcher(clock, gate);

            dispatcher.Dispatch(new CampaignAction { TypeRaw = "purchase", ProductId = "pack1" }, Offer("starter", null));
            clock.Advance(3);
            gate.TrySetResult(); // the player cancelled in the popup; the handler returned without buying
            clock.Advance(10);   // ...then bought the same SKU from the game's own shop

            Assert.IsNull(dispatcher.RecentPurchaseCampaign("pack1"), "the shop order is not tagged");
            dispatcher.HandlePurchaseSettled(new PurchaseOutcome("pack1", clock.Now, "completed", orderId: 2));

            Assert.IsEmpty(credited, "not credited, so a buy-once campaign is not locked by a shop purchase");
            Assert.AreEqual(0, ResultEvents(events));
        }

        [Test]
        public void ADifferentProduct_IsNeverTheCampaigns_EvenWhileThePopupBuys()
        {
            var clock = new Clock();
            var gate = new UniTaskCompletionSource();
            var (dispatcher, credited, _) = AwaitableDispatcher(clock, gate);

            dispatcher.Dispatch(new CampaignAction { TypeRaw = "purchase", ProductId = "pack1" }, Offer("starter", null));

            Assert.IsNull(dispatcher.RecentPurchaseCampaign("gems_100"));
            dispatcher.HandlePurchaseSettled(new PurchaseOutcome("gems_100", clock.Now, "completed", orderId: 3));
            gate.TrySetResult();

            Assert.IsEmpty(credited);
        }

        [Test]
        public void APurchaseThatStartedBeforeTheTap_IsNotTheCampaigns_AndTheTapKeepsWaiting()
        {
            var clock = new Clock();
            var gate = new UniTaskCompletionSource();
            var (dispatcher, credited, _) = AwaitableDispatcher(clock, gate);
            var earlierShopStart = clock.Now;

            clock.Advance(30);
            dispatcher.Dispatch(new CampaignAction { TypeRaw = "purchase", ProductId = "pack1" }, Offer("starter", null));
            dispatcher.HandlePurchaseSettled(new PurchaseOutcome("pack1", earlierShopStart, "completed", orderId: 4));
            Assert.IsEmpty(credited, "an older shop purchase finishing now is not the popup's");

            dispatcher.HandlePurchaseSettled(new PurchaseOutcome("pack1", clock.Now, "completed", orderId: 5));
            gate.TrySetResult();
            CollectionAssert.AreEqual(new[] { "starter" }, credited, "the popup's own purchase still counts");
        }

        [Test]
        public void TheLegacyEventPath_KeepsItsTimeWindow()
        {
            var clock = new Clock();
            var credited = new List<string>();
            var dispatcher = new CampaignActionDispatcher(new CampaignActionHandlers
            {
                Purchase = (_, __) => { },
                PurchaseCredited = (campaignId, _) => credited.Add(campaignId),
            }, new MockEventSender(), now: () => clock.Now);

            dispatcher.Dispatch(new CampaignAction { TypeRaw = "purchase", ProductId = "pack1" }, Offer("starter", null));
            clock.Advance(CampaignActionDispatcher.PurchaseAttributionWindowSeconds + 5);
            dispatcher.HandlePurchaseSettled(new PurchaseOutcome("pack1", clock.Now, "completed", orderId: 6));

            Assert.IsEmpty(credited, "no end signal, so only the window protects this path");
        }

        [Test]
        public void MergeOrderExtra_GameKeysWin_AndNothingIsMutated()
        {
            var added = new Dictionary<string, string> { { "campaign_id", "starter" }, { "source", "campaign" } };
            var game = new Dictionary<string, string> { { "source", "shop" } };

            var merged = NoctuaIAPService.MergeOrderExtra(added, game);

            Assert.AreEqual("starter", merged["campaign_id"]);
            Assert.AreEqual("shop", merged["source"]);
            Assert.AreEqual(1, game.Count);
            Assert.AreSame(game, NoctuaIAPService.MergeOrderExtra(null, game), "ordinary orders are untouched");
            Assert.IsNull(NoctuaIAPService.MergeOrderExtra(new Dictionary<string, string>(), null));
        }

        // ---- One setup call ----------------------------------------------

        [Test]
        public void GameHandlers_Merge_KeepsEarlierFieldsTheNewSetLeavesOut()
        {
            Action<string> deeplink = _ => { };
            Func<CampaignItem, string, JToken, string> resolver = (_, __, ___) => "x";

            var merged = NoctuaLiveOpsCampaign.GameHandlers.Merge(
                new NoctuaLiveOpsCampaign.GameHandlers { Deeplink = deeplink },
                new NoctuaLiveOpsCampaign.GameHandlers { ProductResolver = resolver });

            Assert.AreSame(deeplink, merged.Deeplink);
            Assert.AreSame(resolver, merged.ProductResolver);
        }

        [Test]
        public void Configure_AppliesTheResolver_AndReplacesTheClaimedHandlerInsteadOfAddingOne()
        {
            var item = Offer("dyn", new Dictionary<string, string> { { "offer", "{\"tier\":\"t1\"}" } }, DynamicBuy("offer"));
            var facade = Facade(item);
            var calls = new List<string>();

            facade.Configure(new NoctuaLiveOpsCampaign.GameHandlers
            {
                ProductResolver = (_, __, bundle) => bundle["tier"]?.ToString() == "t1" ? "pack1" : null,
                MissionsClaimed = (_, __) => calls.Add("first"),
            });
            facade.Configure(new NoctuaLiveOpsCampaign.GameHandlers { MissionsClaimed = (_, __) => calls.Add("second") });

            Assert.AreEqual("pack1", facade.WithDynamicProducts(item).View.Children[0].Action.ProductId,
                "the resolver from the first call is kept");

            var raise = typeof(NoctuaLiveOpsCampaign).GetMethod("NotifyMissionsClaimed",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            raise.Invoke(facade, new object[] { "season", new List<string> { "m1" } });
            CollectionAssert.AreEqual(new[] { "second" }, calls);
        }
    }
}
