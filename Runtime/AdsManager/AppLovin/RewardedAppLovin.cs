#if UNITY_APPLOVIN
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace com.noctuagames.sdk.AppLovin
{
    public class RewardedAppLovin
    {
        private readonly NoctuaLogger _log = new(typeof(RewardedAppLovin));

        private string _adUnitIDRewarded;

        int retryAttempt;

        public event Action RewardedOnAdDisplayed;
        public event Action RewardedOnAdFailedDisplayed;
        public event Action RewardedOnAdClicked;
        public event Action<MaxSdk.Reward> RewardedOnUserEarnedReward;
        public event Action RewardedOnAdClosed;
        public event Action<MaxSdkBase.AdInfo> RewardedOnAdRevenuePaid;

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
            
            // Track ad loaded event
            TrackAdCustomEventRewarded("ad_loaded", adUnitId, adInfo);
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

            // Track ad load failed event
            var extraPayload = new Dictionary<string, IConvertible>
            {
                { "error_code", errorInfo.Code },
                { "error_message", errorInfo.Message },
                { "mediator_error_code", errorInfo.MediatedNetworkErrorCode },
                { "mediator_error_message", errorInfo.MediatedNetworkErrorMessage }
            };
            TrackAdCustomEventRewarded("ad_load_failed", adUnitId, null, extraPayload);
        }

        private async UniTaskVoid RetryLoadRewardedAsync()
        {
            retryAttempt++;
            double retryDelay = Math.Pow(2, Math.Min(6, retryAttempt));

            await UniTask.Delay((int)(retryDelay * 1000));
            LoadRewardedAd();

            _log.Debug("Retrying to load interstitial ad after " + retryDelay + " seconds");
        }


        private void OnRewardedAdDisplayedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) {
            // Rewarded ad displayed

            _log.Debug("Rewarded ad displayed for ad unit id : " + adUnitId);

            // Track ad shown event
            TrackAdCustomEventRewarded("ad_shown", adUnitId, adInfo);

            RewardedOnAdDisplayed?.Invoke();
        }

        private void OnRewardedAdFailedToDisplayEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
        {
            // Rewarded ad failed to display. AppLovin recommends that you load the next ad.
            LoadRewardedAd();

            _log.Debug("Rewarded ad failed to display for ad unit id : " + adUnitId + " with error code : " + errorInfo.Code);

            // Track ad show failed event
            var extraPayload = new Dictionary<string, IConvertible>
            {
                { "error_code", errorInfo.Code },
                { "error_message", errorInfo.Message },
                { "mediator_error_code", errorInfo.MediatedNetworkErrorCode },
                { "mediator_error_message", errorInfo.MediatedNetworkErrorMessage }
            };
            TrackAdCustomEventRewarded("ad_shown_failed", adUnitId, adInfo, extraPayload);

            RewardedOnAdFailedDisplayed?.Invoke();
        }

        private void OnRewardedAdClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) {
            _log.Debug("Rewarded ad clicked for ad unit id : " + adUnitId);

            // Track ad clicked event
            TrackAdCustomEventRewarded("ad_clicked", adUnitId, adInfo);

            RewardedOnAdClicked?.Invoke();
        }

        private void OnRewardedAdHiddenEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            // Rewarded ad is hidden. Pre-load the next ad
            LoadRewardedAd();

            _log.Debug("Rewarded ad hidden for ad unit id : " + adUnitId);

            // Track ad closed event
            TrackAdCustomEventRewarded("ad_closed", adUnitId, adInfo);

            RewardedOnAdClosed?.Invoke();
        }

        private void OnRewardedAdReceivedRewardEvent(string adUnitId, MaxSdk.Reward reward, MaxSdkBase.AdInfo adInfo)
        {
            // The rewarded ad displayed and the user should receive the reward.
            _log.Info("Rewarded user: " + reward.Amount + " " + reward.Label);

            _log.Debug("Rewarded ad received reward for ad unit id : " + adUnitId);

            // Track reward earned event
            var extraPayload = new Dictionary<string, IConvertible>
            {
                { "reward_amount", reward.Amount },
                { "reward_type", reward.Label }
            };
            TrackAdCustomEventRewarded("reward_earned", adUnitId, adInfo, extraPayload);

            RewardedOnUserEarnedReward?.Invoke(reward);
        }

        private void OnRewardedAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            // Ad revenue paid. Use this callback to track user revenue.
            _log.Debug("Rewarded ad revenue paid for ad unit id : " + adUnitId);
            
            RewardedOnAdRevenuePaid?.Invoke(adInfo);
        }
        
        private void TrackAdCustomEventRewarded(string eventName, string adUnitId, MaxSdkBase.AdInfo adInfo, Dictionary<string, IConvertible> extraPayload = null)
        {
            try
            {
                _log.Debug("Tracking custom event for rewarded ad: " + eventName);

                extraPayload ??= new Dictionary<string, IConvertible>();

                // Add basic information that doesn't require the ad info
                extraPayload.Add("ad_format", "rewarded");
                extraPayload.Add("mediation_service", "applovin");
                extraPayload.Add("ad_unit_id", adUnitId ?? _adUnitIDRewarded ?? "unknown");
                
                // Add ad info if available
                if (adInfo != null)
                {
                    extraPayload.Add("ad_network", adInfo.NetworkName ?? "unknown");
                    extraPayload.Add("placement", adInfo.Placement ?? "unknown");
                    extraPayload.Add("network_placement", adInfo.NetworkPlacement ?? "unknown");
                }
                else
                {
                    extraPayload.Add("ad_network", "unknown");
                }

                string properties = "";
                foreach (var (key, value) in extraPayload)
                {
                    properties += $"{key}={value}, ";
                }

                _log.Debug($"Event name: {eventName}, Event properties: {properties}");
            
                Noctua.Event.TrackCustomEvent(eventName, extraPayload);
            }
            catch (Exception ex)
            {
                _log.Error($"Error tracking rewarded ad event '{eventName}': {ex.Message}\n{ex.StackTrace}");
                // Continue execution - tracking errors shouldn't affect ad functionality
            }
        }
    }
}
#endif