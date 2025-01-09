using System;
using System.Collections.Generic;
using Newtonsoft.Json;
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

    public interface INativePlugin : INativeTracker, INativeIAP, INativeAccountStore
    {
        void Init(List<String> activeBundleIds);

        void OnApplicationPause(bool pause);

        void ShowDatePicker(int year, int month, int day, int id);

        void CloseDatePicker();
    }
}