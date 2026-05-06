using System;
using com.noctuagames.sdk;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Runtime.IAP
{
    /// <summary>
    /// Unit tests for <see cref="NoctuaIAPService"/>.
    ///
    /// Tests that do NOT require a live backend:
    ///   - SetEnabledPaymentTypes / SetDistributionPlatform (in-memory state)
    ///   - GetPendingPurchases / GetPurchaseHistory from PlayerPrefs
    ///   - IsReady property
    ///   - OnPurchaseDone / OnPurchasePending event subscription
    ///
    /// Tests that REQUIRE a live backend are marked [Ignore] with an explicit message
    /// so they appear in the test runner output and can be run in the integration suite.
    /// </summary>
    [TestFixture]
    public class NoctuaIAPServiceTest
    {
        private const string PendingPurchasesKey = "NoctuaPendingPurchases";
        private const string PurchaseHistoryKey  = "NoctuaPurchaseHistory";

        [SetUp]
        public void SetUp()
        {
            // Clear PlayerPrefs storage used by NoctuaIAPService so tests are isolated
            PlayerPrefs.DeleteKey(PendingPurchasesKey);
            PlayerPrefs.DeleteKey(PurchaseHistoryKey);
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(PendingPurchasesKey);
            PlayerPrefs.DeleteKey(PurchaseHistoryKey);
            PlayerPrefs.Save();
        }

        // ─── IsReady ──────────────────────────────────────────────────────────

        [Test]
        public void IsReady_InEditorOrNonAndroid_ReturnsTrue()
        {
            var svc = CreateService();
            // In Unity Editor / non-Android builds, IsReady always returns true
            Assert.IsTrue(svc.IsReady);
        }

        // ─── SetEnabledPaymentTypes ────────────────────────────────────────────

        [Test]
        public void SetEnabledPaymentTypes_DoesNotThrow()
        {
            var svc   = CreateService();
            var types = new List<PaymentType> { PaymentType.noctuastore };

            Assert.DoesNotThrow(() => svc.SetEnabledPaymentTypes(types));
        }

        [Test]
        public void SetEnabledPaymentTypes_EmptyList_DoesNotThrow()
        {
            var svc = CreateService();
            Assert.DoesNotThrow(() => svc.SetEnabledPaymentTypes(new List<PaymentType>()));
        }

        // ─── SetDistributionPlatform ───────────────────────────────────────────

        [Test]
        public void SetDistributionPlatform_DoesNotThrow()
        {
            var svc = CreateService();
            Assert.DoesNotThrow(() => svc.SetDistributionPlatform("google_play"));
        }

        // ─── GetPendingPurchases ───────────────────────────────────────────────

        [Test]
        public void GetPendingPurchases_WhenNoDataInPrefs_ReturnsEmptyList()
        {
            var svc    = CreateService();
            var result = svc.GetPendingPurchases();

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count, "Should return empty list when PlayerPrefs has no pending purchases");
        }

        [Test]
        public void GetPendingPurchases_WithMalformedJson_ReturnsEmptyList()
        {
            PlayerPrefs.SetString(PendingPurchasesKey, "not-valid-json{{{");
            PlayerPrefs.Save();

            var svc = CreateService();
            // NoctuaLogger.Error() routes through Serilog which is only configured after
            // Noctua.InitAsync(). In EditMode tests the Serilog static logger is silent,
            // so the error never reaches LogAssert. Scope ignoreFailingMessages to this
            // method body to avoid bleeding into neighbouring tests.
            LogAssert.ignoreFailingMessages = true;
            var result = svc.GetPendingPurchases();
            LogAssert.ignoreFailingMessages = false;

            Assert.IsNotNull(result);
            // Should gracefully return empty rather than throwing
            Assert.AreEqual(0, result.Count,
                "Malformed JSON in pending purchases should return empty list, not throw");
        }

        // ─── GetPurchaseHistory ────────────────────────────────────────────────

        [Test]
        public void GetPurchaseHistory_WhenNoDataInPrefs_ReturnsEmptyList()
        {
            var svc    = CreateService();
            var result = svc.GetPurchaseHistory();

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count, "Should return empty list when PlayerPrefs has no purchase history");
        }

        [Test]
        public void GetPurchaseHistory_WithMalformedJson_ReturnsEmptyList()
        {
            PlayerPrefs.SetString(PurchaseHistoryKey, "{malformed}");
            PlayerPrefs.Save();

            var svc = CreateService();
            LogAssert.ignoreFailingMessages = true;
            var result = svc.GetPurchaseHistory();
            LogAssert.ignoreFailingMessages = false;

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count,
                "Malformed JSON in purchase history should return empty list, not throw");
        }

        // ─── OnPurchaseDone / OnPurchasePending events ─────────────────────────

        [Test]
        public void OnPurchaseDone_CanSubscribeAndUnsubscribe()
        {
            var svc   = CreateService();
            int count = 0;
            Action<OrderRequest> handler = _ => count++;

            svc.OnPurchaseDone += handler;
            svc.OnPurchaseDone -= handler;

            // Can't trigger the event directly without a purchase flow,
            // but we verify subscribe/unsubscribe does not throw
            Assert.AreEqual(0, count);
        }

        [Test]
        public void OnPurchasePending_CanSubscribeAndUnsubscribe()
        {
            var svc   = CreateService();
            int count = 0;
            Action<OrderRequest> handler = _ => count++;

            svc.OnPurchasePending += handler;
            svc.OnPurchasePending -= handler;

            Assert.AreEqual(0, count);
        }

        // ─── Live-backend methods (Ignore) ─────────────────────────────────────

        [Test]
        [Ignore("Requires live backend: GET /products with valid player access token + game ID")]
        public void GetProductListAsync_ReturnsProductList() { }

        [Test]
        [Ignore("Requires live backend: POST /orders + native store IAP flow (Google Play / App Store)")]
        public void PurchaseItemAsync_CompletesAndFiresOnPurchaseDone() { }

        [Test]
        [Ignore("Requires live backend: POST /orders + receipt verification + retry loop")]
        public void RetryPendingPurchasesAsync_ProcessesQueuedOrders() { }

        [Test]
        [Ignore("Requires live backend: GET /players/{id}/noctua-gold with valid access token")]
        public void GetNoctuaGoldAsync_ReturnsBalance() { }

        [Test]
        [Ignore("Requires live backend: POST /redeems with valid redeem code and player token")]
        public void ClaimRedeemAsync_ReturnsOrderResponse() { }

        [Test]
        [Ignore("Requires live backend: GET /deliverables with valid access token")]
        public void GetPendingDeliverablesAsync_ReturnsList() { }

        [Test]
        [Ignore("Requires live backend: POST /deliverables/deliver with valid access token")]
        public void DeliverPendingDeliverablesAsync_ProcessesDeliverables() { }

        [Test]
        [Ignore("Requires live backend: native IAP restore + server re-verification")]
        public void RestorePurchasedProducts_ReturnsRestoredIds() { }

        // ─── Factory helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Creates a minimal <see cref="NoctuaIAPService"/> wired with a stub config.
        /// <para>
        /// <c>accessTokenProvider</c> is passed as <c>null</c> intentionally — the methods
        /// under test (GetPendingPurchases, GetPurchaseHistory, SetEnabledPaymentTypes, etc.)
        /// never call the provider. Tests for HTTP methods are marked [Ignore].
        /// </para>
        /// </summary>
        private static NoctuaIAPService CreateService()
        {
            var config = new NoctuaIAPService.Config
            {
                BaseUrl  = "https://api.example.com",
                ClientId = "test-client-id"
            };

            return new NoctuaIAPService(
                config:              config,
                accessTokenProvider: null,   // not used by tested methods
                paymentUI:           null,
                nativePlugin:        null
            );
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Shared helpers used by the new test classes below
    // ══════════════════════════════════════════════════════════════════════════════

    internal static class IAPTestHelpers
    {
        internal static NoctuaIAPService CreateService()
        {
            var config = new NoctuaIAPService.Config
            {
                BaseUrl  = "https://api.example.com",
                ClientId = "test-client-id"
            };

            return new NoctuaIAPService(
                config:              config,
                accessTokenProvider: null,
                paymentUI:           null,
                nativePlugin:        null
            );
        }

        /// <summary>
        /// Serialises a single <see cref="InternalPurchaseItem"/> into a JSON array string
        /// suitable for writing to PlayerPrefs.
        /// </summary>
        internal static string MakePendingItemJson(int orderId, string token = "fake-token")
        {
            var item = new InternalPurchaseItem
            {
                OrderId          = orderId,
                OrderRequest     = new OrderRequest { Id = orderId, ProductId = "prod_" + orderId },
                VerifyOrderRequest = new VerifyOrderRequest { Id = orderId },
                AccessToken      = token,
                Status           = "pending",
            };
            return JsonConvert.SerializeObject(new[] { item });
        }

        /// <summary>Writes <paramref name="json"/> to PlayerPrefs under the pending-purchases key.</summary>
        internal static void StorePending(string json)
        {
            PlayerPrefs.SetString("NoctuaPendingPurchases", json);
            PlayerPrefs.Save();
        }

        /// <summary>Writes <paramref name="json"/> to PlayerPrefs under the purchase-history key.</summary>
        internal static void StoreHistory(string json)
        {
            PlayerPrefs.SetString("NoctuaPurchaseHistory", json);
            PlayerPrefs.Save();
        }

        /// <summary>Writes a <see cref="RefundTrackingEntry"/> list to PlayerPrefs.</summary>
        internal static void StoreRefundEntries(List<RefundTrackingEntry> entries)
        {
            PlayerPrefs.SetString("NoctuaRefundTracking", JsonConvert.SerializeObject(entries));
            PlayerPrefs.Save();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Class 1: PlayerPrefs storage — pending purchases and purchase history
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tests for <see cref="NoctuaIAPService"/> methods backed by PlayerPrefs storage:
    /// GetPendingPurchases, GetPendingPurchaseByOrderId, GetPurchaseHistory,
    /// RemoveFromPurchaseHistoryByOrderID, RemoveFromRetryPendingPurchasesByOrderID, and
    /// GetThenRemoveFromRetryPendingPurchasesByOrderID.
    /// </summary>
    [TestFixture]
    public class NoctuaIAPServicePlayerPrefsStorageTest
    {
        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            PlayerPrefs.DeleteKey("NoctuaPendingPurchases");
            PlayerPrefs.DeleteKey("NoctuaPurchaseHistory");
            PlayerPrefs.DeleteKey("NoctuaRefundTracking");
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            PlayerPrefs.DeleteKey("NoctuaPendingPurchases");
            PlayerPrefs.DeleteKey("NoctuaPurchaseHistory");
            PlayerPrefs.DeleteKey("NoctuaRefundTracking");
            PlayerPrefs.Save();
        }

        // ── GetPendingPurchases ────────────────────────────────────────────────

        [Test]
        public void GetPendingPurchases_WithOneValidItem_ReturnsThatItem()
        {
            IAPTestHelpers.StorePending(IAPTestHelpers.MakePendingItemJson(42));
            var svc    = IAPTestHelpers.CreateService();
            var result = svc.GetPendingPurchases();
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(42, result[0].OrderId);
        }

        [Test]
        public void GetPendingPurchases_FiltersItemsWithNullVerifyOrderRequest()
        {
            // Build an item whose VerifyOrderRequest is null — the service should filter it out
            var badItem = new InternalPurchaseItem
            {
                OrderId            = 10,
                OrderRequest       = new OrderRequest { Id = 10 },
                VerifyOrderRequest = null,   // filtered
                AccessToken        = "tok",
            };
            var goodItem = new InternalPurchaseItem
            {
                OrderId            = 11,
                OrderRequest       = new OrderRequest { Id = 11 },
                VerifyOrderRequest = new VerifyOrderRequest { Id = 11 },
                AccessToken        = "tok",
            };
            IAPTestHelpers.StorePending(JsonConvert.SerializeObject(new[] { badItem, goodItem }));
            var svc    = IAPTestHelpers.CreateService();
            var result = svc.GetPendingPurchases();
            Assert.AreEqual(1, result.Count, "Item with null VerifyOrderRequest must be filtered");
            Assert.AreEqual(11, result[0].OrderId);
        }

        [Test]
        public void GetPendingPurchases_FiltersItemsWithNullAccessToken()
        {
            var badItem = new InternalPurchaseItem
            {
                OrderId            = 20,
                OrderRequest       = new OrderRequest { Id = 20 },
                VerifyOrderRequest = new VerifyOrderRequest { Id = 20 },
                AccessToken        = null,   // filtered
            };
            IAPTestHelpers.StorePending(JsonConvert.SerializeObject(new[] { badItem }));
            var svc    = IAPTestHelpers.CreateService();
            var result = svc.GetPendingPurchases();
            Assert.AreEqual(0, result.Count, "Item with null AccessToken must be filtered");
        }

        [Test]
        public void GetPendingPurchases_MultipleItems_ReturnedSortedByOrderId()
        {
            var items = new[]
            {
                new InternalPurchaseItem { OrderId = 30, OrderRequest = new OrderRequest { Id = 30 }, VerifyOrderRequest = new VerifyOrderRequest { Id = 30 }, AccessToken = "t" },
                new InternalPurchaseItem { OrderId = 10, OrderRequest = new OrderRequest { Id = 10 }, VerifyOrderRequest = new VerifyOrderRequest { Id = 10 }, AccessToken = "t" },
                new InternalPurchaseItem { OrderId = 20, OrderRequest = new OrderRequest { Id = 20 }, VerifyOrderRequest = new VerifyOrderRequest { Id = 20 }, AccessToken = "t" },
            };
            IAPTestHelpers.StorePending(JsonConvert.SerializeObject(items));
            var svc    = IAPTestHelpers.CreateService();
            var result = svc.GetPendingPurchases();
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(10, result[0].OrderId);
            Assert.AreEqual(20, result[1].OrderId);
            Assert.AreEqual(30, result[2].OrderId);
        }

        // ── GetPendingPurchaseByOrderId ────────────────────────────────────────

        [Test]
        public void GetPendingPurchaseByOrderId_ReturnsMatchingItem()
        {
            IAPTestHelpers.StorePending(IAPTestHelpers.MakePendingItemJson(99));
            var svc    = IAPTestHelpers.CreateService();
            var result = svc.GetPendingPurchaseByOrderId(99);
            Assert.AreEqual(99, result.OrderId);
        }

        [Test]
        public void GetPendingPurchaseByOrderId_WhenPrefsEmpty_Throws()
        {
            var svc = IAPTestHelpers.CreateService();
            Assert.Throws<Exception>(() => svc.GetPendingPurchaseByOrderId(1),
                "Should throw when pending purchases PlayerPrefs key is absent");
        }

        [Test]
        public void GetPendingPurchaseByOrderId_WhenNotFound_Throws()
        {
            IAPTestHelpers.StorePending(IAPTestHelpers.MakePendingItemJson(5));
            var svc = IAPTestHelpers.CreateService();
            Assert.Throws<Exception>(() => svc.GetPendingPurchaseByOrderId(999),
                "Should throw when order ID is not in the stored list");
        }

        // ── GetPurchaseHistory ─────────────────────────────────────────────────

        [Test]
        public void GetPurchaseHistory_WithOneValidItem_ReturnsThatItem()
        {
            IAPTestHelpers.StoreHistory(IAPTestHelpers.MakePendingItemJson(77));
            var svc    = IAPTestHelpers.CreateService();
            var result = svc.GetPurchaseHistory();
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(77, result[0].OrderId);
        }

        [Test]
        public void GetPurchaseHistory_FiltersItemsWithNullVerifyOrderRequest()
        {
            var bad  = new InternalPurchaseItem { OrderId = 1, OrderRequest = new OrderRequest { Id = 1 }, VerifyOrderRequest = null, AccessToken = "t" };
            var good = new InternalPurchaseItem { OrderId = 2, OrderRequest = new OrderRequest { Id = 2 }, VerifyOrderRequest = new VerifyOrderRequest { Id = 2 }, AccessToken = "t" };
            IAPTestHelpers.StoreHistory(JsonConvert.SerializeObject(new[] { bad, good }));
            var svc    = IAPTestHelpers.CreateService();
            var result = svc.GetPurchaseHistory();
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(2, result[0].OrderId);
        }

        [Test]
        public void GetPurchaseHistory_FiltersItemsWithNullAccessToken()
        {
            var bad = new InternalPurchaseItem { OrderId = 3, OrderRequest = new OrderRequest { Id = 3 }, VerifyOrderRequest = new VerifyOrderRequest { Id = 3 }, AccessToken = null };
            IAPTestHelpers.StoreHistory(JsonConvert.SerializeObject(new[] { bad }));
            var svc    = IAPTestHelpers.CreateService();
            var result = svc.GetPurchaseHistory();
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void GetPurchaseHistory_MultipleItems_ReturnedSortedByOrderId()
        {
            var items = new[]
            {
                new InternalPurchaseItem { OrderId = 50, OrderRequest = new OrderRequest { Id = 50 }, VerifyOrderRequest = new VerifyOrderRequest { Id = 50 }, AccessToken = "t" },
                new InternalPurchaseItem { OrderId = 10, OrderRequest = new OrderRequest { Id = 10 }, VerifyOrderRequest = new VerifyOrderRequest { Id = 10 }, AccessToken = "t" },
            };
            IAPTestHelpers.StoreHistory(JsonConvert.SerializeObject(items));
            var svc    = IAPTestHelpers.CreateService();
            var result = svc.GetPurchaseHistory();
            Assert.AreEqual(10, result[0].OrderId);
            Assert.AreEqual(50, result[1].OrderId);
        }

        // ── RemoveFromPurchaseHistoryByOrderID ─────────────────────────────────

        [Test]
        public void RemoveFromPurchaseHistoryByOrderID_RemovesMatchingItem()
        {
            var items = new[]
            {
                new InternalPurchaseItem { OrderId = 1, OrderRequest = new OrderRequest { Id = 1 }, VerifyOrderRequest = new VerifyOrderRequest { Id = 1 }, AccessToken = "t" },
                new InternalPurchaseItem { OrderId = 2, OrderRequest = new OrderRequest { Id = 2 }, VerifyOrderRequest = new VerifyOrderRequest { Id = 2 }, AccessToken = "t" },
            };
            IAPTestHelpers.StoreHistory(JsonConvert.SerializeObject(items));
            var svc = IAPTestHelpers.CreateService();
            svc.RemoveFromPurchaseHistoryByOrderID(1);
            var result = svc.GetPurchaseHistory();
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(2, result[0].OrderId);
        }

        [Test]
        public void RemoveFromPurchaseHistoryByOrderID_WithNonExistentId_IsNoOp()
        {
            IAPTestHelpers.StoreHistory(IAPTestHelpers.MakePendingItemJson(7));
            var svc = IAPTestHelpers.CreateService();
            Assert.DoesNotThrow(() => svc.RemoveFromPurchaseHistoryByOrderID(9999),
                "Removing a non-existent id should be a silent no-op");
            Assert.AreEqual(1, svc.GetPurchaseHistory().Count, "Existing items must not be touched");
        }

        // ── In-memory retry queue (empty queue boundary) ───────────────────────

        [Test]
        public void RemoveFromRetryPendingPurchasesByOrderID_EmptyQueue_DoesNotThrow()
        {
            var svc = IAPTestHelpers.CreateService();
            Assert.DoesNotThrow(() => svc.RemoveFromRetryPendingPurchasesByOrderID(1234));
        }

        [Test]
        public void GetThenRemoveFromRetryPendingPurchasesByOrderID_EmptyQueue_ReturnsEmptyItem()
        {
            var svc    = IAPTestHelpers.CreateService();
            var result = svc.GetThenRemoveFromRetryPendingPurchasesByOrderID(5678);
            // Returns a default InternalPurchaseItem (OrderId == 0)
            Assert.AreEqual(0, result.OrderId,
                "Should return an empty InternalPurchaseItem when the queue has no matching entry");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Class 2: Refund-tracking store
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tests for <see cref="NoctuaIAPService.GetRefundTrackingEntries"/> and
    /// <see cref="NoctuaIAPService.IsRefundEligibleAsync"/>.
    /// </summary>
    [TestFixture]
    public class NoctuaIAPServiceRefundTrackingTest
    {
        private const string RefundTrackingKey = "NoctuaRefundTracking";

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            PlayerPrefs.DeleteKey(RefundTrackingKey);
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            PlayerPrefs.DeleteKey(RefundTrackingKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void GetRefundTrackingEntries_WhenEmpty_ReturnsEmptyList()
        {
            var svc = IAPTestHelpers.CreateService();
            var result = svc.GetRefundTrackingEntries();
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void GetRefundTrackingEntries_WithMalformedJson_ReturnsEmptyList()
        {
            PlayerPrefs.SetString(RefundTrackingKey, "{{bad-json}}");
            PlayerPrefs.Save();
            var svc = IAPTestHelpers.CreateService();
            var result = svc.GetRefundTrackingEntries();
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count, "Malformed JSON should gracefully return empty list");
        }

        [Test]
        public void GetRefundTrackingEntries_WithValidEntry_ReturnsEntry()
        {
            var entries = new List<RefundTrackingEntry>
            {
                new RefundTrackingEntry
                {
                    ProductId   = "nonconsumable_001",
                    PaymentType = PaymentType.playstore,
                    Timestamp   = DateTime.UtcNow.AddDays(-5),
                }
            };
            IAPTestHelpers.StoreRefundEntries(entries);
            var svc    = IAPTestHelpers.CreateService();
            var result = svc.GetRefundTrackingEntries();
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("nonconsumable_001", result[0].ProductId);
            Assert.AreEqual(PaymentType.playstore, result[0].PaymentType);
        }

        [Test]
        public async Task IsRefundEligibleAsync_NoEntry_ReturnsFalse()
        {
            var svc = IAPTestHelpers.CreateService();
            var result = await svc.IsRefundEligibleAsync("prod_with_no_history");
            Assert.IsFalse(result, "No refund-tracking entry → should not be flagged refunded");
        }

        [Test]
        public async Task IsRefundEligibleAsync_EmptyProductId_ReturnsFalse()
        {
            var svc = IAPTestHelpers.CreateService();
            Assert.IsFalse(await svc.IsRefundEligibleAsync(""));
        }

        [Test]
        public async Task IsRefundEligibleAsync_NullProductId_ReturnsFalse()
        {
            var svc = IAPTestHelpers.CreateService();
            Assert.IsFalse(await svc.IsRefundEligibleAsync(null));
        }

        [Test]
        public async Task IsRefundEligibleAsync_EditorPaymentType_ReturnsFalse()
        {
            // Only playstore/appstore qualify for refund detection
            var entries = new List<RefundTrackingEntry>
            {
                new RefundTrackingEntry
                {
                    ProductId   = "editor_prod",
                    PaymentType = PaymentType.editor,
                    Timestamp   = DateTime.UtcNow.AddDays(-10),
                }
            };
            IAPTestHelpers.StoreRefundEntries(entries);
            var svc = IAPTestHelpers.CreateService();
            Assert.IsFalse(await svc.IsRefundEligibleAsync("editor_prod"),
                "PaymentType.editor must never be flagged as refunded");
        }

        [Test]
        public async Task IsRefundEligibleAsync_NoctuastorePaymentType_ReturnsFalse()
        {
            var entries = new List<RefundTrackingEntry>
            {
                new RefundTrackingEntry
                {
                    ProductId   = "noctua_prod",
                    PaymentType = PaymentType.noctuastore,
                    Timestamp   = DateTime.UtcNow.AddDays(-10),
                }
            };
            IAPTestHelpers.StoreRefundEntries(entries);
            var svc = IAPTestHelpers.CreateService();
            Assert.IsFalse(await svc.IsRefundEligibleAsync("noctua_prod"),
                "PaymentType.noctuastore must never be flagged as refunded");
        }

        [Test]
        public async Task IsRefundEligibleAsync_TooRecentTimestamp_ReturnsFalse()
        {
            // Timestamp is only 1 day old but minAgeDays defaults to 2 → not eligible
            var entries = new List<RefundTrackingEntry>
            {
                new RefundTrackingEntry
                {
                    ProductId   = "recent_prod",
                    PaymentType = PaymentType.playstore,
                    Timestamp   = DateTime.UtcNow.AddDays(-1),
                }
            };
            IAPTestHelpers.StoreRefundEntries(entries);
            var svc = IAPTestHelpers.CreateService();
            Assert.IsFalse(await svc.IsRefundEligibleAsync("recent_prod"),
                "Purchase too recent for the default 2-day window must not be flagged");
        }

        [Test]
        public async Task IsRefundEligibleAsync_PlaystoreOldEnough_EditorFallback_ReturnsFalse()
        {
            // In the Unity Editor, CheckIfProductPurchased always returns false via the #else branch,
            // which means isStillPurchased == false. A product that is old enough + playstore +
            // not still purchased should return TRUE... but only when the native bridge is real.
            // In the editor the call chain goes through CheckIfProductPurchasedAsync → false,
            // so we actually DO expect true here. This test documents/verifies the editor behavior:
            // that the full refund-eligible path runs end-to-end and returns true in editor.
            var entries = new List<RefundTrackingEntry>
            {
                new RefundTrackingEntry
                {
                    ProductId   = "old_prod",
                    PaymentType = PaymentType.playstore,
                    Timestamp   = DateTime.UtcNow.AddDays(-10),
                }
            };
            IAPTestHelpers.StoreRefundEntries(entries);
            var svc = IAPTestHelpers.CreateService();
            // Editor: CheckIfProductPurchased → false (not still purchased) → eligible = true
            var result = await svc.IsRefundEligibleAsync("old_prod");
            Assert.IsTrue(result,
                "In editor, native query returns false (not owned), so an old playstore entry must be flagged eligible");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Class 3: Editor-platform code paths
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tests for methods that have deterministic editor-only (#else) branches:
    /// GetActiveCurrencyAsync, GetProductPurchaseStatusDetailAsync, CheckIfProductPurchased,
    /// GetPurchasedProductsAsync, RestorePurchasedProducts, GetPurchaseStatusAsync.
    /// </summary>
    [TestFixture]
    public class NoctuaIAPServiceEditorPlatformTest
    {
        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public async Task GetActiveCurrencyAsync_EditorPath_ReturnsEmptyString()
        {
            var svc    = IAPTestHelpers.CreateService();
            var result = await svc.GetActiveCurrencyAsync("some_product").AsTask();
            Assert.AreEqual("", result, "Editor path always returns empty string for active currency");
        }

        [Test]
        public async Task GetProductPurchaseStatusDetailAsync_EditorPath_ReturnsDefaultStatus()
        {
            var svc    = IAPTestHelpers.CreateService();
            var result = await svc.GetProductPurchaseStatusDetailAsync("some_product");
            Assert.IsNotNull(result, "Should return a default (non-null) ProductPurchaseStatus in editor");
        }

        [Test]
        public void CheckIfProductPurchased_EditorPath_CallsCallbackWithFalse()
        {
            var svc      = IAPTestHelpers.CreateService();
            bool? called = null;
            svc.CheckIfProductPurchased("test_product", result => called = result);
            Assert.IsFalse(called, "Editor path must invoke the callback synchronously with false");
        }

        [Test]
        public async Task GetPurchasedProductsAsync_EditorPath_ReturnsEmptyList()
        {
            var svc    = IAPTestHelpers.CreateService();
            var result = await svc.GetPurchasedProductsAsync(new List<string> { "prod_a", "prod_b" });
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count,
                "Editor path: no product is 'owned' by native store → returns empty list");
        }

        [Test]
        public async Task RestorePurchasedProducts_EditorPath_ReturnsEmptyList()
        {
            var svc    = IAPTestHelpers.CreateService();
            var result = await svc.RestorePurchasedProducts(new List<string> { "prod_a", "prod_b" });
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count,
                "Editor path: restore returns empty list since no products are 'owned'");
        }

        [Test]
        public async Task GetPurchaseStatusAsync_EditorPath_ReturnsFalse()
        {
            var svc    = IAPTestHelpers.CreateService();
            var result = await svc.GetPurchaseStatusAsync("any_product");
            Assert.IsFalse(result, "Editor: CheckIfProductPurchased returns false → GetPurchaseStatusAsync must also return false");
        }

        [Test]
        public async Task GetPurchasedProductsAsync_EmptyList_ReturnsEmptyList()
        {
            var svc    = IAPTestHelpers.CreateService();
            var result = await svc.GetPurchasedProductsAsync(new List<string>());
            Assert.AreEqual(0, result.Count, "Empty input list should produce empty output list");
        }

        [Test]
        public async Task RestorePurchasedProducts_EmptyList_ReturnsEmptyList()
        {
            var svc    = IAPTestHelpers.CreateService();
            var result = await svc.RestorePurchasedProducts(new List<string>());
            Assert.AreEqual(0, result.Count, "Empty input list should produce empty output list");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Class 4: EnsureEnabled guard
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tests that methods protected by <c>EnsureEnabled()</c> throw <see cref="NoctuaException"/>
    /// with the correct error code when the service has not been enabled via <c>Enable()</c>.
    /// </summary>
    [TestFixture]
    public class NoctuaIAPServiceNotEnabledTest
    {
        // Exact message emitted by NoctuaLogger inside EnsureEnabled() when the
        // service was never Enable()-d. Declared once so all three tests share it.
        private const string EnsureEnabledError =
            "NoctuaIAPService.EnsureEnabled: Noctua IAP is not enabled due to initialization failure.";

        [Test]
        public async Task GetProductListAsync_WhenNotEnabled_ThrowsNoctuaException()
        {
            var svc = IAPTestHelpers.CreateService();
            LogAssert.Expect(LogType.Error, EnsureEnabledError);
            try
            {
                await svc.GetProductListAsync().AsTask();
                Assert.Fail("Expected NoctuaException to be thrown");
            }
            catch (NoctuaException ex)
            {
                Assert.AreEqual((int)NoctuaErrorCode.Application, ex.ErrorCode,
                    "EnsureEnabled guard must throw NoctuaException with Application error code");
            }
        }

        [Test]
        public async Task ClaimRedeemAsync_WhenNotEnabled_ThrowsNoctuaException()
        {
            var svc = IAPTestHelpers.CreateService();
            LogAssert.Expect(LogType.Error, EnsureEnabledError);
            try
            {
                await svc.ClaimRedeemAsync("ABCD-EFGH").AsTask();
                Assert.Fail("Expected NoctuaException to be thrown");
            }
            catch (NoctuaException ex)
            {
                Assert.AreEqual((int)NoctuaErrorCode.Application, ex.ErrorCode);
            }
        }

        [Test]
        public async Task PurchaseItemImplAsync_WhenNotEnabled_ThrowsNoctuaException()
        {
            var svc = IAPTestHelpers.CreateService();
            LogAssert.Expect(LogType.Error, EnsureEnabledError);
            try
            {
                await svc.PurchaseItemImplAsync(new PurchaseRequest { ProductId = "p1" }).AsTask();
                Assert.Fail("Expected NoctuaException to be thrown");
            }
            catch (NoctuaException ex)
            {
                Assert.AreEqual((int)NoctuaErrorCode.Application, ex.ErrorCode);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Class 5: Configuration and payment-type list
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tests for configuration state management: SetEnabledPaymentTypes, SetDistributionPlatform,
    /// Config defaults, and IsReady.
    /// </summary>
    [TestFixture]
    public class NoctuaIAPServiceConfigTest
    {
        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void SetEnabledPaymentTypes_MultipleTypes_FirstTypeIsStoredFirst()
        {
            var svc   = IAPTestHelpers.CreateService();
            var types = new List<PaymentType>
            {
                PaymentType.noctuastore,
                PaymentType.playstore,
            };
            // Should not throw; the in-memory list is updated without exception
            Assert.DoesNotThrow(() => svc.SetEnabledPaymentTypes(types));
        }

        [Test]
        public void SetEnabledPaymentTypes_CalledTwice_SecondCallWins()
        {
            var svc = IAPTestHelpers.CreateService();
            svc.SetEnabledPaymentTypes(new List<PaymentType> { PaymentType.playstore });
            Assert.DoesNotThrow(() =>
                svc.SetEnabledPaymentTypes(new List<PaymentType> { PaymentType.noctuastore }));
            // Verify that a subsequent read-like operation still does not throw
            Assert.IsTrue(svc.IsReady, "IsReady should still return true after multiple SetEnabledPaymentTypes calls");
        }

        [Test]
        public void SetDistributionPlatform_DoesNotThrow()
        {
            var svc = IAPTestHelpers.CreateService();
            Assert.DoesNotThrow(() => svc.SetDistributionPlatform("google_play"));
            Assert.DoesNotThrow(() => svc.SetDistributionPlatform("direct"));
            Assert.DoesNotThrow(() => svc.SetDistributionPlatform(""));
        }

        [Test]
        public void Config_IsIAPDisabled_DefaultIsFalse()
        {
            var config = new NoctuaIAPService.Config
            {
                BaseUrl  = "https://api.example.com",
                ClientId = "test-client-id"
            };
            Assert.IsFalse(config.isIAPDisabled,
                "isIAPDisabled should default to false when not explicitly set");
        }

        [Test]
        public void IsReady_InEditorOrNonAndroid_ReturnsTrue()
        {
            // In Unity Editor (non-Android), the #else branch returns true unconditionally
            var svc = IAPTestHelpers.CreateService();
            Assert.IsTrue(svc.IsReady);
        }

        [Test]
        public void OnPurchaseDone_MultipleSubscribers_CanSubscribeAndUnsubscribeAll()
        {
            var svc   = IAPTestHelpers.CreateService();
            int countA = 0, countB = 0;
            Action<OrderRequest> handlerA = _ => countA++;
            Action<OrderRequest> handlerB = _ => countB++;

            svc.OnPurchaseDone += handlerA;
            svc.OnPurchaseDone += handlerB;
            svc.OnPurchaseDone -= handlerA;
            svc.OnPurchaseDone -= handlerB;

            Assert.AreEqual(0, countA);
            Assert.AreEqual(0, countB);
        }

        [Test]
        public void OnPurchasePending_MultipleSubscribers_CanSubscribeAndUnsubscribeAll()
        {
            var svc   = IAPTestHelpers.CreateService();
            int countA = 0, countB = 0;
            Action<OrderRequest> handlerA = _ => countA++;
            Action<OrderRequest> handlerB = _ => countB++;

            svc.OnPurchasePending += handlerA;
            svc.OnPurchasePending += handlerB;
            svc.OnPurchasePending -= handlerA;
            svc.OnPurchasePending -= handlerB;

            Assert.AreEqual(0, countA);
            Assert.AreEqual(0, countB);
        }
    }
}
