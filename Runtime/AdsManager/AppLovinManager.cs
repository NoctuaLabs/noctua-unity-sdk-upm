#if UNITY_APPLOVIN
using System;
using com.noctuagames.sdk.AppLovin;


namespace com.noctuagames.sdk
{
    public class AppLovinManager: IAdNetwork {

        private readonly NoctuaLogger _log = new(typeof(AppLovinManager));

        private InterstitialAppLovin _interstitialAppLovin;

        // public event Action InterstitialOnAdImpressionRecorded;
        public event Action InterstitialOnAdClicked;
        public event Action InterstitialOnAdFullScreenContentOpened;
        public event Action InterstitialOnAdFullScreenContentClosed;
        public void Initialize(Action initCompleteAction) {
            _log.Info("Initializing AppLovin SDK");

            MaxSdkCallbacks.OnSdkInitializedEvent += (MaxSdkBase.SdkConfiguration sdkConfiguration) => {
                _log.Debug("AppLovin initialized");

                initCompleteAction?.Invoke();
            };

            MaxSdk.InitializeSdk();
        }

        public void LoadInterstitialAd(string adUnitID)
        {
            _interstitialAppLovin = new InterstitialAppLovin();

            _interstitialAppLovin.LoadInterstitial(adUnitID);
        }

        public void ShowInterstitial()
        {
            throw new NotImplementedException();
        }

        public void LoadRewardedAd(string adUnitID)
        {
            throw new NotImplementedException();
        }

        public void ShowRewardedAd()
        {
            throw new NotImplementedException();
        }

        public void SetBannerAdUnitId(string adUnitID)
        {
            throw new NotImplementedException();
        }

        public void LoadBannerAd()
        {
            throw new NotImplementedException();
        }

        public void OnDestroy()
        {
            throw new NotImplementedException();
        }
    }
}
#endif