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
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
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
                _log.Info($"Page started: {url}");

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
                _log.Debug("on page closed");
                tcs.TrySetResult(new PaymentResult { Status = PaymentStatus.Canceled, Message = "Payment window closed" });
            }

            bool ShouldClose(UniWebView webView)
            {
                _log.Debug("on should close");
                tcs.TrySetResult(new PaymentResult { Status = PaymentStatus.Canceled, Message = "Payment window closed" });
                
                return true;
            }

            void PageFinished(UniWebView webView, int statusCode, string url)
            {
                _log.Debug($"page finished: {url}");
                
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
            uniWebView.EmbeddedToolbar.SetBackgroundColor(new Color(31/255f, 35/255f, 39/255f));
            uniWebView.EmbeddedToolbar.Show();
            uniWebView.EmbeddedToolbar.SetDoneButtonText("Close");
            uniWebView.EmbeddedToolbar.SetButtonTextColor(Color.white);
            uniWebView.EmbeddedToolbar.SetPosition(UniWebViewToolbarPosition.Top);
            uniWebView.Frame = new Rect(0, 0, Screen.width, Screen.height);

            _log.Debug("showing web view");
            uniWebView.SetShowSpinnerWhileLoading(true);
            uniWebView.Show();
            uniWebView.Load(paymentUrl);

            try
            {
                return await tcs.Task;
            }
            finally
            {
                _log.Debug("web view closing");
                uniWebView.Hide();
                uniWebView.OnPageFinished -= PageFinished;
                uniWebView.OnPageStarted -= PageStarted;
                uniWebView.OnMultipleWindowClosed -= PageClosed;
                uniWebView.OnShouldClose -= ShouldClose;

                UnityEngine.Object.Destroy(gameObject);
            }
#else
            throw new NoctuaException(NoctuaErrorCode.Application, "Web payment is not supported in this platform");
#endif
        }
    }
}