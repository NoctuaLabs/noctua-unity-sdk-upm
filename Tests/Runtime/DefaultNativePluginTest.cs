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

        // ─── Per-row event storage ────────────────────────────────────────────

        [UnityTest]
        public IEnumerator InsertEvent_GetEventCount_RoundTrip()
        {
            _plugin.InsertEvent("{\"event_name\":\"test\"}");

            int count = -1;
            _plugin.GetEventCount(c => count = c);
            Assert.AreEqual(1, count);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InsertMultipleEvents_GetEventsBatch_ReturnsBatch()
        {
            _plugin.InsertEvent("{\"event_name\":\"a\"}");
            _plugin.InsertEvent("{\"event_name\":\"b\"}");
            _plugin.InsertEvent("{\"event_name\":\"c\"}");

            System.Collections.Generic.List<NativeEvent> batch = null;
            _plugin.GetEventsBatch(2, 0, b => batch = b);

            Assert.AreEqual(2, batch.Count);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetEventsBatch_WithOffset_SkipsCorrectly()
        {
            _plugin.InsertEvent("{\"event_name\":\"a\"}");
            _plugin.InsertEvent("{\"event_name\":\"b\"}");

            System.Collections.Generic.List<NativeEvent> batch = null;
            _plugin.GetEventsBatch(10, 1, b => batch = b);

            Assert.AreEqual(1, batch.Count);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DeleteEventsByIds_RemovesCorrectEvents()
        {
            _plugin.InsertEvent("{\"event_name\":\"a\"}");
            _plugin.InsertEvent("{\"event_name\":\"b\"}");

            System.Collections.Generic.List<NativeEvent> all = null;
            _plugin.GetEventsBatch(100, 0, b => all = b);
            var firstId = all[0].Id;

            int removed = -1;
            _plugin.DeleteEventsByIds(new long[] { firstId }, c => removed = c);

            Assert.AreEqual(1, removed);

            int count = -1;
            _plugin.GetEventCount(c => count = c);
            Assert.AreEqual(1, count);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetEventCount_EmptyStore_ReturnsZero()
        {
            int count = -1;
            _plugin.GetEventCount(c => count = c);
            Assert.AreEqual(0, count);
            yield return null;
        }

        // ─── Legacy blob event storage ────────────────────────────────────────

        [UnityTest]
        public IEnumerator SaveEvents_GetEvents_RoundTrip()
        {
            _plugin.SaveEvents("[\"evt1\",\"evt2\"]");

            System.Collections.Generic.List<string> events = null;
            _plugin.GetEvents(e => events = e);

            Assert.AreEqual(2, events.Count);
            Assert.AreEqual("evt1", events[0]);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DeleteEvents_ClearsAll()
        {
            _plugin.SaveEvents("[\"evt1\"]");
            _plugin.DeleteEvents();

            System.Collections.Generic.List<string> events = null;
            _plugin.GetEvents(e => events = e);

            Assert.AreEqual(0, events.Count);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetEvents_CorruptJson_ReturnsEmptyList()
        {
            PlayerPrefs.SetString("NoctuaEvents", "notjson{{");

            System.Collections.Generic.List<string> events = null;
            _plugin.GetEvents(e => events = e);

            Assert.IsNotNull(events);
            Assert.AreEqual(0, events.Count);
            yield return null;
        }

        // ─── No-op and stub methods ───────────────────────────────────────────

        [UnityTest]
        public IEnumerator Init_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.Init(new System.Collections.Generic.List<string> { "com.test" }));
            yield return null;
        }

        [UnityTest]
        public IEnumerator OnApplicationPause_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.OnApplicationPause(true));
            Assert.DoesNotThrow(() => _plugin.OnApplicationPause(false));
            yield return null;
        }

        [UnityTest]
        public IEnumerator OnOnline_OnOffline_DoNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.OnOnline());
            Assert.DoesNotThrow(() => _plugin.OnOffline());
            yield return null;
        }

        [UnityTest]
        public IEnumerator IsStoreKitReady_ReturnsFalse()
        {
            Assert.IsFalse(_plugin.IsStoreKitReady());
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisposeStoreKit_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.DisposeStoreKit());
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
        public IEnumerator TrackAdRevenue_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.TrackAdRevenue("admob", 0.01, "USD", null));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrackPurchase_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.TrackPurchase("order1", 1.99, "USD", null));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrackCustomEvent_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.TrackCustomEvent("my_event", null));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrackCustomEventWithRevenue_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.TrackCustomEventWithRevenue("rev_event", 0.5, "USD", null));
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetFirebaseMessagingToken_ReturnsEmpty()
        {
            string result = null;
            _plugin.GetFirebaseMessagingToken(t => result = t);
            Assert.AreEqual(string.Empty, result);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetFirebaseRemoteConfigBoolean_ReturnsFalse()
        {
            bool? result = null;
            _plugin.GetFirebaseRemoteConfigBoolean("key", b => result = b);
            Assert.AreEqual(false, result);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CompletePurchaseProcessing_InvokesTrue()
        {
            bool? result = null;
            _plugin.CompletePurchaseProcessing("token", NoctuaConsumableType.Consumable, true, r => result = r);
            Assert.IsTrue(result);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetProductPurchaseStatusDetail_ReturnsStatus()
        {
            ProductPurchaseStatus status = null;
            _plugin.GetProductPurchaseStatusDetail("prod1", s => status = s);
            Assert.IsNotNull(status);
            yield return null;
        }

        // ─── In-app update / review stubs ─────────────────────────────────────

        [UnityTest]
        public IEnumerator RequestInAppReview_ReturnsFalse()
        {
            bool? result = null;
            _plugin.RequestInAppReview(r => result = r);
            Assert.AreEqual(false, result);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CheckForUpdate_ReturnsEmptyJson()
        {
            string result = null;
            _plugin.CheckForUpdate(r => result = r);
            Assert.AreEqual("{}", result);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartImmediateUpdate_ReturnsNotAvailable()
        {
            int? result = null;
            _plugin.StartImmediateUpdate(r => result = r);
            Assert.AreEqual(3, result);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartFlexibleUpdate_ReturnsNotAvailable()
        {
            int? result = null;
            _plugin.StartFlexibleUpdate(_ => { }, r => result = r);
            Assert.AreEqual(3, result);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CompleteUpdate_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _plugin.CompleteUpdate());
            yield return null;
        }

        // ─── IAP stubs that throw ──────────────────────────────────────────────

        [UnityTest]
        public IEnumerator PurchaseItem_ThrowsNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() => _plugin.PurchaseItem("prod1", (_, _) => { }));
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetActiveCurrency_ThrowsNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() => _plugin.GetActiveCurrency("prod1", (_, _) => { }));
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetProductPurchasedById_ThrowsNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() => _plugin.GetProductPurchasedById("prod1", _ => { }));
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetReceiptProductPurchasedStoreKit1_ThrowsNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() =>
                _plugin.GetReceiptProductPurchasedStoreKit1("prod1", _ => { }));
            yield return null;
        }
    }
}
