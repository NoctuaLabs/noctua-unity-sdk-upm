#if UNITY_APPLOVIN
using Cysharp.Threading.Tasks;
using System;

namespace com.noctuagames.sdk.AppLovin
{
    public class RewardedAppLovin
    {
        private readonly NoctuaLogger _log = new(typeof(RewardedAppLovin));

        private string _adUnitIDRewarded;

        int retryAttempt;

        public void SetRewardedAdUnitID(string adUnitID)
        {
            if (adUnitID == null)
            {
                _log.Error("Ad unit ID rewarded is empty.");
                return;
            }

            _adUnitIDRewarded = adUnitID;

            _log.Debug("Ad unit ID rewarded set to : " + _adUnitIDRewarded);
        }

        public void LoadRewardedAds()
        {
            if(_adUnitIDRewarded == null)
            {
                _log.Error("Ad unit ID rewarded is empty.");
                return;
            }

            // Attach callback
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += OnRewardedAdLoadedEvent;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += OnRewardedAdLoadFailedEvent;
            MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += OnRewardedAdDisplayedEvent;
            MaxSdkCallbacks.Rewarded.OnAdClickedEvent += OnRewardedAdClickedEvent;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += OnRewardedAdRevenuePaidEvent;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += OnRewardedAdHiddenEvent;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += OnRewardedAdFailedToDisplayEvent;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnRewardedAdReceivedRewardEvent;

            // Load the first rewarded ad
            LoadRewardedAd();

            _log.Debug("Rewarded ad loaded for ad unit id : " + _adUnitIDRewarded);
        }

        private void LoadRewardedAd()
        {
            MaxSdk.LoadRewardedAd(_adUnitIDRewarded);

            _log.Debug("Loading rewarded ad for ad unit id : " + _adUnitIDRewarded);
        }
        public void ShowRewardedAd()
        {
            if (MaxSdk.IsRewardedAdReady(_adUnitIDRewarded))
            {
                MaxSdk.ShowRewardedAd(_adUnitIDRewarded);

                _log.Debug("Showing rewarded ad for ad unit id : " + _adUnitIDRewarded);
            }
        }
        

        private void OnRewardedAdLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            // Rewarded ad is ready for you to show. MaxSdk.IsRewardedAdReady(adUnitId) now returns 'true'.

            // Reset retry attempt
            retryAttempt = 0;

            _log.Debug("Rewarded ad loaded for ad unit id : " + adUnitId);
        }

        private void OnRewardedAdLoadFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            // Rewarded ad failed to load
            // AppLovin recommends that you retry with exponentially higher delays, up to a maximum delay (in this case 64 seconds).

            retryAttempt++;
            double retryDelay = Math.Pow(2, Math.Min(6, retryAttempt));

            // Invoke("LoadRewardedAd", (float) retryDelay);
            RetryLoadRewardedAsync().Forget();

            _log.Debug("Rewarded ad failed to load for ad unit id : " + adUnitId + " with error code : " + errorInfo.Code);

        }

        private async UniTaskVoid RetryLoadRewardedAsync()
        {
            retryAttempt++;
            double retryDelay = Math.Pow(2, Math.Min(6, retryAttempt));

            await UniTask.Delay((int)(retryDelay * 1000));
            LoadRewardedAd();

            _log.Debug("Retrying to load interstitial ad after " + retryDelay + " seconds");
        }


        private void OnRewardedAdDisplayedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) {}

        private void OnRewardedAdFailedToDisplayEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
        {
            // Rewarded ad failed to display. AppLovin recommends that you load the next ad.
            LoadRewardedAd();

            _log.Debug("Rewarded ad failed to display for ad unit id : " + adUnitId + " with error code : " + errorInfo.Code);
        }

        private void OnRewardedAdClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) {
            _log.Debug("Rewarded ad clicked for ad unit id : " + adUnitId);
        }

        private void OnRewardedAdHiddenEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            // Rewarded ad is hidden. Pre-load the next ad
            LoadRewardedAd();

            _log.Debug("Rewarded ad hidden for ad unit id : " + adUnitId);
        }

        private void OnRewardedAdReceivedRewardEvent(string adUnitId, MaxSdk.Reward reward, MaxSdkBase.AdInfo adInfo)
        {
            // The rewarded ad displayed and the user should receive the reward.
            _log.Info("Rewarded user: " + reward.Amount + " " + reward.Label);

            _log.Debug("Rewarded ad received reward for ad unit id : " + adUnitId);
        }

        private void OnRewardedAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            // Ad revenue paid. Use this callback to track user revenue.
            _log.Debug("Rewarded ad revenue paid for ad unit id : " + adUnitId);
        }
    }
}
#endif