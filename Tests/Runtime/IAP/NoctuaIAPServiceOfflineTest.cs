using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using com.noctuagames.sdk;

namespace Tests.Runtime.IAP
{
    /// <summary>
    /// Offline-safety regression tests for the purchase flow: when the device is
    /// offline, PurchaseItemAsync must fail FAST and CLEAN — a controlled
    /// NoctuaException, the retry dialog offered, the loading spinner dismissed —
    /// and the service must remain fully usable for the next attempt (nothing
    /// hung, no purchase-flow gate leaked).
    /// </summary>
    [TestFixture]
    public class NoctuaIAPServiceOfflineTest
    {
        private const int HangTimeoutMs = 5000;

        // ── Mocks ──────────────────────────────────────────────────────────────

        private class OfflineConnectivity : IConnectivityProvider
        {
            public UniTask<bool> IsOfflineAsync() => UniTask.FromResult(true);
            public bool IsInitialized() => true;
            public UniTask InitAsync() => UniTask.CompletedTask;
        }

        private class NoRetryPaymentUI : IPaymentUI
        {
            public int RetryDialogShownCount;
            public bool LoadingShownLast; // last value passed to ShowLoadingProgress

            public UniTask<string> ShowCustomPaymentCompleteDialog(bool nativePaymentButtonEnabled)
                => UniTask.FromResult("cancel");

            public UniTask<bool> ShowFailedPaymentDialog(PaymentStatus status)
                => UniTask.FromResult(false);

            public void ShowLoadingProgress(bool show) => LoadingShownLast = show;

            public UniTask<bool> ShowRetryDialog(string message, string context = "general")
            {
                RetryDialogShownCount++;
                return UniTask.FromResult(false); // user declines retry
            }

            public void ShowError(string message) { }
            public void ShowError(LocaleTextKey textKey) { }
            public void ShowGeneralNotification(string message, bool isSuccess = false, uint durationMs = 3000) { }

#if UNITY_EDITOR
            public UniTask<bool> ShowEditorPaymentSheet(string productId, string price, string currency)
                => UniTask.FromResult(false);
#endif
        }

        private class StubLocale : ILocaleProvider
        {
            public string GetLanguage() => "en";
            public string GetCountry() => "US";
            public string GetCurrency() => "USD";
            public string GetTranslation(LocaleTextKey textKey) => textKey.ToString();
        }

        private static NoctuaIAPService CreateOfflineService(NoRetryPaymentUI paymentUI)
        {
            return new NoctuaIAPService(
                config: new NoctuaIAPService.Config
                {
                    BaseUrl  = "https://api.example.com",
                    ClientId = "test-client-id"
                },
                accessTokenProvider: null, // offline path throws before token use
                paymentUI:           paymentUI,
                nativePlugin:        null,
                localeProvider:      new StubLocale(),
                connectivity:        new OfflineConnectivity()
            );
        }

        private static PurchaseRequest MakeRequest() => new PurchaseRequest
        {
            ProductId = "offline_test_product",
            Price     = 0.99m,
            Currency  = "USD"
        };

        /// <summary>
        /// Awaits the purchase, expecting a NoctuaException, racing a hang timeout.
        /// Returns the caught exception; fails the test on hang or wrong outcome.
        /// </summary>
        private static async UniTask<NoctuaException> AwaitOfflinePurchase(NoctuaIAPService svc)
        {
            NoctuaException caught = null;
            Exception unexpected = null;

            var purchase = UniTask.Create(async () =>
            {
                try
                {
                    await svc.PurchaseItemAsync(MakeRequest());
                }
                catch (NoctuaException e)
                {
                    caught = e;
                }
                catch (Exception e)
                {
                    unexpected = e;
                }
            });

            var winner = await UniTask.WhenAny(purchase, UniTask.Delay(HangTimeoutMs));

            Assert.AreEqual(0, winner,
                $"PurchaseItemAsync did not complete within {HangTimeoutMs} ms while offline — process is hung");
            Assert.IsNull(unexpected,
                $"Offline purchase must fail with NoctuaException, not {unexpected?.GetType().Name}: {unexpected?.Message}");
            Assert.IsNotNull(caught, "Offline purchase must throw NoctuaException");

            return caught;
        }

        // ── Tests ──────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator PurchaseItemAsync_WhenOffline_FailsFastWithNoctuaException() =>
            UniTask.ToCoroutine(async () =>
            {
                var paymentUI = new NoRetryPaymentUI();
                var svc = CreateOfflineService(paymentUI);

                await AwaitOfflinePurchase(svc);

                Assert.AreEqual(1, paymentUI.RetryDialogShownCount,
                    "Offline purchase must offer the retry dialog exactly once");
                Assert.IsFalse(paymentUI.LoadingShownLast,
                    "Loading spinner must be dismissed before the offline throw (no stuck spinner)");
            });

        [UnityTest]
        public IEnumerator PurchaseItemAsync_WhenOffline_ServiceStaysUsableForNextAttempt() =>
            UniTask.ToCoroutine(async () =>
            {
                var paymentUI = new NoRetryPaymentUI();
                var svc = CreateOfflineService(paymentUI);

                // First attempt fails clean...
                await AwaitOfflinePurchase(svc);

                // ...and a second attempt on the SAME instance must behave identically:
                // proves the failure left no hung await, lock, or stale payment state.
                await AwaitOfflinePurchase(svc);

                Assert.AreEqual(2, paymentUI.RetryDialogShownCount,
                    "Both offline attempts must reach the retry dialog");
            });
    }
}
