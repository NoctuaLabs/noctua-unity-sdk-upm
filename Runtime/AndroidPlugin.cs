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

        public void GetFirebaseInstallationID(Action<string> callback) {
            try
            {
                using var noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");
                noctua.Call("getFirebaseInstallationID", new AndroidCallback<string>(callback));
            }
            catch (Exception e)
            {
                _log.Warning($"[Noctua] Failed to get Firebase Installation ID: {e.Message}");
                callback?.Invoke(string.Empty);
            }
        }

        public void GetFirebaseAnalyticsSessionID(Action<string> callback) {
            try
            {
                using var noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");
                noctua.Call("getFirebaseAnalyticsSessionID", new AndroidCallback<string>(callback));
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[Noctua] Failed to get Firebase Analytics Session ID: {e.Message}");
                callback?.Invoke(string.Empty);
            }
        }

        public void GetFirebaseRemoteConfigString(string key, Action<string> callback)
        {
            try
            {
                using var noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");
                noctua.Call("getFirebaseRemoteConfigString", key, new AndroidCallback<string>(callback));
            }
            catch (Exception e)
            {
                _log.Warning($"[Noctua] Failed to get Firebase Remote Config String for key '{key}': {e.Message}");
                callback?.Invoke(string.Empty);
            }
        }

        public void GetFirebaseRemoteConfigBoolean(string key, Action<bool> callback)
        {
            try
            {
                using var noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");
                noctua.Call("getFirebaseRemoteConfigBoolean", key, new AndroidCallback<bool>(callback));
            }
            catch (Exception e)
            {
                _log.Warning($"[Noctua] Failed to get Firebase Remote Config Boolean for key '{key}': {e.Message}");
                callback?.Invoke(false);
            }
        }

        public void GetFirebaseRemoteConfigDouble(string key, Action<double> callback)
        {
            try
            {
                using var noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");
                noctua.Call("getFirebaseRemoteConfigDouble", key, new AndroidCallback<double>(callback));
            }
            catch (Exception e)
            {
                _log.Warning($"[Noctua] Failed to get Firebase Remote Config Double for key '{key}': {e.Message}");
                callback?.Invoke(0.0);
            }
        }

        public void GetFirebaseRemoteConfigLong(string key, Action<long> callback)
        {
            try
            {
                using var noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");
                noctua.Call("getFirebaseRemoteConfigLong", key, new AndroidCallback<long>(callback));
            }
            catch (Exception e)
            {
                _log.Warning($"[Noctua] Failed to get Firebase Remote Config Long for key '{key}': {e.Message}");
                callback?.Invoke(0L);
            }
        }

        public void SaveEvents(string jsonString)
        {
            try
            {
                using var noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");
                noctua.Call("saveEvents", jsonString);
            }
            catch (Exception e)
            {
                if(e.Message == null) return;
                
                _log.Warning($"[Noctua] Failed to save events: {e.Message}");
            }
        }

        public void GetEvents(Action<List<string>> callback)
        {
            try
            {
                using var noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");
                
                var androidCallback = new AndroidCallback<AndroidJavaObject>(result =>
                {
                    var list = new List<string>();

                    int size = result.Call<int>("size");
                    for (int i = 0; i < size; i++)
                    {
                        string item = result.Call<string>("get", i);

                        Debug.Log($"[Noctua] Retrieved event: {i}");

                        list.Add(item);
                    }

                    Debug.Log($"[Noctua] Total events retrieved: {list.Count}");

                    callback?.Invoke(list);
                });

                noctua.Call("getEvents", androidCallback);
            }
            catch (Exception e)
            {
                callback?.Invoke(new List<string>());

                if(e.Message == null) return;
                _log.Warning($"[Noctua] Failed to get events: {e.Message}");
            }
        }

        public void DeleteEvents()
        {
            try
            {
                using var noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");
                noctua.Call("deleteEvents");

                Debug.Log($"[Noctua] Deleted all events");

            }
            catch (Exception e)
            {
                if(e.Message == null) return;
                _log.Warning($"[Noctua] Failed to delete events: {e.Message}");
            }
        }

        private int saveEvents = 0;
        public void SaveEvents(string jsonString)
        {
            try
            {
                using var noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");
                noctua.Call("saveEvents", jsonString);

                saveEvents++;

                Debug.Log($"[Noctua] Saved events: {saveEvents}");
            }
            catch (Exception e)
            {
                _log.Warning($"[Noctua] Failed to save events: {e.Message}");
            }
        }

        public void GetEvents(Action<List<string>> callback)
        {
            try
            {
                using var noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");
                
                var androidCallback = new AndroidCallback<AndroidJavaObject>(result =>
                {
                    var list = new List<string>();

                    int size = result.Call<int>("size");
                    for (int i = 0; i < size; i++)
                    {
                        string item = result.Call<string>("get", i);

                        Debug.Log($"[Noctua] Retrieved event: {i}");

                        list.Add(item);
                    }

                    Debug.Log($"[Noctua] Total events retrieved: {list.Count}");

                    callback?.Invoke(list);
                });

                noctua.Call("getEvents", androidCallback);
            }
            catch (Exception e)
            {
                _log.Warning($"[Noctua] Failed to get events: {e.Message}");
                callback?.Invoke(new List<string>());
            }
        }

        public void DeleteEvents()
        {
            try
            {
                using var noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");
                noctua.Call("deleteEvents");

                Debug.Log($"[Noctua] Deleted all events " + saveEvents);

                saveEvents = 0;
            }
            catch (Exception e)
            {
                _log.Warning($"[Noctua] Failed to delete events: {e.Message}");
            }
        }
    }
}

public class AndroidCallback<T> : AndroidJavaProxy
{
    private readonly Action<T> _callback;

    public AndroidCallback(Action<T> callback)
        : base("kotlin.jvm.functions.Function1")
    {
        _callback = callback;
    }

    // Support Kotlin calling invoke(String)
    public AndroidJavaObject invoke(string arg)
    {
        if (typeof(T) == typeof(string))
            _callback?.Invoke((T)(object)arg);

        return new AndroidJavaClass("kotlin.Unit").GetStatic<AndroidJavaObject>("INSTANCE");
    }

    // Support Kotlin calling invoke(Boolean)
    public AndroidJavaObject invoke(bool arg)
    {
        if (typeof(T) == typeof(bool))
            _callback?.Invoke((T)(object)arg);

        return new AndroidJavaClass("kotlin.Unit").GetStatic<AndroidJavaObject>("INSTANCE");
    }

    // Support Kotlin calling invoke(Double)
    public AndroidJavaObject invoke(double arg)
    {
        if (typeof(T) == typeof(double))
            _callback?.Invoke((T)(object)arg);

        return new AndroidJavaClass("kotlin.Unit").GetStatic<AndroidJavaObject>("INSTANCE");
    }

    // Support Kotlin calling invoke(Long)
    public AndroidJavaObject invoke(long arg)
    {
        if (typeof(T) == typeof(long))
            _callback?.Invoke((T)(object)arg);

        return new AndroidJavaClass("kotlin.Unit").GetStatic<AndroidJavaObject>("INSTANCE");
    }

    // Fallback if Kotlin dispatches invoke(Object)
    public AndroidJavaObject invoke(AndroidJavaObject arg)
    {
        object value = null;

        if (typeof(T) == typeof(string))
            value = arg?.Call<string>("toString");
        else if (typeof(T) == typeof(int))
            value = arg?.Call<int>("intValue");
        else if (typeof(T) == typeof(bool))
            value = arg?.Call<bool>("booleanValue");
        else if (typeof(T) == typeof(double))
            value = arg?.Call<double>("doubleValue");
        else if (typeof(T) == typeof(long))
            value = arg?.Call<long>("longValue");
        else
            value = arg;

        _callback?.Invoke((T)value);

        return new AndroidJavaClass("kotlin.Unit").GetStatic<AndroidJavaObject>("INSTANCE");
    }
}
#endif