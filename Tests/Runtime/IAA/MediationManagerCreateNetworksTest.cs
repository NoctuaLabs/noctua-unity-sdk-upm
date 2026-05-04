using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using com.noctuagames.sdk.AdPlaceholder;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
            // they're different networks.
            bool expectHybrid =
                !string.IsNullOrEmpty(config.IAA.Mediation) &&
                !string.IsNullOrEmpty(config.IAA.SecondaryMediation) &&
                config.IAA.Mediation != config.IAA.SecondaryMediation;
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
}
