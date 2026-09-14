using System;
using System.Collections.Generic;
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
    }
}
