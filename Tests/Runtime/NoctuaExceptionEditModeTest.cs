using com.noctuagames.sdk;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// EditMode NUnit tests for <see cref="NoctuaException"/>, <see cref="NoctuaErrorCode"/>,
    /// and <see cref="ErrorResponse"/>.
    ///
    /// All tests in <see cref="NoctuaExceptionExtendedTest"/> use <c>[UnityTest]</c> with
    /// <c>yield return null</c> — they run in PlayMode only and contribute zero to the
    /// EditMode coverage report.  These tests exercise the same logic as plain <c>[Test]</c>
    /// methods so the covered branches are counted in the EditMode pass.
    ///
    /// Covered:
    ///   — <see cref="NoctuaException"/> constructor → ErrorCode, Payload, Message, ToString
    ///   — <see cref="NoctuaErrorCode"/> enum: all numeric values
    ///   — Static pre-built instances (OtherWebRequestError, MissingAccessToken, …)
    ///   — <see cref="ErrorResponse"/> JSON round-trip
    /// </summary>
    [TestFixture]
    public class NoctuaExceptionEditModeTest
    {
        // ─── NoctuaErrorCode enum values ──────────────────────────────────────

        [Test]
        public void NoctuaErrorCode_Unknown_Is3000()
        {
            Assert.AreEqual(3000, (int)NoctuaErrorCode.Unknown);
        }

        [Test]
        public void NoctuaErrorCode_Networking_Is3001()
        {
            Assert.AreEqual(3001, (int)NoctuaErrorCode.Networking);
        }

        [Test]
        public void NoctuaErrorCode_Application_Is3002()
        {
            Assert.AreEqual(3002, (int)NoctuaErrorCode.Application);
        }

        [Test]
        public void NoctuaErrorCode_Authentication_Is3003()
        {
            Assert.AreEqual(3003, (int)NoctuaErrorCode.Authentication);
        }

        [Test]
        public void NoctuaErrorCode_ActiveCurrencyFailure_Is3004()
        {
            Assert.AreEqual(3004, (int)NoctuaErrorCode.ActiveCurrencyFailure);
        }

        [Test]
        public void NoctuaErrorCode_MissingCompletionHandler_Is3005()
        {
            Assert.AreEqual(3005, (int)NoctuaErrorCode.MissingCompletionHandler);
        }

        [Test]
        public void NoctuaErrorCode_Payment_Is3006()
        {
            Assert.AreEqual(3006, (int)NoctuaErrorCode.Payment);
        }

        [Test]
        public void NoctuaErrorCode_AccountStorage_Is3007()
        {
            Assert.AreEqual(3007, (int)NoctuaErrorCode.AccountStorage);
        }

        [Test]
        public void NoctuaErrorCode_PaymentStatusCanceled_Is3008()
        {
            Assert.AreEqual(3008, (int)NoctuaErrorCode.PaymentStatusCanceled);
        }

        [Test]
        public void NoctuaErrorCode_PaymentStatusItemAlreadyOwned_Is3009()
        {
            Assert.AreEqual(3009, (int)NoctuaErrorCode.PaymentStatusItemAlreadyOwned);
        }

        [Test]
        public void NoctuaErrorCode_PaymentStatusIapNotReady_Is3010()
        {
            Assert.AreEqual(3010, (int)NoctuaErrorCode.PaymentStatusIapNotReady);
        }

        [Test]
        public void NoctuaErrorCode_UserBanned_Is2202()
        {
            // UserBanned uses a different series (2xxx) to distinguish it from SDK errors
            Assert.AreEqual(2202, (int)NoctuaErrorCode.UserBanned);
        }

        // ─── NoctuaException constructor ──────────────────────────────────────

        [Test]
        public void Constructor_SetsErrorCode_AsIntegerOfEnum()
        {
            var ex = new NoctuaException(NoctuaErrorCode.Application, "test message");
            Assert.AreEqual((int)NoctuaErrorCode.Application, ex.ErrorCode);
            Assert.AreEqual(3002, ex.ErrorCode);
        }

        [Test]
        public void Constructor_WithoutPayload_PayloadIsEmptyString()
        {
            var ex = new NoctuaException(NoctuaErrorCode.Unknown, "some error");
            Assert.AreEqual("", ex.Payload);
        }

        [Test]
        public void Constructor_WithPayload_PayloadIsPreserved()
        {
            var ex = new NoctuaException(NoctuaErrorCode.Authentication, "auth failed", "extra-payload-data");
            Assert.AreEqual("extra-payload-data", ex.Payload);
        }

        [Test]
        public void Constructor_Message_ContainsEnumName()
        {
            var ex = new NoctuaException(NoctuaErrorCode.Networking, "connection refused");
            // Base message format: "ErrorCode: {enum}, Message: \"{message}\""
            StringAssert.Contains("Networking", ex.Message);
        }

        [Test]
        public void Constructor_Message_ContainsHumanMessage()
        {
            var ex = new NoctuaException(NoctuaErrorCode.Payment, "payment declined");
            StringAssert.Contains("payment declined", ex.Message);
        }

        [Test]
        public void Constructor_IsException_Subclass()
        {
            var ex = new NoctuaException(NoctuaErrorCode.Unknown, "test");
            Assert.IsInstanceOf<System.Exception>(ex);
        }

        // ─── NoctuaException.ToString ─────────────────────────────────────────

        [Test]
        public void ToString_ContainsErrorCode()
        {
            var ex = new NoctuaException(NoctuaErrorCode.Application, "test message", "payload-data");
            var str = ex.ToString();
            StringAssert.Contains("3002", str);
        }

        [Test]
        public void ToString_ContainsHumanMessage()
        {
            var ex = new NoctuaException(NoctuaErrorCode.Application, "test message", "payload-data");
            var str = ex.ToString();
            StringAssert.Contains("test message", str);
        }

        [Test]
        public void ToString_ContainsPayload()
        {
            var ex = new NoctuaException(NoctuaErrorCode.Application, "test message", "payload-data");
            var str = ex.ToString();
            StringAssert.Contains("payload-data", str);
        }

        [Test]
        public void ToString_EmptyPayload_ContainsPayloadLabel()
        {
            var ex = new NoctuaException(NoctuaErrorCode.Networking, "net error");
            var str = ex.ToString();
            StringAssert.Contains("Payload: ", str);
        }

        [Test]
        public void ToString_WithPayload_FormatIsConsistent()
        {
            var ex = new NoctuaException(NoctuaErrorCode.Networking, "net error", "payload");
            var str = ex.ToString();
            // Expected: "ErrorCode: 3001, Message: ..., Payload: payload"
            StringAssert.Contains("ErrorCode:", str);
            StringAssert.Contains("Message:", str);
            StringAssert.Contains("Payload:", str);
        }

        // ─── Static pre-built instances ───────────────────────────────────────

        [Test]
        public void OtherWebRequestError_HasNetworkingErrorCode()
        {
            Assert.AreEqual((int)NoctuaErrorCode.Networking, NoctuaException.OtherWebRequestError.ErrorCode);
        }

        [Test]
        public void RequestConnectionError_HasNetworkingErrorCode()
        {
            Assert.AreEqual((int)NoctuaErrorCode.Networking, NoctuaException.RequestConnectionError.ErrorCode);
        }

        [Test]
        public void RequestDataProcessingError_HasNetworkingErrorCode()
        {
            Assert.AreEqual((int)NoctuaErrorCode.Networking, NoctuaException.RequestDataProcessingError.ErrorCode);
        }

        [Test]
        public void RequestInProgress_HasNetworkingErrorCode()
        {
            Assert.AreEqual((int)NoctuaErrorCode.Networking, NoctuaException.RequestInProgress.ErrorCode);
        }

        [Test]
        public void RequestProtocolError_HasNetworkingErrorCode()
        {
            Assert.AreEqual((int)NoctuaErrorCode.Networking, NoctuaException.RequestProtocolError.ErrorCode);
        }

        [Test]
        public void RequestUnreplacedParam_HasApplicationErrorCode()
        {
            Assert.AreEqual((int)NoctuaErrorCode.Application, NoctuaException.RequestUnreplacedParam.ErrorCode);
        }

        [Test]
        public void MissingAccessToken_HasAuthenticationErrorCode()
        {
            Assert.AreEqual((int)NoctuaErrorCode.Authentication, NoctuaException.MissingAccessToken.ErrorCode);
        }

        [Test]
        public void ActiveCurrencyFailure_HasActiveCurrencyFailureErrorCode()
        {
            Assert.AreEqual((int)NoctuaErrorCode.ActiveCurrencyFailure, NoctuaException.ActiveCurrencyFailure.ErrorCode);
        }

        [Test]
        public void MissingCompletionHandler_HasMissingCompletionHandlerErrorCode()
        {
            Assert.AreEqual((int)NoctuaErrorCode.MissingCompletionHandler, NoctuaException.MissingCompletionHandler.ErrorCode);
        }

        [Test]
        public void OtherWebRequestError_MessageContainsOtherWebRequest()
        {
            StringAssert.Contains("Other web request error", NoctuaException.OtherWebRequestError.Message);
        }

        [Test]
        public void RequestConnectionError_MessageContainsConnectionError()
        {
            StringAssert.Contains("Request connection error", NoctuaException.RequestConnectionError.Message);
        }

        [Test]
        public void MissingAccessToken_MessageContainsMissingAccessToken()
        {
            StringAssert.Contains("Missing access token", NoctuaException.MissingAccessToken.Message);
        }

        [Test]
        public void MissingCompletionHandler_MessageContainsMissingTaskCompletion()
        {
            StringAssert.Contains("Missing task completion handler", NoctuaException.MissingCompletionHandler.Message);
        }

        [Test]
        public void StaticInstances_AreNotNull()
        {
            Assert.IsNotNull(NoctuaException.OtherWebRequestError);
            Assert.IsNotNull(NoctuaException.RequestConnectionError);
            Assert.IsNotNull(NoctuaException.RequestDataProcessingError);
            Assert.IsNotNull(NoctuaException.RequestInProgress);
            Assert.IsNotNull(NoctuaException.RequestProtocolError);
            Assert.IsNotNull(NoctuaException.RequestUnreplacedParam);
            Assert.IsNotNull(NoctuaException.MissingAccessToken);
            Assert.IsNotNull(NoctuaException.ActiveCurrencyFailure);
            Assert.IsNotNull(NoctuaException.MissingCompletionHandler);
        }

        // ─── ErrorResponse JSON round-trip ────────────────────────────────────

        [Test]
        public void ErrorResponse_Deserialize_Success_IsFalse()
        {
            const string json = @"{ ""success"": false, ""error_message"": ""Not found"", ""error_code"": 404 }";
            var response = JsonConvert.DeserializeObject<ErrorResponse>(json);
            Assert.IsFalse(response.Success);
        }

        [Test]
        public void ErrorResponse_Deserialize_ErrorMessage_IsPopulated()
        {
            const string json = @"{ ""success"": false, ""error_message"": ""Unauthorized"", ""error_code"": 401 }";
            var response = JsonConvert.DeserializeObject<ErrorResponse>(json);
            Assert.AreEqual("Unauthorized", response.ErrorMessage);
        }

        [Test]
        public void ErrorResponse_Deserialize_ErrorCode_IsPreserved()
        {
            const string json = @"{ ""success"": false, ""error_message"": ""Server error"", ""error_code"": 500 }";
            var response = JsonConvert.DeserializeObject<ErrorResponse>(json);
            Assert.AreEqual(500, response.ErrorCode);
        }

        [Test]
        public void ErrorResponse_Serialize_UsesSnakeCaseKeys()
        {
            var response = new ErrorResponse
            {
                Success = false,
                ErrorMessage = "bad request",
                ErrorCode = 400
            };
            var json = JsonConvert.SerializeObject(response);
            StringAssert.Contains("\"success\"", json);
            StringAssert.Contains("\"error_message\"", json);
            StringAssert.Contains("\"error_code\"", json);
        }

        [Test]
        public void ErrorResponse_DefaultCtor_SuccessIsFalse()
        {
            var response = new ErrorResponse();
            Assert.IsFalse(response.Success);
        }

        [Test]
        public void ErrorResponse_DefaultCtor_NullFields()
        {
            var response = new ErrorResponse();
            Assert.IsNull(response.ErrorMessage);
            Assert.AreEqual(0, response.ErrorCode);
        }
    }
}
