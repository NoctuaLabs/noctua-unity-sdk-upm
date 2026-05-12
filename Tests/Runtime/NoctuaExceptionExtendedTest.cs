using System.Collections;
using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.Runtime
{
    public class NoctuaExceptionExtendedTest
    {
        [UnityTest]
        public IEnumerator ToString_ContainsAllFields()
        {
            var ex = new NoctuaException(NoctuaErrorCode.Application, "test message", "payload-data");
            var str = ex.ToString();

            Assert.IsTrue(str.Contains("3002"));
            Assert.IsTrue(str.Contains("test message"));
            Assert.IsTrue(str.Contains("payload-data"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ToString_EmptyPayload()
        {
            var ex = new NoctuaException(NoctuaErrorCode.Networking, "net error");
            var str = ex.ToString();

            Assert.IsTrue(str.Contains("3001"));
            Assert.IsTrue(str.Contains("net error"));
            Assert.IsTrue(str.Contains("Payload: "));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ErrorCode_Property_MatchesEnum()
        {
            var ex = new NoctuaException(NoctuaErrorCode.Payment, "pay error");
            Assert.AreEqual((int)NoctuaErrorCode.Payment, ex.ErrorCode);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Payload_Default_IsEmptyString()
        {
            var ex = new NoctuaException(NoctuaErrorCode.Unknown, "msg");
            Assert.AreEqual("", ex.Payload);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StaticInstances_HaveCorrectErrorCodes()
        {
            Assert.AreEqual((int)NoctuaErrorCode.Networking, NoctuaException.OtherWebRequestError.ErrorCode);
            Assert.AreEqual((int)NoctuaErrorCode.Networking, NoctuaException.RequestConnectionError.ErrorCode);
            Assert.AreEqual((int)NoctuaErrorCode.Networking, NoctuaException.RequestDataProcessingError.ErrorCode);
            Assert.AreEqual((int)NoctuaErrorCode.Networking, NoctuaException.RequestInProgress.ErrorCode);
            Assert.AreEqual((int)NoctuaErrorCode.Networking, NoctuaException.RequestProtocolError.ErrorCode);
            Assert.AreEqual((int)NoctuaErrorCode.Application, NoctuaException.RequestUnreplacedParam.ErrorCode);
            Assert.AreEqual((int)NoctuaErrorCode.Authentication, NoctuaException.MissingAccessToken.ErrorCode);
            Assert.AreEqual((int)NoctuaErrorCode.ActiveCurrencyFailure, NoctuaException.ActiveCurrencyFailure.ErrorCode);
            Assert.AreEqual((int)NoctuaErrorCode.MissingCompletionHandler, NoctuaException.MissingCompletionHandler.ErrorCode);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StaticInstances_HaveCorrectMessages()
        {
            Assert.IsTrue(NoctuaException.OtherWebRequestError.Message.Contains("Other web request error"));
            Assert.IsTrue(NoctuaException.RequestConnectionError.Message.Contains("Request connection error"));
            Assert.IsTrue(NoctuaException.MissingAccessToken.Message.Contains("Missing access token"));
            Assert.IsTrue(NoctuaException.MissingCompletionHandler.Message.Contains("Missing task completion handler"));
            yield return null;
        }
    }
}
