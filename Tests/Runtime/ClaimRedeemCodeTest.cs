using System.Collections;
using com.noctuagames.sdk;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.Runtime
{
    public class ClaimRedeemCodeTest
    {
        private readonly JsonSerializerSettings _jsonSettings = new()
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            },
            NullValueHandling = NullValueHandling.Ignore,
        };

        // --- Request serialization tests ---

        [UnityTest]
        public IEnumerator Request_Serialization_CorrectJsonKeys()
        {
            var request = new ClaimRedeemCodeRequest
            {
                Code = "ABCD-EFGH-IJKL-MNOP",
                UserId = 12345
            };

            var json = JsonConvert.SerializeObject(request, _jsonSettings);

            Assert.IsTrue(json.Contains("\"code\""));
            Assert.IsTrue(json.Contains("\"user_id\""));
            Assert.IsTrue(json.Contains("ABCD-EFGH-IJKL-MNOP"));
            Assert.IsTrue(json.Contains("12345"));

            yield return null;
        }

        [UnityTest]
        public IEnumerator Request_Deserialization_RoundTrip()
        {
            var original = new ClaimRedeemCodeRequest
            {
                Code = "A1B2-C3D4-E5F6-G7H8",
                UserId = 99999
            };

            var json = JsonConvert.SerializeObject(original, _jsonSettings);
            var deserialized = JsonConvert.DeserializeObject<ClaimRedeemCodeRequest>(json, _jsonSettings);

            Assert.AreEqual(original.Code, deserialized.Code);
            Assert.AreEqual(original.UserId, deserialized.UserId);

            yield return null;
        }

        // --- Response deserialization tests ---

        [UnityTest]
        public IEnumerator Response_Deserialization_Success()
        {
            var json = @"{
                ""success"": true,
                ""order_ids"": [1001, 1002],
                ""message"": ""Code redeemed successfully""
            }";

            var response = JsonConvert.DeserializeObject<ClaimRedeemCodeResponse>(json, _jsonSettings);

            Assert.IsTrue(response.Success);
            Assert.AreEqual(2, response.OrderIds.Length);
            Assert.AreEqual(1001, response.OrderIds[0]);
            Assert.AreEqual(1002, response.OrderIds[1]);
            Assert.AreEqual("Code redeemed successfully", response.Message);

            yield return null;
        }

        [UnityTest]
        public IEnumerator Response_Deserialization_EmptyOrderIds()
        {
            var json = @"{
                ""success"": true,
                ""order_ids"": [],
                ""message"": ""Code redeemed successfully""
            }";

            var response = JsonConvert.DeserializeObject<ClaimRedeemCodeResponse>(json, _jsonSettings);

            Assert.IsTrue(response.Success);
            Assert.IsNotNull(response.OrderIds);
            Assert.AreEqual(0, response.OrderIds.Length);

            yield return null;
        }

        [UnityTest]
        public IEnumerator Response_Deserialization_NullMessage()
        {
            var json = @"{
                ""success"": true,
                ""order_ids"": [5001]
            }";

            var response = JsonConvert.DeserializeObject<ClaimRedeemCodeResponse>(json, _jsonSettings);

            Assert.IsTrue(response.Success);
            Assert.AreEqual(1, response.OrderIds.Length);
            Assert.AreEqual(5001, response.OrderIds[0]);
            Assert.IsNull(response.Message);

            yield return null;
        }

        [UnityTest]
        public IEnumerator Response_Deserialization_FailureResponse()
        {
            var json = @"{
                ""success"": false,
                ""order_ids"": [],
                ""message"": null
            }";

            var response = JsonConvert.DeserializeObject<ClaimRedeemCodeResponse>(json, _jsonSettings);

            Assert.IsFalse(response.Success);

            yield return null;
        }

        // --- Error response deserialization tests ---

        [UnityTest]
        public IEnumerator ErrorResponse_Deserialization_CodeNotFound()
        {
            var json = @"{
                ""success"": false,
                ""error_code"": 3050,
                ""error_message"": ""Redeem code not found""
            }";

            var response = JsonConvert.DeserializeObject<ErrorResponse>(json, _jsonSettings);

            Assert.IsFalse(response.Success);
            Assert.AreEqual(3050, response.ErrorCode);
            Assert.AreEqual("Redeem code not found", response.ErrorMessage);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ErrorResponse_Deserialization_AlreadyClaimed()
        {
            var json = @"{
                ""success"": false,
                ""error_code"": 3051,
                ""error_message"": ""Redeem code has already been claimed""
            }";

            var response = JsonConvert.DeserializeObject<ErrorResponse>(json, _jsonSettings);

            Assert.IsFalse(response.Success);
            Assert.AreEqual(3051, response.ErrorCode);
            Assert.AreEqual("Redeem code has already been claimed", response.ErrorMessage);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ErrorResponse_Deserialization_Expired()
        {
            var json = @"{
                ""success"": false,
                ""error_code"": 3052,
                ""error_message"": ""Redeem code has expired""
            }";

            var response = JsonConvert.DeserializeObject<ErrorResponse>(json, _jsonSettings);

            Assert.IsFalse(response.Success);
            Assert.AreEqual(3052, response.ErrorCode);
            Assert.AreEqual("Redeem code has expired", response.ErrorMessage);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ErrorResponse_Deserialization_Revoked()
        {
            var json = @"{
                ""success"": false,
                ""error_code"": 3053,
                ""error_message"": ""Redeem code has been revoked""
            }";

            var response = JsonConvert.DeserializeObject<ErrorResponse>(json, _jsonSettings);

            Assert.IsFalse(response.Success);
            Assert.AreEqual(3053, response.ErrorCode);
            Assert.AreEqual("Redeem code has been revoked", response.ErrorMessage);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ErrorResponse_Deserialization_UserRestricted()
        {
            var json = @"{
                ""success"": false,
                ""error_code"": 3054,
                ""error_message"": ""Redeem code is restricted to a specific user""
            }";

            var response = JsonConvert.DeserializeObject<ErrorResponse>(json, _jsonSettings);

            Assert.IsFalse(response.Success);
            Assert.AreEqual(3054, response.ErrorCode);
            Assert.AreEqual("Redeem code is restricted to a specific user", response.ErrorMessage);

            yield return null;
        }

        // --- DataWrapper deserialization (simulating HTTP layer envelope unwrap) ---

        [UnityTest]
        public IEnumerator Response_Deserialization_WithDataEnvelope()
        {
            var json = @"{
                ""data"": {
                    ""success"": true,
                    ""order_ids"": [2001, 2002, 2003],
                    ""message"": ""Code redeemed successfully""
                }
            }";

            var wrapper = JsonConvert.DeserializeObject<DataWrapper<ClaimRedeemCodeResponse>>(json, _jsonSettings);

            Assert.IsNotNull(wrapper.Data);
            Assert.IsTrue(wrapper.Data.Success);
            Assert.AreEqual(3, wrapper.Data.OrderIds.Length);
            Assert.AreEqual(2001, wrapper.Data.OrderIds[0]);
            Assert.AreEqual(2003, wrapper.Data.OrderIds[2]);
            Assert.AreEqual("Code redeemed successfully", wrapper.Data.Message);

            yield return null;
        }

        // --- Integration test (requires full SDK init + backend) ---

        [Ignore("Requires full SDK resources and a live backend server.")]
        [UnityTest]
        public IEnumerator ClaimRedeem_ValidCode_ReturnsOrderIds() => UniTask.ToCoroutine(async () =>
        {
            await Noctua.InitAsync();
            await Noctua.Auth.AuthenticateAsync();

            var response = await Noctua.IAP.ClaimRedeemAsync("ABCD-EFGH-IJKL-MNOP");

            Assert.IsNotNull(response);
            Assert.IsTrue(response.Success);
            Assert.IsNotNull(response.OrderIds);
            Assert.Greater(response.OrderIds.Length, 0);
        });

        // Helper class to simulate the HTTP layer data envelope unwrap
        private class DataWrapper<T>
        {
            [JsonProperty("data")]
            public T Data;
        }
    }
}
