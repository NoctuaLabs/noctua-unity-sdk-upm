using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace com.noctuagames.sdk.Tests.IAP
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

            var svc    = CreateService();
            var result = svc.GetPendingPurchases();

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

            var svc    = CreateService();
            var result = svc.GetPurchaseHistory();

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
}
