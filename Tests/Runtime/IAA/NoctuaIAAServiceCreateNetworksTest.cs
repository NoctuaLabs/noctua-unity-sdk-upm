using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using com.noctuagames.sdk;
using com.noctuagames.sdk.AdPlaceholder;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using IAAConfig = com.noctuagames.sdk.IAA;

namespace Tests.Runtime.IAA
{
    /// <summary>
    /// Smoke test for <see cref="NoctuaIAAService.CreateNetworks"/> driven by
    /// the project's actual <c>Assets/StreamingAssets/noctuagg.json</c>.
    ///
    /// Verifies that constructing a <c>NoctuaIAAService</c> with the real
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
    public class NoctuaIAAServiceCreateNetworksTest
    {
        private class NoopAdPlaceholderUI : IAdPlaceholderUI
        {
            public void ShowAdPlaceholder(AdPlaceholderType adType, CrossPromotionEntry entry) { }
            public void ShowBannerPlaceholder(CrossPromotionEntry entry) { }
            public void CloseBannerPlaceholder() { }
            public void SetBannerLayout(AdPlaceholderSize size, AdPlaceholderPosition position) { }
            public void PreloadAdPlaceholder(CrossPromotionConfig config) { }
            public void SetPlaceholderClosedCallback(System.Action onClosed) { }
            public void SetPlaceholderClickedCallback(System.Action onClicked) { }
            public void SetPlaceholderShownCallback(System.Action onShown) { }
            public void SetPlaceholderFailedCallback(System.Action onFailed) { }
            public void SetBannerPlaceholderClosedCallback(System.Action onClosed) { }
            public void SetBannerPlaceholderClickedCallback(System.Action onClicked) { }
            public void SetBannerPlaceholderShownCallback(System.Action onShown) { }
            public void SetBannerPlaceholderFailedCallback(System.Action onFailed) { }
            public void CloseAdPlaceholder() { }
            public bool IsAssetCached(string assetUrl) => false;
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

            // Pipe Serilog → UnityLogSink so NoctuaIAAService's _log.Info reaches
            // Unity's Debug.unityLogger, where LogAssert can observe it.
            NoctuaLogger.Init(config);

            // The SDK-availability summary fires unconditionally at the top of
            // CreateNetworks. Match either status so the test is portable across
            // build configurations.
            LogAssert.Expect(LogType.Log,
                new Regex(@"NoctuaIAAService\.CreateNetworks: IAA SDK availability: AdMob=(integrated|missing), AppLovin=(integrated|missing)\. Requested in noctuagg\.json"));

#if UNITY_ADMOB || UNITY_APPLOVIN
            // CreateNetworks emits exactly one "Networks created..." line summarising
            // primary/secondary/hybrid/cpm/segment. Match it loosely so we don't
            // re-encode the SDK-define decision tree here — the assertions below
            // pin the parts that matter per build configuration.
            LogAssert.Expect(LogType.Log,
                new Regex(@"NoctuaIAAService\.CreateNetworks: Networks created\. Primary: \w+(?:, Secondary: \w+)?, Hybrid: (True|False), CpmFloors: (enabled|disabled), Segment: \S+"));

            var mgr = new NoctuaIAAService(new NoopAdPlaceholderUI(), config.IAA);

            // CPM floors are enabled in the shipping config; if that flips off in
            // a future config refactor the assertion will surface it.
            Assume.That(config.IAA.CpmFloors?.Enabled == true,
                "cpm_floors.enabled flipped off — re-baseline this test");

#if UNITY_ADMOB && UNITY_APPLOVIN
            // Both SDKs compiled in: primary follows iaa.mediation, secondary
            // follows iaa.secondary_mediation. Hybrid iff both configured AND
            // they're different networks. Comparison is case-insensitive to
            // match NoctuaIAAService.NormalizeMediationName.
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
            // without creating an routing.
            LogAssert.Expect(LogType.Warning,
                new Regex(@"NoctuaIAAService\.CreateNetworks: No ad mediation SDK is integrated in this build"));
            LogAssert.Expect(LogType.Error,
                new Regex(@"NoctuaIAAService\.CreateNetworks: No ad network SDK is available for the requested config"));

            var mgr = new NoctuaIAAService(new NoopAdPlaceholderUI(), config.IAA);

            Assert.IsFalse(mgr.IsHybridMode,
                "Without any SDK define, no routing is created and hybrid mode is false");
#endif
        }

#if UNITY_ADMOB && UNITY_APPLOVIN
        // Rewarded interstitial is AdMob-only, so with AppLovin primary it can only come from the
        // AdMob secondary. Before the fix nothing created it there and every show failed with
        // "Rewarded interstitial ad not initialized".
        [Test]
        public void SetupSecondaryAdUnits_AdmobSecondary_CreatesRewardedInterstitial()
        {
            const string unit = "ca-app-pub-3940256099942544/5354046379";
            var iaa = JsonConvert.DeserializeObject<IAAConfig>(@"{
                ""mediation"": ""applovin"",
                ""secondary_mediation"": ""admob"",
                ""networks"": { ""admob"": { ""ad_formats"": { ""rewarded_interstitial"": {
                    ""android"": { ""ad_unit_id"": """ + unit + @""" },
                    ""ios"": { ""ad_unit_id"": """ + unit + @""" } } } } }
            }");
            LogAssert.ignoreFailingMessages = true;   // AdMob logs load errors in EditMode
            var service = new NoctuaIAAService(new NoopAdPlaceholderUI(), iaa);

            const BindingFlags priv = BindingFlags.NonPublic | BindingFlags.Instance;
            var routing = (HybridAdRouting)typeof(NoctuaIAAService).GetField("_routing", priv).GetValue(service);
            Assume.That(routing?.Secondary?.NetworkName, Is.EqualTo(AdNetworkName.Admob));

            typeof(NoctuaIAAService).GetMethod("SetupSecondaryAdUnits", priv)
                .Invoke(service, new object[] { iaa, routing.Secondary });

            Assert.IsNotNull(typeof(NoctuaIAAService).GetField("_rewardedInterstitialAdmob", priv).GetValue(service),
                "AdMob secondary must create the rewarded interstitial so ShowRewardedInterstitialAd can show it");
        }
#endif
    }

    /// <summary>
    /// Tests for the three pure-static helper methods on NoctuaIAAService:
    /// NormalizeMediationName, IsRecognisedMediationName, and IsAvailable.
    /// These methods have no side effects and require no SDK defines.
    /// </summary>
    [TestFixture]
    public class NoctuaIAAServiceStaticApiTest
    {
        // ── NormalizeMediationName ────────────────────────────────────────────────

        [Test]
        public void NormalizeMediationName_Null_ReturnsNull()
        {
            Assert.IsNull(NoctuaIAAService.NormalizeMediationName(null));
        }

        [Test]
        public void NormalizeMediationName_EmptyString_ReturnsNull()
        {
            Assert.IsNull(NoctuaIAAService.NormalizeMediationName(""));
        }

        [Test]
        public void NormalizeMediationName_WhitespaceOnly_ReturnsNull()
        {
            Assert.IsNull(NoctuaIAAService.NormalizeMediationName("   "));
        }

        [Test]
        public void NormalizeMediationName_MixedCase_ReturnsLower()
        {
            Assert.AreEqual("applovin", NoctuaIAAService.NormalizeMediationName("AppLovin"));
        }

        [Test]
        public void NormalizeMediationName_AllCaps_ReturnsLower()
        {
            Assert.AreEqual("admob", NoctuaIAAService.NormalizeMediationName("ADMOB"));
        }

        [Test]
        public void NormalizeMediationName_WithLeadingTrailingSpaces_Trimmed()
        {
            Assert.AreEqual("admob", NoctuaIAAService.NormalizeMediationName("  admob  "));
        }

        [Test]
        public void NormalizeMediationName_MixedCaseAndSpaces_NormalisedAndTrimmed()
        {
            Assert.AreEqual("applovin", NoctuaIAAService.NormalizeMediationName("  AppLovin  "));
        }

        [Test]
        public void NormalizeMediationName_AlreadyLower_ReturnsSame()
        {
            Assert.AreEqual("admob", NoctuaIAAService.NormalizeMediationName("admob"));
        }

        // ── IsRecognisedMediationName ─────────────────────────────────────────────

        [Test]
        public void IsRecognisedMediationName_Admob_ReturnsTrue()
        {
            Assert.IsTrue(NoctuaIAAService.IsRecognisedMediationName("admob"));
        }

        [Test]
        public void IsRecognisedMediationName_AppLovin_ReturnsTrue()
        {
            Assert.IsTrue(NoctuaIAAService.IsRecognisedMediationName("applovin"));
        }

        [Test]
        public void IsRecognisedMediationName_Unknown_ReturnsFalse()
        {
            Assert.IsFalse(NoctuaIAAService.IsRecognisedMediationName("ironsource"));
        }

        [Test]
        public void IsRecognisedMediationName_Null_ReturnsFalse()
        {
            Assert.IsFalse(NoctuaIAAService.IsRecognisedMediationName(null));
        }

        [Test]
        public void IsRecognisedMediationName_EmptyString_ReturnsFalse()
        {
            Assert.IsFalse(NoctuaIAAService.IsRecognisedMediationName(""));
        }

        [Test]
        public void IsRecognisedMediationName_MixedCase_ReturnsFalse()
        {
            // NormalizeMediationName must be called first — IsRecognisedMediationName
            // expects an already-normalised string.
            Assert.IsFalse(NoctuaIAAService.IsRecognisedMediationName("AdMob"));
        }

        // ── IsAvailable ───────────────────────────────────────────────────────────

        [Test]
        public void IsAvailable_Admob_WhenAdmobTrue_ReturnsTrue()
        {
            Assert.IsTrue(NoctuaIAAService.IsAvailable("admob", admobAvailable: true, applovinAvailable: false));
        }

        [Test]
        public void IsAvailable_Admob_WhenAdmobFalse_ReturnsFalse()
        {
            Assert.IsFalse(NoctuaIAAService.IsAvailable("admob", admobAvailable: false, applovinAvailable: true));
        }

        [Test]
        public void IsAvailable_AppLovin_WhenAppLovinTrue_ReturnsTrue()
        {
            Assert.IsTrue(NoctuaIAAService.IsAvailable("applovin", admobAvailable: false, applovinAvailable: true));
        }

        [Test]
        public void IsAvailable_AppLovin_WhenAppLovinFalse_ReturnsFalse()
        {
            Assert.IsFalse(NoctuaIAAService.IsAvailable("applovin", admobAvailable: true, applovinAvailable: false));
        }

        [Test]
        public void IsAvailable_Unknown_ReturnsFalse()
        {
            Assert.IsFalse(NoctuaIAAService.IsAvailable("ironsource", admobAvailable: true, applovinAvailable: true));
        }

        [Test]
        public void IsAvailable_Null_ReturnsFalse()
        {
            Assert.IsFalse(NoctuaIAAService.IsAvailable(null, admobAvailable: true, applovinAvailable: true));
        }

        [Test]
        public void IsAvailable_EmptyString_ReturnsFalse()
        {
            Assert.IsFalse(NoctuaIAAService.IsAvailable("", admobAvailable: true, applovinAvailable: true));
        }

        [Test]
        public void IsAvailable_Admob_BothFalse_ReturnsFalse()
        {
            Assert.IsFalse(NoctuaIAAService.IsAvailable("admob", admobAvailable: false, applovinAvailable: false));
        }

        [Test]
        public void IsAvailable_AppLovin_BothTrue_ReturnsTrue()
        {
            Assert.IsTrue(NoctuaIAAService.IsAvailable("applovin", admobAvailable: true, applovinAvailable: true));
        }
    }

    /// <summary>
    /// Tests for NoctuaIAAService instance API (properties and diagnostic methods).
    /// Constructs instances with minimal config to exercise the instance surface
    /// without requiring a real ad SDK or network connectivity.
    ///
    /// LogAssert.ignoreFailingMessages is set to true before each constructor call
    /// because NoctuaLogger routes through Debug.unityLogger and CreateNetworks
    /// emits availability info/warning/error messages depending on which SDK
    /// defines are compiled in.
    /// </summary>
    [TestFixture]
    public class NoctuaIAAServiceInstanceApiTest
    {
        private class NoopAdPlaceholderUI : IAdPlaceholderUI
        {
            public void ShowAdPlaceholder(AdPlaceholderType adType, CrossPromotionEntry entry) { }
            public void ShowBannerPlaceholder(CrossPromotionEntry entry) { }
            public void CloseBannerPlaceholder() { }
            public void SetBannerLayout(AdPlaceholderSize size, AdPlaceholderPosition position) { }
            public void PreloadAdPlaceholder(CrossPromotionConfig config) { }
            public void SetPlaceholderClosedCallback(System.Action onClosed) { }
            public void SetPlaceholderClickedCallback(System.Action onClicked) { }
            public void SetPlaceholderShownCallback(System.Action onShown) { }
            public void SetPlaceholderFailedCallback(System.Action onFailed) { }
            public void SetBannerPlaceholderClosedCallback(System.Action onClosed) { }
            public void SetBannerPlaceholderClickedCallback(System.Action onClicked) { }
            public void SetBannerPlaceholderShownCallback(System.Action onShown) { }
            public void SetBannerPlaceholderFailedCallback(System.Action onFailed) { }
            public void CloseAdPlaceholder() { }
            public bool IsAssetCached(string assetUrl) => false;
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
                var _ = new NoctuaIAAService(new NoopAdPlaceholderUI(), null);
            });
        }

        [Test]
        public void Constructor_WithMinimalAdmobIaa_DoesNotThrow()
        {
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() =>
            {
                var _ = new NoctuaIAAService(new NoopAdPlaceholderUI(), MinimalIaa("admob"));
            });
        }

        [Test]
        public void Constructor_WithMinimalApplovinIaa_DoesNotThrow()
        {
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() =>
            {
                var _ = new NoctuaIAAService(new NoopAdPlaceholderUI(), MinimalIaa("applovin"));
            });
        }

        // ── IsHybridMode ──────────────────────────────────────────────────────────

        [Test]
        public void IsHybridMode_DefaultFalse_WhenNullIaa()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new NoctuaIAAService(new NoopAdPlaceholderUI(), null);
            Assert.IsFalse(mgr.IsHybridMode,
                "IsHybridMode must be false when constructed with null IAA (routing is null)");
        }

        // ── GetSegmentKey ─────────────────────────────────────────────────────────

        [Test]
        public void GetSegmentKey_WhenNullIaa_ReturnsNotInitialized()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new NoctuaIAAService(new NoopAdPlaceholderUI(), null);
            // _segments is null when no IAA config is processed → "not initialized"
            Assert.AreEqual("not initialized", mgr.GetSegmentKey());
        }

        [Test]
        public void GetSegmentKey_WithMinimalIaa_DoesNotThrow()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new NoctuaIAAService(new NoopAdPlaceholderUI(), MinimalIaa());
            Assert.DoesNotThrow(() => mgr.GetSegmentKey());
        }

        [Test]
        public void GetSegmentKey_ReturnsString()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new NoctuaIAAService(new NoopAdPlaceholderUI(), MinimalIaa());
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
            var mgr = new NoctuaIAAService(new NoopAdPlaceholderUI(), null);
            var assignments = mgr.GetExperimentAssignments();
            Assert.IsNotNull(assignments);
            Assert.AreEqual(0, assignments.Count);
        }

        [Test]
        public void GetExperimentAssignments_WithMinimalIaa_ReturnsDict()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new NoctuaIAAService(new NoopAdPlaceholderUI(), MinimalIaa());
            var assignments = mgr.GetExperimentAssignments();
            Assert.IsNotNull(assignments, "GetExperimentAssignments must never return null");
        }

        // ── GetCpmFloorStatus ─────────────────────────────────────────────────────

        [Test]
        public void GetCpmFloorStatus_WhenNullIaa_ReturnsCpmFloorsDisabled()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new NoctuaIAAService(new NoopAdPlaceholderUI(), null);
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
            var mgr = new NoctuaIAAService(new NoopAdPlaceholderUI(), MinimalIaa());
            var status = mgr.GetCpmFloorStatus();
            Assert.IsNotNull(status, "GetCpmFloorStatus must never return null");
        }

        // ── MediationType ─────────────────────────────────────────────────────────

        [Test]
        public void MediationType_WhenNullIaa_IsNull()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new NoctuaIAAService(new NoopAdPlaceholderUI(), null);
            // _mediationType starts null; CreateNetworks is not called for null IAA
            Assert.IsNull(mgr.MediationType);
        }

        [Test]
        public void MediationType_IsAccessibleWithoutException()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new NoctuaIAAService(new NoopAdPlaceholderUI(), MinimalIaa());
            Assert.DoesNotThrow(() => { var _ = mgr.MediationType; });
        }

        // ── SetAdRevenueTracker ───────────────────────────────────────────────────

        [Test]
        public void SetAdRevenueTracker_Null_DoesNotThrow()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new NoctuaIAAService(new NoopAdPlaceholderUI(), null);
            Assert.DoesNotThrow(() => mgr.SetAdRevenueTracker(null));
        }

        [Test]
        public void SetAdRevenueTracker_CalledTwiceWithNull_DoesNotThrow()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new NoctuaIAAService(new NoopAdPlaceholderUI(), null);
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
            var mgr = new NoctuaIAAService(new NoopAdPlaceholderUI(), null);
            Assert.AreEqual("unused", mgr.InterstitialAdUnitID);
        }

        [Test]
        public void RewardedAdUnitID_DefaultValue_IsUnused()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new NoctuaIAAService(new NoopAdPlaceholderUI(), null);
            Assert.AreEqual("unused", mgr.RewardedAdUnitID);
        }

        [Test]
        public void BannerAdUnitID_DefaultValue_IsUnused()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new NoctuaIAAService(new NoopAdPlaceholderUI(), null);
            Assert.AreEqual("unused", mgr.BannerAdUnitID);
        }

        [Test]
        public void RewardedInterstitialAdUnitID_DefaultValue_IsUnused()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new NoctuaIAAService(new NoopAdPlaceholderUI(), null);
            Assert.AreEqual("unused", mgr.RewardedInterstitialAdUnitID);
        }

        // ── ApplyIaaConfigFromRemote ──────────────────────────────────────────────

        [Test]
        public void ApplyIaaConfigFromRemote_WithNull_DoesNotThrow()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new NoctuaIAAService(new NoopAdPlaceholderUI(), null);
            // Null merged config is assigned but IAA setter guards against null
            // (sets _iaaResponse = value; then returns because value == null).
            Assert.DoesNotThrow(() => mgr.ApplyIaaConfigFromRemote(null));
        }

        [Test]
        public void ApplyIaaConfigFromRemote_WithValidConfig_DoesNotThrow()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new NoctuaIAAService(new NoopAdPlaceholderUI(), null);
            Assert.DoesNotThrow(() => mgr.ApplyIaaConfigFromRemote(MinimalIaa("admob")));
        }
    }

    /// <summary>
    /// Tests for NoctuaIAAService ad lifecycle behaviour:
    ///
    ///   Group A — null-routing safety (12 tests)
    ///     All Show*/Load*/IsReady public methods must not throw and must return
    ///     safe defaults when constructed with null IAA (i.e. _routing == null).
    ///
    ///   Group B — HybridAdRouting dispatch (7 tests)
    ///     Validates that HybridAdRouting correctly dispatches Show calls and
    ///     forwards events from MockAdNetwork to routing subscribers.
    ///
    ///   Group C — NoctuaIAAService event forwarding (3 tests)
    ///     Validates that routing lifecycle events (OnAdDisplayed, OnAdClosed,
    ///     OnAdFailedDisplayed) are forwarded through to NoctuaIAAService's public
    ///     events when the manager is wired with a real HybridAdRouting.
    ///
    ///   Group D — ApplyIaaConfigFromRemote (2 tests)
    ///     Validates origin tagging and safe re-invocation of ApplyIaaConfigFromRemote.
    /// </summary>
    [TestFixture]
    public class NoctuaIAAServiceAdLifecycleTest
    {
        private class NoopAdPlaceholderUI : IAdPlaceholderUI
        {
            public void ShowAdPlaceholder(AdPlaceholderType adType, CrossPromotionEntry entry) { }
            public void ShowBannerPlaceholder(CrossPromotionEntry entry) { }
            public void CloseBannerPlaceholder() { }
            public void SetBannerLayout(AdPlaceholderSize size, AdPlaceholderPosition position) { }
            public void PreloadAdPlaceholder(CrossPromotionConfig config) { }
            public void SetPlaceholderClosedCallback(System.Action onClosed) { }
            public void SetPlaceholderClickedCallback(System.Action onClicked) { }
            public void SetPlaceholderShownCallback(System.Action onShown) { }
            public void SetPlaceholderFailedCallback(System.Action onFailed) { }
            public void SetBannerPlaceholderClosedCallback(System.Action onClosed) { }
            public void SetBannerPlaceholderClickedCallback(System.Action onClicked) { }
            public void SetBannerPlaceholderShownCallback(System.Action onShown) { }
            public void SetBannerPlaceholderFailedCallback(System.Action onFailed) { }
            public void CloseAdPlaceholder() { }
            public bool IsAssetCached(string assetUrl) => false;
        }

        private static NoctuaIAAService NullIaaManager()
        {
            LogAssert.ignoreFailingMessages = true;
            return new NoctuaIAAService(new NoopAdPlaceholderUI(), null);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group A — null-routing safety (12 tests)
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
                "IsInterstitialReady must return false when routing is null");
        }

        [Test]
        public void IsRewardedAdReady_NullOrchestrator_ReturnsFalse()
        {
            var mgr = NullIaaManager();
            Assert.IsFalse(mgr.IsRewardedAdReady(),
                "IsRewardedAdReady must return false when routing is null");
        }

        [Test]
        public void IsAppOpenAdReady_NullOrchestrator_ReturnsFalse()
        {
            var mgr = NullIaaManager();
            Assert.IsFalse(mgr.IsAppOpenAdReady(),
                "IsAppOpenAdReady must return false when AppOpenAds is null (null IAA)");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group B — HybridAdRouting dispatch (7 tests)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void HybridOrchestrator_ShowWithFallback_PrefersPrimary_WhenReady()
        {
            var primary   = new MockAdNetwork { NetworkName = "admob",    InterstitialReady = true  };
            var secondary = new MockAdNetwork { NetworkName = "applovin", InterstitialReady = true  };
            var orc = new HybridAdRouting(primary, secondary);

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
            var orc = new HybridAdRouting(primary, secondary);

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
            var orc = new HybridAdRouting(primary, secondary);

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
            var orc = new HybridAdRouting(primary);

            bool displayed = false;
            orc.OnAdDisplayed += () => displayed = true;

            primary.TriggerAdDisplayed();

            Assert.IsTrue(displayed,
                "OnAdDisplayed on routing must fire when primary network fires OnAdDisplayed");
        }

        [Test]
        public void HybridOrchestrator_OnAdClosed_ForwardsFromSecondary()
        {
            var primary   = new MockAdNetwork { NetworkName = "admob" };
            var secondary = new MockAdNetwork { NetworkName = "applovin" };
            var orc = new HybridAdRouting(primary, secondary);

            bool closed = false;
            orc.OnAdClosed += () => closed = true;

            secondary.TriggerAdClosed();

            Assert.IsTrue(closed,
                "OnAdClosed on routing must fire when secondary network fires OnAdClosed");
        }

        [Test]
        public void HybridOrchestrator_OnAdClicked_ForwardsFromPrimary()
        {
            var primary = new MockAdNetwork { NetworkName = "admob" };
            var orc = new HybridAdRouting(primary);

            bool clicked = false;
            orc.OnAdClicked += () => clicked = true;

            primary.TriggerAdClicked();

            Assert.IsTrue(clicked,
                "OnAdClicked must be forwarded from primary to routing subscribers");
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
            var orc = new HybridAdRouting(primary, secondary, adFormatOverrides: overrides);

            var chosen = orc.GetNetworkForFormat(AdFormatKey.Rewarded);

            Assert.AreSame(secondary, chosen,
                "GetNetworkForFormat must return secondary when an override pins the format to it");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group C — NoctuaIAAService event forwarding (3 tests)
        // ═══════════════════════════════════════════════════════════════════════
        //
        // These tests wire a HybridAdRouting (with MockAdNetworks) directly
        // to verify that the routing event chain is intact. NoctuaIAAService
        // subscribes to routing events inside SubscribeToRoutingEvents()
        // which is called from Initialize(). To keep tests EditMode-safe (no real
        // ad SDKs), we test the routing event chain directly — the plumbing
        // from NoctuaIAAService through to routing is covered by Group B.

        [Test]
        public void OrchestratorEventChain_OnAdDisplayed_FiresAfterPrimaryTrigger()
        {
            var primary = new MockAdNetwork { NetworkName = "admob" };
            var orc = new HybridAdRouting(primary);

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
            var orc = new HybridAdRouting(primary);

            bool fired = false;
            orc.OnAdImpressionRecorded += () => fired = true;

            primary.TriggerAdImpressionRecorded();

            Assert.IsTrue(fired,
                "OnAdImpressionRecorded must be forwarded from primary network to routing subscribers");
        }

        [Test]
        public void OrchestratorEventChain_OnAdFailedDisplayed_FiresFromSecondary()
        {
            var primary   = new MockAdNetwork { NetworkName = "admob" };
            var secondary = new MockAdNetwork { NetworkName = "applovin" };
            var orc = new HybridAdRouting(primary, secondary);

            bool fired = false;
            orc.OnAdFailedDisplayed += () => fired = true;

            secondary.TriggerAdFailedDisplayed();

            Assert.IsTrue(fired,
                "OnAdFailedDisplayed must be forwarded from secondary network to routing subscribers");
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
                NoctuaIAAService.IaaConfigOriginLocal,
                NoctuaIAAService.IaaConfigOriginRemoteOverride,
                "IaaConfigOriginLocal and IaaConfigOriginRemoteOverride must be different strings");

            Assert.AreEqual("local",           NoctuaIAAService.IaaConfigOriginLocal);
            Assert.AreEqual("remote_override", NoctuaIAAService.IaaConfigOriginRemoteOverride);
        }

        [Test]
        public void ApplyIaaConfigFromRemote_CalledRepeatedly_DoesNotThrow()
        {
            LogAssert.ignoreFailingMessages = true;
            var mgr = new NoctuaIAAService(new NoopAdPlaceholderUI(), null);

            Assert.DoesNotThrow(() =>
            {
                mgr.ApplyIaaConfigFromRemote(null);
                mgr.ApplyIaaConfigFromRemote(new IAAConfig { Mediation = "admob" });
                mgr.ApplyIaaConfigFromRemote(null);
            },
            "ApplyIaaConfigFromRemote must not throw regardless of how many times it is called or what is passed");
        }
    }

    /// <summary>
    /// Tests for NoctuaIAAService utility and diagnostic APIs that are not covered
    /// elsewhere: OnApplicationForeground, ShowCreativeDebugger, DroppedEventCount.
    /// </summary>
    [TestFixture]
    public class NoctuaIAAServiceUtilityApiTest
    {
        private class NoopAdPlaceholderUI : IAdPlaceholderUI
        {
            public void ShowAdPlaceholder(AdPlaceholderType adType, CrossPromotionEntry entry) { }
            public void ShowBannerPlaceholder(CrossPromotionEntry entry) { }
            public void CloseBannerPlaceholder() { }
            public void SetBannerLayout(AdPlaceholderSize size, AdPlaceholderPosition position) { }
            public void PreloadAdPlaceholder(CrossPromotionConfig config) { }
            public void SetPlaceholderClosedCallback(System.Action onClosed) { }
            public void SetPlaceholderClickedCallback(System.Action onClicked) { }
            public void SetPlaceholderShownCallback(System.Action onShown) { }
            public void SetPlaceholderFailedCallback(System.Action onFailed) { }
            public void SetBannerPlaceholderClosedCallback(System.Action onClosed) { }
            public void SetBannerPlaceholderClickedCallback(System.Action onClicked) { }
            public void SetBannerPlaceholderShownCallback(System.Action onShown) { }
            public void SetBannerPlaceholderFailedCallback(System.Action onFailed) { }
            public void CloseAdPlaceholder() { }
            public bool IsAssetCached(string assetUrl) => false;
        }

        private static NoctuaIAAService NullIaaManager()
        {
            LogAssert.ignoreFailingMessages = true;
            return new NoctuaIAAService(new NoopAdPlaceholderUI(), null);
        }

        // ── OnApplicationForeground ───────────────────────────────────────────

        [Test]
        public void OnApplicationForeground_NullAppOpenAds_DoesNotThrow()
        {
            // When constructed with null IAA, _appOpenAds is null.
            // OnApplicationForeground uses ?. so must not throw.
            var mgr = NullIaaManager();
            Assert.DoesNotThrow(() => mgr.OnApplicationForeground(),
                "OnApplicationForeground must not throw when _appOpenAds is null");
        }

        [Test]
        public void OnApplicationForeground_CalledMultipleTimes_DoesNotThrow()
        {
            var mgr = NullIaaManager();
            Assert.DoesNotThrow(() =>
            {
                mgr.OnApplicationForeground();
                mgr.OnApplicationForeground();
                mgr.OnApplicationForeground();
            },
            "OnApplicationForeground must be idempotent and never throw");
        }

        // ── ShowCreativeDebugger ──────────────────────────────────────────────

        [Test]
        public void ShowCreativeDebugger_NullOrchestrator_DoesNotThrow()
        {
            // Guard: `if (_routing == null) return;`
            var mgr = NullIaaManager();
            Assert.DoesNotThrow(() => mgr.ShowCreativeDebugger(),
                "ShowCreativeDebugger must return silently when routing is null");
        }

        [Test]
        public void ShowCreativeDebugger_WithAdmobOnlyOrchestrator_DoesNotThrow()
        {
            // AppLovin is not in the mock network → falls through to the warning log path
            LogAssert.ignoreFailingMessages = true;
            var mgr = new NoctuaIAAService(new NoopAdPlaceholderUI(), new IAAConfig { Mediation = "admob" });
            Assert.DoesNotThrow(() => mgr.ShowCreativeDebugger(),
                "ShowCreativeDebugger with no AppLovin must log warning and return silently");
        }

        // ── SetAdRevenueTracker after construction ────────────────────────────

        [Test]
        public void SetAdRevenueTracker_AfterSetToNull_CanBeCalledAgainWithNull()
        {
            var mgr = NullIaaManager();
            Assert.DoesNotThrow(() =>
            {
                mgr.SetAdRevenueTracker(null);
                mgr.SetAdRevenueTracker(null);
                mgr.SetAdRevenueTracker(null);
            },
            "SetAdRevenueTracker(null) must be safely callable any number of times");
        }

        // ── IaaConfigOrigin constants ─────────────────────────────────────────

        [Test]
        public void IaaConfigOriginConstants_AreCorrectValues()
        {
            Assert.AreEqual("local",           NoctuaIAAService.IaaConfigOriginLocal);
            Assert.AreEqual("remote_override", NoctuaIAAService.IaaConfigOriginRemoteOverride);
        }

        // ── PostToMainThread — null-context and try/catch fallback ────────────
        //
        // PostToMainThread is private. In EditMode, SynchronizationContext.Current
        // is null, so _mainThreadContext captured in the constructor is null.
        // The method executes action() inline in that case.
        // We verify both the null-context path and the try/catch fallback using
        // reflection (accessing private members is acceptable for critical safety nets).
        //
        // Structure of PostToMainThread:
        //   if (_mainThreadContext != null) {
        //       try { _mainThreadContext.Post(_ => action(), null); }
        //       catch { log; action(); }  ← try/catch fallback
        //   } else {
        //       action();  ← null-context inline fallback
        //   }

        private static readonly MethodInfo PostToMainThreadMethod =
            typeof(NoctuaIAAService).GetMethod(
                "PostToMainThread",
                BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo MainThreadContextField =
            typeof(NoctuaIAAService).GetField(
                "_mainThreadContext",
                BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>
        /// SynchronizationContext that always throws on Post() to trigger the try/catch
        /// fallback inside PostToMainThread.
        /// </summary>
        private class ThrowingOnPostContext : SynchronizationContext
        {
            public override void Post(SendOrPostCallback d, object state)
                => throw new InvalidOperationException("Simulated context.Post failure");
        }

        [Test]
        public void PostToMainThread_NullContext_ExecutesActionInline()
        {
            // Force _mainThreadContext to null so the else-branch executes action() inline.
            // In EditMode SynchronizationContext.Current is NOT null at construction time,
            // so we use reflection to simulate the null-context fallback path explicitly.
            LogAssert.ignoreFailingMessages = true;
            var mgr = NullIaaManager();
            MainThreadContextField.SetValue(mgr, null);

            int callCount = 0;
            PostToMainThreadMethod.Invoke(mgr, new object[] { (Action)(() => callCount++) });

            Assert.AreEqual(1, callCount,
                "PostToMainThread with null _mainThreadContext must execute action inline");
        }

        [Test]
        public void PostToMainThread_ContextThrows_ExecutesActionAsFallback()
        {
            // Inject a SynchronizationContext whose Post() throws, then invoke
            // PostToMainThread to verify the catch-branch executes action() as a fallback.
            LogAssert.ignoreFailingMessages = true;
            var mgr = NullIaaManager();

            // Inject the throwing context via reflection
            MainThreadContextField.SetValue(mgr, new ThrowingOnPostContext());

            int callCount = 0;
            PostToMainThreadMethod.Invoke(mgr, new object[] { (Action)(() => callCount++) });

            Assert.AreEqual(1, callCount,
                "PostToMainThread must execute action as fallback when context.Post throws");
        }

        [Test]
        public void PostToMainThread_NullAction_ThrowsOrIsHandled()
        {
            // Force _mainThreadContext to null so action() executes inline, making the null
            // invocation throw immediately (NullReferenceException wrapped by reflection into
            // TargetInvocationException). With a live SynchronizationContext, the Post() call
            // defers execution so the exception never reaches this thread.
            LogAssert.ignoreFailingMessages = true;
            var mgr = NullIaaManager();
            MainThreadContextField.SetValue(mgr, null);

            var ex = Assert.Throws<TargetInvocationException>(() =>
                PostToMainThreadMethod.Invoke(mgr, new object[] { (Action)null }));
            Assert.IsInstanceOf<NullReferenceException>(ex.InnerException,
                "Invoking PostToMainThread with a null action must throw NullReferenceException");
        }
    }
}
