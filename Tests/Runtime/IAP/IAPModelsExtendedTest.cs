using System.Collections.Generic;
using com.noctuagames.sdk;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Tests.Runtime.IAP
{
    /// <summary>
    /// JSON round-trip and property tests for DTOs in IAPModels.cs not covered
    /// by ModelSerializationTests.  Covers: PaymentSettings, ProductList,
    /// OrderRequest, UnpairedPurchaseRequest, ClaimRedeemCodeRequest/Response,
    /// RedeemOrderRequest/Response, OrderResponse, PendingDeliverables,
    /// PendingDeliverablesData, PaymentStatus, PaymentResult, VerifyOrderRequest,
    /// OrderStatus, VerifyOrderResponse, PurchaseRequest, NoctuaGoldData,
    /// VerifyOrderTrigger.
    /// </summary>
    [TestFixture]
    public class IAPModelsExtendedTest
    {
        // ─── PaymentSettings ─────────────────────────────────────────────────

        [Test]
        public void PaymentSettings_JsonRoundTrip_PreservesPaymentType()
        {
            var original = new PaymentSettings { PaymentType = PaymentType.appstore };
            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<PaymentSettings>(json);
            Assert.AreEqual(PaymentType.appstore, restored.PaymentType);
        }

        // ─── ProductList ──────────────────────────────────────────────────────

        [Test]
        public void ProductList_CanContainMultipleProducts()
        {
            var list = new ProductList
            {
                new Product { Id = "sku_001", Description = "100 Gold", Price = 0.99m, Currency = "USD" },
                new Product { Id = "sku_002", Description = "500 Gold", Price = 3.99m, Currency = "USD" },
            };

            Assert.AreEqual(2, list.Count);
            Assert.AreEqual("sku_001", list[0].Id);
            Assert.AreEqual(3.99m,    list[1].Price);
        }

        [Test]
        public void ProductList_JsonRoundTrip_PreservesCount()
        {
            var list = new ProductList
            {
                new Product { Id = "p1", Currency = "USD", Price = 1.99m },
                new Product { Id = "p2", Currency = "USD", Price = 4.99m },
            };

            var json = JsonConvert.SerializeObject(list);
            var restored = JsonConvert.DeserializeObject<ProductList>(json);

            Assert.AreEqual(2, restored.Count);
            Assert.AreEqual("p1", restored[0].Id);
            Assert.AreEqual("p2", restored[1].Id);
        }

        // ─── OrderRequest ─────────────────────────────────────────────────────

        [Test]
        public void OrderRequest_JsonRoundTrip_PreservesAllFields()
        {
            var original = new OrderRequest
            {
                Id = 0,
                PaymentType = PaymentType.playstore,
                ProductId = "sku_gold_100",
                Price = 0.99m,
                Currency = "USD",
                PriceInUSD = 0.99m,
                RoleId = "mage",
                ServerId = "svr-1",
                IngameItemId = "item_001",
                IngameItemName = "100 Gold",
                Extra = new Dictionary<string, string> { { "promo", "yes" } },
                Timestamp = "2026-01-01T00:00:00Z",
                AllowPaymentTypeOverride = true,
                StoreAmount = "0.99",
                StoreCurrency = "USD",
                CurrentStageLevel = "5"
            };

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<OrderRequest>(json);

            Assert.AreEqual(PaymentType.playstore, restored.PaymentType);
            Assert.AreEqual("sku_gold_100",        restored.ProductId);
            Assert.AreEqual(0.99m,                 restored.Price);
            Assert.AreEqual("USD",                 restored.Currency);
            Assert.AreEqual("mage",                restored.RoleId);
            Assert.AreEqual("svr-1",               restored.ServerId);
            Assert.AreEqual("item_001",            restored.IngameItemId);
            Assert.AreEqual("100 Gold",            restored.IngameItemName);
            Assert.AreEqual("yes",                 restored.Extra["promo"]);
            Assert.AreEqual("2026-01-01T00:00:00Z", restored.Timestamp);
            Assert.IsTrue(restored.AllowPaymentTypeOverride);
            Assert.AreEqual("0.99",                restored.StoreAmount);
            Assert.AreEqual("USD",                 restored.StoreCurrency);
            Assert.AreEqual("5",                   restored.CurrentStageLevel);
        }

        [Test]
        public void OrderRequest_AllowPaymentTypeOverride_DefaultTrue()
        {
            // Default value is true per field initializer in the class
            var req = new OrderRequest();
            Assert.IsTrue(req.AllowPaymentTypeOverride);
        }

        // ─── UnpairedPurchaseRequest ──────────────────────────────────────────

        [Test]
        public void UnpairedPurchaseRequest_JsonRoundTrip_PreservesAllFields()
        {
            var original = new UnpairedPurchaseRequest
            {
                ReceiptData = "receipt-token-abc",
                PaymentType = PaymentType.appstore,
                ProductId = "sku_999",
                Currency = "USD",
                Timestamp = "2026-05-01T12:00:00Z"
            };

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<UnpairedPurchaseRequest>(json);

            Assert.AreEqual("receipt-token-abc",    restored.ReceiptData);
            Assert.AreEqual(PaymentType.appstore,   restored.PaymentType);
            Assert.AreEqual("sku_999",              restored.ProductId);
            Assert.AreEqual("USD",                  restored.Currency);
            Assert.AreEqual("2026-05-01T12:00:00Z", restored.Timestamp);
        }

        // ─── ClaimRedeemCodeRequest / Response ────────────────────────────────

        [Test]
        public void ClaimRedeemCodeRequest_JsonRoundTrip_PreservesAllFields()
        {
            var original = new ClaimRedeemCodeRequest { Code = "PROMO2026", UserId = 42L };
            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<ClaimRedeemCodeRequest>(json);
            Assert.AreEqual("PROMO2026", restored.Code);
            Assert.AreEqual(42L,         restored.UserId);
        }

        [Test]
        public void ClaimRedeemCodeResponse_JsonRoundTrip_PreservesAllFields()
        {
            var original = new ClaimRedeemCodeResponse
            {
                Success = true,
                OrderIds = new[] { 101, 102 },
                Message = "Claimed successfully"
            };
            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<ClaimRedeemCodeResponse>(json);
            Assert.IsTrue(restored.Success);
            Assert.AreEqual(2, restored.OrderIds.Length);
            Assert.AreEqual(101, restored.OrderIds[0]);
            Assert.AreEqual("Claimed successfully", restored.Message);
        }

        // ─── RedeemOrderRequest / Response ───────────────────────────────────

        [Test]
        public void RedeemOrderRequest_JsonRoundTrip()
        {
            var original = new RedeemOrderRequest { ProductId = "redeem_sku" };
            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<RedeemOrderRequest>(json);
            Assert.AreEqual("redeem_sku", restored.ProductId);
        }

        [Test]
        public void RedeemOrderResponse_JsonRoundTrip()
        {
            var original = new RedeemOrderResponse { Id = 77 };
            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<RedeemOrderResponse>(json);
            Assert.AreEqual(77, restored.Id);
        }

        // ─── OrderResponse ────────────────────────────────────────────────────

        [Test]
        public void OrderResponse_JsonRoundTrip_PreservesAllFields()
        {
            var original = new OrderResponse
            {
                Id = 55,
                ProductId = "sku_pack",
                PaymentUrl = "https://pay.noctua.gg/order/55",
                PaymentType = PaymentType.noctuastore
            };

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<OrderResponse>(json);

            Assert.AreEqual(55, restored.Id);
            Assert.AreEqual("sku_pack",                  restored.ProductId);
            Assert.AreEqual("https://pay.noctua.gg/order/55", restored.PaymentUrl);
            Assert.AreEqual(PaymentType.noctuastore,     restored.PaymentType);
        }

        // ─── UnpairedPurchaseResponse ─────────────────────────────────────────

        [Test]
        public void UnpairedPurchaseResponse_JsonRoundTrip()
        {
            var original = new UnpairedPurchaseResponse { Id = 33 };
            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<UnpairedPurchaseResponse>(json);
            Assert.AreEqual(33, restored.Id);
        }

        // ─── PendingDeliverables / PendingDeliverablesData ────────────────────

        [Test]
        public void PendingDeliverables_JsonRoundTrip_PreservesAllFields()
        {
            var original = new PendingDeliverables
            {
                OrderId = 88,
                PaymentType = PaymentType.noctuastore_redeem,
                Status = "pending",
                ProductId = "redeem_sku"
            };

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<PendingDeliverables>(json);

            Assert.AreEqual(88,                              restored.OrderId);
            Assert.AreEqual(PaymentType.noctuastore_redeem,  restored.PaymentType);
            Assert.AreEqual("pending",                       restored.Status);
            Assert.AreEqual("redeem_sku",                    restored.ProductId);
        }

        [Test]
        public void PendingDeliverablesData_JsonRoundTrip_PreservesArray()
        {
            var original = new PendingDeliverablesData
            {
                PendingNoctuaRedeemOrders = new[]
                {
                    new PendingDeliverables { OrderId = 1, ProductId = "sku_a" },
                    new PendingDeliverables { OrderId = 2, ProductId = "sku_b" },
                }
            };

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<PendingDeliverablesData>(json);

            Assert.AreEqual(2, restored.PendingNoctuaRedeemOrders.Length);
            Assert.AreEqual(1, restored.PendingNoctuaRedeemOrders[0].OrderId);
            Assert.AreEqual("sku_b", restored.PendingNoctuaRedeemOrders[1].ProductId);
        }

        // ─── PaymentStatus ────────────────────────────────────────────────────

        [Test]
        public void PaymentStatus_AllValues_Exist()
        {
            var values = System.Enum.GetValues(typeof(PaymentStatus));
            Assert.AreEqual(9, values.Length);
            Assert.IsTrue(System.Enum.IsDefined(typeof(PaymentStatus), PaymentStatus.Successful));
            Assert.IsTrue(System.Enum.IsDefined(typeof(PaymentStatus), PaymentStatus.Canceled));
            Assert.IsTrue(System.Enum.IsDefined(typeof(PaymentStatus), PaymentStatus.Failed));
            Assert.IsTrue(System.Enum.IsDefined(typeof(PaymentStatus), PaymentStatus.Confirmed));
            Assert.IsTrue(System.Enum.IsDefined(typeof(PaymentStatus), PaymentStatus.ItemAlreadyOwned));
            Assert.IsTrue(System.Enum.IsDefined(typeof(PaymentStatus), PaymentStatus.Pending));
            Assert.IsTrue(System.Enum.IsDefined(typeof(PaymentStatus), PaymentStatus.InvalidPurchaseObject));
            Assert.IsTrue(System.Enum.IsDefined(typeof(PaymentStatus), PaymentStatus.PendingPurchaseOngoing));
            Assert.IsTrue(System.Enum.IsDefined(typeof(PaymentStatus), PaymentStatus.IapNotReady));
        }

        // ─── PaymentResult ────────────────────────────────────────────────────

        [Test]
        public void PaymentResult_Fields_CanBeSetAndRead()
        {
            var result = new PaymentResult
            {
                Status = PaymentStatus.Successful,
                ReceiptId = "rcpt-001",
                ReceiptData = "base64data",
                PurchaseToken = "token-xyz",
                Message = "OK"
            };

            Assert.AreEqual(PaymentStatus.Successful, result.Status);
            Assert.AreEqual("rcpt-001",   result.ReceiptId);
            Assert.AreEqual("base64data", result.ReceiptData);
            Assert.AreEqual("token-xyz",  result.PurchaseToken);
            Assert.AreEqual("OK",         result.Message);
        }

        // ─── VerifyOrderRequest ───────────────────────────────────────────────

        [Test]
        public void VerifyOrderRequest_JsonRoundTrip_PreservesAllFields()
        {
            var original = new VerifyOrderRequest
            {
                Id = 99,
                ReceiptId = "rcpt-abc",
                ReceiptData = "encoded-receipt",
                Trigger = "payment_flow"
            };

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<VerifyOrderRequest>(json);

            Assert.AreEqual(99,              restored.Id);
            Assert.AreEqual("rcpt-abc",      restored.ReceiptId);
            Assert.AreEqual("encoded-receipt", restored.ReceiptData);
            Assert.AreEqual("payment_flow",  restored.Trigger);
        }

        // ─── OrderStatus ──────────────────────────────────────────────────────

        [Test]
        public void OrderStatus_AllValues_Exist()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(OrderStatus), OrderStatus.unknown));
            Assert.IsTrue(System.Enum.IsDefined(typeof(OrderStatus), OrderStatus.pending));
            Assert.IsTrue(System.Enum.IsDefined(typeof(OrderStatus), OrderStatus.completed));
            Assert.IsTrue(System.Enum.IsDefined(typeof(OrderStatus), OrderStatus.failed));
            Assert.IsTrue(System.Enum.IsDefined(typeof(OrderStatus), OrderStatus.verification_failed));
            Assert.IsTrue(System.Enum.IsDefined(typeof(OrderStatus), OrderStatus.delivery_callback_failed));
            Assert.IsTrue(System.Enum.IsDefined(typeof(OrderStatus), OrderStatus.error));
            Assert.IsTrue(System.Enum.IsDefined(typeof(OrderStatus), OrderStatus.refunded));
            Assert.IsTrue(System.Enum.IsDefined(typeof(OrderStatus), OrderStatus.canceled));
            Assert.IsTrue(System.Enum.IsDefined(typeof(OrderStatus), OrderStatus.expired));
            Assert.IsTrue(System.Enum.IsDefined(typeof(OrderStatus), OrderStatus.invalid));
            Assert.IsTrue(System.Enum.IsDefined(typeof(OrderStatus), OrderStatus.voided));
            Assert.IsTrue(System.Enum.IsDefined(typeof(OrderStatus), OrderStatus.fallback_to_native_payment));
        }

        // ─── VerifyOrderResponse ──────────────────────────────────────────────

        [Test]
        public void VerifyOrderResponse_JsonRoundTrip_PreservesAllFields()
        {
            var original = new VerifyOrderResponse
            {
                Id = 42,
                Status = OrderStatus.completed,
                StoreAmount = "0.99",
                StoreCurrency = "USD"
            };

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<VerifyOrderResponse>(json);

            Assert.AreEqual(42,                   restored.Id);
            Assert.AreEqual(OrderStatus.completed, restored.Status);
            Assert.AreEqual("0.99",               restored.StoreAmount);
            Assert.AreEqual("USD",                restored.StoreCurrency);
        }

        // ─── PurchaseRequest ──────────────────────────────────────────────────

        [Test]
        public void PurchaseRequest_JsonRoundTrip_PreservesAllFields()
        {
            var original = new PurchaseRequest
            {
                ProductId = "sku_diamond",
                Price = 9.99m,
                Currency = "USD",
                RoleId = "knight",
                ServerId = "svr-eu",
                IngameItemId = "diamond_pack",
                IngameItemName = "500 Diamonds",
                Extra = new Dictionary<string, string> { { "campaign", "summer" } }
            };

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<PurchaseRequest>(json);

            Assert.AreEqual("sku_diamond",   restored.ProductId);
            Assert.AreEqual(9.99m,           restored.Price);
            Assert.AreEqual("USD",           restored.Currency);
            Assert.AreEqual("knight",        restored.RoleId);
            Assert.AreEqual("svr-eu",        restored.ServerId);
            Assert.AreEqual("diamond_pack",  restored.IngameItemId);
            Assert.AreEqual("500 Diamonds",  restored.IngameItemName);
            Assert.AreEqual("summer",        restored.Extra["campaign"]);
        }

        // ─── NoctuaGoldData ───────────────────────────────────────────────────

        [Test]
        public void NoctuaGoldData_JsonRoundTrip_PreservesAllFields()
        {
            var original = new NoctuaGoldData
            {
                VipLevel = 3.5,
                GoldAmount = 1000.0,
                BoundGoldAmount = 250.0,
                TotalGoldAmount = 1250.0,
                EligibleGoldAmount = 900.0
            };

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<NoctuaGoldData>(json);

            Assert.AreEqual(3.5,    restored.VipLevel);
            Assert.AreEqual(1000.0, restored.GoldAmount);
            Assert.AreEqual(250.0,  restored.BoundGoldAmount);
            Assert.AreEqual(1250.0, restored.TotalGoldAmount);
            Assert.AreEqual(900.0,  restored.EligibleGoldAmount);
        }

        // ─── VerifyOrderTrigger ───────────────────────────────────────────────

        [Test]
        public void VerifyOrderTrigger_AllValues_Exist()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(VerifyOrderTrigger), VerifyOrderTrigger.payment_flow));
            Assert.IsTrue(System.Enum.IsDefined(typeof(VerifyOrderTrigger), VerifyOrderTrigger.manual_retry));
            Assert.IsTrue(System.Enum.IsDefined(typeof(VerifyOrderTrigger), VerifyOrderTrigger.client_automatic_retry));
            Assert.IsTrue(System.Enum.IsDefined(typeof(VerifyOrderTrigger), VerifyOrderTrigger.pending_deliverable));
        }
    }
}
