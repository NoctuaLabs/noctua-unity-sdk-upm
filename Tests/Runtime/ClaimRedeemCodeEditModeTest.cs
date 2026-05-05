using com.noctuagames.sdk;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// EditMode NUnit tests for <see cref="ClaimRedeemCodeRequest"/> and
    /// <see cref="ClaimRedeemCodeResponse"/> JSON serialisation.
    ///
    /// All 12 runnable tests in <c>ClaimRedeemCodeTest</c> use <c>[UnityTest]</c> /
    /// <c>yield return null</c> despite every method under test being entirely synchronous
    /// JSON serialisation — they run in PlayMode only and contribute zero to the EditMode
    /// coverage report.  These plain <c>[Test]</c> counterparts ensure the same branches
    /// are counted during the EditMode pass.
    ///
    /// Covered:
    ///   — <see cref="ClaimRedeemCodeRequest"/> — JSON key names, round-trip serialisation
    ///   — <see cref="ClaimRedeemCodeResponse"/> — success/failure payloads, empty/null fields
    ///   — <see cref="ErrorResponse"/> with redeem-specific error codes
    ///   — DataWrapper (data-envelope unwrap pattern)
    /// </summary>
    [TestFixture]
    public class ClaimRedeemCodeEditModeTest
    {
        // Shared settings: snake_case keys + NullValueHandling.Ignore
        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            },
            NullValueHandling = NullValueHandling.Ignore,
        };

        // ─── ClaimRedeemCodeRequest serialisation ─────────────────────────────

        [Test]
        public void Request_Serialization_ContainsSnakeCaseCodeKey()
        {
            var request = new ClaimRedeemCodeRequest { Code = "ABCD-EFGH-IJKL-MNOP", UserId = 12345 };
            var json = JsonConvert.SerializeObject(request, JsonSettings);
            StringAssert.Contains("\"code\"", json);
        }

        [Test]
        public void Request_Serialization_ContainsSnakeCaseUserIdKey()
        {
            var request = new ClaimRedeemCodeRequest { Code = "ABCD-EFGH-IJKL-MNOP", UserId = 12345 };
            var json = JsonConvert.SerializeObject(request, JsonSettings);
            StringAssert.Contains("\"user_id\"", json);
        }

        [Test]
        public void Request_Serialization_ContainsCodeValue()
        {
            var request = new ClaimRedeemCodeRequest { Code = "ABCD-EFGH-IJKL-MNOP", UserId = 12345 };
            var json = JsonConvert.SerializeObject(request, JsonSettings);
            StringAssert.Contains("ABCD-EFGH-IJKL-MNOP", json);
        }

        [Test]
        public void Request_Serialization_ContainsUserIdValue()
        {
            var request = new ClaimRedeemCodeRequest { Code = "ABCD-EFGH-IJKL-MNOP", UserId = 12345 };
            var json = JsonConvert.SerializeObject(request, JsonSettings);
            StringAssert.Contains("12345", json);
        }

        [Test]
        public void Request_Deserialization_RoundTrip_PreservesCode()
        {
            var original = new ClaimRedeemCodeRequest { Code = "A1B2-C3D4-E5F6-G7H8", UserId = 99999 };
            var json = JsonConvert.SerializeObject(original, JsonSettings);
            var back = JsonConvert.DeserializeObject<ClaimRedeemCodeRequest>(json, JsonSettings);
            Assert.AreEqual(original.Code,   back.Code);
            Assert.AreEqual(original.UserId, back.UserId);
        }

        // ─── ClaimRedeemCodeResponse deserialization ───────────────────────────

        [Test]
        public void Response_Deserialization_SuccessTrue_ParsedCorrectly()
        {
            const string json = @"{
                ""success"": true,
                ""order_ids"": [1001, 1002],
                ""message"": ""Code redeemed successfully""
            }";
            var response = JsonConvert.DeserializeObject<ClaimRedeemCodeResponse>(json, JsonSettings);
            Assert.IsTrue(response.Success);
        }

        [Test]
        public void Response_Deserialization_OrderIds_ParsedCorrectly()
        {
            const string json = @"{
                ""success"": true,
                ""order_ids"": [1001, 1002],
                ""message"": ""Code redeemed successfully""
            }";
            var response = JsonConvert.DeserializeObject<ClaimRedeemCodeResponse>(json, JsonSettings);
            Assert.AreEqual(2,    response.OrderIds.Length);
            Assert.AreEqual(1001, response.OrderIds[0]);
            Assert.AreEqual(1002, response.OrderIds[1]);
        }

        [Test]
        public void Response_Deserialization_Message_ParsedCorrectly()
        {
            const string json = @"{
                ""success"": true,
                ""order_ids"": [1001],
                ""message"": ""Code redeemed successfully""
            }";
            var response = JsonConvert.DeserializeObject<ClaimRedeemCodeResponse>(json, JsonSettings);
            Assert.AreEqual("Code redeemed successfully", response.Message);
        }

        [Test]
        public void Response_Deserialization_EmptyOrderIds_IsEmptyArray()
        {
            const string json = @"{""success"": true, ""order_ids"": [], ""message"": ""OK""}";
            var response = JsonConvert.DeserializeObject<ClaimRedeemCodeResponse>(json, JsonSettings);
            Assert.IsTrue(response.Success);
            Assert.IsNotNull(response.OrderIds);
            Assert.AreEqual(0, response.OrderIds.Length);
        }

        [Test]
        public void Response_Deserialization_NullMessage_IsNull()
        {
            const string json = @"{""success"": true, ""order_ids"": [5001]}";
            var response = JsonConvert.DeserializeObject<ClaimRedeemCodeResponse>(json, JsonSettings);
            Assert.IsNull(response.Message);
            Assert.AreEqual(5001, response.OrderIds[0]);
        }

        [Test]
        public void Response_Deserialization_FailureResponse_SuccessIsFalse()
        {
            const string json = @"{""success"": false, ""order_ids"": [], ""message"": null}";
            var response = JsonConvert.DeserializeObject<ClaimRedeemCodeResponse>(json, JsonSettings);
            Assert.IsFalse(response.Success);
        }

        // ─── ErrorResponse — redeem-specific error codes ──────────────────────

        [Test]
        public void ErrorResponse_Deserialization_CodeNotFound_ErrorCode3050()
        {
            const string json = @"{
                ""success"": false,
                ""error_code"": 3050,
                ""error_message"": ""Redeem code not found""
            }";
            var response = JsonConvert.DeserializeObject<ErrorResponse>(json, JsonSettings);
            Assert.IsFalse(response.Success);
            Assert.AreEqual(3050,                  response.ErrorCode);
            Assert.AreEqual("Redeem code not found", response.ErrorMessage);
        }

        [Test]
        public void ErrorResponse_Deserialization_AlreadyClaimed_ErrorCode3051()
        {
            const string json = @"{
                ""success"": false,
                ""error_code"": 3051,
                ""error_message"": ""Redeem code has already been claimed""
            }";
            var response = JsonConvert.DeserializeObject<ErrorResponse>(json, JsonSettings);
            Assert.AreEqual(3051, response.ErrorCode);
            Assert.AreEqual("Redeem code has already been claimed", response.ErrorMessage);
        }

        [Test]
        public void ErrorResponse_Deserialization_Expired_ErrorCode3052()
        {
            const string json = @"{
                ""success"": false,
                ""error_code"": 3052,
                ""error_message"": ""Redeem code has expired""
            }";
            var response = JsonConvert.DeserializeObject<ErrorResponse>(json, JsonSettings);
            Assert.AreEqual(3052, response.ErrorCode);
        }

        [Test]
        public void ErrorResponse_Deserialization_Revoked_ErrorCode3053()
        {
            const string json = @"{
                ""success"": false,
                ""error_code"": 3053,
                ""error_message"": ""Redeem code has been revoked""
            }";
            var response = JsonConvert.DeserializeObject<ErrorResponse>(json, JsonSettings);
            Assert.AreEqual(3053, response.ErrorCode);
        }

        [Test]
        public void ErrorResponse_Deserialization_UserRestricted_ErrorCode3054()
        {
            const string json = @"{
                ""success"": false,
                ""error_code"": 3054,
                ""error_message"": ""Redeem code is restricted to a specific user""
            }";
            var response = JsonConvert.DeserializeObject<ErrorResponse>(json, JsonSettings);
            Assert.AreEqual(3054, response.ErrorCode);
            Assert.AreEqual("Redeem code is restricted to a specific user", response.ErrorMessage);
        }

        // ─── DataWrapper data-envelope unwrap ─────────────────────────────────

        [Test]
        public void Response_Deserialization_WithDataEnvelope_UnwrapsCorrectly()
        {
            const string json = @"{
                ""data"": {
                    ""success"": true,
                    ""order_ids"": [2001, 2002, 2003],
                    ""message"": ""Code redeemed successfully""
                }
            }";
            var wrapper = JsonConvert.DeserializeObject<DataWrapper<ClaimRedeemCodeResponse>>(json, JsonSettings);
            Assert.IsNotNull(wrapper.Data);
            Assert.IsTrue(wrapper.Data.Success);
            Assert.AreEqual(3,    wrapper.Data.OrderIds.Length);
            Assert.AreEqual(2001, wrapper.Data.OrderIds[0]);
            Assert.AreEqual(2003, wrapper.Data.OrderIds[2]);
            Assert.AreEqual("Code redeemed successfully", wrapper.Data.Message);
        }

        // Helper: simulates the SDK HTTP layer data-envelope unwrap
        private class DataWrapper<T>
        {
            [JsonProperty("data")]
            public T Data;
        }
    }
}
