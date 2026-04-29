using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Stub implementation of <see cref="INativePlugin"/> used in the Unity Editor and unsupported platforms.
    /// Provides in-memory or PlayerPrefs-backed storage and no-op implementations for native-only features.
    /// </summary>
    public class DefaultNativePlugin : INativePlugin
    {
        private readonly ILogger _log = new NoctuaLogger(typeof(DefaultNativePlugin));

        // Per-row event storage (in-memory, backed by JSONL file for editor/tests)
        private readonly List<NativeEvent> _eventStore = new();
        private long _nextId = 1;
        private readonly string _eventStorePath;

        /// <summary>
        /// Initializes the default plugin with a JSONL-backed event store in the persistent data path.
        /// </summary>
        public DefaultNativePlugin()
        {
            _eventStorePath = Path.Combine(Application.persistentDataPath, "noctua_events.jsonl");
            LoadEventStore();
        }

        private void LoadEventStore()
        {
            // Migrate old PlayerPrefs blob if present
            var oldBlob = PlayerPrefs.GetString("NoctuaEvents", "");
            if (!string.IsNullOrEmpty(oldBlob) && oldBlob != "[]")
            {
                try
                {
                    var oldEvents = JsonConvert.DeserializeObject<List<string>>(oldBlob);
                    if (oldEvents != null)
                    {
                        foreach (var eventJson in oldEvents)
                        {
                            _eventStore.Add(new NativeEvent
                            {
                                Id = _nextId++,
                                EventJson = eventJson,
                                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                            });
                        }

                        SaveEventStoreToFile();
                        PlayerPrefs.DeleteKey("NoctuaEvents");
                        PlayerPrefs.Save();
                    }
                }
                catch
                {
                    // Old blob corrupted, ignore
                }
            }

            // Load from JSONL file if exists
            if (!File.Exists(_eventStorePath)) return;

            try
            {
                var lines = File.ReadAllLines(_eventStorePath);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var evt = JsonConvert.DeserializeObject<NativeEvent>(line);
                    if (evt != null)
                    {
                        _eventStore.Add(evt);
                        if (evt.Id >= _nextId) _nextId = evt.Id + 1;
                    }
                }
            }
            catch
            {
                // File corrupted, start fresh
                _eventStore.Clear();
                _nextId = 1;
            }
        }

        private void SaveEventStoreToFile()
        {
            try
            {
                var lines = _eventStore.Select(e => JsonConvert.SerializeObject(e));
                File.WriteAllLines(_eventStorePath, lines);
            }
            catch
            {
                // Ignore file write errors in editor
            }
        }

        /// <inheritdoc />
        public void Init(List<string> activeBundleIds)
        {
        }

        /// <inheritdoc />
        public void OnApplicationPause(bool pause)
        {
        }

        /// <summary>
        /// No-op in the Editor; native store services are unavailable.
        /// </summary>
        public void DisposeStoreKit()
        {
        }

        /// <summary>
        /// Always returns false in the Editor; native store services are unavailable.
        /// </summary>
        public bool IsStoreKitReady()
        {
            return false;
        }

        /// <summary>
        /// No-op in the Editor; native lifecycle callbacks are unavailable.
        /// </summary>
        public void RegisterNativeLifecycleCallback(Action<string> callback)
        {
        }

        /// <inheritdoc />
        public void GetFirebaseInstallationID(Action<string> callback) {

        }

        /// <inheritdoc />
        public void GetFirebaseAnalyticsSessionID(Action<string> callback) {

        }

        /// <inheritdoc />
        public void GetFirebaseMessagingToken(Action<string> callback)
        {
            // Editor has no Firebase runtime — return empty synchronously.
            callback?.Invoke(string.Empty);
        }

        /// <inheritdoc />
        public void GetFirebaseRemoteConfigString(string key, Action<string> callback)
        {
            callback?.Invoke(string.Empty);
        }

        /// <inheritdoc />
        public void GetFirebaseRemoteConfigBoolean(string key, Action<bool> callback)
        {
            callback?.Invoke(false);
        }

        /// <inheritdoc />
        public void GetFirebaseRemoteConfigDouble(string key, Action<double> callback)
        {
            callback?.Invoke(0.0);
        }

        /// <inheritdoc />
        public void GetFirebaseRemoteConfigLong(string key, Action<long> callback)
        {
            callback?.Invoke(0L);
        }

        /// <summary>
        /// Not supported in the Editor. Always throws <see cref="NotImplementedException"/>.
        /// </summary>
        public void ShowDatePicker(int year, int month, int day, int id)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void CloseDatePicker()
        {
        }

        /// <summary>
        /// No-op in the Editor; native trackers are unavailable.
        /// </summary>
        public void TrackAdRevenue(string source, double revenue, string currency, Dictionary<string, IConvertible> extraPayload = null)
        {
        }

        /// <summary>
        /// No-op in the Editor; native trackers are unavailable.
        /// </summary>
        public void TrackPurchase(string orderId, double amount, string currency, Dictionary<string, IConvertible> extraPayload = null)
        {
        }

        /// <summary>
        /// No-op in the Editor; native trackers are unavailable.
        /// </summary>
        public void TrackCustomEvent(string name, Dictionary<string, IConvertible> extraPayload = null)
        {
        }

        /// <summary>
        /// No-op in the Editor; native trackers are unavailable.
        /// </summary>
        public void TrackCustomEventWithRevenue(string name, double revenue, string currency, Dictionary<string, IConvertible> extraPayload = null)
        {
        }

        /// <inheritdoc />
        public void OnOnline()
        {
        }

        /// <inheritdoc />
        public void OnOffline()
        {
        }

        /// <summary>
        /// Not supported in the Editor. Always throws <see cref="NotImplementedException"/>.
        /// </summary>
        public void PurchaseItem(string productId, Action<bool, string> callback)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Not supported in the Editor. Always throws <see cref="NotImplementedException"/>.
        /// </summary>
        public void GetActiveCurrency(string productId, Action<bool, string> callback)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Not supported in the Editor. Always throws <see cref="NotImplementedException"/>.
        /// </summary>
        public void GetProductPurchasedById(string productId, Action<bool> callback)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Not supported in the Editor. Always throws <see cref="NotImplementedException"/>.
        /// </summary>
        public void GetReceiptProductPurchasedStoreKit1(string productId, Action<string> callback)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns an empty <see cref="ProductPurchaseStatus"/> in the Editor.
        /// </summary>
        public void GetProductPurchaseStatusDetail(string productId, Action<ProductPurchaseStatus> callback)
        {
            callback?.Invoke(new ProductPurchaseStatus());
        }

        /// <summary>
        /// No-op in the Editor; native store services are unavailable.
        /// </summary>
        public void CompletePurchaseProcessing(string purchaseToken, NoctuaConsumableType consumableType, bool verified, Action<bool> callback)
        {
            callback?.Invoke(true);
        }

        /// <summary>
        /// Retrieves a single account from PlayerPrefs-backed storage by player and game ID.
        /// </summary>
        public NativeAccount GetAccount(long userId, long gameId)
        {
            var rawAccounts = PlayerPrefs.GetString("NoctuaAccountContainer");
            var accounts = JsonConvert.DeserializeObject<List<NativeAccount>>(rawAccounts);

            return accounts.Find(a => a.PlayerId == userId && a.GameId == gameId);
        }

        /// <summary>
        /// Retrieves all accounts from PlayerPrefs-backed storage.
        /// </summary>
        public List<NativeAccount> GetAccounts()
        {
            var rawAccounts = PlayerPrefs.GetString("NoctuaAccountContainer");

            try
            {
                return JsonConvert.DeserializeObject<List<NativeAccount>>(rawAccounts) ?? new List<NativeAccount>();
            }
            catch (Exception)
            {
                _log.Error("Failed to parse account container");

                return new List<NativeAccount>();
            }
        }

        /// <summary>
        /// Inserts or updates an account in PlayerPrefs-backed storage.
        /// </summary>
        public void PutAccount(NativeAccount account)
        {
            var accounts = GetAccounts();

            accounts.RemoveAll(a => a.PlayerId == account.PlayerId && a.GameId == account.GameId);
            account.LastUpdated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            accounts.Add(account);

            PlayerPrefs.SetString("NoctuaAccountContainer", JsonConvert.SerializeObject(accounts));
        }

        /// <summary>
        /// Deletes an account from PlayerPrefs-backed storage.
        /// </summary>
        public int DeleteAccount(NativeAccount account)
        {
            var accounts = GetAccounts();

            accounts.RemoveAll(a => a.PlayerId == account.PlayerId && a.GameId == account.GameId);

            PlayerPrefs.SetString("NoctuaAccountContainer", JsonConvert.SerializeObject(accounts));
            return 1;
        }

        /// <summary>
        /// Returns an empty string in the Editor; Adjust is not available.
        /// </summary>
        public void GetAdjustAttribution(Action<string> callback)
        {
            callback?.Invoke(string.Empty);
        }

        /// <summary>
        /// Saves events as a JSON blob to PlayerPrefs (legacy blob API).
        /// </summary>
        public void SaveEvents(string jsonString)
        {
            PlayerPrefs.SetString("NoctuaEvents", jsonString);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Retrieves events from PlayerPrefs (legacy blob API).
        /// </summary>
        public void GetEvents(Action<List<string>> callback)
        {
            var json = PlayerPrefs.GetString("NoctuaEvents", "[]");
            try
            {
                var events = JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
                callback?.Invoke(events);
            }
            catch
            {
                callback?.Invoke(new List<string>());
            }
        }

        /// <summary>
        /// Deletes all events from PlayerPrefs (legacy blob API).
        /// </summary>
        public void DeleteEvents()
        {
            PlayerPrefs.DeleteKey("NoctuaEvents");
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Inserts a single event into the in-memory store and appends it to the JSONL file.
        /// </summary>
        public void InsertEvent(string eventJson)
        {
            var evt = new NativeEvent
            {
                Id = _nextId++,
                EventJson = eventJson,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            _eventStore.Add(evt);

            // Append to JSONL file
            try
            {
                File.AppendAllText(_eventStorePath, JsonConvert.SerializeObject(evt) + "\n");
            }
            catch
            {
                // Ignore file write errors in editor
            }
        }

        /// <summary>
        /// Retrieves a paginated batch of events from the in-memory store.
        /// </summary>
        public void GetEventsBatch(int limit, int offset, Action<List<NativeEvent>> callback)
        {
            var batch = _eventStore.Skip(offset).Take(limit).ToList();
            callback?.Invoke(batch);
        }

        /// <summary>
        /// Deletes events by their IDs from the in-memory store and persists the change to file.
        /// </summary>
        public void DeleteEventsByIds(long[] ids, Action<int> callback)
        {
            var idSet = new HashSet<long>(ids);
            var removedCount = _eventStore.RemoveAll(e => idSet.Contains(e.Id));
            SaveEventStoreToFile();
            callback?.Invoke(removedCount);
        }

        /// <summary>
        /// Returns the total number of events currently held in the in-memory store.
        /// </summary>
        public void GetEventCount(Action<int> callback)
        {
            callback?.Invoke(_eventStore.Count);
        }

        // ------------------------------------
        // INativeAppManagement
        // ------------------------------------

        public void RequestInAppReview(Action<bool> callback)
        {
            _log.Debug("In-App Review not available in Editor");
            callback?.Invoke(false);
        }

        public void CheckForUpdate(Action<string> callback) => callback?.Invoke("{}");
        public void StartImmediateUpdate(Action<int> callback) => callback?.Invoke(3); // NotAvailable
        public void StartFlexibleUpdate(Action<float> onProgress, Action<int> onResult) => onResult?.Invoke(3);
        public void CompleteUpdate() { }

        // ------------------------------------
        // INativeLogStream / INativeDeviceMetrics — Inspector debug bridges.
        // No-ops in Editor: Unity logs are captured via UnityLogStream, and
        // Editor doesn't expose iOS phys_footprint / Android PSS so an
        // empty snapshot is the truthful answer.
        // ------------------------------------

        public void SetLogStreamEnabled(bool enabled) { }

        public void RegisterNativeLogCallback(Action<int, string, string, string, long> callback) { }

        public DeviceMetricsSnapshot SnapshotDeviceMetrics() =>
            DeviceMetricsSnapshot.Empty(DateTime.UtcNow);
    }
}