using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    [Preserve]
    public class PaymentSettings
    {
        [JsonProperty("payment_type")]
        public PaymentType PaymentType;
    }

    /// <summary>
    /// Product data model returned by product list API.
    /// </summary>
    [Preserve]
    public class Product
    {
        [JsonProperty("id")]
        public string Id;

        [JsonProperty("description")]
        public string Description;

        /// <summary>Game ID owning this product.</summary>
        [JsonProperty("game_id")]
        public int GameId;

        /// <summary>Enabled payment types for this product.</summary>
        [JsonProperty("enabled_payment_types")]
        public PaymentType[] EnabledPaymentTypes;

        /// <summary>Price amount as decimal.</summary>
        [JsonProperty("price")]
        public decimal Price;

        /// <summary>Currency ISO code.</summary>
        [JsonProperty("currency")]
        public string Currency;

        /// <summary>Display price string (localized).</summary>
        [JsonProperty("display_price")]
        public string DisplayPrice;

        /// <summary>Price expressed in USD (string for backward compatibility).</summary>
        [JsonProperty("price_in_usd")]
        public string PriceInUsd;

        /// <summary>Platform identifier.</summary>
        [JsonProperty("platform")]
        public string Platform;
    }

    [JsonArray]
    public class ProductList : List<Product>
    {
    }

    /// <summary>
    /// Request payload to create an order on remote server.
    /// </summary>
    [Preserve]
    public class OrderRequest
    {
        [JsonProperty("id")]
        public int Id;

        [JsonProperty("payment_type")]
        public PaymentType PaymentType;

        [JsonProperty("product_id")]
        public string ProductId;

        [JsonProperty("price")]
        public decimal Price;

        [JsonProperty("currency")]
        public string Currency;

        [JsonProperty("price_in_usd")]
        public decimal PriceInUSD;

        [JsonProperty("role_id")]
        public string RoleId;

        /// <summary>Server id that the player is on.</summary>
        [JsonProperty("server_id")]
        public string ServerId;

        /// <summary>In-game item id.</summary>
        [JsonProperty("ingame_item_id")]
        public string IngameItemId;

        /// <summary>In-game item name.</summary>
        [JsonProperty("ingame_item_name")]
        public string IngameItemName;

        /// <summary>Extra metadata for the order.</summary>
        [JsonProperty("extra")]
        public Dictionary<string, string> Extra;

        /// <summary>Timestamp (string) when the order was created.</summary>
        [JsonProperty("timestamp")]
        public string Timestamp;

        [JsonProperty("allow_payment_type_override")]
        public bool AllowPaymentTypeOverride = true;

        // store_amount and store_currency will serve as placeholder for OnPurchaseDone
        [JsonProperty("store_amount")]
        public string StoreAmount;

        [JsonProperty("store_currency")]
        public string StoreCurrency;
    }

    /// <summary>
    /// Request payload for unpaired purchases (fallback receipts).
    /// </summary>
    [Preserve]
    public class UnpairedPurchaseRequest
    {
        [JsonProperty("receipt_data")]
        public string ReceiptData;

        [JsonProperty("payment_type")]
        public PaymentType PaymentType;

        [JsonProperty("product_id")]
        public string ProductId;

        [JsonProperty("currency")]
        public string Currency;

        [JsonProperty("timestamp")]
        public string Timestamp;
    }

    /// <summary>
    /// Request payload for claiming a redeem code.
    /// </summary>
    [Preserve]
    public class ClaimRedeemCodeRequest
    {
        [JsonProperty("code")]
        public string Code;

        [JsonProperty("user_id")]
        public long UserId;
    }

    /// <summary>
    /// Response payload after claiming a redeem code.
    /// </summary>
    [Preserve]
    public class ClaimRedeemCodeResponse
    {
        [JsonProperty("success")]
        public bool Success;

        [JsonProperty("order_ids")]
        public int[] OrderIds;

        [JsonProperty("message")]
        public string Message;
    }

    /// <summary>
    /// Request payload for redeem order.
    /// </summary>
    [Preserve]
    public class RedeemOrderRequest
    {
        [JsonProperty("product_id")]
        public string ProductId;
    }

    /// <summary>
    /// Response returned when creating an order on server.
    /// </summary>
    [Preserve]
    public class OrderResponse
    {
        [JsonProperty("id")]
        public int Id;

        [JsonProperty("product_id")]
        public string ProductId;

        [JsonProperty("payment_url")]
        public string PaymentUrl;

        [JsonProperty("payment_type")]
        public PaymentType PaymentType;
    }

    /// <summary>
    /// Response payload after unpaired purchase submission.
    /// </summary>
    [Preserve]
    public class UnpairedPurchaseResponse
    {
        /// <summary>Identifier of stored unpaired purchase record.</summary>
        [JsonProperty("id")]
        public int Id;
    }

    /// <summary>
    /// Response payload after redeem order submission.
    /// </summary>
    [Preserve]
    public class RedeemOrderResponse
    {
        /// <summary>Identifier of stored redeem record.</summary>
        [JsonProperty("id")]
        public int Id;
    }

    /// <summary>
    /// Pending deliverables data model.
    /// </summary>
    [Preserve]
    public class PendingDeliverables
    {
        [JsonProperty("order_id")]
        public int OrderId;

        [JsonProperty("payment_type")]
        public PaymentType PaymentType;

        [JsonProperty("status")]
        public string Status;

        [JsonProperty("product_id")]
        public string ProductId;
    }

    /// <summary>
    /// Pending deliverables response data (already unwrapped by HTTP library).
    /// </summary>
    [Preserve]
    public class PendingDeliverablesData
    {
        [JsonProperty("pending_noctua_redeem_orders")]
        public PendingDeliverables[] PendingNoctuaRedeemOrders;
    }

    /// <summary>
    /// Payment outcome enumeration.
    /// </summary>
    public enum PaymentStatus
    {
        Successful,
        Canceled,
        Failed,
        Confirmed,
        ItemAlreadyOwned,
        Pending,
        InvalidPurchaseObject,
        PendingPurchaseOngoing,
        IapNotReady,
    }

    /// <summary>
    /// Result of a local/native payment operation.
    /// </summary>
    public class PaymentResult
    {
        public PaymentStatus Status;
        public string ReceiptId;
        public string ReceiptData;
        public string Message;
    }

    /// <summary>
    /// Request payload used to verify an order on the server.
    /// </summary>
    [Preserve]
    public class VerifyOrderRequest
    {
        [JsonProperty("id")]
        public int Id;

        [JsonProperty("receipt_id")]
        public string ReceiptId;

        [JsonProperty("receipt_data")]
        public string ReceiptData;

        [JsonProperty("trigger")]
        public string Trigger;
    }

    /// <summary>
    /// Order status reported by the server.
    /// </summary>
    [Preserve]
    public enum OrderStatus
    {
        unknown,
        pending,
        completed,
        failed,
        verification_failed,
        delivery_callback_failed,
        error,
        refunded,
        canceled,
        expired,
        invalid,
        voided,
        fallback_to_native_payment
    }

    /// <summary>
    /// Response payload for order verification requests.
    /// </summary>
    [Preserve]
    public class VerifyOrderResponse
    {
        [JsonProperty("id")]
        public int Id;

        [JsonProperty("order_status")]
        public OrderStatus Status;

        [JsonProperty("store_amount")]
        public string StoreAmount;

        [JsonProperty("store_currency")]
        public string StoreCurrency;
    }

    /// <summary>
    /// Purchase request payload sent to server for regular orders.
    /// </summary>
    [Preserve]
    public class PurchaseRequest
    {
        [JsonProperty("product_id")]
        public string ProductId;

        [JsonProperty("price")]
        public decimal Price;

        [JsonProperty("currency")]
        public string Currency;

        [JsonProperty("role_id")]
        public string RoleId;

        [JsonProperty("server_id")]
        public string ServerId;

        [JsonProperty("ingame_item_id")]
        public string IngameItemId;

        [JsonProperty("ingame_item_name")]
        public string IngameItemName;

        [JsonProperty("extra")]
        public Dictionary<string, string> Extra;

    }

    /// <summary>
    /// Response returned after creating a purchase.
    /// </summary>
    [Preserve]
    public class PurchaseResponse
    {
        [JsonProperty("order_id")]
        public int OrderId;

        [JsonProperty("status")]
        public OrderStatus Status;

        [JsonProperty("message")]
        public string Message;
    }

    /// <summary>
    /// Noctua-specific gold/virtual currency data returned for player.
    /// </summary>
    [Preserve]
    public class NoctuaGoldData
    {
        [JsonProperty("vip_level")]
        public double VipLevel;

        [JsonProperty("gold_amount")]
        public double GoldAmount;

        [JsonProperty("bound_gold_amount")]
        public double BoundGoldAmount;

        [JsonProperty("total_gold_amount")]
        public double TotalGoldAmount;

        [JsonProperty("eligible_gold_amount")]
        public double EligibleGoldAmount;
    }

    /// <summary>
    /// Enumeration for what triggered a verify order attempt.
    /// </summary>
    public enum VerifyOrderTrigger
    {
        payment_flow,
        manual_retry,
        client_automatic_retry,
        pending_deliverable,
    }
}
