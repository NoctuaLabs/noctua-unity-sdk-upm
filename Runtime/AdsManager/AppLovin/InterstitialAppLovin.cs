#if UNITY_APPLOVIN
using Cysharp.Threading.Tasks;
using System;

namespace com.noctuagames.sdk.AppLovin
{
    public class InterstitialAppLovin
    {
        private readonly NoctuaLogger _log = new(typeof(InterstitialAppLovin));

        private string _adUnitIDInterstitial;

        int retryAttempt;

        public void LoadInterstitial(string adUnitID)
        {
            if (adUnitID == null)
            {
                _log.Error("Ad unit ID Interstitial is empty.");
                return;
            }

            _adUnitIDInterstitial = adUnitID;

            // Attach callback
            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += OnInterstitialLoadedEvent;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += OnInterstitialLoadFailedEvent;
            MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent += OnInterstitialDisplayedEvent;
            MaxSdkCallbacks.Interstitial.OnAdClickedEvent += OnInterstitialClickedEvent;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += OnInterstitialHiddenEvent;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += OnInterstitialAdFailedToDisplayEvent;

            // Load the first interstitial
            LoadInterstitial();

            _log.Debug("Interstitial ad loaded for ad unit id : " + adUnitID);
        }

        public void ShowInterstitial()
        {
            if (MaxSdk.IsInterstitialReady(_adUnitIDInterstitial) )
            {
                MaxSdk.ShowInterstitial(_adUnitIDInterstitial);

                _log.Debug("Showing interstitial ad for ad unit id : " + _adUnitIDInterstitial);
            }
        }

        private void LoadInterstitial()
        {
            MaxSdk.LoadInterstitial(_adUnitIDInterstitial);

            _log.Debug("Loading interstitial ad for ad unit id : " + _adUnitIDInterstitial);
        }

        private void OnInterstitialLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            // Interstitial ad is ready for you to show. MaxSdk.IsInterstitialReady(adUnitId) now returns 'true'

            // Reset retry attempt
            retryAttempt = 0;

            _log.Debug("Interstitial ad loaded for ad unit id : " + adUnitId);
        }

        private void OnInterstitialLoadFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            // Interstitial ad failed to load
            // AppLovin recommends that you retry with exponentially higher delays, up to a maximum delay (in this case 64 seconds)
            RetryLoadInterstitialAsync().Forget();

            _log.Debug("Interstitial ad failed to load for ad unit id : " + adUnitId + " with error code : " + errorInfo.Code);
        }

        // Async method handling the delay
        private async UniTaskVoid RetryLoadInterstitialAsync()
        {
            retryAttempt++;
            double retryDelay = Math.Pow(2, Math.Min(6, retryAttempt));

            await UniTask.Delay((int)(retryDelay * 1000));
            LoadInterstitial();

            _log.Debug("Retrying to load interstitial ad after " + retryDelay + " seconds");
        }

        private void OnInterstitialDisplayedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) {
            // Interstitial ad displayed

            _log.Debug("Interstitial ad displayed for ad unit id : " + adUnitId);
        }

        private void OnInterstitialAdFailedToDisplayEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
        {
            // Interstitial ad failed to display. AppLovin recommends that you load the next ad.
            LoadInterstitial();

            _log.Debug("Interstitial ad failed to display for ad unit id : " + adUnitId + " with error code : " + errorInfo.Code);
        }

        private void OnInterstitialClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) {

            _log.Debug("Interstitial ad clicked for ad unit id : " + adUnitId);
        }

        private void OnInterstitialHiddenEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            // Interstitial ad is hidden. Pre-load the next ad.
            LoadInterstitial();

            _log.Debug("Interstitial ad hidden for ad unit id : " + adUnitId);
        }

    }
}
#endif