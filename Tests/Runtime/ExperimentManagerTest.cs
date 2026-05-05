using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.Runtime
{
    public class ExperimentManagerTest
    {
        [SetUp]
        public void SetUp()
        {
            ExperimentManager.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            ExperimentManager.Clear();
        }

        [UnityTest]
        public IEnumerator SetFlag_GetFlag_String()
        {
            ExperimentManager.SetFlag("key1", "value1");
            var result = ExperimentManager.GetFlag<string>("key1");
            Assert.AreEqual("value1", result);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SetFlag_GetFlag_Int()
        {
            ExperimentManager.SetFlag("intKey", 42);
            var result = ExperimentManager.GetFlag<int>("intKey");
            Assert.AreEqual(42, result);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SetFlag_GetFlag_Bool()
        {
            ExperimentManager.SetFlag("boolKey", true);
            var result = ExperimentManager.GetFlag<bool>("boolKey");
            Assert.IsTrue(result);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetFlag_MissingKey_ReturnsDefault()
        {
            var stringResult = ExperimentManager.GetFlag<string>("missing");
            Assert.IsNull(stringResult);

            var intResult = ExperimentManager.GetFlag<int>("missing");
            Assert.AreEqual(0, intResult);

            var boolResult = ExperimentManager.GetFlag<bool>("missing");
            Assert.IsFalse(boolResult);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetFlag_MissingKey_ReturnsCustomDefault()
        {
            var result = ExperimentManager.GetFlag("missing", "fallback");
            Assert.AreEqual("fallback", result);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Clear_RemovesAllFlags()
        {
            ExperimentManager.SetFlag("a", "1");
            ExperimentManager.SetFlag("b", "2");
            ExperimentManager.Clear();

            Assert.IsNull(ExperimentManager.GetFlag<string>("a"));
            Assert.IsNull(ExperimentManager.GetFlag<string>("b"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SetSessionId_GetSessionId()
        {
            ExperimentManager.SetSessionId("session-123");
            Assert.AreEqual("session-123", ExperimentManager.GetSessionId());
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetSessionId_Default_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, ExperimentManager.GetSessionId());
            yield return null;
        }

        [UnityTest]
        public IEnumerator SetExperiment_GetActiveExperiment()
        {
            ExperimentManager.SetExperiment("exp-abc");
            Assert.AreEqual("exp-abc", ExperimentManager.GetActiveExperiment());
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetActiveExperiment_Default_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, ExperimentManager.GetActiveExperiment());
            yield return null;
        }

        [UnityTest]
        public IEnumerator SetCurrentFeature_GetCurrentFeature()
        {
            ExperimentManager.SetCurrentFeature("feature-x");
            Assert.AreEqual("feature-x", ExperimentManager.GetCurrentFeature());
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetCurrentFeature_Default_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, ExperimentManager.GetCurrentFeature());
            yield return null;
        }

        [UnityTest]
        public IEnumerator SetGeneralExperiment_GetGeneralExperiment()
        {
            ExperimentManager.SetGeneralExperiment("custom_key", "custom_value");
            Assert.AreEqual("custom_value", ExperimentManager.GetGeneralExperiment("custom_key"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetGeneralExperiment_MissingKey_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, ExperimentManager.GetGeneralExperiment("nonexistent"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SetFlag_OverwritesExistingKey()
        {
            ExperimentManager.SetFlag("key", "old");
            ExperimentManager.SetFlag("key", "new");
            Assert.AreEqual("new", ExperimentManager.GetFlag<string>("key"));
            yield return null;
        }
    }

    [TestFixture]
    public class ExperimentManagerEdgeCaseTest
    {
        [SetUp]
        public void SetUp()
        {
            ExperimentManager.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            ExperimentManager.Clear();
        }

        // 1. Clear() after SetExperiment — GetActiveExperiment() returns empty string (the default)
        [Test]
        public void Clear_AfterSetExperiment_GetActiveExperiment_ReturnsEmpty()
        {
            ExperimentManager.SetExperiment("exp-xyz");
            ExperimentManager.Clear();
            Assert.AreEqual(string.Empty, ExperimentManager.GetActiveExperiment());
        }

        // 2. SetExperiment(null) — does not throw, clears active experiment back to empty
        [Test]
        public void SetExperiment_Null_DoesNotThrow_AndClearsActiveExperiment()
        {
            ExperimentManager.SetExperiment("initial");
            Assert.DoesNotThrow(() => ExperimentManager.SetExperiment(null));
            // After setting null, the stored value is null; GetFlag with defaultValue=empty returns null
            // cast to string succeeds. The result is either null or empty — both are acceptable "cleared" states.
            var result = ExperimentManager.GetActiveExperiment();
            // GetActiveExperiment() calls GetFlag<string>(KEY, string.Empty) — if value is null,
            // (string)null is returned (not the default), so result is null or empty.
            Assert.IsTrue(result == null || result == string.Empty,
                $"Expected null or empty after SetExperiment(null), got '{result}'");
        }

        // 3. SetExperiment("") — behaves like null/clear
        [Test]
        public void SetExperiment_Empty_StoresEmptyAndGetActiveExperimentReturnsEmpty()
        {
            ExperimentManager.SetExperiment("initial");
            ExperimentManager.SetExperiment("");
            var result = ExperimentManager.GetActiveExperiment();
            // empty string stored; GetFlag returns it (not the defaultValue since key exists)
            Assert.IsTrue(result == "" || result == null,
                $"Expected empty or null after SetExperiment(\"\"), got '{result}'");
        }

        // 4. GetActiveExperiment() before any Set — returns empty string (default)
        [Test]
        public void GetActiveExperiment_BeforeAnySet_ReturnsEmpty()
        {
            // SetUp calls Clear() so state is fresh
            Assert.AreEqual(string.Empty, ExperimentManager.GetActiveExperiment());
        }

        // 5. SetExperiment multiple times — last value wins
        [Test]
        public void SetExperiment_MultipleTimes_LastValueWins()
        {
            ExperimentManager.SetExperiment("first");
            ExperimentManager.SetExperiment("second");
            ExperimentManager.SetExperiment("third");
            Assert.AreEqual("third", ExperimentManager.GetActiveExperiment());
        }

        // 6. GetSessionId() after SetSessionId — returns non-null, non-empty string
        [Test]
        public void GetSessionId_AfterSet_ReturnsNonNullString()
        {
            ExperimentManager.SetSessionId("session-abc");
            var result = ExperimentManager.GetSessionId();
            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result);
            Assert.AreEqual("session-abc", result);
        }

        // 7. Static state is isolated: Clear() then GetActiveExperiment() returns empty
        [Test]
        public void StaticState_AfterClear_GetActiveExperiment_ReturnsEmpty()
        {
            ExperimentManager.SetExperiment("isolated-test");
            ExperimentManager.SetSessionId("some-session");
            ExperimentManager.SetCurrentFeature("some-feature");

            ExperimentManager.Clear();

            Assert.AreEqual(string.Empty, ExperimentManager.GetActiveExperiment());
            Assert.AreEqual(string.Empty, ExperimentManager.GetSessionId());
            Assert.AreEqual(string.Empty, ExperimentManager.GetCurrentFeature());
        }

        // 8. Sequential calls work correctly — no thread-safety guarantees required
        [Test]
        public void SequentialSetAndGet_WorksCorrectly()
        {
            for (int i = 0; i < 10; i++)
            {
                ExperimentManager.SetExperiment($"exp-{i}");
                Assert.AreEqual($"exp-{i}", ExperimentManager.GetActiveExperiment());
            }
        }

        // 9. Snapshot() after Clear — returns empty dictionary
        [Test]
        public void Snapshot_AfterClear_ReturnsEmptyDictionary()
        {
            ExperimentManager.SetFlag("a", "1");
            ExperimentManager.Clear();
            var snapshot = ExperimentManager.Snapshot();
            Assert.IsNotNull(snapshot);
            Assert.AreEqual(0, snapshot.Count);
        }

        // 10. Snapshot() contains all set flags
        [Test]
        public void Snapshot_ContainsAllSetFlags()
        {
            ExperimentManager.SetFlag("x", "hello");
            ExperimentManager.SetFlag("y", 42);
            var snapshot = ExperimentManager.Snapshot();
            Assert.IsTrue(snapshot.ContainsKey("x"));
            Assert.IsTrue(snapshot.ContainsKey("y"));
            Assert.AreEqual("hello", snapshot["x"]);
            Assert.AreEqual(42, snapshot["y"]);
        }

        // 11. GeneralExperiment: SetGeneralExperiment then GetGeneralExperiment — sequential overwrites
        [Test]
        public void GeneralExperiment_SequentialOverwrites_LastValueWins()
        {
            ExperimentManager.SetGeneralExperiment("mode", "a");
            ExperimentManager.SetGeneralExperiment("mode", "b");
            ExperimentManager.SetGeneralExperiment("mode", "c");
            Assert.AreEqual("c", ExperimentManager.GetGeneralExperiment("mode"));
        }

        // 12. GetGeneralExperiment for unknown key after Clear — returns empty string
        [Test]
        public void GetGeneralExperiment_UnknownKey_AfterClear_ReturnsEmpty()
        {
            ExperimentManager.Clear();
            Assert.AreEqual(string.Empty, ExperimentManager.GetGeneralExperiment("no_such_key"));
        }
    }
}
