using System;
using System.Collections.Generic;

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
        void GetProductPurchaseStatusDetail(string productId, Action<ProductPurchaseStatus> callback);
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
