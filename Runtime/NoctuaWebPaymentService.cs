using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace com.noctuagames.sdk
{
    internal class NoctuaWebPaymentService
    {
        private readonly ILogger _log = new NoctuaLogger();
        private readonly string _basePaymentUrl;

        internal NoctuaWebPaymentService(string basePaymentUrl)
        {
            Uri paymentUri = new Uri(basePaymentUrl);
            _basePaymentUrl = paymentUri.Host + paymentUri.AbsolutePath;
        }

        internal async UniTask<PaymentResult> PayAsync(string paymentUrl)
        {

            if (string.IsNullOrEmpty(paymentUrl)) {
                throw new NoctuaException(NoctuaErrorCode.Payment, "Payment URL is empty.");
            }

            var tcs = new UniTaskCompletionSource<PaymentResult>();

            _log.Debug(paymentUrl);
            Application.OpenURL(paymentUrl);

            PaymentResult paymentResult = new PaymentResult();

            // TODO
            // 1. Open native browser payment completed dialog
            // 2. Verify the order
            // tcs.TrySetResult(new PaymentResult { Status = PaymentStatus.Confirmed, Message = "Payment confirmed" });

            try
            {
                return await tcs.Task;
            }
            finally
            {
                _log.Debug("web payment completed");
            }
        }
    }
}