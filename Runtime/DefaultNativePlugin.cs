using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace com.noctuagames.sdk
{
    public class DefaultNativePlugin : INativePlugin
    {
        private readonly ILogger _log = new NoctuaLogger(typeof(DefaultNativePlugin));

        // Per-row event storage (in-memory, backed by JSONL file for editor/tests)
        private readonly List<NativeEvent> _eventStore = new();
        private long _nextId = 1;
        private readonly string _eventStorePath;

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

        public void Init(List<string> activeBundleIds)
        {
        }

        public void OnApplicationPause(bool pause)
        {
        }

        public void GetFirebaseInstallationID(Action<string> callback) {
           
        }

        public void GetFirebaseAnalyticsSessionID(Action<string> callback) {

        }

        public void GetFirebaseRemoteConfigString(string key, Action<string> callback)
        {
            callback?.Invoke(string.Empty);
        }

        public void GetFirebaseRemoteConfigBoolean(string key, Action<bool> callback)
        {
            callback?.Invoke(false);
        }

        public void GetFirebaseRemoteConfigDouble(string key, Action<double> callback)
        {
            callback?.Invoke(0.0);
        }

        public void GetFirebaseRemoteConfigLong(string key, Action<long> callback)
        {
            callback?.Invoke(0L);
        }

        public void ShowDatePicker(int year, int month, int day, int id)
        {
            throw new NotImplementedException();
        }

        public void CloseDatePicker()
        {
        }

        public void TrackAdRevenue(string source, double revenue, string currency, Dictionary<string, IConvertible> extraPayload = null)
        {
        }

        public void TrackPurchase(string orderId, double amount, string currency, Dictionary<string, IConvertible> extraPayload = null)
        {
        }

        public void TrackCustomEvent(string name, Dictionary<string, IConvertible> extraPayload = null)
        {
        }

        public void TrackCustomEventWithRevenue(string name, double revenue, string currency, Dictionary<string, IConvertible> extraPayload = null)
        {
        }

        public void OnOnline()
        {
        }

        public void OnOffline()
        {
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

        public void GetProductPurchaseStatusDetail(string productId, Action<ProductPurchaseStatus> callback)
        {
            callback?.Invoke(new ProductPurchaseStatus());
        }

        public NativeAccount GetAccount(long userId, long gameId)
        {
            var rawAccounts = PlayerPrefs.GetString("NoctuaAccountContainer");
            var accounts = JsonConvert.DeserializeObject<List<NativeAccount>>(rawAccounts);

            return accounts.Find(a => a.PlayerId == userId && a.GameId == gameId);
        }

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

        public void PutAccount(NativeAccount account)
        {
            var accounts = GetAccounts();
            
            accounts.RemoveAll(a => a.PlayerId == account.PlayerId && a.GameId == account.GameId);
            account.LastUpdated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            accounts.Add(account);
            
            PlayerPrefs.SetString("NoctuaAccountContainer", JsonConvert.SerializeObject(accounts));
        }

        public int DeleteAccount(NativeAccount account)
        {
            var accounts = GetAccounts();
            
            accounts.RemoveAll(a => a.PlayerId == account.PlayerId && a.GameId == account.GameId);

            PlayerPrefs.SetString("NoctuaAccountContainer", JsonConvert.SerializeObject(accounts));
            return 1;
        }

        public void GetAdjustAttribution(Action<string> callback)
        {
            callback?.Invoke(string.Empty);
        }

        public void SaveEvents(string jsonString)
        {
            PlayerPrefs.SetString("NoctuaEvents", jsonString);
            PlayerPrefs.Save();
        }

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

        public void DeleteEvents()
        {
            PlayerPrefs.DeleteKey("NoctuaEvents");
            PlayerPrefs.Save();
        }

        // Per-row event storage for unlimited event tracking

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

        public void GetEventsBatch(int limit, int offset, Action<List<NativeEvent>> callback)
        {
            var batch = _eventStore.Skip(offset).Take(limit).ToList();
            callback?.Invoke(batch);
        }

        public void DeleteEventsByIds(long[] ids, Action<int> callback)
        {
            var idSet = new HashSet<long>(ids);
            var removedCount = _eventStore.RemoveAll(e => idSet.Contains(e.Id));
            SaveEventStoreToFile();
            callback?.Invoke(removedCount);
        }

        public void GetEventCount(Action<int> callback)
        {
            callback?.Invoke(_eventStore.Count);
        }
    }
}