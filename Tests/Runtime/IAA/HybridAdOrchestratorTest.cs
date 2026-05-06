using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Runtime.IAA
{
    /// <summary>
    /// Unit tests for <see cref="HybridAdOrchestrator"/>.
    /// Covers: initialization, format routing (overrides + dynamic optimization),
    /// ShowWithFallback paths, and event forwarding from both networks.
    /// </summary>
    [TestFixture]
    public class HybridAdOrchestratorTest
    {
        private MockAdNetwork _primary;
        private MockAdNetwork _secondary;

        [SetUp]
        public void SetUp()
        {
            _primary   = new MockAdNetwork { NetworkName = "admob" };
            _secondary = new MockAdNetwork { NetworkName = "applovin" };

            // Clear AdNetworkPerformanceTracker PlayerPrefs to prevent cross-test pollution
            foreach (var network in new[] { "admob", "applovin" })
            foreach (var format  in new[] { AdFormatKey.Interstitial, AdFormatKey.Rewarded,
                                             AdFormatKey.Banner,       AdFormatKey.AppOpen })
            {
                PlayerPrefs.DeleteKey($"NoctuaAdPerf_fill_{network}_{format}");
                PlayerPrefs.DeleteKey($"NoctuaAdPerf_rev_{network}_{format}");
            }
            PlayerPrefs.Save();
        }

        // ─── Constructor / IsHybridMode ───────────────────────────────────────

        [Test]
        public void IsHybridMode_PrimaryOnly_ReturnsFalse()
        {
            var orc = new HybridAdOrchestrator(_primary);
            Assert.IsFalse(orc.IsHybridMode);
        }

        [Test]
        public void IsHybridMode_WithSecondary_ReturnsTrue()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            Assert.IsTrue(orc.IsHybridMode);
        }

        [Test]
        public void Primary_AlwaysReturnsConstructedPrimary()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            Assert.AreSame(_primary, orc.Primary);
        }

        [Test]
        public void Secondary_WithSecondary_ReturnsIt()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            Assert.AreSame(_secondary, orc.Secondary);
        }

        [Test]
        public void Secondary_PrimaryOnly_ReturnsNull()
        {
            var orc = new HybridAdOrchestrator(_primary);
            Assert.IsNull(orc.Secondary);
        }

        // ─── Initialize ───────────────────────────────────────────────────────

        [Test]
        public void Initialize_CallsPrimaryInitCallback()
        {
            var orc = new HybridAdOrchestrator(_primary);
            bool primaryReady = false;

            orc.Initialize(onPrimaryReady: () => primaryReady = true);

            Assert.IsTrue(primaryReady);
            Assert.AreEqual(1, _primary.InitializeCallCount);
        }

        [Test]
        public void Initialize_WithSecondary_CallsBothCallbacks()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            bool primaryReady   = false;
            bool secondaryReady = false;

            orc.Initialize(
                onPrimaryReady:   () => primaryReady = true,
                onSecondaryReady: () => secondaryReady = true
            );

            Assert.IsTrue(primaryReady,   "Primary callback should fire");
            Assert.IsTrue(secondaryReady, "Secondary callback should fire");
            Assert.AreEqual(1, _primary.InitializeCallCount);
            Assert.AreEqual(1, _secondary.InitializeCallCount);
        }

        [Test]
        public void Initialize_PrimaryOnly_SecondaryCallbackNeverFires()
        {
            var orc = new HybridAdOrchestrator(_primary);
            bool secondaryReady = false;

            orc.Initialize(
                onPrimaryReady:   () => { },
                onSecondaryReady: () => secondaryReady = true
            );

            Assert.IsFalse(secondaryReady,
                "Secondary callback should not fire when there is no secondary");
        }

        // ─── GetNetworkForFormat ───────────────────────────────────────────────

        [Test]
        public void GetNetworkForFormat_NoOverride_ReturnsPrimary()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            var net = orc.GetNetworkForFormat(AdFormatKey.Interstitial);

            Assert.AreSame(_primary, net);
        }

        [Test]
        public void GetNetworkForFormat_OverrideToPrimary_ReturnsPrimary()
        {
            var overrides = new Dictionary<string, string>
            {
                [AdFormatKey.Interstitial] = "admob"
            };
            var orc = new HybridAdOrchestrator(_primary, _secondary, overrides);

            var net = orc.GetNetworkForFormat(AdFormatKey.Interstitial);

            Assert.AreSame(_primary, net);
        }

        [Test]
        public void GetNetworkForFormat_OverrideToSecondary_ReturnsSecondary()
        {
            var overrides = new Dictionary<string, string>
            {
                [AdFormatKey.Interstitial] = "applovin"
            };
            var orc = new HybridAdOrchestrator(_primary, _secondary, overrides);

            var net = orc.GetNetworkForFormat(AdFormatKey.Interstitial);

            Assert.AreSame(_secondary, net);
        }

        [Test]
        public void GetNetworkForFormat_OverrideToSecondaryButNoSecondary_ReturnsPrimary()
        {
            // Override points to secondary but secondary is null → fall through to primary
            var overrides = new Dictionary<string, string>
            {
                [AdFormatKey.Interstitial] = "applovin"
            };
            var orc = new HybridAdOrchestrator(_primary, null, overrides);

            var net = orc.GetNetworkForFormat(AdFormatKey.Interstitial);

            Assert.AreSame(_primary, net);
        }

        [Test]
        public void GetNetworkForFormat_DifferentFormatsHaveDifferentOverrides()
        {
            var overrides = new Dictionary<string, string>
            {
                [AdFormatKey.Interstitial] = "applovin",   // secondary
                [AdFormatKey.Banner]       = "admob",      // primary
            };
            var orc = new HybridAdOrchestrator(_primary, _secondary, overrides);

            Assert.AreSame(_secondary, orc.GetNetworkForFormat(AdFormatKey.Interstitial));
            Assert.AreSame(_primary,   orc.GetNetworkForFormat(AdFormatKey.Banner));
        }

        [Test]
        public void GetNetworkForFormat_DynamicOptimization_PrefersBestScoringNetwork()
        {
            var tracker = new AdNetworkPerformanceTracker();
            // Give secondary a perfect fill rate
            for (int i = 0; i < 5; i++)
                tracker.RecordFillAttempt("applovin", AdFormatKey.Interstitial, filled: true);
            for (int i = 0; i < 5; i++)
                tracker.RecordRevenue("applovin", AdFormatKey.Interstitial, 0.01);

            // Primary has poor fill
            for (int i = 0; i < 5; i++)
                tracker.RecordFillAttempt("admob", AdFormatKey.Interstitial, filled: false);

            var orc = new HybridAdOrchestrator(
                _primary, _secondary,
                performanceTracker: tracker,
                dynamicOptimization: true
            );

            var net = orc.GetNetworkForFormat(AdFormatKey.Interstitial);

            Assert.AreSame(_secondary, net, "Dynamic optimization should prefer secondary with better score");
        }

        [Test]
        public void GetNetworkForFormat_DynamicOptimizationOff_IgnoresTracker()
        {
            var tracker = new AdNetworkPerformanceTracker();
            for (int i = 0; i < 5; i++)
            {
                tracker.RecordFillAttempt("applovin", AdFormatKey.Interstitial, true);
                tracker.RecordRevenue("applovin", AdFormatKey.Interstitial, 0.1);
            }

            var orc = new HybridAdOrchestrator(
                _primary, _secondary,
                performanceTracker: tracker,
                dynamicOptimization: false  // OFF
            );

            var net = orc.GetNetworkForFormat(AdFormatKey.Interstitial);
            Assert.AreSame(_primary, net, "Without dynamic optimization, always returns primary");
        }

        // ─── ShowWithFallback ─────────────────────────────────────────────────

        [Test]
        public void ShowWithFallback_PreferredReady_ShowsPreferred()
        {
            _primary.InterstitialReady   = true;
            _secondary.InterstitialReady = false;

            IAdNetwork shown = null;
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            orc.ShowWithFallback(
                AdFormatKey.Interstitial,
                showAction: net => shown = net,
                isReady:    net => net.IsInterstitialReady()
            );

            Assert.AreSame(_primary, shown);
        }

        [Test]
        public void ShowWithFallback_PreferredNotReady_FallsBackToSecondary()
        {
            _primary.InterstitialReady   = false;
            _secondary.InterstitialReady = true;

            IAdNetwork shown = null;
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            orc.ShowWithFallback(
                AdFormatKey.Interstitial,
                showAction: net => shown = net,
                isReady:    net => net.IsInterstitialReady()
            );

            Assert.AreSame(_secondary, shown);
        }

        [Test]
        public void ShowWithFallback_NeitherReady_FiresOnAdFailedDisplayed()
        {
            _primary.InterstitialReady   = false;
            _secondary.InterstitialReady = false;

            bool failed = false;
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            orc.OnAdFailedDisplayed += () => failed = true;

            orc.ShowWithFallback(
                AdFormatKey.Interstitial,
                showAction: _ => { },
                isReady:    net => net.IsInterstitialReady()
            );

            Assert.IsTrue(failed);
        }

        [Test]
        public void ShowWithFallback_NoIsReadyCheck_AlwaysShowsPreferred()
        {
            IAdNetwork shown = null;
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            orc.ShowWithFallback(
                AdFormatKey.Interstitial,
                showAction: net => shown = net
                // isReady not passed → null → always true
            );

            Assert.AreSame(_primary, shown);
        }

        [Test]
        public void ShowWithFallback_IsAdShowing_SetTrueAfterShow()
        {
            _primary.InterstitialReady = true;
            var orc = new HybridAdOrchestrator(_primary);

            Assert.IsFalse(orc.IsAdShowing, "Should start as not showing");

            orc.ShowWithFallback(AdFormatKey.Interstitial, showAction: _ => { });

            Assert.IsTrue(orc.IsAdShowing);
        }

        // ─── Event forwarding ─────────────────────────────────────────────────

        [Test]
        public void Events_AdDisplayed_ForwardedFromPrimary()
        {
            int count = 0;
            var orc   = new HybridAdOrchestrator(_primary, _secondary);
            orc.OnAdDisplayed += () => count++;

            _primary.TriggerAdDisplayed();
            Assert.AreEqual(1, count);
        }

        [Test]
        public void Events_AdDisplayed_ForwardedFromSecondary()
        {
            int count = 0;
            var orc   = new HybridAdOrchestrator(_primary, _secondary);
            orc.OnAdDisplayed += () => count++;

            _secondary.TriggerAdDisplayed();
            Assert.AreEqual(1, count);
        }

        [Test]
        public void Events_AdClosed_ResetsIsAdShowing()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            orc.ShowWithFallback(AdFormatKey.Interstitial, _ => { });

            Assert.IsTrue(orc.IsAdShowing);

            _primary.TriggerAdClosed();
            Assert.IsFalse(orc.IsAdShowing);
        }

        [Test]
        public void Events_AdFailedDisplayed_ResetsIsAdShowing()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            orc.ShowWithFallback(AdFormatKey.Interstitial, _ => { });

            _primary.TriggerAdFailedDisplayed();
            Assert.IsFalse(orc.IsAdShowing);
        }

        [Test]
        public void Events_UserEarnedReward_ForwardedFromAnyNetwork()
        {
            double earnedAmount = 0;
            string earnedType   = null;
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            orc.OnUserEarnedReward += (amt, type) => { earnedAmount = amt; earnedType = type; };

            _secondary.TriggerUserEarnedReward(100, "gold");
            Assert.AreEqual(100, earnedAmount);
            Assert.AreEqual("gold", earnedType);
        }

        [Test]
        public void Events_AdRevenuePaid_ForwardedFromBothNetworks()
        {
            int revenueCount = 0;
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            orc.OnAdRevenuePaid += (_, __, ___) => revenueCount++;

            _primary.TriggerAdRevenuePaid(0.01, "USD", null);
            _secondary.TriggerAdRevenuePaid(0.02, "USD", null);

            Assert.AreEqual(2, revenueCount);
        }

        [Test]
        public void Events_AdClicked_ForwardedFromBothNetworks()
        {
            int clicks = 0;
            var orc    = new HybridAdOrchestrator(_primary, _secondary);
            orc.OnAdClicked += () => clicks++;

            _primary.TriggerAdClicked();
            _secondary.TriggerAdClicked();

            Assert.AreEqual(2, clicks);
        }

        [Test]
        public void Events_AdImpressionRecorded_ForwardedFromBothNetworks()
        {
            int impressions = 0;
            var orc         = new HybridAdOrchestrator(_primary, _secondary);
            orc.OnAdImpressionRecorded += () => impressions++;

            _primary.TriggerAdImpressionRecorded();
            _secondary.TriggerAdImpressionRecorded();

            Assert.AreEqual(2, impressions);
        }

        // ─── CPM Floor integration ────────────────────────────────────────────

        private static CpmFloorManager MakeFloorManager(double soft, double hard, int minSamples = 0)
        {
            return new CpmFloorManager(new CpmFloorConfig
            {
                Enabled    = true,
                MinSamples = minSamples,
                Floors = new Dictionary<string, Dictionary<string, CpmFloorEntry>>
                {
                    [AdFormatKey.Interstitial] = new Dictionary<string, CpmFloorEntry>
                    {
                        ["t1"] = new CpmFloorEntry { Soft = soft, Hard = hard }
                    }
                },
                SegmentOverrides = new Dictionary<string, Dictionary<string, CpmFloorEntry>>()
            });
        }

        [Test]
        public void ShowWithFallback_PreferredHardBlocked_FallsBackToSecondary()
        {
            _primary.InterstitialReady   = true;
            _secondary.InterstitialReady = true;

            var tracker = new AdNetworkPerformanceTracker();
            // Primary has very low CPM → will hard-fail
            tracker.RecordRevenue("admob",    AdFormatKey.Interstitial, 0.01);
            // Secondary has high CPM → will pass
            tracker.RecordRevenue("applovin", AdFormatKey.Interstitial, 1.00);

            // hard=0.50 → primary(0.01) < hard → HardFail
            var floorMgr = MakeFloorManager(soft: 1.00, hard: 0.50, minSamples: 1);

            IAdNetwork shown = null;
            var orc = new HybridAdOrchestrator(
                _primary, _secondary,
                performanceTracker: tracker,
                cpmFloorManager:    floorMgr,
                segmentKey:         "t1_nonpayer_new_d0d1"
            );

            orc.ShowWithFallback(AdFormatKey.Interstitial, net => shown = net,
                isReady: net => net.IsInterstitialReady());

            Assert.AreSame(_secondary, shown, "Hard-blocked preferred should fall back to secondary");
        }

        [Test]
        public void ShowWithFallback_BothHardBlocked_FiresOnAdFailedDisplayed()
        {
            _primary.InterstitialReady   = true;
            _secondary.InterstitialReady = true;

            var tracker = new AdNetworkPerformanceTracker();
            tracker.RecordRevenue("admob",    AdFormatKey.Interstitial, 0.01);
            tracker.RecordRevenue("applovin", AdFormatKey.Interstitial, 0.01);

            var floorMgr = MakeFloorManager(soft: 1.00, hard: 0.50, minSamples: 1);

            bool failed = false;
            var orc = new HybridAdOrchestrator(
                _primary, _secondary,
                performanceTracker: tracker,
                cpmFloorManager:    floorMgr,
                segmentKey:         "t1_nonpayer_new_d0d1"
            );
            orc.OnAdFailedDisplayed += () => failed = true;

            orc.ShowWithFallback(AdFormatKey.Interstitial, _ => { },
                isReady: net => net.IsInterstitialReady());

            Assert.IsTrue(failed, "Both networks hard-blocked must fire OnAdFailedDisplayed");
        }

        [Test]
        public void ShowWithFallback_SoftFail_ProceedsWithPreferred()
        {
            _primary.InterstitialReady = true;

            var tracker = new AdNetworkPerformanceTracker();
            // CPM between hard and soft → SoftFail → proceed anyway
            tracker.RecordRevenue("admob", AdFormatKey.Interstitial, 0.30);

            // soft=0.50, hard=0.20 → 0.30 is SoftFail → still shows
            var floorMgr = MakeFloorManager(soft: 0.50, hard: 0.20, minSamples: 1);

            IAdNetwork shown = null;
            var orc = new HybridAdOrchestrator(
                _primary,
                performanceTracker: tracker,
                cpmFloorManager:    floorMgr,
                segmentKey:         "t1_nonpayer_new_d0d1"
            );

            orc.ShowWithFallback(AdFormatKey.Interstitial, net => shown = net,
                isReady: net => net.IsInterstitialReady());

            Assert.AreSame(_primary, shown, "SoftFail must not block the ad — just a warning");
        }

        [Test]
        public void ShowWithFallback_NoFloorManager_BehavesAsOriginal()
        {
            _primary.InterstitialReady   = false;
            _secondary.InterstitialReady = true;

            IAdNetwork shown = null;
            // No cpmFloorManager passed → original fallback logic unchanged
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            orc.ShowWithFallback(AdFormatKey.Interstitial, net => shown = net,
                isReady: net => net.IsInterstitialReady());

            Assert.AreSame(_secondary, shown, "Without floor manager, normal readiness fallback applies");
        }

        // ─── Real noctuagg.json hybrid init smoke test ────────────────────────
        //
        // Drives HybridAdOrchestrator.Initialize using the primary/secondary
        // mediation values straight out of the project's actual noctuagg.json
        // (Assets/StreamingAssets/noctuagg.json). Verifies that the init log
        // lines fire in the expected order and with the expected network names,
        // and that both Initialize callbacks are invoked.

        private static GlobalConfig LoadActualNoctuaggJson()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "noctuagg.json");
            Assume.That(File.Exists(path), $"noctuagg.json not found at: {path}");

            string json = File.ReadAllText(path, Encoding.UTF8);
            return JsonConvert.DeserializeObject<GlobalConfig>(json);
        }

        [Test]
        public void Initialize_FromRealNoctuaggJson_LogsHybridFlowAndFiresBothCallbacks()
        {
            var config = LoadActualNoctuaggJson();
            Assume.That(config?.IAA != null, "IAA section missing in noctuagg.json — skipping");
            Assume.That(!string.IsNullOrEmpty(config.IAA.Mediation),
                "iaa.mediation must be set for hybrid init");
            Assume.That(!string.IsNullOrEmpty(config.IAA.SecondaryMediation),
                "iaa.secondary_mediation must be set to exercise hybrid path");

            string primaryName   = config.IAA.Mediation;
            string secondaryName = config.IAA.SecondaryMediation;

            // Wire Serilog -> UnityLogSink so HybridAdOrchestrator's _log.Info()
            // calls reach Unity's Debug.unityLogger, where LogAssert can observe
            // them. Without this the global Serilog logger is a silent no-op.
            NoctuaLogger.Init(config);

            var primary   = new MockAdNetwork { NetworkName = primaryName };
            var secondary = new MockAdNetwork { NetworkName = secondaryName };
            var orc       = new HybridAdOrchestrator(primary, secondary);

            // NoctuaLogger format is "{ClassName}.{MemberName}: {message}".
            // LogAssert.Expect treats its second arg as a regex.
            string esc(string s) => Regex.Escape(s);
            LogAssert.Expect(LogType.Log,
                new Regex($"HybridAdOrchestrator\\.Initialize: Initializing orchestrator\\. Primary: {esc(primaryName)}, Secondary: {esc(secondaryName)}"));
            LogAssert.Expect(LogType.Log,
                new Regex($"HybridAdOrchestrator\\.Initialize: Primary network \\({esc(primaryName)}\\) initialized\\."));
            LogAssert.Expect(LogType.Log,
                new Regex($"HybridAdOrchestrator\\.Initialize: Primary ready — starting secondary network init: {esc(secondaryName)}"));
            LogAssert.Expect(LogType.Log,
                new Regex($"HybridAdOrchestrator\\.Initialize: Secondary network \\({esc(secondaryName)}\\) initialized\\."));

            bool primaryReady   = false;
            bool secondaryReady = false;
            orc.Initialize(
                onPrimaryReady:   () => primaryReady = true,
                onSecondaryReady: () => secondaryReady = true
            );

            Assert.IsTrue(orc.IsHybridMode,
                $"Hybrid mode must be active when both '{primaryName}' and '{secondaryName}' are configured");
            Assert.AreEqual(primaryName,   orc.Primary.NetworkName);
            Assert.AreEqual(secondaryName, orc.Secondary.NetworkName);
            Assert.IsTrue(primaryReady,    "Primary onReady callback must fire");
            Assert.IsTrue(secondaryReady,  "Secondary onReady callback must fire after primary");
            Assert.AreEqual(1, primary.InitializeCallCount,
                "Primary network must be initialized exactly once");
            Assert.AreEqual(1, secondary.InitializeCallCount,
                "Secondary network must be initialized exactly once");
        }
    }

    /// <summary>
    /// Edge-case tests for <see cref="HybridAdOrchestrator"/>.
    /// Focuses on uncovered branches: primary-only GetNetworkForFormat paths for all formats,
    /// unknown override targets, HasFormatOverride, dynamic optimization with null tracker,
    /// and IsHybridMode with various configurations.
    /// </summary>
    [TestFixture]
    public class HybridAdOrchestratorEdgeCaseTest
    {
        private MockAdNetwork _primary;
        private MockAdNetwork _secondary;

        [SetUp]
        public void SetUp()
        {
            _primary   = new MockAdNetwork { NetworkName = "admob" };
            _secondary = new MockAdNetwork { NetworkName = "applovin" };

            // Clear AdNetworkPerformanceTracker PlayerPrefs to prevent cross-test pollution
            foreach (var network in new[] { "admob", "applovin" })
            foreach (var format  in new[] { AdFormatKey.Interstitial, AdFormatKey.Rewarded,
                                             AdFormatKey.Banner,       AdFormatKey.AppOpen })
            {
                PlayerPrefs.DeleteKey($"NoctuaAdPerf_fill_{network}_{format}");
                PlayerPrefs.DeleteKey($"NoctuaAdPerf_rev_{network}_{format}");
            }
            PlayerPrefs.Save();

            // Suppress warning logs for tests that deliberately use unknown override targets
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
        }

        // ─── GetNetworkForFormat — primary-only (no secondary) ────────────────

        [Test]
        public void GetNetworkForFormat_PrimaryOnlyNoOverride_ReturnsPrimary()
        {
            var orc = new HybridAdOrchestrator(_primary);
            // No secondary, no overrides — must always return primary
            var net = orc.GetNetworkForFormat(AdFormatKey.Interstitial);
            Assert.AreSame(_primary, net);
        }

        [Test]
        public void GetNetworkForFormat_Banner_UsesPrimaryNetwork()
        {
            // Banner format with no override → primary
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            var net = orc.GetNetworkForFormat(AdFormatKey.Banner);
            Assert.AreSame(_primary, net,
                "Banner format with no override must route to primary");
        }

        [Test]
        public void GetNetworkForFormat_Interstitial_UsesPrimaryByDefault()
        {
            // Default routing (no overrides, no dynamic opt) → primary for interstitial
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            var net = orc.GetNetworkForFormat(AdFormatKey.Interstitial);
            Assert.AreSame(_primary, net,
                "Interstitial with no override must route to primary by default");
        }

        [Test]
        public void GetNetworkForFormat_AppOpen_UsesPrimaryNetwork()
        {
            // AppOpen format with no override → primary
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            var net = orc.GetNetworkForFormat(AdFormatKey.AppOpen);
            Assert.AreSame(_primary, net,
                "AppOpen format with no override must route to primary");
        }

        // ─── GetNetworkForFormat — override to unknown network ────────────────

        [Test]
        public void GetNetworkForFormat_OverrideToUnknownNetwork_FallsBackToPrimary()
        {
            // Override points to a network name that matches neither primary nor secondary
            var overrides = new Dictionary<string, string>
            {
                [AdFormatKey.Interstitial] = "unknown_network"
            };
            var orc = new HybridAdOrchestrator(_primary, _secondary, overrides);

            // unknown_network != secondary.NetworkName ("applovin") AND != primary.NetworkName ("admob")
            // → falls back to primary with a warning
            var net = orc.GetNetworkForFormat(AdFormatKey.Interstitial);

            Assert.AreSame(_primary, net,
                "Override targeting an unknown network must fall back to primary");
        }

        [Test]
        public void GetNetworkForFormat_OverrideForOtherFormat_NoOverrideForQueried_ReturnsPrimary()
        {
            // Override is set for Banner only; querying Interstitial (no override) → primary
            var overrides = new Dictionary<string, string>
            {
                [AdFormatKey.Banner] = "applovin"
            };
            var orc = new HybridAdOrchestrator(_primary, _secondary, overrides);

            var net = orc.GetNetworkForFormat(AdFormatKey.Interstitial);

            Assert.AreSame(_primary, net,
                "Format without an override must always route to primary");
        }

        [Test]
        public void GetNetworkForFormat_WithOverride_UsesOverrideNetwork()
        {
            // Explicit override to secondary for Rewarded; no override for Banner → primary
            var overrides = new Dictionary<string, string>
            {
                [AdFormatKey.Rewarded] = "applovin"
            };
            var orc = new HybridAdOrchestrator(_primary, _secondary, overrides);

            Assert.AreSame(_secondary, orc.GetNetworkForFormat(AdFormatKey.Rewarded),
                "Rewarded with override=applovin must return secondary");
            Assert.AreSame(_primary, orc.GetNetworkForFormat(AdFormatKey.Banner),
                "Banner without override must still return primary");
        }

        [Test]
        public void GetNetworkForFormat_OverrideNetworkNull_SecondaryNull_FallsBackToPrimary()
        {
            // Override targets secondary name but secondary is not provided
            var overrides = new Dictionary<string, string>
            {
                [AdFormatKey.Rewarded] = "applovin"  // secondary name, but secondary is null
            };
            var orc = new HybridAdOrchestrator(_primary, null, overrides);

            var net = orc.GetNetworkForFormat(AdFormatKey.Rewarded);

            Assert.AreSame(_primary, net,
                "Override targeting secondary name when secondary is null must fall back to primary");
        }

        // ─── GetNetworkForFormat — dynamic optimization with no tracker ────────

        [Test]
        public void GetNetworkForFormat_DynamicOptimizationOn_NullTracker_ReturnsPrimary()
        {
            // dynamicOptimization=true but performanceTracker is null → guard in source → primary
            var orc = new HybridAdOrchestrator(
                _primary, _secondary,
                performanceTracker: null,
                dynamicOptimization: true
            );

            var net = orc.GetNetworkForFormat(AdFormatKey.Interstitial);

            Assert.AreSame(_primary, net,
                "Dynamic optimization with null tracker must default to primary");
        }

        // ─── HasFormatOverride ────────────────────────────────────────────────

        [Test]
        public void HasFormatOverride_WithOverrideForFormat_ReturnsTrue()
        {
            var overrides = new Dictionary<string, string>
            {
                [AdFormatKey.Interstitial] = "admob"
            };
            var orc = new HybridAdOrchestrator(_primary, _secondary, overrides);

            Assert.IsTrue(orc.HasFormatOverride(AdFormatKey.Interstitial),
                "HasFormatOverride must return true when an override is registered for the format");
        }

        [Test]
        public void HasFormatOverride_WithoutOverrideForFormat_ReturnsFalse()
        {
            var overrides = new Dictionary<string, string>
            {
                [AdFormatKey.Banner] = "admob"
            };
            var orc = new HybridAdOrchestrator(_primary, _secondary, overrides);

            Assert.IsFalse(orc.HasFormatOverride(AdFormatKey.Interstitial),
                "HasFormatOverride must return false when no override is registered for the queried format");
        }

        [Test]
        public void HasFormatOverride_NoOverridesAtAll_ReturnsFalse()
        {
            // No overrides dictionary passed → empty internal dict
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            Assert.IsFalse(orc.HasFormatOverride(AdFormatKey.Interstitial));
            Assert.IsFalse(orc.HasFormatOverride(AdFormatKey.Banner));
            Assert.IsFalse(orc.HasFormatOverride(AdFormatKey.Rewarded));
            Assert.IsFalse(orc.HasFormatOverride(AdFormatKey.AppOpen));
        }

        // ─── IsHybridMode with various configurations ─────────────────────────

        [Test]
        public void IsHybridMode_WithBothNetworks_ReturnsTrue()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            Assert.IsTrue(orc.IsHybridMode,
                "IsHybridMode must be true when both primary and secondary networks are configured");
        }

        [Test]
        public void IsHybridMode_WithOneNetwork_ReturnsFalse()
        {
            var orc = new HybridAdOrchestrator(_primary);
            Assert.IsFalse(orc.IsHybridMode,
                "IsHybridMode must be false when only the primary network is configured");
        }

        // ─── GetNetworkForFormat — WhenPrimaryAndSecondaryBothNull branch ──────
        //
        // Constructor rejects null primary (ArgumentNullException), so a truly
        // null primary is impossible at runtime. The closest testable analogue is
        // a single-network orchestrator where GetNetworkForFormat always returns
        // primary regardless of the format key.

        [Test]
        public void GetNetworkForFormat_WhenNoSecondary_AllFormatsReturnPrimary()
        {
            var orc = new HybridAdOrchestrator(_primary); // no secondary

            Assert.AreSame(_primary, orc.GetNetworkForFormat(AdFormatKey.Interstitial),
                "Interstitial with no secondary must return primary");
            Assert.AreSame(_primary, orc.GetNetworkForFormat(AdFormatKey.Rewarded),
                "Rewarded with no secondary must return primary");
            Assert.AreSame(_primary, orc.GetNetworkForFormat(AdFormatKey.Banner),
                "Banner with no secondary must return primary");
            Assert.AreSame(_primary, orc.GetNetworkForFormat(AdFormatKey.AppOpen),
                "AppOpen with no secondary must return primary");
        }

        // ─── SetPreferredNetwork via dynamic optimization ─────────────────────

        [Test]
        public void SetPreferredNetwork_ThenGetNetworkForFormat_ReturnsPreferred()
        {
            // Seed the performance tracker so secondary scores higher than primary
            var tracker = new AdNetworkPerformanceTracker();
            // Give secondary a perfect fill rate and good revenue
            for (int i = 0; i < 5; i++)
            {
                tracker.RecordFillAttempt("applovin", AdFormatKey.Rewarded, filled: true);
                tracker.RecordRevenue("applovin", AdFormatKey.Rewarded, 0.05);
            }
            // Primary has zero fills
            for (int i = 0; i < 5; i++)
                tracker.RecordFillAttempt("admob", AdFormatKey.Rewarded, filled: false);

            var orc = new HybridAdOrchestrator(
                _primary, _secondary,
                performanceTracker: tracker,
                dynamicOptimization: true
            );

            var net = orc.GetNetworkForFormat(AdFormatKey.Rewarded);

            Assert.AreSame(_secondary, net,
                "After seeding tracker with better secondary performance, dynamic opt must prefer secondary");
        }
    }
}
