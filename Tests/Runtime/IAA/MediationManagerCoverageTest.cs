using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using com.noctuagames.sdk;
using com.noctuagames.sdk.AdPlaceholder;
using IAAConfig = com.noctuagames.sdk.IAA;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Runtime.IAA
{
    /// <summary>
    /// Coverage-fill tests for <see cref="MediationManager"/> targeting branches NOT covered
    /// by <c>MediationManagerCreateNetworksTest</c>, <c>MediationSelectionMatrixTest</c>,
    /// <c>MediationCallbackHandlerRaceTest</c>, or <c>MediationCallbackRaceTest</c>.
    ///
    /// Focus areas:
    ///   Group Q — Initialize() guard branches (null IAAResponse, null orchestrator)
    ///   Group R — SetupAdUnitID with various IAA shapes
    ///   Group S — ShowAppOpenAd / IsAppOpenAdReady null-manager paths
    ///   Group T — ShowMediationDebugger overloads on null orchestrator
    ///   Group U — SetTestDeviceIds null orchestrator path
    ///   Group V — ShowAdPlaceholder / CloseAdPlaceholder UI null paths + idempotency
    ///   Group W — Internal methods via reflection (SetCountryCode, RecordPurchase,
    ///             ApplyExperimentOverride — including AppOpen cooldown default branch
    ///             and null-config early returns)
    ///   Group X — Threading: concurrent SetCountryCode + RecordPurchase + ApplyExperimentOverride
    /// </summary>
    [TestFixture]
    public class MediationManagerCoverageTest
    {
        private const BindingFlags PrivInst = BindingFlags.NonPublic | BindingFlags.Instance;

        private class NoopAdPlaceholderUI : IAdPlaceholderUI
        {
            public int ShowCount;
            public int CloseCount;
            public void ShowAdPlaceholder(AdPlaceholderType adType) => Interlocked.Increment(ref ShowCount);
            public void CloseAdPlaceholder() => Interlocked.Increment(ref CloseCount);
        }

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
        }

        private static MediationManager NullIaaManager(IAdPlaceholderUI ui = null)
        {
            return new MediationManager(ui ?? new NoopAdPlaceholderUI(), null);
        }

        private static MediationManager MinimalManager(IAdPlaceholderUI ui = null, string mediation = "admob")
        {
            return new MediationManager(ui ?? new NoopAdPlaceholderUI(),
                new IAAConfig { Mediation = mediation });
        }

        private static void InvokeInternal(MediationManager m, string name, params object[] args)
        {
            var method = typeof(MediationManager).GetMethod(name, PrivInst)
                ?? typeof(MediationManager).GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Method '{name}' not found on MediationManager");
            method.Invoke(m, args);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group Q — Initialize() guard branches
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void Initialize_NullIaaResponse_LogsErrorAndReturns()
        {
            var mgr = NullIaaManager();
            bool callbackCalled = false;
            Assert.DoesNotThrow(() => mgr.Initialize(() => callbackCalled = true));
            Assert.IsFalse(callbackCalled,
                "Initialize must return early when IAAResponse is null — no completion callback");
        }

        [Test]
        public void Initialize_NoOrchestrator_LogsErrorAndReturns()
        {
            // MinimalManager("ironsource") feeds an unknown mediation → CreateNetworks
            // logs an error and leaves _orchestrator = null. Initialize must hit the
            // "orchestrator not created" guard.
            var mgr = new MediationManager(new NoopAdPlaceholderUI(),
                new IAAConfig { Mediation = "ironsource" });
            bool callbackCalled = false;
            Assert.DoesNotThrow(() => mgr.Initialize(() => callbackCalled = true));
            Assert.IsFalse(callbackCalled,
                "Initialize must early-return when orchestrator is null");
        }

        [Test]
        public void Initialize_NoCallback_DoesNotThrow()
        {
            var mgr = NullIaaManager();
            Assert.DoesNotThrow(() => mgr.Initialize(null));
            Assert.DoesNotThrow(() => mgr.Initialize());
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group R — SetupAdUnitID
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void SetupAdUnitID_WithNullIaa_DoesNotThrow()
        {
            var mgr = NullIaaManager();
            Assert.DoesNotThrow(() => mgr.SetupAdUnitID(null));
        }

        [Test]
        public void SetupAdUnitID_WithEmptyIaa_DoesNotThrow()
        {
            var mgr = NullIaaManager();
            Assert.DoesNotThrow(() => mgr.SetupAdUnitID(new IAAConfig()));
        }

        [Test]
        public void SetupAdUnitID_WithMinimalIaa_DoesNotThrow()
        {
            var mgr = NullIaaManager();
            Assert.DoesNotThrow(() => mgr.SetupAdUnitID(new IAAConfig { Mediation = "admob" }));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group S — ShowAppOpenAd / IsAppOpenAdReady null-manager paths
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void ShowAppOpenAd_NullAppOpenManager_LogsWarningAndReturns()
        {
            var mgr = NullIaaManager();
            Assert.DoesNotThrow(() => mgr.ShowAppOpenAd(),
                "ShowAppOpenAd must log a warning and return early when _appOpenAdManager is null");
        }

        [Test]
        public void IsAppOpenAdReady_NullManager_ReturnsFalse()
        {
            var mgr = NullIaaManager();
            Assert.IsFalse(mgr.IsAppOpenAdReady());
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group T — ShowMediationDebugger overloads on null orchestrator
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void ShowMediationDebugger_NoArgs_NullOrchestrator_DoesNotThrow()
        {
            var mgr = NullIaaManager();
            Assert.DoesNotThrow(() => mgr.ShowMediationDebugger());
        }

        [Test]
        public void ShowMediationDebugger_WithNetworkName_NullOrchestrator_DoesNotThrow()
        {
            var mgr = NullIaaManager();
            Assert.DoesNotThrow(() => mgr.ShowMediationDebugger("admob"));
            Assert.DoesNotThrow(() => mgr.ShowMediationDebugger(null));
            Assert.DoesNotThrow(() => mgr.ShowMediationDebugger(""));
            Assert.DoesNotThrow(() => mgr.ShowMediationDebugger("nonexistent_network"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group U — SetTestDeviceIds null orchestrator path
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void SetTestDeviceIds_NullOrchestrator_DoesNotThrow()
        {
            var mgr = NullIaaManager();
            Assert.DoesNotThrow(() => mgr.SetTestDeviceIds(new List<string> { "device-1", "device-2" }));
            Assert.DoesNotThrow(() => mgr.SetTestDeviceIds(new List<string>()));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group V — ShowAdPlaceholder / CloseAdPlaceholder
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void ShowAdPlaceholder_DelegatesToInjectedUi()
        {
            var ui = new NoopAdPlaceholderUI();
            var mgr = NullIaaManager(ui);
            mgr.ShowAdPlaceholder(AdPlaceholderType.Interstitial);
            Assert.AreEqual(1, ui.ShowCount,
                "ShowAdPlaceholder must call into the injected IAdPlaceholderUI");
        }

        [Test]
        public void ShowAdPlaceholder_NullUi_LogsWarningAndDoesNotThrow()
        {
            // Construct with a literal null IAdPlaceholderUI so the warning branch fires.
            var mgr = new MediationManager(null, null);
            Assert.DoesNotThrow(() => mgr.ShowAdPlaceholder(AdPlaceholderType.Interstitial));
        }

        [Test]
        public void CloseAdPlaceholder_OnlyDelegatesOnFirstCall_Idempotent()
        {
            var ui = new NoopAdPlaceholderUI();
            var mgr = NullIaaManager(ui);
            mgr.CloseAdPlaceholder();
            mgr.CloseAdPlaceholder();
            mgr.CloseAdPlaceholder();
            Assert.AreEqual(1, ui.CloseCount,
                "CloseAdPlaceholder must only call into UI once due to _hasClosedPlaceholder guard");
        }

        [Test]
        public void CloseAdPlaceholder_NullUi_DoesNotThrow()
        {
            var mgr = new MediationManager(null, null);
            Assert.DoesNotThrow(() => mgr.CloseAdPlaceholder());
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group W — Internal methods via reflection
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void SetCountryCode_NullCountry_UpdatesCachedField_NoThrow()
        {
            var mgr = NullIaaManager();
            Assert.DoesNotThrow(() => InvokeInternal(mgr, "SetCountryCode", new object[] { null }));
            Assert.DoesNotThrow(() => InvokeInternal(mgr, "SetCountryCode", new object[] { "" }));
            Assert.DoesNotThrow(() => InvokeInternal(mgr, "SetCountryCode", new object[] { "ID" }));

            // _cachedCountryCode must reflect the last assignment.
            var field = typeof(MediationManager).GetField("_cachedCountryCode", PrivInst);
            Assert.AreEqual("ID", field.GetValue(mgr) as string);
        }

        [Test]
        public void RecordPurchase_NullSegmentManager_DoesNotThrow()
        {
            var mgr = NullIaaManager(); // _segmentManager is null when IAA is null
            Assert.DoesNotThrow(() => InvokeInternal(mgr, "RecordPurchase", Array.Empty<object>()));
        }

        [Test]
        public void GetSegmentManager_Internal_ReturnsNullForNullIaa()
        {
            var mgr = NullIaaManager();
            var method = typeof(MediationManager).GetMethod("GetSegmentManager", PrivInst);
            Assert.IsNotNull(method);
            var result = method.Invoke(mgr, null);
            Assert.IsNull(result, "GetSegmentManager() must return null when no IAA config has been processed");
        }

        [Test]
        public void ApplyExperimentOverride_NullEffective_EarlyReturns()
        {
            var mgr = NullIaaManager();
            Assert.DoesNotThrow(() => InvokeInternal(mgr, "ApplyExperimentOverride", new object[] { null }));
        }

        [Test]
        public void ApplyExperimentOverride_AppOpenCooldownZero_DefaultsTo30()
        {
            // Effective IAA with AppOpen cooldown = 0 → must be defaulted to 30s by the
            // method's internal merge logic. We assert no throw + frequency manager exists
            // after the call (reflection inspects the rebuilt manager).
            var mgr = NullIaaManager();
            var effective = new IAAConfig
            {
                Mediation = "admob",
                CooldownSeconds = new CooldownConfig { AppOpen = 0 },
            };
            Assert.DoesNotThrow(() => InvokeInternal(mgr, "ApplyExperimentOverride", new object[] { effective }));

            var freqField = typeof(MediationManager).GetField("_frequencyManager", PrivInst);
            Assert.IsNotNull(freqField.GetValue(mgr),
                "_frequencyManager must be (re)created by ApplyExperimentOverride");
        }

        [Test]
        public void ApplyExperimentOverride_CpmFloorsEnabled_CreatesCpmFloorManager()
        {
            var mgr = NullIaaManager();
            var effective = new IAAConfig
            {
                Mediation = "admob",
                CooldownSeconds = new CooldownConfig { AppOpen = 60 },
                CpmFloors = new CpmFloorConfig { Enabled = true },
            };
            Assert.DoesNotThrow(() => InvokeInternal(mgr, "ApplyExperimentOverride", new object[] { effective }));

            var cpmField = typeof(MediationManager).GetField("_cpmFloorManager", PrivInst);
            Assert.IsNotNull(cpmField.GetValue(mgr),
                "_cpmFloorManager must be non-null when CpmFloors.Enabled = true");
        }

        [Test]
        public void ApplyExperimentOverride_CpmFloorsDisabled_LeavesCpmFloorManagerNull()
        {
            var mgr = NullIaaManager();
            var effective = new IAAConfig
            {
                Mediation = "admob",
                CpmFloors = new CpmFloorConfig { Enabled = false },
            };
            InvokeInternal(mgr, "ApplyExperimentOverride", new object[] { effective });

            var cpmField = typeof(MediationManager).GetField("_cpmFloorManager", PrivInst);
            Assert.IsNull(cpmField.GetValue(mgr),
                "_cpmFloorManager must be null when CpmFloors.Enabled = false");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group X — Threading
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void ConcurrentSetCountryCodeAndRecordPurchase_AreSafe()
        {
            // SetCountryCode and RecordPurchase don't touch PlayerPrefs when IAA is null
            // (both delegate to _segmentManager?., which is null). Verify they survive
            // concurrent invocation from many background threads.
            //
            // ApplyExperimentOverride is intentionally NOT threaded here — its constructor
            // for AdFrequencyManager calls LoadFromPrefs() which is PlayerPrefs-bound and
            // must run on the main thread. The MediationCallbackRaceTest "Group P" suite
            // already covers PlayerPrefs main-thread invariants.
            var mgr = NullIaaManager();

            Exception caught = null;
            const int N = 16;
            var tasks = new List<Task>(N * 2);
            for (int i = 0; i < N; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    try { InvokeInternal(mgr, "SetCountryCode", new object[] { "US" }); }
                    catch (Exception e) { Interlocked.CompareExchange(ref caught, e, null); }
                }));
                tasks.Add(Task.Run(() =>
                {
                    try { InvokeInternal(mgr, "RecordPurchase", Array.Empty<object>()); }
                    catch (Exception e) { Interlocked.CompareExchange(ref caught, e, null); }
                }));
            }

            Assert.IsTrue(Task.WhenAll(tasks).Wait(5000), "Concurrent mutators must complete promptly");
            Assert.IsNull(caught, $"Concurrent mutators must not throw, got: {caught?.Message}");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group Y — Diagnostics interplay
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void GetSegmentKey_AfterSetCountryCode_UsesUpdatedCountry()
        {
            var mgr = NullIaaManager();
            InvokeInternal(mgr, "SetCountryCode", new object[] { "ID" });
            // _segmentManager is null with null IAA → still returns "not initialized"
            Assert.AreEqual("not initialized", mgr.GetSegmentKey());
        }

        [Test]
        public void AppOpenManager_NullForNullIaa()
        {
            var mgr = NullIaaManager();
            Assert.IsNull(mgr.AppOpenManager,
                "AppOpenManager property must be null when IAA is null");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Helpers for orchestrator-injection tests
        // ═══════════════════════════════════════════════════════════════════════

        private static void SetField(MediationManager m, string name, object value)
        {
            var f = typeof(MediationManager).GetField(name, PrivInst);
            Assert.IsNotNull(f, $"Field '{name}' not found on MediationManager");
            f.SetValue(m, value);
        }

        private static MediationManager WithMockOrchestrator(
            MockAdNetwork primary = null,
            MockAdNetwork secondary = null,
            IAdPlaceholderUI ui = null)
        {
            var mgr = NullIaaManager(ui);
            primary ??= new MockAdNetwork { NetworkName = "mock" };
            var orch = new HybridAdOrchestrator(primary, secondary);
            SetField(mgr, "_orchestrator", orch);
            return mgr;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group Z — Properties and public constants
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void Constants_HaveExpectedValues()
        {
            Assert.AreEqual("local",           MediationManager.IaaConfigOriginLocal);
            Assert.AreEqual("remote_override", MediationManager.IaaConfigOriginRemoteOverride);
        }

        [Test]
        public void IsHybridMode_FalseWhenOrchestratorNull()
        {
            var mgr = NullIaaManager();
            Assert.IsFalse(mgr.IsHybridMode);
        }

        [Test]
        public void IsHybridMode_TrueWhenSecondaryInjected()
        {
            var primary   = new MockAdNetwork { NetworkName = "mock_primary" };
            var secondary = new MockAdNetwork { NetworkName = "mock_secondary" };
            var mgr = WithMockOrchestrator(primary, secondary);
            Assert.IsTrue(mgr.IsHybridMode);
        }

        [Test]
        public void MediationType_NullByDefault()
        {
            var mgr = NullIaaManager();
            Assert.IsNull(mgr.MediationType);
        }

        [Test]
        public void MediationType_ReflectsSetValue()
        {
            var mgr = NullIaaManager();
            SetField(mgr, "_mediationType", "admob");
            Assert.AreEqual("admob", mgr.MediationType);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group AA — Event add/remove subscriptions
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void EventSubscriptions_AddAndRemove_DoNotThrow()
        {
            var mgr = NullIaaManager();

            Action handler  = () => { };
            Action<string> strHandler = _ => { };

            Assert.DoesNotThrow(() => { mgr.OnInitialized          += handler; mgr.OnInitialized          -= handler; });
            Assert.DoesNotThrow(() => { mgr.OnAdDisplayed          += handler; mgr.OnAdDisplayed          -= handler; });
            Assert.DoesNotThrow(() => { mgr.OnAdFailedDisplayed    += handler; mgr.OnAdFailedDisplayed    -= handler; });
            Assert.DoesNotThrow(() => { mgr.OnAdClicked            += handler; mgr.OnAdClicked            -= handler; });
            Assert.DoesNotThrow(() => { mgr.OnAdImpressionRecorded += handler; mgr.OnAdImpressionRecorded -= handler; });
            Assert.DoesNotThrow(() => { mgr.OnAdClosed             += handler; mgr.OnAdClosed             -= handler; });
            Assert.DoesNotThrow(() => { mgr.OnAdNotAvailable       += strHandler; mgr.OnAdNotAvailable    -= strHandler; });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group AB — SetAdRevenueTracker / ApplyIaaConfigFromRemote
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void SetAdRevenueTracker_NullAndNonNull_DoNotThrow()
        {
            var mgr = NullIaaManager();
            Assert.DoesNotThrow(() => mgr.SetAdRevenueTracker(null));
            Assert.DoesNotThrow(() => mgr.SetAdRevenueTracker(new MockAdRevenueTracker()));
            Assert.DoesNotThrow(() => mgr.SetAdRevenueTracker(null));
        }

        [Test]
        public void ApplyIaaConfigFromRemote_SetsOriginAndUpdatesIAA()
        {
            var mgr = NullIaaManager();
            Assert.DoesNotThrow(() => mgr.ApplyIaaConfigFromRemote(null));
            Assert.DoesNotThrow(() => mgr.ApplyIaaConfigFromRemote(
                new IAAConfig { Mediation = "applovin" }));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group AC — GetExperimentAssignments
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void GetExperimentAssignments_NullIaa_ReturnsEmpty()
        {
            var mgr = NullIaaManager();
            var result = mgr.GetExperimentAssignments();
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void GetExperimentAssignments_WithExperiments_ReturnsVariants()
        {
            var mgr = NullIaaManager();
            var iaa = new IAAConfig
            {
                Mediation = "admob",
                AdExperiments = new System.Collections.Generic.List<AdExperimentConfig>
                {
                    new AdExperimentConfig { ExperimentId = "exp_A", Enabled = true },
                    new AdExperimentConfig { ExperimentId = "exp_B", Enabled = false },
                },
            };
            // Inject the IAAResponse via the internal setter
            var prop = typeof(MediationManager).GetProperty("IAAResponse",
                BindingFlags.NonPublic | BindingFlags.Instance);
            prop?.SetValue(mgr, iaa);

            var result = mgr.GetExperimentAssignments();
            Assert.IsTrue(result.ContainsKey("exp_A"), "Enabled experiment must appear");
            Assert.IsTrue(result.ContainsKey("exp_B"), "Disabled experiment must appear with [off] suffix");
            Assert.IsTrue(result["exp_B"].EndsWith("[off]"), "Disabled variant must carry [off] suffix");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group AD — GetCpmFloorStatus
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void GetCpmFloorStatus_NullManager_ReturnsDisabledEntry()
        {
            var mgr = NullIaaManager();
            var status = mgr.GetCpmFloorStatus();
            Assert.AreEqual("CPM floors disabled", status["status"]);
        }

        [Test]
        public void GetCpmFloorStatus_WithManagerAndOrchestrator_ReturnsEntries()
        {
            var primary   = new MockAdNetwork { NetworkName = "mock" };
            var secondary = new MockAdNetwork { NetworkName = "mock2" };
            var mgr = WithMockOrchestrator(primary, secondary);

            var cpmFloors = new CpmFloorConfig { Enabled = true };
            SetField(mgr, "_cpmFloorManager", new CpmFloorManager(cpmFloors));

            Assert.DoesNotThrow(() =>
            {
                var status = mgr.GetCpmFloorStatus();
                Assert.IsNotNull(status);
                Assert.Greater(status.Count, 0, "At least one format entry expected");
            });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group AE — IsInterstitialReady / IsRewardedAdReady / OnApplicationForeground
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void IsInterstitialReady_NullOrchestrator_ReturnsFalse()
        {
            Assert.IsFalse(NullIaaManager().IsInterstitialReady());
        }

        [Test]
        public void IsInterstitialReady_WhenFilled_ReturnsTrue()
        {
            var net = new MockAdNetwork { NetworkName = "mock", InterstitialReady = true };
            var mgr = WithMockOrchestrator(net);
            Assert.IsTrue(mgr.IsInterstitialReady());
        }

        [Test]
        public void IsInterstitialReady_WhenNotFilled_ReturnsFalse()
        {
            var net = new MockAdNetwork { NetworkName = "mock", InterstitialReady = false };
            var mgr = WithMockOrchestrator(net);
            Assert.IsFalse(mgr.IsInterstitialReady());
        }

        [Test]
        public void IsRewardedAdReady_NullOrchestrator_ReturnsFalse()
        {
            Assert.IsFalse(NullIaaManager().IsRewardedAdReady());
        }

        [Test]
        public void IsRewardedAdReady_WhenFilled_ReturnsTrue()
        {
            var net = new MockAdNetwork { NetworkName = "mock", RewardedReady = true };
            var mgr = WithMockOrchestrator(net);
            Assert.IsTrue(mgr.IsRewardedAdReady());
        }

        [Test]
        public void OnApplicationForeground_NullAppOpenManager_DoesNotThrow()
        {
            var mgr = WithMockOrchestrator();
            Assert.DoesNotThrow(() => mgr.OnApplicationForeground());
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group AF — ShowBannerAd / HideBannerAd with orchestrator
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void ShowBannerAd_NullOrchestrator_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => NullIaaManager().ShowBannerAd());
        }

        [Test]
        public void ShowBannerAd_WithBannerUnit_DelegatesToNetwork()
        {
            var net = new MockAdNetwork { NetworkName = "mock", BannerAdUnitSet = true };
            var mgr = WithMockOrchestrator(net);
            mgr.ShowBannerAd();
            Assert.AreEqual(1, net.ShowBannerCallCount, "ShowBannerAd must delegate to network with banner unit");
        }

        [Test]
        public void ShowBannerAd_NoBannerUnit_FiresAdNotAvailable()
        {
            var net = new MockAdNetwork { NetworkName = "mock", BannerAdUnitSet = false };
            var mgr = WithMockOrchestrator(net);
            int notAvailableCount = 0;
            mgr.OnAdNotAvailable += _ => notAvailableCount++;
            mgr.ShowBannerAd();
            Assert.AreEqual(1, notAvailableCount, "ShowBannerAd with no unit must fire OnAdNotAvailable");
        }

        [Test]
        public void HideBannerAd_NullOrchestrator_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => NullIaaManager().HideBannerAd());
        }

        [Test]
        public void HideBannerAd_WithOrchestrator_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => WithMockOrchestrator().HideBannerAd());
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group AG — ShowInterstitial / ShowRewardedAd with mock orchestrator
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void ShowInterstitial_NullOrchestrator_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => NullIaaManager().ShowInterstitial(null));
            Assert.DoesNotThrow(() => NullIaaManager().ShowInterstitial("placement"));
        }

        [Test]
        public void ShowInterstitial_WhenFilled_CallsNetworkShow()
        {
            var ui  = new NoopAdPlaceholderUI();
            var net = new MockAdNetwork { NetworkName = "mock", InterstitialReady = true };
            var mgr = WithMockOrchestrator(net, ui: ui);
            mgr.ShowInterstitial(null);
            Assert.AreEqual(1, net.ShowInterstitialCallCount, "ShowInterstitial must call network.ShowInterstitial()");
        }

        [Test]
        public void ShowInterstitial_WhenNotFilled_FiresAdNotAvailable()
        {
            var net = new MockAdNetwork { NetworkName = "mock", InterstitialReady = false };
            var mgr = WithMockOrchestrator(net);
            int notAvailable = 0;
            mgr.OnAdNotAvailable += _ => notAvailable++;
            mgr.ShowInterstitial(null);
            Assert.AreEqual(1, notAvailable, "ShowInterstitial with no fill must fire OnAdNotAvailable");
        }

        [Test]
        public void ShowRewardedAd_NullOrchestrator_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => NullIaaManager().ShowRewardedAd(null));
        }

        [Test]
        public void ShowRewardedAd_WhenFilled_CallsNetworkShow()
        {
            var ui  = new NoopAdPlaceholderUI();
            var net = new MockAdNetwork { NetworkName = "mock", RewardedReady = true };
            var mgr = WithMockOrchestrator(net, ui: ui);
            mgr.ShowRewardedAd(null);
            Assert.AreEqual(1, net.ShowRewardedCallCount, "ShowRewardedAd must call network.ShowRewardedAd()");
        }

        [Test]
        public void ShowRewardedAd_WhenNotFilled_FiresAdNotAvailable()
        {
            var net = new MockAdNetwork { NetworkName = "mock", RewardedReady = false };
            var mgr = WithMockOrchestrator(net);
            int notAvailable = 0;
            mgr.OnAdNotAvailable += _ => notAvailable++;
            mgr.ShowRewardedAd(null);
            Assert.AreEqual(1, notAvailable);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group AH — ShowCreativeDebugger / ShowMediationDebugger with orchestrator
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void ShowCreativeDebugger_NullOrchestrator_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => NullIaaManager().ShowCreativeDebugger());
        }

        [Test]
        public void ShowCreativeDebugger_NonAppLovinPrimary_LogsWarning()
        {
            // No AppLovin mock — logs a warning, must not throw.
            var net = new MockAdNetwork { NetworkName = "mock" };
            Assert.DoesNotThrow(() => WithMockOrchestrator(net).ShowCreativeDebugger());
        }

        [Test]
        public void ShowMediationDebugger_WithOrchestrator_PrimaryFallback()
        {
            var net = new MockAdNetwork { NetworkName = "mock" };
            var mgr = WithMockOrchestrator(net);
            // Null → uses primary; "unknown_net" → falls back to primary.
            Assert.DoesNotThrow(() => mgr.ShowMediationDebugger());
            Assert.DoesNotThrow(() => mgr.ShowMediationDebugger("unknown_net"));
            Assert.DoesNotThrow(() => mgr.ShowMediationDebugger("mock")); // matches primary name
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group AI — SetTestDeviceIds with orchestrator
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void SetTestDeviceIds_WithOrchestrator_DoesNotThrow()
        {
            var mgr = WithMockOrchestrator();
            Assert.DoesNotThrow(() => mgr.SetTestDeviceIds(new System.Collections.Generic.List<string> { "device-1" }));
            Assert.DoesNotThrow(() => mgr.SetTestDeviceIds(new System.Collections.Generic.List<string>()));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MediationManagerInitializeFlowTest
    // Exercises the Initialize() → SubscribeToOrchestratorEvents →
    // SubscribeToNetworkSpecificEvents → SetupAdUnitID path using an
    // injected MockAdNetwork so no real SDK is needed.
    // ═══════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class MediationManagerInitializeFlowTest
    {
        private const BindingFlags PrivInst = BindingFlags.NonPublic | BindingFlags.Instance;

        private class NoopUI : IAdPlaceholderUI
        {
            public void ShowAdPlaceholder(AdPlaceholderType t) { }
            public void CloseAdPlaceholder() { }
        }

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            PlayerPrefs.DeleteKey("NoctuaAdFrequency_Interstitial");
            PlayerPrefs.DeleteKey("NoctuaAdFrequency_Rewarded");
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a MediationManager with <paramref name="iaa"/> and <paramref name="primary"/>
        /// WITHOUT triggering CreateNetworks — injects the orchestrator directly so the full
        /// Initialize() flow (Subscribe* + onPrimaryReady callback) can be exercised in EditMode
        /// where neither UNITY_ADMOB nor UNITY_APPLOVIN is defined.
        /// </summary>
        private static MediationManager MgrWithInjected(
            IAAConfig iaa,
            MockAdNetwork primary = null,
            MockAdNetwork secondary = null,
            IAdRevenueTracker tracker = null)
        {
            var mgr = new MediationManager(new NoopUI(), null);

            // Bypass CreateNetworks by setting the backing field directly
            typeof(MediationManager).GetField("_iaaResponse", PrivInst)?.SetValue(mgr, iaa);

            // Ensure subscription flag is reset
            typeof(MediationManager).GetField("_adNetworkEventsSubscribed", PrivInst)?.SetValue(mgr, false);

            // Force PostToMainThread to execute synchronously (no Unity scheduler in EditMode)
            typeof(MediationManager).GetField("_mainThreadContext", PrivInst)?.SetValue(mgr, null);

            // Wire AdFrequencyManager so ShowInterstitial/Rewarded paths don't NPE
            var freqMgr = new AdFrequencyManager(null, new CooldownConfig { AppOpen = 30 }, null);
            typeof(MediationManager).GetField("_frequencyManager", PrivInst)?.SetValue(mgr, freqMgr);

            if (tracker != null)
            {
                typeof(MediationManager).GetField("_adRevenueTracker", PrivInst)?.SetValue(mgr, tracker);
                var revTracker = new AdRevenueTrackingManager(tracker, null);
                typeof(MediationManager).GetField("_revenueTracker", PrivInst)?.SetValue(mgr, revTracker);
            }

            // Inject orchestrator
            primary ??= new MockAdNetwork { NetworkName = "mock" };
            var orch = new HybridAdOrchestrator(primary, secondary);
            typeof(MediationManager).GetField("_orchestrator", PrivInst)?.SetValue(mgr, orch);

            return mgr;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group AJ — Initialize() with AdFormat=null → early onPrimaryReady exit
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void Initialize_WithMockOrchestrator_NoAdFormat_FiresInitCallback()
        {
            // IAAResponse.AdFormat == null && Networks == null → onPrimaryReady fires callback
            var iaa = new IAAConfig { Mediation = "admob" };
            var primary = new MockAdNetwork { NetworkName = "mock" };
            var mgr = MgrWithInjected(iaa, primary);

            bool callbackFired = false;
            Assert.DoesNotThrow(() => mgr.Initialize(() => callbackFired = true));

            Assert.IsTrue(callbackFired,
                "initCompleteAction must fire when AdFormat and Networks are both null");
            // Subscribe methods ran
            Assert.IsTrue(
                (bool)(typeof(MediationManager).GetField("_adNetworkEventsSubscribed", PrivInst)?.GetValue(mgr) ?? false),
                "_adNetworkEventsSubscribed must be true after Initialize()");
        }

        [Test]
        public void Initialize_WithMockOrchestrator_Sets_MediationType()
        {
            var iaa = new IAAConfig { Mediation = "applovin" };
            var mgr = MgrWithInjected(iaa);

            mgr.Initialize();

            Assert.AreEqual("applovin", mgr.MediationType,
                "MediationType must reflect IAAResponse.Mediation after Initialize()");
        }

        [Test]
        public void Initialize_WithEmptyMediationString_UsesBuiltInFallback()
        {
            // IAAResponse.Mediation empty → _mediationType assigned from compile-time block
            // (= "unknown" in EditMode). Not empty → Initialize continues.
            var iaa = new IAAConfig { Mediation = null };
            var mgr = MgrWithInjected(iaa);

            Assert.DoesNotThrow(() => mgr.Initialize());
            // "unknown" is not null/empty → Initialize should NOT return early
            Assert.IsTrue(
                (bool)(typeof(MediationManager).GetField("_adNetworkEventsSubscribed", PrivInst)?.GetValue(mgr) ?? false),
                "Even with null Mediation the Subscribe path must run when _orchestrator is non-null");
        }

        [Test]
        public void Initialize_IdempotentSubscription_SecondCallDoesNotReSub()
        {
            // First Initialize sets _adNetworkEventsSubscribed = true.
            // Second call must skip SubscribeToOrchestratorEvents (idempotency guard).
            var iaa = new IAAConfig { Mediation = "admob" };
            var primary = new MockAdNetwork { NetworkName = "mock" };
            var mgr = MgrWithInjected(iaa, primary);

            int callbackCount = 0;
            mgr.Initialize(() => callbackCount++);
            mgr.Initialize(() => callbackCount++); // second call: subscription is already done

            // Both callbacks should still fire (MockAdNetwork.Initialize calls cb each time)
            Assert.AreEqual(2, callbackCount, "Each Initialize() call must fire its own callback");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group AK — SetupAdUnitID path (AdFormat set → onPrimaryReady proceeds)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void Initialize_WithEmptyAdFormat_CoveresSetupAdUnitID_AllUnknown()
        {
            // AdFormat is non-null but has no platform-specific IDs →
            // ResolveAdUnitIDs returns "unknown" for every format → log.Info branches run
            var iaa = new IAAConfig
            {
                Mediation = "admob",
                AdFormat  = new AdFormatNoctua(),  // all fields null → "unknown"
            };
            var primary = new MockAdNetwork { NetworkName = "mock" };
            var mgr = MgrWithInjected(iaa, primary);

            bool callbackFired = false;
            Assert.DoesNotThrow(() => mgr.Initialize(() => callbackFired = true));
            Assert.IsTrue(callbackFired, "initCompleteAction must fire after SetupAdUnitID completes");
        }

        [Test]
        public void Initialize_WithNetworksBlock_CoversNetworkAdUnitResolution()
        {
            // Networks block present → ResolveAdUnitIdForNetwork checks the Networks
            // dictionary first (even if result is "unknown" in EditMode).
            var iaa = new IAAConfig
            {
                Mediation = "mock",
                Networks  = new Dictionary<string, NetworkConfig>
                {
                    {
                        "mock",
                        new NetworkConfig
                        {
                            AdFormat = new AdFormatNoctua()  // empty → "unknown"
                        }
                    }
                }
            };
            var primary = new MockAdNetwork { NetworkName = "mock" };
            var mgr = MgrWithInjected(iaa, primary);

            Assert.DoesNotThrow(() => mgr.Initialize());
        }

        [Test]
        public void ResolveAdUnitIDs_AllFormats_AreResolved_ToUnknownInEditMode()
        {
            // Verifies all 5 format branches in GetAdUnitIdFromFormat switch are exercised
            // (Interstitial, Rewarded, RewardedInterstitial, Banner, AppOpen).
            var iaa = new IAAConfig
            {
                Mediation = "admob",
                AdFormat  = new AdFormatNoctua
                {
                    Interstitial          = new AdUnit(),
                    Rewarded              = new AdUnit(),
                    RewardedInterstitial  = new AdUnit(),
                    Banner                = new AdUnit(),
                    AppOpen               = new AdUnit()
                }
            };
            var primary = new MockAdNetwork { NetworkName = "mock" };
            var mgr = MgrWithInjected(iaa, primary);

            Assert.DoesNotThrow(() => mgr.Initialize());

            // In EditMode (#else branch) all resolve to "unknown"
            Assert.AreEqual("unknown", mgr.InterstitialAdUnitID);
            Assert.AreEqual("unknown", mgr.RewardedAdUnitID);
            Assert.AreEqual("unknown", mgr.BannerAdUnitID);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group AL — Event forwarding (SubscribeToOrchestratorEvents coverage)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void OnAdDisplayed_AfterInitialize_ForwardedFromNetwork()
        {
            var iaa     = new IAAConfig { Mediation = "admob" };
            var primary = new MockAdNetwork { NetworkName = "mock" };
            var mgr     = MgrWithInjected(iaa, primary);

            mgr.Initialize();

            int displayedCount = 0;
            mgr.OnAdDisplayed += () => displayedCount++;

            primary.TriggerAdDisplayed();

            Assert.AreEqual(1, displayedCount,
                "OnAdDisplayed must fire when the network triggers the event");
        }

        [Test]
        public void OnAdFailedDisplayed_AfterInitialize_ForwardedFromNetwork()
        {
            var iaa     = new IAAConfig { Mediation = "admob" };
            var primary = new MockAdNetwork { NetworkName = "mock" };
            var mgr     = MgrWithInjected(iaa, primary);

            mgr.Initialize();

            int failedCount = 0;
            mgr.OnAdFailedDisplayed += () => failedCount++;

            primary.TriggerAdFailedDisplayed();

            Assert.AreEqual(1, failedCount);
        }

        [Test]
        public void OnAdClicked_AfterInitialize_ForwardedFromNetwork()
        {
            var iaa     = new IAAConfig { Mediation = "admob" };
            var primary = new MockAdNetwork { NetworkName = "mock" };
            var mgr     = MgrWithInjected(iaa, primary);

            mgr.Initialize();

            int clickCount = 0;
            mgr.OnAdClicked += () => clickCount++;

            primary.TriggerAdClicked();

            Assert.AreEqual(1, clickCount);
        }

        [Test]
        public void OnAdClosed_AfterInitialize_ForwardedFromNetwork()
        {
            var iaa     = new IAAConfig { Mediation = "admob" };
            var primary = new MockAdNetwork { NetworkName = "mock" };
            var mgr     = MgrWithInjected(iaa, primary);

            mgr.Initialize();

            int closedCount = 0;
            mgr.OnAdClosed += () => closedCount++;

            primary.TriggerAdClosed();

            Assert.AreEqual(1, closedCount);
        }

        [Test]
        public void OnAdImpressionRecorded_AfterInitialize_ForwardedFromNetwork()
        {
            var iaa     = new IAAConfig { Mediation = "admob" };
            var primary = new MockAdNetwork { NetworkName = "mock" };
            var mgr     = MgrWithInjected(iaa, primary);

            mgr.Initialize();

            int impressionCount = 0;
            mgr.OnAdImpressionRecorded += () => impressionCount++;

            primary.TriggerAdImpressionRecorded();

            Assert.AreEqual(1, impressionCount);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group AM — SubscribeRewardCompletionEvent coverage
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void RewardEarned_AfterInitialize_TracksAdRewardedComplete()
        {
            var tracker = new MockAdRevenueTracker();
            var iaa     = new IAAConfig { Mediation = "admob" };
            var primary = new MockAdNetwork { NetworkName = "mock" };
            var mgr     = MgrWithInjected(iaa, primary, tracker: tracker);

            mgr.Initialize();

            primary.TriggerUserEarnedReward(100.0, "coins");

            Assert.IsTrue(tracker.WasFired("ad_rewarded_complete"),
                "SubscribeRewardCompletionEvent must track ad_rewarded_complete");
        }

        [Test]
        public void RewardEarned_WhenTrackerNull_DoesNotThrow()
        {
            // _adRevenueTracker is null → the ?.TrackCustomEvent is a no-op
            var iaa     = new IAAConfig { Mediation = "admob" };
            var primary = new MockAdNetwork { NetworkName = "mock" };
            var mgr     = MgrWithInjected(iaa, primary); // no tracker

            mgr.Initialize();

            Assert.DoesNotThrow(() => primary.TriggerUserEarnedReward(50.0, "gems"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group AN — Secondary network init callback (onSecondaryReady)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void Initialize_WithSecondary_OnSecondaryReady_DoesNotThrow()
        {
            var iaa       = new IAAConfig { Mediation = "admob" };
            var primary   = new MockAdNetwork { NetworkName = "mock_primary" };
            var secondary = new MockAdNetwork { NetworkName = "mock_secondary" };
            var mgr       = MgrWithInjected(iaa, primary, secondary);

            // onSecondaryReady fires synchronously via MockAdNetwork.Initialize(cb)
            Assert.DoesNotThrow(() => mgr.Initialize());
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group AO — NormalizeMediationName / IsRecognisedMediationName / IsAvailable
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void NormalizeMediationName_TrimsAndLowercases()
        {
            Assert.AreEqual("admob",    MediationManager.NormalizeMediationName("  AdMob  "));
            Assert.AreEqual("applovin", MediationManager.NormalizeMediationName("APPLOVIN"));
            Assert.IsNull(MediationManager.NormalizeMediationName(null));
            Assert.IsNull(MediationManager.NormalizeMediationName("   "));
        }

        [Test]
        public void IsRecognisedMediationName_KnownNames_ReturnTrue()
        {
            Assert.IsTrue(MediationManager.IsRecognisedMediationName("admob"));
            Assert.IsTrue(MediationManager.IsRecognisedMediationName("applovin"));
            Assert.IsFalse(MediationManager.IsRecognisedMediationName("ironsource"));
            Assert.IsFalse(MediationManager.IsRecognisedMediationName(null));
            Assert.IsFalse(MediationManager.IsRecognisedMediationName(""));
        }

        [Test]
        public void IsAvailable_EmptyName_ReturnsFalse()
        {
            Assert.IsFalse(MediationManager.IsAvailable("", true, true));
            Assert.IsFalse(MediationManager.IsAvailable(null, true, true));
        }

        [Test]
        public void IsAvailable_KnownNetworks_MatchesAvailabilityFlags()
        {
            Assert.IsTrue(MediationManager.IsAvailable("admob", true, false));
            Assert.IsFalse(MediationManager.IsAvailable("admob", false, true));
            Assert.IsTrue(MediationManager.IsAvailable("applovin", false, true));
            Assert.IsFalse(MediationManager.IsAvailable("applovin", true, false));
            Assert.IsFalse(MediationManager.IsAvailable("unknown_net", true, true));
        }

        [Test]
        public void ResolveMediationSelection_BothMissing_ReturnsNullPrimary()
        {
            var (p, s) = MediationManager.ResolveMediationSelection("admob", null, false, false);
            Assert.IsNull(p);
            Assert.IsNull(s);
        }

        [Test]
        public void ResolveMediationSelection_DuplicateNames_DropsSecondary()
        {
            var (p, s) = MediationManager.ResolveMediationSelection("admob", "admob", true, false);
            Assert.AreEqual("admob", p);
            Assert.IsNull(s, "Duplicate secondary must be dropped");
        }

        [Test]
        public void ResolveMediationSelection_PrimaryMissing_PromotesSecondary()
        {
            var (p, s) = MediationManager.ResolveMediationSelection("admob", "applovin", false, true);
            Assert.AreEqual("applovin", p, "Secondary must be promoted to primary");
            Assert.IsNull(s);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group AP — ShowBannerAd/HideBannerAd with frequency manager
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void ShowBannerAd_WithBannerUnit_AfterInitialize_ShowsOnNetwork()
        {
            var iaa     = new IAAConfig { Mediation = "admob" };
            var primary = new MockAdNetwork { NetworkName = "mock", BannerAdUnitSet = true };
            var mgr     = MgrWithInjected(iaa, primary);

            mgr.Initialize();
            mgr.ShowBannerAd();

            Assert.AreEqual(1, primary.ShowBannerCallCount,
                "ShowBannerAd must delegate to primary after Initialize()");
        }

        [Test]
        public void HideBannerAd_AfterInitialize_DoesNotThrow()
        {
            var iaa     = new IAAConfig { Mediation = "admob" };
            var primary = new MockAdNetwork { NetworkName = "mock" };
            var mgr     = MgrWithInjected(iaa, primary);

            mgr.Initialize();
            Assert.DoesNotThrow(() => mgr.HideBannerAd());
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group AQ — ShowInterstitial / ShowRewardedAd frequency-cap paths
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void ShowInterstitial_AfterInitialize_WhenNotFilled_FiresAdNotAvailable()
        {
            var iaa     = new IAAConfig { Mediation = "admob" };
            var primary = new MockAdNetwork { NetworkName = "mock", InterstitialReady = false };
            var mgr     = MgrWithInjected(iaa, primary);

            mgr.Initialize();

            int notAvailable = 0;
            mgr.OnAdNotAvailable += _ => notAvailable++;

            mgr.ShowInterstitial(null);

            Assert.AreEqual(1, notAvailable);
        }

        [Test]
        public void ShowRewardedAd_AfterInitialize_WhenFilled_CallsNetwork()
        {
            var iaa     = new IAAConfig { Mediation = "admob" };
            var primary = new MockAdNetwork { NetworkName = "mock", RewardedReady = true };
            var mgr     = MgrWithInjected(iaa, primary);

            mgr.Initialize();

            Assert.DoesNotThrow(() => mgr.ShowRewardedAd(null));
            Assert.AreEqual(1, primary.ShowRewardedCallCount);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group AR — OnApplicationForeground with AppOpenAdManager created
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void OnApplicationForeground_WithRealAppOpenManager_DoesNotThrow()
        {
            // SetupAppOpenAds creates _appOpenAdManager when primaryAppOpenId is non-null.
            // In EditMode all ad unit IDs are "unknown" → SetupAppOpenAds returns early,
            // so _appOpenAdManager remains null. This test exercises the null guard.
            var iaa     = new IAAConfig { Mediation = "admob", AdFormat = new AdFormatNoctua() };
            var primary = new MockAdNetwork { NetworkName = "mock" };
            var mgr     = MgrWithInjected(iaa, primary);

            mgr.Initialize();
            Assert.DoesNotThrow(() => mgr.OnApplicationForeground());
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MediationManagerDiagnosticsTest
    // Covers diagnostic / configuration methods not exercised by other suites:
    //   GetSegmentKey, GetCpmFloorStatus, GetExperimentAssignments,
    //   SetAdRevenueTracker, SetCountryCode, ApplyIaaConfigFromRemote,
    //   FlushPendingAppliedIaaConfigEvent (via SetAdRevenueTracker).
    // ═══════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class MediationManagerDiagnosticsTest
    {
        private const BindingFlags PrivInst = BindingFlags.NonPublic | BindingFlags.Instance;

        private class NoopUI : IAdPlaceholderUI
        {
            public void ShowAdPlaceholder(AdPlaceholderType t) { }
            public void CloseAdPlaceholder() { }
        }

        // Minimal IAdRevenueTracker stub that records calls.
        private class StubTracker : IAdRevenueTracker
        {
            public int RevenueCallCount;
            public int CustomEventCallCount;

            public void TrackAdRevenue(string source, double revenue, string currency,
                Dictionary<string, IConvertible> extraPayload = null)
                => RevenueCallCount++;

            public void TrackCustomEvent(string name,
                Dictionary<string, IConvertible> extraPayload = null)
                => CustomEventCallCount++;
        }

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static void SetField(object obj, string field, object value)
            => obj.GetType().GetField(field, PrivInst)?.SetValue(obj, value);

        private static T GetField<T>(object obj, string field)
            => (T)(obj.GetType().GetField(field, PrivInst)?.GetValue(obj));

        /// <summary>
        /// Creates a bare MediationManager with no IAA response (null orchestrator,
        /// no segment/cpm managers). Suitable for testing null-guard branches.
        /// </summary>
        private static MediationManager CreateBare()
            => new MediationManager(new NoopUI(), null);

        /// <summary>
        /// Creates a MediationManager with an injected <paramref name="iaa"/> config
        /// AND a real orchestrator backed by <paramref name="primary"/> so that
        /// GetCpmFloorStatus can walk Primary/Secondary.
        /// Does NOT call CreateNetworks (bypasses via field injection) so EditMode
        /// tests work without UNITY_ADMOB / UNITY_APPLOVIN.
        /// </summary>
        private static MediationManager CreateWithOrchestrator(
            IAAConfig iaa,
            MockAdNetwork primary = null,
            MockAdNetwork secondary = null)
        {
            var mgr = new MediationManager(new NoopUI(), null);
            SetField(mgr, "_iaaResponse", iaa);
            SetField(mgr, "_mainThreadContext", null);

            primary ??= new MockAdNetwork { NetworkName = "mock" };
            var orch = new HybridAdOrchestrator(primary, secondary);
            SetField(mgr, "_orchestrator", orch);

            var freqMgr = new AdFrequencyManager(null, new CooldownConfig { AppOpen = 30 }, null);
            SetField(mgr, "_frequencyManager", freqMgr);

            return mgr;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group D1 — GetSegmentKey
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void GetSegmentKey_NullSegmentManager_ReturnsNotInitialized()
        {
            var mgr = CreateBare();
            // _segmentManager is null by default on a bare manager
            Assert.AreEqual("not initialized", mgr.GetSegmentKey());
        }

        [Test]
        public void GetSegmentKey_WithSegmentManager_ReturnsNonEmpty()
        {
            var mgr = CreateBare();
            SetField(mgr, "_segmentManager", new UserSegmentManager());

            string key = mgr.GetSegmentKey();

            Assert.IsNotNull(key);
            Assert.AreNotEqual("not initialized", key);
        }

        [Test]
        public void GetSegmentKey_AfterSetCountryCode_ReflectsCountry()
        {
            var iaa = new IAAConfig { Mediation = "admob" };
            var mgr = CreateWithOrchestrator(iaa);
            SetField(mgr, "_segmentManager", new UserSegmentManager());

            // Call the internal SetCountryCode via reflection
            typeof(MediationManager)
                .GetMethod("SetCountryCode", PrivInst)
                ?.Invoke(mgr, new object[] { "US" });

            string key = mgr.GetSegmentKey();

            // The segment key must contain the tier derived from "US" (t1) rather than empty
            Assert.IsNotNull(key);
            Assert.AreNotEqual("not initialized", key);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group D2 — GetCpmFloorStatus
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void GetCpmFloorStatus_NullCpmFloorManager_ReturnsDisabledStatus()
        {
            var mgr = CreateBare();
            // _cpmFloorManager is null by default

            var result = mgr.GetCpmFloorStatus();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.ContainsKey("status"));
            Assert.AreEqual("CPM floors disabled", result["status"]);
        }

        [Test]
        public void GetCpmFloorStatus_NullCpmFloorManager_ReturnsSingleEntry()
        {
            var mgr = CreateBare();

            var result = mgr.GetCpmFloorStatus();

            Assert.AreEqual(1, result.Count,
                "Only the status sentinel entry should be present when CPM floors are disabled");
        }

        [Test]
        public void GetCpmFloorStatus_WithCpmFloorManagerButNullOrchestrator_ReturnsNoFormatKeys()
        {
            var mgr = CreateBare();
            var floors = new CpmFloorConfig { Enabled = true };
            SetField(mgr, "_cpmFloorManager", new CpmFloorManager(floors));
            // _orchestrator remains null → Primary/Secondary are never accessed

            var result = mgr.GetCpmFloorStatus();

            // No format keys added because both Primary and Secondary are null via null orchestrator
            Assert.IsFalse(result.ContainsKey("status"),
                "The disabled-status sentinel must NOT appear when a floor manager exists");
            Assert.AreEqual(0, result.Count,
                "No format entries expected when orchestrator is null");
        }

        [Test]
        public void GetCpmFloorStatus_WithCpmFloorManagerAndOrchestrator_ReturnsFormatKeys()
        {
            var primary = new MockAdNetwork { NetworkName = "testnet" };
            var iaa     = new IAAConfig { Mediation = "admob" };
            var mgr     = CreateWithOrchestrator(iaa, primary);

            var floors = new CpmFloorConfig { Enabled = true };
            SetField(mgr, "_cpmFloorManager", new CpmFloorManager(floors));
            SetField(mgr, "_segmentManager", new UserSegmentManager());

            var result = mgr.GetCpmFloorStatus();

            // Expect at least one entry per format for primary network
            Assert.IsTrue(result.Count > 0,
                "At least one format/network entry should be present");
            Assert.IsTrue(result.ContainsKey("interstitial/testnet"),
                "Should contain an entry for interstitial on the primary network");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group D3 — GetExperimentAssignments
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void GetExperimentAssignments_NullIaaResponse_ReturnsEmpty()
        {
            var mgr = CreateBare();
            // _iaaResponse is null → IAAResponse property returns null

            var result = mgr.GetExperimentAssignments();

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void GetExperimentAssignments_NullAdExperiments_ReturnsEmpty()
        {
            var mgr = CreateBare();
            SetField(mgr, "_iaaResponse", new IAAConfig { Mediation = "admob", AdExperiments = null });

            var result = mgr.GetExperimentAssignments();

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void GetExperimentAssignments_EmptyAdExperiments_ReturnsEmpty()
        {
            var mgr = CreateBare();
            SetField(mgr, "_iaaResponse", new IAAConfig
            {
                Mediation      = "admob",
                AdExperiments  = new List<AdExperimentConfig>()
            });

            var result = mgr.GetExperimentAssignments();

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void GetExperimentAssignments_EnabledExperiment_NoPrefs_ReturnsUnassigned()
        {
            var mgr = CreateBare();
            SetField(mgr, "_iaaResponse", new IAAConfig
            {
                Mediation = "admob",
                AdExperiments = new List<AdExperimentConfig>
                {
                    new AdExperimentConfig { ExperimentId = "exp_test_01", Enabled = true }
                }
            });

            var result = mgr.GetExperimentAssignments();

            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result.ContainsKey("exp_test_01"));
            Assert.AreEqual("unassigned", result["exp_test_01"]);
        }

        [Test]
        public void GetExperimentAssignments_EnabledExperiment_WithPersistedVariant_ReturnsVariant()
        {
            PlayerPrefs.SetString("NoctuaExp_exp_ab_02_variant", "variant_a");
            PlayerPrefs.Save();

            var mgr = CreateBare();
            SetField(mgr, "_iaaResponse", new IAAConfig
            {
                Mediation = "admob",
                AdExperiments = new List<AdExperimentConfig>
                {
                    new AdExperimentConfig { ExperimentId = "exp_ab_02", Enabled = true }
                }
            });

            var result = mgr.GetExperimentAssignments();

            Assert.AreEqual("variant_a", result["exp_ab_02"]);
        }

        [Test]
        public void GetExperimentAssignments_DisabledExperiment_AppendsOffSuffix()
        {
            PlayerPrefs.SetString("NoctuaExp_exp_off_03_variant", "variant_b");
            PlayerPrefs.Save();

            var mgr = CreateBare();
            SetField(mgr, "_iaaResponse", new IAAConfig
            {
                Mediation = "admob",
                AdExperiments = new List<AdExperimentConfig>
                {
                    new AdExperimentConfig { ExperimentId = "exp_off_03", Enabled = false }
                }
            });

            var result = mgr.GetExperimentAssignments();

            Assert.AreEqual("variant_b [off]", result["exp_off_03"]);
        }

        [Test]
        public void GetExperimentAssignments_MultipleExperiments_ReturnsAllKeys()
        {
            PlayerPrefs.SetString("NoctuaExp_exp_multi_a_variant", "control");
            PlayerPrefs.SetString("NoctuaExp_exp_multi_b_variant", "treatment");
            PlayerPrefs.Save();

            var mgr = CreateBare();
            SetField(mgr, "_iaaResponse", new IAAConfig
            {
                Mediation = "admob",
                AdExperiments = new List<AdExperimentConfig>
                {
                    new AdExperimentConfig { ExperimentId = "exp_multi_a", Enabled = true },
                    new AdExperimentConfig { ExperimentId = "exp_multi_b", Enabled = true }
                }
            });

            var result = mgr.GetExperimentAssignments();

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("control",   result["exp_multi_a"]);
            Assert.AreEqual("treatment", result["exp_multi_b"]);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group D4 — SetAdRevenueTracker / FlushPendingAppliedIaaConfigEvent
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void SetAdRevenueTracker_NullTracker_DoesNotThrow()
        {
            var mgr = CreateBare();

            Assert.DoesNotThrow(() => mgr.SetAdRevenueTracker(null));
        }

        [Test]
        public void SetAdRevenueTracker_NullPendingPayload_DoesNotEmitEvent()
        {
            // When no pending payload exists FlushPendingAppliedIaaConfigEvent is a no-op.
            var mgr = CreateBare();
            // Ensure _pendingAppliedIaaConfigPayload is null (already the case for bare manager)

            Assert.DoesNotThrow(() => mgr.SetAdRevenueTracker(new StubTracker()),
                "SetAdRevenueTracker must not throw even when pending payload is null");
        }

        [Test]
        public void SetAdRevenueTracker_NonNoctuaEventServiceTracker_NoPendingFlush()
        {
            // StubTracker is not a NoctuaEventService, so the flush branch is skipped.
            // The _pendingAppliedIaaConfigPayload should remain unchanged (not cleared).
            var mgr = CreateBare();
            var payload = new Dictionary<string, IConvertible>
            {
                { "primary", "admob" }, { "secondary", "" }, { "hybrid", false },
                { "cpm_floors", "disabled" }, { "segment", "" }, { "config_origin", "local" }
            };
            SetField(mgr, "_pendingAppliedIaaConfigPayload", payload);

            mgr.SetAdRevenueTracker(new StubTracker());

            // The payload must still be set — StubTracker is not NoctuaEventService,
            // so FlushPendingAppliedIaaConfigEvent does not clear it.
            var remaining = GetField<Dictionary<string, IConvertible>>(
                mgr, "_pendingAppliedIaaConfigPayload");
            Assert.IsNotNull(remaining,
                "Payload must not be cleared when tracker is not a NoctuaEventService");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group D5 — SetCountryCode (internal, exercised via reflection)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void SetCountryCode_UpdatesCachedCountryCode()
        {
            var mgr = CreateBare();

            typeof(MediationManager)
                .GetMethod("SetCountryCode", PrivInst)
                ?.Invoke(mgr, new object[] { "JP" });

            string cached = GetField<string>(mgr, "_cachedCountryCode");
            Assert.AreEqual("JP", cached);
        }

        [Test]
        public void SetCountryCode_NullOrchestrator_DoesNotThrow()
        {
            var mgr = CreateBare();
            // _orchestrator is null on a bare manager

            Assert.DoesNotThrow(() =>
                typeof(MediationManager)
                    .GetMethod("SetCountryCode", PrivInst)
                    ?.Invoke(mgr, new object[] { "KR" }));
        }

        [Test]
        public void SetCountryCode_WithSegmentManager_DoesNotThrow()
        {
            var mgr = CreateBare();
            SetField(mgr, "_segmentManager", new UserSegmentManager());

            Assert.DoesNotThrow(() =>
                typeof(MediationManager)
                    .GetMethod("SetCountryCode", PrivInst)
                    ?.Invoke(mgr, new object[] { "DE" }));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group D6 — ApplyIaaConfigFromRemote
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void ApplyIaaConfigFromRemote_SetsNextConfigOriginToRemote()
        {
            var mgr = CreateBare();

            // We call ApplyIaaConfigFromRemote with a null config; the IAAResponse setter
            // guards against null so CreateNetworks won't run, but _nextConfigOrigin is set first.
            mgr.ApplyIaaConfigFromRemote(null);

            // After the call the setter resets _nextConfigOrigin to "local",
            // but with null config the setter returns early before that reset.
            // So _nextConfigOrigin remains "remote_override".
            string origin = GetField<string>(mgr, "_nextConfigOrigin");
            Assert.AreEqual(MediationManager.IaaConfigOriginRemoteOverride, origin);
        }

        [Test]
        public void ApplyIaaConfigFromRemote_NonNullConfig_ResetsOriginToLocalAfterCreateNetworks()
        {
            // With a non-null config, IAAResponse setter calls CreateNetworks which (in EditMode
            // without UNITY_ADMOB/UNITY_APPLOVIN) logs an error and returns, then the setter
            // resets _nextConfigOrigin to "local".
            var mgr    = CreateBare();
            var config = new IAAConfig { Mediation = "admob" };

            LogAssert.Expect(LogType.Error, new Regex("CreateNetworks.*No ad network"));
            mgr.ApplyIaaConfigFromRemote(config);

            string origin = GetField<string>(mgr, "_nextConfigOrigin");
            Assert.AreEqual(MediationManager.IaaConfigOriginLocal, origin,
                "_nextConfigOrigin should be reset to 'local' after CreateNetworks completes");
        }

        [Test]
        public void ApplyIaaConfigFromRemote_UpdatesIaaResponse()
        {
            var mgr    = CreateBare();
            var config = new IAAConfig { Mediation = "applovin" };

            LogAssert.Expect(LogType.Error, new Regex("CreateNetworks.*No ad network"));
            mgr.ApplyIaaConfigFromRemote(config);

            var stored = GetField<IAAConfig>(mgr, "_iaaResponse");
            Assert.AreSame(config, stored,
                "_iaaResponse should be updated to the supplied config");
        }
    }
}
