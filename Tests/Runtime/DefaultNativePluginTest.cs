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

        [UnityTest]
        public IEnumerator GetAccounts_Empty_ReturnsEmptyList()
        {
            var accounts = _plugin.GetAccounts();
            Assert.IsNotNull(accounts);
            Assert.AreEqual(0, accounts.Count);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetAccounts_CorruptJson_ReturnsEmptyList()
        {
            PlayerPrefs.SetString("NoctuaAccountContainer", "not valid json{{{");
            var accounts = _plugin.GetAccounts();
            Assert.IsNotNull(accounts);
            Assert.AreEqual(0, accounts.Count);
            yield return null;
        }

        // PutAccount / GetAccount tests

        [UnityTest]
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

        [UnityTest]
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

        [UnityTest]
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

        [UnityTest]
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

        [UnityTest]
        public IEnumerator DeleteAccount_NonExistent_Returns1()
        {
            var account = new NativeAccount { PlayerId = 999, GameId = 999, RawData = "{}" };
            var result = _plugin.DeleteAccount(account);
            Assert.AreEqual(1, result);
            yield return null;
        }

        // Init / lifecycle no-op tests

        [UnityTest]
        public IEnumerator Init_WithEmptyList_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.Init(new System.Collections.Generic.List<string>()));
            yield return null;
        }

        [UnityTest]
        public IEnumerator OnApplicationPause_True_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.OnApplicationPause(true));
            yield return null;
        }

        [UnityTest]
        public IEnumerator OnApplicationPause_False_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.OnApplicationPause(false));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisposeStoreKit_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.DisposeStoreKit());
            yield return null;
        }

        [UnityTest]
        public IEnumerator IsStoreKitReady_ReturnsFalse()
        {
            Assert.IsFalse(_plugin.IsStoreKitReady());
            yield return null;
        }

        [UnityTest]
        public IEnumerator RegisterNativeLifecycleCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.RegisterNativeLifecycleCallback(_ => { }));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CloseDatePicker_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.CloseDatePicker());
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClearNativeHttpCache_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.ClearNativeHttpCache());
            yield return null;
        }

        [UnityTest]
        public IEnumerator CompleteUpdate_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.CompleteUpdate());
            yield return null;
        }

        // TrackAdRevenue / TrackPurchase / TrackCustomEvent / TrackCustomEventWithRevenue

        [UnityTest]
        public IEnumerator TrackAdRevenue_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.TrackAdRevenue(
                "applovin", 0.01, "USD",
                new System.Collections.Generic.Dictionary<string, IConvertible> { { "k", "v" } }));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrackAdRevenue_NullPayload_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.TrackAdRevenue("applovin", 0.0, "USD", null));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrackPurchase_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.TrackPurchase("order-123", 9.99, "USD", null));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrackCustomEvent_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.TrackCustomEvent("level_complete", null));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrackCustomEventWithRevenue_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.TrackCustomEventWithRevenue("purchase_done", 4.99, "USD", null));
            yield return null;
        }

        // GetFirebaseInstallationID / GetFirebaseAnalyticsSessionID — empty bodies (callback NOT called)

        [UnityTest]
        public IEnumerator GetFirebaseInstallationID_CallbackNotInvoked()
        {
            bool called = false;
            _plugin.GetFirebaseInstallationID(_ => called = true);
            Assert.IsFalse(called, "GetFirebaseInstallationID should NOT invoke callback in Editor stub");
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetFirebaseAnalyticsSessionID_CallbackNotInvoked()
        {
            bool called = false;
            _plugin.GetFirebaseAnalyticsSessionID(_ => called = true);
            Assert.IsFalse(called, "GetFirebaseAnalyticsSessionID should NOT invoke callback in Editor stub");
            yield return null;
        }

        // GetFirebaseMessagingToken — does call callback with string.Empty

        [UnityTest]
        public IEnumerator GetFirebaseMessagingToken_CallsCallbackWithEmpty()
        {
            string result = "not_called";
            _plugin.GetFirebaseMessagingToken(val => result = val);
            Assert.AreEqual(string.Empty, result);
            yield return null;
        }

        // OnOnline / OnOffline

        [UnityTest]
        public IEnumerator OnOnline_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.OnOnline());
            yield return null;
        }

        [UnityTest]
        public IEnumerator OnOffline_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.OnOffline());
            yield return null;
        }

        // INativeLogStream / INativeDeviceMetrics

        [UnityTest]
        public IEnumerator SetLogStreamEnabled_True_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.SetLogStreamEnabled(true));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SetLogStreamEnabled_False_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.SetLogStreamEnabled(false));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RegisterNativeLogCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.RegisterNativeLogCallback((level, source, tag, msg, ts) => { }));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SnapshotDeviceMetrics_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.SnapshotDeviceMetrics());
            yield return null;
        }

        // StoreKit methods that throw NotImplementedException

        [UnityTest]
        public IEnumerator PurchaseItem_ThrowsNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() => _plugin.PurchaseItem("product_1", (ok, msg) => { }));
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetActiveCurrency_ThrowsNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() => _plugin.GetActiveCurrency("product_1", (ok, msg) => { }));
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetProductPurchasedById_ThrowsNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() => _plugin.GetProductPurchasedById("product_1", _ => { }));
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetReceiptProductPurchasedStoreKit1_ThrowsNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() =>
                _plugin.GetReceiptProductPurchasedStoreKit1("product_1", _ => { }));
            yield return null;
        }

        // GetProductPurchaseStatusDetail — calls callback with empty status

        [UnityTest]
        public IEnumerator GetProductPurchaseStatusDetail_CallsCallbackWithEmptyStatus()
        {
            ProductPurchaseStatus result = null;
            _plugin.GetProductPurchaseStatusDetail("product_1", s => result = s);
            Assert.IsNotNull(result);
            yield return null;
        }

        // CompletePurchaseProcessing — calls callback with true

        [UnityTest]
        public IEnumerator CompletePurchaseProcessing_CallsCallbackWithTrue()
        {
            bool? result = null;
            _plugin.CompletePurchaseProcessing("token-abc", NoctuaConsumableType.Consumable, true, ok => result = ok);
            Assert.IsTrue(result);
            yield return null;
        }

        // App management callbacks

        [UnityTest]
        public IEnumerator RequestInAppReview_CallsCallbackWithFalse()
        {
            bool? result = null;
            _plugin.RequestInAppReview(ok => result = ok);
            Assert.IsFalse(result);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CheckForUpdate_CallsCallbackWithEmptyJson()
        {
            string result = null;
            _plugin.CheckForUpdate(s => result = s);
            Assert.AreEqual("{}", result);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartImmediateUpdate_CallsCallbackWith3()
        {
            int? result = null;
            _plugin.StartImmediateUpdate(code => result = code);
            Assert.AreEqual(3, result);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartFlexibleUpdate_CallsOnResultWith3()
        {
            int? resultCode = null;
            _plugin.StartFlexibleUpdate(null, code => resultCode = code);
            Assert.AreEqual(3, resultCode);
            yield return null;
        }

        // INativeBuildInfo properties

        [UnityTest]
        public IEnumerator GetNativeSdkVersion_ReturnsEditorString()
        {
            Assert.AreEqual("n/a (Editor)", _plugin.GetNativeSdkVersion());
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetFirebaseProjectId_ReturnsEmpty()
        {
            Assert.AreEqual("", _plugin.GetFirebaseProjectId());
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetSkAdNetworksCount_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, _plugin.GetSkAdNetworksCount());
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetAndroidPermissionsCount_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, _plugin.GetAndroidPermissionsCount());
            yield return null;
        }

        // Legacy event storage (PlayerPrefs-backed)

        [UnityTest]
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

        [UnityTest]
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

        [UnityTest]
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

        [UnityTest]
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

        [UnityTest]
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

        [UnityTest]
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

        [UnityTest]
        public IEnumerator GetFirebaseRemoteConfigString_ReturnsEmpty()
        {
            string result = null;
            _plugin.GetFirebaseRemoteConfigString("key", val => result = val);
            Assert.AreEqual(string.Empty, result);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetFirebaseRemoteConfigBoolean_ReturnsFalse()
        {
            bool? result = null;
            _plugin.GetFirebaseRemoteConfigBoolean("key", val => result = val);
            Assert.AreEqual(false, result);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetFirebaseRemoteConfigDouble_ReturnsZero()
        {
            double? result = null;
            _plugin.GetFirebaseRemoteConfigDouble("key", val => result = val);
            Assert.AreEqual(0.0, result);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetFirebaseRemoteConfigLong_ReturnsZero()
        {
            long? result = null;
            _plugin.GetFirebaseRemoteConfigLong("key", val => result = val);
            Assert.AreEqual(0L, result);
            yield return null;
        }

        // GetAdjustAttribution test

        [UnityTest]
        public IEnumerator GetAdjustAttribution_ReturnsEmpty()
        {
            string result = null;
            _plugin.GetAdjustAttribution(val => result = val);
            Assert.AreEqual(string.Empty, result);
            yield return null;
        }

        // ShowDatePicker test

        [UnityTest]
        public IEnumerator ShowDatePicker_ThrowsNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() => _plugin.ShowDatePicker(2024, 1, 1, 0));
            yield return null;
        }
    }
}
