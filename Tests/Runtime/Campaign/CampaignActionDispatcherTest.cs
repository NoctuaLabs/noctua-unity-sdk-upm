using System;
using System.Collections.Generic;
using com.noctuagames.sdk;
using com.noctuagames.sdk.LiveOpsCampaign;
using Cysharp.Threading.Tasks;
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
        private List<bool> _busyLog;
        private DateTime _fakeNow;

        [SetUp]
        public void SetUp()
        {
            _events = new MockEventSender();
            _deeplink = _purchasedProduct = null;
            _busyLog = new List<bool>();
            _fakeNow = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            _handlers = new CampaignActionHandlers
            {
                Deeplink = d => _deeplink = d,
                Purchase = (p, _) => _purchasedProduct = p,
            };

            _dispatcher = new CampaignActionDispatcher(_handlers, _events, now: () => _fakeNow);
            _dispatcher.CurrentBusy = b => _busyLog.Add(b);
        }

        /// <summary>Installs an awaitable handler whose completion the test drives by hand.</summary>
        private UniTaskCompletionSource ArmAsyncPurchase(List<string> calls = null)
        {
            var gate = new UniTaskCompletionSource();
            _handlers.PurchaseAsync = (p, _) =>
            {
                calls?.Add(p);
                return gate.Task;
            };
            return gate;
        }

        private void DispatchPurchase(string productId = "p1") =>
            Dispatch(new CampaignAction { TypeRaw = "purchase", ProductId = productId });

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
        public void Deeplink_KeepOpen_ForwardsRouteWithoutClosing()
        {
            var closed = false;
            _dispatcher.CurrentDismiss = () => closed = true;
            Dispatch(new CampaignAction { TypeRaw = "deeplink", Deeplink = "missions/claim/stm", KeepOpen = true });
            Assert.AreEqual("missions/claim/stm", _deeplink);
            Assert.IsFalse(closed, "keep_open must leave the popup up so the game can refresh it");
        }

        [Test]
        public void KeepOpen_DeserializesFromJson()
        {
            var action = Newtonsoft.Json.JsonConvert.DeserializeObject<CampaignAction>(
                "{ \"type\": \"deeplink\", \"deeplink\": \"x\", \"keep_open\": true }");
            Assert.IsTrue(action.KeepOpen);
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
        public void DeeplinkClick_CarriesRoute()
        {
            Dispatch(new CampaignAction { TypeRaw = "deeplink", Deeplink = "event/weekend-tournament" });

            var click = _events.GetEventsByName(CampaignActionDispatcher.ClickEvent)[0];
            Assert.AreEqual("event/weekend-tournament", click.Data["deeplink"]);
        }

        [Test]
        public void NonDeeplinkClick_HasNoRoute()
        {
            Dispatch(new CampaignAction { TypeRaw = "dismiss" });

            var click = _events.GetEventsByName(CampaignActionDispatcher.ClickEvent)[0];
            Assert.IsFalse(click.Data.ContainsKey("deeplink"));
        }

        [Test]
        public void PurchaseClick_CarriesProductAndButtonId()
        {
            Dispatch(new CampaignAction { TypeRaw = "purchase", ProductId = "pass.gold", Id = "offer_1_btn_0" });

            var click = _events.GetEventsByName(CampaignActionDispatcher.ClickEvent)[0];
            Assert.AreEqual("pass.gold", click.Data["product_id"]);
            Assert.AreEqual("offer_1_btn_0", click.Data["button_id"]);
        }

        [Test]
        public void ClickWithoutId_HasNoButtonId_AndNonPurchaseHasNoProduct()
        {
            Dispatch(new CampaignAction { TypeRaw = "deeplink", Deeplink = "pass/details" });

            var click = _events.GetEventsByName(CampaignActionDispatcher.ClickEvent)[0];
            Assert.IsFalse(click.Data.ContainsKey("button_id"));
            Assert.IsFalse(click.Data.ContainsKey("product_id"));
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

        // ── Awaitable purchase handler ────────────────────────────────────────────

        [Test]
        public void PurchaseAsync_WhenSet_TakesPrecedenceOverLegacyHandler()
        {
            var calls = new List<string>();
            ArmAsyncPurchase(calls);

            DispatchPurchase("gold_pack_1");

            CollectionAssert.AreEqual(new[] { "gold_pack_1" }, calls);
            Assert.IsNull(_purchasedProduct, "legacy handler must not also fire — that would double-charge");
        }

        [Test]
        public void PurchaseAsync_BracketsBusyTrueThenFalse()
        {
            var gate = ArmAsyncPurchase();

            DispatchPurchase();
            CollectionAssert.AreEqual(new[] { true }, _busyLog, "busy must latch before the await");

            gate.TrySetResult();
            CollectionAssert.AreEqual(new[] { true, false }, _busyLog);
        }

        [Test]
        public void PurchaseAsync_SecondTapWhileInFlight_Ignored()
        {
            var calls = new List<string>();
            var gate = ArmAsyncPurchase(calls);

            DispatchPurchase();
            DispatchPurchase();
            DispatchPurchase();

            Assert.AreEqual(1, calls.Count, "taps during an in-flight purchase must be dropped");
            gate.TrySetResult();
        }

        [Test]
        public void PurchaseAsync_SuppressedTap_EmitsNoClickEvent()
        {
            var gate = ArmAsyncPurchase();

            DispatchPurchase();
            DispatchPurchase();

            // Guards the suppression check sitting ahead of EmitClick.
            Assert.AreEqual(1, _events.GetEventsByName(CampaignActionDispatcher.ClickEvent).Count);
            gate.TrySetResult();
        }

        [Test]
        public void PurchaseAsync_AfterCompletion_AllowsNextPurchase()
        {
            var calls = new List<string>();
            var gate = ArmAsyncPurchase(calls);

            DispatchPurchase("first");
            gate.TrySetResult();

            var gate2 = ArmAsyncPurchase(calls);
            DispatchPurchase("second");
            gate2.TrySetResult();

            CollectionAssert.AreEqual(new[] { "first", "second" }, calls);
            CollectionAssert.AreEqual(new[] { true, false, true, false }, _busyLog);
        }

        [Test]
        public void PurchaseAsync_HandlerThrowsBeforeAwait_BusyCleared_NoThrow()
        {
            _handlers.PurchaseAsync = (_, __) => throw new InvalidOperationException("boom");

            Assert.DoesNotThrow(() => DispatchPurchase());
            CollectionAssert.AreEqual(new[] { true, false }, _busyLog, "the veil must lift on a synchronous throw");
        }

        [Test]
        public void PurchaseAsync_HandlerThrowsAfterAwait_BusyCleared_AndReleasesInFlight()
        {
            var calls = new List<string>();
            var gate = ArmAsyncPurchase(calls);

            DispatchPurchase();
            gate.TrySetException(new InvalidOperationException("store said no"));

            CollectionAssert.AreEqual(new[] { true, false }, _busyLog);

            // The failed purchase must not leave the CTA permanently dead.
            var gate2 = ArmAsyncPurchase(calls);
            DispatchPurchase();
            Assert.AreEqual(2, calls.Count, "a failed purchase must release the in-flight latch");
            gate2.TrySetResult();
        }

        [Test]
        public void PurchaseAsync_DoesNotCloseThePopup()
        {
            var closed = false;
            _dispatcher.CurrentDismiss = () => closed = true;
            var gate = ArmAsyncPurchase();

            DispatchPurchase();
            Assert.IsFalse(closed, "the offer stays on screen while the purchase runs");

            gate.TrySetException(new InvalidOperationException("cancelled"));
            Assert.IsFalse(closed, "a failed purchase returns the player to the offer");
        }

        [Test]
        public void PurchaseAsync_BusyCallbackIsCapturedAtStart()
        {
            var gate = ArmAsyncPurchase();
            DispatchPurchase();

            // Stands in for the popup closing and a second ShowPopup repointing CurrentBusy.
            var laterPopup = new List<bool>();
            _dispatcher.CurrentBusy = b => laterPopup.Add(b);

            gate.TrySetResult();

            CollectionAssert.AreEqual(new[] { true, false }, _busyLog, "the originating popup gets its own un-busy");
            CollectionAssert.IsEmpty(laterPopup, "a later popup must not be un-busied by an older purchase");
        }

        [Test]
        public void PurchaseAsync_NoBusyCallbackWired_NoThrow()
        {
            _dispatcher.CurrentBusy = null;
            var gate = ArmAsyncPurchase();

            Assert.DoesNotThrow(() =>
            {
                DispatchPurchase();
                gate.TrySetResult();
            });
        }

        [Test]
        public void PurchaseAsync_MissingProductId_NoCall_NoBusy()
        {
            var calls = new List<string>();
            ArmAsyncPurchase(calls);

            Dispatch(new CampaignAction { TypeRaw = "purchase" });

            CollectionAssert.IsEmpty(calls);
            CollectionAssert.IsEmpty(_busyLog, "nothing started, so nothing to veil");
        }

        // ── Legacy fire-and-forget path ───────────────────────────────────────────

        [Test]
        public void Purchase_LegacyHandler_RapidDoubleTap_CallsHandlerOnce()
        {
            var calls = new List<string>();
            _handlers.Purchase = (p, _) => calls.Add(p);

            DispatchPurchase();
            DispatchPurchase();

            Assert.AreEqual(1, calls.Count, "the debounce window eats the second tap");
        }

        [Test]
        public void Purchase_LegacyHandler_AfterDebounceWindow_PurchasesAgain()
        {
            var calls = new List<string>();
            _handlers.Purchase = (p, _) => calls.Add(p);

            DispatchPurchase();
            _fakeNow = _fakeNow.AddMilliseconds(CampaignActionDispatcher.SyncPurchaseDebounceMs + 1);
            DispatchPurchase();

            Assert.AreEqual(2, calls.Count);
        }

        [Test]
        public void Purchase_LegacyHandler_ShowsNoBusyState()
        {
            DispatchPurchase();

            // With no completion signal a veil could never lift, so the legacy path shows none.
            CollectionAssert.IsEmpty(_busyLog);
        }

        // ─── Dismiss reason ────────────────────────────────────────────────────

        [Test]
        public void DismissCta_ExposesReasonOnlyDuringTheClose()
        {
            string seen = null;
            _dispatcher.CurrentDismiss = () => seen = _dispatcher.ActiveDismissReason;

            Dispatch(new CampaignAction { TypeRaw = "dismiss" });

            Assert.AreEqual(CampaignActionDispatcher.DismissReasonCtaDismiss, seen);
            Assert.IsNull(_dispatcher.ActiveDismissReason);
        }

        [Test]
        public void DeeplinkCta_ClosesWithDeeplinkReason()
        {
            string seen = null;
            _dispatcher.CurrentDismiss = () => seen = _dispatcher.ActiveDismissReason;

            Dispatch(new CampaignAction { TypeRaw = "deeplink", Deeplink = "shop" });

            Assert.AreEqual(CampaignActionDispatcher.DismissReasonCtaDeeplink, seen);
            Assert.IsNull(_dispatcher.ActiveDismissReason);
        }

        [Test]
        public void DeeplinkHandlerClosingThePopup_IsStillTheDeeplinkCta()
        {
            // Games are told to call ClosePopup() from their deeplink handler.
            string seenInHandler = null;
            _handlers.Deeplink = _ => seenInHandler = _dispatcher.ActiveDismissReason;

            Dispatch(new CampaignAction { TypeRaw = "deeplink", Deeplink = "shop", KeepOpen = true });

            Assert.AreEqual(CampaignActionDispatcher.DismissReasonCtaDeeplink, seenInHandler);
            Assert.IsNull(_dispatcher.ActiveDismissReason);
        }

        [Test]
        public void KeepOpenDeeplink_DoesNotClose()
        {
            var dismissed = false;
            _dispatcher.CurrentDismiss = () => dismissed = true;

            Dispatch(new CampaignAction { TypeRaw = "deeplink", Deeplink = "claim", KeepOpen = true });

            Assert.IsFalse(dismissed);
        }

        [Test]
        public void ThrowingDismiss_StillClearsReason()
        {
            _dispatcher.CurrentDismiss = () => throw new InvalidOperationException("boom");

            Assert.DoesNotThrow(() => Dispatch(new CampaignAction { TypeRaw = "dismiss" }));
            Assert.IsNull(_dispatcher.ActiveDismissReason);
        }

        [Test]
        public void ReasonIsNullOutsideAnyDispatch()
        {
            Assert.IsNull(_dispatcher.ActiveDismissReason);
        }

        // ─── Purchase result attribution ──────────────────────────────────────

        private PurchaseOutcome Settled(string sku, double secondsAfterTap, string status = "completed",
            int orderId = 0, int errorCode = 0) =>
            new PurchaseOutcome(sku, _fakeNow.AddSeconds(secondsAfterTap), status, orderId, errorCode);

        private List<(string Name, Dictionary<string, IConvertible> Data)> PurchaseResults() =>
            _events.GetEventsByName(CampaignActionDispatcher.PurchaseResultEvent);

        [Test]
        public void CampaignPurchase_Completed_EmitsResultLinkedToCampaign()
        {
            Dispatch(new CampaignAction { TypeRaw = "purchase", ProductId = "gem.100", Id = "offer_1_btn_0" });

            _dispatcher.HandlePurchaseSettled(Settled("gem.100", 1, "completed", orderId: 42));

            var results = PurchaseResults();
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("cmp", results[0].Data["campaign_id"]);
            Assert.AreEqual("gem.100", results[0].Data["product_id"]);
            Assert.AreEqual("completed", results[0].Data["status"]);
            Assert.AreEqual(42, results[0].Data["order_id"]);
            Assert.AreEqual("offer_1_btn_0", results[0].Data["button_id"]);
            Assert.AreEqual(CampaignItem.EngagementPurchase, results[0].Data["engagement_type"]);
            Assert.IsFalse(results[0].Data.ContainsKey("error_code"));
        }

        [Test]
        public void CampaignPurchase_Cancelled_CarriesErrorCodeNotOrderId()
        {
            DispatchPurchase("gem.100");

            _dispatcher.HandlePurchaseSettled(Settled("gem.100", 1, PurchaseOutcome.StatusCancelled,
                errorCode: (int)com.noctuagames.sdk.NoctuaErrorCode.PaymentStatusCanceled));

            var result = PurchaseResults()[0];
            Assert.AreEqual(PurchaseOutcome.StatusCancelled, result.Data["status"]);
            Assert.AreEqual((int)com.noctuagames.sdk.NoctuaErrorCode.PaymentStatusCanceled, result.Data["error_code"]);
            Assert.IsFalse(result.Data.ContainsKey("order_id"));
        }

        [Test]
        public void PurchaseWithoutCampaignTap_EmitsNothing()
        {
            _dispatcher.HandlePurchaseSettled(Settled("gem.100", 1));

            Assert.AreEqual(0, PurchaseResults().Count);
        }

        [Test]
        public void PurchaseOfAnotherSku_EmitsNothing()
        {
            DispatchPurchase("gem.100");

            _dispatcher.HandlePurchaseSettled(Settled("gem.500", 1));

            Assert.AreEqual(0, PurchaseResults().Count);
        }

        [Test]
        public void SecondSettleForSameTap_IsNotCountedTwice()
        {
            // The offline retry dialog re-enters PurchaseItemAsync, so one tap can settle twice.
            DispatchPurchase("gem.100");

            _dispatcher.HandlePurchaseSettled(Settled("gem.100", 1, "completed", orderId: 1));
            _dispatcher.HandlePurchaseSettled(Settled("gem.100", 1, PurchaseOutcome.StatusFailed));

            Assert.AreEqual(1, PurchaseResults().Count);
            Assert.AreEqual("completed", PurchaseResults()[0].Data["status"]);
        }

        [Test]
        public void PurchaseStartedLongAfterTap_IsNotCredited()
        {
            DispatchPurchase("gem.100");

            _dispatcher.HandlePurchaseSettled(
                Settled("gem.100", CampaignActionDispatcher.PurchaseAttributionWindowSeconds + 1));

            Assert.AreEqual(0, PurchaseResults().Count);
        }

        [Test]
        public void PurchaseStartedBeforeTap_IsIgnored_AndTheTapStillCounts()
        {
            DispatchPurchase("gem.100");

            // An older purchase of the same SKU finishing now must not steal the credit.
            _dispatcher.HandlePurchaseSettled(Settled("gem.100", -30, "completed", orderId: 1));
            _dispatcher.HandlePurchaseSettled(Settled("gem.100", 2, "completed", orderId: 2));

            Assert.AreEqual(1, PurchaseResults().Count);
            Assert.AreEqual(2, PurchaseResults()[0].Data["order_id"]);
        }

        [Test]
        public void ExpiredTap_IsPrunedByTheNextTap()
        {
            DispatchPurchase("gem.100");
            _fakeNow = _fakeNow.AddSeconds(CampaignActionDispatcher.PurchaseAttributionWindowSeconds + 5);
            DispatchPurchase("gem.500"); // prunes gem.100

            _dispatcher.HandlePurchaseSettled(Settled("gem.100", 0));

            Assert.AreEqual(0, PurchaseResults().Count);
        }

        [Test]
        public void NullOrEmptyOutcome_DoesNotThrow()
        {
            DispatchPurchase("gem.100");

            Assert.DoesNotThrow(() => _dispatcher.HandlePurchaseSettled(null));
            Assert.DoesNotThrow(() => _dispatcher.HandlePurchaseSettled(new PurchaseOutcome(null, _fakeNow, "completed")));
            Assert.AreEqual(0, PurchaseResults().Count);
        }

        // ─── engagement_type on clicks ────────────────────────────────────────

        [Test]
        public void Click_CarriesEngagementType()
        {
            Dispatch(new CampaignAction { TypeRaw = "dismiss" });

            var click = _events.GetEventsByName(CampaignActionDispatcher.ClickEvent)[0];
            Assert.AreEqual(CampaignItem.EngagementPurchase, click.Data["engagement_type"]);
        }

        [Test]
        public void Click_WithoutEngagementType_OmitsTheKey()
        {
            _dispatcher.Dispatch(new CampaignAction { TypeRaw = "dismiss" }, CampaignFactory.Item("cmp", null, null));

            var click = _events.GetEventsByName(CampaignActionDispatcher.ClickEvent)[0];
            Assert.IsFalse(click.Data.ContainsKey("engagement_type"));
            Assert.AreEqual("cmp", click.Data["campaign_id"]);
        }
    }
}
