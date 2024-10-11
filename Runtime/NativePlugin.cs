using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    
    public interface INativeTracker
    {
        void TrackAdRevenue(string source, double revenue, string currency, Dictionary<string, IConvertible> extraPayload = null);
        void TrackPurchase(string orderId, double amount, string currency, Dictionary<string, IConvertible> extraPayload = null);
        void TrackCustomEvent(string name, Dictionary<string, IConvertible> extraPayload = null);
    }
    
    public interface INativeIAP
    {
        void PurchaseItem(string productId, Action<bool, string> callback);
        void GetActiveCurrency(string productId, Action<bool, string> callback);
    }
    
    
    public class NativeAccount
    {
        public long PlayerId;
        public long GameId;
        public string RawData;
        public DateTime LastUpdated = DateTime.UtcNow;
    }

    public interface INativeAccountStore
    {
        NativeAccount GetAccount(long userId, long gameId);

        List<NativeAccount> GetAccounts();

        void PutAccount(NativeAccount account);

        int DeleteAccount(NativeAccount account);
    }

    public interface INativePlugin : INativeTracker, INativeIAP, INativeAccountStore
    {
        void Init(List<String> activeBundleIds);

        void OnApplicationPause(bool pause);

        void ShowDatePicker(int year, int month, int day);

    }
}