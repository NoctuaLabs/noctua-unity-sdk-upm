using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using com.noctuagames.sdk;

namespace Tests.Runtime
{
    // =========================================================================
    // NoctuaStaticApiCoverageTest.cs
    //
    // Pushes com.noctuagames.sdk.Noctua toward 80%+ line coverage.
    //
    // Organised into five test fixtures:
    //
    //  A. NoctuaStaticFlagTests          — IsOfflineMode / IsInitialized
    //                                      (pure static fields, no Instance)
    //  B. NoctuaExperimentDelegateTests  — SetGeneralExperiment / GetGeneralExperiment
    //                                      / SetExperiment / GetActiveExperiment
    //                                      (delegate to static ExperimentManager)
    //  C. NoctuaFirebaseEditorStubTests  — Firebase / Adjust methods' #else branches
    //                                      compiled in Editor (return Task.FromResult)
    //  D. NoctuaPushEventTests           — OnRemoteNotificationReceived /
    //                                      OnNotificationTapped /
    //                                      OnFirebaseMessagingTokenRefresh /
    //                                      OnInitSuccess subscribe/unsubscribe
    //  E. NoctuaDatePickerTests          — OpenDatePicker (delegates to
    //                                      MobileDateTimePicker, no Instance needed)
    //  F. NoctuaInstanceApiTests         — All methods that access Instance.Value.
    //                                      Uses [OneTimeSetUp] to warm up the Lazy<Noctua>
    //                                      singleton once; skips gracefully if
    //                                      noctuagg.json is not accessible in this
    //                                      environment.
    // =========================================================================

    // -------------------------------------------------------------------------
    // A. Static flags — read directly from static fields, never touch Instance
    // -------------------------------------------------------------------------

    [TestFixture]
    public class NoctuaStaticFlagTests
    {
        [Test]
        public void IsOfflineMode_BeforeInit_ReturnsFalse()
        {
            // _offlineMode is volatile static, default false
            Assert.IsFalse(Noctua.IsOfflineMode());
        }

        [Test]
        public void IsInitialized_BeforeInit_ReturnsFalse()
        {
            // _initialized is set only after InitAsync completes
            Assert.IsFalse(Noctua.IsInitialized());
        }
    }

    // -------------------------------------------------------------------------
    // B. ExperimentManager facade — delegates to static ExperimentManager,
    //    no access to Instance.Value at all.
    // -------------------------------------------------------------------------

    [TestFixture]
    public class NoctuaExperimentDelegateTests
    {
        private const string TestKey = "__noctua_test_key__";

        [SetUp]
        public void SetUp()
        {
            // Ensure clean state even when this is the first test in the fixture.
            ExperimentManager.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            // ExperimentManager.Clear() wipes _experimentFlags which backs
            // all experiment keys, general experiments, and active experiment.
            ExperimentManager.Clear();
        }

        // ── SetGeneralExperiment / GetGeneralExperiment ───────────────────────

        [Test]
        public void SetGeneralExperiment_ThenGet_RoundTripsValue()
        {
            Noctua.SetGeneralExperiment(TestKey, "hello");
            Assert.AreEqual("hello", Noctua.GetGeneralExperiment(TestKey));
        }

        [Test]
        public void SetGeneralExperiment_OverwriteValue_ReturnsLatest()
        {
            Noctua.SetGeneralExperiment(TestKey, "first");
            Noctua.SetGeneralExperiment(TestKey, "second");
            Assert.AreEqual("second", Noctua.GetGeneralExperiment(TestKey));
        }

        [Test]
        public void SetGeneralExperiment_EmptyValue_CanBeReadBack()
        {
            Noctua.SetGeneralExperiment(TestKey, "non-empty");
            Noctua.SetGeneralExperiment(TestKey, "");
            Assert.AreEqual("", Noctua.GetGeneralExperiment(TestKey));
        }

        [Test]
        public void GetGeneralExperiment_UnknownKey_ReturnsEmpty()
        {
            var result = Noctua.GetGeneralExperiment("__never_set__" + Guid.NewGuid());
            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void SetGeneralExperiment_MultipleKeys_StoreIndependently()
        {
            Noctua.SetGeneralExperiment("__key_a__", "aaa");
            Noctua.SetGeneralExperiment("__key_b__", "bbb");
            Assert.AreEqual("aaa", Noctua.GetGeneralExperiment("__key_a__"));
            Assert.AreEqual("bbb", Noctua.GetGeneralExperiment("__key_b__"));
        }

        // ── SetExperiment / GetActiveExperiment ───────────────────────────────

        [Test]
        public void SetExperiment_ThenGetActive_RoundTripsName()
        {
            Noctua.SetExperiment("exp_test_q1");
            Assert.AreEqual("exp_test_q1", Noctua.GetActiveExperiment());
        }

        [Test]
        public void SetExperiment_Empty_ClearsActiveExperiment()
        {
            Noctua.SetExperiment("exp_first");
            Noctua.SetExperiment("");
            Assert.AreEqual("", Noctua.GetActiveExperiment());
        }

        [Test]
        public void GetActiveExperiment_WhenNeverSet_ReturnsEmpty()
        {
            // TearDown + fresh fixture — flag is clear
            Assert.AreEqual(string.Empty, Noctua.GetActiveExperiment());
        }

        [Test]
        public void SetExperiment_OverwriteExperiment_ReturnsLatest()
        {
            Noctua.SetExperiment("exp_old");
            Noctua.SetExperiment("exp_new");
            Assert.AreEqual("exp_new", Noctua.GetActiveExperiment());
        }
    }

    // -------------------------------------------------------------------------
    // C. Firebase / Adjust — Editor #else stubs
    //    All methods are guarded by #if UNITY_ANDROID || UNITY_IOS.
    //    In the Editor their #else branches return Task.FromResult immediately
    //    without accessing Instance.Value.
    // -------------------------------------------------------------------------

    [TestFixture]
    public class NoctuaFirebaseEditorStubTests
    {
        // Pre-warm the Lazy<Noctua> singleton once per fixture run.
        // With AppLovin+AdMob defines enabled, the constructor can take 5-10 s;
        // doing it here prevents individual tests from hitting their per-test timeout.
        [OneTimeSetUp]
        public static void OneTimeSetUp()
        {
            try { _ = Noctua.IsSandbox(); }
            catch (Exception) { /* noctuagg.json absent in this environment — tests will still run */ }
        }

        [Test, Timeout(30000)]
        public async Task GetFirebaseInstallationID_InEditor_ReturnsEmptyString()
        {
            var result = await Noctua.GetFirebaseInstallationID();
            Assert.AreEqual(string.Empty, result);
        }

        [Test, Timeout(30000)]
        public async Task GetFirebaseAnalyticsSessionID_InEditor_ReturnsEmptyString()
        {
            var result = await Noctua.GetFirebaseAnalyticsSessionID();
            Assert.AreEqual(string.Empty, result);
        }

        [Test, Timeout(30000)]
        public async Task GetFirebaseMessagingToken_InEditor_ReturnsEmptyString()
        {
            var result = await Noctua.GetFirebaseMessagingToken();
            Assert.AreEqual(string.Empty, result);
        }

        [Test, Timeout(30000)]
        public async Task GetFirebaseRemoteConfigString_InEditor_ReturnsEmptyString()
        {
            var result = await Noctua.GetFirebaseRemoteConfigString("someKey");
            Assert.AreEqual(string.Empty, result);
        }

        [Test, Timeout(30000)]
        public async Task GetFirebaseRemoteConfigString_NullKey_InEditor_ReturnsEmptyString()
        {
            var result = await Noctua.GetFirebaseRemoteConfigString(null);
            Assert.AreEqual(string.Empty, result);
        }

        [Test, Timeout(30000)]
        public async Task GetFirebaseRemoteConfigBoolean_InEditor_ReturnsFalse()
        {
            var result = await Noctua.GetFirebaseRemoteConfigBoolean("someKey");
            Assert.IsFalse(result);
        }

        [Test, Timeout(30000)]
        public async Task GetFirebaseRemoteConfigDouble_InEditor_ReturnsZero()
        {
            var result = await Noctua.GetFirebaseRemoteConfigDouble("someKey");
            Assert.AreEqual(0.0, result);
        }

        [Test, Timeout(30000)]
        public async Task GetFirebaseRemoteConfigLong_InEditor_ReturnsZero()
        {
            var result = await Noctua.GetFirebaseRemoteConfigLong("someKey");
            Assert.AreEqual(0L, result);
        }

        [Test, Timeout(30000)]
        public async Task GetAdjustAttributionAsync_InEditor_ReturnsDefaultInstance()
        {
            var result = await Noctua.GetAdjustAttributionAsync();
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<NoctuaAdjustAttribution>(result);
        }
    }

    // -------------------------------------------------------------------------
    // D. Push event add/remove — static event properties backed by the static
    //    PushHandlers field; subscribing does NOT access Instance.Value.
    // -------------------------------------------------------------------------

    [TestFixture]
    public class NoctuaPushEventTests
    {
        private Action<NoctuaNotificationPayload> _receivedHandler;
        private Action<NoctuaNotificationPayload> _tappedHandler;
        private Action<string>                    _tokenRefreshHandler;
        private Action                            _initSuccessHandler;

        [SetUp]
        public void SetUp()
        {
            _receivedHandler     = _ => { };
            _tappedHandler       = _ => { };
            _tokenRefreshHandler = _ => { };
            _initSuccessHandler  = () => { };
        }

        [TearDown]
        public void TearDown()
        {
            // Always unsubscribe so handlers don't accumulate across tests.
            Noctua.OnRemoteNotificationReceived -= _receivedHandler;
            Noctua.OnNotificationTapped         -= _tappedHandler;
            Noctua.OnFirebaseMessagingTokenRefresh -= _tokenRefreshHandler;
            Noctua.OnInitSuccess                -= _initSuccessHandler;
        }

        // ── OnRemoteNotificationReceived ──────────────────────────────────────

        [Test]
        public void OnRemoteNotificationReceived_Subscribe_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                Noctua.OnRemoteNotificationReceived += _receivedHandler);
        }

        [Test]
        public void OnRemoteNotificationReceived_SubscribeThenUnsubscribe_DoesNotThrow()
        {
            Noctua.OnRemoteNotificationReceived += _receivedHandler;
            Assert.DoesNotThrow(() =>
                Noctua.OnRemoteNotificationReceived -= _receivedHandler);
        }

        [Test]
        public void OnRemoteNotificationReceived_UnsubscribeNeverAdded_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                Noctua.OnRemoteNotificationReceived -= _receivedHandler);
        }

        // ── OnNotificationTapped ──────────────────────────────────────────────

        [Test]
        public void OnNotificationTapped_Subscribe_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                Noctua.OnNotificationTapped += _tappedHandler);
        }

        [Test]
        public void OnNotificationTapped_SubscribeThenUnsubscribe_DoesNotThrow()
        {
            Noctua.OnNotificationTapped += _tappedHandler;
            Assert.DoesNotThrow(() =>
                Noctua.OnNotificationTapped -= _tappedHandler);
        }

        // ── OnFirebaseMessagingTokenRefresh ───────────────────────────────────

        [Test]
        public void OnFirebaseMessagingTokenRefresh_Subscribe_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                Noctua.OnFirebaseMessagingTokenRefresh += _tokenRefreshHandler);
        }

        [Test]
        public void OnFirebaseMessagingTokenRefresh_SubscribeThenUnsubscribe_DoesNotThrow()
        {
            Noctua.OnFirebaseMessagingTokenRefresh += _tokenRefreshHandler;
            Assert.DoesNotThrow(() =>
                Noctua.OnFirebaseMessagingTokenRefresh -= _tokenRefreshHandler);
        }

        // ── OnInitSuccess ─────────────────────────────────────────────────────

        [Test]
        public void OnInitSuccess_Subscribe_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                Noctua.OnInitSuccess += _initSuccessHandler);
        }

        [Test]
        public void OnInitSuccess_SubscribeThenUnsubscribe_DoesNotThrow()
        {
            Noctua.OnInitSuccess += _initSuccessHandler;
            Assert.DoesNotThrow(() =>
                Noctua.OnInitSuccess -= _initSuccessHandler);
        }

        [Test]
        public void OnInitSuccess_MultipleSubscribers_DoesNotThrow()
        {
            Action h1 = () => { };
            Action h2 = () => { };
            Noctua.OnInitSuccess += h1;
            Noctua.OnInitSuccess += h2;
            Assert.DoesNotThrow(() =>
            {
                Noctua.OnInitSuccess -= h1;
                Noctua.OnInitSuccess -= h2;
            });
        }
    }

    // -------------------------------------------------------------------------
    // E. OpenDatePicker — delegates directly to MobileDateTimePicker.CreateDate,
    //    which calls the static _showDatePickerAction if non-null (null-safe).
    //    No Instance.Value access.
    // -------------------------------------------------------------------------

    [TestFixture]
    public class NoctuaDatePickerTests
    {
        [Test]
        public void OpenDatePicker_WithNullCallbacks_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                Noctua.OpenDatePicker(2024, 1, 1));
        }

        [Test]
        public void OpenDatePicker_WithOnChangeAndOnClose_DoesNotThrow()
        {
            bool onChangeCalled = false;
            bool onCloseCalled  = false;
            Assert.DoesNotThrow(() =>
                Noctua.OpenDatePicker(
                    year:     2024,
                    month:    6,
                    day:      15,
                    pickerId: 1,
                    onChange: dt => onChangeCalled = true,
                    onClose:  dt => onCloseCalled  = true));
        }

        [Test]
        public void OpenDatePicker_CustomPickerId_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                Noctua.OpenDatePicker(2025, 12, 31, pickerId: 99));
        }
    }

    // -------------------------------------------------------------------------
    // F. Instance-dependent tests
    //
    //    Warming up Noctua.Instance.Value runs the private Noctua() constructor
    //    which reads Assets/StreamingAssets/noctuagg.json (present in this repo)
    //    via File.ReadAllText on macOS / Linux Editor.
    //
    //    [OneTimeSetUp] attempts init once and sets _initAvailable.
    //    Every test calls RequireInit() which issues Assert.Ignore when init
    //    is unavailable — keeping CI green in environments without the config.
    //
    //    Expected config values (from Assets/StreamingAssets/noctuagg.json):
    //      sandboxEnabled:       true
    //      offlineFirstEnabled:  true
    //      adjustOfflineModeDisabled not present → AdjustOfflineModeDisabled() = false
    // -------------------------------------------------------------------------

    [TestFixture]
    public class NoctuaInstanceApiTests
    {
        private static bool _initAvailable;

        [OneTimeSetUp]
        public static void OneTimeSetUp()
        {
            try
            {
                // Cheapest Instance.Value access — reads _config which is set
                // by the constructor before the sandbox/service wiring.
                _ = Noctua.IsSandbox();
                _initAvailable = true;
            }
            catch (Exception)
            {
                _initAvailable = false;
            }
        }

        private void RequireInit([System.Runtime.CompilerServices.CallerMemberName] string caller = "")
        {
            if (!_initAvailable)
                Assert.Ignore(
                    $"{caller}: Noctua singleton init not available " +
                    "(no valid noctuagg.json at Assets/StreamingAssets/).");
        }

        // ── Config / mode flags ───────────────────────────────────────────────

        [Test]
        public void IsSandbox_WithSandboxEnabledTrue_ReturnsTrue()
        {
            RequireInit();
            Assert.IsTrue(Noctua.IsSandbox(),
                "noctuagg.json has sandboxEnabled: true");
        }

        [Test]
        public void IsSandbox_CalledTwice_ReturnsSameValue()
        {
            RequireInit();
            Assert.AreEqual(Noctua.IsSandbox(), Noctua.IsSandbox());
        }

        [Test]
        public void IsOfflineFirst_WithOfflineFirstEnabledTrue_ReturnsTrue()
        {
            RequireInit();
            Assert.IsTrue(Noctua.IsOfflineFirst(),
                "noctuagg.json has offlineFirstEnabled: true");
        }

        [Test]
        public void AdjustOfflineModeDisabled_FlagAbsentInConfig_ReturnsFalse()
        {
            RequireInit();
            // adjustOfflineModeDisabled is not in remoteFeatureFlags
            Assert.IsFalse(Noctua.AdjustOfflineModeDisabled());
        }

        // ── BuildSanity ───────────────────────────────────────────────────────

        [Test]
        public void BuildSanity_InSandboxMode_ReturnsNonNullRecord()
        {
            RequireInit();
            var info = Noctua.BuildSanity();
            Assert.IsNotNull(info,
                "BuildSanity must return a record even if fields are empty");
        }

        [Test]
        public void BuildSanity_CalledRepeatedly_DoesNotThrow()
        {
            RequireInit();
            for (int i = 0; i < 3; i++)
                Assert.DoesNotThrow(() => Noctua.BuildSanity());
        }

        // ── Inspector helpers ─────────────────────────────────────────────────

        [Test]
        public void ShowInspector_WhenInstanceReady_DoesNotThrow()
        {
            RequireInit();
            Assert.DoesNotThrow(() => Noctua.ShowInspector());
        }

        [Test]
        public void HideInspector_WhenInstanceReady_DoesNotThrow()
        {
            RequireInit();
            Assert.DoesNotThrow(() => Noctua.HideInspector());
        }

        [Test]
        public void ToggleInspector_WhenInstanceReady_DoesNotThrow()
        {
            RequireInit();
            Assert.DoesNotThrow(() => Noctua.ToggleInspector());
        }

        [Test]
        public void ShowHideToggleInspector_Sequence_DoesNotThrow()
        {
            RequireInit();
            Assert.DoesNotThrow(() =>
            {
                Noctua.ShowInspector();
                Noctua.HideInspector();
                Noctua.ToggleInspector();
                Noctua.ToggleInspector();
            });
        }

        // ── Pseudo user ID ────────────────────────────────────────────────────

        [Test]
        public void GetPseudoUserId_WhenInstanceReady_ReturnsNonEmptyString()
        {
            RequireInit();
            var id = Noctua.GetPseudoUserId();
            Assert.IsNotNull(id);
            Assert.IsNotEmpty(id,
                "PseudoUserId must be a non-empty deterministic device ID");
        }

        [Test]
        public void GetPseudoUserId_CalledTwice_ReturnsSameValue()
        {
            RequireInit();
            Assert.AreEqual(Noctua.GetPseudoUserId(), Noctua.GetPseudoUserId(),
                "PseudoUserId must be deterministic within the same session");
        }

        // ── Online / Offline notification ─────────────────────────────────────

        [Test]
        public void OnOnline_WhenInstanceReady_DoesNotThrow()
        {
            RequireInit();
            Assert.DoesNotThrow(() => Noctua.OnOnline());
        }

        [Test]
        public void OnOffline_WhenInstanceReady_DoesNotThrow()
        {
            RequireInit();
            Assert.DoesNotThrow(() => Noctua.OnOffline());
        }

        [Test]
        public void OnOffline_ThenOnOnline_ToggleDoesNotThrow()
        {
            RequireInit();
            Assert.DoesNotThrow(() =>
            {
                Noctua.OnOffline();
                Noctua.OnOnline();
            });
        }

        // ── Native event storage (blob API) ───────────────────────────────────

        [Test]
        public void SaveEvents_ValidJsonArray_DoesNotThrow()
        {
            RequireInit();
            Assert.DoesNotThrow(() => Noctua.SaveEvents("[]"));
        }

        [Test]
        public void SaveEvents_NonEmptyJson_DoesNotThrow()
        {
            RequireInit();
            Assert.DoesNotThrow(() => Noctua.SaveEvents("[\"evt1\",\"evt2\"]"));
        }

        [Test]
        public void SaveEvents_NullString_DoesNotThrow()
        {
            RequireInit();
            // DefaultNativePlugin stores to PlayerPrefs — a null JSON is valid
            Assert.DoesNotThrow(() => Noctua.SaveEvents(null));
        }

        [Test]
        public void DeleteEvents_WhenInstanceReady_DoesNotThrow()
        {
            RequireInit();
            Assert.DoesNotThrow(() => Noctua.DeleteEvents());
        }

        [Test, Timeout(5000)]
        public async Task GetEventsAsync_WhenInstanceReady_ReturnsNonNullList()
        {
            RequireInit();
            var result = await Noctua.GetEventsAsync();
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<List<string>>(result);
        }

        [Test, Timeout(5000)]
        public async Task GetEventsAsync_AfterSaveAndDelete_ReturnsEmptyList()
        {
            RequireInit();
            Noctua.SaveEvents("[\"evt\"]");
            Noctua.DeleteEvents();
            var result = await Noctua.GetEventsAsync();
            Assert.IsNotNull(result);
            Assert.IsEmpty(result);
        }

        // ── Native event storage (per-row API) ────────────────────────────────

        [Test]
        public void InsertEvent_ValidJson_DoesNotThrow()
        {
            RequireInit();
            Assert.DoesNotThrow(() =>
                Noctua.InsertEvent("{\"event_name\":\"test_event\"}"));
        }

        [Test]
        public void InsertEvent_EmptyJson_DoesNotThrow()
        {
            RequireInit();
            Assert.DoesNotThrow(() => Noctua.InsertEvent("{}"));
        }

        [Test, Timeout(5000)]
        public async Task GetEventsBatchAsync_WhenInstanceReady_ReturnsNonNullList()
        {
            RequireInit();
            var result = await Noctua.GetEventsBatchAsync(10, 0);
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<List<NativeEvent>>(result);
        }

        [Test, Timeout(5000)]
        public async Task GetEventsBatchAsync_ZeroLimit_ReturnsEmptyList()
        {
            RequireInit();
            var result = await Noctua.GetEventsBatchAsync(0, 0);
            Assert.IsNotNull(result);
            Assert.IsEmpty(result);
        }

        [Test, Timeout(5000)]
        public async Task DeleteEventsByIdsAsync_EmptyArray_ReturnsZero()
        {
            RequireInit();
            var deleted = await Noctua.DeleteEventsByIdsAsync(new long[0]);
            Assert.AreEqual(0, deleted);
        }

        [Test, Timeout(5000)]
        public async Task GetEventCountAsync_WhenInstanceReady_ReturnsNonNegative()
        {
            RequireInit();
            var count = await Noctua.GetEventCountAsync();
            Assert.GreaterOrEqual(count, 0);
        }

        [Test, Timeout(5000)]
        public async Task InsertThenCount_SingleEvent_IncrementsCountByOne()
        {
            RequireInit();
            // DefaultNativePlugin callbacks are synchronous so no race between
            // the count reads and the insert.
            var before = await Noctua.GetEventCountAsync();
            Noctua.InsertEvent("{\"event_name\":\"count_test\"}");
            var after = await Noctua.GetEventCountAsync();
            Assert.AreEqual(before + 1, after,
                "Inserting one event must increment the per-row store count by exactly 1");
        }

        // ── Native date picker ────────────────────────────────────────────────

        [Test]
        public void ShowDatePicker_WhenInstanceReady_DoesNotThrow()
        {
            RequireInit();
            // DefaultNativePlugin.ShowDatePicker throws NotImplementedException;
            // Noctua.ShowDatePicker catches it silently and logs Debug — facade
            // must not propagate the exception to callers.
            Assert.DoesNotThrow(() => Noctua.ShowDatePicker(2024, 1, 1, 1));
        }

        [Test]
        public void CloseDatePicker_WhenInstanceReady_DoesNotThrow()
        {
            RequireInit();
            Assert.DoesNotThrow(() => Noctua.CloseDatePicker());
        }

        [Test]
        public void ShowThenCloseDatePicker_DoesNotThrow()
        {
            RequireInit();
            Assert.DoesNotThrow(() =>
            {
                Noctua.ShowDatePicker(2025, 6, 15, 2);
                Noctua.CloseDatePicker();
            });
        }
    }
}
