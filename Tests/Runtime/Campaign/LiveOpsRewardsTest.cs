using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using com.noctuagames.sdk;
using com.noctuagames.sdk.LiveOpsCampaign;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.Runtime.Campaign.LiveOpsProgressTestKit;

namespace Tests.Runtime.Campaign
{
    /// <summary>
    /// The shared Rewards flow, case by case: reward items, campaign bundle purchases (which order
    /// the SDK delivers, which bundle it carries, double reports, another buyer, a crash before
    /// delivery, a purchase verified before live ops is ready), and milestone chests claimed from
    /// points per mission.
    /// </summary>
    [TestFixture]
    public class LiveOpsRewardsTest
    {
        private const string BaseUrl = "http://localhost:7791/api/v1";
        private const string ServerUrl = "http://localhost:7791/api/v1/";
        private const string FixturePath = "Packages/com.noctuagames.sdk/Tests/Runtime/Campaign/Fixtures/daily-missions.json";
        private const string InboxKey = "Noctua.LiveOps.Campaign.PurchaseInbox";
        private const string Scope = "season";
        private const string List = "/progress/42";
        private static readonly string Stm = $"/progress/42/{Scope}.use_stm/claim";
        private static readonly string Chest25 = $"/progress/42/{Scope}.milestone_25";
        private static readonly string Chest25Claim = $"/progress/42/{Scope}.milestone_25/claim";
        private static readonly string Chest50 = $"/progress/42/{Scope}.milestone_50";

        private HttpMockServer _server;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _server = new HttpMockServer(ServerUrl);
            _server.Start();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() => _server.Dispose();

        [SetUp]
        public void SetUp()
        {
            ResetServer(_server, new[] { List, Stm, Chest25, Chest25Claim, Chest50 });
            ClearSaved();
            PlayerPrefs.DeleteKey(InboxKey);
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            DestroyUiRoots();
            ClearSaved();
            PlayerPrefs.DeleteKey(InboxKey);
        }

        // ---- helpers ----------------------------------------------------------

        private static string Items(params (string id, long qty)[] items) =>
            JsonConvert.SerializeObject(items.Select(i => new { id = i.id, qty = i.qty }));

        private static CampaignHandlers Collect(List<LiveOpsReward> delivered) => new CampaignHandlers
        {
            Rewards = rewards => { delivered.AddRange(rewards); return UniTask.CompletedTask; },
        };

        /// <summary>A purchase campaign with two Buy buttons that sell the same SKU with different items.</summary>
        private static CampaignItem SharedSkuOffer() =>
            CampaignFactory.Item("summer", CampaignItem.EngagementPurchase,
                CampaignFactory.Node(CampaignNode.TypeContainer, children: new[]
                {
                    CampaignFactory.Node(CampaignNode.TypeButton, action: new CampaignAction { TypeRaw = "purchase", ProductKey = "offer_gems", Id = "gems" }),
                    CampaignFactory.Node(CampaignNode.TypeButton, action: new CampaignAction { TypeRaw = "purchase", ProductKey = "offer_gold", Id = "gold" }),
                    CampaignFactory.Node(CampaignNode.TypeButton, action: new CampaignAction { TypeRaw = "purchase", ProductKey = "offer_tier", Id = "tier" }),
                }),
                new Dictionary<string, string>
                {
                    { "offer_gems", "{\"sku\":\"pack_499\",\"items\":[{\"id\":\"gem\",\"qty\":500}]}" },
                    { "offer_gold", "{\"sku\":\"pack_499\",\"items\":[{\"id\":\"gold\",\"qty\":20000},{\"id\":\"chest\",\"qty\":1}]}" },
                    { "offer_tier", "{\"sku\":\"pack_999\",\"tier\":\"t3\"}" },
                });

        private static NoctuaLiveOpsCampaign Module(params CampaignItem[] campaigns) =>
            NewFacade(BaseUrl, new CampaignConfig { SchemaVersion = 3, Campaigns = campaigns.ToList() });

        /// <summary>
        /// Taps the resolved Buy button <paramref name="buttonId"/> with a purchase handler that is
        /// still running — the moment the real order is created — and returns that order's extra.
        /// </summary>
        private static IReadOnlyDictionary<string, string> OrderExtraOfTap(NoctuaLiveOpsCampaign module, CampaignItem item, string buttonId)
        {
            var resolved = module.WithDynamicProducts(item);
            var action = resolved.View.Children.Select(child => child.Action).First(a => a?.Id == buttonId);
            var dispatcher = (CampaignActionDispatcher)typeof(NoctuaLiveOpsCampaign)
                .GetField("_dispatcher", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(module);

            // The order is created while the game's purchase handler runs; it ends afterwards, so
            // the next tap is not ignored as a double tap.
            var running = new UniTaskCompletionSource();
            module.Configure(new CampaignHandlers { Purchase = (_, __) => running.Task });
            dispatcher.Dispatch(action, item);
            var extra = module.CampaignOrderExtra(action.ProductId);
            running.TrySetResult();
            return extra;
        }

        private static OrderRequest Order(int id, IReadOnlyDictionary<string, string> extra, string sku = "pack_499") =>
            new OrderRequest { Id = id, ProductId = sku, Extra = extra?.ToDictionary(p => p.Key, p => p.Value) };

        // ---- reward items -----------------------------------------------------

        [Test]
        public void Items_ReadAListForMissionsAndOnlyAnItemsKeyForBundles()
        {
            Assert.AreEqual(2, RewardItem.ParseList("[{\"id\":\"gem\",\"qty\":5},{\"id\":\"gold\",\"qty\":9}]").Count);
            Assert.AreEqual("gem x5", RewardItem.ParseBundle("{\"sku\":\"p\",\"items\":[{\"id\":\"gem\",\"qty\":5}]}").Single().ToString());
            CollectionAssert.AreEqual(new[] { "gem x5", "gold x9" },
                RewardItem.ParseBundle("[{\"items\":[{\"id\":\"gem\",\"qty\":5}]},{\"items\":[{\"id\":\"gold\",\"qty\":9}]}]")
                    .Select(i => i.ToString()).ToArray(), "a bundle array joins its entries' items");

            Assert.IsNull(RewardItem.ParseBundle("[{\"id\":\"coin\",\"qty\":10}]"),
                "a bundle lists items only under 'items' (same rule as the admin): this one stays the game's");
            Assert.IsNull(RewardItem.ParseBundle("[{\"items\":[{\"id\":\"gem\",\"qty\":5}]},{\"tier\":\"t3\"}]"),
                "one entry without items: not deliverable whole");
            Assert.IsNull(RewardItem.ParseList("{\"items\":[{\"id\":\"gem\",\"qty\":5}]}"), "a mission's rewards are a plain list");
        }

        [TestCase("[{\"id\":\"gem\",\"qty\":0}]", TestName = "Items_Reject_ZeroQuantity")]
        [TestCase("[{\"id\":\"gem\",\"qty\":-5}]", TestName = "Items_Reject_NegativeQuantity")]
        [TestCase("[{\"id\":\"gem\",\"qty\":\"5\"}]", TestName = "Items_Reject_QuantityAsText")]
        [TestCase("[{\"id\":\"gem\",\"qty\":1.5}]", TestName = "Items_Reject_FractionalQuantity")]
        [TestCase("[{\"id\":\"gem\",\"qty\":1000000001}]", TestName = "Items_Reject_QuantityAboveMax")]
        [TestCase("[{\"id\":\"gem pack\",\"qty\":1}]", TestName = "Items_Reject_IdWithASpace")]
        [TestCase("[{\"id\":\"\",\"qty\":1}]", TestName = "Items_Reject_EmptyId")]
        [TestCase("[{\"qty\":1}]", TestName = "Items_Reject_MissingId")]
        [TestCase("[{\"id\":\"gem\",\"qty\":1},{\"id\":\"bad id\",\"qty\":1}]", TestName = "Items_Reject_OneBadItemSpoilsAll")]
        [TestCase("[]", TestName = "Items_Reject_EmptyList")]
                [TestCase("not json", TestName = "Items_Reject_BrokenJson")]
        [TestCase("", TestName = "Items_Reject_Empty")]
        public void Items_RejectAnythingNotWhollyValid(string json)
        {
            Assert.IsNull(RewardItem.ParseList(json));
            Assert.IsNull(RewardItem.ParseBundle("{\"items\":" + (string.IsNullOrEmpty(json) ? "null" : json) + "}"));
        }

        [Test]
        public void Bundle_WithoutItems_IsNotDeliverable() =>
            Assert.IsNull(RewardItem.ParseBundle("{\"sku\":\"p\",\"tier\":\"t3\"}"));

        [Test]
        public void Items_RejectAnIdTooLongOrTooManyItems()
        {
            Assert.IsNull(RewardItem.ParseList(Items((new string('a', RewardItem.MaxIdLength + 1), 1))));
            Assert.IsNull(RewardItem.ParseList(Items(Enumerable.Range(0, RewardItem.MaxItems + 1).Select(n => ($"i{n}", 1L)).ToArray())));
            Assert.AreEqual(RewardItem.MaxItems,
                RewardItem.ParseList(Items(Enumerable.Range(0, RewardItem.MaxItems).Select(n => ($"i{n}", 1L)).ToArray())).Count);
        }

        // ---- the item rules shared with noctua-admin and the noctua-data DAG -------

        private const string SharedRulesPath = "Packages/com.noctuagames.sdk/Tests/Runtime/Campaign/Fixtures/reward-items.json";

        private static IEnumerable<TestCaseData> SharedLists() =>
            JObject.Parse(File.ReadAllText(Path.GetFullPath(SharedRulesPath)))["lists"]!
                .Select(c => new TestCaseData(c["items"]!.ToString(Formatting.None), (bool)c["valid"]!)
                    .SetName("SharedRules_List_" + ((string)c["name"]!).Replace(' ', '_')));

        private static IEnumerable<TestCaseData> SharedBundles() =>
            JObject.Parse(File.ReadAllText(Path.GetFullPath(SharedRulesPath)))["bundles"]!
                .Select(c => new TestCaseData(c["bundle"]!.ToString(Formatting.None), (string)c["result"]!)
                    .SetName("SharedRules_Bundle_" + ((string)c["name"]!).Replace(' ', '_')));

        /// <summary>Same file as noctua-admin's reward-items.json and the DAG's: all three must agree.</summary>
        [TestCaseSource(nameof(SharedLists))]
        public void SharedRules_ItemList(string json, bool valid) =>
            Assert.AreEqual(valid, RewardItem.ParseList(json) != null);

        /// <summary>The SDK delivers only "deliverable" bundles; "no_items" and "invalid" stay the game's.</summary>
        [TestCaseSource(nameof(SharedBundles))]
        public void SharedRules_Bundle(string json, string result) =>
            Assert.AreEqual(result == "deliverable", RewardItem.ParseBundle(json) != null);

        // ---- which order the SDK delivers, and with which bundle ----------------

        [Test]
        public void Order_CarriesTheTappedButtonsBundle_WhenTwoButtonsShareASku()
        {
            var item = SharedSkuOffer();
            var module = Module(item);
            module.Configure(Collect(new List<LiveOpsReward>()));

            var gold = OrderExtraOfTap(module, item, "gold");
            Assert.AreEqual("offer_gold", gold[NoctuaLiveOpsCampaign.ExtraBundleKey]);
            StringAssert.Contains("chest", gold[NoctuaLiveOpsCampaign.ExtraBundle]);

            var gems = OrderExtraOfTap(module, item, "gems");
            Assert.AreEqual("offer_gems", gems[NoctuaLiveOpsCampaign.ExtraBundleKey], "the other button of the same SKU keeps its own bundle");
            StringAssert.DoesNotContain("chest", gems[NoctuaLiveOpsCampaign.ExtraBundle]);
        }

        [Test]
        public void Order_IsMarkedForRewards_OnlyWithItemsAndARewardsHandler()
        {
            var item = SharedSkuOffer();

            var noHandler = Module(item);
            Assert.IsFalse(NoctuaLiveOpsCampaign.IsDeliveredByRewards(OrderExtraOfTap(noHandler, item, "gems")),
                "without a Rewards handler the game keeps delivering by SKU");

            var withHandler = Module(item);
            withHandler.Configure(Collect(new List<LiveOpsReward>()));
            Assert.IsTrue(NoctuaLiveOpsCampaign.IsDeliveredByRewards(OrderExtraOfTap(withHandler, item, "gems")));
            Assert.IsFalse(NoctuaLiveOpsCampaign.IsDeliveredByRewards(OrderExtraOfTap(withHandler, item, "tier")),
                "a bundle with no items list stays the game's to deliver");
        }

        [Test]
        public void Order_FromAFixedSkuOrOutsideAPopup_IsNeverMarked()
        {
            var item = CampaignFactory.Item("fixed", CampaignItem.EngagementPurchase,
                CampaignFactory.Node(CampaignNode.TypeButton, action: new CampaignAction { TypeRaw = "purchase", ProductId = "pack_499", Id = "buy" }));
            var module = Module(item);
            module.Configure(Collect(new List<LiveOpsReward>()));

            var dispatcher = (CampaignActionDispatcher)typeof(NoctuaLiveOpsCampaign)
                .GetField("_dispatcher", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(module);
            module.Configure(new CampaignHandlers { Purchase = (_, __) => UniTask.Never(default) });
            dispatcher.Dispatch(item.View.Action, item);

            var extra = module.CampaignOrderExtra("pack_499");
            Assert.AreEqual("fixed", extra[NoctuaLiveOpsCampaign.ExtraCampaignId]);
            Assert.IsFalse(NoctuaLiveOpsCampaign.IsDeliveredByRewards(extra));
            Assert.IsNull(module.CampaignOrderExtra("shop_pack"), "a purchase no popup started carries nothing");
        }

        [Test]
        public void IsDeliveredByRewards_NeedsTheMarkerARealOrderAndValidItems()
        {
            var campaign = new NoctuaCampaign();
            Assert.IsTrue(campaign.IsDeliveredByRewards(Order(1, MarkedExtra())));
            Assert.IsFalse(campaign.IsDeliveredByRewards(Order(1, new Dictionary<string, string> { { "noctua_delivery", "rewards" } })),
                "a marker without a readable bundle could never be delivered, so the game keeps it");
            Assert.IsFalse(campaign.IsDeliveredByRewards(Order(0, MarkedExtra())), "no order id: the Editor's fake order");
            Assert.IsFalse(campaign.IsDeliveredByRewards(Order(1, new Dictionary<string, string> { { "campaign_bundle", "{}" } })));
            Assert.IsFalse(campaign.IsDeliveredByRewards(Order(1, null)), "a redeem / pending-deliverable order has no extra");
            Assert.IsFalse(campaign.IsDeliveredByRewards(null));
        }

        // ---- purchase rewards -------------------------------------------------

        private static Dictionary<string, string> MarkedExtra(string items = "[{\"id\":\"gem\",\"qty\":500}]") => new()
        {
            { NoctuaLiveOpsCampaign.ExtraCampaignId, "summer" },
            { NoctuaLiveOpsCampaign.ExtraBundleKey, "offer_gems" },
            { NoctuaLiveOpsCampaign.ExtraBundle, "{\"sku\":\"pack_499\",\"items\":" + items + "}" },
            { NoctuaLiveOpsCampaign.ExtraDelivery, NoctuaLiveOpsCampaign.DeliveryRewards },
        };

        [UnityTest]
        public IEnumerator Purchase_ReportedTwice_IsDeliveredOnce() => UniTask.ToCoroutine(async () =>
        {
            var delivered = new List<LiveOpsReward>();
            var module = Module();
            module.Configure(Collect(delivered));

            module.RecordPurchaseReward(Order(901, MarkedExtra()), PlayerId);
            module.RecordPurchaseReward(Order(901, MarkedExtra()), PlayerId);
            await module.DeliverRewardsAsync();

            var reward = delivered.Single();
            Assert.AreEqual(RewardSource.Purchase, reward.Source);
            Assert.AreEqual("order:901", reward.ClaimId);
            Assert.AreEqual("summer", reward.CampaignId);
            Assert.AreEqual("pack_499", reward.Key);
            Assert.AreEqual("gem x500", reward.Items.Single().ToString());

            // Reported again in a later session: still not twice.
            var later = new List<LiveOpsReward>();
            var next = Module();
            next.Configure(Collect(later));
            next.RecordPurchaseReward(Order(901, MarkedExtra()), PlayerId);
            await next.DeliverRewardsAsync();
            Assert.IsEmpty(later);
        });

        [Test]
        public void Purchase_WithoutTheMarkerOrWithBrokenItems_RecordsNothing()
        {
            var module = Module();
            var unmarked = MarkedExtra();
            unmarked.Remove(NoctuaLiveOpsCampaign.ExtraDelivery);

            module.RecordPurchaseReward(Order(902, unmarked), PlayerId);
            module.RecordPurchaseReward(Order(903, MarkedExtra("[{\"id\":\"gem\",\"qty\":0}]")), PlayerId);
            module.RecordPurchaseReward(Order(0, MarkedExtra()), PlayerId);

            Assert.IsEmpty(module.UndeliveredRewards);
        }

        [UnityTest]
        public IEnumerator Purchase_HandlerThrows_ThenTheAppRestarts_IsDeliveredThen() => UniTask.ToCoroutine(async () =>
        {
            var first = Module();
            first.Configure(new CampaignHandlers { Rewards = _ => throw new InvalidOperationException("inventory not loaded") });
            first.RecordPurchaseReward(Order(904, MarkedExtra()), PlayerId);
            await first.DeliverRewardsAsync();
            Assert.AreEqual(1, first.UndeliveredRewards.Count, "kept after the handler threw");

            var delivered = new List<LiveOpsReward>();
            var next = Module();
            next.Configure(Collect(delivered));
            await next.DeliverRewardsAsync();
            Assert.AreEqual("order:904", delivered.Single().ClaimId);
        });

        [UnityTest]
        public IEnumerator Purchase_PaidByAnotherAccount_WaitsForThatAccount() => UniTask.ToCoroutine(async () =>
        {
            var delivered = new List<LiveOpsReward>();
            var module = Module();
            module.Configure(Collect(delivered));

            module.RecordPurchaseReward(Order(905, MarkedExtra()), OtherPlayerId);
            await module.DeliverRewardsAsync();
            Assert.IsEmpty(delivered, "the player logged in now did not pay for it");
            StringAssert.Contains("order:905", Saved(OtherPlayerId));

            var buyer = new List<LiveOpsReward>();
            var buyersSession = NewFacade(BaseUrl, new CampaignConfig(), playerId: () => OtherPlayerId);
            buyersSession.Configure(Collect(buyer));
            await buyersSession.DeliverRewardsAsync();
            Assert.AreEqual("order:905", buyer.Single().ClaimId);
        });

        [UnityTest]
        public IEnumerator Purchase_VerifiedBeforeLiveOpsIsReady_IsKeptAndDelivered() => UniTask.ToCoroutine(async () =>
        {
            var early = new NoctuaCampaign();
            early.OnPurchaseDone(Order(906, MarkedExtra()), PlayerId);
            early.OnPurchaseDone(Order(907, new Dictionary<string, string> { { "campaign_id", "x" } }), PlayerId);
            StringAssert.Contains("906", PlayerPrefs.GetString(InboxKey, ""), "kept on the device in case the app dies");
            StringAssert.DoesNotContain("907", PlayerPrefs.GetString(InboxKey, ""), "an order the game delivers is not kept");

            // The app restarts; live ops comes up with the game's handler.
            var delivered = new List<LiveOpsReward>();
            var campaign = new NoctuaCampaign();
            campaign.Configure(Collect(delivered));
            campaign.Attach(Module());
            await campaign.Module.DeliverRewardsAsync();

            Assert.AreEqual("order:906", delivered.Single().ClaimId);
            Assert.IsFalse(PlayerPrefs.HasKey(InboxKey), "the inbox is emptied once recorded");
        });

        [UnityTest]
        public IEnumerator Purchase_AfterLiveOpsIsReady_GoesStraightToTheHandler() => UniTask.ToCoroutine(async () =>
        {
            var delivered = new List<LiveOpsReward>();
            var campaign = new NoctuaCampaign();
            campaign.Configure(Collect(delivered));
            campaign.Attach(Module());

            campaign.OnPurchaseDone(Order(908, MarkedExtra()), PlayerId);
            await campaign.Module.DeliverRewardsAsync();

            Assert.AreEqual("order:908", delivered.Single().ClaimId);
            Assert.IsFalse(PlayerPrefs.HasKey(InboxKey));
        });

        // ---- the game's OnPurchaseDone never sees an order the SDK delivers --------

        private static (NoctuaIAPService iap, List<int> game, List<int> sdk) Iap(Func<OrderRequest, bool> deliveredBySdk)
        {
            var iap = new NoctuaIAPService(
                new NoctuaIAPService.Config { BaseUrl = "https://api.example.com", ClientId = "test-client-id" },
                accessTokenProvider: null, paymentUI: null, nativePlugin: new DefaultNativePlugin());
            var game = new List<int>();
            var sdk = new List<int>();
            iap.OnPurchaseDone += order => game.Add(order.Id);
            iap.OnOrderVerified += order => sdk.Add(order.Id);
            iap.SetDeliveredBySdk(deliveredBySdk);
            return (iap, game, sdk);
        }

        private static void Verified(NoctuaIAPService iap, OrderRequest order) =>
            typeof(NoctuaIAPService).GetMethod("RaisePurchaseDone", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(iap, new object[] { order });

        [Test]
        public void Iap_ABundleTheSdkDelivers_NeverReachesTheGamesOnPurchaseDone()
        {
            var campaign = new NoctuaCampaign();
            var (iap, game, sdk) = Iap(order => campaign.IsDeliveredByRewards(order));

            Verified(iap, Order(911, MarkedExtra()));
            Verified(iap, Order(912, new Dictionary<string, string> { { "campaign_id", "summer" } }, "pack_fixed"));
            Verified(iap, Order(913, null, "shop_pack"));

            CollectionAssert.AreEqual(new[] { 912, 913 }, game, "fixed-SKU and shop orders stay the game's");
            CollectionAssert.AreEqual(new[] { 911, 912, 913 }, sdk, "the SDK's own listeners see every order");
        }

        [Test]
        public void Iap_AnOrderTheSdkCannotRecord_StillReachesTheGame()
        {
            var campaign = new NoctuaCampaign();
            var (iap, game, _) = Iap(order => campaign.IsDeliveredByRewards(order));

            Verified(iap, Order(0, MarkedExtra()));                                        // the Editor's fake order: no id
            Verified(iap, Order(914, MarkedExtra("[{\"id\":\"gem\",\"qty\":0}]")));   // marked, but its items are invalid

            CollectionAssert.AreEqual(new[] { 0, 914 }, game, "never hidden from the game unless the SDK can deliver it");
        }

        [Test]
        public void Iap_ABrokenCheck_HandsTheOrderToTheGame()
        {
            var (iap, game, _) = Iap(_ => throw new InvalidOperationException("boom"));
            Verified(iap, Order(915, MarkedExtra()));
            CollectionAssert.AreEqual(new[] { 915 }, game);
        }

        [Test]
        public void Purchase_Reward_CarriesTheOrder()
        {
            var order = Order(916, MarkedExtra());
            order.Price = 75000;
            order.Currency = "IDR";

            var reward = NoctuaLiveOpsCampaign.PurchaseRewardOf(order);

            Assert.AreEqual(916, reward.OrderId);
            Assert.AreEqual(75000m, reward.Price);
            Assert.AreEqual("IDR", reward.Currency);
            Assert.AreEqual(0, new LiveOpsReward { Source = RewardSource.Mission }.OrderId, "only purchases carry an order");
        }

        // ---- milestone chests from points per mission ---------------------------

        /// <summary>
        /// The fixture's daily missions with points per mission (use_stm 20, craft_badge 10,
        /// defeat_enemies 30) and items on the 25-point chest.
        /// </summary>
        private static CampaignItem MissionsWithPoints(bool points = true)
        {
            var item = JsonConvert.DeserializeObject<CampaignItem>(File.ReadAllText(Path.GetFullPath(FixturePath)));
            item.Id = "season_4";
            item.ProgressScope = Scope;
            item.AutoShow = false;
            item.Frequency = null;
            if (points)
            {
                item.Data["m_use_stm_points"] = "20";
                item.Data["m_craft_badge_points"] = "10";
                item.Data["m_defeat_enemies_points"] = "30";
            }
            item.Data["milestone_25_rewards"] = "[{\"id\":\"chest_bronze\",\"qty\":1},{\"id\":\"gem\",\"qty\":50}]";
            MakeChestBuiltIn(item.View, 25);
            return item;
        }

        /// <summary>The admin's built-in chest: its claim deeplink becomes the SDK's claim action.</summary>
        private static void MakeChestBuiltIn(CampaignNode node, int points)
        {
            if (node == null) return;
            if (node.Action?.Deeplink == $"missions/milestone/{points}")
            {
                node.Action = new CampaignAction { TypeRaw = "claim", Milestone = points.ToString(), Id = $"claim_milestone_{points}" };
            }
            foreach (var child in node.Children ?? new List<CampaignNode>()) MakeChestBuiltIn(child, points);
        }

        private NoctuaLiveOpsCampaign MissionsModule(CampaignItem item, List<LiveOpsReward> delivered)
        {
            var module = NewFacade(BaseUrl, new CampaignConfig { SchemaVersion = 3, Campaigns = new List<CampaignItem> { item } });
            module.Configure(Collect(delivered));
            return module;
        }

        /// <summary>
        /// The tracker as it really answers: a key the player never touched (an unopened chest) has
        /// no row at all.
        /// </summary>
        private void ChestServer(bool stmClaimed, bool chest25Claimed = false)
        {
            var rows = new List<string>
            {
                Row($"{Scope}.use_stm", 20, 20, completed: true, claimed: stmClaimed),
                Row($"{Scope}.craft_badge", 1, 1, completed: true, claimed: true),
                Row($"{Scope}.defeat_enemies", 3, 2000),
            };
            if (chest25Claimed) rows.Add(Row($"{Scope}.milestone_25", 1, 1, completed: true, claimed: true));
            _server.AddHandler(List, _ => Envelope(Rows(rows.ToArray())));
            _server.AddHandler(Stm, _ => Envelope(Row($"{Scope}.use_stm", 20, 20, completed: true, claimed: true)));
            _server.AddHandler(Chest25, _ => Envelope(Row($"{Scope}.milestone_25", 1, 1, completed: true)));
            _server.AddHandler(Chest25Claim, _ => Envelope(Row($"{Scope}.milestone_25", 1, 1, completed: true, claimed: true)));
        }

        [UnityTest]
        public IEnumerator Chest_StateAndPoints_AreFilledForThePopup() => UniTask.ToCoroutine(async () =>
        {
            ChestServer(stmClaimed: true);
            var item = MissionsWithPoints();
            var module = MissionsModule(item, new List<LiveOpsReward>());

            var data = NoctuaLiveOpsCampaign.ToMilestonePlayerData(item, await module.GetProgressAsync());

            Assert.AreEqual("30", data[NoctuaLiveOpsCampaign.PointsKey], "use_stm 20 + craft_badge 10, both claimed");
            Assert.AreEqual("false", data["milestone_25_claimed"], "no progress row yet: unclaimed");
            Assert.IsFalse(data.ContainsKey("milestone_50_claimed"), "the 50 chest has a deeplink: the game fills it");
            Assert.AreEqual("1", data[NoctuaLiveOpsCampaign.ClaimableCountKey], "only the built-in 25 chest");
        });

        [UnityTest]
        public IEnumerator Chest_ReachedByPoints_IsSetClaimedAndDelivered() => UniTask.ToCoroutine(async () =>
        {
            ChestServer(stmClaimed: true);
            var delivered = new List<LiveOpsReward>();
            var module = MissionsModule(MissionsWithPoints(), delivered);

            var reward = await module.ClaimMilestoneRewardAsync("season_4", 25);
            await module.DeliverRewardsAsync();

            Assert.AreEqual(RewardSource.Milestone, reward.Source);
            Assert.AreEqual($"{Scope}.milestone_25", reward.ClaimId);
            CollectionAssert.AreEqual(new[] { "chest_bronze x1", "gem x50" }, delivered.Single().Items.Select(i => i.ToString()).ToArray());
            var sent = Sent(_server);
            Assert.Less(sent.IndexOf("PUT /api/v1" + Chest25), sent.IndexOf("POST /api/v1" + Chest25Claim), "set to 1, then claimed");
        });

        [UnityTest]
        public IEnumerator Chest_BelowItsPoints_IsRefused_WithoutTouchingTheTracker() => UniTask.ToCoroutine(async () =>
        {
            ChestServer(stmClaimed: false);   // only craft_badge claimed: 10 points
            var module = MissionsModule(MissionsWithPoints(), new List<LiveOpsReward>());

            var e = await Throws(async () => await module.ClaimMilestoneRewardAsync("season_4", 25));

            Assert.AreEqual((int)NoctuaErrorCode.LiveOpsProgressNotCompleted, e.ErrorCode);
            CollectionAssert.DoesNotContain(Sent(_server), "PUT /api/v1" + Chest25);
        });

        [UnityTest]
        public IEnumerator Chest_WithADeeplink_IsTheGamesToClaim_EvenWhenReached() => UniTask.ToCoroutine(async () =>
        {
            ChestServer(stmClaimed: true);
            var item = MissionsWithPoints();
            item.Data["m_defeat_enemies_points"] = "0";
            item.Data["m_use_stm_points"] = "60";     // 70 points: past the 50 chest
            var module = MissionsModule(item, new List<LiveOpsReward>());
            await module.GetProgressAsync();

            CollectionAssert.AreEqual(new[] { 25 }, NoctuaLiveOpsCampaign.ClaimableMilestones(item, module.Progress));
            var e = await Throws(async () => await module.ClaimMilestoneRewardAsync("season_4", 50));
            Assert.AreEqual((int)NoctuaErrorCode.Application, e.ErrorCode);
            CollectionAssert.DoesNotContain(Sent(_server), "PUT /api/v1" + Chest50);
        });

        [UnityTest]
        public IEnumerator Chest_ClaimedAlready_IsRefused() => UniTask.ToCoroutine(async () =>
        {
            ChestServer(stmClaimed: true, chest25Claimed: true);
            var delivered = new List<LiveOpsReward>();
            var module = MissionsModule(MissionsWithPoints(), delivered);

            var e = await Throws(async () => await module.ClaimMilestoneRewardAsync("season_4", 25));

            Assert.AreEqual((int)NoctuaErrorCode.LiveOpsProgressAlreadyClaimed, e.ErrorCode);
            Assert.IsEmpty(delivered);
        });

        [UnityTest]
        public IEnumerator Chest_WithoutPointsPerMission_StaysTheGamesJob() => UniTask.ToCoroutine(async () =>
        {
            ChestServer(stmClaimed: true);
            var item = MissionsWithPoints(points: false);
            var module = MissionsModule(item, new List<LiveOpsReward>());

            var data = NoctuaLiveOpsCampaign.ToMilestonePlayerData(item, await module.GetProgressAsync());
            Assert.IsFalse(data.ContainsKey(NoctuaLiveOpsCampaign.PointsKey), "the game keeps supplying points");
            Assert.IsEmpty(NoctuaLiveOpsCampaign.ClaimableMilestones(item, module.Progress));

            var e = await Throws(async () => await module.ClaimMilestoneRewardAsync("season_4", 25));
            Assert.AreEqual((int)NoctuaErrorCode.Application, e.ErrorCode);
        });

        [UnityTest]
        public IEnumerator ClaimAll_ClaimsTheMission_ThenTheChestItUnlocks() => UniTask.ToCoroutine(async () =>
        {
            // Before: craft_badge claimed (10 points); use_stm complete but not claimed (+20).
            ChestServer(stmClaimed: false);
            var delivered = new List<LiveOpsReward>();
            var module = MissionsModule(MissionsWithPoints(), delivered);

            var claimed = await module.ClaimAllMissionsAsync("season_4");
            await module.DeliverRewardsAsync();

            CollectionAssert.AreEqual(new[] { "Mission use_stm", "Milestone 25" },
                claimed.Select(r => $"{r.Source} {r.Key}").ToArray(), "10 + 20 = 30 points reach the 25 chest in the same tap");
            Assert.AreEqual(2, delivered.Count);
            CollectionAssert.DoesNotContain(Sent(_server), "PUT /api/v1" + Chest50, "50 is still out of reach");
        });
    }
}
