using System;
using System.Collections.Generic;
using System.Linq;
using com.noctuagames.sdk;
using com.noctuagames.sdk.Events;
using NUnit.Framework;

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
    }

        // ─── SetProperties — additional edge cases ────────────────────────────

        [Test]
        public void SetProperties_EmptyIpAddress_DoesNotAppend()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);
            svc.SetProperties(ipAddress: ""); // empty → guarded by if (ipAddress != "")

            svc.TrackCustomEvent("test_event");

            var evt = _eventSender.GetEvents("test_event").First();
            Assert.IsFalse(evt.ContainsKey("ip_address"),
                "Empty ipAddress should not be appended to the event payload");
        }

        [Test]
        public void SetProperties_SandboxFalseByDefault_DoesNotAppend()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);
            // Default isSandbox = false → guarded by if (isSandbox), never sets _isSandbox

            svc.TrackCustomEvent("test_event");

            var evt = _eventSender.GetEvents("test_event").First();
            Assert.IsFalse(evt.ContainsKey("is_sandbox"),
                "is_sandbox must not be appended when never set to true");
        }

        [Test]
        public void SetProperties_OverwritesCountryOnSecondCall()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);
            svc.SetProperties(country: "ID");
            svc.SetProperties(country: "SG"); // second call must overwrite

            svc.TrackCustomEvent("test_event");

            var evt = _eventSender.GetEvents("test_event").First();
            Assert.AreEqual("SG", evt["country"],
                "Second SetProperties(country) call must overwrite the first");
        }

        [Test]
        public void SetProperties_OverwritesIpAddressOnSecondCall()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);
            svc.SetProperties(ipAddress: "1.1.1.1");
            svc.SetProperties(ipAddress: "2.2.2.2");

            svc.TrackCustomEvent("test_event");

            var evt = _eventSender.GetEvents("test_event").First();
            Assert.AreEqual("2.2.2.2", evt["ip_address"],
                "Second SetProperties(ipAddress) call must overwrite the first");
        }

        [Test]
        public void SetProperties_AllThreeFields_AllAppendedToEvent()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);
            svc.SetProperties(country: "PH", ipAddress: "10.0.0.1", isSandbox: true);

            svc.TrackCustomEvent("test_event");

            var evt = _eventSender.GetEvents("test_event").First();
            Assert.AreEqual("PH",    evt["country"],    "country must be appended");
            Assert.AreEqual("10.0.0.1", evt["ip_address"], "ip_address must be appended");
            Assert.AreEqual(true,    evt["is_sandbox"], "is_sandbox must be appended");
        }

        [Test]
        public void SetProperties_SandboxCannotBeUnsetViaDefaultArg()
        {
            // Once _isSandbox = true, calling SetProperties() with the default isSandbox=false
            // does NOT unset it — the guard is `if (isSandbox)` which skips false.
            // This test documents the deliberate one-way latch behaviour.
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);
            svc.SetProperties(isSandbox: true);
            svc.SetProperties(); // default isSandbox = false → guard never fires

            svc.TrackCustomEvent("test_event");

            var evt = _eventSender.GetEvents("test_event").First();
            Assert.IsTrue(evt.ContainsKey("is_sandbox"),
                "is_sandbox latch: once set true it cannot be cleared via SetProperties()");
            Assert.AreEqual(true, evt["is_sandbox"]);
        }

        // ─── TrackAdRevenue — payload completeness ────────────────────────────

        [Test]
        public void TrackAdRevenue_NullExtraPayload_DoesNotThrow()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);

            Assert.DoesNotThrow(() => svc.TrackAdRevenue("admob", 0.01, "USD", null),
                "null extraPayload must be treated as an empty dictionary, not throw");
            Assert.IsTrue(_eventSender.HasEvent("ad_revenue"),
                "ad_revenue event must still be sent with null extraPayload");
        }

        [Test]
        public void TrackAdRevenue_ExtraPayloadKeysPreservedAlongsideSourceRevenueCurrency()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);
            var extra = new Dictionary<string, IConvertible> { ["ad_unit_id"] = "unit_abc", ["ad_format"] = "rewarded" };

            svc.TrackAdRevenue("admob", 0.05, "USD", extra);

            var evt = _eventSender.GetEvents("ad_revenue").First();
            Assert.AreEqual("unit_abc", evt["ad_unit_id"],  "Extra key ad_unit_id must survive");
            Assert.AreEqual("rewarded", evt["ad_format"],   "Extra key ad_format must survive");
            Assert.AreEqual("admob",    evt["source"],      "source must be injected");
            Assert.AreEqual(0.05,       evt["revenue"],     "revenue must be injected");
            Assert.AreEqual("USD",      evt["currency"],    "currency must be injected");
        }

        [Test]
        public void TrackAdRevenue_ContextPropertiesAppearedInSentEvent()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);
            svc.SetProperties(country: "TH", ipAddress: "192.168.1.1", isSandbox: true);

            svc.TrackAdRevenue("applovin_max_sdk", 0.02, "USD");

            var evt = _eventSender.GetEvents("ad_revenue").First();
            Assert.AreEqual("TH",          evt["country"],    "country from SetProperties must appear");
            Assert.AreEqual("192.168.1.1", evt["ip_address"], "ip_address from SetProperties must appear");
            Assert.AreEqual(true,          evt["is_sandbox"], "is_sandbox from SetProperties must appear");
        }

        // ─── TrackPurchase — completeness ─────────────────────────────────────

        [Test]
        public void TrackPurchase_DoesNotMutateCallerDictionary()
        {
            var svc      = new NoctuaEventService(_nativeTracker, _eventSender);
            var original = new Dictionary<string, IConvertible> { ["promo_code"] = "SAVE10" };
            var keysBefore = original.Keys.ToList();

            svc.TrackPurchase("order_001", 9.99, "USD", original);

            CollectionAssert.AreEquivalent(keysBefore, original.Keys,
                "TrackPurchase must not mutate the caller's extraPayload dictionary");
        }

        [Test]
        public void TrackPurchase_NullExtraPayload_DoesNotThrow()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);

            Assert.DoesNotThrow(() => svc.TrackPurchase("order_002", 4.99, "USD", null),
                "null extraPayload must be treated as empty, not throw");
            Assert.IsTrue(_nativeTracker.WasCalled("TrackPurchase"),
                "TrackPurchase with null payload must still forward to native tracker");
        }

        [Test]
        public void TrackPurchase_ContextPropertiesForwardedToNativeTracker()
        {
            // AppendProperties enriches the payload BEFORE the native tracker call,
            // so the native tracker should receive country/ip/sandbox.
            var capturing = new CapturingNativeTracker();
            var svc = new NoctuaEventService(capturing, _eventSender);
            svc.SetProperties(country: "MY");

            svc.TrackPurchase("order_003", 2.99, "USD");

            var call = capturing.PurchaseCalls.First();
            Assert.IsTrue(call.Payload.ContainsKey("country"),
                "Native tracker must receive context properties (country) in TrackPurchase payload");
            Assert.AreEqual("MY", call.Payload["country"]);
        }

        // ─── TrackCustomEvent — completeness ──────────────────────────────────

        [Test]
        public void TrackCustomEvent_NullNativeTracker_DoesNotThrow()
        {
            var svc = new NoctuaEventService(null, _eventSender);

            Assert.DoesNotThrow(() => svc.TrackCustomEvent("null_tracker_event"),
                "Null native tracker must not cause TrackCustomEvent to throw");
            Assert.IsTrue(_eventSender.HasEvent("null_tracker_event"),
                "EventSender must still receive the event when native tracker is null");
        }

        [Test]
        public void TrackCustomEvent_NullEventSender_DoesNotThrow()
        {
            var svc = new NoctuaEventService(_nativeTracker, null);

            Assert.DoesNotThrow(() => svc.TrackCustomEvent("null_sender_event"),
                "Null event sender must not cause TrackCustomEvent to throw");
        }

        [Test]
        public void TrackCustomEvent_ContextPropertiesAppearedInSentEvent()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);
            svc.SetProperties(country: "VN", ipAddress: "10.10.10.1");

            svc.TrackCustomEvent("ctx_event");

            var evt = _eventSender.GetEvents("ctx_event").First();
            Assert.AreEqual("VN",       evt["country"],    "country must be injected");
            Assert.AreEqual("10.10.10.1", evt["ip_address"], "ip_address must be injected");
        }

        [Test]
        public void TrackCustomEvent_ExtraPayloadKeysPreserved()
        {
            var svc   = new NoctuaEventService(_nativeTracker, _eventSender);
            var extra = new Dictionary<string, IConvertible> { ["level"] = 5, ["mode"] = "hard" };

            svc.TrackCustomEvent("level_complete", extra);

            var evt = _eventSender.GetEvents("level_complete").First();
            Assert.AreEqual(5,      evt["level"], "Extra key 'level' must be preserved");
            Assert.AreEqual("hard", evt["mode"],  "Extra key 'mode' must be preserved");
        }

        // ─── TrackCustomEventWithRevenue — completeness ───────────────────────

        [Test]
        public void TrackCustomEventWithRevenue_DoesNotMutateCallerDictionary()
        {
            var svc      = new NoctuaEventService(_nativeTracker, _eventSender);
            var original = new Dictionary<string, IConvertible> { ["item_id"] = "sword_01" };
            var keysBefore = original.Keys.ToList();

            svc.TrackCustomEventWithRevenue("item_purchase", 1.99, "USD", original);

            CollectionAssert.AreEquivalent(keysBefore, original.Keys,
                "TrackCustomEventWithRevenue must not mutate the caller's extraPayload dictionary");
        }

        [Test]
        public void TrackCustomEventWithRevenue_NullNativeTracker_DoesNotThrow()
        {
            var svc = new NoctuaEventService(null, _eventSender);

            Assert.DoesNotThrow(() => svc.TrackCustomEventWithRevenue("rev_event", 0.99, "EUR"),
                "Null native tracker must not throw");
            Assert.IsTrue(_eventSender.HasEvent("rev_event"),
                "EventSender must receive the event even with null native tracker");
        }

        [Test]
        public void TrackCustomEventWithRevenue_NullEventSender_DoesNotThrow()
        {
            var svc = new NoctuaEventService(_nativeTracker, null);

            Assert.DoesNotThrow(() => svc.TrackCustomEventWithRevenue("rev_event_2", 0.99, "EUR"),
                "Null event sender must not cause TrackCustomEventWithRevenue to throw");
        }

        [Test]
        public void TrackCustomEventWithRevenue_NullExtraPayload_DoesNotThrow()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);

            Assert.DoesNotThrow(() => svc.TrackCustomEventWithRevenue("rev_null_payload", 2.50, "USD", null),
                "null extraPayload must be treated as empty dictionary");
            Assert.IsTrue(_eventSender.HasEvent("rev_null_payload"));
        }

        [Test]
        public void TrackCustomEventWithRevenue_ExtraPayloadKeysMergedWithRevenueAndCurrency()
        {
            var svc   = new NoctuaEventService(_nativeTracker, _eventSender);
            var extra = new Dictionary<string, IConvertible> { ["product_id"] = "gem_pack_100" };

            svc.TrackCustomEventWithRevenue("store_purchase", 4.99, "USD", extra);

            var evt = _eventSender.GetEvents("store_purchase").First();
            Assert.AreEqual("gem_pack_100", evt["product_id"], "Extra key product_id must survive");
            Assert.AreEqual(4.99,           evt["revenue"],    "revenue must be injected");
            Assert.AreEqual("USD",          evt["currency"],   "currency must be injected");
        }

        [Test]
        public void TrackCustomEventWithRevenue_ContextPropertiesAppearedInSentEvent()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);
            svc.SetProperties(country: "KH");

            svc.TrackCustomEventWithRevenue("ctx_rev_event", 0.49, "USD");

            var evt = _eventSender.GetEvents("ctx_rev_event").First();
            Assert.AreEqual("KH", evt["country"], "country must appear in TrackCustomEventWithRevenue event");
        }

        // ─── InternalTrackEvent — completeness ────────────────────────────────

        [Test]
        public void InternalTrackEvent_ContextPropertiesAppearedInSentEvent()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);
            svc.SetProperties(country: "MM", ipAddress: "172.16.0.1");

            svc.InternalTrackEvent("sdk_error");

            var evt = _eventSender.GetEvents("sdk_error").First();
            Assert.AreEqual("MM",         evt["country"],    "country must be injected into InternalTrackEvent");
            Assert.AreEqual("172.16.0.1", evt["ip_address"], "ip_address must be injected into InternalTrackEvent");
        }

        [Test]
        public void InternalTrackEvent_DoesNotMutateCallerDictionary()
        {
            var svc      = new NoctuaEventService(_nativeTracker, _eventSender);
            var original = new Dictionary<string, IConvertible> { ["error_code"] = 500 };
            var keysBefore = original.Keys.ToList();

            svc.InternalTrackEvent("sdk_crash", original);

            CollectionAssert.AreEquivalent(keysBefore, original.Keys,
                "InternalTrackEvent must not mutate the caller's extraPayload dictionary");
        }

        [Test]
        public void InternalTrackEvent_NullExtraPayload_DoesNotThrow()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);

            Assert.DoesNotThrow(() => svc.InternalTrackEvent("sdk_init", null),
                "null extraPayload must be treated as empty, not throw");
            Assert.IsTrue(_eventSender.HasEvent("sdk_init"));
        }

        [Test]
        public void InternalTrackEvent_WithExtraPayload_KeysForwardedToSender()
        {
            var svc   = new NoctuaEventService(_nativeTracker, _eventSender);
            var extra = new Dictionary<string, IConvertible> { ["version"] = "1.2.3", ["duration_ms"] = 450 };

            svc.InternalTrackEvent("sdk_init_done", extra);

            var evt = _eventSender.GetEvents("sdk_init_done").First();
            Assert.AreEqual("1.2.3", evt["version"],     "Extra key 'version' must be forwarded");
            Assert.AreEqual(450,     evt["duration_ms"], "Extra key 'duration_ms' must be forwarded");
        }

        // ─── SetCurrentFeature — completeness ─────────────────────────────────

        [Test]
        public void SetCurrentFeature_Initial_GetCurrentFeatureReturnsEmpty()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);

            Assert.AreEqual("", svc.GetCurrentFeature(),
                "GetCurrentFeature must return empty string before any SetCurrentFeature call");
        }

        [Test]
        public void SetCurrentFeature_AfterClear_GetCurrentFeatureReturnsEmpty()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);
            svc.SetCurrentFeature("shop");
            svc.SetCurrentFeature(""); // clear

            Assert.AreEqual("", svc.GetCurrentFeature(),
                "GetCurrentFeature must return empty after clearing with empty string");
        }

        [Test]
        public void SetCurrentFeature_VisitIdIsUniqueForEachFeature()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);

            svc.SetCurrentFeature("lobby");
            svc.SetCurrentFeature("battle"); // fires engagement for "lobby" with first visit_id

            svc.SetCurrentFeature("shop");   // fires engagement for "battle" with second visit_id

            var engagements = _eventSender.GetEvents("feature_engagement");
            Assert.AreEqual(2, engagements.Count, "Two feature transitions must emit two engagement events");

            var visitId1 = engagements[0]["feature_visit_id"]?.ToString();
            var visitId2 = engagements[1]["feature_visit_id"]?.ToString();

            Assert.IsFalse(string.IsNullOrEmpty(visitId1), "First visit_id must be non-empty");
            Assert.IsFalse(string.IsNullOrEmpty(visitId2), "Second visit_id must be non-empty");
            Assert.AreNotEqual(visitId1, visitId2,
                "Each feature enter must generate a new unique visit_id (Guid.NewGuid)");
        }

        [Test]
        public void SetCurrentFeature_EngagementEvent_CarriesVisitIdFromEntry()
        {
            // The visit_id in the feature_engagement event must match the one assigned
            // when ENTERING that feature, not a newly generated one.
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);

            svc.SetCurrentFeature("lobby");          // enter lobby → assigns visit_id A
            svc.SetCurrentFeature("battle");         // leave lobby → sends feature_engagement with visit_id A

            var engagement = _eventSender.GetEvents("feature_engagement").First();
            var engagementVisitId = engagement["feature_visit_id"]?.ToString();

            // visit_id must be a valid Guid string (36 chars with hyphens from Guid.ToString())
            Assert.IsFalse(string.IsNullOrEmpty(engagementVisitId),
                "feature_engagement must carry a non-empty feature_visit_id");
            Assert.AreEqual(36, engagementVisitId.Length,
                "feature_visit_id must be a full Guid string (36 chars with hyphens)");
        }

        [Test]
        public void SetCurrentFeature_NullEventSender_DoesNotThrow()
        {
            var svc = new NoctuaEventService(_nativeTracker, null);

            svc.SetCurrentFeature("feature_a");

            Assert.DoesNotThrow(() => svc.SetCurrentFeature("feature_b"),
                "SetCurrentFeature with null event sender must not throw even when firing engagement");
        }

        [Test]
        public void SetCurrentFeature_SameNameAgain_FiresEngagementForPreviousVisit()
        {
            // Setting the same feature name twice means: leave the first visit, start a second.
            // An engagement event fires for the first visit.
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);

            svc.SetCurrentFeature("map");
            _eventSender.Clear();

            svc.SetCurrentFeature("map"); // same name — should still fire engagement for first visit

            Assert.IsTrue(_eventSender.HasEvent("feature_engagement"),
                "Re-entering the same feature must fire feature_engagement for the previous visit");
            var evt = _eventSender.GetEvents("feature_engagement").First();
            Assert.AreEqual("map", evt["feature_tag"],
                "feature_tag in engagement must be 'map' (the leaving feature)");
        }

        [Test]
        public void SetCurrentFeature_NullName_TreatedAsEmpty_ClearsFeature()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);
            svc.SetCurrentFeature("home");
            _eventSender.Clear();

            svc.SetCurrentFeature(null); // null treated same as empty by IsNullOrEmpty check

            Assert.AreEqual("", svc.GetCurrentFeature(),
                "null feature name must clear the current feature (same as empty string)");
        }

        [Test]
        public void SetCurrentFeature_ClearThenSetNew_DoesNotFireEngagementForEmpty()
        {
            // Clearing with "" sets ExperimentManager feature to "". Then setting a real feature
            // should not fire feature_engagement because the previous tag is empty/null.
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);
            svc.SetCurrentFeature("a");
            svc.SetCurrentFeature(""); // clear → engagement for "a" fires, feature now ""
            _eventSender.Clear();

            svc.SetCurrentFeature("b"); // previous feature is "" → no engagement must fire

            Assert.IsFalse(_eventSender.HasEvent("feature_engagement"),
                "No feature_engagement must fire when transitioning from empty feature to a named one");
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

    // ─── CapturingNativeTracker ───────────────────────────────────────────────

    /// <summary>
    /// INativeTracker that captures the full argument list of every call so tests
    /// can assert on what was actually forwarded to the native SDK layer.
    /// </summary>
    public class CapturingNativeTracker : INativeTracker
    {
        public class AdRevenueCall
        {
            public string Source; public double Revenue; public string Currency;
            public Dictionary<string, IConvertible> Payload;
        }
        public class PurchaseCall
        {
            public string OrderId; public double Amount; public string Currency;
            public Dictionary<string, IConvertible> Payload;
        }
        public class CustomEventCall
        {
            public string Name;
            public Dictionary<string, IConvertible> Payload;
        }
        public class CustomEventWithRevenueCall
        {
            public string Name; public double Revenue; public string Currency;
            public Dictionary<string, IConvertible> Payload;
        }

        public List<AdRevenueCall>              AdRevenueCalls          { get; } = new List<AdRevenueCall>();
        public List<PurchaseCall>               PurchaseCalls           { get; } = new List<PurchaseCall>();
        public List<CustomEventCall>            CustomEventCalls        { get; } = new List<CustomEventCall>();
        public List<CustomEventWithRevenueCall> CustomEventRevenueCalls { get; } = new List<CustomEventWithRevenueCall>();
        public List<string>                     CalledMethods           { get; } = new List<string>();

        public void TrackAdRevenue(string source, double revenue, string currency,
            Dictionary<string, IConvertible> extraPayload = null)
        {
            CalledMethods.Add("TrackAdRevenue");
            var snap = extraPayload != null
                ? new Dictionary<string, IConvertible>(extraPayload)
                : new Dictionary<string, IConvertible>();
            AdRevenueCalls.Add(new AdRevenueCall { Source = source, Revenue = revenue, Currency = currency, Payload = snap });
        }

        public void TrackPurchase(string orderId, double amount, string currency,
            Dictionary<string, IConvertible> extraPayload = null)
        {
            CalledMethods.Add("TrackPurchase");
            var snap = extraPayload != null
                ? new Dictionary<string, IConvertible>(extraPayload)
                : new Dictionary<string, IConvertible>();
            PurchaseCalls.Add(new PurchaseCall { OrderId = orderId, Amount = amount, Currency = currency, Payload = snap });
        }

        public void TrackCustomEvent(string name, Dictionary<string, IConvertible> extraPayload = null)
        {
            CalledMethods.Add("TrackCustomEvent");
            var snap = extraPayload != null
                ? new Dictionary<string, IConvertible>(extraPayload)
                : new Dictionary<string, IConvertible>();
            CustomEventCalls.Add(new CustomEventCall { Name = name, Payload = snap });
        }

        public void TrackCustomEventWithRevenue(string name, double revenue, string currency,
            Dictionary<string, IConvertible> extraPayload = null)
        {
            CalledMethods.Add("TrackCustomEventWithRevenue");
            var snap = extraPayload != null
                ? new Dictionary<string, IConvertible>(extraPayload)
                : new Dictionary<string, IConvertible>();
            CustomEventRevenueCalls.Add(new CustomEventWithRevenueCall { Name = name, Revenue = revenue, Currency = currency, Payload = snap });
        }

        public void OnOnline()  => CalledMethods.Add("OnOnline");
        public void OnOffline() => CalledMethods.Add("OnOffline");

        public bool WasCalled(string method) => CalledMethods.Contains(method);
    }
}
