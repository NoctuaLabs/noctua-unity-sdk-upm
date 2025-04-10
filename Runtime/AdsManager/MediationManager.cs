using System;
using UnityEngine;
using System.Collections.Generic;

#if UNITY_ADMOB
using GoogleMobileAds.Api;
using static GoogleMobileAds.Api.AdValue;
#endif

namespace com.noctuagames.sdk
{
    public class MediationManager
    {
        private readonly NoctuaLogger _log = new(typeof(MediationManager));
        private IAdNetwork _adNetwork;
        private string _mediationType;

        // Private event handlers
        private event Action _onAdDisplayed;
        private event Action _onAdFailedDisplayed;
        private event Action _onAdClicked;
        private event Action _onAdImpressionRecorded;
        private event Action _onAdClosed;

        #if UNITY_ADMOB
        private event Action<Reward> _admobOnUserEarnedReward;
        private event Action<AdValue, ResponseInfo> _admobOnAdRevenuePaid;
        #endif

        #if UNITY_APPLOVIN
        private event Action<MaxSdk.Reward> _appLovinOnUserEarnedReward;
        private event Action<MaxSdkBase.AdInfo> _appLovinOnAdRevenuePaid;
        #endif

        // public event handlers
        public event Action OnAdDisplayed { add => _onAdDisplayed += value; remove => _onAdDisplayed -= value; }
        public event Action OnAdFailedDisplayed { add => _onAdFailedDisplayed += value; remove => _onAdFailedDisplayed -= value; } 
        public event Action OnAdClicked { add => _onAdClicked += value; remove => _onAdClicked -= value; }
        public event Action OnAdImpressionRecorded { add => _onAdImpressionRecorded += value; remove => _onAdImpressionRecorded -= value; }
        public event Action OnAdClosed { add => _onAdClosed += value; remove => _onAdClosed -= value; }

        #if UNITY_ADMOB
        public event Action<Reward> AdmobOnUserEarnedReward { add => _admobOnUserEarnedReward += value; remove => _admobOnUserEarnedReward -= value; }
        public event Action<AdValue, ResponseInfo> AdmobOnAdRevenuePaid { add => _admobOnAdRevenuePaid += value; remove => _admobOnAdRevenuePaid -= value; }
        #endif
        #if UNITY_APPLOVIN
        public event Action<MaxSdk.Reward> AppLovinOnUserEarnedReward { add => _appLovinOnUserEarnedReward += value; remove => _appLovinOnUserEarnedReward -= value; }
        public event Action<MaxSdkBase.AdInfo> AppLovinOnAdRevenuePaid { add => _appLovinOnAdRevenuePaid += value; remove => _appLovinOnAdRevenuePaid -= value; }
        #endif

        public void Initialize(IAA iAAResponse, Action initCompleteAction)
        {
            _log.Info("Initializing Ad Network");

            _mediationType = iAAResponse.Mediation;

            if (string.IsNullOrEmpty(_mediationType))
            {
                _log.Error("Mediation type is empty or null.");
                return;
            }

            switch (iAAResponse.Mediation)
            {
                case "admob":
                #if UNITY_ADMOB
                    InitializeAdmob();
                #endif
                    break;

                case "applovin":
                #if UNITY_APPLOVIN
                    InitializeAppLovin();
                #endif
                    break;

                default:
                    _log.Info("No mediation found: " + iAAResponse.Mediation);
                    break;
            }

            _adNetwork.Initialize(() => {

                initCompleteAction?.Invoke();

                var interstitialAdUnitID = "unused";
                var rewardedAdUnitID = "unused";
                var rewardedInterstitialAdUnitID = "unused";
                var bannerAdUnitID = "unused";

                #if UNITY_ANDROID
                    interstitialAdUnitID = iAAResponse.AdFormat.Interstitial.Android.adUnitID;
                #elif UNITY_IPHONE
                    interstitialAdUnitID = iAAResponse.AdFormat.Interstitial.IOS.adUnitID;
                #endif

                 #if UNITY_ANDROID
                    rewardedAdUnitID = iAAResponse.AdFormat.Rewarded.Android.adUnitID;
                #elif UNITY_IPHONE
                    rewardedAdUnitID = iAAResponse.AdFormat.Rewarded.IOS.adUnitID;
                #endif

                #if UNITY_ANDROID
                    rewardedInterstitialAdUnitID = iAAResponse.AdFormat.Rewarded.Android.adUnitID;
                #elif UNITY_IPHONE
                    rewardedInterstitialAdUnitID = iAAResponse.AdFormat.Rewarded.IOS.adUnitID;
                #endif

                 #if UNITY_ANDROID
                    bannerAdUnitID = iAAResponse.AdFormat.Banner.Android.adUnitID;
                #elif UNITY_IPHONE
                    bannerAdUnitID = iAAResponse.AdFormat.Banner.IOS.adUnitID;
                #endif

                //Setup Ad Unit ID
                SetInterstitialAdUnitId(interstitialAdUnitID);
                SetRewardedAdUnitId(rewardedAdUnitID);
                
                #if UNITY_ADMOB
                SetRewardedInterstitialAdUnitId(rewardedInterstitialAdUnitID);
                #endif

                SetBannerAdUnitId(bannerAdUnitID);

                _log.Debug("Setup Ad Unit ID is Done");

                //Prepare the ads
                LoadInterstitialAd();
                LoadRewardedAd();

            });
        }

        #if UNITY_ADMOB
        private void InitializeAdmob()
        {
            _adNetwork = new AdmobManager();

            _adNetwork.OnAdDisplayed += () => { _onAdDisplayed?.Invoke(); };
            _adNetwork.OnAdFailedDisplayed += () => { _onAdFailedDisplayed?.Invoke(); };
            _adNetwork.OnAdClicked += () => { _onAdClicked?.Invoke(); };
            _adNetwork.OnAdImpressionRecorded += () => { _onAdImpressionRecorded?.Invoke(); };
            _adNetwork.OnAdClosed += () => { _onAdClosed?.Invoke(); };
            _adNetwork.AdmobOnUserEarnedReward += (reward) => { _admobOnUserEarnedReward?.Invoke(reward); };
            _adNetwork.AdmobOnAdRevenuePaid += (adValue, responseInfo) => {

                // Send the impression-level ad revenue information
                long valueMicros = adValue.Value;
                string currencyCode = adValue.CurrencyCode;
                PrecisionType precision = adValue.Precision;

                string responseId = responseInfo.GetResponseId();

                AdapterResponseInfo loadedAdapterResponseInfo = responseInfo.GetLoadedAdapterResponseInfo();
                string adSourceId = loadedAdapterResponseInfo.AdSourceId;
                string adSourceInstanceId = loadedAdapterResponseInfo.AdSourceInstanceId;
                string adSourceInstanceName = loadedAdapterResponseInfo.AdSourceInstanceName;
                string adSourceName = loadedAdapterResponseInfo.AdSourceName;
                string adapterClassName = loadedAdapterResponseInfo.AdapterClassName;
                long latencyMillis = loadedAdapterResponseInfo.LatencyMillis;
                Dictionary<string, string> credentials = loadedAdapterResponseInfo.AdUnitMapping;

                Dictionary<string, string> extras = responseInfo.GetResponseExtras();
                string mediationGroupName = extras["mediation_group_name"];
                string mediationABTestName = extras["mediation_ab_test_name"];
                string mediationABTestVariant = extras["mediation_ab_test_variant"];

                double revenue = valueMicros / 1_000_000.0; // convert micros to currency unit

                _log.Debug($"Admob Ad Revenue Paid: value in micros: {adValue.Value} / converted micros: {revenue}, {adValue.CurrencyCode} " +
                    $"Precision: {adValue.Precision} " +
                    $"Response ID: {responseId} " +
                    $"Ad Source ID: {adSourceId} " +
                    $"Ad Source Instance ID: {adSourceInstanceId} " +
                    $"Ad Source Instance Name: {adSourceInstanceName} " +
                    $"Ad Source Name: {adSourceName} " +
                    $"Adapter Class Name: {adapterClassName} " +
                    $"Latency Millis: {latencyMillis}");

                Noctua.Event.TrackAdRevenue("admob_sdk", revenue, currencyCode, new Dictionary<string, IConvertible>
                {
                    { "ad_source_id", adSourceId },
                    { "ad_source_instance_id", adSourceInstanceId },
                    { "ad_source_instance_name", adSourceInstanceName },
                    { "ad_source_name", adSourceName },
                    { "adapter_class_name", adapterClassName },
                    { "latency_millis", latencyMillis },
                    { "response_id", responseId },
                    { "mediation_group_name", mediationGroupName },
                    { "mediation_ab_test_name", mediationABTestName },
                    { "mediation_ab_test_variant", mediationABTestVariant }
                });

                _admobOnAdRevenuePaid?.Invoke(adValue, responseInfo);
            };
        }
        #endif

        #if UNITY_APPLOVIN
        private void InitializeAppLovin()
        {
            _adNetwork = new AppLovinManager();

            _adNetwork.OnAdDisplayed += () => { _onAdDisplayed?.Invoke(); };
            _adNetwork.OnAdFailedDisplayed += () => { _onAdFailedDisplayed?.Invoke(); };
            _adNetwork.OnAdClicked += () => { _onAdClicked?.Invoke(); };
            _adNetwork.OnAdImpressionRecorded += () => { _onAdImpressionRecorded?.Invoke(); };
            _adNetwork.OnAdClosed += () => { _onAdClosed?.Invoke(); };
            _adNetwork.AppLovinOnUserEarnedReward += (Reward) => { _appLovinOnUserEarnedReward?.Invoke(Reward); };
            _adNetwork.AppLovinOnAdRevenuePaid += (adInfo) => { 

                double revenue = adInfo.Revenue;

                // Miscellaneous data
                string countryCode = MaxSdk.GetSdkConfiguration().CountryCode; // "US" for the United States, etc - Note: Do not confuse this with currency code which is "USD"
                string networkName = adInfo.NetworkName; // Display name of the network that showed the ad
                string adUnitIdentifier = adInfo.AdUnitIdentifier; // The MAX Ad Unit ID
                string placement = adInfo.Placement; // The placement this ad's postbacks are tied to
                string networkPlacement = adInfo.NetworkPlacement; // The placement ID from the network that showed the ad
                string revenuePrecision = adInfo.RevenuePrecision;

                _log.Debug($"AppLovin Ad Revenue Paid: revenue: {adInfo.Revenue}, " + 
                    "currency: USD, " + 
                    $"country code: {countryCode}, " + 
                    $"network name: {networkName}, " + 
                    $"ad unit identifier: {adUnitIdentifier}, " +
                    $"placement: {placement}, " + 
                    $"network placement: {networkPlacement}, " +
                    $"revenue precision: {revenuePrecision}");

                Noctua.Event.TrackAdRevenue("applovin_max_sdk", revenue, "USD", new Dictionary<string, IConvertible>
                {
                    { "country_code", countryCode },
                    { "network_name", networkName },
                    { "ad_unit_identifier", adUnitIdentifier },
                    { "placement", placement },
                    { "network_placement", networkPlacement },
                    { "revenue_precision", revenuePrecision }
                });
            
                _appLovinOnAdRevenuePaid?.Invoke(adInfo); 
            };
        }
        #endif

        //Interstitial public functions
        private void SetInterstitialAdUnitId(string adUnitID) => _adNetwork.SetInterstitialAdUnitID(adUnitID);
        public void LoadInterstitialAd() => _adNetwork.LoadInterstitialAd();
        public void ShowInterstitial() => _adNetwork.ShowInterstitial();

        //Rewarded public functions
        private void SetRewardedAdUnitId(string adUnitID) => _adNetwork.SetRewardedAdUnitID(adUnitID);
        public void LoadRewardedAd() => _adNetwork.LoadRewardedAd();
        public void ShowRewardedAd() => _adNetwork.ShowRewardedAd();

        //Rewarded Interstitial public functions for Admob
        #if UNITY_ADMOB
        private void SetRewardedInterstitialAdUnitId(string adUnitID) {
            
            if (!IsAdmob()) { return; }

            _adNetwork.SetRewardeInterstitialdAdUnitID(adUnitID);
        }
        public void LoadRewardedInterstitialAd() => _adNetwork.LoadRewardedInterstitialAd();
        public void ShowRewardedInterstitialAd() => _adNetwork.ShowRewardedInterstitialAd();
        #endif

        //Banner public functions
        private void SetBannerAdUnitId(string adUnitID) => _adNetwork.SetBannerAdUnitId(adUnitID);
        public void ShowBannerAd() => _adNetwork.ShowBannerAd();

        #if UNITY_ADMOB
        public void CreateBannerViewAdAdmob(AdSize adSize, AdPosition adPosition) 
        {
           if(!IsAdmob()) { return; }

            _adNetwork.CreateBannerViewAdAdmob(adSize, adPosition);
        }
        #endif

        //Banner public function for AppLovin
        #if UNITY_APPLOVIN
        public void CreateBannerViewAdAppLovin(Color color, MaxSdkBase.BannerPosition bannerPosition) 
        {
            if(!IsAppLovin()) { return; }

            _adNetwork.CreateBannerViewAdAppLovin(color, bannerPosition);
        }
        public void HideAppLovinBanner() 
        {
            if(!IsAppLovin()) { return; }

            _adNetwork.HideBannerAppLovin();
        } 
        public void DestroyBannerAppLovin() 
        {
            if(!IsAppLovin()) { return; }

            _adNetwork.DestroyBannerAppLovin();
        }
        public void SetBannerWidth(int width)
        {
            if(!IsAppLovin()) { return; }

            _adNetwork.SetBannerWidth(width);
        }
        public Rect GetBannerPosition() 
        {
            if(!IsAppLovin()) { return new Rect(); }

            return _adNetwork.GetBannerPosition();
        }
        public void StopBannerAutoRefresh()
        {
            if(!IsAppLovin()) { return; }

            _adNetwork.StopBannerAutoRefresh();
        }
        public void StartBannerAutoRefresh()
        {
            if(!IsAppLovin()) { return; }

            _adNetwork.StartBannerAutoRefresh();
        }
        #endif

        public void ShowCreativeDebugger()
        {
            _adNetwork.ShowCreativeDebugger();
        }

        public void ShowMediationDebugger()
        {
            _adNetwork.ShowMediationDebugger();
        }

        private bool IsAppLovin() 
        {
            if (_mediationType == "applovin")
            {
                return true;
            }
            else
            {
                _log.Info("Mediation type is not AppLovin. Cannot perform AppLovin specific actions. " + "current mediation is : " + _mediationType);
                return false;
            }
        }

        private bool IsAdmob() 
        {
            if (_mediationType == "admob")
            {
                return true;
            }
            else
            {
                _log.Info("Mediation type is not Admob. Cannot perform Admob specific actions." + "current mediation is : " + _mediationType);
                return false;
            }
        }
    }
}
