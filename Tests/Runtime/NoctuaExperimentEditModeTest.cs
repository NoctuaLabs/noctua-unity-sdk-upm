using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// EditMode NUnit tests for <see cref="NoctuaExperiment"/> — the static in-memory
    /// A/B-test flag / session-tag store.
    ///
    /// <c>NoctuaExperiment</c> is a pure static class with zero Unity dependencies
    /// (no <c>using UnityEngine</c>).  All 15 tests in <c>NoctuaExperimentTest</c>
    /// use <c>[UnityTest]</c> / <c>yield return null</c> despite being entirely
    /// synchronous dictionary operations — PlayMode only, zero EditMode coverage.
    ///
    /// These plain <c>[Test]</c> counterparts cover the core CRUD use-cases not yet
    /// addressed by the 12 existing EditMode tests in <c>NoctuaExperimentEdgeCaseTest</c>.
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
    public class NoctuaExperimentEditModeTest
    {
        [SetUp]
        public void SetUp() => NoctuaExperiment.Clear();

        [TearDown]
        public void TearDown() => NoctuaExperiment.Clear();

        // ─── SetFlag / GetFlag — typed round-trips ────────────────────────────

        [Test]
        public void SetFlag_GetFlag_String_RoundTrip()
        {
            NoctuaExperiment.SetFlag("key1", "value1");
            Assert.AreEqual("value1", NoctuaExperiment.GetFlag<string>("key1"));
        }

        [Test]
        public void SetFlag_GetFlag_Int_RoundTrip()
        {
            NoctuaExperiment.SetFlag("intKey", 42);
            Assert.AreEqual(42, NoctuaExperiment.GetFlag<int>("intKey"));
        }

        [Test]
        public void SetFlag_GetFlag_Bool_RoundTrip()
        {
            NoctuaExperiment.SetFlag("boolKey", true);
            Assert.IsTrue(NoctuaExperiment.GetFlag<bool>("boolKey"));
        }

        // ─── GetFlag — missing key returns typed defaults ─────────────────────

        [Test]
        public void GetFlag_MissingStringKey_ReturnsNull()
        {
            Assert.IsNull(NoctuaExperiment.GetFlag<string>("missing"));
        }

        [Test]
        public void GetFlag_MissingIntKey_ReturnsZero()
        {
            Assert.AreEqual(0, NoctuaExperiment.GetFlag<int>("missing"));
        }

        [Test]
        public void GetFlag_MissingBoolKey_ReturnsFalse()
        {
            Assert.IsFalse(NoctuaExperiment.GetFlag<bool>("missing"));
        }

        [Test]
        public void GetFlag_MissingKey_WithCustomDefault_ReturnsCustomDefault()
        {
            Assert.AreEqual("fallback", NoctuaExperiment.GetFlag("missing", "fallback"));
        }

        // ─── Clear ────────────────────────────────────────────────────────────

        [Test]
        public void Clear_RemovesAllPreviouslySetFlags()
        {
            NoctuaExperiment.SetFlag("a", "1");
            NoctuaExperiment.SetFlag("b", "2");
            NoctuaExperiment.Clear();

            Assert.IsNull(NoctuaExperiment.GetFlag<string>("a"));
            Assert.IsNull(NoctuaExperiment.GetFlag<string>("b"));
        }

        // ─── SetSessionId / GetSessionId ──────────────────────────────────────

        [Test]
        public void SetSessionId_GetSessionId_RoundTrip()
        {
            NoctuaExperiment.SetSessionId("session-123");
            Assert.AreEqual("session-123", NoctuaExperiment.GetSessionId());
        }

        [Test]
        public void GetSessionId_BeforeAnySet_ReturnsEmptyString()
        {
            Assert.AreEqual(string.Empty, NoctuaExperiment.GetSessionId());
        }

        // ─── SetExperiment / GetActiveExperiment ──────────────────────────────

        [Test]
        public void SetExperiment_GetActiveExperiment_RoundTrip()
        {
            NoctuaExperiment.SetExperiment("exp-abc");
            Assert.AreEqual("exp-abc", NoctuaExperiment.GetActiveExperiment());
        }

        [Test]
        public void GetActiveExperiment_BeforeAnySet_ReturnsEmptyString()
        {
            Assert.AreEqual(string.Empty, NoctuaExperiment.GetActiveExperiment());
        }

        // ─── SetCurrentFeature / GetCurrentFeature ────────────────────────────

        [Test]
        public void SetCurrentFeature_GetCurrentFeature_RoundTrip()
        {
            NoctuaExperiment.SetCurrentFeature("feature-x");
            Assert.AreEqual("feature-x", NoctuaExperiment.GetCurrentFeature());
        }

        [Test]
        public void GetCurrentFeature_BeforeAnySet_ReturnsEmptyString()
        {
            Assert.AreEqual(string.Empty, NoctuaExperiment.GetCurrentFeature());
        }

        // ─── SetGeneralExperiment / GetGeneralExperiment ──────────────────────

        [Test]
        public void SetGeneralExperiment_GetGeneralExperiment_RoundTrip()
        {
            NoctuaExperiment.SetGeneralExperiment("custom_key", "custom_value");
            Assert.AreEqual("custom_value", NoctuaExperiment.GetGeneralExperiment("custom_key"));
        }

        [Test]
        public void GetGeneralExperiment_MissingKey_ReturnsEmptyString()
        {
            Assert.AreEqual(string.Empty, NoctuaExperiment.GetGeneralExperiment("no_such_key"));
        }

        // ─── SetFlag — overwrite existing key ─────────────────────────────────

        [Test]
        public void SetFlag_OverwritesExistingKey_LastValueWins()
        {
            NoctuaExperiment.SetFlag("key", "original");
            NoctuaExperiment.SetFlag("key", "updated");
            Assert.AreEqual("updated", NoctuaExperiment.GetFlag<string>("key"));
        }

        // ─── Snapshot ─────────────────────────────────────────────────────────

        [Test]
        public void Snapshot_EmptyManager_ReturnsEmptyDictionary()
        {
            var snap = NoctuaExperiment.Snapshot();
            Assert.IsNotNull(snap);
            Assert.AreEqual(0, snap.Count);
        }

        [Test]
        public void Snapshot_WithFlags_ReturnsAllFlags()
        {
            NoctuaExperiment.SetFlag("a", "1");
            NoctuaExperiment.SetFlag("b", 42);
            var snap = NoctuaExperiment.Snapshot();

            Assert.AreEqual(2, snap.Count);
            Assert.AreEqual("1", snap["a"]);
            Assert.AreEqual(42, snap["b"]);
        }

        [Test]
        public void Snapshot_ReturnsIndependentCopy_MutatingOriginalDoesNotAffectSnap()
        {
            NoctuaExperiment.SetFlag("x", "first");
            var snap = NoctuaExperiment.Snapshot();

            // Modify the live state after taking the snapshot
            NoctuaExperiment.SetFlag("x", "second");

            // Snapshot must still reflect the old value
            Assert.AreEqual("first", snap["x"],
                "Snapshot must return an independent copy — live changes must not affect it");
        }

        [Test]
        public void Snapshot_AfterClear_ReturnsEmptyDictionary()
        {
            NoctuaExperiment.SetFlag("p", "q");
            NoctuaExperiment.Clear();
            var snap = NoctuaExperiment.Snapshot();
            Assert.AreEqual(0, snap.Count);
        }

        // ─── SetGeneralExperiment — overwrite ────────────────────────────────

        [Test]
        public void SetGeneralExperiment_OverwritesExistingKey()
        {
            NoctuaExperiment.SetGeneralExperiment("custom_key", "v1");
            NoctuaExperiment.SetGeneralExperiment("custom_key", "v2");
            Assert.AreEqual("v2", NoctuaExperiment.GetGeneralExperiment("custom_key"));
        }
    }
}
