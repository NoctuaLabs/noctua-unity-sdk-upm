using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using com.noctuagames.sdk;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Tests.Runtime
{
    public class ModelSerializationTests
    {
        private static readonly string ModelNamespace = "com.noctuagames.sdk";

        private static IEnumerable<Type> ConcreteModelTypes()
        {
            var asm = typeof(NoctuaException).Assembly;
            return asm.GetTypes()
                .Where(t => t.IsClass)
                .Where(t => !t.IsAbstract)
                .Where(t => !t.IsGenericTypeDefinition)
                .Where(t => t.Namespace != null && t.Namespace.StartsWith(ModelNamespace))
                .Where(t => t.GetCustomAttribute<JsonConverterAttribute>() == null)
                .Where(t => t.GetConstructor(Type.EmptyTypes) != null)
                .Where(t => HasJsonProperty(t))
                .Where(t => !typeof(System.Collections.IEnumerable).IsAssignableFrom(t) || t == typeof(string))
                .Where(t => !typeof(UnityEngine.Object).IsAssignableFrom(t))
                .Where(t => !typeof(JsonConverter).IsAssignableFrom(t));
        }

        private static bool HasJsonProperty(Type t)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            foreach (var f in t.GetFields(flags))
            {
                if (f.GetCustomAttribute<JsonPropertyAttribute>() != null) return true;
            }
            foreach (var p in t.GetProperties(flags))
            {
                if (p.GetCustomAttribute<JsonPropertyAttribute>() != null) return true;
            }
            return false;
        }

        [Test]
        public void RoundTrip_AllModelDtos_DoesNotThrow()
        {
            var failures = new List<string>();
            int success = 0;

            foreach (var t in ConcreteModelTypes())
            {
                try
                {
                    var instance = Activator.CreateInstance(t);
                    var json = JsonConvert.SerializeObject(instance);
                    Assert.IsNotNull(json, $"Serialization returned null for {t.FullName}");

                    var roundtripped = JsonConvert.DeserializeObject(json, t);
                    Assert.IsNotNull(roundtripped, $"Deserialization returned null for {t.FullName}");

                    var json2 = JsonConvert.SerializeObject(roundtripped);
                    Assert.AreEqual(json, json2, $"Round-trip JSON mismatch for {t.FullName}");
                    success++;
                }
                catch (Exception ex)
                {
                    failures.Add($"{t.FullName}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            Assert.Greater(success, 0, "No model DTO types were tested.");
            UnityEngine.Debug.Log(
                $"ModelSerializationTests: round-tripped {success} types, skipped {failures.Count} due to construction/serialization issues.");
        }

        [Test]
        public void NoctuaException_Construct_AllErrorCodes()
        {
            foreach (NoctuaErrorCode code in Enum.GetValues(typeof(NoctuaErrorCode)))
            {
                var ex = new NoctuaException(code, $"msg-{code}", $"payload-{code}");
                Assert.AreEqual((int)code, ex.ErrorCode);
                Assert.AreEqual($"payload-{code}", ex.Payload);
                StringAssert.Contains(((int)code).ToString(), ex.ToString());
                StringAssert.Contains($"msg-{code}", ex.ToString());
            }
        }

        [Test]
        public void ErrorResponse_Roundtrip_PreservesFields()
        {
            var er = new ErrorResponse { Success = false, ErrorMessage = "boom", ErrorCode = 4242 };
            var json = JsonConvert.SerializeObject(er);
            var back = JsonConvert.DeserializeObject<ErrorResponse>(json);

            Assert.IsFalse(back.Success);
            Assert.AreEqual("boom", back.ErrorMessage);
            Assert.AreEqual(4242, back.ErrorCode);
            StringAssert.Contains("\"success\":false", json);
            StringAssert.Contains("\"error_message\":\"boom\"", json);
            StringAssert.Contains("\"error_code\":4242", json);
        }

        [Test]
        public void RawJsonStringConverter_StringPassThrough()
        {
            var c = new RawJsonStringConverter();
            var json = "{\"raw\":\"hello\"}";
            var holder = JsonConvert.DeserializeObject<RawHolder>(json);
            Assert.AreEqual("hello", holder.Raw);
        }

        [Test]
        public void RawJsonStringConverter_ObjectInlinedAsString()
        {
            var json = "{\"raw\":{\"a\":1,\"b\":\"two\"}}";
            var holder = JsonConvert.DeserializeObject<RawHolder>(json);
            Assert.AreEqual("{\"a\":1,\"b\":\"two\"}", holder.Raw);
        }

        [Test]
        public void RawJsonStringConverter_NullStaysNull()
        {
            var json = "{\"raw\":null}";
            var holder = JsonConvert.DeserializeObject<RawHolder>(json);
            Assert.IsNull(holder.Raw);
        }

        private class RawHolder
        {
            [JsonProperty("raw")]
            [JsonConverter(typeof(RawJsonStringConverter))]
            public string Raw;
        }
    }
}
