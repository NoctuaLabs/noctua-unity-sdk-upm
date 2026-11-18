using System;
using System.Collections;
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

        private static CampaignNode FixedBuy(string sku) =>
            CampaignFactory.Node(CampaignNode.TypeButton,
                action: new CampaignAction { TypeRaw = "purchase", ProductId = sku });

        private static NoctuaLiveOpsCampaign Facade(params CampaignItem[] campaigns) =>
            new(new CampaignConfig { SchemaVersion = 3, Campaigns = campaigns.ToList() },
                null, null, new MockEventSender(), () => Array.Empty<string>());

        // ---- Noctua.LiveOps facade -----------------------------------------

        [Test]
        public void LiveOps_BeforeInit_ReadsAreEmpty_AndConfigureIsKeptForAttach()
        {
            var liveOps = new NoctuaCampaign();
            var delivered = new List<string>();
            liveOps.Configure(new CampaignHandlers
            {
                Rewards = rewards => { delivered.AddRange(rewards.Select(r => r.ClaimId)); return UniTask.CompletedTask; },
            });

            Assert.IsFalse(liveOps.IsReady);
            Assert.IsEmpty(liveOps.GetActiveCampaigns());
            Assert.IsEmpty(liveOps.Progress);
            Assert.DoesNotThrow(() => { liveOps.ClosePopup(); liveOps.UpdatePopupData(null); });

            var module = Facade(Offer("starter", null, FixedBuy("pack1")));
            liveOps.Attach(module);

            Assert.IsTrue(liveOps.IsReady);
            Assert.AreSame(module, liveOps.Module);
            typeof(NoctuaLiveOpsCampaign).GetMethod("RecordRewards",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(module, new object[] { new List<LiveOpsReward> { new() { ClaimId = "season.m1" } } });
            CollectionAssert.AreEqual(new[] { "season.m1" }, delivered, "handlers set before init reach the module");
        }

        [UnityTest]
        public IEnumerator LiveOps_CallBeforeInit_WaitsForAttach() => UniTask.ToCoroutine(async () =>
        {
            var liveOps = new NoctuaCampaign();
            var call = liveOps.ClaimAllMissionsAsync("nope").Preserve();
            Assert.AreEqual(UniTaskStatus.Pending, call.Status);

            liveOps.Attach(Facade());

            var e = await LiveOpsProgressTestKit.Throws(async () => await call);
            Assert.AreEqual((int)NoctuaErrorCode.Application, e.ErrorCode, "ran against the attached module");
        });

        [UnityTest]
        public IEnumerator LiveOps_InitFailed_WaitingCallsThrow() => UniTask.ToCoroutine(async () =>
        {
            var liveOps = new NoctuaCampaign();
            var call = liveOps.GetProgressAsync().Preserve();

            liveOps.Fail(new NoctuaException(NoctuaErrorCode.Application, "Live ops unavailable"));

            var e = await LiveOpsProgressTestKit.Throws(async () => await call);
            Assert.AreEqual((int)NoctuaErrorCode.Application, e.ErrorCode);
        });

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

        [Test]
        public void Claim_ActionType_CarriesTheMission()
        {
            var action = JsonConvert.DeserializeObject<CampaignAction>("{\"type\":\"claim\",\"mission\":\"use_stm\"}");
            Assert.AreEqual(CampaignActionType.Claim, action.Type);
            Assert.AreEqual("use_stm", action.Mission);
        }

        [Test]
        public void Claim_RunsTheHandlerForItsMission_OneAtATime_AlsoAgainstClaimAll()
        {
            var gate = new UniTaskCompletionSource();
            var calls = new List<string>();
            var busy = new List<bool>();
            var dispatcher = new CampaignActionDispatcher(new CampaignActionHandlers
            {
                ClaimAsync = (_, mission, milestone) => { calls.Add(mission ?? "milestone " + milestone); return gate.Task; },
                ClaimAllAsync = _ => { calls.Add("all"); return UniTask.CompletedTask; },
            }, new MockEventSender()) { CurrentBusy = busy.Add };
            var missions = Offer("season", null);

            dispatcher.Dispatch(new CampaignAction { TypeRaw = "claim", Mission = " use_stm " }, missions);
            dispatcher.Dispatch(new CampaignAction { TypeRaw = "claim", Mission = "use_stm" }, missions); // double tap
            dispatcher.Dispatch(new CampaignAction { TypeRaw = "claim_all" }, missions);
            gate.TrySetResult();

            CollectionAssert.AreEqual(new[] { "use_stm" }, calls, "one claim at a time, mission trimmed");
            CollectionAssert.AreEqual(new[] { true, false }, busy);

            dispatcher.Dispatch(new CampaignAction { TypeRaw = "claim", Milestone = "25" }, missions);
            CollectionAssert.AreEqual(new[] { "use_stm", "milestone 25" }, calls, "a chest button names its points");
        }

        // ---- Campaign info on orders -------------------------------------

        [Test]
        public void CampaignOrderExtra_OnlyForAProductJustTappedInACampaign()
        {
            var item = Offer("starter", null, FixedBuy("pack1"));
            var dispatcher = new CampaignActionDispatcher(new CampaignActionHandlers { PurchaseAsync = (_, __) => new UniTaskCompletionSource().Task },
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
        public void CampaignHandlers_Merge_KeepsEarlierFieldsTheNewSetLeavesOut()
        {
            Action<string> deeplink = _ => { };
            Func<string, UniTask<bool>> owned = _ => UniTask.FromResult(true);

            var merged = CampaignHandlers.Merge(
                new CampaignHandlers { Deeplink = deeplink },
                new CampaignHandlers { OwnershipProvider = owned });

            Assert.AreSame(deeplink, merged.Deeplink);
            Assert.AreSame(owned, merged.OwnershipProvider);
        }

        [Test]
        public void Configure_ReplacesTheRewardsHandlerInsteadOfAddingOne()
        {
            var facade = Facade(Offer("starter", null, FixedBuy("pack1")));
            var calls = new List<string>();

            facade.Configure(new CampaignHandlers
            {
                Rewards = _ => { calls.Add("first"); return UniTask.CompletedTask; },
            });
            facade.Configure(new CampaignHandlers
            {
                Rewards = _ => { calls.Add("second"); return UniTask.CompletedTask; },
            });

            var record = typeof(NoctuaLiveOpsCampaign).GetMethod("RecordRewards",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            record.Invoke(facade, new object[] { new List<LiveOpsReward> { new() { ClaimId = "season.m1" } } });
            CollectionAssert.AreEqual(new[] { "second" }, calls);
        }
    }
}
