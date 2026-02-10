using System.Collections;
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
        public IEnumerator SetSessionTag_GetSessionTag()
        {
            ExperimentManager.SetSessionTag("feature-x");
            Assert.AreEqual("feature-x", ExperimentManager.GetSessionTag());
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetSessionTag_Default_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, ExperimentManager.GetSessionTag());
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
}
