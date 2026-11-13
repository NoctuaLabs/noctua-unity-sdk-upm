using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using com.noctuagames.sdk;
using com.noctuagames.sdk.LiveOpsCampaign;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.Runtime.Campaign.LiveOpsProgressTestKit;

namespace Tests.Runtime.Campaign
{
    /// <summary>
    /// <c>purchase_once</c> campaigns: a completed purchase claims the tracker key
    /// <c>&lt;id&gt;.purchase</c>, after which the campaign counts as bought — hidden, Buy buttons
    /// purchased, further taps blocked.
    /// </summary>
    [TestFixture]
    public class CampaignPurchaseOnceTest
    {
        private const string BaseUrl = "http://localhost:7787/api/v1";
        private const string ServerUrl = "http://localhost:7787/api/v1/";
        private const string Id = "starter";
        private const string Key = Id + NoctuaLiveOpsCampaign.PurchaseKeySuffix;
        private const string List = "/progress/42";
        private const string Put = "/progress/42/" + Key;
        private const string Claim = Put + "/claim";

        // A remote campaign: name-based id, campaign id as progress_scope (what the DAG registers).
        private const string RemoteId = "starter_pack_once";
        private const string RemoteScope = "cmpurchaseonce0001starter";
        private const string ScopedPut = "/progress/42/" + RemoteScope + NoctuaLiveOpsCampaign.PurchaseKeySuffix;
        private const string ScopedClaim = ScopedPut + "/claim";

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
            ResetServer(_server, new[] { List, Put, Claim, ScopedPut, ScopedClaim });
            ClearSaved();
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            DestroyUiRoots();
            ClearSaved();
        }

        private static CampaignItem Offer(bool purchaseOnce = true)
        {
            var item = CampaignFactory.Item(Id, CampaignItem.EngagementPurchase,
                CampaignFactory.Node(CampaignNode.TypeContainer, children: new[]
                {
                    CampaignFactory.Node(CampaignNode.TypeButton,
                        props: new Dictionary<string, object> { { "text", "Buy" } },
                        action: new CampaignAction { TypeRaw = "purchase", ProductId = "pack1" }),
                    CampaignFactory.Node(CampaignNode.TypeButton,
                        props: new Dictionary<string, object> { { "text", "Buy" } },
                        action: new CampaignAction { TypeRaw = "purchase", ProductId = "pack2" }),
                }));
            item.PurchaseOnce = purchaseOnce;
            return item;
        }

        private static NoctuaLiveOpsCampaign Facade(CampaignItem item, string baseUrl = BaseUrl) =>
            NewFacade(baseUrl, new CampaignConfig { SchemaVersion = 2, Campaigns = new List<CampaignItem> { item } });

        private void ClaimWorks()
        {
            _server.AddHandler(Put, _ => Envelope(Row(Key, 1, 1, completed: true)));
            _server.AddHandler(Claim, _ => Envelope(Row(Key, 1, 1, completed: true, claimed: true)));
        }

        [Test]
        public void PurchaseKey_IsTheCampaignIdWithSuffix()
        {
            Assert.AreEqual("starter.purchase", NoctuaLiveOpsCampaign.PurchaseKey(Offer()));
        }

        [Test]
        public void PurchaseKey_UsesTheProgressScope_WhenTheCampaignHasOne()
        {
            var remote = Offer();
            remote.Id = RemoteId;
            remote.ProgressScope = RemoteScope;
            Assert.AreEqual(RemoteScope + ".purchase", NoctuaLiveOpsCampaign.PurchaseKey(remote),
                "the DAG registers the key under the campaign id, not the name-based payload id");
        }

        [UnityTest]
        public IEnumerator Record_RemoteCampaign_ClaimsTheScopedKey_AndReportsItsId() => UniTask.ToCoroutine(async () =>
        {
            var scopedKey = RemoteScope + NoctuaLiveOpsCampaign.PurchaseKeySuffix;
            _server.AddHandler(ScopedPut, _ => Envelope(Row(scopedKey, 1, 1, completed: true)));
            _server.AddHandler(ScopedClaim, _ => Envelope(Row(scopedKey, 1, 1, completed: true, claimed: true)));
            var remote = Offer();
            remote.Id = RemoteId;
            remote.ProgressScope = RemoteScope;
            var facade = Facade(remote);
            string recorded = null;
            facade.OnCampaignPurchaseRecorded += id => recorded = id;

            Assert.IsTrue(await facade.RecordCampaignPurchaseAsync(RemoteId));

            CollectionAssert.AreEqual(new[] { "PUT /api/v1" + ScopedPut, "POST /api/v1" + ScopedClaim }, Sent(_server));
            Assert.IsTrue(facade.IsCampaignPurchased(remote));
            Assert.AreEqual(RemoteId, recorded, "the event names the campaign, not its tracker scope");
        });

        [Test]
        public void PurchaseOnce_DeserializesFromJson()
        {
            var item = Newtonsoft.Json.JsonConvert.DeserializeObject<CampaignItem>("{\"id\":\"x\",\"purchase_once\":true}");
            Assert.IsTrue(item.PurchaseOnce);
        }

        [UnityTest]
        public IEnumerator Record_SetsThenClaimsTheKey_AndTheCampaignCountsAsBought() => UniTask.ToCoroutine(async () =>
        {
            ClaimWorks();
            var item = Offer();
            var facade = Facade(item);
            string recorded = null;
            facade.OnCampaignPurchaseRecorded += id => recorded = id;

            Assert.IsFalse(facade.IsCampaignPurchased(item));
            Assert.IsTrue(await facade.RecordCampaignPurchaseAsync(Id, orderId: 77));

            CollectionAssert.AreEqual(new[] { "PUT /api/v1" + Put, "POST /api/v1" + Claim }, Sent(_server));
            Assert.IsTrue(facade.IsCampaignPurchased(item));
            Assert.IsTrue(facade.IsFullyOwned(item));
            Assert.IsEmpty(facade.GetActiveCampaigns(), "a bought purchase_once campaign is no longer shown");
            Assert.AreEqual(Id, recorded);
        });

        [UnityTest]
        public IEnumerator Record_AlreadyClaimedOnTheTracker_StillCountsAsBought() => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler(Put, _ => Envelope(Row(Key, 1, 1, completed: true)));
            _server.AddHandler(Claim, _ => (409, Error(2400, "Progress already claimed")));
            var item = Offer();
            var facade = Facade(item);

            Assert.IsTrue(await facade.RecordCampaignPurchaseAsync(Id));
            Assert.IsTrue(facade.IsCampaignPurchased(item));
        });

        [UnityTest]
        public IEnumerator Record_TrackerDown_CountsAsBought_AndSurvivesARelaunch() => UniTask.ToCoroutine(async () =>
        {
            var item = Offer();
            var facade = Facade(item, UnreachableBaseUrl);

            Assert.IsTrue(await facade.RecordCampaignPurchaseAsync(Id));
            Assert.IsTrue(facade.IsCampaignPurchased(item), "bought on this device while the tracker is down");
            StringAssert.Contains(Key, Saved(), "the waiting claim is kept on disk");

            var relaunched = Facade(item, UnreachableBaseUrl);
            Assert.IsTrue(relaunched.IsCampaignPurchased(item));
        });

        [UnityTest]
        public IEnumerator Record_TrackerBackUp_FlushClaimsTheWaitingPurchase_AndReportsIt() => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler(Put, Down);
            var item = Offer();
            var facade = Facade(item);
            string recorded = null;
            facade.OnCampaignPurchaseRecorded += id => recorded = id;

            await facade.RecordCampaignPurchaseAsync(Id);
            Assert.IsNull(recorded, "not recorded while the tracker is down");
            Assert.IsTrue(facade.IsCampaignPurchased(item), "but already counts as bought here");

            ResetServer(_server, new[] { Put });
            ClaimWorks();
            await facade.FlushPendingProgressAsync();

            Assert.AreEqual(Id, recorded, "the retried claim reports the purchase too");
            Assert.IsTrue(Sent(_server).Contains("POST /api/v1" + Claim));
            Assert.IsTrue(facade.IsCampaignPurchased(item));
        });

        [UnityTest]
        public IEnumerator ShowPopup_OfABoughtCampaign_ShowsNothing() => UniTask.ToCoroutine(async () =>
        {
            ClaimWorks();
            var item = Offer();
            var facade = Facade(item);
            await facade.RecordCampaignPurchaseAsync(Id);

            facade.ShowPopup(Id);

            Assert.IsNull(facade.ShownCampaignId);
        });

        [UnityTest]
        public IEnumerator Record_KeyRefused_DoesNotLockTheCampaign() => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler(Put, _ => (404, Error(2300, "Progress key is not configured for this game")));
            var item = Offer();
            var facade = Facade(item);

            await facade.RecordCampaignPurchaseAsync(Id);

            Assert.IsFalse(facade.IsCampaignPurchased(item));
            Assert.IsFalse(Saved().Contains("pending_claims\":[\""), "nothing waits for a refused key");
        });

        [UnityTest]
        public IEnumerator Record_IgnoresCampaignsThatAreNotPurchaseOnce() => UniTask.ToCoroutine(async () =>
        {
            var item = Offer(purchaseOnce: false);
            var facade = Facade(item);

            Assert.IsFalse(await facade.RecordCampaignPurchaseAsync(Id));
            Assert.IsEmpty(Sent(_server));
            Assert.IsFalse(facade.IsCampaignPurchased(item));
        });

        [UnityTest]
        public IEnumerator IsCampaignPurchasedAsync_ReadsAClaimFromAnotherDevice() => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler(List, _ => Envelope(Rows(Row(Key, 1, 1, completed: true, claimed: true))));
            var item = Offer();
            var facade = Facade(item);

            Assert.IsTrue(await facade.IsCampaignPurchasedAsync(Id));
            Assert.IsTrue(Sent(_server).Contains("GET /api/v1" + List));
        });

        // ---- Auto-show waits for the player (found on a device: a bought offer popped up greyed out) --

        private const string MultiOfferFixture =
            "Packages/com.noctuagames.sdk/Tests/Runtime/Campaign/Fixtures/multi-offer.json";

        /// <summary>An admin-shaped buy-once offer that renders, with this file's campaign id.</summary>
        private static CampaignItem RenderableBuyOnceOffer()
        {
            var item = Newtonsoft.Json.JsonConvert.DeserializeObject<CampaignItem>(
                System.IO.File.ReadAllText(System.IO.Path.GetFullPath(MultiOfferFixture)));
            item.Id = Id;
            item.ProgressScope = null;
            item.PurchaseOnce = true;
            item.AutoShow = true;
            item.Frequency = null;
            item.Targeting = null;
            item.Schedule = null;
            return item;
        }

        private (NoctuaLiveOpsCampaign facade, UnityEngine.UIElements.PanelSettings panel, Func<long?> setPlayer)
            AutoShowFacade(CampaignItem item, Action<long?> holder)
        {
            long? player = null;
            holder(null);
            var panel = ScriptableObject.CreateInstance<UnityEngine.UIElements.PanelSettings>();
            var facade = NewFacade(BaseUrl,
                new CampaignConfig { SchemaVersion = 3, Campaigns = new List<CampaignItem> { item } },
                playerId: () => player, panel: panel, locale: new NoctuaLocale());
            return (facade, panel, () => { player = PlayerId; return player; });
        }

        [UnityTest]
        public IEnumerator AutoShow_ABoughtOffer_NeverPopsUp_EvenBeforeLogin() => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler(List, _ => Envelope(Rows(Row(Key, 1, 1, completed: true, claimed: true))));
            var item = RenderableBuyOnceOffer();
            var (facade, panel, login) = AutoShowFacade(item, _ => { });
            try
            {
                Assert.IsFalse(await facade.RunAutoShowAsync(), "before login it cannot tell, so it waits");
                Assert.IsNull(facade.ShownCampaignId);

                login();
                await facade.OnAccountChangedAsync(); // auto-show runs again now the player is known

                Assert.IsNull(facade.ShownCampaignId, "bought, so it stays hidden");
                Assert.IsTrue(Sent(_server).Contains("GET /api/v1" + List), "the purchase state was read");
            }
            finally { UnityEngine.Object.DestroyImmediate(panel); }
        });

        [UnityTest]
        public IEnumerator AutoShow_AnUnboughtOffer_ShowsOnceThePlayerIsKnown() => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler(List, _ => Envelope(Rows()));
            var item = RenderableBuyOnceOffer();
            var (facade, panel, login) = AutoShowFacade(item, _ => { });
            try
            {
                Assert.IsFalse(await facade.RunAutoShowAsync());

                login();
                await facade.OnAccountChangedAsync();
                await UniTask.WaitUntil(() => facade.ShownCampaignId != null).Timeout(TimeSpan.FromSeconds(5));

                var reasons = string.Join("; ", facade.Manager.LastResolutions.Select(r => $"{r.Id}:{r.Eligible}:{r.Reason}"));
                Assert.AreEqual(Id, facade.ShownCampaignId, "resolutions: " + reasons + " | sent: " + string.Join(",", Sent(_server)));
            }
            finally { UnityEngine.Object.DestroyImmediate(panel); }
        });

        [Test]
        public void Dispatcher_BlocksABoughtCampaign_AndReportsIt()
        {
            var events = new MockEventSender();
            string purchased = null;
            CampaignItem blocked = null;
            var dispatcher = new CampaignActionDispatcher(new CampaignActionHandlers
            {
                Purchase = (pid, _) => purchased = pid,
                IsPurchaseBlocked = _ => true,
                PurchaseBlocked = item => blocked = item,
            }, events);
            var offer = Offer();

            dispatcher.Dispatch(new CampaignAction { TypeRaw = "purchase", ProductId = "pack1", Id = "btn" }, offer);

            Assert.IsNull(purchased, "no purchase starts");
            Assert.AreSame(offer, blocked);
            var result = events.SentEvents.Single(e => e.Name == CampaignActionDispatcher.PurchaseResultEvent);
            Assert.AreEqual(CampaignActionDispatcher.StatusAlreadyPurchased, result.Data["status"]);
            Assert.AreEqual("btn", result.Data["button_id"]);
        }

        [Test]
        public void Dispatcher_ABrokenBlockCheck_NeverStopsASale()
        {
            string purchased = null;
            var dispatcher = new CampaignActionDispatcher(new CampaignActionHandlers
            {
                Purchase = (pid, _) => purchased = pid,
                IsPurchaseBlocked = _ => throw new InvalidOperationException("boom"),
            }, new MockEventSender());

            dispatcher.Dispatch(new CampaignAction { TypeRaw = "purchase", ProductId = "pack1" }, Offer());

            Assert.AreEqual("pack1", purchased);
        }

        [Test]
        public void Dispatcher_CreditsASettledPurchaseToItsCampaign()
        {
            var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            (string id, PurchaseOutcome outcome) credited = default;
            var dispatcher = new CampaignActionDispatcher(new CampaignActionHandlers
            {
                Purchase = (_, __) => { },
                PurchaseCredited = (id, outcome) => credited = (id, outcome),
            }, new MockEventSender(), now: () => now);

            dispatcher.Dispatch(new CampaignAction { TypeRaw = "purchase", ProductId = "pack1" }, Offer());
            dispatcher.HandlePurchaseSettled(new PurchaseOutcome("pack1", now, "completed", orderId: 9));

            Assert.AreEqual(Id, credited.id);
            Assert.AreEqual(9, credited.outcome.OrderId);
        }

        // ---- recorded from the verified order --------------------------------------

        private const string InboxKey = "Noctua.LiveOps.Campaign.PurchaseInbox";

        private static OrderRequest CampaignOrder(int id, string campaignId = Id) => new OrderRequest
        {
            Id = id,
            ProductId = "pack1",
            Extra = new Dictionary<string, string> { { NoctuaLiveOpsCampaign.ExtraCampaignId, campaignId } },
        };

        [UnityTest]
        public IEnumerator VerifiedOrder_OfAPurchaseOnceCampaign_RecordsIt() => UniTask.ToCoroutine(async () =>
        {
            // The order came back pending and was verified later (pending-purchase retry): only the
            // verified order says the campaign was bought.
            ClaimWorks();
            var item = Offer();
            var facade = Facade(item);

            Assert.IsTrue(await facade.RecordOrderPurchaseAsync(CampaignOrder(77), PlayerId));

            CollectionAssert.AreEqual(new[] { "PUT /api/v1" + Put, "POST /api/v1" + Claim }, Sent(_server));
            Assert.IsTrue(facade.IsCampaignPurchased(item));
        });

        [UnityTest]
        public IEnumerator VerifiedOrder_AlreadyRecorded_IsNotClaimedAgain() => UniTask.ToCoroutine(async () =>
        {
            ClaimWorks();
            var item = Offer();
            var facade = Facade(item);
            await facade.RecordCampaignPurchaseAsync(Id, orderId: 77);
            ResetServer(_server, new[] { List, Put, Claim });
            ClaimWorks();

            await facade.RecordOrderPurchaseAsync(CampaignOrder(77), PlayerId);

            Assert.IsEmpty(Sent(_server), "the campaign already counts as bought");
        });

        [UnityTest]
        public IEnumerator VerifiedOrder_WithoutACampaign_OrOfAnotherKind_RecordsNothing() => UniTask.ToCoroutine(async () =>
        {
            ClaimWorks();
            var item = Offer(purchaseOnce: false);
            var facade = Facade(item);

            Assert.IsFalse(await facade.RecordOrderPurchaseAsync(new OrderRequest { Id = 1, ProductId = "pack1" }, PlayerId));
            Assert.IsFalse(await facade.RecordOrderPurchaseAsync(CampaignOrder(2), PlayerId));
            Assert.IsFalse(await facade.RecordOrderPurchaseAsync(CampaignOrder(3, "unknown"), PlayerId));
            Assert.IsEmpty(Sent(_server));
        });

        [UnityTest]
        public IEnumerator VerifiedOrder_PaidByAnotherPlayer_IsNotRecordedForThisOne() => UniTask.ToCoroutine(async () =>
        {
            ClaimWorks();
            var item = Offer();
            var facade = Facade(item);

            Assert.IsFalse(await facade.RecordOrderPurchaseAsync(CampaignOrder(77), OtherPlayerId));

            Assert.IsEmpty(Sent(_server));
            Assert.IsFalse(facade.IsCampaignPurchased(item));
        });

        [UnityTest]
        public IEnumerator VerifiedOrder_BeforeLiveOpsIsReady_IsRecordedOnceItIs() => UniTask.ToCoroutine(async () =>
        {
            PlayerPrefs.DeleteKey(InboxKey);
            ClaimWorks();
            var item = Offer();

            // Verified during InitAsync (the pending-purchase retry), before live ops is up.
            var early = new NoctuaCampaign();
            early.OnPurchaseDone(CampaignOrder(78), PlayerId);
            StringAssert.Contains("78", PlayerPrefs.GetString(InboxKey, ""), "kept on the device in case the app dies");

            var facade = Facade(item);
            var recorded = false;
            facade.OnCampaignPurchaseRecorded += _ => recorded = true;
            var campaign = new NoctuaCampaign();
            campaign.Attach(facade);

            Assert.IsTrue(campaign.Module.IsCampaignPurchased(item));
            Assert.IsFalse(PlayerPrefs.HasKey(InboxKey), "the inbox is emptied once recorded");

            // Let the claim finish here, so it cannot write into the next test's saved progress.
            await UniTask.WaitUntil(() => recorded).Timeout(TimeSpan.FromSeconds(5));
            CollectionAssert.AreEqual(new[] { "PUT /api/v1" + Put, "POST /api/v1" + Claim }, Sent(_server));
        });
    }
}
