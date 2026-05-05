using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// EditMode NUnit tests for <see cref="ExperimentManager"/> — the static in-memory
    /// A/B-test flag / session-tag store.
    ///
    /// <c>ExperimentManager</c> is a pure static class with zero Unity dependencies
    /// (no <c>using UnityEngine</c>).  All 15 tests in <c>ExperimentManagerTest</c>
    /// use <c>[UnityTest]</c> / <c>yield return null</c> despite being entirely
    /// synchronous dictionary operations — PlayMode only, zero EditMode coverage.
    ///
    /// These plain <c>[Test]</c> counterparts cover the core CRUD use-cases not yet
    /// addressed by the 12 existing EditMode tests in <c>ExperimentManagerEdgeCaseTest</c>.
    ///
    /// Covered:
    ///   — SetFlag / GetFlag&lt;T&gt; for string, int, bool
    ///   — GetFlag with missing key: typed defaults (null / 0 / false)
    ///   — GetFlag with custom default value
    ///   — Clear removes all flags
    ///   — SetSessionId / GetSessionId round-trip and default
    ///   — SetExperiment / GetActiveExperiment round-trip and default
    ///   — SetCurrentFeature / GetCurrentFeature round-trip and default
    ///   — SetGeneralExperiment / GetGeneralExperiment round-trip and missing-key
    ///   — SetFlag overwrites an existing key
    /// </summary>
    [TestFixture]
    public class ExperimentManagerEditModeTest
    {
        [SetUp]
        public void SetUp() => ExperimentManager.Clear();

        [TearDown]
        public void TearDown() => ExperimentManager.Clear();

        // ─── SetFlag / GetFlag — typed round-trips ────────────────────────────

        [Test]
        public void SetFlag_GetFlag_String_RoundTrip()
        {
            ExperimentManager.SetFlag("key1", "value1");
            Assert.AreEqual("value1", ExperimentManager.GetFlag<string>("key1"));
        }

        [Test]
        public void SetFlag_GetFlag_Int_RoundTrip()
        {
            ExperimentManager.SetFlag("intKey", 42);
            Assert.AreEqual(42, ExperimentManager.GetFlag<int>("intKey"));
        }

        [Test]
        public void SetFlag_GetFlag_Bool_RoundTrip()
        {
            ExperimentManager.SetFlag("boolKey", true);
            Assert.IsTrue(ExperimentManager.GetFlag<bool>("boolKey"));
        }

        // ─── GetFlag — missing key returns typed defaults ─────────────────────

        [Test]
        public void GetFlag_MissingStringKey_ReturnsNull()
        {
            Assert.IsNull(ExperimentManager.GetFlag<string>("missing"));
        }

        [Test]
        public void GetFlag_MissingIntKey_ReturnsZero()
        {
            Assert.AreEqual(0, ExperimentManager.GetFlag<int>("missing"));
        }

        [Test]
        public void GetFlag_MissingBoolKey_ReturnsFalse()
        {
            Assert.IsFalse(ExperimentManager.GetFlag<bool>("missing"));
        }

        [Test]
        public void GetFlag_MissingKey_WithCustomDefault_ReturnsCustomDefault()
        {
            Assert.AreEqual("fallback", ExperimentManager.GetFlag("missing", "fallback"));
        }

        // ─── Clear ────────────────────────────────────────────────────────────

        [Test]
        public void Clear_RemovesAllPreviouslySetFlags()
        {
            ExperimentManager.SetFlag("a", "1");
            ExperimentManager.SetFlag("b", "2");
            ExperimentManager.Clear();

            Assert.IsNull(ExperimentManager.GetFlag<string>("a"));
            Assert.IsNull(ExperimentManager.GetFlag<string>("b"));
        }

        // ─── SetSessionId / GetSessionId ──────────────────────────────────────

        [Test]
        public void SetSessionId_GetSessionId_RoundTrip()
        {
            ExperimentManager.SetSessionId("session-123");
            Assert.AreEqual("session-123", ExperimentManager.GetSessionId());
        }

        [Test]
        public void GetSessionId_BeforeAnySet_ReturnsEmptyString()
        {
            Assert.AreEqual(string.Empty, ExperimentManager.GetSessionId());
        }

        // ─── SetExperiment / GetActiveExperiment ──────────────────────────────

        [Test]
        public void SetExperiment_GetActiveExperiment_RoundTrip()
        {
            ExperimentManager.SetExperiment("exp-abc");
            Assert.AreEqual("exp-abc", ExperimentManager.GetActiveExperiment());
        }

        [Test]
        public void GetActiveExperiment_BeforeAnySet_ReturnsEmptyString()
        {
            Assert.AreEqual(string.Empty, ExperimentManager.GetActiveExperiment());
        }

        // ─── SetCurrentFeature / GetCurrentFeature ────────────────────────────

        [Test]
        public void SetCurrentFeature_GetCurrentFeature_RoundTrip()
        {
            ExperimentManager.SetCurrentFeature("feature-x");
            Assert.AreEqual("feature-x", ExperimentManager.GetCurrentFeature());
        }

        [Test]
        public void GetCurrentFeature_BeforeAnySet_ReturnsEmptyString()
        {
            Assert.AreEqual(string.Empty, ExperimentManager.GetCurrentFeature());
        }

        // ─── SetGeneralExperiment / GetGeneralExperiment ──────────────────────

        [Test]
        public void SetGeneralExperiment_GetGeneralExperiment_RoundTrip()
        {
            ExperimentManager.SetGeneralExperiment("custom_key", "custom_value");
            Assert.AreEqual("custom_value", ExperimentManager.GetGeneralExperiment("custom_key"));
        }

        [Test]
        public void GetGeneralExperiment_MissingKey_ReturnsEmptyString()
        {
            Assert.AreEqual(string.Empty, ExperimentManager.GetGeneralExperiment("no_such_key"));
        }

        // ─── SetFlag — overwrite existing key ─────────────────────────────────

        [Test]
        public void SetFlag_OverwritesExistingKey_LastValueWins()
        {
            ExperimentManager.SetFlag("key", "original");
            ExperimentManager.SetFlag("key", "updated");
            Assert.AreEqual("updated", ExperimentManager.GetFlag<string>("key"));
        }

        // ─── Snapshot ─────────────────────────────────────────────────────────

        [Test]
        public void Snapshot_EmptyManager_ReturnsEmptyDictionary()
        {
            var snap = ExperimentManager.Snapshot();
            Assert.IsNotNull(snap);
            Assert.AreEqual(0, snap.Count);
        }

        [Test]
        public void Snapshot_WithFlags_ReturnsAllFlags()
        {
            ExperimentManager.SetFlag("a", "1");
            ExperimentManager.SetFlag("b", 42);
            var snap = ExperimentManager.Snapshot();

            Assert.AreEqual(2, snap.Count);
            Assert.AreEqual("1", snap["a"]);
            Assert.AreEqual(42, snap["b"]);
        }

        [Test]
        public void Snapshot_ReturnsIndependentCopy_MutatingOriginalDoesNotAffectSnap()
        {
            ExperimentManager.SetFlag("x", "first");
            var snap = ExperimentManager.Snapshot();

            // Modify the live state after taking the snapshot
            ExperimentManager.SetFlag("x", "second");

            // Snapshot must still reflect the old value
            Assert.AreEqual("first", snap["x"],
                "Snapshot must return an independent copy — live changes must not affect it");
        }

        [Test]
        public void Snapshot_AfterClear_ReturnsEmptyDictionary()
        {
            ExperimentManager.SetFlag("p", "q");
            ExperimentManager.Clear();
            var snap = ExperimentManager.Snapshot();
            Assert.AreEqual(0, snap.Count);
        }

        // ─── SetGeneralExperiment — overwrite ────────────────────────────────

        [Test]
        public void SetGeneralExperiment_OverwritesExistingKey()
        {
            ExperimentManager.SetGeneralExperiment("custom_key", "v1");
            ExperimentManager.SetGeneralExperiment("custom_key", "v2");
            Assert.AreEqual("v2", ExperimentManager.GetGeneralExperiment("custom_key"));
        }
    }
}
