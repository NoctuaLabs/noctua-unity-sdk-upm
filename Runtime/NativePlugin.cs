using System;
using System.Collections.Generic;
using Newtonsoft.Json;
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
    }
    
    
    [Preserve]
    public class NativeAccount
    {
        [JsonProperty("playerId")] public long PlayerId;
        [JsonProperty("gameId")] public long GameId;
        [JsonProperty("rawData")] public string RawData;
        [JsonProperty("lastUpdated")] public long LastUpdated;
    }

    public interface INativeAccountStore
    {
        NativeAccount GetAccount(long userId, long gameId);

        List<NativeAccount> GetAccounts();

        void PutAccount(NativeAccount account);

        int DeleteAccount(NativeAccount account);
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
    }

    public interface INativeDatePicker
    {
        void ShowDatePicker(int year, int month, int day, int id);
        void CloseDatePicker();
    }
}