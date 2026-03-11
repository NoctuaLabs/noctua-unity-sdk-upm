using System;
using UnityEngine;
using System.Collections.Generic;
using com.noctuagames.sdk.AdPlaceholder;

#if UNITY_ADMOB
using GoogleMobileAds.Api;
using static GoogleMobileAds.Api.AdValue;
#endif

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Manages ad mediation across multiple ad networks (AdMob, AppLovin), handling initialization,
    /// ad loading, display, and revenue tracking.
    /// </summary>
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

        /// <summary>Fires when the ad mediation SDK finishes initialization.</summary>
        public event Action OnInitialized { add => _onInitialized += value; remove => _onInitialized -= value; }
        /// <summary>Fires when an ad is successfully displayed to the user.</summary>
        public event Action OnAdDisplayed { add => _onAdDisplayed += value; remove => _onAdDisplayed -= value; }
        /// <summary>Fires when an ad fails to display.</summary>
        public event Action OnAdFailedDisplayed { add => _onAdFailedDisplayed += value; remove => _onAdFailedDisplayed -= value; }
        /// <summary>Fires when the user clicks on a displayed ad.</summary>
        public event Action OnAdClicked { add => _onAdClicked += value; remove => _onAdClicked -= value; }
        /// <summary>Fires when an ad impression is recorded.</summary>
        public event Action OnAdImpressionRecorded { add => _onAdImpressionRecorded += value; remove => _onAdImpressionRecorded -= value; }
        /// <summary>Fires when a displayed ad is closed by the user.</summary>
        public event Action OnAdClosed { add => _onAdClosed += value; remove => _onAdClosed -= value; }

#if UNITY_ADMOB
        /// <summary>Fires when the user earns a reward from an AdMob rewarded ad.</summary>
        public event Action<Reward> AdmobOnUserEarnedReward { add => _admobOnUserEarnedReward += value; remove => _admobOnUserEarnedReward -= value; }
        /// <summary>Fires when AdMob reports impression-level ad revenue data.</summary>
        public event Action<AdValue, ResponseInfo> AdmobOnAdRevenuePaid { add => _admobOnAdRevenuePaid += value; remove => _admobOnAdRevenuePaid -= value; }
#endif
#if UNITY_APPLOVIN
        /// <summary>Fires when the user earns a reward from an AppLovin rewarded ad.</summary>
        public event Action<MaxSdk.Reward> AppLovinOnUserEarnedReward { add => _appLovinOnUserEarnedReward += value; remove => _appLovinOnUserEarnedReward -= value; }
        /// <summary>Fires when AppLovin reports impression-level ad revenue data.</summary>
        public event Action<MaxSdkBase.AdInfo> AppLovinOnAdRevenuePaid { add => _appLovinOnAdRevenuePaid += value; remove => _appLovinOnAdRevenuePaid -= value; }
#endif

#if UNITY_ADMOB
        private static AdmobAdPreloadManager _preloadManager;
        private event Action<PreloadConfiguration> _onAdsAvailable;
        private event Action<PreloadConfiguration> _onAdExhausted;

        /// <summary>Fires when a preloaded ad becomes available for display.</summary>
        public event Action<PreloadConfiguration> OnAdsAvailable { add => _onAdsAvailable += value; remove => _onAdsAvailable -= value; }
        /// <summary>Fires when all preloaded ads of a given configuration are exhausted.</summary>
        public event Action<PreloadConfiguration> OnAdExhausted { add => _onAdExhausted += value; remove => _onAdExhausted -= value; }
#endif

        private string _interstitialAdUnitID = "unused";
        private string _rewardedAdUnitID = "unused";
        private string _rewardedInterstitialAdUnitID = "unused";
        private string _bannerAdUnitID = "unused";

        /// <summary>Gets the configured interstitial ad unit ID for the current platform.</summary>
        public string InterstitialAdUnitID => _interstitialAdUnitID;
        /// <summary>Gets the configured rewarded ad unit ID for the current platform.</summary>
        public string RewardedAdUnitID => _rewardedAdUnitID;
        /// <summary>Gets the configured rewarded interstitial ad unit ID for the current platform (AdMob only).</summary>
        public string RewardedInterstitialAdUnitID => _rewardedInterstitialAdUnitID;
        /// <summary>Gets the configured banner ad unit ID for the current platform.</summary>
        public string BannerAdUnitID => _bannerAdUnitID;

        private readonly IAdPlaceholderUI _adPlaceholderUI;
        private IAdRevenueTracker _adRevenueTracker;
        private bool _hasClosedPlaceholder = false; // Track if the placeholder has been closed to prevent multiple closures
        private bool _adNetworkEventsSubscribed; // Guard against duplicate event subscriptions on re-init
        private bool _preloadManagerEventsSubscribed; // Guard against duplicate preload event subscriptions

        internal IAA IAAResponse { get; set; }

        internal void SetAdRevenueTracker(IAdRevenueTracker tracker) => _adRevenueTracker = tracker;

        internal MediationManager(IAdPlaceholderUI adPlaceholderUI, IAA iAAResponse)
        {
            _adPlaceholderUI = adPlaceholderUI;

            if (iAAResponse == null)
            {
                _log.Warning("Constructor received null IAA response. MediationManager will not be usable.");
                return;
            }

            if (IAAResponse != null)
            {
                _log.Info("IAA response already set in MediationManager");
                return;
            }

            #if UNITY_ADMOB
                _adNetwork = new AdmobManager();
            #endif

            #if UNITY_APPLOVIN
                _adNetwork = new AppLovinManager();
                
                if(_adNetwork == null) 
                {
                    _log.Error("Failed to create AppLovinManager instance.");
                }
            #endif
            
            IAAResponse = iAAResponse;
        }

        /// <summary>
        /// Initializes the ad mediation SDK based on the configured mediation type (AdMob or AppLovin).
        /// </summary>
        /// <param name="initCompleteAction">Optional callback invoked when initialization completes.</param>
        public void Initialize(Action initCompleteAction = null)
        {
            if (IAAResponse == null)
            {
                _log.Error("Cannot initialize MediationManager: IAA response is null.");
                return;
            }

            _log.Info("Initializing Ad Mediation : " + IAAResponse.Mediation);

            var mediationType =
            #if UNITY_APPLOVIN
                "applovin";
            #elif UNITY_ADMOB
                "admob";
            #else
                "unknown";
            #endif

            _mediationType = !string.IsNullOrEmpty(IAAResponse.Mediation)
                ? IAAResponse.Mediation
                : mediationType;

            if (string.IsNullOrEmpty(_mediationType))
            {
                _log.Error("Mediation type is empty or null.");
                return;
            }

            switch (_mediationType)
            {
                case "admob":
                    InitializeAdmob(initCompleteAction);
                    break;

                case "applovin":
                    InitializeAppLovin(initCompleteAction);
                    break;

                default:
                    _log.Info("No mediation found: " + _mediationType);
                    break;
            }
        }

        private void InitializeAdmob(Action initCompleteAction = null)
        {
#if UNITY_ADMOB
            _adNetwork.Initialize(() =>
            {

                initCompleteAction?.Invoke();

                _log.Info("Ad Mediation Initialized: " + IAAResponse.Mediation);

                if (IAAResponse.AdFormat == null)
                {
                    _log.Info("Ad Format is null in IAA response. Cannot proceed with ad unit ID setup.");
                    return;
                }

                if (IsAdmob())
                {
                    _preloadManager = AdmobAdPreloadManager.Instance;
                }
                SetupAdUnitID(IAAResponse);
            });

            if(_adNetwork == null)
            {
                _log.Warning("Ad Network is not initialized. callback cannot be registered.");
                return;
            }

            if (!_adNetworkEventsSubscribed)
            {
                _adNetworkEventsSubscribed = true;

                _adNetwork.OnAdDisplayed += () =>
                {
                    CloseAdPlaceholder();

                    _onAdDisplayed?.Invoke();
                };
                _adNetwork.OnAdFailedDisplayed += () => {
                    CloseAdPlaceholder();

                    _onAdFailedDisplayed?.Invoke();
                };
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

                    _adRevenueTracker?.TrackAdRevenue("admob_sdk", revenue, currencyCode, new Dictionary<string, IConvertible>
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
                        { "mediation_ab_test_variant", mediationABTestVariant },
                        { "ad_user_id", SystemInfo.deviceUniqueIdentifier }
                    });

                    _admobOnAdRevenuePaid?.Invoke(adValue, responseInfo);
                };
            }
#endif
        }

        private void InitializeAppLovin(Action initCompleteAction = null)
        {
#if UNITY_APPLOVIN
            _adNetwork.Initialize(() =>
            {
                _log.Info("AppLovin SDK initialization callback invoked.");

            });

            if(_adNetwork == null)
            {
                _log.Warning("Ad Network is not initialized. callback cannot be registered.");
                return;
            }

            if (!_adNetworkEventsSubscribed)
            {
                _adNetworkEventsSubscribed = true;

                _adNetwork.OnInitialized += () => {
                    _log.Info("Ad Mediation Initialized: " + IAAResponse.Mediation);

                    if (IAAResponse.AdFormat == null)
                    {
                        _log.Info("Ad Format is null in IAA response. Cannot proceed with ad unit ID setup.");
                        return;
                    }

                    SetupAdUnitID(IAAResponse);

                    initCompleteAction?.Invoke();
                };

                _adNetwork.OnAdDisplayed += () => {

                    CloseAdPlaceholder();

                    _onAdDisplayed?.Invoke();
                };
                _adNetwork.OnAdFailedDisplayed += () => {
                    CloseAdPlaceholder();

                    _onAdFailedDisplayed?.Invoke();
                };
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
                    string adFormat = adInfo.AdFormat;

                    _log.Debug($"AppLovin Ad Revenue Paid: revenue: {adInfo.Revenue}, " +
                        "currency: USD, " +
                        $"country code: {countryCode}, " +
                        $"network name: {networkName}, " +
                        $"ad unit identifier: {adUnitIdentifier}, " +
                        $"placement: {placement}, " +
                        $"network placement: {networkPlacement}, " +
                        $"revenue precision: {revenuePrecision}");

                    _adRevenueTracker?.TrackAdRevenue("applovin_max_sdk", revenue, "USD", new Dictionary<string, IConvertible>
                    {
                        { "country_code", countryCode },
                        { "network_name", networkName },
                        { "ad_unit_identifier", adUnitIdentifier },
                        { "placement", placement },
                        { "network_placement", networkPlacement },
                        { "revenue_precision", revenuePrecision },
                        { "ad_format", adFormat },
                        { "ad_user_id", SystemInfo.deviceUniqueIdentifier }
                    });

                    _appLovinOnAdRevenuePaid?.Invoke(adInfo);
                };
            }
#endif
        }

        /// <summary>
        /// Configures ad unit IDs for all ad formats based on the IAA server response, then loads initial ads.
        /// </summary>
        /// <param name="iAAResponse">The IAA configuration response containing ad unit IDs per platform.</param>
        public void SetupAdUnitID(IAA iAAResponse)
        {
            
#if UNITY_ANDROID
    _interstitialAdUnitID = string.IsNullOrEmpty(iAAResponse?.AdFormat?.Interstitial?.Android?.adUnitID)
        ? "unknown"
        : iAAResponse.AdFormat.Interstitial.Android.adUnitID;
#elif UNITY_IPHONE
    _interstitialAdUnitID = string.IsNullOrEmpty(iAAResponse?.AdFormat?.Interstitial?.IOS?.adUnitID)
        ? "unknown"
        : iAAResponse.AdFormat.Interstitial.IOS.adUnitID;
#endif

#if UNITY_ANDROID
    _rewardedAdUnitID = string.IsNullOrEmpty(iAAResponse?.AdFormat?.Rewarded?.Android?.adUnitID)
        ? "unknown"
        : iAAResponse.AdFormat.Rewarded.Android.adUnitID;
#elif UNITY_IPHONE
    _rewardedAdUnitID = string.IsNullOrEmpty(iAAResponse?.AdFormat?.Rewarded?.IOS?.adUnitID)
        ? "unknown"
        : iAAResponse.AdFormat.Rewarded.IOS.adUnitID;
#endif

#if UNITY_ANDROID && UNITY_ADMOB
    _rewardedInterstitialAdUnitID = string.IsNullOrEmpty(iAAResponse?.AdFormat?.RewardedInterstitial?.Android?.adUnitID)
        ? "unknown"
        : iAAResponse.AdFormat.RewardedInterstitial.Android.adUnitID;
#elif UNITY_IPHONE && UNITY_ADMOB
    _rewardedInterstitialAdUnitID = string.IsNullOrEmpty(iAAResponse?.AdFormat?.RewardedInterstitial?.IOS?.adUnitID)
        ? "unknown"
        : iAAResponse.AdFormat.RewardedInterstitial.IOS.adUnitID;
#endif

#if UNITY_ANDROID
    _bannerAdUnitID = string.IsNullOrEmpty(iAAResponse?.AdFormat?.Banner?.Android?.adUnitID)
        ? "unknown"
        : iAAResponse.AdFormat.Banner.Android.adUnitID;
#elif UNITY_IPHONE
    _bannerAdUnitID = string.IsNullOrEmpty(iAAResponse?.AdFormat?.Banner?.IOS?.adUnitID)
        ? "unknown"
        : iAAResponse.AdFormat.Banner.IOS.adUnitID;
#endif

            SetBannerAdUnitId(_bannerAdUnitID);

            if (IsAdmob())
            {
#if UNITY_ADMOB
                SetRewardedInterstitialAdUnitId(_rewardedInterstitialAdUnitID);
                LoadRewardedInterstitialAd();

                var configs = new List<PreloadConfiguration>
                {
                    _preloadManager.CreateInterstitialPreloadConfig(_interstitialAdUnitID),
                    _preloadManager.CreateRewardedPreloadConfig(_rewardedAdUnitID)
                };

                // Subscribe to events (only once to prevent duplicate handlers)
                if (!_preloadManagerEventsSubscribed)
                {
                    _preloadManagerEventsSubscribed = true;

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
                }

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
        private void SetInterstitialAdUnitId(string adUnitID) {
            if(_adNetwork == null) 
            {
                _log.Warning("Ad Network is not initialized. Cannot set interstitial ad unit ID.");
                return;
            }
            _adNetwork.SetInterstitialAdUnitID(adUnitID);
        }
        /// <summary>
        /// Loads an interstitial ad from the ad network.
        /// </summary>
        public void LoadInterstitialAd() {
            if(_adNetwork == null) 
            {
                _log.Warning("Ad Network is not initialized. Cannot load interstitial ad.");
                return;
            } 
            _adNetwork.LoadInterstitialAd(); 
        }
        /// <summary>
        /// Shows a full-screen interstitial ad with a placeholder overlay while loading.
        /// </summary>
        public void ShowInterstitial() {

            if (IsAdmob())
            {
#if UNITY_ADMOB
                _hasClosedPlaceholder = false;

                ShowAdPlaceholder(AdPlaceholderType.Interstitial);

                if (_preloadManager == null)
                {
                    _log.Warning("Admob Preload Manager is not initialized. Cannot show interstitial ad.");
                    CloseAdPlaceholder();
                    return;
                }

                // Check if the ad is available before showing
                if (_preloadManager.IsAdAvailable(_interstitialAdUnitID, AdFormat.INTERSTITIAL))
                {
                    var ad = _preloadManager.PollInterstitialAd(_interstitialAdUnitID);
                    if (ad != null)
                    {
                        try
                        {
                            _log.Info("Showing Admob Interstitial Ad");
                            RegisterCallbackAdInterstitial(ad);
                            ad.Show();
                        }
                        catch (Exception ex)
                        {
                            _log.Error($"Exception showing Admob Interstitial Ad: {ex.Message}\n{ex.StackTrace}");
                            CloseAdPlaceholder();
                        }
                    }
                    else
                    {
                        _log.Warning("Admob Interstitial Ad poll returned null");
                        CloseAdPlaceholder();
                    }
                }
                else
                {
                    _log.Info("Admob Interstitial Ad not available");
                    CloseAdPlaceholder();
                }
#endif
            }
            else
            {
                if(_adNetwork == null)
                {
                    _log.Warning("Ad Network is not initialized. Cannot show interstitial ad.");
                    return;
                }

                _hasClosedPlaceholder = false;

                ShowAdPlaceholder(AdPlaceholderType.Interstitial);

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
                _onAdDisplayed?.Invoke();
                _log.Info("Noctua Placeholder closed for Interstitial Ad");
            };
            interstitialAd.OnAdFullScreenContentFailed += (AdError error) =>
            {
                CloseAdPlaceholder();
                _onAdFailedDisplayed?.Invoke();
                _log.Warning("Interstitial Ad failed to show full screen content, placeholder closed. Error: " + error);
            };
            interstitialAd.OnAdFullScreenContentClosed += () =>
            {
                _onAdClosed?.Invoke();
                _log.Debug("Preloaded Interstitial Ad closed");
            };
            interstitialAd.OnAdClicked += () =>
            {
                _onAdClicked?.Invoke();
                _log.Debug("Preloaded Interstitial Ad clicked");
            };
            interstitialAd.OnAdImpressionRecorded += () =>
            {
                _onAdImpressionRecorded?.Invoke();
                _log.Debug("Preloaded Interstitial Ad impression recorded");
            };
            interstitialAd.OnAdPaid += (AdValue adValue) =>
            {
                TrackPreloadedAdRevenue(adValue, interstitialAd.GetResponseInfo());
            };
        }
        #endif

        //Rewarded public functions
        private void SetRewardedAdUnitId(string adUnitID) { 
            if(_adNetwork == null) 
            {
                _log.Warning("Ad Network is not initialized. Cannot set rewarded ad unit ID.");
                return;
            }
            _adNetwork.SetRewardedAdUnitID(adUnitID); 
        }
        /// <summary>
        /// Loads a rewarded ad from the ad network.
        /// </summary>
        public void LoadRewardedAd() {
            if(_adNetwork == null)
            {
                _log.Warning("Ad Network is not initialized. Cannot load rewarded ad.");
                return;
            }
            
            _adNetwork.LoadRewardedAd();
        }
        /// <summary>
        /// Shows a rewarded ad with a placeholder overlay while loading.
        /// </summary>
        public void ShowRewardedAd()
        {
            if (IsAdmob())
            {
#if UNITY_ADMOB
                _hasClosedPlaceholder = false;

                ShowAdPlaceholder(AdPlaceholderType.Rewarded);

                if (_preloadManager == null)
                {
                    _log.Warning("Admob Preload Manager is not initialized. Cannot show rewarded ad.");
                    CloseAdPlaceholder();
                    return;
                }

                // Check if the ad is available before showing
                if (_preloadManager.IsAdAvailable(_rewardedAdUnitID, AdFormat.REWARDED))
                {
                    var ad = _preloadManager.PollRewardedAd(_rewardedAdUnitID);
                    if (ad != null)
                    {
                        try
                        {
                            _log.Info("Showing Admob Rewarded Ad");
                            RegisterCallbackAdRewarded(ad);
                            ad.Show((Reward reward) =>
                            {
                                _log.Info("User earned reward from mediation manager : " + reward.Type + " - " + reward.Amount);
                                _admobOnUserEarnedReward?.Invoke(reward);
                            });
                        }
                        catch (Exception ex)
                        {
                            _log.Error($"Exception showing Admob Rewarded Ad: {ex.Message}\n{ex.StackTrace}");
                            CloseAdPlaceholder();
                        }
                    }
                    else
                    {
                        _log.Warning("Admob Rewarded Ad poll returned null");
                        CloseAdPlaceholder();
                    }
                }
                else
                {
                    _log.Info("Admob Rewarded Ad not available");
                    CloseAdPlaceholder();
                }
#endif
            }
            else
            {
                if (_adNetwork == null)
                {
                    _log.Warning("Ad Network is not initialized. Cannot show rewarded ad.");
                    return;
                }

                _hasClosedPlaceholder = false;

                ShowAdPlaceholder(AdPlaceholderType.Rewarded);

                // For other networks, just show the ad
                _adNetwork.ShowRewardedAd();
            }
        }

        #if UNITY_ADMOB
        private void RegisterCallbackAdRewarded(RewardedAd rewardedAd)
        {
            rewardedAd.OnAdFullScreenContentOpened += () =>
            {
                CloseAdPlaceholder();
                _onAdDisplayed?.Invoke();
                _log.Info("Noctua Placeholder closed for Rewarded Ad");
            };
            rewardedAd.OnAdFullScreenContentFailed += (AdError error) =>
            {
                CloseAdPlaceholder();
                _onAdFailedDisplayed?.Invoke();
                _log.Warning("Rewarded Ad failed to show full screen content, placeholder closed. Error: " + error);
            };
            rewardedAd.OnAdFullScreenContentClosed += () =>
            {
                _onAdClosed?.Invoke();
                _log.Debug("Preloaded Rewarded Ad closed");
            };
            rewardedAd.OnAdClicked += () =>
            {
                _onAdClicked?.Invoke();
                _log.Debug("Preloaded Rewarded Ad clicked");
            };
            rewardedAd.OnAdImpressionRecorded += () =>
            {
                _onAdImpressionRecorded?.Invoke();
                _log.Debug("Preloaded Rewarded Ad impression recorded");
            };
            rewardedAd.OnAdPaid += (AdValue adValue) =>
            {
                TrackPreloadedAdRevenue(adValue, rewardedAd.GetResponseInfo());
            };
        }
        #endif

        //Rewarded Interstitial public functions for Admob
        #if UNITY_ADMOB
        private void SetRewardedInterstitialAdUnitId(string adUnitID) {

            if (!IsAdmob()) { return; }

            if(_adNetwork == null) 
            {
                _log.Warning("Ad Network is not initialized. Cannot set rewarded interstitial ad unit ID.");
                return;
            }

            _adNetwork.SetRewardedInterstitialAdUnitID(adUnitID);
        }
        /// <summary>
        /// Loads a rewarded interstitial ad (AdMob only).
        /// </summary>
        public void LoadRewardedInterstitialAd() {

            if(_adNetwork == null) 
            {
                _log.Warning("Ad Network is not initialized. Cannot load rewarded interstitial ad.");
                return;
            }
            _adNetwork.LoadRewardedInterstitialAd();
        }
        
        /// <summary>
        /// Shows a rewarded interstitial ad with a placeholder overlay (AdMob only).
        /// </summary>
        public void ShowRewardedInterstitialAd()
        {
            if(_adNetwork == null) 
            {
                _log.Warning("Ad Network is not initialized. Cannot show rewarded interstitial ad.");
                return;
            }

            _hasClosedPlaceholder = false;

            ShowAdPlaceholder(AdPlaceholderType.Rewarded);
            
            _adNetwork.ShowRewardedInterstitialAd();
        }
        #endif

        //Banner public functions
        private void SetBannerAdUnitId(string adUnitID) {
            if(_adNetwork == null) 
            {
                _log.Warning("Ad Network is not initialized. Cannot set banner ad unit ID.");
                return;
            }
            _adNetwork.SetBannerAdUnitId(adUnitID); 
        }
        /// <summary>
        /// Shows a banner ad using the configured ad network.
        /// </summary>
        public void ShowBannerAd()
        {
            if (_adNetwork == null)
            {
                _log.Warning("Ad Network is not initialized. Cannot show banner ad.");
                return;
            }

            // Disabled placeholder for banner ads for temporary
            // _hasClosedPlaceholder = false;
            // ShowAdPlaceholder(AdPlaceholderType.Banner);

            _adNetwork.ShowBannerAd();
        } 

        #if UNITY_ADMOB
        /// <summary>
        /// Creates a banner ad view with specified size and position (AdMob only).
        /// </summary>
        /// <param name="adSize">The ad size for the banner.</param>
        /// <param name="adPosition">The screen position for the banner.</param>
        public void CreateBannerViewAdAdmob(AdSize adSize, AdPosition adPosition)
        {
            if (!IsAdmob()) { return; }

            if(_adNetwork == null) 
            {
                _log.Warning("Ad Network is not initialized. Cannot create banner ad.");
                return;
            }

            _adNetwork.CreateBannerViewAdAdmob(adSize, adPosition);
        }
        #endif

        //Banner public function for AppLovin
        #if UNITY_APPLOVIN
        /// <summary>
        /// Creates a banner ad view with specified background color and position (AppLovin only, deprecated).
        /// </summary>
        /// <param name="color">Background color for the banner.</param>
        /// <param name="bannerPosition">The screen position for the banner.</param>
        [Obsolete(
            "This method is deprecated. Please use CreateBannerViewAdAppLovin(Color, MaxSdkBase.AdViewPosition) instead."
        )]
        public void CreateBannerViewAdAppLovin(Color color, MaxSdkBase.BannerPosition bannerPosition) 
        {
            if(!IsAppLovin()) { return; }

            if(_adNetwork == null) 
            {
                _log.Warning("Ad Network is not initialized. Cannot create banner ad.");
                return;
            }

            _adNetwork.CreateBannerViewAdAppLovin(color, bannerPosition);
        }

        /// <summary>
        /// Creates a banner ad view with specified background color and position (AppLovin only).
        /// </summary>
        /// <param name="color">Background color for the banner.</param>
        /// <param name="bannerPosition">The screen position for the banner.</param>
        public void CreateBannerViewAdAppLovin(Color color, MaxSdkBase.AdViewPosition bannerPosition)
        {
            if(!IsAppLovin()) { return; }

            if(_adNetwork == null)
            {
                _log.Warning("Ad Network is not initialized. Cannot create banner ad.");
                return;
            }

            _adNetwork.CreateBannerViewAdAppLovin(color, bannerPosition);
        }

        /// <summary>
        /// Hides the currently displayed AppLovin banner ad.
        /// </summary>
        public void HideAppLovinBanner() 
        {
            if(!IsAppLovin()) { return; }

            if(_adNetwork == null) 
            {
                _log.Warning("Ad Network is not initialized. Cannot hide banner ad.");
                return;
            }

            _adNetwork.HideBannerAppLovin();
        } 
        /// <summary>
        /// Destroys the AppLovin banner ad view and releases resources.
        /// </summary>
        public void DestroyBannerAppLovin() 
        {
            if(!IsAppLovin()) { return; }

            if(_adNetwork == null) 
            {
                _log.Warning("Ad Network is not initialized. Cannot destroy banner ad.");
                return;
            }

            _adNetwork.DestroyBannerAppLovin();
        }
        /// <summary>
        /// Sets the width of the AppLovin banner ad in pixels.
        /// </summary>
        /// <param name="width">Banner width in pixels.</param>
        public void SetBannerWidth(int width)
        {
            if(!IsAppLovin()) { return; }

            if(_adNetwork == null) 
            {
                _log.Warning("Ad Network is not initialized. Cannot set banner width.");
                return;
            }

            _adNetwork.SetBannerWidth(width);
        }
        /// <summary>
        /// Gets the current screen position and size of the AppLovin banner ad.
        /// </summary>
        /// <returns>A Rect representing the banner's position and dimensions.</returns>
        public Rect GetBannerPosition() 
        {
            if(!IsAppLovin()) { return new Rect(); }

            if(_adNetwork == null) 
            {
                _log.Warning("Ad Network is not initialized. Cannot get banner position.");
                return new Rect();
            }

            return _adNetwork.GetBannerPosition();
        }
        /// <summary>
        /// Stops automatic refresh of the AppLovin banner ad.
        /// </summary>
        public void StopBannerAutoRefresh()
        {
            if(!IsAppLovin()) { return; }

            if(_adNetwork == null) 
            {
                _log.Warning("Ad Network is not initialized. Cannot stop banner auto refresh.");
                return;
            }

            _adNetwork.StopBannerAutoRefresh();
        }
        /// <summary>
        /// Starts automatic refresh of the AppLovin banner ad.
        /// </summary>
        public void StartBannerAutoRefresh()
        {
            if(!IsAppLovin()) { return; }

            if(_adNetwork == null) 
            {
                _log.Warning("Ad Network is not initialized. Cannot start banner auto refresh.");
                return;
            }

            _adNetwork.StartBannerAutoRefresh();
        }
        #endif

        /// <summary>
        /// Opens the ad network's creative debugger UI for inspecting loaded ad creatives.
        /// </summary>
        public void ShowCreativeDebugger()
        {
            if(_adNetwork == null) 
            {
                _log.Warning("Ad Network is not initialized. Cannot show creative debugger.");
                return;
            }
            _adNetwork.ShowCreativeDebugger();
        }

        /// <summary>
        /// Opens the ad network's mediation debugger UI for inspecting waterfall and ad source configuration.
        /// </summary>
        public void ShowMediationDebugger()
        {
            if(_adNetwork == null) 
            {
                _log.Warning("Ad Network is not initialized. Cannot show mediation debugger.");
                return;
            }
            _adNetwork.ShowMediationDebugger();
        }
        
        /// <summary>
        /// Shows a loading placeholder overlay while an ad is being prepared for display.
        /// </summary>
        /// <param name="adType">The type of ad placeholder to show (Interstitial, Rewarded, Banner).</param>
        public void ShowAdPlaceholder(AdPlaceholderType adType)
        {
            _log.Info($"Showing ad placeholder for type: {adType}");

            if(_adPlaceholderUI == null)
            {
                _log.Warning("Ad placeholder UI is not initialized. Cannot show ad placeholder.");
                return;
            }

            _adPlaceholderUI.ShowAdPlaceholder(adType);
        }

        /// <summary>
        /// Closes the ad loading placeholder overlay. No-op if already closed.
        /// </summary>
        public void CloseAdPlaceholder()
        {
            if (_hasClosedPlaceholder)
            {
                _log.Info("Ad placeholder already closed. Skipping close action.");
                return;
            }
            
            if(_adPlaceholderUI == null)
            {
                _log.Warning("Ad placeholder UI is not initialized. Cannot close ad placeholder.");
                return;
            }

            _log.Info("Closing ad placeholder");

            _adPlaceholderUI.CloseAdPlaceholder();

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

#if UNITY_ADMOB
        /// <summary>
        /// Tracks ad revenue for preloaded ads. Replicates the same logic as the
        /// AdmobOnAdRevenuePaid handler in InitializeAdmob, ensuring preloaded ads
        /// have full revenue attribution.
        /// </summary>
        private void TrackPreloadedAdRevenue(AdValue adValue, ResponseInfo responseInfo)
        {
            try
            {
                long valueMicros = adValue.Value;
                string currencyCode = adValue.CurrencyCode;

                string responseId = responseInfo?.GetResponseId() ?? "empty";

                AdapterResponseInfo loadedAdapterResponseInfo = responseInfo?.GetLoadedAdapterResponseInfo();

                string adSourceId = loadedAdapterResponseInfo?.AdSourceId ?? "empty";
                string adSourceInstanceId = loadedAdapterResponseInfo?.AdSourceInstanceId ?? "empty";
                string adSourceInstanceName = loadedAdapterResponseInfo?.AdSourceInstanceName ?? "empty";
                string adSourceName = loadedAdapterResponseInfo?.AdSourceName ?? "empty";
                string adapterClassName = loadedAdapterResponseInfo?.AdapterClassName ?? "empty";
                long latencyMillis = loadedAdapterResponseInfo?.LatencyMillis ?? 0;

                Dictionary<string, string> extras = responseInfo?.GetResponseExtras();
                string mediationGroupName = extras != null && extras.ContainsKey("mediation_group_name") ? extras["mediation_group_name"] : "empty";
                string mediationABTestName = extras != null && extras.ContainsKey("mediation_ab_test_name") ? extras["mediation_ab_test_name"] : "empty";
                string mediationABTestVariant = extras != null && extras.ContainsKey("mediation_ab_test_variant") ? extras["mediation_ab_test_variant"] : "empty";

                double revenue = valueMicros / 1_000_000.0;

                _log.Debug($"Preloaded Ad Revenue Paid: value in micros: {adValue.Value} / converted: {revenue}, {currencyCode} " +
                    $"Ad Source: {adSourceName}, Adapter: {adapterClassName}");

                _adRevenueTracker?.TrackAdRevenue("admob_sdk", revenue, currencyCode, new Dictionary<string, IConvertible>
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
            }
            catch (Exception ex)
            {
                _log.Error($"Error tracking preloaded ad revenue: {ex.Message}\n{ex.StackTrace}");
            }
        }
#endif
    }
}
