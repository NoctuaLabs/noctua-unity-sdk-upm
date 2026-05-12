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
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlayerPrefs.DeleteKey("NoctuaAccountContainer");
            yield return null;
        }

        // GetAccounts tests

        [Test]
        [Timeout(5000)]
        public IEnumerator GetAccounts_Empty_ReturnsEmptyList()
        {
            var accounts = _plugin.GetAccounts();
            Assert.IsNotNull(accounts);
            Assert.AreEqual(0, accounts.Count);
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetAccounts_CorruptJson_ReturnsEmptyList()
        {
            PlayerPrefs.SetString("NoctuaAccountContainer", "not valid json{{{");
            var accounts = _plugin.GetAccounts();
            Assert.IsNotNull(accounts);
            Assert.AreEqual(0, accounts.Count);
            yield return null;
        }

        // PutAccount / GetAccount tests

        [Test]
        [Timeout(5000)]
        public IEnumerator PutAccount_GetAccount_RoundTrip()
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
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator PutAccount_Duplicate_ReplacesExisting()
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
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator PutAccount_MultipleAccounts_AllStored()
        {
            _plugin.PutAccount(new NativeAccount { PlayerId = 1, GameId = 100, RawData = "{}" });
            _plugin.PutAccount(new NativeAccount { PlayerId = 2, GameId = 100, RawData = "{}" });
            _plugin.PutAccount(new NativeAccount { PlayerId = 3, GameId = 200, RawData = "{}" });

            var accounts = _plugin.GetAccounts();
            Assert.AreEqual(3, accounts.Count);
            yield return null;
        }

        // DeleteAccount tests

        [Test]
        [Timeout(5000)]
        public IEnumerator DeleteAccount_RemovesAccount()
        {
            var account = new NativeAccount { PlayerId = 1, GameId = 100, RawData = "{}" };
            _plugin.PutAccount(account);

            var result = _plugin.DeleteAccount(account);
            Assert.AreEqual(1, result);

            var accounts = _plugin.GetAccounts();
            Assert.AreEqual(0, accounts.Count);
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator DeleteAccount_NonExistent_Returns1()
        {
            var account = new NativeAccount { PlayerId = 999, GameId = 999, RawData = "{}" };
            var result = _plugin.DeleteAccount(account);
            Assert.AreEqual(1, result);
            yield return null;
        }

        // Init / lifecycle no-op tests

        [Test]
        [Timeout(5000)]
        public IEnumerator Init_WithEmptyList_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.Init(new System.Collections.Generic.List<string>()));
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator OnApplicationPause_True_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.OnApplicationPause(true));
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator OnApplicationPause_False_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.OnApplicationPause(false));
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator DisposeStoreKit_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.DisposeStoreKit());
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator IsStoreKitReady_ReturnsFalse()
        {
            Assert.IsFalse(_plugin.IsStoreKitReady());
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator RegisterNativeLifecycleCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.RegisterNativeLifecycleCallback(_ => { }));
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator CloseDatePicker_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.CloseDatePicker());
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator ClearNativeHttpCache_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.ClearNativeHttpCache());
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator CompleteUpdate_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.CompleteUpdate());
            yield return null;
        }

        // TrackAdRevenue / TrackPurchase / TrackCustomEvent / TrackCustomEventWithRevenue

        [Test]
        [Timeout(5000)]
        public IEnumerator TrackAdRevenue_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.TrackAdRevenue(
                "applovin", 0.01, "USD",
                new System.Collections.Generic.Dictionary<string, IConvertible> { { "k", "v" } }));
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator TrackAdRevenue_NullPayload_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.TrackAdRevenue("applovin", 0.0, "USD", null));
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator TrackPurchase_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.TrackPurchase("order-123", 9.99, "USD", null));
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator TrackCustomEvent_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.TrackCustomEvent("level_complete", null));
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator TrackCustomEventWithRevenue_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.TrackCustomEventWithRevenue("purchase_done", 4.99, "USD", null));
            yield return null;
        }

        // GetFirebaseInstallationID / GetFirebaseAnalyticsSessionID — now call callback synchronously with string.Empty

        [Test]
        [Timeout(5000)]
        public IEnumerator GetFirebaseInstallationID_CallbackInvokedWithEmpty()
        {
            bool called = false;
            _plugin.GetFirebaseInstallationID(_ => called = true);
            Assert.IsTrue(called, "GetFirebaseInstallationID must invoke callback synchronously in Editor stub");
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetFirebaseAnalyticsSessionID_CallbackInvokedWithEmpty()
        {
            bool called = false;
            _plugin.GetFirebaseAnalyticsSessionID(_ => called = true);
            Assert.IsTrue(called, "GetFirebaseAnalyticsSessionID must invoke callback synchronously in Editor stub");
            yield return null;
        }

        // GetFirebaseMessagingToken — does call callback with string.Empty

        [Test]
        [Timeout(5000)]
        public IEnumerator GetFirebaseMessagingToken_CallsCallbackWithEmpty()
        {
            string result = "not_called";
            _plugin.GetFirebaseMessagingToken(val => result = val);
            Assert.AreEqual(string.Empty, result);
            yield return null;
        }

        // OnOnline / OnOffline

        [Test]
        [Timeout(5000)]
        public IEnumerator OnOnline_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.OnOnline());
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator OnOffline_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.OnOffline());
            yield return null;
        }

        // INativeLogStream / INativeDeviceMetrics

        [Test]
        [Timeout(5000)]
        public IEnumerator SetLogStreamEnabled_True_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.SetLogStreamEnabled(true));
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator SetLogStreamEnabled_False_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.SetLogStreamEnabled(false));
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator RegisterNativeLogCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.RegisterNativeLogCallback((level, source, tag, msg, ts) => { }));
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator SnapshotDeviceMetrics_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.SnapshotDeviceMetrics());
            yield return null;
        }

        // StoreKit methods that throw NotImplementedException

        [Test]
        [Timeout(5000)]
        public IEnumerator PurchaseItem_ThrowsNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() => _plugin.PurchaseItem("product_1", (ok, msg) => { }));
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetActiveCurrency_ThrowsNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() => _plugin.GetActiveCurrency("product_1", (ok, msg) => { }));
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetProductPurchasedById_ThrowsNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() => _plugin.GetProductPurchasedById("product_1", _ => { }));
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetReceiptProductPurchasedStoreKit1_ThrowsNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() =>
                _plugin.GetReceiptProductPurchasedStoreKit1("product_1", _ => { }));
            yield return null;
        }

        // GetProductPurchaseStatusDetail — calls callback with empty status

        [Test]
        [Timeout(5000)]
        public IEnumerator GetProductPurchaseStatusDetail_CallsCallbackWithEmptyStatus()
        {
            ProductPurchaseStatus result = null;
            _plugin.GetProductPurchaseStatusDetail("product_1", s => result = s);
            Assert.IsNotNull(result);
            yield return null;
        }

        // CompletePurchaseProcessing — calls callback with true

        [Test]
        [Timeout(5000)]
        public IEnumerator CompletePurchaseProcessing_CallsCallbackWithTrue()
        {
            bool? result = null;
            _plugin.CompletePurchaseProcessing("token-abc", NoctuaConsumableType.Consumable, true, ok => result = ok);
            Assert.IsTrue(result);
            yield return null;
        }

        // App management callbacks

        [Test]
        [Timeout(5000)]
        public IEnumerator RequestInAppReview_CallsCallbackWithFalse()
        {
            bool? result = null;
            _plugin.RequestInAppReview(ok => result = ok);
            Assert.IsFalse(result);
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator CheckForUpdate_CallsCallbackWithEmptyJson()
        {
            string result = null;
            _plugin.CheckForUpdate(s => result = s);
            Assert.AreEqual("{}", result);
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator StartImmediateUpdate_CallsCallbackWith3()
        {
            int? result = null;
            _plugin.StartImmediateUpdate(code => result = code);
            Assert.AreEqual(3, result);
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator StartFlexibleUpdate_CallsOnResultWith3()
        {
            int? resultCode = null;
            _plugin.StartFlexibleUpdate(null, code => resultCode = code);
            Assert.AreEqual(3, resultCode);
            yield return null;
        }

        // INativeBuildInfo properties

        [Test]
        [Timeout(5000)]
        public IEnumerator GetNativeSdkVersion_ReturnsEditorString()
        {
            Assert.AreEqual("n/a (Editor)", _plugin.GetNativeSdkVersion());
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetFirebaseProjectId_ReturnsEmpty()
        {
            Assert.AreEqual("", _plugin.GetFirebaseProjectId());
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetSkAdNetworksCount_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, _plugin.GetSkAdNetworksCount());
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetAndroidPermissionsCount_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, _plugin.GetAndroidPermissionsCount());
            yield return null;
        }

        // Legacy event storage (PlayerPrefs-backed)

        [Test]
        [Timeout(5000)]
        public IEnumerator SaveEvents_GetEvents_RoundTrip()
        {
            var json = "[\"event1\",\"event2\"]";
            _plugin.SaveEvents(json);

            System.Collections.Generic.List<string> retrieved = null;
            _plugin.GetEvents(list => retrieved = list);

            Assert.IsNotNull(retrieved);
            Assert.AreEqual(2, retrieved.Count);
            Assert.AreEqual("event1", retrieved[0]);
            Assert.AreEqual("event2", retrieved[1]);
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator DeleteEvents_ClearsStorage()
        {
            _plugin.SaveEvents("[\"event1\"]");
            _plugin.DeleteEvents();

            System.Collections.Generic.List<string> retrieved = null;
            _plugin.GetEvents(list => retrieved = list);

            Assert.IsNotNull(retrieved);
            Assert.AreEqual(0, retrieved.Count);
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetEvents_WithCorruptJson_ReturnsEmptyList()
        {
            PlayerPrefs.SetString("NoctuaEvents", "not_valid_json");
            System.Collections.Generic.List<string> retrieved = null;
            _plugin.GetEvents(list => retrieved = list);
            Assert.IsNotNull(retrieved);
            Assert.AreEqual(0, retrieved.Count);
            yield return null;
        }

        // Per-row event storage (in-memory JSONL-backed)

        [Test]
        [Timeout(5000)]
        public IEnumerator InsertEvent_GetEventCount_IncreasesCount()
        {
            int? count = null;
            _plugin.GetEventCount(c => count = c);
            var initialCount = count ?? 0;

            _plugin.InsertEvent("{\"event_name\":\"test_event\"}");

            _plugin.GetEventCount(c => count = c);
            Assert.AreEqual(initialCount + 1, count);
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetEventsBatch_LimitAndOffset_ReturnsCorrectSlice()
        {
            _plugin.InsertEvent("{\"event_name\":\"e1\"}");
            _plugin.InsertEvent("{\"event_name\":\"e2\"}");
            _plugin.InsertEvent("{\"event_name\":\"e3\"}");

            System.Collections.Generic.List<NativeEvent> batch = null;
            _plugin.GetEventsBatch(2, 0, list => batch = list);
            Assert.IsNotNull(batch);
            Assert.LessOrEqual(batch.Count, 2);
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator DeleteEventsByIds_RemovesMatchingEvents()
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
            yield return null;
        }

        // GetFirebaseRemoteConfig* tests

        [Test]
        [Timeout(5000)]
        public IEnumerator GetFirebaseRemoteConfigString_ReturnsEmpty()
        {
            string result = null;
            _plugin.GetFirebaseRemoteConfigString("key", val => result = val);
            Assert.AreEqual(string.Empty, result);
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetFirebaseRemoteConfigBoolean_ReturnsFalse()
        {
            bool? result = null;
            _plugin.GetFirebaseRemoteConfigBoolean("key", val => result = val);
            Assert.AreEqual(false, result);
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetFirebaseRemoteConfigDouble_ReturnsZero()
        {
            double? result = null;
            _plugin.GetFirebaseRemoteConfigDouble("key", val => result = val);
            Assert.AreEqual(0.0, result);
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetFirebaseRemoteConfigLong_ReturnsZero()
        {
            long? result = null;
            _plugin.GetFirebaseRemoteConfigLong("key", val => result = val);
            Assert.AreEqual(0L, result);
            yield return null;
        }

        // GetAdjustAttribution test

        [Test]
        [Timeout(5000)]
        public IEnumerator GetAdjustAttribution_ReturnsEmpty()
        {
            string result = null;
            _plugin.GetAdjustAttribution(val => result = val);
            Assert.AreEqual(string.Empty, result);
            yield return null;
        }

        // ShowDatePicker test

        [Test]
        [Timeout(5000)]
        public IEnumerator ShowDatePicker_ThrowsNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() => _plugin.ShowDatePicker(2024, 1, 1, 0));
            yield return null;
        }

        // ── Corrected Firebase callback tests ────────────────────────────────────
        // The source was updated to call callback?.Invoke(string.Empty) synchronously.
        // The earlier tests in this file assert IsFalse(called) which no longer matches
        // the implementation. These tests document and verify the current behaviour.

        [Test]
        [Timeout(5000)]
        public IEnumerator GetFirebaseInstallationID_InvokesCallbackWithEmptyString()
        {
            string received = "sentinel";
            _plugin.GetFirebaseInstallationID(val => received = val);
            Assert.AreEqual(string.Empty, received,
                "GetFirebaseInstallationID must invoke callback with string.Empty in Editor stub");
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetFirebaseAnalyticsSessionID_InvokesCallbackWithEmptyString()
        {
            string received = "sentinel";
            _plugin.GetFirebaseAnalyticsSessionID(val => received = val);
            Assert.AreEqual(string.Empty, received,
                "GetFirebaseAnalyticsSessionID must invoke callback with string.Empty in Editor stub");
            yield return null;
        }

        // ── Null-callback safety ─────────────────────────────────────────────────

        [Test]
        [Timeout(5000)]
        public IEnumerator GetFirebaseInstallationID_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.GetFirebaseInstallationID(null));
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetFirebaseAnalyticsSessionID_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.GetFirebaseAnalyticsSessionID(null));
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetFirebaseMessagingToken_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.GetFirebaseMessagingToken(null));
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetProductPurchaseStatusDetail_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.GetProductPurchaseStatusDetail("product_1", null));
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator CompletePurchaseProcessing_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                _plugin.CompletePurchaseProcessing("token", NoctuaConsumableType.Consumable, true, null));
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetAdjustAttribution_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.GetAdjustAttribution(null));
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetEvents_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.GetEvents(null));
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetEventCount_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.GetEventCount(null));
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetEventsBatch_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.GetEventsBatch(10, 0, null));
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator DeleteEventsByIds_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.DeleteEventsByIds(new long[0], null));
            yield return null;
        }

        // ── GetAccount edge cases ────────────────────────────────────────────────

        [Test]
        [Timeout(5000)]
        public IEnumerator GetAccount_WhenNoMatchingAccount_ReturnsNull()
        {
            _plugin.PutAccount(new NativeAccount { PlayerId = 1, GameId = 100, RawData = "{}" });
            var result = _plugin.GetAccount(999, 999);
            Assert.IsNull(result);
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetAccount_MatchesByBothPlayerIdAndGameId()
        {
            _plugin.PutAccount(new NativeAccount { PlayerId = 1, GameId = 100, RawData = "{\"a\":1}" });
            _plugin.PutAccount(new NativeAccount { PlayerId = 1, GameId = 200, RawData = "{\"a\":2}" });

            var result = _plugin.GetAccount(1, 200);
            Assert.IsNotNull(result);
            Assert.AreEqual("{\"a\":2}", result.RawData);
            yield return null;
        }

        // ── SnapshotDeviceMetrics returns non-null ────────────────────────────────

        [Test]
        [Timeout(5000)]
        public IEnumerator SnapshotDeviceMetrics_ReturnsNonNull()
        {
            var snapshot = _plugin.SnapshotDeviceMetrics();
            Assert.IsNotNull(snapshot);
            yield return null;
        }

        // ── StartFlexibleUpdate with onProgress callback ──────────────────────────

        [Test]
        [Timeout(5000)]
        public IEnumerator StartFlexibleUpdate_WithOnProgressCallback_DoesNotThrow()
        {
            float? progress = null;
            int? resultCode = null;
            Assert.DoesNotThrow(() =>
                _plugin.StartFlexibleUpdate(p => progress = p, code => resultCode = code));
            Assert.AreEqual(3, resultCode);
            yield return null;
        }

        // ── GetEventsBatch with non-zero offset ───────────────────────────────────

        [Test]
        [Timeout(5000)]
        public IEnumerator GetEventsBatch_WithOffset_SkipsEvents()
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
            yield return null;
        }

        // ── DeleteEventsByIds with empty array ────────────────────────────────────

        [Test]
        [Timeout(5000)]
        public IEnumerator DeleteEventsByIds_EmptyArray_RemovesZeroEvents()
        {
            _plugin.InsertEvent("{\"event_name\":\"keep_me\"}");

            int? removedCount = null;
            _plugin.DeleteEventsByIds(new long[0], c => removedCount = c);

            Assert.AreEqual(0, removedCount);

            int? countAfter = null;
            _plugin.GetEventCount(c => countAfter = c);
            Assert.Greater(countAfter, 0);
            yield return null;
        }

        // ── Legacy PlayerPrefs migration path ─────────────────────────────────────
        // LoadEventStore migrates old NoctuaEvents blob from PlayerPrefs into the
        // in-memory store on first construction.

        [Test]
        [Timeout(5000)]
        public IEnumerator Constructor_WithLegacyPlayerPrefsBlobPresent_MigratesEventsToStore()
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
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator Constructor_WithCorruptLegacyPlayerPrefsBlob_DoesNotThrow()
        {
            PlayerPrefs.SetString("NoctuaEvents", "{ not valid json [[[");
            PlayerPrefs.Save();

            DefaultNativePlugin freshPlugin = null;
            Assert.DoesNotThrow(() => freshPlugin = new DefaultNativePlugin());
            Assert.IsNotNull(freshPlugin);
            yield return null;
        }

        // ── CompletePurchaseProcessing with verified=false ────────────────────────

        [Test]
        [Timeout(5000)]
        public IEnumerator CompletePurchaseProcessing_UnverifiedPurchase_StillCallsCallbackWithTrue()
        {
            bool? result = null;
            _plugin.CompletePurchaseProcessing("token-xyz", NoctuaConsumableType.NonConsumable, false, ok => result = ok);
            Assert.IsTrue(result,
                "Editor stub always returns true regardless of the verified flag");
            yield return null;
        }

        // ── RequestInAppReview null-callback safety ───────────────────────────────

        [Test]
        [Timeout(5000)]
        public IEnumerator RequestInAppReview_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.RequestInAppReview(null));
            yield return null;
        }

        // ── CheckForUpdate / StartImmediateUpdate / StartFlexibleUpdate null-safety

        [Test]
        [Timeout(5000)]
        public IEnumerator CheckForUpdate_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.CheckForUpdate(null));
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator StartImmediateUpdate_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.StartImmediateUpdate(null));
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator StartFlexibleUpdate_NullCallbacks_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.StartFlexibleUpdate(null, null));
            yield return null;
        }
    }
}
