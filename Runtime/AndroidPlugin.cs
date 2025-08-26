using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

#if UNITY_ANDROID && !UNITY_EDITOR
namespace com.noctuagames.sdk
{
    internal class AndroidPlugin : INativePlugin
    {
        private readonly ILogger _log = new NoctuaLogger(typeof(AndroidPlugin));
        
        public void Init(List<string> activeBundleIds)
        {
            _log.Info($"Initialize to nativePlugin");
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            using var javaActiveBundleIds = new AndroidJavaObject("java.util.ArrayList");
            
            using var noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");
            noctua.Call("init", unityActivity, javaActiveBundleIds);
            noctua.Call("onResume");
        }

        public void OnApplicationPause(bool pause)
        {
            using AndroidJavaObject noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");
            noctua.Call(pause ? "onPause" : "onResume");
        }

        public void TrackAdRevenue(
            string source,
            double revenue,
            string currency,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            using AndroidJavaObject javaPayload = ConvertToJavaHashMap(extraPayload);
            using AndroidJavaObject noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");

            noctua.Call("trackAdRevenue", source, revenue, currency, javaPayload);
        }

        public void TrackPurchase(
            string orderId,
            double amount,
            string currency,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            using AndroidJavaObject javaPayload = ConvertToJavaHashMap(extraPayload);
            using AndroidJavaObject noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");

            noctua.Call("trackPurchase", orderId, amount, currency, javaPayload);
        }

        public void TrackCustomEvent(
            string name,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            using AndroidJavaObject javaPayload = ConvertToJavaHashMap(extraPayload);
            using AndroidJavaObject noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");

            noctua.Call("trackCustomEvent", name, javaPayload);

            _log.Info($"forwarded event '{name}' to native tracker");
        }

        public void TrackCustomEventWithRevenue(
            string name,
            double revenue,
            string currency,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            using AndroidJavaObject javaPayload = ConvertToJavaHashMap(extraPayload);
            using AndroidJavaObject noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");

            noctua.Call("trackCustomEventWithRevenue", name, revenue, currency, javaPayload);

            _log.Info($"forwarded event '{name}' to native tracker");
        }

        private static AndroidJavaObject ConvertToJavaHashMap(Dictionary<string, IConvertible> dictionary)
        {
            var hashMap = new AndroidJavaObject("java.util.HashMap");

            if (dictionary == null)
            {
                return hashMap;
            }

            foreach (var pair in dictionary)
            {
                var boxValue = pair.Value switch
                {
                    sbyte sbyteValue => new AndroidJavaObject("java.lang.Byte", sbyteValue),
                    short shortValue => new AndroidJavaObject("java.lang.Short", shortValue),
                    int intValue => new AndroidJavaObject("java.lang.Integer", intValue),
                    long longValue => new AndroidJavaObject("java.lang.Long", longValue),
                    float floatValue => new AndroidJavaObject("java.lang.Float", floatValue),
                    double doubleValue => new AndroidJavaObject("java.lang.Double", doubleValue),
                    char charValue => new AndroidJavaObject("java.lang.Character", charValue),
                    bool boolValue => new AndroidJavaObject("java.lang.Boolean", boolValue),
                    string stringValue => new AndroidJavaObject("java.lang.String", stringValue),
                    DateTime time => new AndroidJavaObject("java.lang.String", time.ToString("o")),
                    _ => new AndroidJavaObject("java.lang.String", pair.Value.ToString(CultureInfo.InvariantCulture))
                };

                hashMap.Call<AndroidJavaObject>("put", pair.Key, boxValue);
            }

            return hashMap;
        }

        public void PurchaseItem(string productId, Action<bool, string> callback)
        {
            throw new NotImplementedException();
        }

        public void GetActiveCurrency(string productId, Action<bool, string> callback)
        {
            throw new NotImplementedException();
        }

        public void GetProductPurchasedById(string productId, Action<bool> callback)
        {
            throw new NotImplementedException();
        }

        public void GetReceiptProductPurchasedStoreKit1(string productId, Action<string> callback)
        {
            throw new NotImplementedException();
        }

        public void ShowDatePicker(int year, int month, int day, int id)
        {
            AndroidJavaClass javaUnityClass = new AndroidJavaClass("com.pingak9.nativepopup.Bridge");
            javaUnityClass.CallStatic("ShowDatePicker", year, month, day, id);
        }

        // Suppresses ReSharper warning for unused global parameter in CloseDatePicker method.
        public void CloseDatePicker() {}

        public NativeAccount GetAccount(long playerId, long gameId)
        {
            using AndroidJavaObject noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");
            AndroidJavaObject javaAccount = noctua.Call<AndroidJavaObject>("getAccount", playerId, gameId);
            
            return new NativeAccount
            {
                PlayerId = javaAccount.Get<long>("userId"),
                GameId = javaAccount.Get<long>("gameId"),
                RawData = javaAccount.Get<string>("rawData"),
                LastUpdated = javaAccount.Get<long>("lastUpdated")
            };
        }

        public List<NativeAccount> GetAccounts()
        {
            using var noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");
            var javaAccounts = noctua.Call<AndroidJavaObject>("getAccounts");
            var size = javaAccounts.Call<int>("size");
            
            var accounts = new List<NativeAccount>();
            
            for (var i = 0; i < size; i++)
            {
                var javaAccount = javaAccounts.Call<AndroidJavaObject>("get", i);

                accounts.Add(
                    new NativeAccount
                    {
                        PlayerId = javaAccount.Get<long>("userId"),
                        GameId = javaAccount.Get<long>("gameId"),
                        RawData = javaAccount.Get<string>("rawData"),
                        LastUpdated = javaAccount.Get<long>("lastUpdated")
                    }
                );
            }
            
            return accounts;
        }

        public void PutAccount(NativeAccount account)
        {
            using var noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");

            using var javaAccount = new AndroidJavaObject(
                "com.noctuagames.sdk.Account",
                account.PlayerId,
                account.GameId,
                account.RawData
            );
            
            noctua.Call("putAccount", javaAccount);
        }

        public int DeleteAccount(NativeAccount account)
        {
            using var noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");

            using var javaAccount = new AndroidJavaObject(
                "com.noctuagames.sdk.Account",
                account.PlayerId,
                account.GameId,
                ""
            );
            
            return noctua.Call<int>("deleteAccount", javaAccount);
        }

        public void OnOnline()
        {
            using var noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");
            try
            {
                noctua.Call("onOnline");
                _log.Info($"trigger online mode to native plugin");
            }
            catch (AndroidJavaException e)
            {
                _log.Warning("Failed to call onOnline method: " + e.Message);
            }
        }

        public void OnOffline()
        {
            using var noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");
            try
            {
                noctua.Call("onOffline");
                _log.Info($"trigger offline mode to native plugin");
            }
            catch (AndroidJavaException e)
            {
                _log.Warning("Failed to call onOffline method: " + e.Message);
            }
        }
    }
}
#endif