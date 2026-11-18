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
    /// The shared Rewards flow and campaign purchases, case by case: reward items, which bundle an
    /// order carries, every order reaching the game's OnPurchaseDone, a purchase verified before live ops is ready, and milestone chests
    /// claimed from points per mission.
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

        private static NoctuaLiveOpsCampaign Module(params CampaignItem[] campaigns) =>
            NewFacade(BaseUrl, new CampaignConfig { SchemaVersion = 3, Campaigns = campaigns.ToList() });

        private static OrderRequest Order(int id, IReadOnlyDictionary<string, string> extra, string sku = "pack_499") =>
            new OrderRequest { Id = id, ProductId = sku, Extra = extra?.ToDictionary(p => p.Key, p => p.Value) };

        // ---- reward items -----------------------------------------------------

        [Test]
        public void Items_ReadAListForMissions()
        {
            Assert.AreEqual(2, RewardItem.ParseList("[{\"id\":\"gem\",\"qty\":5},{\"id\":\"gold\",\"qty\":9}]").Count);
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
        }

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

        /// <summary>Same file as noctua-admin's reward-items.json and the DAG's: all three must agree.</summary>
        [TestCaseSource(nameof(SharedLists))]
        public void SharedRules_ItemList(string json, bool valid) =>
            Assert.AreEqual(valid, RewardItem.ParseList(json) != null);

        // ---- which bundle an order carries --------------------------------------

        [Test]
        public void Order_FromAFixedSku_CarriesOnlyTheCampaign()
        {
            var item = CampaignFactory.Item("fixed", CampaignItem.EngagementPurchase,
                CampaignFactory.Node(CampaignNode.TypeButton, action: new CampaignAction { TypeRaw = "purchase", ProductId = "pack_499", Id = "buy" }));
            var module = Module(item);

            var dispatcher = (CampaignActionDispatcher)typeof(NoctuaLiveOpsCampaign)
                .GetField("_dispatcher", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(module);
            module.Configure(new CampaignHandlers { Purchase = (_, __) => UniTask.Never(default) });
            dispatcher.Dispatch(item.View.Action, item);

            var extra = module.CampaignOrderExtra("pack_499");
            Assert.AreEqual("fixed", extra[NoctuaLiveOpsCampaign.ExtraCampaignId]);
            Assert.AreEqual(1, extra.Count);
            Assert.IsNull(module.CampaignOrderExtra("shop_pack"), "a purchase no popup started carries nothing");
        }

        // ---- every order reaches the game's OnPurchaseDone ------------------------

        private static Dictionary<string, string> BundleExtra() => new()
        {
            { NoctuaLiveOpsCampaign.ExtraCampaignId, "summer" },
        };

        private static (NoctuaIAPService iap, List<OrderRequest> game, List<int> sdk) Iap()
        {
            var iap = new NoctuaIAPService(
                new NoctuaIAPService.Config { BaseUrl = "https://api.example.com", ClientId = "test-client-id" },
                accessTokenProvider: null, paymentUI: null, nativePlugin: new DefaultNativePlugin());
            var game = new List<OrderRequest>();
            var sdk = new List<int>();
            iap.OnPurchaseDone += order => game.Add(order);
            iap.OnOrderVerified += order => sdk.Add(order.Id);
            return (iap, game, sdk);
        }

        private static void Verified(NoctuaIAPService iap, OrderRequest order) =>
            typeof(NoctuaIAPService).GetMethod("RaisePurchaseDone", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(iap, new object[] { order });

        [Test]
        public void Iap_EveryOrder_ReachesTheGame()
        {
            var (iap, game, sdk) = Iap();

            Verified(iap, Order(911, BundleExtra()));
            Verified(iap, Order(912, new Dictionary<string, string> { { "campaign_id", "summer" } }, "pack_fixed"));
            Verified(iap, Order(913, null, "shop_pack"));

            CollectionAssert.AreEqual(new[] { 911, 912, 913 }, game.Select(o => o.Id).ToArray());
            CollectionAssert.AreEqual(new[] { 911, 912, 913 }, sdk, "the SDK's own listeners see every order first");
            Assert.AreEqual("summer", game[0].CampaignId, "a campaign order keeps its attribution");
        }

        [Test]
        public void Iap_ABrokenSdkListener_StillHandsTheOrderToTheGame()
        {
            var (iap, game, _) = Iap();
            iap.OnOrderVerified += _ => throw new InvalidOperationException("boom");
            Verified(iap, Order(915, BundleExtra()));
            Assert.AreEqual(915, game.Single().Id);
        }

        [Test]
        public void Purchase_NeverBecomesARewardsEntry()
        {
            var delivered = new List<LiveOpsReward>();
            var campaign = new NoctuaCampaign();
            campaign.Configure(Collect(delivered));
            campaign.Attach(Module());

            campaign.OnPurchaseDone(Order(908, BundleExtra()), PlayerId);

            Assert.IsEmpty(campaign.Module.UndeliveredRewards);
            Assert.IsEmpty(delivered, "Rewards is for missions and milestones only");
        }

        [Test]
        public void Purchase_VerifiedBeforeLiveOpsIsReady_IsKeptUntilItIs()
        {
            var early = new NoctuaCampaign();
            early.OnPurchaseDone(Order(906, BundleExtra()), PlayerId);
            early.OnPurchaseDone(Order(907, new Dictionary<string, string> { { "note", "x" } }), PlayerId);
            StringAssert.Contains("906", PlayerPrefs.GetString(InboxKey, ""), "a campaign order is kept for its purchase limit");
            StringAssert.DoesNotContain("907", PlayerPrefs.GetString(InboxKey, ""), "an order outside a campaign is not kept");

            var campaign = new NoctuaCampaign();
            campaign.Attach(Module());
            Assert.IsFalse(PlayerPrefs.HasKey(InboxKey), "the inbox is emptied once live ops is ready");
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
