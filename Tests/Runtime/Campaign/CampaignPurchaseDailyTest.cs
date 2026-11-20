using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using com.noctuagames.sdk;
using com.noctuagames.sdk.LiveOpsCampaign;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.Runtime.Campaign.LiveOpsProgressTestKit;

namespace Tests.Runtime.Campaign
{
    /// <summary>
    /// <c>purchase_daily</c> campaigns: a completed purchase is recorded on this device for the
    /// player, and the campaign counts as bought until the device's next local midnight. Nothing is
    /// sent to the progress tracker. A SKU the store still reports owned (a non-consumable) stays
    /// owned the next day: only consumables are sold again.
    /// </summary>
    [TestFixture]
    public class CampaignPurchaseDailyTest
    {
        private const string BaseUrl = "http://localhost:7796/api/v1";
        private const string ServerUrl = "http://localhost:7796/api/v1/";
        private const string Id = "daily_deal";
        private const string List = "/progress/42";
        private const string FixturePath =
            "Packages/com.noctuagames.sdk/Tests/Runtime/Campaign/Fixtures/purchase-daily.json";

        private HttpMockServer _server;
        private FakeClock _clock;

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
            ResetServer(_server, new[] { List });
            ClearSaved();
            _clock = new FakeClock { Now = LocalTime(2026, 11, 20, 23, 0) };
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            DestroyUiRoots();
            ClearSaved();
        }

        /// <summary>A device-local wall-clock time as the UTC instant the SDK's clock returns.</summary>
        private static DateTime LocalTime(int year, int month, int day, int hour, int minute) =>
            new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Local).ToUniversalTime();

        private static CampaignItem Offer(bool daily = true)
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
            item.PurchaseDaily = daily;
            return item;
        }

        private NoctuaLiveOpsCampaign Facade(
            CampaignItem item,
            Func<long?> playerId = null,
            Func<string, UniTask<bool>> isOwned = null,
            Func<string, UniTask<bool?>> isNonConsumable = null) =>
            new NoctuaLiveOpsCampaign(
                new CampaignConfig { SchemaVersion = 4, Campaigns = new List<CampaignItem> { item } },
                null, null, new MockEventSender(), () => Array.Empty<string>(),
                progress: new NoctuaLiveOpsCampaign.ProgressTrackerOptions
                {
                    BaseUrl = BaseUrl,
                    AccessTokens = new StubTokens(),
                    PlayerId = playerId ?? (() => PlayerId),
                    TimeoutSeconds = 1,
                    UtcNow = () => _clock.Now,
                    RetryDelay = _ => UniTask.Never(System.Threading.CancellationToken.None),
                },
                isProductOwned: isOwned,
                isNonConsumable: isNonConsumable);

        // ---- the payload ---------------------------------------------------------------

        [Test]
        public void PurchaseDaily_DeserializesFromJson()
        {
            var item = JsonConvert.DeserializeObject<CampaignItem>("{\"id\":\"x\",\"purchase_daily\":true}");
            Assert.IsTrue(item.PurchaseDaily);
            Assert.IsFalse(item.PurchaseOnce);
        }

        [Test]
        public void ContractFixture_IsASchemaV4DailyOffer_ThisSdkAccepts()
        {
            var item = JsonConvert.DeserializeObject<CampaignItem>(File.ReadAllText(Path.GetFullPath(FixturePath)));

            Assert.IsTrue(item.PurchaseDaily);
            Assert.IsFalse(item.PurchaseOnce, "one rule per campaign");
            Assert.AreEqual(4, item.SchemaVersion);
            Assert.AreEqual("cmpurchasedaily0001gems", item.ProgressScope);
            Assert.IsTrue(CampaignValidator.TryValidate(item, out var error), error);
            Assert.LessOrEqual(item.SchemaVersion, CampaignItem.SupportedSchemaVersion, "this SDK renders it");
        }

        // ---- bought today --------------------------------------------------------------

        [UnityTest]
        public IEnumerator Record_HidesTheCampaignToday_WithoutAnyTrackerCall() => UniTask.ToCoroutine(async () =>
        {
            var item = Offer();
            var facade = Facade(item);
            string recorded = null;
            facade.OnCampaignPurchaseRecorded += id => recorded = id;

            Assert.IsFalse(facade.IsCampaignPurchased(item));
            Assert.IsTrue(await facade.RecordCampaignPurchaseAsync(Id, orderId: 77));

            Assert.IsEmpty(Sent(_server), "a daily purchase lives on the device only");
            Assert.IsTrue(facade.IsCampaignPurchased(item));
            Assert.IsTrue(facade.IsFullyOwned(item), "its Buy buttons render Purchased");
            Assert.IsEmpty(facade.GetActiveCampaigns(), "hidden for the rest of the day");
            Assert.AreEqual(Id, recorded);
        });

        [UnityTest]
        public IEnumerator ShowPopup_OfACampaignBoughtToday_ShowsNothing() => UniTask.ToCoroutine(async () =>
        {
            var item = Offer();
            var facade = Facade(item);
            await facade.RecordCampaignPurchaseAsync(Id);

            facade.ShowPopup(Id);

            Assert.IsNull(facade.ShownCampaignId);
        });

        [UnityTest]
        public IEnumerator NextLocalDay_TheCampaignShowsAgain() => UniTask.ToCoroutine(async () =>
        {
            var item = Offer();
            var facade = Facade(item);
            await facade.RecordCampaignPurchaseAsync(Id);   // 23:00 local

            _clock.Now = LocalTime(2026, 11, 20, 23, 59);
            Assert.IsTrue(facade.IsCampaignPurchased(item), "still the same local day");

            _clock.Now = LocalTime(2026, 11, 21, 0, 1);
            Assert.IsFalse(facade.IsCampaignPurchased(item), "a new local day");
            Assert.AreEqual(1, facade.GetActiveCampaigns().Count);
        });

        [UnityTest]
        public IEnumerator ClockMovedBack_TheCampaignStaysHidden() => UniTask.ToCoroutine(async () =>
        {
            var item = Offer();
            var facade = Facade(item);
            await facade.RecordCampaignPurchaseAsync(Id);

            _clock.Now = LocalTime(2026, 11, 19, 12, 0);

            Assert.IsTrue(facade.IsCampaignPurchased(item), "moving the date back does not reopen the offer");
        });

        [UnityTest]
        public IEnumerator BoughtByOnePlayer_AnotherPlayerStillSeesIt() => UniTask.ToCoroutine(async () =>
        {
            long? player = PlayerId;
            var item = Offer();
            var facade = Facade(item, () => player);
            await facade.RecordCampaignPurchaseAsync(Id);

            player = OtherPlayerId;

            Assert.IsFalse(facade.IsCampaignPurchased(item));
        });

        [UnityTest]
        public IEnumerator BoughtToday_SurvivesARelaunch() => UniTask.ToCoroutine(async () =>
        {
            var item = Offer();
            await Facade(item).RecordCampaignPurchaseAsync(Id);

            var relaunched = Facade(item);

            Assert.IsTrue(relaunched.IsCampaignPurchased(item));
        });

        [UnityTest]
        public IEnumerator IsCampaignPurchasedAsync_TrueToday_FalseTomorrow_AndNeverAsksTheTracker() => UniTask.ToCoroutine(async () =>
        {
            var item = Offer();
            var facade = Facade(item);
            await facade.RecordCampaignPurchaseAsync(Id);

            Assert.IsTrue(await facade.IsCampaignPurchasedAsync(Id));
            _clock.Now = LocalTime(2026, 11, 21, 9, 0);
            Assert.IsFalse(await facade.IsCampaignPurchasedAsync(Id));
            Assert.IsEmpty(Sent(_server));
        });

        [UnityTest]
        public IEnumerator BothRules_PurchaseOnceWins() => UniTask.ToCoroutine(async () =>
        {
            var key = Id + NoctuaLiveOpsCampaign.PurchaseKeySuffix;
            _server.AddHandler("/progress/42/" + key, _ => Envelope(Row(key, 1, 1, completed: true)));
            _server.AddHandler("/progress/42/" + key + "/claim", _ => Envelope(Row(key, 1, 1, completed: true, claimed: true)));
            var item = Offer();
            item.PurchaseOnce = true;
            var facade = Facade(item);

            await facade.RecordCampaignPurchaseAsync(Id);
            _clock.Now = LocalTime(2026, 11, 22, 9, 0);

            Assert.IsTrue(facade.IsCampaignPurchased(item), "once is stricter, so it wins");
            Assert.IsTrue(Sent(_server).Any(s => s.EndsWith("/claim")), "and it is claimed on the tracker as usual");
            _server.RemoveHandler("/progress/42/" + key);
            _server.RemoveHandler("/progress/42/" + key + "/claim");
        });

        // ---- consumable vs non-consumable ---------------------------------------------

        [UnityTest]
        public IEnumerator NonConsumable_StillOwnedInTheStore_StaysHiddenTheNextDay() => UniTask.ToCoroutine(async () =>
        {
            var item = Offer();
            var facade = Facade(item, isOwned: _ => UniTask.FromResult(true),
                isNonConsumable: _ => UniTask.FromResult<bool?>(true));
            await facade.RecordCampaignPurchaseAsync(Id);

            _clock.Now = LocalTime(2026, 11, 21, 9, 0);
            await facade.PrefetchOwnershipAsync(item);

            Assert.IsFalse(facade.IsCampaignPurchased(item), "the daily record is over");
            Assert.IsTrue(facade.IsFullyOwned(item), "but the store still owns every SKU, so it stays hidden");
            Assert.IsEmpty(facade.GetActiveCampaigns());
        });

        [UnityTest]
        public IEnumerator MixedOffer_NextDay_Shows_WithItsOwnedCardPurchased() => UniTask.ToCoroutine(async () =>
        {
            var item = Offer();
            var facade = Facade(item, isOwned: sku => UniTask.FromResult(sku == "pack1"),
                isNonConsumable: sku => UniTask.FromResult<bool?>(sku == "pack1"));
            await facade.RecordCampaignPurchaseAsync(Id);

            _clock.Now = LocalTime(2026, 11, 21, 9, 0);
            await facade.PrefetchOwnershipAsync(item);

            Assert.IsFalse(facade.IsFullyOwned(item), "the consumable card can be bought again");
            Assert.IsTrue(facade.Ownership["pack1"], "the non-consumable card reads Purchased");
            Assert.IsFalse(facade.Ownership["pack2"]);
            Assert.AreEqual(1, facade.GetActiveCampaigns().Count);
        });

        // ---- recorded from the verified order --------------------------------------------

        private static OrderRequest CampaignOrder(int id) => new OrderRequest
        {
            Id = id,
            ProductId = "pack1",
            Extra = new Dictionary<string, string> { { NoctuaLiveOpsCampaign.ExtraCampaignId, Id } },
        };

        [UnityTest]
        public IEnumerator VerifiedOrder_OfADailyCampaign_RecordsIt() => UniTask.ToCoroutine(async () =>
        {
            var item = Offer();
            var facade = Facade(item);

            Assert.IsTrue(await facade.RecordOrderPurchaseAsync(CampaignOrder(77), PlayerId));

            Assert.IsTrue(facade.IsCampaignPurchased(item));
            Assert.IsEmpty(Sent(_server));
        });

        [UnityTest]
        public IEnumerator VerifiedOrder_PaidByAnotherPlayer_IsNotRecordedForThisOne() => UniTask.ToCoroutine(async () =>
        {
            var item = Offer();
            var facade = Facade(item);

            Assert.IsFalse(await facade.RecordOrderPurchaseAsync(CampaignOrder(77), OtherPlayerId));

            Assert.IsFalse(facade.IsCampaignPurchased(item));
        });

        [UnityTest]
        public IEnumerator Record_NotADailyCampaign_RecordsNothing() => UniTask.ToCoroutine(async () =>
        {
            var item = Offer(daily: false);
            var facade = Facade(item);

            Assert.IsFalse(await facade.RecordCampaignPurchaseAsync(Id));
            Assert.IsFalse(facade.IsCampaignPurchased(item));
        });

        // ---- auto-show ---------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AutoShow_WaitsForTheLogin_SoABoughtOfferNeverPopsUp() => UniTask.ToCoroutine(async () =>
        {
            long? player = null;
            var item = Offer();
            item.AutoShow = true;
            var facade = Facade(item, () => player);

            Assert.IsFalse(await facade.RunAutoShowAsync(), "before login it cannot tell if this player bought it today");
            Assert.IsNull(facade.ShownCampaignId);
        });
    }
}
