#undef UNITY_EDITOR

using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace com.noctuagames.sdk
{
    internal class NoctuaWebPaymentService
    {
        private readonly string _basePaymentUrl;

        internal NoctuaWebPaymentService(string basePaymentUrl)
        {
            Uri paymentUri = new Uri(basePaymentUrl);
            _basePaymentUrl = paymentUri.Host + paymentUri.AbsolutePath;
        }

        internal async UniTask<PaymentResult> PayAsync(string paymentUrl)
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (!paymentUrl.Contains(_basePaymentUrl))
            {
                throw new NoctuaException(NoctuaErrorCode.Payment, $"Invalid payment URL: {paymentUrl}. Base URL: {_basePaymentUrl}");
            }

            var gameObject = new GameObject("SocialLoginWebView");
            var uniWebView = gameObject.AddComponent<UniWebView>();

            if (Application.platform == RuntimePlatform.Android)
            {
                uniWebView.SetUserAgent("Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/118.0.0.0 Mobile Safari/537.3");
            }
            else if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                uniWebView.SetUserAgent("Mozilla/5.0 (iPhone; CPU iPhone OS 14_4 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/14.0 Mobile/15E148 Safari/604.1");
            }

            var tcs = new UniTaskCompletionSource<PaymentResult>();

            void PageStarted(UniWebView webView, string url)
            {
                Debug.Log($"Page started: {url}");

                if (!url.Contains(_basePaymentUrl))
                {
                    return;
                }
                
                if (url.Contains("status=confirmed"))
                {
                    tcs.TrySetResult(new PaymentResult { Status = PaymentStatus.Confirmed, Message = "Payment confirmed" });
                }
                else if (url.Contains("status=cancel"))
                {
                    tcs.TrySetResult(new PaymentResult { Status = PaymentStatus.Canceled, Message = "Payment canceled" });
                }
                else if (url.Contains("status=failure"))
                {
                    tcs.TrySetResult(new PaymentResult { Status = PaymentStatus.Failed, Message = "Payment failed" });
                }
                else if (url.Contains("status=success"))
                {
                    tcs.TrySetResult(new PaymentResult { Status = PaymentStatus.Successful, ReceiptData = "" });
                }
            }

            void PageClosed(UniWebView webView, string windowId)
            {
                Debug.Log("Page closed");
                tcs.TrySetResult(new PaymentResult { Status = PaymentStatus.Canceled, Message = "Payment window closed" });
            }

            bool ShouldClose(UniWebView webView)
            {
                Debug.Log("Should close");
                tcs.TrySetResult(new PaymentResult { Status = PaymentStatus.Canceled, Message = "Payment window closed" });
                
                return true;
            }

            void PageFinished(UniWebView webView, int statusCode, string url)
            {
                Debug.Log($"Page finished: {url}");
                
                if (!url.Contains(_basePaymentUrl))
                {
                    return;
                }

                if (url.Contains("status=confirmed"))
                {
                    tcs.TrySetResult(
                        new PaymentResult { Status = PaymentStatus.Confirmed, Message = "Payment confirmed" }
                    );
                }
                else if (url.Contains("status=cancel"))
                {
                    tcs.TrySetResult(
                        new PaymentResult { Status = PaymentStatus.Canceled, Message = "Payment canceled" }
                    );
                }
                else if (url.Contains("status=failure"))
                {
                    tcs.TrySetResult(new PaymentResult { Status = PaymentStatus.Failed, Message = "Payment failed" });
                }
                else if (url.Contains("status=success"))
                {
                    tcs.TrySetResult(new PaymentResult { Status = PaymentStatus.Successful, ReceiptData = "" });
                }
            }

            uniWebView.OnPageFinished += PageFinished;
            uniWebView.OnPageStarted += PageStarted;
            uniWebView.OnMultipleWindowClosed += PageClosed;
            uniWebView.OnShouldClose += ShouldClose;

            uniWebView.SetBackButtonEnabled(true);
            uniWebView.EmbeddedToolbar.Show();
            uniWebView.EmbeddedToolbar.SetDoneButtonText("Close");
            uniWebView.EmbeddedToolbar.SetPosition(UniWebViewToolbarPosition.Top);
            uniWebView.Frame = new Rect(0, 0, Screen.width, Screen.height);

            Debug.Log("Showing WebView");
            uniWebView.Show();
            uniWebView.Load(paymentUrl);

            try
            {
                return await tcs.Task;
            }
            finally
            {
                Debug.Log("Closing WebView");
                uniWebView.Hide();
                uniWebView.OnPageFinished -= PageFinished;
                uniWebView.OnPageStarted -= PageStarted;
                uniWebView.OnMultipleWindowClosed -= PageClosed;
                uniWebView.OnShouldClose -= ShouldClose;

                Object.Destroy(gameObject);
            }
#else
            throw new NoctuaException(NoctuaErrorCode.Application, "Web payment is not supported in this platform");
#endif
        }
    }
}