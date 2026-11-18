using System.Collections;
using System.Collections.Generic;
using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.Runtime
{
    public class NoctuaExperimentTest
    {
        [SetUp]
        public void SetUp()
        {
            NoctuaExperiment.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            NoctuaExperiment.Clear();
        }

        [UnityTest]
        public IEnumerator SetFlag_GetFlag_String()
        {
            NoctuaExperiment.SetFlag("key1", "value1");
            var result = NoctuaExperiment.GetFlag<string>("key1");
            Assert.AreEqual("value1", result);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SetFlag_GetFlag_Int()
        {
            NoctuaExperiment.SetFlag("intKey", 42);
            var result = NoctuaExperiment.GetFlag<int>("intKey");
            Assert.AreEqual(42, result);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SetFlag_GetFlag_Bool()
        {
            NoctuaExperiment.SetFlag("boolKey", true);
            var result = NoctuaExperiment.GetFlag<bool>("boolKey");
            Assert.IsTrue(result);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetFlag_MissingKey_ReturnsDefault()
        {
            var stringResult = NoctuaExperiment.GetFlag<string>("missing");
            Assert.IsNull(stringResult);

            var intResult = NoctuaExperiment.GetFlag<int>("missing");
            Assert.AreEqual(0, intResult);

            var boolResult = NoctuaExperiment.GetFlag<bool>("missing");
            Assert.IsFalse(boolResult);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetFlag_MissingKey_ReturnsCustomDefault()
        {
            var result = NoctuaExperiment.GetFlag("missing", "fallback");
            Assert.AreEqual("fallback", result);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Clear_RemovesAllFlags()
        {
            NoctuaExperiment.SetFlag("a", "1");
            NoctuaExperiment.SetFlag("b", "2");
            NoctuaExperiment.Clear();

            Assert.IsNull(NoctuaExperiment.GetFlag<string>("a"));
            Assert.IsNull(NoctuaExperiment.GetFlag<string>("b"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SetSessionId_GetSessionId()
        {
            NoctuaExperiment.SetSessionId("session-123");
            Assert.AreEqual("session-123", NoctuaExperiment.GetSessionId());
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetSessionId_Default_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, NoctuaExperiment.GetSessionId());
            yield return null;
        }

        [UnityTest]
        public IEnumerator SetExperiment_GetActiveExperiment()
        {
            NoctuaExperiment.SetExperiment("exp-abc");
            Assert.AreEqual("exp-abc", NoctuaExperiment.GetActiveExperiment());
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetActiveExperiment_Default_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, NoctuaExperiment.GetActiveExperiment());
            yield return null;
        }

        [UnityTest]
        public IEnumerator SetCurrentFeature_GetCurrentFeature()
        {
            NoctuaExperiment.SetCurrentFeature("feature-x");
            Assert.AreEqual("feature-x", NoctuaExperiment.GetCurrentFeature());
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetCurrentFeature_Default_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, NoctuaExperiment.GetCurrentFeature());
            yield return null;
        }

        [UnityTest]
        public IEnumerator SetGeneralExperiment_GetGeneralExperiment()
        {
            NoctuaExperiment.SetGeneralExperiment("custom_key", "custom_value");
            Assert.AreEqual("custom_value", NoctuaExperiment.GetGeneralExperiment("custom_key"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetGeneralExperiment_MissingKey_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, NoctuaExperiment.GetGeneralExperiment("nonexistent"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SetFlag_OverwritesExistingKey()
        {
            NoctuaExperiment.SetFlag("key", "old");
            NoctuaExperiment.SetFlag("key", "new");
            Assert.AreEqual("new", NoctuaExperiment.GetFlag<string>("key"));
            yield return null;
        }
    }

    [TestFixture]
    public class NoctuaExperimentEdgeCaseTest
    {
        [SetUp]
        public void SetUp()
        {
            NoctuaExperiment.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            NoctuaExperiment.Clear();
        }

        // 1. Clear() after SetExperiment — GetActiveExperiment() returns empty string (the default)
        [Test]
        public void Clear_AfterSetExperiment_GetActiveExperiment_ReturnsEmpty()
        {
            NoctuaExperiment.SetExperiment("exp-xyz");
            NoctuaExperiment.Clear();
            Assert.AreEqual(string.Empty, NoctuaExperiment.GetActiveExperiment());
        }

        // 2. SetExperiment(null) — does not throw, clears active experiment back to empty
        [Test]
        public void SetExperiment_Null_DoesNotThrow_AndClearsActiveExperiment()
        {
            NoctuaExperiment.SetExperiment("initial");
            Assert.DoesNotThrow(() => NoctuaExperiment.SetExperiment(null));
            // After setting null, the stored value is null; GetFlag with defaultValue=empty returns null
            // cast to string succeeds. The result is either null or empty — both are acceptable "cleared" states.
            var result = NoctuaExperiment.GetActiveExperiment();
            // GetActiveExperiment() calls GetFlag<string>(KEY, string.Empty) — if value is null,
            // (string)null is returned (not the default), so result is null or empty.
            Assert.IsTrue(result == null || result == string.Empty,
                $"Expected null or empty after SetExperiment(null), got '{result}'");
        }

        // 3. SetExperiment("") — behaves like null/clear
        [Test]
        public void SetExperiment_Empty_StoresEmptyAndGetActiveExperimentReturnsEmpty()
        {
            NoctuaExperiment.SetExperiment("initial");
            NoctuaExperiment.SetExperiment("");
            var result = NoctuaExperiment.GetActiveExperiment();
            // empty string stored; GetFlag returns it (not the defaultValue since key exists)
            Assert.IsTrue(result == "" || result == null,
                $"Expected empty or null after SetExperiment(\"\"), got '{result}'");
        }

        // 4. GetActiveExperiment() before any Set — returns empty string (default)
        [Test]
        public void GetActiveExperiment_BeforeAnySet_ReturnsEmpty()
        {
            // SetUp calls Clear() so state is fresh
            Assert.AreEqual(string.Empty, NoctuaExperiment.GetActiveExperiment());
        }

        // 5. SetExperiment multiple times — last value wins
        [Test]
        public void SetExperiment_MultipleTimes_LastValueWins()
        {
            NoctuaExperiment.SetExperiment("first");
            NoctuaExperiment.SetExperiment("second");
            NoctuaExperiment.SetExperiment("third");
            Assert.AreEqual("third", NoctuaExperiment.GetActiveExperiment());
        }

        // 6. GetSessionId() after SetSessionId — returns non-null, non-empty string
        [Test]
        public void GetSessionId_AfterSet_ReturnsNonNullString()
        {
            NoctuaExperiment.SetSessionId("session-abc");
            var result = NoctuaExperiment.GetSessionId();
            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result);
            Assert.AreEqual("session-abc", result);
        }

        // 7. Static state is isolated: Clear() then GetActiveExperiment() returns empty
        [Test]
        public void StaticState_AfterClear_GetActiveExperiment_ReturnsEmpty()
        {
            NoctuaExperiment.SetExperiment("isolated-test");
            NoctuaExperiment.SetSessionId("some-session");
            NoctuaExperiment.SetCurrentFeature("some-feature");

            NoctuaExperiment.Clear();

            Assert.AreEqual(string.Empty, NoctuaExperiment.GetActiveExperiment());
            Assert.AreEqual(string.Empty, NoctuaExperiment.GetSessionId());
            Assert.AreEqual(string.Empty, NoctuaExperiment.GetCurrentFeature());
        }

        // 8. Sequential calls work correctly — no thread-safety guarantees required
        [Test]
        public void SequentialSetAndGet_WorksCorrectly()
        {
            for (int i = 0; i < 10; i++)
            {
                NoctuaExperiment.SetExperiment($"exp-{i}");
                Assert.AreEqual($"exp-{i}", NoctuaExperiment.GetActiveExperiment());
            }
        }

        // 9. Snapshot() after Clear — returns empty dictionary
        [Test]
        public void Snapshot_AfterClear_ReturnsEmptyDictionary()
        {
            NoctuaExperiment.SetFlag("a", "1");
            NoctuaExperiment.Clear();
            var snapshot = NoctuaExperiment.Snapshot();
            Assert.IsNotNull(snapshot);
            Assert.AreEqual(0, snapshot.Count);
        }

        // 10. Snapshot() contains all set flags
        [Test]
        public void Snapshot_ContainsAllSetFlags()
        {
            NoctuaExperiment.SetFlag("x", "hello");
            NoctuaExperiment.SetFlag("y", 42);
            var snapshot = NoctuaExperiment.Snapshot();
            Assert.IsTrue(snapshot.ContainsKey("x"));
            Assert.IsTrue(snapshot.ContainsKey("y"));
            Assert.AreEqual("hello", snapshot["x"]);
            Assert.AreEqual(42, snapshot["y"]);
        }

        // 11. GeneralExperiment: SetGeneralExperiment then GetGeneralExperiment — sequential overwrites
        [Test]
        public void GeneralExperiment_SequentialOverwrites_LastValueWins()
        {
            NoctuaExperiment.SetGeneralExperiment("mode", "a");
            NoctuaExperiment.SetGeneralExperiment("mode", "b");
            NoctuaExperiment.SetGeneralExperiment("mode", "c");
            Assert.AreEqual("c", NoctuaExperiment.GetGeneralExperiment("mode"));
        }

        // 12. GetGeneralExperiment for unknown key after Clear — returns empty string
        [Test]
        public void GetGeneralExperiment_UnknownKey_AfterClear_ReturnsEmpty()
        {
            NoctuaExperiment.Clear();
            Assert.AreEqual(string.Empty, NoctuaExperiment.GetGeneralExperiment("no_such_key"));
        }
    }
}
