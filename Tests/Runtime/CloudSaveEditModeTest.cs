using System.Collections.Generic;
using com.noctuagames.sdk;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// EditMode NUnit tests for CloudSave model classes:
    ///   — <see cref="CloudSaveMetadata"/> JSON round-trip and defaults
    ///   — <see cref="CloudSaveListResponse"/> JSON round-trip and defaults
    ///   — <see cref="ErrorResponse"/> with CloudSave-specific error codes
    ///
    /// The 10 runnable JSON-deserialization tests in <c>CloudSaveTest</c> use
    /// <c>[UnityTest]</c> / <c>yield return null</c> despite being entirely
    /// synchronous — PlayMode only, zero EditMode coverage.  These plain
    /// <c>[Test]</c> equivalents ensure the same branches are counted during
    /// the EditMode pass.
    /// </summary>
    [TestFixture]
    public class CloudSaveEditModeTest
    {
        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            },
            NullValueHandling = NullValueHandling.Ignore,
        };

        // ─── CloudSaveMetadata deserialization ────────────────────────────────

        [Test]
        public void CloudSaveMetadata_Deserialization_SlotKey()
        {
            const string json = @"{
                ""slot_key"": ""profile"",
                ""content_type"": ""application/octet-stream"",
                ""size_bytes"": 42,
                ""checksum"": ""a1b2c3d4"",
                ""created_at"": ""2026-02-18T12:00:00Z"",
                ""updated_at"": ""2026-02-18T12:05:00Z""
            }";
            var m = JsonConvert.DeserializeObject<CloudSaveMetadata>(json, JsonSettings);
            Assert.AreEqual("profile", m.SlotKey);
        }

        [Test]
        public void CloudSaveMetadata_Deserialization_ContentTypeAndSizeBytes()
        {
            const string json = @"{
                ""slot_key"": ""profile"",
                ""content_type"": ""application/octet-stream"",
                ""size_bytes"": 42,
                ""checksum"": ""a1b2c3d4"",
                ""created_at"": ""2026-02-18T12:00:00Z"",
                ""updated_at"": ""2026-02-18T12:05:00Z""
            }";
            var m = JsonConvert.DeserializeObject<CloudSaveMetadata>(json, JsonSettings);
            Assert.AreEqual("application/octet-stream", m.ContentType);
            Assert.AreEqual(42, m.SizeBytes);
        }

        [Test]
        public void CloudSaveMetadata_Deserialization_ChecksumAndTimestamps()
        {
            const string json = @"{
                ""slot_key"": ""profile"",
                ""content_type"": ""application/octet-stream"",
                ""size_bytes"": 42,
                ""checksum"": ""a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2"",
                ""created_at"": ""2026-02-18T12:00:00Z"",
                ""updated_at"": ""2026-02-18T12:05:00Z""
            }";
            var m = JsonConvert.DeserializeObject<CloudSaveMetadata>(json, JsonSettings);
            Assert.AreEqual("a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2", m.Checksum);
            Assert.AreEqual("2026-02-18T12:00:00Z", m.CreatedAt);
            Assert.AreEqual("2026-02-18T12:05:00Z", m.UpdatedAt);
        }

        [Test]
        public void CloudSaveMetadata_Deserialization_WithDataEnvelope_UnwrapsCorrectly()
        {
            const string json = @"{
                ""data"": {
                    ""slot_key"": ""inventory"",
                    ""content_type"": ""application/octet-stream"",
                    ""size_bytes"": 1024,
                    ""checksum"": ""abc123def456"",
                    ""created_at"": ""2026-02-18T12:00:00Z"",
                    ""updated_at"": ""2026-02-18T12:05:00Z""
                }
            }";
            var wrapper = JsonConvert.DeserializeObject<DataWrapper<CloudSaveMetadata>>(json, JsonSettings);
            Assert.IsNotNull(wrapper.Data);
            Assert.AreEqual("inventory",            wrapper.Data.SlotKey);
            Assert.AreEqual(1024,                   wrapper.Data.SizeBytes);
            Assert.AreEqual("abc123def456",         wrapper.Data.Checksum);
        }

        [Test]
        public void CloudSaveMetadata_DefaultInstance_AllFieldsNullOrZero()
        {
            var m = new CloudSaveMetadata();
            Assert.IsNull(m.SlotKey);
            Assert.IsNull(m.ContentType);
            Assert.AreEqual(0, m.SizeBytes);
            Assert.IsNull(m.Checksum);
            Assert.IsNull(m.CreatedAt);
            Assert.IsNull(m.UpdatedAt);
        }

        // ─── CloudSaveListResponse deserialization ────────────────────────────

        [Test]
        public void CloudSaveListResponse_Deserialization_MultipleSaves_Count()
        {
            const string json = @"{
                ""saves"": [
                    { ""slot_key"": ""inventory"", ""content_type"": ""application/octet-stream"", ""size_bytes"": 1024, ""checksum"": ""abc123"", ""created_at"": ""2026-02-18T12:00:00Z"", ""updated_at"": ""2026-02-18T12:05:00Z"" },
                    { ""slot_key"": ""profile"",   ""content_type"": ""application/octet-stream"", ""size_bytes"": 62,   ""checksum"": ""def456"", ""created_at"": ""2026-02-18T12:00:00Z"", ""updated_at"": ""2026-02-18T12:10:00Z"" }
                ],
                ""total"": 2
            }";
            var r = JsonConvert.DeserializeObject<CloudSaveListResponse>(json, JsonSettings);
            Assert.AreEqual(2, r.Total);
            Assert.AreEqual(2, r.Saves.Count);
        }

        [Test]
        public void CloudSaveListResponse_Deserialization_MultipleSaves_SlotKeys()
        {
            const string json = @"{
                ""saves"": [
                    { ""slot_key"": ""inventory"", ""content_type"": ""application/octet-stream"", ""size_bytes"": 1024, ""checksum"": ""abc123"", ""created_at"": ""2026-02-18T12:00:00Z"", ""updated_at"": ""2026-02-18T12:05:00Z"" },
                    { ""slot_key"": ""profile"",   ""content_type"": ""application/octet-stream"", ""size_bytes"": 62,   ""checksum"": ""def456"", ""created_at"": ""2026-02-18T12:00:00Z"", ""updated_at"": ""2026-02-18T12:10:00Z"" }
                ],
                ""total"": 2
            }";
            var r = JsonConvert.DeserializeObject<CloudSaveListResponse>(json, JsonSettings);
            Assert.AreEqual("inventory", r.Saves[0].SlotKey);
            Assert.AreEqual("profile",   r.Saves[1].SlotKey);
            Assert.AreEqual(1024, r.Saves[0].SizeBytes);
            Assert.AreEqual(62,   r.Saves[1].SizeBytes);
        }

        [Test]
        public void CloudSaveListResponse_Deserialization_EmptySaves()
        {
            const string json = @"{ ""saves"": [], ""total"": 0 }";
            var r = JsonConvert.DeserializeObject<CloudSaveListResponse>(json, JsonSettings);
            Assert.AreEqual(0, r.Total);
            Assert.IsNotNull(r.Saves);
            Assert.AreEqual(0, r.Saves.Count);
        }

        [Test]
        public void CloudSaveListResponse_Deserialization_WithDataEnvelope()
        {
            const string json = @"{
                ""data"": {
                    ""saves"": [
                        { ""slot_key"": ""progress"", ""content_type"": ""application/octet-stream"", ""size_bytes"": 512, ""checksum"": ""aaa111"", ""created_at"": ""2026-02-18T12:00:00Z"", ""updated_at"": ""2026-02-18T12:05:00Z"" }
                    ],
                    ""total"": 1
                }
            }";
            var wrapper = JsonConvert.DeserializeObject<DataWrapper<CloudSaveListResponse>>(json, JsonSettings);
            Assert.IsNotNull(wrapper.Data);
            Assert.AreEqual(1,          wrapper.Data.Total);
            Assert.AreEqual("progress", wrapper.Data.Saves[0].SlotKey);
        }

        [Test]
        public void CloudSaveListResponse_ExtractKeys_ContainsAllSlotKeys()
        {
            const string json = @"{
                ""saves"": [
                    { ""slot_key"": ""inventory"", ""content_type"": """", ""size_bytes"": 1, ""checksum"": """", ""created_at"": """", ""updated_at"": """" },
                    { ""slot_key"": ""profile"",   ""content_type"": """", ""size_bytes"": 1, ""checksum"": """", ""created_at"": """", ""updated_at"": """" },
                    { ""slot_key"": ""settings"",  ""content_type"": """", ""size_bytes"": 1, ""checksum"": """", ""created_at"": """", ""updated_at"": """" }
                ],
                ""total"": 3
            }";
            var r = JsonConvert.DeserializeObject<CloudSaveListResponse>(json, JsonSettings);
            var keys = new List<string>();
            foreach (var save in r.Saves) keys.Add(save.SlotKey);

            Assert.AreEqual(3, keys.Count);
            Assert.Contains("inventory", keys);
            Assert.Contains("profile",   keys);
            Assert.Contains("settings",  keys);
        }

        [Test]
        public void CloudSaveListResponse_DefaultInstance_TotalIsZeroSavesNullOrEmpty()
        {
            var r = new CloudSaveListResponse();
            Assert.AreEqual(0, r.Total);
            Assert.IsTrue(r.Saves == null || r.Saves.Count == 0);
        }

        // ─── ErrorResponse — CloudSave-specific error codes ───────────────────

        [Test]
        public void ErrorResponse_Deserialization_InvalidSlotKey_ErrorCode2064()
        {
            const string json = @"{
                ""success"": false,
                ""error_code"": 2064,
                ""error_message"": ""Cloud save slot key is invalid""
            }";
            var r = JsonConvert.DeserializeObject<ErrorResponse>(json, JsonSettings);
            Assert.IsFalse(r.Success);
            Assert.AreEqual(2064,                          r.ErrorCode);
            Assert.AreEqual("Cloud save slot key is invalid", r.ErrorMessage);
        }

        [Test]
        public void ErrorResponse_Deserialization_DataTooLarge_ErrorCode2065()
        {
            const string json = @"{
                ""success"": false,
                ""error_code"": 2065,
                ""error_message"": ""Cloud save data exceeds maximum size""
            }";
            var r = JsonConvert.DeserializeObject<ErrorResponse>(json, JsonSettings);
            Assert.AreEqual(2065,                                  r.ErrorCode);
            Assert.AreEqual("Cloud save data exceeds maximum size", r.ErrorMessage);
        }

        [Test]
        public void ErrorResponse_Deserialization_SaveNotFound_ErrorCode2306()
        {
            const string json = @"{
                ""success"": false,
                ""error_code"": 2306,
                ""error_message"": ""Cloud save not found""
            }";
            var r = JsonConvert.DeserializeObject<ErrorResponse>(json, JsonSettings);
            Assert.AreEqual(2306,               r.ErrorCode);
            Assert.AreEqual("Cloud save not found", r.ErrorMessage);
        }

        [Test]
        public void ErrorResponse_Deserialization_Unauthorized_ErrorCode2100()
        {
            const string json = @"{
                ""success"": false,
                ""error_code"": 2100,
                ""error_message"": ""Unauthorized""
            }";
            var r = JsonConvert.DeserializeObject<ErrorResponse>(json, JsonSettings);
            Assert.IsFalse(r.Success);
            Assert.AreEqual(2100,          r.ErrorCode);
            Assert.AreEqual("Unauthorized", r.ErrorMessage);
        }

        // Shared DataWrapper for envelope-unwrap tests
        private class DataWrapper<T>
        {
            [JsonProperty("data")]
            public T Data;
        }
    }
}
