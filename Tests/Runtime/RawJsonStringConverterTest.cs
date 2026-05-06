using System.IO;
using com.noctuagames.sdk;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// EditMode NUnit tests for <see cref="RawJsonStringConverter"/>.
    ///
    /// Covers both the read (JSON → string) and write (string → JSON) paths
    /// via full round-trip serialization so we exercise the converter through
    /// Newtonsoft's normal pipeline without constructing raw JsonReader/Writer
    /// objects manually.
    /// </summary>
    [TestFixture]
    public class RawJsonStringConverterTest
    {
        // ─── Helpers ──────────────────────────────────────────────────────────

        /// <summary>Carrier class that applies the converter to a single field.</summary>
        private class Carrier
        {
            [JsonConverter(typeof(RawJsonStringConverter))]
            public string Value { get; set; }
        }

        private static Carrier Deserialize(string json) =>
            JsonConvert.DeserializeObject<Carrier>(json);

        private static string Serialize(string value) =>
            JsonConvert.SerializeObject(new Carrier { Value = value });

        // ─── ReadJson — string token ───────────────────────────────────────

        [Test]
        public void ReadJson_StringToken_ReturnsSameString()
        {
            var result = Deserialize(@"{""value"":""hello""}");
            Assert.AreEqual("hello", result.Value);
        }

        [Test]
        public void ReadJson_EmptyString_ReturnsEmptyString()
        {
            var result = Deserialize(@"{""value"":""""}");
            Assert.AreEqual("", result.Value);
        }

        [Test]
        public void ReadJson_NullToken_ReturnsNull()
        {
            var result = Deserialize(@"{""value"":null}");
            Assert.IsNull(result.Value);
        }

        // ─── ReadJson — nested object / array ─────────────────────────────

        [Test]
        public void ReadJson_NestedObject_ReturnsCompactJsonString()
        {
            var result = Deserialize(@"{""value"":{""a"":1,""b"":""x""}}");

            Assert.IsNotNull(result.Value, "Nested object must be serialized to a string");
            // Compact JSON — no extra whitespace
            StringAssert.Contains("\"a\"", result.Value);
            StringAssert.Contains("\"b\"", result.Value);
        }

        [Test]
        public void ReadJson_NestedObject_DoesNotContainNewlines()
        {
            var result = Deserialize(@"{""value"":{""k"":""v""}}");

            StringAssert.DoesNotContain("\n", result.Value,
                "Compact formatting must not include newlines");
        }

        [Test]
        public void ReadJson_NestedArray_ReturnsJsonArrayString()
        {
            var result = Deserialize(@"{""value"":[1,2,3]}");

            Assert.IsNotNull(result.Value);
            StringAssert.StartsWith("[", result.Value);
            StringAssert.EndsWith("]", result.Value);
        }

        [Test]
        public void ReadJson_NestedBoolean_ReturnsBoolString()
        {
            var result = Deserialize(@"{""value"":true}");

            Assert.AreEqual("true", result.Value);
        }

        [Test]
        public void ReadJson_NestedInteger_ReturnsIntString()
        {
            var result = Deserialize(@"{""value"":42}");

            Assert.AreEqual("42", result.Value);
        }

        // ─── WriteJson ─────────────────────────────────────────────────────

        [Test]
        public void WriteJson_PlainString_WritesStringLiteral()
        {
            var json = Serialize("world");

            StringAssert.Contains("\"world\"", json);
        }

        [Test]
        public void WriteJson_NullValue_WritesNullToken()
        {
            var json = Serialize(null);

            StringAssert.Contains("null", json);
        }

        [Test]
        public void WriteJson_EmptyString_WritesEmptyStringLiteral()
        {
            var json = Serialize("");

            StringAssert.Contains("\"\"", json);
        }

        // ─── Round-trip ────────────────────────────────────────────────────

        [Test]
        public void RoundTrip_PlainString_PreservesValue()
        {
            const string original = "round-trip-test";
            var json   = Serialize(original);
            var result = Deserialize(json);

            Assert.AreEqual(original, result.Value);
        }

        [Test]
        public void RoundTrip_Null_PreservesNull()
        {
            var json   = Serialize(null);
            var result = Deserialize(json);

            Assert.IsNull(result.Value);
        }

        [Test]
        public void RoundTrip_StringWithEscapes_PreservesValue()
        {
            // String containing JSON-special characters
            const string original = "line1\nline2\ttab\"quote\"";
            var json   = Serialize(original);
            var result = Deserialize(json);

            Assert.AreEqual(original, result.Value);
        }
    }
}
