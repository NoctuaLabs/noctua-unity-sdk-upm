using System;
using System.Collections.Generic;
using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime
{
    /// <summary>
    /// EditMode NUnit tests for <see cref="DefaultNativePlugin"/> — the Editor / unsupported-platform
    /// stub that backs all SDK operations when running in the Unity Editor.
    ///
    /// The 56 existing tests in <c>DefaultNativePluginTest</c> all use <c>[UnityTest]</c> /
    /// <c>yield return null</c> despite every method under test being entirely synchronous.
    /// They run in PlayMode only and contribute zero to the EditMode coverage report.
    /// These plain <c>[Test]</c> counterparts exercise the same branches so they are
    /// counted during the EditMode pass.
    ///
    /// Covered:
    ///   — GetAccounts / PutAccount / GetAccount / DeleteAccount (PlayerPrefs-backed)
    ///   — Lifecycle no-ops (Init, OnApplicationPause, Dispose, IsStoreKitReady, …)
    ///   — Tracker no-ops (TrackAdRevenue, TrackPurchase, TrackCustomEvent, …)
    ///   — Firebase stubs (GetFirebaseInstallationID, GetFirebaseMessagingToken, …)
    ///   — StoreKit methods that throw <see cref="NotImplementedException"/>
    ///   — Callback-returning stubs (GetProductPurchaseStatusDetail, CompletePurchaseProcessing, …)
    ///   — App-management stubs (RequestInAppReview, CheckForUpdate, StartImmediateUpdate, …)
    ///   — INativeBuildInfo properties (GetNativeSdkVersion, GetFirebaseProjectId, …)
    ///   — Per-row event storage (InsertEvent, GetEventCount, GetEventsBatch, DeleteEventsByIds)
    ///   — Legacy event storage (SaveEvents / GetEvents / DeleteEvents)
    ///   — Firebase remote config stubs
    ///   — GetAdjustAttribution stub
    ///   — INativeLogStream / INativeDeviceMetrics stubs
    /// </summary>
    [TestFixture]
    public class DefaultNativePluginEditModeTest
    {
        private DefaultNativePlugin _plugin;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey("NoctuaAccountContainer");
            PlayerPrefs.DeleteKey("NoctuaEvents");
            var eventsPath = System.IO.Path.Combine(Application.persistentDataPath, "noctua_events.jsonl");
            if (System.IO.File.Exists(eventsPath)) System.IO.File.Delete(eventsPath);
            _plugin = new DefaultNativePlugin();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey("NoctuaAccountContainer");
            PlayerPrefs.DeleteKey("NoctuaEvents");
            var eventsPath = System.IO.Path.Combine(Application.persistentDataPath, "noctua_events.jsonl");
            if (System.IO.File.Exists(eventsPath)) System.IO.File.Delete(eventsPath);
        }

        // ─── GetAccounts ──────────────────────────────────────────────────────

        [Test]
        public void GetAccounts_WhenEmpty_ReturnsNonNullEmptyList()
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

        // ─── PutAccount / GetAccount ──────────────────────────────────────────

        [Test]
        public void PutAccount_GetAccount_RoundTrip()
        {
            var account = new NativeAccount
            {
                PlayerId  = 1,
                GameId    = 100,
                RawData   = "{\"test\":true}"
            };

            _plugin.PutAccount(account);

            var retrieved = _plugin.GetAccount(1, 100);
            Assert.IsNotNull(retrieved);
            Assert.AreEqual(1,               retrieved.PlayerId);
            Assert.AreEqual(100,             retrieved.GameId);
            Assert.AreEqual("{\"test\":true}", retrieved.RawData);
            Assert.Greater(retrieved.LastUpdated, 0L);
        }

        [Test]
        public void PutAccount_Duplicate_ReplacesExisting()
        {
            _plugin.PutAccount(new NativeAccount { PlayerId = 1, GameId = 100, RawData = "{\"version\":1}" });
            _plugin.PutAccount(new NativeAccount { PlayerId = 1, GameId = 100, RawData = "{\"version\":2}" });

            var accounts = _plugin.GetAccounts();
            Assert.AreEqual(1, accounts.Count);
            Assert.AreEqual("{\"version\":2}", accounts[0].RawData);
        }

        [Test]
        public void PutAccount_MultipleDistinctAccounts_AllStored()
        {
            _plugin.PutAccount(new NativeAccount { PlayerId = 1, GameId = 100, RawData = "{}" });
            _plugin.PutAccount(new NativeAccount { PlayerId = 2, GameId = 100, RawData = "{}" });
            _plugin.PutAccount(new NativeAccount { PlayerId = 3, GameId = 200, RawData = "{}" });

            Assert.AreEqual(3, _plugin.GetAccounts().Count);
        }

        // ─── DeleteAccount ────────────────────────────────────────────────────

        [Test]
        public void DeleteAccount_ExistingAccount_RemovesIt()
        {
            var account = new NativeAccount { PlayerId = 1, GameId = 100, RawData = "{}" };
            _plugin.PutAccount(account);

            var result = _plugin.DeleteAccount(account);
            Assert.AreEqual(1, result);
            Assert.AreEqual(0, _plugin.GetAccounts().Count);
        }

        [Test]
        public void DeleteAccount_NonExistentAccount_Returns1()
        {
            var account = new NativeAccount { PlayerId = 999, GameId = 999, RawData = "{}" };
            Assert.AreEqual(1, _plugin.DeleteAccount(account));
        }

        // ─── Lifecycle no-ops ─────────────────────────────────────────────────

        [Test]
        public void Init_WithEmptyList_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.Init(new List<string>()));
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

        // ─── Tracker no-ops ───────────────────────────────────────────────────

        [Test]
        public void TrackAdRevenue_WithPayload_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.TrackAdRevenue(
                "applovin", 0.01, "USD",
                new Dictionary<string, IConvertible> { { "k", "v" } }));
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

        // ─── Firebase stubs ───────────────────────────────────────────────────

        [Test]
        public void GetFirebaseInstallationID_InvokesCallbackWithEmpty()
        {
            bool called = false;
            string received = "not_called";
            _plugin.GetFirebaseInstallationID(v => { called = true; received = v; });
            Assert.IsTrue(called, "Editor stub must invoke the callback synchronously");
            Assert.AreEqual(string.Empty, received, "Callback value must be string.Empty");
        }

        [Test]
        public void GetFirebaseAnalyticsSessionID_InvokesCallbackWithEmpty()
        {
            bool called = false;
            string received = "not_called";
            _plugin.GetFirebaseAnalyticsSessionID(v => { called = true; received = v; });
            Assert.IsTrue(called, "Editor stub must invoke the callback synchronously");
            Assert.AreEqual(string.Empty, received, "Callback value must be string.Empty");
        }

        [Test]
        public void GetFirebaseMessagingToken_CallsCallbackWithEmptyString()
        {
            string result = "not_called";
            _plugin.GetFirebaseMessagingToken(val => result = val);
            Assert.AreEqual(string.Empty, result);
        }

        // ─── INativeLogStream / INativeDeviceMetrics stubs ────────────────────

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

        // ─── StoreKit methods that throw NotImplementedException ───────────────

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

        [Test]
        public void ShowDatePicker_ThrowsNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() => _plugin.ShowDatePicker(2024, 1, 1, 0));
        }

        // ─── Callback-returning stubs ─────────────────────────────────────────

        [Test]
        public void GetProductPurchaseStatusDetail_CallsCallbackWithNonNullStatus()
        {
            ProductPurchaseStatus result = null;
            _plugin.GetProductPurchaseStatusDetail("product_1", s => result = s);
            Assert.IsNotNull(result);
        }

        [Test]
        public void CompletePurchaseProcessing_CallsCallbackWithTrue()
        {
            bool? result = null;
            _plugin.CompletePurchaseProcessing("token-abc", NoctuaConsumableType.Consumable, true, ok => result = ok);
            Assert.IsTrue(result);
        }

        // ─── App management stubs ─────────────────────────────────────────────

        [Test]
        public void RequestInAppReview_CallsCallbackWithFalse()
        {
            bool? result = null;
            _plugin.RequestInAppReview(ok => result = ok);
            Assert.IsFalse(result);
        }

        [Test]
        public void CheckForUpdate_CallsCallbackWithEmptyJsonObject()
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

        // ─── INativeBuildInfo properties ──────────────────────────────────────

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

        // ─── Legacy event storage (SaveEvents / GetEvents / DeleteEvents) ──────

        [Test]
        public void SaveEvents_GetEvents_RoundTrip()
        {
            _plugin.SaveEvents("[\"event1\",\"event2\"]");

            List<string> retrieved = null;
            _plugin.GetEvents(list => retrieved = list);

            Assert.IsNotNull(retrieved);
            Assert.AreEqual(2,        retrieved.Count);
            Assert.AreEqual("event1", retrieved[0]);
            Assert.AreEqual("event2", retrieved[1]);
        }

        [Test]
        public void DeleteEvents_ClearsStorage()
        {
            _plugin.SaveEvents("[\"event1\"]");
            _plugin.DeleteEvents();

            List<string> retrieved = null;
            _plugin.GetEvents(list => retrieved = list);

            Assert.IsNotNull(retrieved);
            Assert.AreEqual(0, retrieved.Count);
        }

        [Test]
        public void GetEvents_WithCorruptJson_ReturnsEmptyList()
        {
            PlayerPrefs.SetString("NoctuaEvents", "not_valid_json");

            List<string> retrieved = null;
            _plugin.GetEvents(list => retrieved = list);

            Assert.IsNotNull(retrieved);
            Assert.AreEqual(0, retrieved.Count);
        }

        // ─── Per-row event storage (JSONL-backed) ─────────────────────────────

        [Test]
        public void InsertEvent_IncreasesGetEventCount()
        {
            int? before = null;
            _plugin.GetEventCount(c => before = c);
            int initial = before ?? 0;

            _plugin.InsertEvent("{\"event_name\":\"test_event\"}");

            int? after = null;
            _plugin.GetEventCount(c => after = c);
            Assert.AreEqual(initial + 1, after);
        }

        [Test]
        public void GetEventsBatch_LimitAndOffset_ReturnsCorrectSlice()
        {
            _plugin.InsertEvent("{\"event_name\":\"e1\"}");
            _plugin.InsertEvent("{\"event_name\":\"e2\"}");
            _plugin.InsertEvent("{\"event_name\":\"e3\"}");

            List<NativeEvent> batch = null;
            _plugin.GetEventsBatch(2, 0, list => batch = list);

            Assert.IsNotNull(batch);
            Assert.LessOrEqual(batch.Count, 2);
        }

        [Test]
        public void DeleteEventsByIds_RemovesMatchingEvents()
        {
            _plugin.InsertEvent("{\"event_name\":\"to_delete\"}");

            List<NativeEvent> batch = null;
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

        // ─── Firebase remote config stubs ─────────────────────────────────────

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

        // ─── GetAdjustAttribution stub ────────────────────────────────────────

        [Test]
        public void GetAdjustAttribution_CallsCallbackWithEmptyString()
        {
            string result = null;
            _plugin.GetAdjustAttribution(val => result = val);
            Assert.AreEqual(string.Empty, result);
        }
    }
}
