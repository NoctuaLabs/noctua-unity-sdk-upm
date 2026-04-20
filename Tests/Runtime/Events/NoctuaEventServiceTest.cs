using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using com.noctuagames.sdk;
using com.noctuagames.sdk.Events;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.Runtime.Events
{
    /// <summary>
    /// Unit tests for <see cref="NoctuaEventService"/>.
    /// Covers: SetProperties payload enrichment, TrackAdRevenue/TrackPurchase/TrackCustomEvent
    /// routing to native tracker and event sender, InternalTrackEvent (event sender only),
    /// SetCurrentFeature/GetCurrentFeature, and null-safety for both dependencies.
    /// </summary>
    [TestFixture]
    public class NoctuaEventServiceTest
    {
        private MockNativeTracker _nativeTracker;
        private MockEventSenderForEvents _eventSender;

        [SetUp]
        public void SetUp()
        {
            _nativeTracker = new MockNativeTracker();
            _eventSender   = new MockEventSenderForEvents();

            // Clear ExperimentManager to prevent cross-test feature-tag pollution
            ExperimentManager.Clear();
        }

        // ─── SetProperties / AppendProperties ────────────────────────────────

        [Test]
        public void SetProperties_Country_AppendsToNextEvent()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);
            svc.SetProperties(country: "ID");

            svc.TrackCustomEvent("test_event");

            var evt = _eventSender.GetEvents("test_event").First();
            Assert.AreEqual("ID", evt["country"]);
        }

        [Test]
        public void SetProperties_IpAddress_AppendsToNextEvent()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);
            svc.SetProperties(ipAddress: "1.2.3.4");

            svc.TrackCustomEvent("test_event");

            var evt = _eventSender.GetEvents("test_event").First();
            Assert.AreEqual("1.2.3.4", evt["ip_address"]);
        }

        [Test]
        public void SetProperties_Sandbox_AppendsToNextEvent()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);
            svc.SetProperties(isSandbox: true);

            svc.TrackCustomEvent("test_event");

            var evt = _eventSender.GetEvents("test_event").First();
            Assert.AreEqual(true, evt["is_sandbox"]);
        }

        [Test]
        public void SetProperties_EmptyCountry_DoesNotAppend()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);
            svc.SetProperties(country: ""); // empty → not set

            svc.TrackCustomEvent("test_event");

            var evt = _eventSender.GetEvents("test_event").First();
            Assert.IsFalse(evt.ContainsKey("country"), "Empty country should not be appended");
        }

        // ─── TrackAdRevenue ───────────────────────────────────────────────────

        [Test]
        public void TrackAdRevenue_CallsNativeTracker()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);

            svc.TrackAdRevenue("admob", 0.05, "USD");

            Assert.IsTrue(_nativeTracker.WasCalled("TrackAdRevenue"),
                "TrackAdRevenue should forward to native tracker");
        }

        [Test]
        public void TrackAdRevenue_SendsAdRevenueEventToEventSender()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);

            svc.TrackAdRevenue("applovin", 0.02, "USD");

            Assert.IsTrue(_eventSender.HasEvent("ad_revenue"),
                "TrackAdRevenue should send 'ad_revenue' event");
            var evt = _eventSender.GetEvents("ad_revenue").First();
            Assert.AreEqual("applovin", evt["source"]);
            Assert.AreEqual(0.02,       evt["revenue"]);
            Assert.AreEqual("USD",      evt["currency"]);
        }

        [Test]
        public void TrackAdRevenue_DoesNotMutateCallerDictionary()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);
            var original = new Dictionary<string, IConvertible> { ["ad_type"] = "rewarded" };
            var keysBefore = original.Keys.ToList();

            svc.TrackAdRevenue("admob", 0.01, "USD", original);

            CollectionAssert.AreEquivalent(keysBefore, original.Keys,
                "TrackAdRevenue must not mutate the caller's extraPayload dictionary");
        }

        [Test]
        public void TrackAdRevenue_NullNativeTracker_DoesNotThrow()
        {
            var svc = new NoctuaEventService(null, _eventSender);

            Assert.DoesNotThrow(() => svc.TrackAdRevenue("admob", 0.01, "USD"),
                "Null native tracker must not throw");
        }

        [Test]
        public void TrackAdRevenue_NullEventSender_DoesNotThrow()
        {
            var svc = new NoctuaEventService(_nativeTracker, null);

            Assert.DoesNotThrow(() => svc.TrackAdRevenue("admob", 0.01, "USD"),
                "Null event sender must not throw");
        }

        // ─── TrackPurchase ─────────────────────────────────────────────────────

        [Test]
        public void TrackPurchase_CallsNativeTracker()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);

            svc.TrackPurchase("order_123", 4.99, "USD");

            Assert.IsTrue(_nativeTracker.WasCalled("TrackPurchase"),
                "TrackPurchase should forward to native tracker");
        }

        [Test]
        public void TrackPurchase_DoesNotCallEventSender()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);

            svc.TrackPurchase("order_456", 9.99, "USD");

            Assert.IsFalse(_eventSender.HasEvent("purchase"),
                "TrackPurchase must NOT send any event to the event sender");
            Assert.AreEqual(0, _eventSender.AllEvents.Count,
                "TrackPurchase must not enqueue any events");
        }

        [Test]
        public void TrackPurchase_NullNativeTracker_DoesNotThrow()
        {
            var svc = new NoctuaEventService(null, _eventSender);

            Assert.DoesNotThrow(() => svc.TrackPurchase("order_789", 1.99, "USD"),
                "Null native tracker must not throw");
        }

        // ─── TrackCustomEvent ─────────────────────────────────────────────────

        [Test]
        public void TrackCustomEvent_CallsBothNativeTrackerAndEventSender()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);

            svc.TrackCustomEvent("level_complete", new Dictionary<string, IConvertible> { ["level"] = 5 });

            Assert.IsTrue(_nativeTracker.WasCalled("TrackCustomEvent"),
                "TrackCustomEvent should forward to native tracker");
            Assert.IsTrue(_eventSender.HasEvent("level_complete"),
                "TrackCustomEvent should send event to event sender");
        }

        [Test]
        public void TrackCustomEvent_DoesNotMutateCallerDictionary()
        {
            var svc    = new NoctuaEventService(_nativeTracker, _eventSender);
            var dict   = new Dictionary<string, IConvertible> { ["key"] = "value" };
            var before = dict.Keys.ToList();

            svc.SetProperties(country: "SG"); // adds "country" to copies, not original
            svc.TrackCustomEvent("evt", dict);

            CollectionAssert.AreEquivalent(before, dict.Keys,
                "TrackCustomEvent must not mutate the caller's extraPayload dictionary");
        }

        // ─── TrackCustomEventWithRevenue ──────────────────────────────────────

        [Test]
        public void TrackCustomEventWithRevenue_CallsBothNativeTrackerAndEventSender()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);

            svc.TrackCustomEventWithRevenue("special_offer", 1.99, "USD");

            Assert.IsTrue(_nativeTracker.WasCalled("TrackCustomEventWithRevenue"),
                "TrackCustomEventWithRevenue should forward to native tracker");
            Assert.IsTrue(_eventSender.HasEvent("special_offer"),
                "TrackCustomEventWithRevenue should send event to event sender");
        }

        [Test]
        public void TrackCustomEventWithRevenue_InjectsRevenueAndCurrencyIntoSentPayload()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);

            svc.TrackCustomEventWithRevenue("ad_impression", 0.07, "EUR");

            var evt = _eventSender.GetEvents("ad_impression").First();
            Assert.AreEqual(0.07,  evt["revenue"]);
            Assert.AreEqual("EUR", evt["currency"]);
        }

        // ─── InternalTrackEvent ───────────────────────────────────────────────

        [Test]
        public void InternalTrackEvent_SendsViaEventSender()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);

            svc.InternalTrackEvent("sdk_init_success");

            Assert.IsTrue(_eventSender.HasEvent("sdk_init_success"),
                "InternalTrackEvent should send event via event sender");
        }

        [Test]
        public void InternalTrackEvent_DoesNotCallNativeTracker()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);

            svc.InternalTrackEvent("sdk_init_success");

            Assert.AreEqual(0, _nativeTracker.CalledMethods.Count,
                "InternalTrackEvent must NOT forward to native tracker");
        }

        [Test]
        public void InternalTrackEvent_NullEventSender_DoesNotThrow()
        {
            var svc = new NoctuaEventService(_nativeTracker, null);

            Assert.DoesNotThrow(() => svc.InternalTrackEvent("sdk_error"),
                "Null event sender must not throw");
        }

        // ─── SetCurrentFeature / GetCurrentFeature ────────────────────────────

        [Test]
        public void GetCurrentFeature_ReturnsActiveFeatureName()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);

            svc.SetCurrentFeature("map_screen");

            Assert.AreEqual("map_screen", svc.GetCurrentFeature());
        }

        [Test]
        public void SetCurrentFeature_SendsFeatureEngagementForPreviousFeature()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);

            // Enter first feature — no event sent yet (nothing previous)
            svc.SetCurrentFeature("lobby");
            Assert.IsFalse(_eventSender.HasEvent("feature_engagement"),
                "No feature_engagement on first SetCurrentFeature");

            // Switch to a new feature — triggers feature_engagement for "lobby"
            svc.SetCurrentFeature("battle");

            Assert.IsTrue(_eventSender.HasEvent("feature_engagement"),
                "SetCurrentFeature should fire feature_engagement when leaving a feature");
            var evt = _eventSender.GetEvents("feature_engagement").First();
            Assert.AreEqual("lobby", evt["feature_tag"],
                "feature_engagement payload should carry the previous feature tag");
            Assert.IsTrue(evt.ContainsKey("feature_time_msec"),
                "feature_engagement payload should include feature_time_msec");
            Assert.IsTrue(evt.ContainsKey("feature_visit_id"),
                "feature_engagement payload should include feature_visit_id");
        }

        [Test]
        public void SetProperties_MultipleCalls_BothPropertiesAccumulate()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);
            svc.SetProperties(country: "ID");
            svc.SetProperties(ipAddress: "1.2.3.4");

            svc.TrackCustomEvent("test_event");

            var evt = _eventSender.GetEvents("test_event").First();
            Assert.AreEqual("ID",      evt["country"],    "country set in first call should persist");
            Assert.AreEqual("1.2.3.4", evt["ip_address"], "ip_address set in second call should also appear");
        }

        [UnityTest]
        public IEnumerator SetCurrentFeature_AfterDelay_FeatureTimeMsecIsPositive() => UniTask.ToCoroutine(async () =>
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);
            svc.SetCurrentFeature("lobby");

            await UniTask.Delay(150);

            svc.SetCurrentFeature("battle");

            var evt = _eventSender.GetEvents("feature_engagement").First();
            var timeMsec = Convert.ToInt64(evt["feature_time_msec"]);
            Assert.GreaterOrEqual(timeMsec, 100L,
                "feature_time_msec should be at least 100ms when leaving a feature after a delay");
        });

        [Test]
        public void SetCurrentFeature_ClearWithEmpty_StopsTracking()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);
            svc.SetCurrentFeature("store");

            svc.SetCurrentFeature(""); // clear

            Assert.AreEqual("", svc.GetCurrentFeature(),
                "Clearing feature with empty string should make GetCurrentFeature return empty");

            // Calling SetCurrentFeature again with empty should not fire event
            _eventSender.Clear();
            svc.SetCurrentFeature("home");
            Assert.IsFalse(_eventSender.HasEvent("feature_engagement"),
                "No feature_engagement should fire for empty → next feature transition");
        }

        [Test]
        public void SetCurrentFeature_DoubleClear_DoesNotThrow()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);
            svc.SetCurrentFeature("store");
            svc.SetCurrentFeature(""); // first clear — fires feature_engagement for "store"

            _eventSender.Clear();
            Assert.DoesNotThrow(() => svc.SetCurrentFeature(""), "Second clear on empty feature must not throw");
            Assert.IsFalse(_eventSender.HasEvent("feature_engagement"),
                "No feature_engagement should fire for empty → empty transition");
        }
    }

    // ─── MockNativeTracker ────────────────────────────────────────────────────

    /// <summary>
    /// Controllable fake INativeTracker that records all calls for assertion.
    /// </summary>
    public class MockNativeTracker : INativeTracker
    {
        public List<string> CalledMethods { get; } = new List<string>();

        public void TrackAdRevenue(string source, double revenue, string currency,
            Dictionary<string, IConvertible> extraPayload = null)
            => CalledMethods.Add("TrackAdRevenue");

        public void TrackPurchase(string orderId, double amount, string currency,
            Dictionary<string, IConvertible> extraPayload = null)
            => CalledMethods.Add("TrackPurchase");

        public void TrackCustomEvent(string name, Dictionary<string, IConvertible> extraPayload = null)
            => CalledMethods.Add("TrackCustomEvent");

        public void TrackCustomEventWithRevenue(string name, double revenue, string currency,
            Dictionary<string, IConvertible> extraPayload = null)
            => CalledMethods.Add("TrackCustomEventWithRevenue");

        public void OnOnline()  => CalledMethods.Add("OnOnline");
        public void OnOffline() => CalledMethods.Add("OnOffline");

        public bool WasCalled(string method) => CalledMethods.Contains(method);
    }

    // ─── MockEventSenderForEvents ─────────────────────────────────────────────

    /// <summary>
    /// Inline mock IEventSender for NoctuaEventServiceTest — records sent events.
    /// Named distinctly from Tests.Runtime.MockEventSender to avoid namespace collision.
    /// </summary>
    public class MockEventSenderForEvents : IEventSender
    {
        public List<(string Name, Dictionary<string, IConvertible> Data)> AllEvents { get; }
            = new List<(string, Dictionary<string, IConvertible>)>();

        public string PseudoUserId => "mock-pseudo-id";

        public void Send(string name, Dictionary<string, IConvertible> data = null)
        {
            // Snapshot the data dictionary so mutation after Send() doesn't affect records
            var snapshot = data != null
                ? new Dictionary<string, IConvertible>(data)
                : new Dictionary<string, IConvertible>();
            AllEvents.Add((name, snapshot));
        }

        public void SetProperties(
            long? userId = 0, long? playerId = 0, long? credentialId = 0,
            string credentialProvider = "", long? gameId = 0, long? gamePlatformId = 0,
            string sessionId = "", string ipAddress = "", bool? isSandbox = null)
        {
            // No-op for unit tests
        }

        public void Flush() { }

        public bool HasEvent(string name) => AllEvents.Any(e => e.Name == name);

        public List<Dictionary<string, IConvertible>> GetEvents(string name) =>
            AllEvents.Where(e => e.Name == name).Select(e => e.Data).ToList();

        public void Clear() => AllEvents.Clear();
    }
}
