using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#if UNITY_ANDROID && !UNITY_EDITOR
namespace com.noctuagames.sdk
{
    /// <summary>
    /// Android implementation of <see cref="INativePlugin"/> that bridges to the Kotlin native SDK via JNI.
    /// </summary>
    internal class AndroidPlugin : INativePlugin
    {
        private readonly ILogger _log = new NoctuaLogger(typeof(AndroidPlugin));

        /// <inheritdoc />
        public void Init(List<string> activeBundleIds)
        {
            _log.Info($"Initialize to nativePlugin");
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            using var javaActiveBundleIds = new AndroidJavaObject("java.util.ArrayList");

            // Create NoctuaBillingConfig via Java helper to avoid Kotlin JNI boxing issues
            using var helper = new AndroidJavaClass("com.noctuagames.sdk.NoctuaBillingConfigHelper");
            var billingConfig = helper.CallStatic<AndroidJavaObject>("create", true, true, true);

            using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");
            noctua.Call("init", unityActivity, javaActiveBundleIds, billingConfig);
            noctua.Call("onResume");
        }

        /// <inheritdoc />
        public void OnApplicationPause(bool pause)
        {
            using AndroidJavaObject noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");
            noctua.Call(pause ? "onPause" : "onResume");
        }

        /// <inheritdoc />
        public void DisposeStoreKit()
        {
            try
            {
                using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");
                noctua.Call("disposeBilling");
                _log.Debug("AndroidPlugin.DisposeStoreKit (disposeBilling)");
            }
            catch (AndroidJavaException e)
            {
                _log.Warning("Failed to call disposeBilling: " + e.Message);
            }
        }

        /// <inheritdoc />
        public bool IsStoreKitReady()
        {
            try
            {
                using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");
                return noctua.Call<bool>("isBillingReady");
            }
            catch (AndroidJavaException e)
            {
                _log.Warning("Failed to call isBillingReady: " + e.Message);
                return false;
            }
        }

        /// <inheritdoc />
        public void RegisterNativeLifecycleCallback(Action<string> callback)
        {
            try
            {
                using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua")
                    .GetStatic<AndroidJavaObject>("INSTANCE");

                if (callback != null)
                {
                    noctua.Call("registerLifecycleCallback", new AndroidCallback<string>(callback));
                }
                else
                {
                    noctua.Call("registerLifecycleCallback", (AndroidJavaObject)null);
                }
            }
            catch (AndroidJavaException e)
            {
                _log.Warning("Failed to register native lifecycle callback: " + e.Message);
            }
        }

        /// <inheritdoc />
        public void TrackAdRevenue(
            string source,
            double revenue,
            string currency,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            using AndroidJavaObject javaPayload = ConvertToJavaHashMap(extraPayload);
            using AndroidJavaObject noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");

            noctua.Call("trackAdRevenue", source, revenue, currency, javaPayload);
        }

        /// <inheritdoc />
        public void TrackPurchase(
            string orderId,
            double amount,
            string currency,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            using AndroidJavaObject javaPayload = ConvertToJavaHashMap(extraPayload);
            using AndroidJavaObject noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");

            noctua.Call("trackPurchase", orderId, amount, currency, javaPayload);
        }

        /// <inheritdoc />
        public void TrackCustomEvent(
            string name,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            using AndroidJavaObject javaPayload = ConvertToJavaHashMap(extraPayload);
            using AndroidJavaObject noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");

            noctua.Call("trackCustomEvent", name, javaPayload);

            _log.Info($"forwarded event '{name}' to native tracker");
        }

        /// <inheritdoc />
        public void TrackCustomEventWithRevenue(
            string name,
            double revenue,
            string currency,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            using AndroidJavaObject javaPayload = ConvertToJavaHashMap(extraPayload);
            using AndroidJavaObject noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");

            noctua.Call("trackCustomEventWithRevenue", name, revenue, currency, javaPayload);

            _log.Info($"forwarded event '{name}' to native tracker");
        }

        /// <summary>
        /// Converts a C# dictionary to a Java HashMap for passing through JNI.
        /// </summary>
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

        /// <summary>
        /// Not used on Android; purchasing is handled via <see cref="GoogleBilling"/>.
        /// </summary>
        public void PurchaseItem(string productId, Action<bool, string> callback)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Not used on Android; currency queries are handled via <see cref="GoogleBilling"/>.
        /// </summary>
        public void GetActiveCurrency(string productId, Action<bool, string> callback)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Not used on Android; purchase queries are handled via <see cref="GoogleBilling"/>.
        /// </summary>
        public void GetProductPurchasedById(string productId, Action<bool> callback)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Not applicable on Android; StoreKit is iOS-only.
        /// </summary>
        public void GetReceiptProductPurchasedStoreKit1(string productId, Action<string> callback)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Not used on Android; purchase status queries are handled via <see cref="GoogleBilling"/>.
        /// </summary>
        public void GetProductPurchaseStatusDetail(string productId, Action<ProductPurchaseStatus> callback)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Completes purchase processing by delegating to the native Android SDK.
        /// Consumes or acknowledges the Google Play purchase depending on consumable type.
        /// </summary>
        public void CompletePurchaseProcessing(string purchaseToken, NoctuaConsumableType consumableType, bool verified, Action<bool> callback)
        {
            _log.Debug($"AndroidPlugin.CompletePurchaseProcessing: token={purchaseToken}, type={consumableType}, verified={verified}");
            try
            {
                using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua")
                    .GetStatic<AndroidJavaObject>("INSTANCE");
                using var javaConsumableType = new AndroidJavaClass("com.noctuagames.sdk.models.ConsumableType");
                var values = javaConsumableType.CallStatic<AndroidJavaObject[]>("values");
                var typeEnum = values[(int)consumableType];

                if (callback != null)
                {
                    var javaCallback = new AndroidCallback<bool>(callback);
                    noctua.Call("completePurchaseProcessing", purchaseToken, typeEnum, verified, javaCallback);
                }
                else
                {
                    noctua.Call("completePurchaseProcessing", purchaseToken, typeEnum, verified, null);
                }
            }
            catch (AndroidJavaException e)
            {
                _log.Warning("Failed to call completePurchaseProcessing: " + e.Message);
                callback?.Invoke(false);
            }
        }

        /// <inheritdoc />
        public void ShowDatePicker(int year, int month, int day, int id)
        {
            AndroidJavaClass javaUnityClass = new AndroidJavaClass("com.pingak9.nativepopup.Bridge");
            javaUnityClass.CallStatic("ShowDatePicker", year, month, day, id);
        }

        /// <inheritdoc />
        public void CloseDatePicker() {}

        /// <inheritdoc />
        public NativeAccount GetAccount(long playerId, long gameId)
        {
            using AndroidJavaObject noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");
            AndroidJavaObject javaAccount = noctua.Call<AndroidJavaObject>("getAccount", playerId, gameId);
            
            return new NativeAccount
            {
                PlayerId = javaAccount.Get<long>("userId"),
                GameId = javaAccount.Get<long>("gameId"),
                RawData = javaAccount.Get<string>("rawData"),
                LastUpdated = javaAccount.Get<long>("lastUpdated")
            };
        }

        /// <inheritdoc />
        public List<NativeAccount> GetAccounts()
        {
            using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");
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

        /// <inheritdoc />
        public void PutAccount(NativeAccount account)
        {
            using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");

            using var javaAccount = new AndroidJavaObject(
                "com.noctuagames.sdk.Account",
                account.PlayerId,
                account.GameId,
                account.RawData
            );
            
            noctua.Call("putAccount", javaAccount);
        }

        /// <inheritdoc />
        public int DeleteAccount(NativeAccount account)
        {
            using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");

            using var javaAccount = new AndroidJavaObject(
                "com.noctuagames.sdk.Account",
                account.PlayerId,
                account.GameId,
                ""
            );
            
            return noctua.Call<int>("deleteAccount", javaAccount);
        }

        /// <inheritdoc />
        public void OnOnline()
        {
            using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");
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

        /// <inheritdoc />
        public void OnOffline()
        {
            using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");
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

        /// <inheritdoc />
        public void GetFirebaseInstallationID(Action<string> callback) {
            try
            {
                using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");
                noctua.Call("getFirebaseInstallationID", new AndroidCallback<string>(callback));
            }
            catch (Exception e)
            {
                _log.Warning($"[Noctua] Failed to get Firebase Installation ID: {e.Message}");
                callback?.Invoke(string.Empty);
            }
        }

        /// <inheritdoc />
        public void GetFirebaseAnalyticsSessionID(Action<string> callback) {
            try
            {
                using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");
                noctua.Call("getFirebaseAnalyticsSessionID", new AndroidCallback<string>(callback));
            }
            catch (Exception e)
            {
                _log.Warning($"Failed to get Firebase Analytics Session ID: {e.Message}");
                callback?.Invoke(string.Empty);
            }
        }

        /// <inheritdoc />
        public void GetFirebaseRemoteConfigString(string key, Action<string> callback)
        {
            try
            {
                using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");
                noctua.Call("getFirebaseRemoteConfigString", key, new AndroidCallback<string>(callback));
            }
            catch (Exception e)
            {
                _log.Warning($"[Noctua] Failed to get Firebase Remote Config String for key '{key}': {e.Message}");
                callback?.Invoke(string.Empty);
            }
        }

        /// <inheritdoc />
        public void GetFirebaseRemoteConfigBoolean(string key, Action<bool> callback)
        {
            try
            {
                using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");
                noctua.Call("getFirebaseRemoteConfigBoolean", key, new AndroidCallback<bool>(callback));
            }
            catch (Exception e)
            {
                _log.Warning($"[Noctua] Failed to get Firebase Remote Config Boolean for key '{key}': {e.Message}");
                callback?.Invoke(false);
            }
        }

        /// <inheritdoc />
        public void GetFirebaseRemoteConfigDouble(string key, Action<double> callback)
        {
            try
            {
                using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");
                noctua.Call("getFirebaseRemoteConfigDouble", key, new AndroidCallback<double>(callback));
            }
            catch (Exception e)
            {
                _log.Warning($"[Noctua] Failed to get Firebase Remote Config Double for key '{key}': {e.Message}");
                callback?.Invoke(0.0);
            }
        }

        /// <inheritdoc />
        public void GetFirebaseRemoteConfigLong(string key, Action<long> callback)
        {
            try
            {
                using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");
                noctua.Call("getFirebaseRemoteConfigLong", key, new AndroidCallback<long>(callback));
            }
            catch (Exception e)
            {
                _log.Warning($"[Noctua] Failed to get Firebase Remote Config Long for key '{key}': {e.Message}");
                callback?.Invoke(0L);
            }
        }

        /// <inheritdoc />
        public void GetAdjustAttribution(Action<string> callback)
        {
            try
            {
                using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");
                noctua.Call("getAdjustAttribution", new AndroidCallback<string>(callback));
            }
            catch (Exception e)
            {
                _log.Warning($"[Noctua] Failed to get Adjust Attribution: {e.Message}");
                callback?.Invoke(string.Empty);
            }
        }

        /// <inheritdoc />
        public void SaveEvents(string jsonString)
        {
            try
            {
                using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");
                noctua.Call("saveEvents", jsonString);
            }
            catch (Exception e)
            {
                if(e.Message == null) return;
                
                _log.Warning($"[Noctua] Failed to save events: {e.Message}");
            }
        }

        /// <inheritdoc />
        public void GetEvents(Action<List<string>> callback)
        {
            try
            {
                using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");
                
                var androidCallback = new AndroidCallback<AndroidJavaObject>(result =>
                {
                    var list = new List<string>();

                    int size = result.Call<int>("size");
                    for (int i = 0; i < size; i++)
                    {
                        string item = result.Call<string>("get", i);

                        list.Add(item);
                    }

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

        /// <inheritdoc />
        public void DeleteEvents()
        {
            try
            {
                using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");
                noctua.Call("deleteEvents");

                _log.Debug("Deleted all events");

            }
            catch (Exception e)
            {
                if(e.Message == null) return;
                _log.Warning($"[Noctua] Failed to delete events: {e.Message}");
            }
        }

        /// <inheritdoc />
        public void InsertEvent(string eventJson)
        {
            try
            {
                using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");
                noctua.Call("insertEvent", eventJson);
            }
            catch (Exception e)
            {
                if (e.Message == null) return;
                _log.Warning($"[Noctua] Failed to insert event: {e.Message}");
            }
        }

        /// <inheritdoc />
        public void GetEventsBatch(int limit, int offset, Action<List<NativeEvent>> callback)
        {
            try
            {
                using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");
                var androidCallback = new AndroidCallback<string>(json =>
                {
                    try
                    {
                        var events = JsonConvert.DeserializeObject<List<NativeEvent>>(json) ?? new List<NativeEvent>();
                        callback?.Invoke(events);
                    }
                    catch (Exception ex)
                    {
                        _log.Warning($"[Noctua] Failed to parse events batch, attempting element-wise recovery: {ex.Message}");
                        callback?.Invoke(ParseEventsBatchSafe(json));
                    }
                });

                noctua.Call("getEventsBatch", limit, offset, androidCallback);
            }
            catch (Exception e)
            {
                callback?.Invoke(new List<NativeEvent>());
                if (e.Message == null) return;
                _log.Warning($"[Noctua] Failed to get events batch: {e.Message}");
            }
        }

        /// Attempts to parse a JSON array of <see cref="NativeEvent"/> element by element,
        /// skipping any entries whose <c>eventJson</c> field is malformed.
        /// This prevents a single corrupted row from blocking the entire flush batch.
        private List<NativeEvent> ParseEventsBatchSafe(string json)
        {
            var result = new List<NativeEvent>();

            try
            {
                var array = JArray.Parse(json);

                foreach (var token in array)
                {
                    try
                    {
                        var evt = token.ToObject<NativeEvent>(JsonSerializer.CreateDefault());

                        if (evt != null)
                            result.Add(evt);
                    }
                    catch (Exception elemEx)
                    {
                        var id = token["id"]?.Value<long>() ?? -1;
                        _log.Warning($"[Noctua] Skipping corrupted event id={id} in batch: {elemEx.Message}");
                    }
                }
            }
            catch (Exception parseEx)
            {
                _log.Warning($"[Noctua] Could not parse batch as JSON array during recovery: {parseEx.Message}");
            }

            _log.Info($"[Noctua] Element-wise recovery completed: {result.Count} events recovered");
            return result;
        }

        /// <inheritdoc />
        public void DeleteEventsByIds(long[] ids, Action<int> callback)
        {
            try
            {
                using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");
                var idsJson = "[" + string.Join(",", ids) + "]";
                var androidCallback = new AndroidCallback<int>(deletedCount =>
                {
                    callback?.Invoke(deletedCount);
                });

                noctua.Call("deleteEventsByIds", idsJson, androidCallback);
            }
            catch (Exception e)
            {
                callback?.Invoke(0);
                if (e.Message == null) return;
                _log.Warning($"[Noctua] Failed to delete events by IDs: {e.Message}");
            }
        }

        /// <inheritdoc />
        public void GetEventCount(Action<int> callback)
        {
            try
            {
                using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");
                var androidCallback = new AndroidCallback<int>(count =>
                {
                    callback?.Invoke(count);
                });

                noctua.Call("getEventCount", androidCallback);
            }
            catch (Exception e)
            {
                callback?.Invoke(0);
                if (e.Message == null) return;
                _log.Warning($"[Noctua] Failed to get event count: {e.Message}");
            }
        }

        // ------------------------------------
        // INativeAppManagement
        // ------------------------------------

        public void RequestInAppReview(Action<bool> callback)
        {
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");
                noctua.Call("requestInAppReview", activity, new AndroidCallback<bool>(callback));
            }
            catch (Exception e)
            {
                _log.Warning($"[Noctua] Failed to request in-app review: {e.Message}");
                callback?.Invoke(false);
            }
        }

        public void CheckForUpdate(Action<string> callback)
        {
            try
            {
                using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");
                noctua.Call("checkForUpdate", new AndroidCallback<string>(callback));
            }
            catch (Exception e)
            {
                _log.Warning($"[Noctua] Failed to check for update: {e.Message}");
                callback?.Invoke("{}");
            }
        }

        public void StartImmediateUpdate(Action<int> callback)
        {
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");
                noctua.Call("startImmediateUpdate", activity, new AndroidCallback<int>(callback));
            }
            catch (Exception e)
            {
                _log.Warning($"[Noctua] Failed to start immediate update: {e.Message}");
                callback?.Invoke(2); // Failed
            }
        }

        public void StartFlexibleUpdate(Action<float> onProgress, Action<int> onResult)
        {
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");
                noctua.Call("startFlexibleUpdate", activity, new AndroidCallback<float>(onProgress), new AndroidCallback<int>(onResult));
            }
            catch (Exception e)
            {
                _log.Warning($"[Noctua] Failed to start flexible update: {e.Message}");
                onResult?.Invoke(2); // Failed
            }
        }

        public void CompleteUpdate()
        {
            try
            {
                using var noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua").GetStatic<AndroidJavaObject>("INSTANCE");
                noctua.Call("completeUpdate");
            }
            catch (Exception e)
            {
                _log.Warning($"[Noctua] Failed to complete update: {e.Message}");
            }
        }
    }
}

/// <summary>
/// JNI proxy for Kotlin's Function1 (single-parameter lambdas).
/// Converts typed Kotlin callback invocations to C# <see cref="Action{T}"/> delegates.
/// </summary>
/// <typeparam name="T">The expected callback parameter type (string, bool, int, long, double, or AndroidJavaObject).</typeparam>
public class AndroidCallback<T> : AndroidJavaProxy
{
    private readonly Action<T> _callback;

    /// <summary>
    /// Creates a new callback proxy that bridges Kotlin Function1 to the specified C# action.
    /// </summary>
    /// <param name="callback">The C# action to invoke when Kotlin calls the lambda.</param>
    public AndroidCallback(Action<T> callback)
        : base("kotlin.jvm.functions.Function1")
    {
        _callback = callback;
    }

    /// <summary>
    /// Handles Kotlin invoking the lambda with a string argument.
    /// </summary>
    public AndroidJavaObject invoke(string arg)
    {
        if (typeof(T) == typeof(string))
            _callback?.Invoke((T)(object)arg);

        return new AndroidJavaClass("kotlin.Unit").GetStatic<AndroidJavaObject>("INSTANCE");
    }

    /// <summary>
    /// Handles Kotlin invoking the lambda with a boolean argument.
    /// </summary>
    public AndroidJavaObject invoke(bool arg)
    {
        if (typeof(T) == typeof(bool))
            _callback?.Invoke((T)(object)arg);

        return new AndroidJavaClass("kotlin.Unit").GetStatic<AndroidJavaObject>("INSTANCE");
    }

    /// <summary>
    /// Handles Kotlin invoking the lambda with a double argument.
    /// </summary>
    public AndroidJavaObject invoke(double arg)
    {
        if (typeof(T) == typeof(double))
            _callback?.Invoke((T)(object)arg);

        return new AndroidJavaClass("kotlin.Unit").GetStatic<AndroidJavaObject>("INSTANCE");
    }

    /// <summary>
    /// Handles Kotlin invoking the lambda with a long argument.
    /// </summary>
    public AndroidJavaObject invoke(long arg)
    {
        if (typeof(T) == typeof(long))
            _callback?.Invoke((T)(object)arg);

        return new AndroidJavaClass("kotlin.Unit").GetStatic<AndroidJavaObject>("INSTANCE");
    }

    /// <summary>
    /// Handles Kotlin invoking the lambda with an int argument.
    /// </summary>
    public AndroidJavaObject invoke(int arg)
    {
        if (typeof(T) == typeof(int))
            _callback?.Invoke((T)(object)arg);

        return new AndroidJavaClass("kotlin.Unit").GetStatic<AndroidJavaObject>("INSTANCE");
    }

    /// <summary>
    /// Fallback handler when Kotlin dispatches invoke with a boxed Object argument.
    /// Attempts to unbox the value to the expected type <typeparamref name="T"/>.
    /// </summary>
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

/// <summary>
/// JNI proxy for Kotlin's Function2 (two-parameter lambdas).
/// Used for native SDK callbacks like onBillingError(BillingErrorCode, String)
/// and onServerVerificationRequired(NoctuaPurchaseResult, ConsumableType).
/// </summary>
public class AndroidCallback2 : AndroidJavaProxy
{
    private readonly Action<AndroidJavaObject, AndroidJavaObject> _callback;

    /// <summary>
    /// Creates a new callback proxy that bridges Kotlin Function2 to the specified C# action.
    /// </summary>
    /// <param name="callback">The C# action to invoke with both Kotlin arguments.</param>
    public AndroidCallback2(Action<AndroidJavaObject, AndroidJavaObject> callback)
        : base("kotlin.jvm.functions.Function2")
    {
        _callback = callback;
    }

    /// <summary>
    /// Handles Kotlin invoking the Function2 lambda with two object arguments.
    /// </summary>
    public AndroidJavaObject invoke(AndroidJavaObject arg1, AndroidJavaObject arg2)
    {
        _callback?.Invoke(arg1, arg2);
        return new AndroidJavaClass("kotlin.Unit").GetStatic<AndroidJavaObject>("INSTANCE");
    }
}
#endif