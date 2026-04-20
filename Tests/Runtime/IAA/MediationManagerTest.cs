using System.Collections.Generic;
using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine;
using IAAConfig = com.noctuagames.sdk.IAA;

namespace com.noctuagames.sdk.Tests.Mediation
{
    /// <summary>
    /// Tests MediationManager pure-logic paths that don't require native ad network SDKs.
    /// All tests run without UNITY_ADMOB / UNITY_APPLOVIN defines so CreateNetworks()
    /// exits early after logging an error — no native SDK calls occur.
    /// </summary>
    [TestFixture]
    public class MediationManagerTest
    {
        private class NullAdPlaceholderUI : IAdPlaceholderUI
        {
            public void ShowAdPlaceholder(AdPlaceholder.AdPlaceholderType adType) { }
            public void CloseAdPlaceholder() { }
        }

        private static IAAConfig MakeIAA(string mediation = "applovin") => new IAAConfig
        {
            Mediation = mediation,
            FrequencyCaps = new FrequencyCapConfig(),
            CooldownSeconds = new CooldownConfig { AppOpen = 30 },
            EnabledFormats = new EnabledFormatsConfig()
        };

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }

        // ─── Constructor ──────────────────────────────────────────────────────

        [Test]
        public void Constructor_NullIAA_DoesNotThrow()
        {
            MediationManager mgr = null;
            Assert.DoesNotThrow(() => mgr = new MediationManager(new NullAdPlaceholderUI(), null));
            Assert.IsNull(mgr.IAAResponse);
        }

        [Test]
        public void Constructor_NullPlaceholderUI_WithIAA_DoesNotThrow()
        {
            MediationManager mgr = null;
            Assert.DoesNotThrow(() => mgr = new MediationManager(null, MakeIAA()));
            Assert.IsNotNull(mgr.IAAResponse);
        }

        [Test]
        public void Constructor_ValidIAA_SetsIAAResponse()
        {
            var iaa = MakeIAA("admob");
            var mgr = new MediationManager(new NullAdPlaceholderUI(), iaa);
            Assert.AreEqual(iaa, mgr.IAAResponse);
        }

        // ─── Property defaults (no native SDK) ───────────────────────────────

        [Test]
        public void IsHybridMode_DefaultsFalse_WhenNoOrchestrator()
        {
            var mgr = new MediationManager(new NullAdPlaceholderUI(), MakeIAA());
            Assert.IsFalse(mgr.IsHybridMode);
        }

        [Test]
        public void MediationType_DefaultsNull_WhenNoNetworkInit()
        {
            var mgr = new MediationManager(new NullAdPlaceholderUI(), MakeIAA());
            Assert.IsNull(mgr.MediationType);
        }

        [Test]
        public void AppOpenManager_DefaultsNull_WhenNoNetworkInit()
        {
            var mgr = new MediationManager(new NullAdPlaceholderUI(), MakeIAA());
            Assert.IsNull(mgr.AppOpenManager);
        }

        // ─── GetSegmentKey ────────────────────────────────────────────────────

        [Test]
        public void GetSegmentKey_ReturnsNotInitialized_WhenNoSegmentManager()
        {
            var mgr = new MediationManager(new NullAdPlaceholderUI(), MakeIAA());
            Assert.AreEqual("not initialized", mgr.GetSegmentKey());
        }

        [Test]
        public void GetSegmentKey_ReturnsNotInitialized_WhenNullIAA()
        {
            var mgr = new MediationManager(new NullAdPlaceholderUI(), null);
            Assert.AreEqual("not initialized", mgr.GetSegmentKey());
        }

        // ─── GetExperimentAssignments ─────────────────────────────────────────

        [Test]
        public void GetExperimentAssignments_ReturnsEmpty_WhenNoExperiments()
        {
            var mgr = new MediationManager(new NullAdPlaceholderUI(), MakeIAA());
            var assignments = mgr.GetExperimentAssignments();
            Assert.IsNotNull(assignments);
            Assert.AreEqual(0, assignments.Count);
        }

        [Test]
        public void GetExperimentAssignments_ReturnsEmpty_WhenNullIAA()
        {
            var mgr = new MediationManager(new NullAdPlaceholderUI(), null);
            var assignments = mgr.GetExperimentAssignments();
            Assert.IsNotNull(assignments);
            Assert.AreEqual(0, assignments.Count);
        }

        [Test]
        public void GetExperimentAssignments_WithExperiments_ReadsPlayerPrefs()
        {
            var iaa = MakeIAA();
            iaa.AdExperiments = new List<AdExperimentConfig>
            {
                new AdExperimentConfig { ExperimentId = "exp_001", Enabled = true }
            };

            PlayerPrefs.SetString("NoctuaExp_exp_001_variant", "variant_a");
            PlayerPrefs.Save();

            var mgr = new MediationManager(new NullAdPlaceholderUI(), iaa);
            var assignments = mgr.GetExperimentAssignments();

            Assert.AreEqual(1, assignments.Count);
            Assert.AreEqual("variant_a", assignments["exp_001"]);
        }

        [Test]
        public void GetExperimentAssignments_DisabledExperiment_AppendsOffSuffix()
        {
            var iaa = MakeIAA();
            iaa.AdExperiments = new List<AdExperimentConfig>
            {
                new AdExperimentConfig { ExperimentId = "exp_002", Enabled = false }
            };

            PlayerPrefs.SetString("NoctuaExp_exp_002_variant", "ctrl");
            PlayerPrefs.Save();

            var mgr = new MediationManager(new NullAdPlaceholderUI(), iaa);
            var assignments = mgr.GetExperimentAssignments();

            Assert.IsTrue(assignments["exp_002"].Contains("[off]"));
        }

        [Test]
        public void GetExperimentAssignments_MissingPlayerPrefsKey_UsesUnassigned()
        {
            var iaa = MakeIAA();
            iaa.AdExperiments = new List<AdExperimentConfig>
            {
                new AdExperimentConfig { ExperimentId = "exp_new", Enabled = true }
            };

            var mgr = new MediationManager(new NullAdPlaceholderUI(), iaa);
            var assignments = mgr.GetExperimentAssignments();

            Assert.AreEqual("unassigned", assignments["exp_new"]);
        }

        // ─── GetCpmFloorStatus ────────────────────────────────────────────────

        [Test]
        public void GetCpmFloorStatus_ReturnsCpmFloorsDisabled_WhenNoFloorManager()
        {
            var mgr = new MediationManager(new NullAdPlaceholderUI(), MakeIAA());
            var status = mgr.GetCpmFloorStatus();
            Assert.IsTrue(status.ContainsKey("status"));
            Assert.AreEqual("CPM floors disabled", status["status"]);
        }

        // ─── SetAdRevenueTracker ──────────────────────────────────────────────

        [Test]
        public void SetAdRevenueTracker_WithNullRevenueTrackingManager_DoesNotThrow()
        {
            var mgr = new MediationManager(new NullAdPlaceholderUI(), null);
            Assert.DoesNotThrow(() => mgr.SetAdRevenueTracker(null));
        }

        // ─── SetCountryCode ───────────────────────────────────────────────────

        [Test]
        public void SetCountryCode_WithNoOrchestrator_DoesNotThrow()
        {
            var mgr = new MediationManager(new NullAdPlaceholderUI(), MakeIAA());
            Assert.DoesNotThrow(() => mgr.SetCountryCode("ID"));
        }

        // ─── RecordPurchase ───────────────────────────────────────────────────

        [Test]
        public void RecordPurchase_WithNullSegmentManager_DoesNotThrow()
        {
            var mgr = new MediationManager(new NullAdPlaceholderUI(), MakeIAA());
            Assert.DoesNotThrow(() => mgr.RecordPurchase());
        }

        // ─── GetSegmentManager ────────────────────────────────────────────────

        [Test]
        public void GetSegmentManager_ReturnsNull_WhenNoNetworkInit()
        {
            var mgr = new MediationManager(new NullAdPlaceholderUI(), MakeIAA());
            Assert.IsNull(mgr.GetSegmentManager());
        }

        // ─── ApplyExperimentOverride ──────────────────────────────────────────

        [Test]
        public void ApplyExperimentOverride_NullOverride_DoesNotThrow()
        {
            var mgr = new MediationManager(new NullAdPlaceholderUI(), MakeIAA());
            Assert.DoesNotThrow(() => mgr.ApplyExperimentOverride(null));
        }

        [Test]
        public void ApplyExperimentOverride_WithConfig_UpdatesFrequencyManager()
        {
            var mgr = new MediationManager(new NullAdPlaceholderUI(), MakeIAA());
            var overrideIaa = MakeIAA();
            overrideIaa.FrequencyCaps = new FrequencyCapConfig { Interstitial = new FrequencyCapEntry { MaxImpressions = 5, WindowSeconds = 60 } };
            Assert.DoesNotThrow(() => mgr.ApplyExperimentOverride(overrideIaa));
        }

        [Test]
        public void ApplyExperimentOverride_NullCooldownSeconds_DefaultsAppOpen30()
        {
            var mgr = new MediationManager(new NullAdPlaceholderUI(), MakeIAA());
            var overrideIaa = MakeIAA();
            overrideIaa.CooldownSeconds = null;
            Assert.DoesNotThrow(() => mgr.ApplyExperimentOverride(overrideIaa));
        }

        [Test]
        public void ApplyExperimentOverride_EnabledCpmFloors_CreatesCpmFloorManager()
        {
            var mgr = new MediationManager(new NullAdPlaceholderUI(), MakeIAA());
            var overrideIaa = MakeIAA();
            overrideIaa.CpmFloors = new CpmFloorConfig { Enabled = true };
            Assert.DoesNotThrow(() => mgr.ApplyExperimentOverride(overrideIaa));
        }

        [Test]
        public void ApplyExperimentOverride_DisabledCpmFloors_SetsCpmFloorManagerToNull()
        {
            var mgr = new MediationManager(new NullAdPlaceholderUI(), MakeIAA());
            var overrideIaa = MakeIAA();
            overrideIaa.CpmFloors = new CpmFloorConfig { Enabled = false };
            Assert.DoesNotThrow(() => mgr.ApplyExperimentOverride(overrideIaa));

            var status = mgr.GetCpmFloorStatus();
            Assert.AreEqual("CPM floors disabled", status["status"]);
        }
    }
}
