using System.Collections.Generic;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// EditMode NUnit tests for <see cref="InspectorJson.Deserialize"/>.
    ///
    /// Covers:
    ///   — Null / empty / "{}" input → empty dictionary (no throw)
    ///   — Flat objects with each scalar type (string, long, double, bool, null)
    ///   — Nested objects recursed into <c>IReadOnlyDictionary</c>
    ///   — Array values recursed into <c>List&lt;object&gt;</c>
    ///   — Malformed JSON → empty dictionary (exception swallowed)
    ///   — Round-trip key preservation
    /// </summary>
    [TestFixture]
    public class InspectorJsonTest
    {
        // ═══════════════════════════════════════════════════════════════════
        // Null / empty / trivial inputs
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void Deserialize_Null_ReturnsEmptyDictionary()
        {
            var result = InspectorJson.Deserialize(null);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Deserialize_EmptyString_ReturnsEmptyDictionary()
        {
            var result = InspectorJson.Deserialize("");

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Deserialize_EmptyBraces_ReturnsEmptyDictionary()
        {
            var result = InspectorJson.Deserialize("{}");

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Deserialize_MalformedJson_ReturnsEmptyDictionary()
        {
            var result = InspectorJson.Deserialize("{not valid json!!!");

            Assert.IsNotNull(result, "Malformed JSON must not throw");
            Assert.AreEqual(0, result.Count);
        }

        // ═══════════════════════════════════════════════════════════════════
        // Scalar value types
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void Deserialize_StringValue_ReturnedAsString()
        {
            var result = InspectorJson.Deserialize(@"{""key"":""hello""}");

            Assert.AreEqual("hello", result["key"]);
            Assert.IsInstanceOf<string>(result["key"]);
        }

        [Test]
        public void Deserialize_IntegerValue_ReturnedAsLong()
        {
            var result = InspectorJson.Deserialize(@"{""count"":42}");

            Assert.AreEqual(42L, result["count"],
                "Integer JSON tokens must be returned as long");
            Assert.IsInstanceOf<long>(result["count"]);
        }

        [Test]
        public void Deserialize_FloatValue_ReturnedAsDouble()
        {
            var result = InspectorJson.Deserialize(@"{""rate"":3.14}");

            Assert.IsInstanceOf<double>(result["rate"],
                "Float JSON tokens must be returned as double");
            Assert.AreEqual(3.14, (double)result["rate"], delta: 0.001);
        }

        [Test]
        public void Deserialize_BooleanTrue_ReturnedAsBool()
        {
            var result = InspectorJson.Deserialize(@"{""flag"":true}");

            Assert.IsInstanceOf<bool>(result["flag"]);
            Assert.IsTrue((bool)result["flag"]);
        }

        [Test]
        public void Deserialize_BooleanFalse_ReturnedAsBool()
        {
            var result = InspectorJson.Deserialize(@"{""flag"":false}");

            Assert.IsInstanceOf<bool>(result["flag"]);
            Assert.IsFalse((bool)result["flag"]);
        }

        [Test]
        public void Deserialize_NullValue_ReturnedAsNull()
        {
            var result = InspectorJson.Deserialize(@"{""nothing"":null}");

            Assert.IsTrue(result.ContainsKey("nothing"), "Key with null value must be present");
            Assert.IsNull(result["nothing"]);
        }

        // ═══════════════════════════════════════════════════════════════════
        // Multiple keys
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void Deserialize_MultipleKeys_AllParsed()
        {
            var result = InspectorJson.Deserialize(@"{""a"":1,""b"":""x"",""c"":true}");

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(1L,   result["a"]);
            Assert.AreEqual("x",  result["b"]);
            Assert.AreEqual(true, result["c"]);
        }

        // ═══════════════════════════════════════════════════════════════════
        // Nested objects
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void Deserialize_NestedObject_ReturnedAsDictionary()
        {
            var result = InspectorJson.Deserialize(@"{""child"":{""x"":10}}");

            Assert.IsInstanceOf<Dictionary<string, object>>(result["child"],
                "Nested object must be returned as Dictionary<string,object>");

            var inner = (Dictionary<string, object>)result["child"];
            Assert.AreEqual(10L, inner["x"]);
        }

        [Test]
        public void Deserialize_DeeplyNestedObject_ParsedRecursively()
        {
            var result = InspectorJson.Deserialize(@"{""l1"":{""l2"":{""val"":99}}}");

            var l1 = (Dictionary<string, object>)result["l1"];
            var l2 = (Dictionary<string, object>)l1["l2"];
            Assert.AreEqual(99L, l2["val"]);
        }

        // ═══════════════════════════════════════════════════════════════════
        // Array values
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void Deserialize_ArrayValue_ReturnedAsList()
        {
            var result = InspectorJson.Deserialize(@"{""items"":[1,2,3]}");

            Assert.IsInstanceOf<List<object>>(result["items"],
                "JSON array must be returned as List<object>");

            var list = (List<object>)result["items"];
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(1L, list[0]);
            Assert.AreEqual(2L, list[1]);
            Assert.AreEqual(3L, list[2]);
        }

        [Test]
        public void Deserialize_ArrayOfStrings_ParsedCorrectly()
        {
            var result = InspectorJson.Deserialize(@"{""tags"":[""a"",""b""]}");

            var list = (List<object>)result["tags"];
            Assert.AreEqual("a", list[0]);
            Assert.AreEqual("b", list[1]);
        }

        [Test]
        public void Deserialize_EmptyArray_ReturnedAsEmptyList()
        {
            var result = InspectorJson.Deserialize(@"{""empty"":[]}");

            var list = (List<object>)result["empty"];
            Assert.AreEqual(0, list.Count);
        }

        // ═══════════════════════════════════════════════════════════════════
        // Key preservation
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void Deserialize_KeyNames_PreservedExactly()
        {
            var result = InspectorJson.Deserialize(@"{""camelCase"":1,""snake_case"":2,""PascalCase"":3}");

            Assert.IsTrue(result.ContainsKey("camelCase"));
            Assert.IsTrue(result.ContainsKey("snake_case"));
            Assert.IsTrue(result.ContainsKey("PascalCase"));
        }
    }
}
