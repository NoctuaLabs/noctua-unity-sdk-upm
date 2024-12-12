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

    }
}