using System;
using UnityEngine;
using System.Collections.Generic;
using com.noctuagames.sdk.UI;
using com.noctuagames.sdk.AdPlaceholder;

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
        private event Action _onInitialized;
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
        public event Action OnInitialized { add => _onInitialized += value; remove => _onInitialized -= value; }
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

#if UNITY_ADMOB
        public static AdmobAdPreloadManager _preloadManager = new();
        private event Action<PreloadConfiguration> _onAdsAvailable;
        private event Action<PreloadConfiguration> _onAdExhausted;

        public event Action<PreloadConfiguration> OnAdsAvailable { add => _onAdsAvailable += value; remove => _onAdsAvailable -= value; }
        public event Action<PreloadConfiguration> OnAdExhausted { add => _onAdExhausted += value; remove => _onAdExhausted -= value; }
#endif

        private string _interstitialAdUnitID = "unused";
        private string _rewardedAdUnitID = "unused";
        private string _rewardedInterstitialAdUnitID = "unused";
        private string _bannerAdUnitID = "unused";

        private readonly UIFactory _uiFactory;
        private bool _hasClosedPlaceholder = false; // Track if the placeholder has been closed to prevent multiple closures
        public IAA _iAAResponse;

        internal MediationManager(UIFactory uiFactory, IAA iAAResponse)
        {
            _uiFactory = uiFactory;

            if (_iAAResponse != null)
            {
                _log.Info("IAA response already set in MediationManager");
                return;
            }
            _iAAResponse = iAAResponse;
        }

        public void Initialize(Action initCompleteAction = null)
        {
            _log.Info("Initializing Ad Mediation");

            _mediationType = _iAAResponse.Mediation;

            if (string.IsNullOrEmpty(_mediationType))
            {
                _log.Error("Mediation type is empty or null.");
                return;
            }

            switch (_iAAResponse.Mediation)
            {
                case "admob":
                    InitializeAdmob();
                    break;

                case "applovin":
                    InitializeAppLovin();
                    break;

                default:
                    _log.Info("No mediation found: " + _iAAResponse.Mediation);
                    break;
            }

            _adNetwork.Initialize(() =>
            {

                initCompleteAction?.Invoke();

                _log.Info("Ad Mediation Initialized: " + _iAAResponse.Mediation);

                if (_iAAResponse.AdFormat == null)
                {
                    _log.Info("Ad Format is null in IAA response. Cannot proceed with ad unit ID setup.");
                    return;
                }

                if (IsAdmob())
                {
#if UNITY_ADMOB
                    _preloadManager = AdmobAdPreloadManager.Instance;
#endif
                }
                SetupAdUnitID(_iAAResponse);
            });
        }

        private void InitializeAdmob()
        {
#if UNITY_ADMOB
            _adNetwork = new AdmobManager();

            _adNetwork.OnAdDisplayed += () =>
            {
                CloseAdPlaceholder();

                _onAdDisplayed?.Invoke();
            };
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

                string adSourceId = loadedAdapterResponseInfo?.AdSourceId ?? "empty";
                string adSourceInstanceId = loadedAdapterResponseInfo?.AdSourceInstanceId ?? "empty";
                string adSourceInstanceName = loadedAdapterResponseInfo?.AdSourceInstanceName ?? "empty";
                string adSourceName = loadedAdapterResponseInfo?.AdSourceName ?? "empty";
                string adapterClassName = loadedAdapterResponseInfo?.AdapterClassName ?? "empty";
                long latencyMillis = loadedAdapterResponseInfo?.LatencyMillis ?? 0;
                Dictionary<string, string> credentials = loadedAdapterResponseInfo?.AdUnitMapping;

                Dictionary<string, string> extras = responseInfo.GetResponseExtras();
                string mediationGroupName = extras != null && extras.ContainsKey("mediation_group_name") ? extras["mediation_group_name"] : "empty";
                string mediationABTestName = extras != null && extras.ContainsKey("mediation_ab_test_name") ? extras["mediation_ab_test_name"] : "empty";
                string mediationABTestVariant = extras != null && extras.ContainsKey("mediation_ab_test_variant") ? extras["mediation_ab_test_variant"] : "empty";

                double revenue = valueMicros / 1_000_000.0;

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
#endif
        }

        private void InitializeAppLovin()
        {
#if UNITY_APPLOVIN
            _adNetwork = new AppLovinManager();

            _adNetwork.OnAdDisplayed += () => { 

                CloseAdPlaceholder();

                _onAdDisplayed?.Invoke(); 
            };
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
#endif
        }

        public void SetupAdUnitID(IAA iAAResponse)
        {
#if UNITY_ANDROID
            _interstitialAdUnitID = iAAResponse.AdFormat.Interstitial.Android.adUnitID;
#elif UNITY_IPHONE
            _interstitialAdUnitID = iAAResponse.AdFormat.Interstitial.IOS.adUnitID;
#endif

#if UNITY_ANDROID
            _rewardedAdUnitID = iAAResponse.AdFormat.Rewarded.Android.adUnitID;
#elif UNITY_IPHONE
            _rewardedAdUnitID = iAAResponse.AdFormat.Rewarded.IOS.adUnitID;
#endif

#if UNITY_ANDROID
            _rewardedInterstitialAdUnitID = iAAResponse.AdFormat.RewardedInterstitial.Android.adUnitID;
#elif UNITY_IPHONE
            _rewardedInterstitialAdUnitID = iAAResponse.AdFormat.RewardedInterstitial.IOS.adUnitID;
#endif

#if UNITY_ANDROID
            _bannerAdUnitID = iAAResponse.AdFormat.Banner.Android.adUnitID;
#elif UNITY_IPHONE
            _bannerAdUnitID = iAAResponse.AdFormat.Banner.IOS.adUnitID;
#endif

            SetBannerAdUnitId(_bannerAdUnitID);

            if (IsAdmob())
            {
#if UNITY_ADMOB
                SetRewardedInterstitialAdUnitId(_rewardedInterstitialAdUnitID);

                var configs = new List<PreloadConfiguration>
                {
                    _preloadManager.CreateInterstitialPreloadConfig(_interstitialAdUnitID),
                    _preloadManager.CreateRewardedPreloadConfig(_rewardedAdUnitID)
                };

                // Subscribe to events
                _preloadManager.OnAdsAvailable += (config) =>
                {
                    _log.Debug($"Ad available for {config.Format}");

                    _onAdsAvailable?.Invoke(config);
                };

                _preloadManager.OnAdExhausted += (config) =>
                {
                    _log.Debug($"Ad exhausted for {config.Format}");

                    _onAdExhausted?.Invoke(config);
                };

                _preloadManager.StartPreloading(configs);
#endif
            }
            else
            {
                SetInterstitialAdUnitId(_interstitialAdUnitID);
                SetRewardedAdUnitId(_rewardedAdUnitID);

                //Prepare the ads
                LoadInterstitialAd();
                LoadRewardedAd();
            }

            _onInitialized?.Invoke();
            _log.Info("Ad Unit IDs set up for mediation type: " + _mediationType);
        }

        //Interstitial public functions
        private void SetInterstitialAdUnitId(string adUnitID) => _adNetwork.SetInterstitialAdUnitID(adUnitID);
        public void LoadInterstitialAd() => _adNetwork.LoadInterstitialAd();
        public void ShowInterstitial() {

            _hasClosedPlaceholder = false;

            ShowAdPlaceholder(AdPlaceholderType.Interstitial);

            if (IsAdmob())
            {
#if UNITY_ADMOB
                // Check if the ad is available before showing
                if (_preloadManager.IsAdAvailable(_interstitialAdUnitID, AdFormat.INTERSTITIAL))
                {
                    var ad = _preloadManager.PollInterstitialAd(_interstitialAdUnitID);
                    if (ad != null)
                    {
                        _log.Info("Showing Admob Interstitial Ad");
                        ad.Show();

                        RegisterCallbackAdInterstitial(ad);

                    }
                }
                else
                {
                    _log.Info("Admob Interstitial Ad not available");
                }
#endif
            }
            else
            {
                // For other networks, just show the ad
                _adNetwork.ShowInterstitial();
            }
        }

        #if UNITY_ADMOB
        private void RegisterCallbackAdInterstitial(InterstitialAd interstitialAd)
        {
            interstitialAd.OnAdFullScreenContentOpened += () =>
            {
                CloseAdPlaceholder();

                _log.Info("Noctua Placeholder closed for Interstitial Ad");
            };
        }
        #endif

        //Rewarded public functions
        private void SetRewardedAdUnitId(string adUnitID) => _adNetwork.SetRewardedAdUnitID(adUnitID);
        public void LoadRewardedAd() => _adNetwork.LoadRewardedAd();
        public void ShowRewardedAd()
        {
            _hasClosedPlaceholder = false;

            ShowAdPlaceholder(AdPlaceholderType.Rewarded);

            if (IsAdmob())
            {

#if UNITY_ADMOB
                // Check if the ad is available before showing
                if (_preloadManager.IsAdAvailable(_rewardedAdUnitID, AdFormat.REWARDED))
                {
                    var ad = _preloadManager.PollRewardedAd(_rewardedAdUnitID);
                    if (ad != null)
                    {
                        _log.Info("Showing Admob Rewarded Ad");

                        ad.Show((Reward reward) =>
                        {
                            _log.Info("User earned reward from mediation manager : " + reward.Type + " - " + reward.Amount);
                        });

                        RegisterCallbackAdInterstitial(ad);
                    }
                }
                else
                {
                    _log.Info("Admob Rewarded Ad not available");
                }
#endif
            }
            else
            {
                // For other networks, just show the ad
                _adNetwork.ShowRewardedAd();
            }
        }

        #if UNITY_ADMOB
        private void RegisterCallbackAdInterstitial(RewardedAd rewardedAd)
        {
            rewardedAd.OnAdFullScreenContentOpened += () =>
            {
                CloseAdPlaceholder();

                _log.Info("Noctua Placeholder closed for Rewarded Ad");
            };
        }
        #endif

        //Rewarded Interstitial public functions for Admob
        #if UNITY_ADMOB
        private void SetRewardedInterstitialAdUnitId(string adUnitID) {

            if (!IsAdmob()) { return; }

            _adNetwork.SetRewardeInterstitialdAdUnitID(adUnitID);
        }
        public void LoadRewardedInterstitialAd() => _adNetwork.LoadRewardedInterstitialAd();
        public void ShowRewardedInterstitialAd()
        {
             _hasClosedPlaceholder = false;

            ShowAdPlaceholder(AdPlaceholderType.Rewarded);
            
            _adNetwork.ShowRewardedInterstitialAd();
        }
        #endif

            //Banner public functions
        private void SetBannerAdUnitId(string adUnitID) => _adNetwork.SetBannerAdUnitId(adUnitID);
        public void ShowBannerAd()
        {
            _hasClosedPlaceholder = false;

            ShowAdPlaceholder(AdPlaceholderType.Banner);

            _adNetwork.ShowBannerAd();
        } 

        #if UNITY_ADMOB
        public void CreateBannerViewAdAdmob(AdSize adSize, AdPosition adPosition)
        {
            if (!IsAdmob()) { return; }

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
        
        //Ad Placeholder public functions
        public void ShowAdPlaceholder(AdPlaceholderType adType)
        {
            _log.Info($"Showing ad placeholder for type: {adType}");

            _uiFactory.ShowAdPlaceholder(adType);
        }

        public void CloseAdPlaceholder()
        {
            if (_hasClosedPlaceholder)
            {
                _log.Info("Ad placeholder already closed. Skipping close action.");
                return;
            }

            _log.Info("Closing ad placeholder");

            _uiFactory.CloseAdPlaceholder();

            _hasClosedPlaceholder = true;
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
