using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Settings for the active payment method of the current user.
    /// </summary>
    [Preserve]
    public class PaymentSettings
    {
        /// <summary>The payment type configured for this user.</summary>
        [JsonProperty("payment_type")]
        public PaymentType PaymentType;
    }

    /// <summary>
    /// Product data model returned by product list API.
    /// </summary>
    [Preserve]
    public class Product
    {
        /// <summary>Unique product identifier.</summary>
        [JsonProperty("id")]
        public string Id;

        /// <summary>Human-readable product description.</summary>
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

    /// <summary>
    /// A JSON-serializable list of <see cref="Product"/> items returned by the product list API.
    /// </summary>
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
        /// <summary>Server-assigned order identifier (0 for new orders).</summary>
        [JsonProperty("id")]
        public int Id;

        /// <summary>Payment channel to use for this order.</summary>
        [JsonProperty("payment_type")]
        public PaymentType PaymentType;

        /// <summary>Product identifier being purchased.</summary>
        [JsonProperty("product_id")]
        public string ProductId;

        /// <summary>Price amount in the specified currency.</summary>
        [JsonProperty("price")]
        public decimal Price;

        /// <summary>ISO currency code for the price.</summary>
        [JsonProperty("currency")]
        public string Currency;

        /// <summary>Price amount converted to USD.</summary>
        [JsonProperty("price_in_usd")]
        public decimal PriceInUSD;

        /// <summary>In-game role identifier of the purchasing player.</summary>
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

        /// <summary>When true, the server may override the requested payment type based on user/region settings.</summary>
        [JsonProperty("allow_payment_type_override")]
        public bool AllowPaymentTypeOverride = true;

        // store_amount and store_currency will serve as placeholder for OnPurchaseDone
        /// <summary>Store-reported price amount (populated after native store purchase).</summary>
        [JsonProperty("store_amount")]
        public string StoreAmount;

        /// <summary>Store-reported currency code (populated after native store purchase).</summary>
        [JsonProperty("store_currency")]
        public string StoreCurrency;
    }

    /// <summary>
    /// Request payload for unpaired purchases (fallback receipts).
    /// </summary>
    [Preserve]
    public class UnpairedPurchaseRequest
    {
        /// <summary>Native store receipt data for server-side verification.</summary>
        [JsonProperty("receipt_data")]
        public string ReceiptData;

        /// <summary>Payment channel used for this purchase.</summary>
        [JsonProperty("payment_type")]
        public PaymentType PaymentType;

        /// <summary>Product identifier of the purchased item.</summary>
        [JsonProperty("product_id")]
        public string ProductId;

        /// <summary>ISO currency code of the purchase.</summary>
        [JsonProperty("currency")]
        public string Currency;

        /// <summary>ISO 8601 timestamp of the purchase.</summary>
        [JsonProperty("timestamp")]
        public string Timestamp;
    }

    /// <summary>
    /// Request payload for claiming a redeem code.
    /// </summary>
    [Preserve]
    public class ClaimRedeemCodeRequest
    {
        /// <summary>The redeem code string entered by the user.</summary>
        [JsonProperty("code")]
        public string Code;

        /// <summary>User identifier claiming the redeem code.</summary>
        [JsonProperty("user_id")]
        public long UserId;
    }

    /// <summary>
    /// Response payload after claiming a redeem code.
    /// </summary>
    [Preserve]
    public class ClaimRedeemCodeResponse
    {
        /// <summary>Whether the redeem code was successfully claimed.</summary>
        [JsonProperty("success")]
        public bool Success;

        /// <summary>Array of order IDs created by the redeem operation.</summary>
        [JsonProperty("order_ids")]
        public int[] OrderIds;

        /// <summary>Human-readable result message from the server.</summary>
        [JsonProperty("message")]
        public string Message;
    }

    /// <summary>
    /// Request payload for redeem order.
    /// </summary>
    [Preserve]
    public class RedeemOrderRequest
    {
        /// <summary>Product identifier to redeem.</summary>
        [JsonProperty("product_id")]
        public string ProductId;
    }

    /// <summary>
    /// Response returned when creating an order on server.
    /// </summary>
    [Preserve]
    public class OrderResponse
    {
        /// <summary>Server-assigned order identifier.</summary>
        [JsonProperty("id")]
        public int Id;

        /// <summary>Product identifier for the created order.</summary>
        [JsonProperty("product_id")]
        public string ProductId;

        /// <summary>URL for web-based payment (used by noctuastore payment type).</summary>
        [JsonProperty("payment_url")]
        public string PaymentUrl;

        /// <summary>Payment channel assigned by the server for this order.</summary>
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
        /// <summary>Server-assigned order identifier for this deliverable.</summary>
        [JsonProperty("order_id")]
        public int OrderId;

        /// <summary>Payment channel used for this deliverable's order.</summary>
        [JsonProperty("payment_type")]
        public PaymentType PaymentType;

        /// <summary>Current delivery status string.</summary>
        [JsonProperty("status")]
        public string Status;

        /// <summary>Product identifier of the item pending delivery.</summary>
        [JsonProperty("product_id")]
        public string ProductId;
    }

    /// <summary>
    /// Pending deliverables response data (already unwrapped by HTTP library).
    /// </summary>
    [Preserve]
    public class PendingDeliverablesData
    {
        /// <summary>Array of pending Noctua redeem orders awaiting delivery.</summary>
        [JsonProperty("pending_noctua_redeem_orders")]
        public PendingDeliverables[] PendingNoctuaRedeemOrders;
    }

    /// <summary>
    /// Payment outcome enumeration.
    /// </summary>
    public enum PaymentStatus
    {
        /// <summary>Payment completed successfully.</summary>
        Successful,
        /// <summary>Payment was canceled by the user.</summary>
        Canceled,
        /// <summary>Payment failed due to an error.</summary>
        Failed,
        /// <summary>Payment has been confirmed by the store.</summary>
        Confirmed,
        /// <summary>The item has already been purchased (non-consumable duplicate).</summary>
        ItemAlreadyOwned,
        /// <summary>Payment is pending (e.g., awaiting external confirmation).</summary>
        Pending,
        /// <summary>The purchase object received from the store is invalid.</summary>
        InvalidPurchaseObject,
        /// <summary>Another pending purchase is already in progress.</summary>
        PendingPurchaseOngoing,
        /// <summary>The native IAP subsystem is not ready.</summary>
        IapNotReady,
    }

    /// <summary>
    /// Result of a local/native payment operation.
    /// </summary>
    public class PaymentResult
    {
        /// <summary>Outcome status of the payment operation.</summary>
        public PaymentStatus Status;
        /// <summary>Store-issued receipt identifier (e.g., purchase token on Android, transaction ID on iOS).</summary>
        public string ReceiptId;
        /// <summary>Raw receipt data for server-side verification.</summary>
        public string ReceiptData;
        /// <summary>Human-readable message describing the payment result.</summary>
        public string Message;
    }

    /// <summary>
    /// Request payload used to verify an order on the server.
    /// </summary>
    [Preserve]
    public class VerifyOrderRequest
    {
        /// <summary>Order identifier to verify.</summary>
        [JsonProperty("id")]
        public int Id;

        /// <summary>Store-issued receipt identifier.</summary>
        [JsonProperty("receipt_id")]
        public string ReceiptId;

        /// <summary>Raw receipt data from the native store.</summary>
        [JsonProperty("receipt_data")]
        public string ReceiptData;

        /// <summary>Trigger source that initiated the verification (see <see cref="VerifyOrderTrigger"/>).</summary>
        [JsonProperty("trigger")]
        public string Trigger;
    }

    /// <summary>
    /// Order status reported by the server.
    /// </summary>
    [Preserve]
    public enum OrderStatus
    {
        /// <summary>Order status is unknown or not yet determined.</summary>
        unknown,
        /// <summary>Order has been created and is awaiting payment or verification.</summary>
        pending,
        /// <summary>Order has been successfully completed and delivered.</summary>
        completed,
        /// <summary>Order payment or processing failed.</summary>
        failed,
        /// <summary>Receipt verification against the store failed.</summary>
        verification_failed,
        /// <summary>Delivery callback to the game server failed.</summary>
        delivery_callback_failed,
        /// <summary>An internal server error occurred.</summary>
        error,
        /// <summary>Order has been refunded.</summary>
        refunded,
        /// <summary>Order was canceled.</summary>
        canceled,
        /// <summary>Order has expired before completion.</summary>
        expired,
        /// <summary>Order data is invalid.</summary>
        invalid,
        /// <summary>Purchase was voided by the store.</summary>
        voided,
        /// <summary>Server requests fallback to native store payment flow.</summary>
        fallback_to_native_payment
    }

    /// <summary>
    /// Response payload for order verification requests.
    /// </summary>
    [Preserve]
    public class VerifyOrderResponse
    {
        /// <summary>Order identifier that was verified.</summary>
        [JsonProperty("id")]
        public int Id;

        /// <summary>Updated order status after verification.</summary>
        [JsonProperty("order_status")]
        public OrderStatus Status;

        /// <summary>Store-reported price amount after verification.</summary>
        [JsonProperty("store_amount")]
        public string StoreAmount;

        /// <summary>Store-reported currency code after verification.</summary>
        [JsonProperty("store_currency")]
        public string StoreCurrency;
    }

    /// <summary>
    /// Purchase request payload sent to server for regular orders.
    /// </summary>
    [Preserve]
    public class PurchaseRequest
    {
        /// <summary>Product identifier to purchase.</summary>
        [JsonProperty("product_id")]
        public string ProductId;

        /// <summary>Price amount in the specified currency.</summary>
        [JsonProperty("price")]
        public decimal Price;

        /// <summary>ISO currency code for the price.</summary>
        [JsonProperty("currency")]
        public string Currency;

        /// <summary>In-game role identifier of the purchasing player.</summary>
        [JsonProperty("role_id")]
        public string RoleId;

        /// <summary>In-game server identifier of the purchasing player.</summary>
        [JsonProperty("server_id")]
        public string ServerId;

        /// <summary>In-game item identifier being purchased.</summary>
        [JsonProperty("ingame_item_id")]
        public string IngameItemId;

        /// <summary>In-game item name being purchased.</summary>
        [JsonProperty("ingame_item_name")]
        public string IngameItemName;

        /// <summary>Additional key-value metadata for the purchase.</summary>
        [JsonProperty("extra")]
        public Dictionary<string, string> Extra;

    }

    /// <summary>
    /// Response returned after creating a purchase.
    /// </summary>
    [Preserve]
    public class PurchaseResponse
    {
        /// <summary>Server-assigned order identifier for this purchase.</summary>
        [JsonProperty("order_id")]
        public int OrderId;

        /// <summary>Current order status.</summary>
        [JsonProperty("status")]
        public OrderStatus Status;

        /// <summary>Human-readable result message from the server.</summary>
        [JsonProperty("message")]
        public string Message;
    }

    /// <summary>
    /// Noctua-specific gold/virtual currency data returned for player.
    /// </summary>
    [Preserve]
    public class NoctuaGoldData
    {
        /// <summary>Player's VIP level.</summary>
        [JsonProperty("vip_level")]
        public double VipLevel;

        /// <summary>Amount of freely spendable Noctua Gold.</summary>
        [JsonProperty("gold_amount")]
        public double GoldAmount;

        /// <summary>Amount of bound (non-transferable) Noctua Gold.</summary>
        [JsonProperty("bound_gold_amount")]
        public double BoundGoldAmount;

        /// <summary>Total Noctua Gold (free + bound).</summary>
        [JsonProperty("total_gold_amount")]
        public double TotalGoldAmount;

        /// <summary>Amount of Noctua Gold eligible for purchases.</summary>
        [JsonProperty("eligible_gold_amount")]
        public double EligibleGoldAmount;
    }

    /// <summary>
    /// Enumeration for what triggered a verify order attempt.
    /// </summary>
    public enum VerifyOrderTrigger
    {
        /// <summary>Verification triggered as part of the normal payment flow.</summary>
        payment_flow,
        /// <summary>Verification triggered manually by the user retrying a pending purchase.</summary>
        manual_retry,
        /// <summary>Verification triggered automatically by the client on app startup.</summary>
        client_automatic_retry,
        /// <summary>Verification triggered when processing a pending deliverable.</summary>
        pending_deliverable,
    }
}
