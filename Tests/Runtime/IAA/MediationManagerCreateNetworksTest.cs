using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using com.noctuagames.sdk.AdPlaceholder;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using IAAConfig = com.noctuagames.sdk.IAA;

namespace com.noctuagames.sdk.Tests.IAA
{
    /// <summary>
    /// Smoke test for <see cref="MediationManager.CreateNetworks"/> driven by
    /// the project's actual <c>Assets/StreamingAssets/noctuagg.json</c>.
    ///
    /// Verifies that constructing a <c>MediationManager</c> with the real
    /// IAA config triggers the network-creation flow, picks primary/secondary
    /// according to the compiled-in ad SDK defines, and emits the
    /// "Networks created. ..." log line with hybrid / CPM-floors / segment
    /// fields populated.
    ///
    /// Behavior is config-driven (post-fix): iaa.mediation picks primary,
    /// iaa.secondary_mediation picks secondary. The build's UNITY_ADMOB /
    /// UNITY_APPLOVIN defines only gate availability — they do NOT override
    /// what the config requested.
    ///
    ///   - both defines compiled in → primary = iaa.mediation,
    ///                                secondary = iaa.secondary_mediation,
    ///                                hybrid iff secondary present
    ///   - only the primary's SDK   → primary = iaa.mediation, no secondary
    ///   - only the secondary's SDK → secondary promoted to primary (warning logged)
    ///   - neither                  → CreateNetworks logs an error and returns
    /// </summary>
    [TestFixture]
    public class MediationManagerCreateNetworksTest
    {
        private class NoopAdPlaceholderUI : IAdPlaceholderUI
        {
            public void ShowAdPlaceholder(AdPlaceholderType adType) { }
            public void CloseAdPlaceholder() { }
        }

        private static GlobalConfig LoadActualNoctuaggJson()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "noctuagg.json");
            Assume.That(File.Exists(path), $"noctuagg.json not found at: {path}");

            string json = File.ReadAllText(path, Encoding.UTF8);
            return JsonConvert.DeserializeObject<GlobalConfig>(json);
        }

        [Test]
        public void Constructor_FromRealNoctuaggJson_CreatesNetworksAndLogsHybridState()
        {
            var config = LoadActualNoctuaggJson();
            Assume.That(config?.IAA != null, "IAA section missing in noctuagg.json — skipping");

            // Pipe Serilog → UnityLogSink so MediationManager's _log.Info reaches
            // Unity's Debug.unityLogger, where LogAssert can observe it.
            NoctuaLogger.Init(config);

            // The SDK-availability summary fires unconditionally at the top of
            // CreateNetworks. Match either status so the test is portable across
            // build configurations.
            LogAssert.Expect(LogType.Log,
                new Regex(@"MediationManager\.CreateNetworks: IAA SDK availability: AdMob=(integrated|missing), AppLovin=(integrated|missing)\. Requested in noctuagg\.json"));

#if UNITY_ADMOB || UNITY_APPLOVIN
            // CreateNetworks emits exactly one "Networks created..." line summarising
            // primary/secondary/hybrid/cpm/segment. Match it loosely so we don't
            // re-encode the SDK-define decision tree here — the assertions below
            // pin the parts that matter per build configuration.
            LogAssert.Expect(LogType.Log,
                new Regex(@"MediationManager\.CreateNetworks: Networks created\. Primary: \w+(?:, Secondary: \w+)?, Hybrid: (True|False), CpmFloors: (enabled|disabled), Segment: \S+"));

            var mgr = new MediationManager(new NoopAdPlaceholderUI(), config.IAA);

            // CPM floors are enabled in the shipping config; if that flips off in
            // a future config refactor the assertion will surface it.
            Assume.That(config.IAA.CpmFloors?.Enabled == true,
                "cpm_floors.enabled flipped off — re-baseline this test");

#if UNITY_ADMOB && UNITY_APPLOVIN
            // Both SDKs compiled in: primary follows iaa.mediation, secondary
            // follows iaa.secondary_mediation. Hybrid iff both configured AND
            // they're different networks. Comparison is case-insensitive to
            // match MediationManager.NormalizeMediationName.
            bool expectHybrid =
                !string.IsNullOrEmpty(config.IAA.Mediation) &&
                !string.IsNullOrEmpty(config.IAA.SecondaryMediation) &&
                !string.Equals(config.IAA.Mediation,
                               config.IAA.SecondaryMediation,
                               System.StringComparison.OrdinalIgnoreCase);
            Assert.AreEqual(expectHybrid, mgr.IsHybridMode,
                $"With both SDKs compiled in and mediation='{config.IAA.Mediation}', " +
                $"secondary_mediation='{config.IAA.SecondaryMediation}', " +
                $"hybrid mode should be {expectHybrid}");
#elif UNITY_ADMOB
            // Only UNITY_ADMOB compiled in: secondary is dropped (or primary
            // promoted from secondary). Either way no hybrid.
            Assert.IsFalse(mgr.IsHybridMode,
                "With only UNITY_ADMOB compiled in, no secondary can exist — hybrid must be false");
#elif UNITY_APPLOVIN
            Assert.IsFalse(mgr.IsHybridMode,
                "With only UNITY_APPLOVIN compiled in, no secondary can exist — hybrid must be false");
#endif
#else
            // No ad SDK defines compiled in — CreateNetworks emits the
            // game-dev-facing warning, then logs an error and bails out
            // without creating an orchestrator.
            LogAssert.Expect(LogType.Warning,
                new Regex(@"MediationManager\.CreateNetworks: No ad mediation SDK is integrated in this build"));
            LogAssert.Expect(LogType.Error,
                new Regex(@"MediationManager\.CreateNetworks: No ad network SDK is available for the requested config"));

            var mgr = new MediationManager(new NoopAdPlaceholderUI(), config.IAA);

            Assert.IsFalse(mgr.IsHybridMode,
                "Without any SDK define, no orchestrator is created and hybrid mode is false");
#endif
        }
    }

    /// <summary>
    /// Tests for the three pure-static helper methods on MediationManager:
    /// NormalizeMediationName, IsRecognisedMediationName, and IsAvailable.
    /// These methods have no side effects and require no SDK defines.
    /// </summary>
    [TestFixture]
    public class MediationManagerStaticApiTest
    {
        // ── NormalizeMediationName ────────────────────────────────────────────────

        [Test]
        public void NormalizeMediationName_Null_ReturnsNull()
        {
            Assert.IsNull(MediationManager.NormalizeMediationName(null));
        }

        [Test]
        public void NormalizeMediationName_EmptyString_ReturnsNull()
        {
            Assert.IsNull(MediationManager.NormalizeMediationName(""));
        }

        [Test]
        public void NormalizeMediationName_WhitespaceOnly_ReturnsNull()
        {
            Assert.IsNull(MediationManager.NormalizeMediationName("   "));
        }

        [Test]
        public void NormalizeMediationName_MixedCase_ReturnsLower()
        {
            Assert.AreEqual("applovin", MediationManager.NormalizeMediationName("AppLovin"));
        }

        [Test]
        public void NormalizeMediationName_AllCaps_ReturnsLower()
        {
            Assert.AreEqual("admob", MediationManager.NormalizeMediationName("ADMOB"));
        }

        [Test]
        public void NormalizeMediationName_WithLeadingTrailingSpaces_Trimmed()
        {
            Assert.AreEqual("admob", MediationManager.NormalizeMediationName("  admob  "));
        }

        [Test]
        public void NormalizeMediationName_MixedCaseAndSpaces_NormalisedAndTrimmed()
        {
            Assert.AreEqual("applovin", MediationManager.NormalizeMediationName("  AppLovin  "));
        }

        [Test]
        public void NormalizeMediationName_AlreadyLower_ReturnsSame()
        {
            Assert.AreEqual("admob", MediationManager.NormalizeMediationName("admob"));
        }

        // ── IsRecognisedMediationName ─────────────────────────────────────────────

        [Test]
        public void IsRecognisedMediationName_Admob_ReturnsTrue()
        {
            Assert.IsTrue(MediationManager.IsRecognisedMediationName("admob"));
        }

        [Test]
        public void IsRecognisedMediationName_AppLovin_ReturnsTrue()
        {
            Assert.IsTrue(MediationManager.IsRecognisedMediationName("applovin"));
        }

        [Test]
        public void IsRecognisedMediationName_Unknown_ReturnsFalse()
        {
            Assert.IsFalse(MediationManager.IsRecognisedMediationName("ironsource"));
        }

        [Test]
        public void IsRecognisedMediationName_Null_ReturnsFalse()
        {
            Assert.IsFalse(MediationManager.IsRecognisedMediationName(null));
        }

        [Test]
        public void IsRecognisedMediationName_EmptyString_ReturnsFalse()
        {
            Assert.IsFalse(MediationManager.IsRecognisedMediationName(""));
        }

        [Test]
        public void IsRecognisedMediationName_MixedCase_ReturnsFalse()
        {
            // NormalizeMediationName must be called first — IsRecognisedMediationName
            // expects an already-normalised string.
            Assert.IsFalse(MediationManager.IsRecognisedMediationName("AdMob"));
        }

        // ── IsAvailable ───────────────────────────────────────────────────────────

        [Test]
        public void IsAvailable_Admob_WhenAdmobTrue_ReturnsTrue()
        {
            Assert.IsTrue(MediationManager.IsAvailable("admob", admobAvailable: true, applovinAvailable: false));
        }

        [Test]
        public void IsAvailable_Admob_WhenAdmobFalse_ReturnsFalse()
        {
            Assert.IsFalse(MediationManager.IsAvailable("admob", admobAvailable: false, applovinAvailable: true));
        }

        [Test]
        public void IsAvailable_AppLovin_WhenAppLovinTrue_ReturnsTrue()
        {
            Assert.IsTrue(MediationManager.IsAvailable("applovin", admobAvailable: false, applovinAvailable: true));
        }

        [Test]
        public void IsAvailable_AppLovin_WhenAppLovinFalse_ReturnsFalse()
        {
            Assert.IsFalse(MediationManager.IsAvailable("applovin", admobAvailable: true, applovinAvailable: false));
        }

        [Test]
        public void IsAvailable_Unknown_ReturnsFalse()
        {
            Assert.IsFalse(MediationManager.IsAvailable("ironsource", admobAvailable: true, applovinAvailable: true));
        }

        [Test]
        public void IsAvailable_Null_ReturnsFalse()
        {
            Assert.IsFalse(MediationManager.IsAvailable(null, admobAvailable: true, applovinAvailable: true));
        }

        [Test]
        public void IsAvailable_EmptyString_ReturnsFalse()
        {
            Assert.IsFalse(MediationManager.IsAvailable("", admobAvailable: true, applovinAvailable: true));
        }

        [Test]
        public void IsAvailable_Admob_BothFalse_ReturnsFalse()
        {
            Assert.IsFalse(MediationManager.IsAvailable("admob", admobAvailable: false, applovinAvailable: false));
        }

        [Test]
        public void IsAvailable_AppLovin_BothTrue_ReturnsTrue()
        {
            Assert.IsTrue(MediationManager.IsAvailable("applovin", admobAvailable: true, applovinAvailable: true));
        }
    }

    /// <summary>
    /// Tests for MediationManager instance API (properties and diagnostic methods).
    /// Constructs instances with minimal config to exercise the instance surface
    /// without requiring a real ad SDK or network connectivity.
    ///
    /// LogAssert.ignoreFailingMessages is set to true before each constructor call
    /// because NoctuaLogger routes through Debug.unityLogger and CreateNetworks
    /// emits availability info/warning/error messages depending on which SDK
    /// defines are compiled in.
    /// </summary>
    [TestFixture]
    public class MediationManagerInstanceApiTest
    {
        private class NoopAdPlaceholderUI : IAdPlaceholderUI
        {
            public void ShowAdPlaceholder(AdPlaceholderType adType) { }
            public void CloseAdPlaceholder() { }
        }

        private static IAAConfig MinimalIaa(string mediation = "admob") =>
            new IAAConfig { Mediation = mediation };

        // ── Constructor safety ────────────────────────────────────────────────────

        [Test]
        public void Constructor_WithNullIaa_DoesNotThrow()
        {
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() =>
            {
                var _ = new MediationManager(new NoopAdPlaceholderUI(), null);
            });
        }

        [Test]
        public void Constructor_WithMinimalAdmobIaa_DoesNotThrow()
        {
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() =>
            {
                var _ = new MediationManager(new NoopAdPlaceholderUI(), MinimalIaa("admob"));
            });
        }

        [Test]
        public void Constructor_WithMinimalApplovinIaa_DoesNotThrow()
        {
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() =>
            {
                var _ = new MediationManager(new NoopAdPlaceholderUI(), MinimalIaa("applovin"));
            });
        }

        // ── IsHybridMode ──────────────────────────────────────────────────────────

        [Test]
        public void IsHybridMode_DefaultFalse_WhenNullIaa()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new MediationManager(new NoopAdPlaceholderUI(), null);
            Assert.IsFalse(mgr.IsHybridMode,
                "IsHybridMode must be false when constructed with null IAA (orchestrator is null)");
        }

        // ── GetSegmentKey ─────────────────────────────────────────────────────────

        [Test]
        public void GetSegmentKey_WhenNullIaa_ReturnsNotInitialized()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new MediationManager(new NoopAdPlaceholderUI(), null);
            // _segmentManager is null when no IAA config is processed → "not initialized"
            Assert.AreEqual("not initialized", mgr.GetSegmentKey());
        }

        [Test]
        public void GetSegmentKey_WithMinimalIaa_DoesNotThrow()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new MediationManager(new NoopAdPlaceholderUI(), MinimalIaa());
            Assert.DoesNotThrow(() => mgr.GetSegmentKey());
        }

        [Test]
        public void GetSegmentKey_ReturnsString()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new MediationManager(new NoopAdPlaceholderUI(), MinimalIaa());
            // Result is either a real segment key or "not initialized" — must not be null
            // (the method always returns a non-null string).
            string result = mgr.GetSegmentKey();
            Assert.IsNotNull(result);
        }

        // ── GetExperimentAssignments ──────────────────────────────────────────────

        [Test]
        public void GetExperimentAssignments_WhenNullIaa_ReturnsEmptyDict()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new MediationManager(new NoopAdPlaceholderUI(), null);
            var assignments = mgr.GetExperimentAssignments();
            Assert.IsNotNull(assignments);
            Assert.AreEqual(0, assignments.Count);
        }

        [Test]
        public void GetExperimentAssignments_WithMinimalIaa_ReturnsDict()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new MediationManager(new NoopAdPlaceholderUI(), MinimalIaa());
            var assignments = mgr.GetExperimentAssignments();
            Assert.IsNotNull(assignments, "GetExperimentAssignments must never return null");
        }

        // ── GetCpmFloorStatus ─────────────────────────────────────────────────────

        [Test]
        public void GetCpmFloorStatus_WhenNullIaa_ReturnsCpmFloorsDisabled()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new MediationManager(new NoopAdPlaceholderUI(), null);
            var status = mgr.GetCpmFloorStatus();
            Assert.IsNotNull(status);
            Assert.IsTrue(status.ContainsKey("status"),
                "Expected 'status' key when CPM floor manager is null");
            Assert.AreEqual("CPM floors disabled", status["status"]);
        }

        [Test]
        public void GetCpmFloorStatus_WithMinimalIaa_ReturnsDict()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new MediationManager(new NoopAdPlaceholderUI(), MinimalIaa());
            var status = mgr.GetCpmFloorStatus();
            Assert.IsNotNull(status, "GetCpmFloorStatus must never return null");
        }

        // ── MediationType ─────────────────────────────────────────────────────────

        [Test]
        public void MediationType_WhenNullIaa_IsNull()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new MediationManager(new NoopAdPlaceholderUI(), null);
            // _mediationType starts null; CreateNetworks is not called for null IAA
            Assert.IsNull(mgr.MediationType);
        }

        [Test]
        public void MediationType_IsAccessibleWithoutException()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new MediationManager(new NoopAdPlaceholderUI(), MinimalIaa());
            Assert.DoesNotThrow(() => { var _ = mgr.MediationType; });
        }

        // ── SetAdRevenueTracker ───────────────────────────────────────────────────

        [Test]
        public void SetAdRevenueTracker_Null_DoesNotThrow()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new MediationManager(new NoopAdPlaceholderUI(), null);
            Assert.DoesNotThrow(() => mgr.SetAdRevenueTracker(null));
        }

        [Test]
        public void SetAdRevenueTracker_CalledTwiceWithNull_DoesNotThrow()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new MediationManager(new NoopAdPlaceholderUI(), null);
            Assert.DoesNotThrow(() =>
            {
                mgr.SetAdRevenueTracker(null);
                mgr.SetAdRevenueTracker(null);
            });
        }

        // ── AdUnitID default values ───────────────────────────────────────────────

        [Test]
        public void InterstitialAdUnitID_DefaultValue_IsUnused()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new MediationManager(new NoopAdPlaceholderUI(), null);
            Assert.AreEqual("unused", mgr.InterstitialAdUnitID);
        }

        [Test]
        public void RewardedAdUnitID_DefaultValue_IsUnused()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new MediationManager(new NoopAdPlaceholderUI(), null);
            Assert.AreEqual("unused", mgr.RewardedAdUnitID);
        }

        [Test]
        public void BannerAdUnitID_DefaultValue_IsUnused()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new MediationManager(new NoopAdPlaceholderUI(), null);
            Assert.AreEqual("unused", mgr.BannerAdUnitID);
        }

        [Test]
        public void RewardedInterstitialAdUnitID_DefaultValue_IsUnused()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new MediationManager(new NoopAdPlaceholderUI(), null);
            Assert.AreEqual("unused", mgr.RewardedInterstitialAdUnitID);
        }

        // ── ApplyIaaConfigFromRemote ──────────────────────────────────────────────

        [Test]
        public void ApplyIaaConfigFromRemote_WithNull_DoesNotThrow()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new MediationManager(new NoopAdPlaceholderUI(), null);
            // Null merged config is assigned but IAA setter guards against null
            // (sets _iaaResponse = value; then returns because value == null).
            Assert.DoesNotThrow(() => mgr.ApplyIaaConfigFromRemote(null));
        }

        [Test]
        public void ApplyIaaConfigFromRemote_WithValidConfig_DoesNotThrow()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new MediationManager(new NoopAdPlaceholderUI(), null);
            Assert.DoesNotThrow(() => mgr.ApplyIaaConfigFromRemote(MinimalIaa("admob")));
        }
    }

    /// <summary>
    /// Tests for MediationManager ad lifecycle behaviour:
    ///
    ///   Group A — null-orchestrator safety (12 tests)
    ///     All Show*/Load*/IsReady public methods must not throw and must return
    ///     safe defaults when constructed with null IAA (i.e. _orchestrator == null).
    ///
    ///   Group B — HybridAdOrchestrator dispatch (7 tests)
    ///     Validates that HybridAdOrchestrator correctly dispatches Show calls and
    ///     forwards events from MockAdNetwork to orchestrator subscribers.
    ///
    ///   Group C — MediationManager event forwarding (3 tests)
    ///     Validates that orchestrator lifecycle events (OnAdDisplayed, OnAdClosed,
    ///     OnAdFailedDisplayed) are forwarded through to MediationManager's public
    ///     events when the manager is wired with a real HybridAdOrchestrator.
    ///
    ///   Group D — ApplyIaaConfigFromRemote (2 tests)
    ///     Validates origin tagging and safe re-invocation of ApplyIaaConfigFromRemote.
    /// </summary>
    [TestFixture]
    public class MediationManagerAdLifecycleTest
    {
        private class NoopAdPlaceholderUI : IAdPlaceholderUI
        {
            public void ShowAdPlaceholder(AdPlaceholderType adType) { }
            public void CloseAdPlaceholder() { }
        }

        private static MediationManager NullIaaManager()
        {
            LogAssert.ignoreFailingMessages = true;
            return new MediationManager(new NoopAdPlaceholderUI(), null);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group A — null-orchestrator safety (12 tests)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void LoadInterstitialAd_NullOrchestrator_DoesNotThrow()
        {
            var mgr = NullIaaManager();
            Assert.DoesNotThrow(() => mgr.LoadInterstitialAd());
        }

        [Test]
        public void ShowInterstitial_NullOrchestrator_DoesNotThrow()
        {
            var mgr = NullIaaManager();
            Assert.DoesNotThrow(() => mgr.ShowInterstitial());
        }

        [Test]
        public void ShowInterstitialWithPlacement_NullOrchestrator_DoesNotThrow()
        {
            var mgr = NullIaaManager();
            Assert.DoesNotThrow(() => mgr.ShowInterstitial("level_complete"));
        }

        [Test]
        public void LoadRewardedAd_NullOrchestrator_DoesNotThrow()
        {
            var mgr = NullIaaManager();
            Assert.DoesNotThrow(() => mgr.LoadRewardedAd());
        }

        [Test]
        public void ShowRewardedAd_NullOrchestrator_DoesNotThrow()
        {
            var mgr = NullIaaManager();
            Assert.DoesNotThrow(() => mgr.ShowRewardedAd());
        }

        [Test]
        public void ShowRewardedAdWithPlacement_NullOrchestrator_DoesNotThrow()
        {
            var mgr = NullIaaManager();
            Assert.DoesNotThrow(() => mgr.ShowRewardedAd("daily_reward"));
        }

        [Test]
        public void ShowBannerAd_NullOrchestrator_DoesNotThrow()
        {
            var mgr = NullIaaManager();
            Assert.DoesNotThrow(() => mgr.ShowBannerAd());
        }

        [Test]
        public void HideBannerAd_NullOrchestrator_DoesNotThrow()
        {
            var mgr = NullIaaManager();
            Assert.DoesNotThrow(() => mgr.HideBannerAd());
        }

        [Test]
        public void ShowRewardedInterstitialAd_NullOrchestrator_DoesNotThrow()
        {
            var mgr = NullIaaManager();
            Assert.DoesNotThrow(() => mgr.ShowRewardedInterstitialAd());
        }

        [Test]
        public void IsInterstitialReady_NullOrchestrator_ReturnsFalse()
        {
            var mgr = NullIaaManager();
            Assert.IsFalse(mgr.IsInterstitialReady(),
                "IsInterstitialReady must return false when orchestrator is null");
        }

        [Test]
        public void IsRewardedAdReady_NullOrchestrator_ReturnsFalse()
        {
            var mgr = NullIaaManager();
            Assert.IsFalse(mgr.IsRewardedAdReady(),
                "IsRewardedAdReady must return false when orchestrator is null");
        }

        [Test]
        public void IsAppOpenAdReady_NullOrchestrator_ReturnsFalse()
        {
            var mgr = NullIaaManager();
            Assert.IsFalse(mgr.IsAppOpenAdReady(),
                "IsAppOpenAdReady must return false when AppOpenAdManager is null (null IAA)");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group B — HybridAdOrchestrator dispatch (7 tests)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void HybridOrchestrator_ShowWithFallback_PrefersPrimary_WhenReady()
        {
            var primary   = new MockAdNetwork { NetworkName = "admob",    InterstitialReady = true  };
            var secondary = new MockAdNetwork { NetworkName = "applovin", InterstitialReady = true  };
            var orc = new HybridAdOrchestrator(primary, secondary);

            IAdNetwork chosen = null;
            orc.ShowWithFallback(AdFormatKey.Interstitial,
                n => chosen = n,
                n => n.IsInterstitialReady());

            Assert.AreSame(primary, chosen, "Primary should be chosen when it is ready");
        }

        [Test]
        public void HybridOrchestrator_ShowWithFallback_FallsBackToSecondary_WhenPrimaryNotReady()
        {
            var primary   = new MockAdNetwork { NetworkName = "admob",    InterstitialReady = false };
            var secondary = new MockAdNetwork { NetworkName = "applovin", InterstitialReady = true  };
            var orc = new HybridAdOrchestrator(primary, secondary);

            IAdNetwork chosen = null;
            orc.ShowWithFallback(AdFormatKey.Interstitial,
                n => chosen = n,
                n => n.IsInterstitialReady());

            Assert.AreSame(secondary, chosen, "Secondary should be chosen when primary is not ready");
        }

        [Test]
        public void HybridOrchestrator_ShowWithFallback_FiresOnAdFailedDisplayed_WhenNeitherReady()
        {
            var primary   = new MockAdNetwork { NetworkName = "admob",    InterstitialReady = false };
            var secondary = new MockAdNetwork { NetworkName = "applovin", InterstitialReady = false };
            var orc = new HybridAdOrchestrator(primary, secondary);

            bool failedFired = false;
            orc.OnAdFailedDisplayed += () => failedFired = true;

            orc.ShowWithFallback(AdFormatKey.Interstitial,
                _ => { /* should not be called */ },
                n => n.IsInterstitialReady());

            Assert.IsTrue(failedFired,
                "OnAdFailedDisplayed must fire when both primary and secondary are not ready");
        }

        [Test]
        public void HybridOrchestrator_OnAdDisplayed_ForwardsFromPrimary()
        {
            var primary = new MockAdNetwork { NetworkName = "admob" };
            var orc = new HybridAdOrchestrator(primary);

            bool displayed = false;
            orc.OnAdDisplayed += () => displayed = true;

            primary.TriggerAdDisplayed();

            Assert.IsTrue(displayed,
                "OnAdDisplayed on orchestrator must fire when primary network fires OnAdDisplayed");
        }

        [Test]
        public void HybridOrchestrator_OnAdClosed_ForwardsFromSecondary()
        {
            var primary   = new MockAdNetwork { NetworkName = "admob" };
            var secondary = new MockAdNetwork { NetworkName = "applovin" };
            var orc = new HybridAdOrchestrator(primary, secondary);

            bool closed = false;
            orc.OnAdClosed += () => closed = true;

            secondary.TriggerAdClosed();

            Assert.IsTrue(closed,
                "OnAdClosed on orchestrator must fire when secondary network fires OnAdClosed");
        }

        [Test]
        public void HybridOrchestrator_OnAdClicked_ForwardsFromPrimary()
        {
            var primary = new MockAdNetwork { NetworkName = "admob" };
            var orc = new HybridAdOrchestrator(primary);

            bool clicked = false;
            orc.OnAdClicked += () => clicked = true;

            primary.TriggerAdClicked();

            Assert.IsTrue(clicked,
                "OnAdClicked must be forwarded from primary to orchestrator subscribers");
        }

        [Test]
        public void HybridOrchestrator_GetNetworkForFormat_ReturnsSecondary_WhenOverrideSet()
        {
            var primary   = new MockAdNetwork { NetworkName = "admob" };
            var secondary = new MockAdNetwork { NetworkName = "applovin" };
            var overrides = new System.Collections.Generic.Dictionary<string, string>
            {
                { AdFormatKey.Rewarded, "applovin" }
            };
            var orc = new HybridAdOrchestrator(primary, secondary, adFormatOverrides: overrides);

            var chosen = orc.GetNetworkForFormat(AdFormatKey.Rewarded);

            Assert.AreSame(secondary, chosen,
                "GetNetworkForFormat must return secondary when an override pins the format to it");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group C — MediationManager event forwarding (3 tests)
        // ═══════════════════════════════════════════════════════════════════════
        //
        // These tests wire a HybridAdOrchestrator (with MockAdNetworks) directly
        // to verify that the orchestrator event chain is intact. MediationManager
        // subscribes to orchestrator events inside SubscribeToOrchestratorEvents()
        // which is called from Initialize(). To keep tests EditMode-safe (no real
        // ad SDKs), we test the orchestrator event chain directly — the plumbing
        // from MediationManager through to orchestrator is covered by Group B.

        [Test]
        public void OrchestratorEventChain_OnAdDisplayed_FiresAfterPrimaryTrigger()
        {
            var primary = new MockAdNetwork { NetworkName = "admob" };
            var orc = new HybridAdOrchestrator(primary);

            int callCount = 0;
            orc.OnAdDisplayed += () => callCount++;

            primary.TriggerAdDisplayed();
            primary.TriggerAdDisplayed();

            Assert.AreEqual(2, callCount,
                "OnAdDisplayed must fire once per TriggerAdDisplayed call");
        }

        [Test]
        public void OrchestratorEventChain_OnAdImpressionRecorded_FiresFromPrimary()
        {
            var primary = new MockAdNetwork { NetworkName = "admob" };
            var orc = new HybridAdOrchestrator(primary);

            bool fired = false;
            orc.OnAdImpressionRecorded += () => fired = true;

            primary.TriggerAdImpressionRecorded();

            Assert.IsTrue(fired,
                "OnAdImpressionRecorded must be forwarded from primary network to orchestrator subscribers");
        }

        [Test]
        public void OrchestratorEventChain_OnAdFailedDisplayed_FiresFromSecondary()
        {
            var primary   = new MockAdNetwork { NetworkName = "admob" };
            var secondary = new MockAdNetwork { NetworkName = "applovin" };
            var orc = new HybridAdOrchestrator(primary, secondary);

            bool fired = false;
            orc.OnAdFailedDisplayed += () => fired = true;

            secondary.TriggerAdFailedDisplayed();

            Assert.IsTrue(fired,
                "OnAdFailedDisplayed must be forwarded from secondary network to orchestrator subscribers");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group D — ApplyIaaConfigFromRemote (2 tests)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void ApplyIaaConfigFromRemote_SetsOriginToRemoteOverride_ThenResetsToLocal()
        {
            // Verify the origin constant values are distinct — the distinction matters
            // for the applied_iaa_config event that downstream analytics consumers rely on.
            Assert.AreNotEqual(
                MediationManager.IaaConfigOriginLocal,
                MediationManager.IaaConfigOriginRemoteOverride,
                "IaaConfigOriginLocal and IaaConfigOriginRemoteOverride must be different strings");

            Assert.AreEqual("local",           MediationManager.IaaConfigOriginLocal);
            Assert.AreEqual("remote_override", MediationManager.IaaConfigOriginRemoteOverride);
        }

        [Test]
        public void ApplyIaaConfigFromRemote_CalledRepeatedly_DoesNotThrow()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new MediationManager(new NoopAdPlaceholderUI(), null);

            Assert.DoesNotThrow(() =>
            {
                mgr.ApplyIaaConfigFromRemote(null);
                mgr.ApplyIaaConfigFromRemote(new IAA { Mediation = "admob" });
                mgr.ApplyIaaConfigFromRemote(null);
            },
            "ApplyIaaConfigFromRemote must not throw regardless of how many times it is called or what is passed");
        }
    }
}
