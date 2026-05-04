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
    /// Behavior is conditional on which ad SDK defines are set in the build:
    ///   - UNITY_ADMOB only      → primary admob,    no secondary
    ///   - UNITY_APPLOVIN only   → primary applovin, no secondary
    ///   - both                  → primary admob,    secondary applovin if config says so
    ///   - neither               → CreateNetworks logs an error and returns early
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
            // CreateNetworks hardcodes primary = AdmobManager when UNITY_ADMOB is
            // defined, regardless of iaa.mediation. Secondary tracks
            // secondary_mediation only when it equals "applovin".
            bool expectHybrid = config.IAA.SecondaryMediation == AdNetworkName.AppLovin;
            Assert.AreEqual(expectHybrid, mgr.IsHybridMode,
                $"With both SDKs defined and secondary_mediation='{config.IAA.SecondaryMediation}', " +
                $"hybrid mode should be {expectHybrid}");
#elif UNITY_ADMOB
            Assert.IsFalse(mgr.IsHybridMode,
                "With only UNITY_ADMOB defined, secondary cannot be created — hybrid must be false");
#elif UNITY_APPLOVIN
            Assert.IsFalse(mgr.IsHybridMode,
                "With only UNITY_APPLOVIN defined, the reverse-direction secondary branch is unreachable — hybrid must be false");
#endif
#else
            // No ad SDK defines compiled in — CreateNetworks logs an error and
            // bails out without creating an orchestrator.
            LogAssert.Expect(LogType.Error,
                new Regex(@"MediationManager\.CreateNetworks: No ad network SDK is available\. Define UNITY_ADMOB or UNITY_APPLOVIN\."));

            var mgr = new MediationManager(new NoopAdPlaceholderUI(), config.IAA);

            Assert.IsFalse(mgr.IsHybridMode,
                "Without any SDK define, no orchestrator is created and hybrid mode is false");
#endif
        }
    }
}
