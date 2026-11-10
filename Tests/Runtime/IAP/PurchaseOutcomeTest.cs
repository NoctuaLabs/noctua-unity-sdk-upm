using System;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime.IAP
{
    [TestFixture]
    public class PurchaseOutcomeTest
    {
        private static readonly DateTime Started = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [Test]
        public void FromResponse_UsesOrderStatusNameAndOrderId()
        {
            var outcome = PurchaseOutcome.FromResponse("gem.100", Started,
                new PurchaseResponse { OrderId = 42, Status = OrderStatus.completed });

            Assert.AreEqual("gem.100", outcome.ProductId);
            Assert.AreEqual("completed", outcome.Status);
            Assert.AreEqual(42, outcome.OrderId);
            Assert.AreEqual(0, outcome.ErrorCode);
            Assert.AreEqual(Started, outcome.StartedAtUtc);
        }

        [Test]
        public void FromResponse_Pending_KeepsPending()
        {
            var outcome = PurchaseOutcome.FromResponse("gem.100", Started,
                new PurchaseResponse { OrderId = 7, Status = OrderStatus.pending });

            Assert.AreEqual("pending", outcome.Status);
        }

        [Test]
        public void FromResponse_Null_IsFailed()
        {
            Assert.AreEqual(PurchaseOutcome.StatusFailed,
                PurchaseOutcome.FromResponse("gem.100", Started, null).Status);
        }

        [Test]
        public void FromException_PaymentCanceled_IsCancelled()
        {
            var outcome = PurchaseOutcome.FromException("gem.100", Started,
                new NoctuaException(NoctuaErrorCode.PaymentStatusCanceled, "user cancelled"));

            Assert.AreEqual(PurchaseOutcome.StatusCancelled, outcome.Status);
            Assert.AreEqual((int)NoctuaErrorCode.PaymentStatusCanceled, outcome.ErrorCode);
            Assert.AreEqual(0, outcome.OrderId);
        }

        [Test]
        public void FromException_OtherNoctuaError_IsFailedWithCode()
        {
            var outcome = PurchaseOutcome.FromException("gem.100", Started,
                new NoctuaException(NoctuaErrorCode.Authentication, "offline"));

            Assert.AreEqual(PurchaseOutcome.StatusFailed, outcome.Status);
            Assert.AreEqual((int)NoctuaErrorCode.Authentication, outcome.ErrorCode);
        }

        [Test]
        public void FromException_PlainException_IsFailedWithoutCode()
        {
            var outcome = PurchaseOutcome.FromException("gem.100", Started, new InvalidOperationException("x"));

            Assert.AreEqual(PurchaseOutcome.StatusFailed, outcome.Status);
            Assert.AreEqual(0, outcome.ErrorCode);
        }

        [Test]
        public void NullProductId_BecomesEmpty()
        {
            Assert.AreEqual(string.Empty, new PurchaseOutcome(null, Started, "completed").ProductId);
        }
    }
}
