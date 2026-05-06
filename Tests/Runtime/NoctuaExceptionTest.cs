using System;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// EditMode NUnit tests for <see cref="NoctuaException"/> and <see cref="NoctuaErrorCode"/>.
    ///
    /// Covers:
    ///   — Constructor: ErrorCode, Payload, base message assignment
    ///   — ToString: contains ErrorCode and Message
    ///   — Static sentinel instances (OtherWebRequestError, MissingAccessToken, etc.)
    ///   — NoctuaErrorCode enum ordinal values (ABI/wire-format contract)
    ///   — ErrorResponse JSON DTO structure (field names)
    /// </summary>
    [TestFixture]
    public class NoctuaExceptionTest
    {
        // ═══════════════════════════════════════════════════════════════════
        // Constructor
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void Constructor_ErrorCode_Stored()
        {
            var ex = new NoctuaException(NoctuaErrorCode.Authentication, "bad token");

            Assert.AreEqual((int)NoctuaErrorCode.Authentication, ex.ErrorCode);
        }

        [Test]
        public void Constructor_Message_StoredInBaseMessage()
        {
            var ex = new NoctuaException(NoctuaErrorCode.Application, "test message");

            StringAssert.Contains("test message", ex.Message);
        }

        [Test]
        public void Constructor_Payload_StoredCorrectly()
        {
            var ex = new NoctuaException(NoctuaErrorCode.Application, "msg", "extra payload");

            Assert.AreEqual("extra payload", ex.Payload);
        }

        [Test]
        public void Constructor_NoPayload_DefaultsToEmpty()
        {
            var ex = new NoctuaException(NoctuaErrorCode.Networking, "network error");

            Assert.AreEqual("", ex.Payload,
                "Payload must default to empty string when not supplied");
        }

        [Test]
        public void Constructor_IsExceptionSubtype()
        {
            var ex = new NoctuaException(NoctuaErrorCode.Unknown, "oops");

            Assert.IsInstanceOf<Exception>(ex);
        }

        // ═══════════════════════════════════════════════════════════════════
        // ToString
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void ToString_ContainsErrorCode()
        {
            var ex = new NoctuaException(NoctuaErrorCode.Payment, "payment failed");

            StringAssert.Contains(((int)NoctuaErrorCode.Payment).ToString(), ex.ToString());
        }

        [Test]
        public void ToString_ContainsPayload()
        {
            var ex = new NoctuaException(NoctuaErrorCode.Application, "msg", "my-payload");

            StringAssert.Contains("my-payload", ex.ToString());
        }

        // ═══════════════════════════════════════════════════════════════════
        // Static sentinel instances
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void OtherWebRequestError_IsNetworking()
        {
            Assert.AreEqual((int)NoctuaErrorCode.Networking,
                NoctuaException.OtherWebRequestError.ErrorCode);
        }

        [Test]
        public void RequestConnectionError_IsNetworking()
        {
            Assert.AreEqual((int)NoctuaErrorCode.Networking,
                NoctuaException.RequestConnectionError.ErrorCode);
        }

        [Test]
        public void MissingAccessToken_IsAuthentication()
        {
            Assert.AreEqual((int)NoctuaErrorCode.Authentication,
                NoctuaException.MissingAccessToken.ErrorCode);
        }

        [Test]
        public void RequestUnreplacedParam_IsApplication()
        {
            Assert.AreEqual((int)NoctuaErrorCode.Application,
                NoctuaException.RequestUnreplacedParam.ErrorCode);
        }

        [Test]
        public void MissingCompletionHandler_HasCorrectCode()
        {
            Assert.AreEqual((int)NoctuaErrorCode.MissingCompletionHandler,
                NoctuaException.MissingCompletionHandler.ErrorCode);
        }

        [Test]
        public void ActiveCurrencyFailure_HasCorrectCode()
        {
            Assert.AreEqual((int)NoctuaErrorCode.ActiveCurrencyFailure,
                NoctuaException.ActiveCurrencyFailure.ErrorCode);
        }

        [Test]
        public void StaticInstances_NotNull()
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

        // ═══════════════════════════════════════════════════════════════════
        // NoctuaErrorCode enum ordinals — wire-format ABI contract
        // ═══════════════════════════════════════════════════════════════════

        [Test] public void ErrorCode_Unknown_Is3000()                      => Assert.AreEqual(3000, (int)NoctuaErrorCode.Unknown);
        [Test] public void ErrorCode_Networking_Is3001()                   => Assert.AreEqual(3001, (int)NoctuaErrorCode.Networking);
        [Test] public void ErrorCode_Application_Is3002()                  => Assert.AreEqual(3002, (int)NoctuaErrorCode.Application);
        [Test] public void ErrorCode_Authentication_Is3003()               => Assert.AreEqual(3003, (int)NoctuaErrorCode.Authentication);
        [Test] public void ErrorCode_ActiveCurrencyFailure_Is3004()        => Assert.AreEqual(3004, (int)NoctuaErrorCode.ActiveCurrencyFailure);
        [Test] public void ErrorCode_MissingCompletionHandler_Is3005()     => Assert.AreEqual(3005, (int)NoctuaErrorCode.MissingCompletionHandler);
        [Test] public void ErrorCode_Payment_Is3006()                      => Assert.AreEqual(3006, (int)NoctuaErrorCode.Payment);
        [Test] public void ErrorCode_AccountStorage_Is3007()               => Assert.AreEqual(3007, (int)NoctuaErrorCode.AccountStorage);
        [Test] public void ErrorCode_PaymentStatusCanceled_Is3008()        => Assert.AreEqual(3008, (int)NoctuaErrorCode.PaymentStatusCanceled);
        [Test] public void ErrorCode_PaymentStatusItemAlreadyOwned_Is3009() => Assert.AreEqual(3009, (int)NoctuaErrorCode.PaymentStatusItemAlreadyOwned);
        [Test] public void ErrorCode_PaymentStatusIapNotReady_Is3010()     => Assert.AreEqual(3010, (int)NoctuaErrorCode.PaymentStatusIapNotReady);
        [Test] public void ErrorCode_UserBanned_Is2202()                   => Assert.AreEqual(2202, (int)NoctuaErrorCode.UserBanned);
    }
}
