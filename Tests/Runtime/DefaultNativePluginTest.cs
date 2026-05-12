using System;
using System.Collections;
using com.noctuagames.sdk;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Runtime
{
    public class DefaultNativePluginTest
    {
        private DefaultNativePlugin _plugin;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            PlayerPrefs.DeleteKey("NoctuaAccountContainer");
            _plugin = new DefaultNativePlugin();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlayerPrefs.DeleteKey("NoctuaAccountContainer");
        }

        // GetAccounts tests

        [Test]
        public void GetAccounts_Empty_ReturnsEmptyList()
        {
            var accounts = _plugin.GetAccounts();
            Assert.IsNotNull(accounts);
            Assert.AreEqual(0, accounts.Count);
        }

        [Test]
        public void GetAccounts_CorruptJson_ReturnsEmptyList()
        {
            PlayerPrefs.SetString("NoctuaAccountContainer", "not valid json{{{");
            var accounts = _plugin.GetAccounts();
            Assert.IsNotNull(accounts);
            Assert.AreEqual(0, accounts.Count);
        }

        // PutAccount / GetAccount tests

        [Test]
        public void PutAccount_GetAccount_RoundTrip()
        {
            var account = new NativeAccount
            {
                PlayerId = 1,
                GameId = 100,
                RawData = "{\"test\":true}"
            };

            _plugin.PutAccount(account);

            var retrieved = _plugin.GetAccount(1, 100);
            Assert.IsNotNull(retrieved);
            Assert.AreEqual(1, retrieved.PlayerId);
            Assert.AreEqual(100, retrieved.GameId);
            Assert.AreEqual("{\"test\":true}", retrieved.RawData);
            Assert.Greater(retrieved.LastUpdated, 0);
        }

        [Test]
        public void PutAccount_Duplicate_ReplacesExisting()
        {
            var account1 = new NativeAccount
            {
                PlayerId = 1,
                GameId = 100,
                RawData = "{\"version\":1}"
            };

            var account2 = new NativeAccount
            {
                PlayerId = 1,
                GameId = 100,
                RawData = "{\"version\":2}"
            };

            _plugin.PutAccount(account1);
            _plugin.PutAccount(account2);

            var accounts = _plugin.GetAccounts();
            Assert.AreEqual(1, accounts.Count);
            Assert.AreEqual("{\"version\":2}", accounts[0].RawData);
        }

        [Test]
        public void PutAccount_MultipleAccounts_AllStored()
        {
            _plugin.PutAccount(new NativeAccount { PlayerId = 1, GameId = 100, RawData = "{}" });
            _plugin.PutAccount(new NativeAccount { PlayerId = 2, GameId = 100, RawData = "{}" });
            _plugin.PutAccount(new NativeAccount { PlayerId = 3, GameId = 200, RawData = "{}" });

            var accounts = _plugin.GetAccounts();
            Assert.AreEqual(3, accounts.Count);
        }

        // DeleteAccount tests

        [Test]
        public void DeleteAccount_RemovesAccount()
        {
            var account = new NativeAccount { PlayerId = 1, GameId = 100, RawData = "{}" };
            _plugin.PutAccount(account);

            var result = _plugin.DeleteAccount(account);
            Assert.AreEqual(1, result);

            var accounts = _plugin.GetAccounts();
            Assert.AreEqual(0, accounts.Count);
        }

        [Test]
        public void DeleteAccount_NonExistent_Returns1()
        {
            var account = new NativeAccount { PlayerId = 999, GameId = 999, RawData = "{}" };
            var result = _plugin.DeleteAccount(account);
            Assert.AreEqual(1, result);
        }

        // Init / lifecycle no-op tests

        [Test]
        public void Init_WithEmptyList_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.Init(new System.Collections.Generic.List<string>()));
        }

        [Test]
        public void OnApplicationPause_True_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.OnApplicationPause(true));
        }

        [Test]
        public void OnApplicationPause_False_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.OnApplicationPause(false));
        }

        [Test]
        public void DisposeStoreKit_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.DisposeStoreKit());
        }

        [Test]
        public void IsStoreKitReady_ReturnsFalse()
        {
            Assert.IsFalse(_plugin.IsStoreKitReady());
        }

        [Test]
        public void RegisterNativeLifecycleCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.RegisterNativeLifecycleCallback(_ => { }));
        }

        [Test]
        public void CloseDatePicker_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.CloseDatePicker());
        }

        [Test]
        public void ClearNativeHttpCache_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.ClearNativeHttpCache());
        }

        [Test]
        public void CompleteUpdate_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.CompleteUpdate());
        }

        // TrackAdRevenue / TrackPurchase / TrackCustomEvent / TrackCustomEventWithRevenue

        [Test]
        public void TrackAdRevenue_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.TrackAdRevenue(
                "applovin", 0.01, "USD",
                new System.Collections.Generic.Dictionary<string, IConvertible> { { "k", "v" } }));
        }

        [Test]
        public void TrackAdRevenue_NullPayload_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.TrackAdRevenue("applovin", 0.0, "USD", null));
        }

        [Test]
        public void TrackPurchase_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.TrackPurchase("order-123", 9.99, "USD", null));
        }

        [Test]
        public void TrackCustomEvent_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.TrackCustomEvent("level_complete", null));
        }

        [Test]
        public void TrackCustomEventWithRevenue_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.TrackCustomEventWithRevenue("purchase_done", 4.99, "USD", null));
        }

        // GetFirebaseInstallationID / GetFirebaseAnalyticsSessionID — now call callback synchronously with string.Empty

        [Test]
        public void GetFirebaseInstallationID_CallbackInvokedWithEmpty()
        {
            bool called = false;
            _plugin.GetFirebaseInstallationID(_ => called = true);
            Assert.IsTrue(called, "GetFirebaseInstallationID must invoke callback synchronously in Editor stub");
        }

        [Test]
        public void GetFirebaseAnalyticsSessionID_CallbackInvokedWithEmpty()
        {
            bool called = false;
            _plugin.GetFirebaseAnalyticsSessionID(_ => called = true);
            Assert.IsTrue(called, "GetFirebaseAnalyticsSessionID must invoke callback synchronously in Editor stub");
        }

        // GetFirebaseMessagingToken — does call callback with string.Empty

        [Test]
        public void GetFirebaseMessagingToken_CallsCallbackWithEmpty()
        {
            string result = "not_called";
            _plugin.GetFirebaseMessagingToken(val => result = val);
            Assert.AreEqual(string.Empty, result);
        }

        // OnOnline / OnOffline

        [Test]
        public void OnOnline_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.OnOnline());
        }

        [Test]
        public void OnOffline_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.OnOffline());
        }

        // INativeLogStream / INativeDeviceMetrics

        [Test]
        public void SetLogStreamEnabled_True_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.SetLogStreamEnabled(true));
        }

        [Test]
        public void SetLogStreamEnabled_False_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.SetLogStreamEnabled(false));
        }

        [Test]
        public void RegisterNativeLogCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.RegisterNativeLogCallback((level, source, tag, msg, ts) => { }));
        }

        [Test]
        public void SnapshotDeviceMetrics_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.SnapshotDeviceMetrics());
        }

        // StoreKit methods that throw NotImplementedException

        [Test]
        public void PurchaseItem_ThrowsNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() => _plugin.PurchaseItem("product_1", (ok, msg) => { }));
        }

        [Test]
        public void GetActiveCurrency_ThrowsNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() => _plugin.GetActiveCurrency("product_1", (ok, msg) => { }));
        }

        [Test]
        public void GetProductPurchasedById_ThrowsNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() => _plugin.GetProductPurchasedById("product_1", _ => { }));
        }

        [Test]
        public void GetReceiptProductPurchasedStoreKit1_ThrowsNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() =>
                _plugin.GetReceiptProductPurchasedStoreKit1("product_1", _ => { }));
        }

        // GetProductPurchaseStatusDetail — calls callback with empty status

        [Test]
        public void GetProductPurchaseStatusDetail_CallsCallbackWithEmptyStatus()
        {
            ProductPurchaseStatus result = null;
            _plugin.GetProductPurchaseStatusDetail("product_1", s => result = s);
            Assert.IsNotNull(result);
        }

        // CompletePurchaseProcessing — calls callback with true

        [Test]
        public void CompletePurchaseProcessing_CallsCallbackWithTrue()
        {
            bool? result = null;
            _plugin.CompletePurchaseProcessing("token-abc", NoctuaConsumableType.Consumable, true, ok => result = ok);
            Assert.IsTrue(result);
        }

        // App management callbacks

        [Test]
        public void RequestInAppReview_CallsCallbackWithFalse()
        {
            bool? result = null;
            _plugin.RequestInAppReview(ok => result = ok);
            Assert.IsFalse(result);
        }

        [Test]
        public void CheckForUpdate_CallsCallbackWithEmptyJson()
        {
            string result = null;
            _plugin.CheckForUpdate(s => result = s);
            Assert.AreEqual("{}", result);
        }

        [Test]
        public void StartImmediateUpdate_CallsCallbackWith3()
        {
            int? result = null;
            _plugin.StartImmediateUpdate(code => result = code);
            Assert.AreEqual(3, result);
        }

        [Test]
        public void StartFlexibleUpdate_CallsOnResultWith3()
        {
            int? resultCode = null;
            _plugin.StartFlexibleUpdate(null, code => resultCode = code);
            Assert.AreEqual(3, resultCode);
        }

        // INativeBuildInfo properties

        [Test]
        public void GetNativeSdkVersion_ReturnsEditorString()
        {
            Assert.AreEqual("n/a (Editor)", _plugin.GetNativeSdkVersion());
        }

        [Test]
        public void GetFirebaseProjectId_ReturnsEmpty()
        {
            Assert.AreEqual("", _plugin.GetFirebaseProjectId());
        }

        [Test]
        public void GetSkAdNetworksCount_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, _plugin.GetSkAdNetworksCount());
        }

        [Test]
        public void GetAndroidPermissionsCount_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, _plugin.GetAndroidPermissionsCount());
        }

        // Legacy event storage (PlayerPrefs-backed)

        [Test]
        public void SaveEvents_GetEvents_RoundTrip()
        {
            var json = "[\"event1\",\"event2\"]";
            _plugin.SaveEvents(json);

            System.Collections.Generic.List<string> retrieved = null;
            _plugin.GetEvents(list => retrieved = list);

            Assert.IsNotNull(retrieved);
            Assert.AreEqual(2, retrieved.Count);
            Assert.AreEqual("event1", retrieved[0]);
            Assert.AreEqual("event2", retrieved[1]);
        }

        [Test]
        public void DeleteEvents_ClearsStorage()
        {
            _plugin.SaveEvents("[\"event1\"]");
            _plugin.DeleteEvents();

            System.Collections.Generic.List<string> retrieved = null;
            _plugin.GetEvents(list => retrieved = list);

            Assert.IsNotNull(retrieved);
            Assert.AreEqual(0, retrieved.Count);
        }

        [Test]
        public void GetEvents_WithCorruptJson_ReturnsEmptyList()
        {
            PlayerPrefs.SetString("NoctuaEvents", "not_valid_json");
            System.Collections.Generic.List<string> retrieved = null;
            _plugin.GetEvents(list => retrieved = list);
            Assert.IsNotNull(retrieved);
            Assert.AreEqual(0, retrieved.Count);
        }

        // Per-row event storage (in-memory JSONL-backed)

        [Test]
        public void InsertEvent_GetEventCount_IncreasesCount()
        {
            int? count = null;
            _plugin.GetEventCount(c => count = c);
            var initialCount = count ?? 0;

            _plugin.InsertEvent("{\"event_name\":\"test_event\"}");

            _plugin.GetEventCount(c => count = c);
            Assert.AreEqual(initialCount + 1, count);
        }

        [Test]
        public void GetEventsBatch_LimitAndOffset_ReturnsCorrectSlice()
        {
            _plugin.InsertEvent("{\"event_name\":\"e1\"}");
            _plugin.InsertEvent("{\"event_name\":\"e2\"}");
            _plugin.InsertEvent("{\"event_name\":\"e3\"}");

            System.Collections.Generic.List<NativeEvent> batch = null;
            _plugin.GetEventsBatch(2, 0, list => batch = list);
            Assert.IsNotNull(batch);
            Assert.LessOrEqual(batch.Count, 2);
        }

        [Test]
        public void DeleteEventsByIds_RemovesMatchingEvents()
        {
            _plugin.InsertEvent("{\"event_name\":\"to_delete\"}");

            System.Collections.Generic.List<NativeEvent> batch = null;
            _plugin.GetEventsBatch(10, 0, list => batch = list);
            Assert.IsNotNull(batch);
            Assert.Greater(batch.Count, 0);

            var ids = new long[batch.Count];
            for (int i = 0; i < batch.Count; i++) ids[i] = batch[i].Id;

            int? removedCount = null;
            _plugin.DeleteEventsByIds(ids, c => removedCount = c);

            int? countAfter = null;
            _plugin.GetEventCount(c => countAfter = c);
            Assert.AreEqual(0, countAfter);
        }

        // GetFirebaseRemoteConfig* tests

        [Test]
        public void GetFirebaseRemoteConfigString_ReturnsEmpty()
        {
            string result = null;
            _plugin.GetFirebaseRemoteConfigString("key", val => result = val);
            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void GetFirebaseRemoteConfigBoolean_ReturnsFalse()
        {
            bool? result = null;
            _plugin.GetFirebaseRemoteConfigBoolean("key", val => result = val);
            Assert.AreEqual(false, result);
        }

        [Test]
        public void GetFirebaseRemoteConfigDouble_ReturnsZero()
        {
            double? result = null;
            _plugin.GetFirebaseRemoteConfigDouble("key", val => result = val);
            Assert.AreEqual(0.0, result);
        }

        [Test]
        public void GetFirebaseRemoteConfigLong_ReturnsZero()
        {
            long? result = null;
            _plugin.GetFirebaseRemoteConfigLong("key", val => result = val);
            Assert.AreEqual(0L, result);
        }

        // GetAdjustAttribution test

        [Test]
        public void GetAdjustAttribution_ReturnsEmpty()
        {
            string result = null;
            _plugin.GetAdjustAttribution(val => result = val);
            Assert.AreEqual(string.Empty, result);
        }

        // ShowDatePicker test

        [Test]
        public void ShowDatePicker_ThrowsNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() => _plugin.ShowDatePicker(2024, 1, 1, 0));
        }

        // ── Corrected Firebase callback tests ────────────────────────────────────
        // The source was updated to call callback?.Invoke(string.Empty) synchronously.
        // The earlier tests in this file assert IsFalse(called) which no longer matches
        // the implementation. These tests document and verify the current behaviour.

        [Test]
        public void GetFirebaseInstallationID_InvokesCallbackWithEmptyString()
        {
            string received = "sentinel";
            _plugin.GetFirebaseInstallationID(val => received = val);
            Assert.AreEqual(string.Empty, received,
                "GetFirebaseInstallationID must invoke callback with string.Empty in Editor stub");
        }

        [Test]
        public void GetFirebaseAnalyticsSessionID_InvokesCallbackWithEmptyString()
        {
            string received = "sentinel";
            _plugin.GetFirebaseAnalyticsSessionID(val => received = val);
            Assert.AreEqual(string.Empty, received,
                "GetFirebaseAnalyticsSessionID must invoke callback with string.Empty in Editor stub");
        }

        // ── Null-callback safety ─────────────────────────────────────────────────

        [Test]
        public void GetFirebaseInstallationID_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.GetFirebaseInstallationID(null));
        }

        [Test]
        public void GetFirebaseAnalyticsSessionID_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.GetFirebaseAnalyticsSessionID(null));
        }

        [Test]
        public void GetFirebaseMessagingToken_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.GetFirebaseMessagingToken(null));
        }

        [Test]
        public void GetProductPurchaseStatusDetail_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.GetProductPurchaseStatusDetail("product_1", null));
        }

        [Test]
        public void CompletePurchaseProcessing_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                _plugin.CompletePurchaseProcessing("token", NoctuaConsumableType.Consumable, true, null));
        }

        [Test]
        public void GetAdjustAttribution_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.GetAdjustAttribution(null));
        }

        [Test]
        public void GetEvents_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.GetEvents(null));
        }

        [Test]
        public void GetEventCount_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.GetEventCount(null));
        }

        [Test]
        public void GetEventsBatch_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.GetEventsBatch(10, 0, null));
        }

        [Test]
        public void DeleteEventsByIds_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.DeleteEventsByIds(new long[0], null));
        }

        // ── GetAccount edge cases ────────────────────────────────────────────────

        [Test]
        public void GetAccount_WhenNoMatchingAccount_ReturnsNull()
        {
            _plugin.PutAccount(new NativeAccount { PlayerId = 1, GameId = 100, RawData = "{}" });
            var result = _plugin.GetAccount(999, 999);
            Assert.IsNull(result);
        }

        [Test]
        public void GetAccount_MatchesByBothPlayerIdAndGameId()
        {
            _plugin.PutAccount(new NativeAccount { PlayerId = 1, GameId = 100, RawData = "{\"a\":1}" });
            _plugin.PutAccount(new NativeAccount { PlayerId = 1, GameId = 200, RawData = "{\"a\":2}" });

            var result = _plugin.GetAccount(1, 200);
            Assert.IsNotNull(result);
            Assert.AreEqual("{\"a\":2}", result.RawData);
        }

        // ── SnapshotDeviceMetrics returns non-null ────────────────────────────────

        [Test]
        public void SnapshotDeviceMetrics_ReturnsNonNull()
        {
            var snapshot = _plugin.SnapshotDeviceMetrics();
            Assert.IsNotNull(snapshot);
        }

        // ── StartFlexibleUpdate with onProgress callback ──────────────────────────

        [Test]
        public void StartFlexibleUpdate_WithOnProgressCallback_DoesNotThrow()
        {
            float? progress = null;
            int? resultCode = null;
            Assert.DoesNotThrow(() =>
                _plugin.StartFlexibleUpdate(p => progress = p, code => resultCode = code));
            Assert.AreEqual(3, resultCode);
        }

        // ── GetEventsBatch with non-zero offset ───────────────────────────────────

        [Test]
        public void GetEventsBatch_WithOffset_SkipsEvents()
        {
            _plugin.InsertEvent("{\"event_name\":\"e1\"}");
            _plugin.InsertEvent("{\"event_name\":\"e2\"}");
            _plugin.InsertEvent("{\"event_name\":\"e3\"}");

            System.Collections.Generic.List<NativeEvent> allBatch = null;
            _plugin.GetEventsBatch(10, 0, list => allBatch = list);

            System.Collections.Generic.List<NativeEvent> offsetBatch = null;
            _plugin.GetEventsBatch(10, 2, list => offsetBatch = list);

            Assert.IsNotNull(offsetBatch);
            Assert.AreEqual(allBatch.Count - 2, offsetBatch.Count);
        }

        // ── DeleteEventsByIds with empty array ────────────────────────────────────

        [Test]
        public void DeleteEventsByIds_EmptyArray_RemovesZeroEvents()
        {
            _plugin.InsertEvent("{\"event_name\":\"keep_me\"}");

            int? removedCount = null;
            _plugin.DeleteEventsByIds(new long[0], c => removedCount = c);

            Assert.AreEqual(0, removedCount);

            int? countAfter = null;
            _plugin.GetEventCount(c => countAfter = c);
            Assert.Greater(countAfter, 0);
        }

        // ── Legacy PlayerPrefs migration path ─────────────────────────────────────
        // LoadEventStore migrates old NoctuaEvents blob from PlayerPrefs into the
        // in-memory store on first construction.

        [Test]
        public void Constructor_WithLegacyPlayerPrefsBlobPresent_MigratesEventsToStore()
        {
            // Arrange: write a legacy blob before constructing a new plugin instance
            PlayerPrefs.SetString("NoctuaEvents", "[\"legacy_event_1\",\"legacy_event_2\"]");
            PlayerPrefs.Save();

            var freshPlugin = new DefaultNativePlugin();

            // Assert: events are migrated into the in-memory store
            int? count = null;
            freshPlugin.GetEventCount(c => count = c);
            Assert.AreEqual(2, count, "Legacy blob events should be migrated into the per-row store");

            // Assert: the legacy PlayerPrefs key is cleared after migration
            Assert.AreEqual("", PlayerPrefs.GetString("NoctuaEvents", ""),
                "NoctuaEvents PlayerPrefs key should be deleted after migration");
        }

        [Test]
        public void Constructor_WithCorruptLegacyPlayerPrefsBlob_DoesNotThrow()
        {
            PlayerPrefs.SetString("NoctuaEvents", "{ not valid json [[[");
            PlayerPrefs.Save();

            DefaultNativePlugin freshPlugin = null;
            Assert.DoesNotThrow(() => freshPlugin = new DefaultNativePlugin());
            Assert.IsNotNull(freshPlugin);
        }

        // ── CompletePurchaseProcessing with verified=false ────────────────────────

        [Test]
        public void CompletePurchaseProcessing_UnverifiedPurchase_StillCallsCallbackWithTrue()
        {
            bool? result = null;
            _plugin.CompletePurchaseProcessing("token-xyz", NoctuaConsumableType.NonConsumable, false, ok => result = ok);
            Assert.IsTrue(result,
                "Editor stub always returns true regardless of the verified flag");
        }

        // ── RequestInAppReview null-callback safety ───────────────────────────────

        [Test]
        public void RequestInAppReview_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.RequestInAppReview(null));
        }

        // ── CheckForUpdate / StartImmediateUpdate / StartFlexibleUpdate null-safety

        [Test]
        public void CheckForUpdate_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.CheckForUpdate(null));
        }

        [Test]
        public void StartImmediateUpdate_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.StartImmediateUpdate(null));
        }

        [Test]
        public void StartFlexibleUpdate_NullCallbacks_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.StartFlexibleUpdate(null, null));
        }
    }
}
