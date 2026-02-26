using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Scripting;

// For iOS, the bridging are managed on these files:
// - Runtime/IosPlugin.cs
// - Runtime/Plugins/iOS/NoctuaInterop.h
// - Runtime/Plugins/iOS/NoctuaInterop.m

// For Android:
// - Runtime/IosPlugin.cs

// Both have interface in:
// - Runtime/DefaultNativePlugin.cs

namespace com.noctuagames.sdk
{
    /// <summary>Mirrors com.noctuagames.sdk.models.ConsumableType (Android) / ConsumableType (iOS)</summary>
    [Preserve]
    public enum NoctuaConsumableType
    {
        Consumable = 0,
        NonConsumable = 1,
        Subscription = 2
    }

    /// <summary>Mirrors com.noctuagames.sdk.models.ProductType (Android) / ProductType (iOS)</summary>
    [Preserve]
    public enum NoctuaProductType
    {
        InApp = 0,
        Subs = 1
    }

    /// <summary>
    /// Converts a JSON object/value to its string representation, or passes through if already a string.
    /// Used for fields where the native SDK may return a nested JSON object instead of an escaped string.
    /// </summary>
    [Preserve]
    public class RawJsonStringConverter : JsonConverter<string>
    {
        public override string ReadJson(JsonReader reader, Type objectType, string existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
                return (string)reader.Value;
            if (reader.TokenType == JsonToken.Null)
                return null;
            var token = JToken.Load(reader);
            return token.ToString(Formatting.None);
        }

        public override void WriteJson(JsonWriter writer, string value, JsonSerializer serializer)
        {
            writer.WriteValue(value);
        }
    }

    public interface INativeTracker
    {
        void TrackAdRevenue(string source, double revenue, string currency, Dictionary<string, IConvertible> extraPayload = null);
        void TrackPurchase(string orderId, double amount, string currency, Dictionary<string, IConvertible> extraPayload = null);
        void TrackCustomEvent(string name, Dictionary<string, IConvertible> extraPayload = null);
        void TrackCustomEventWithRevenue(string name, double revenue, string currency, Dictionary<string, IConvertible> extraPayload = null);
        void OnOnline();
        void OnOffline();
    }

    public interface INativeIAP
    {
        void PurchaseItem(string productId, Action<bool, string> callback);
        void GetActiveCurrency(string productId, Action<bool, string> callback);
        void GetProductPurchasedById(string productId, Action<bool> callback);
        void GetReceiptProductPurchasedStoreKit1(string productId, Action<string> callback);
        void GetProductPurchaseStatusDetail(string productId, Action<ProductPurchaseStatus> callback);
    }
    
    
    [Preserve]
    public class ProductPurchaseStatus
    {
        public string ProductId;
        public bool IsPurchased;
        public bool IsAcknowledged;
        public bool IsAutoRenewing;
        public int PurchaseState;       // 0=Unspecified, 1=Purchased, 2=Pending
        public string PurchaseToken;
        public long PurchaseTime;       // ms since epoch
        public long ExpiryTime;         // ms since epoch, 0 if N/A (always 0 on Android)
        public string OrderId;
        public string OriginalJson;
    }

    [Preserve]
    public class NativeAccount
    {
        [JsonProperty("playerId")] public long PlayerId;
        [JsonProperty("gameId")] public long GameId;
        [JsonProperty("rawData")] public string RawData;
        [JsonProperty("lastUpdated")] public long LastUpdated;
    }

    [Preserve]
    public class NoctuaAdjustAttribution
    {
        [JsonProperty("trackerToken")] public string TrackerToken;
        [JsonProperty("trackerName")] public string TrackerName;
        [JsonProperty("network")] public string Network;
        [JsonProperty("campaign")] public string Campaign;
        [JsonProperty("adgroup")] public string Adgroup;
        [JsonProperty("creative")] public string Creative;
        [JsonProperty("clickLabel")] public string ClickLabel;
        [JsonProperty("adid")] public string Adid;
        [JsonProperty("costType")] public string CostType;
        [JsonProperty("costAmount")] public double CostAmount;
        [JsonProperty("costCurrency")] public string CostCurrency;
        [JsonProperty("fbInstallReferrer")] public string FbInstallReferrer;

        public static NoctuaAdjustAttribution FromJson(string json)
        {
            if (string.IsNullOrEmpty(json) || json == "{}")
                return new NoctuaAdjustAttribution();
            try
            {
                var settings = new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    NullValueHandling = NullValueHandling.Ignore
                };

                return JsonConvert.DeserializeObject<NoctuaAdjustAttribution>(json, settings)
                    ?? new NoctuaAdjustAttribution();
            }
            catch (Exception)
            {
                return new NoctuaAdjustAttribution();
            }
        }

    }

    public interface INativeAccountStore
    {
        NativeAccount GetAccount(long userId, long gameId);

        List<NativeAccount> GetAccounts();

        void PutAccount(NativeAccount account);

        int DeleteAccount(NativeAccount account);
    }

    [Preserve]
    public class NativeEvent
    {
        [JsonProperty("id")] public long Id;
        [JsonProperty("eventJson")]
        [JsonConverter(typeof(RawJsonStringConverter))]
        public string EventJson;
        [JsonProperty("createdAt")] public long CreatedAt;
    }

    public interface INativePlugin : INativeTracker, INativeIAP, INativeAccountStore, INativeDatePicker
    {
        void Init(List<String> activeBundleIds);

        void OnApplicationPause(bool pause);

        void GetFirebaseInstallationID(Action<string> callback);

        void GetFirebaseAnalyticsSessionID(Action<string> callback);

        void GetFirebaseRemoteConfigString(string key, Action<string> callback);

        void GetFirebaseRemoteConfigBoolean(string key, Action<bool> callback);

        void GetFirebaseRemoteConfigDouble(string key, Action<double> callback);

        void GetFirebaseRemoteConfigLong(string key, Action<long> callback);

        void GetAdjustAttribution(Action<string> callback);

        void SaveEvents(string jsonString);

        void GetEvents(Action<List<string>> callback);

        void DeleteEvents();

        // Per-row event storage for unlimited event tracking
        void InsertEvent(string eventJson);

        void GetEventsBatch(int limit, int offset, Action<List<NativeEvent>> callback);

        void DeleteEventsByIds(long[] ids, Action<int> callback);

        void GetEventCount(Action<int> callback);
    }

    public interface INativeDatePicker
    {
        void ShowDatePicker(int year, int month, int day, int id);
        void CloseDatePicker();
    }
}