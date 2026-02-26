using System.Collections;
using System.Collections.Generic;
using com.noctuagames.sdk;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.Runtime
{
    public class CloudSaveTest
    {
        private readonly JsonSerializerSettings _jsonSettings = new()
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            },
            NullValueHandling = NullValueHandling.Ignore,
        };

        // --- CloudSaveMetadata deserialization tests ---

        [UnityTest]
        public IEnumerator CloudSaveMetadata_Deserialization_Success()
        {
            var json = @"{
                ""slot_key"": ""profile"",
                ""content_type"": ""application/octet-stream"",
                ""size_bytes"": 42,
                ""checksum"": ""a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2"",
                ""created_at"": ""2026-02-18T12:00:00Z"",
                ""updated_at"": ""2026-02-18T12:05:00Z""
            }";

            var metadata = JsonConvert.DeserializeObject<CloudSaveMetadata>(json, _jsonSettings);

            Assert.AreEqual("profile", metadata.SlotKey);
            Assert.AreEqual("application/octet-stream", metadata.ContentType);
            Assert.AreEqual(42, metadata.SizeBytes);
            Assert.AreEqual("a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2", metadata.Checksum);
            Assert.AreEqual("2026-02-18T12:00:00Z", metadata.CreatedAt);
            Assert.AreEqual("2026-02-18T12:05:00Z", metadata.UpdatedAt);

            yield return null;
        }

        [UnityTest]
        public IEnumerator CloudSaveMetadata_Deserialization_WithDataEnvelope()
        {
            var json = @"{
                ""data"": {
                    ""slot_key"": ""inventory"",
                    ""content_type"": ""application/octet-stream"",
                    ""size_bytes"": 1024,
                    ""checksum"": ""abc123def456"",
                    ""created_at"": ""2026-02-18T12:00:00Z"",
                    ""updated_at"": ""2026-02-18T12:05:00Z""
                }
            }";

            var wrapper = JsonConvert.DeserializeObject<DataWrapper<CloudSaveMetadata>>(json, _jsonSettings);

            Assert.IsNotNull(wrapper.Data);
            Assert.AreEqual("inventory", wrapper.Data.SlotKey);
            Assert.AreEqual("application/octet-stream", wrapper.Data.ContentType);
            Assert.AreEqual(1024, wrapper.Data.SizeBytes);
            Assert.AreEqual("abc123def456", wrapper.Data.Checksum);

            yield return null;
        }

        // --- CloudSaveListResponse deserialization tests ---

        [UnityTest]
        public IEnumerator CloudSaveListResponse_Deserialization_MultipleSaves()
        {
            var json = @"{
                ""saves"": [
                    {
                        ""slot_key"": ""inventory"",
                        ""content_type"": ""application/octet-stream"",
                        ""size_bytes"": 1024,
                        ""checksum"": ""abc123"",
                        ""created_at"": ""2026-02-18T12:00:00Z"",
                        ""updated_at"": ""2026-02-18T12:05:00Z""
                    },
                    {
                        ""slot_key"": ""profile"",
                        ""content_type"": ""application/octet-stream"",
                        ""size_bytes"": 62,
                        ""checksum"": ""def456"",
                        ""created_at"": ""2026-02-18T12:00:00Z"",
                        ""updated_at"": ""2026-02-18T12:10:00Z""
                    }
                ],
                ""total"": 2
            }";

            var response = JsonConvert.DeserializeObject<CloudSaveListResponse>(json, _jsonSettings);

            Assert.AreEqual(2, response.Total);
            Assert.AreEqual(2, response.Saves.Count);
            Assert.AreEqual("inventory", response.Saves[0].SlotKey);
            Assert.AreEqual("profile", response.Saves[1].SlotKey);
            Assert.AreEqual(1024, response.Saves[0].SizeBytes);
            Assert.AreEqual(62, response.Saves[1].SizeBytes);

            yield return null;
        }

        [UnityTest]
        public IEnumerator CloudSaveListResponse_Deserialization_EmptySaves()
        {
            var json = @"{
                ""saves"": [],
                ""total"": 0
            }";

            var response = JsonConvert.DeserializeObject<CloudSaveListResponse>(json, _jsonSettings);

            Assert.AreEqual(0, response.Total);
            Assert.IsNotNull(response.Saves);
            Assert.AreEqual(0, response.Saves.Count);

            yield return null;
        }

        [UnityTest]
        public IEnumerator CloudSaveListResponse_Deserialization_WithDataEnvelope()
        {
            var json = @"{
                ""data"": {
                    ""saves"": [
                        {
                            ""slot_key"": ""progress"",
                            ""content_type"": ""application/octet-stream"",
                            ""size_bytes"": 512,
                            ""checksum"": ""aaa111"",
                            ""created_at"": ""2026-02-18T12:00:00Z"",
                            ""updated_at"": ""2026-02-18T12:05:00Z""
                        }
                    ],
                    ""total"": 1
                }
            }";

            var wrapper = JsonConvert.DeserializeObject<DataWrapper<CloudSaveListResponse>>(json, _jsonSettings);

            Assert.IsNotNull(wrapper.Data);
            Assert.AreEqual(1, wrapper.Data.Total);
            Assert.AreEqual(1, wrapper.Data.Saves.Count);
            Assert.AreEqual("progress", wrapper.Data.Saves[0].SlotKey);

            yield return null;
        }

        [UnityTest]
        public IEnumerator CloudSaveListResponse_ExtractKeys_ReturnsSlotKeys()
        {
            var json = @"{
                ""saves"": [
                    {
                        ""slot_key"": ""inventory"",
                        ""content_type"": ""application/octet-stream"",
                        ""size_bytes"": 1024,
                        ""checksum"": ""abc123"",
                        ""created_at"": ""2026-02-18T12:00:00Z"",
                        ""updated_at"": ""2026-02-18T12:05:00Z""
                    },
                    {
                        ""slot_key"": ""profile"",
                        ""content_type"": ""application/octet-stream"",
                        ""size_bytes"": 62,
                        ""checksum"": ""def456"",
                        ""created_at"": ""2026-02-18T12:00:00Z"",
                        ""updated_at"": ""2026-02-18T12:10:00Z""
                    },
                    {
                        ""slot_key"": ""settings"",
                        ""content_type"": ""application/octet-stream"",
                        ""size_bytes"": 128,
                        ""checksum"": ""ghi789"",
                        ""created_at"": ""2026-02-18T12:00:00Z"",
                        ""updated_at"": ""2026-02-18T12:15:00Z""
                    }
                ],
                ""total"": 3
            }";

            var response = JsonConvert.DeserializeObject<CloudSaveListResponse>(json, _jsonSettings);
            var keys = new List<string>();
            foreach (var save in response.Saves)
            {
                keys.Add(save.SlotKey);
            }

            Assert.AreEqual(3, keys.Count);
            Assert.Contains("inventory", keys);
            Assert.Contains("profile", keys);
            Assert.Contains("settings", keys);

            yield return null;
        }

        // --- Error response deserialization tests ---

        [UnityTest]
        public IEnumerator ErrorResponse_Deserialization_InvalidSlotKey()
        {
            var json = @"{
                ""success"": false,
                ""error_code"": 2064,
                ""error_message"": ""Cloud save slot key is invalid""
            }";

            var response = JsonConvert.DeserializeObject<ErrorResponse>(json, _jsonSettings);

            Assert.IsFalse(response.Success);
            Assert.AreEqual(2064, response.ErrorCode);
            Assert.AreEqual("Cloud save slot key is invalid", response.ErrorMessage);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ErrorResponse_Deserialization_DataTooLarge()
        {
            var json = @"{
                ""success"": false,
                ""error_code"": 2065,
                ""error_message"": ""Cloud save data exceeds maximum size""
            }";

            var response = JsonConvert.DeserializeObject<ErrorResponse>(json, _jsonSettings);

            Assert.IsFalse(response.Success);
            Assert.AreEqual(2065, response.ErrorCode);
            Assert.AreEqual("Cloud save data exceeds maximum size", response.ErrorMessage);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ErrorResponse_Deserialization_SaveNotFound()
        {
            var json = @"{
                ""success"": false,
                ""error_code"": 2306,
                ""error_message"": ""Cloud save not found""
            }";

            var response = JsonConvert.DeserializeObject<ErrorResponse>(json, _jsonSettings);

            Assert.IsFalse(response.Success);
            Assert.AreEqual(2306, response.ErrorCode);
            Assert.AreEqual("Cloud save not found", response.ErrorMessage);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ErrorResponse_Deserialization_Unauthorized()
        {
            var json = @"{
                ""success"": false,
                ""error_code"": 2100,
                ""error_message"": ""Unauthorized""
            }";

            var response = JsonConvert.DeserializeObject<ErrorResponse>(json, _jsonSettings);

            Assert.IsFalse(response.Success);
            Assert.AreEqual(2100, response.ErrorCode);
            Assert.AreEqual("Unauthorized", response.ErrorMessage);

            yield return null;
        }

        // --- Integration tests (requires full SDK init + backend) ---

        [Ignore("Requires full SDK resources and a live backend server.")]
        [UnityTest]
        public IEnumerator SaveAndLoadGameState_RoundTrip() => UniTask.ToCoroutine(async () =>
        {
            await Noctua.InitAsync();
            await Noctua.Auth.AuthenticateAsync();

            var testKey = "test-save";
            var testValue = "hello world this is a test save value";

            await Noctua.Auth.SaveGameState(testKey, testValue);

            var loaded = await Noctua.Auth.LoadGameState(testKey);

            Assert.AreEqual(testValue, loaded);

            await Noctua.Auth.DeleteGameState(testKey);
        });

        [Ignore("Requires full SDK resources and a live backend server.")]
        [UnityTest]
        public IEnumerator GetGameStateKeys_ReturnsKeys() => UniTask.ToCoroutine(async () =>
        {
            await Noctua.InitAsync();
            await Noctua.Auth.AuthenticateAsync();

            await Noctua.Auth.SaveGameState("key-a", "value-a");
            await Noctua.Auth.SaveGameState("key-b", "value-b");

            var keys = await Noctua.Auth.GetGameStateKeys();

            Assert.IsNotNull(keys);
            Assert.Contains("key-a", keys);
            Assert.Contains("key-b", keys);

            await Noctua.Auth.DeleteGameState("key-a");
            await Noctua.Auth.DeleteGameState("key-b");
        });

        [Ignore("Requires full SDK resources and a live backend server.")]
        [UnityTest]
        public IEnumerator DeleteGameState_RemovesKey() => UniTask.ToCoroutine(async () =>
        {
            await Noctua.InitAsync();
            await Noctua.Auth.AuthenticateAsync();

            await Noctua.Auth.SaveGameState("to-delete", "temporary");

            await Noctua.Auth.DeleteGameState("to-delete");

            var keys = await Noctua.Auth.GetGameStateKeys();

            Assert.IsFalse(keys.Contains("to-delete"));
        });

        // Helper class to simulate the HTTP layer data envelope unwrap
        private class DataWrapper<T>
        {
            [JsonProperty("data")]
            public T Data;
        }
    }
}
