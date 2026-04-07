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
    /// ad loading, display, revenue tracking, and hybrid fallback orchestration.
    /// </summary>
    public class MediationManager
    {
        private readonly NoctuaLogger _log = new(typeof(MediationManager));
        private HybridAdOrchestrator _orchestrator;
        private AdRevenueTrackingManager _revenueTracker;
        private AdFrequencyManager _frequencyManager;
        private AppOpenAdManager _appOpenAdManager;
        private AdNetworkPerformanceTracker _performanceTracker;
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
        private string _appOpenAdUnitID = "unused";

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
        private bool _hasClosedPlaceholder;
        private bool _adNetworkEventsSubscribed;
        private bool _preloadManagerEventsSubscribed;

        private IAA _iaaResponse;

        internal IAA IAAResponse
        {
            get => _iaaResponse;
            set
            {
                _iaaResponse = value;
                if (value != null)
                {
                    _adNetworkEventsSubscribed = false;
                    _preloadManagerEventsSubscribed = false;
                    CreateNetworks(value);
                }
            }
        }

        /// <summary>Returns the App Open ad manager for foreground auto-show control.</summary>
        public AppOpenAdManager AppOpenManager => _appOpenAdManager;

        /// <summary>Returns true if running in hybrid mode (both networks active).</summary>
        public bool IsHybridMode => _orchestrator?.IsHybridMode ?? false;

        /// <summary>Returns the active mediation type string.</summary>
        public string MediationType => _mediationType;

        internal void SetAdRevenueTracker(IAdRevenueTracker tracker)
        {
            _adRevenueTracker = tracker;
            _revenueTracker?.SetAdRevenueTracker(tracker);
        }

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

            IAAResponse = iAAResponse;
        }

        private void CreateNetworks(IAA iaaConfig)
        {
            IAdNetwork primary = null;
            IAdNetwork secondary = null;

            #if UNITY_ADMOB
            primary = new AdmobManager();
            #endif

            #if UNITY_APPLOVIN
            // If ADMOB is also defined and secondary_mediation is set, AppLovin becomes secondary
            #if UNITY_ADMOB
            if (!string.IsNullOrEmpty(iaaConfig.SecondaryMediation) &&
                iaaConfig.SecondaryMediation == "applovin")
            {
                secondary = new AppLovinManager();
            }
            #else
            primary = new AppLovinManager();
            #endif
            #endif

            // If no primary was set (no defines), log error
            if (primary == null)
            {
                #if UNITY_APPLOVIN
                // AppLovin is primary when ADMOB is not defined
                #else
                _log.Error("No ad network SDK is available. Define UNITY_ADMOB or UNITY_APPLOVIN.");
                return;
                #endif
            }

            // Check for secondary in the reverse direction: primary is applovin, secondary is admob
            #if UNITY_APPLOVIN && UNITY_ADMOB
            if (primary is AppLovinManager && !string.IsNullOrEmpty(iaaConfig.SecondaryMediation) &&
                iaaConfig.SecondaryMediation == "admob")
            {
                secondary = new AdmobManager();
            }
            #endif

            // Create supporting managers
            _frequencyManager = new AdFrequencyManager(
                iaaConfig.FrequencyCaps,
                iaaConfig.CooldownSeconds,
                iaaConfig.EnabledFormats
            );

            _revenueTracker = new AdRevenueTrackingManager(_adRevenueTracker, iaaConfig.Taichi);

            _performanceTracker = iaaConfig.DynamicOptimization
                ? new AdNetworkPerformanceTracker()
                : null;

            _orchestrator = new HybridAdOrchestrator(
                primary: primary,
                secondary: secondary,
                adFormatOverrides: iaaConfig.AdFormatOverrides,
                performanceTracker: _performanceTracker,
                dynamicOptimization: iaaConfig.DynamicOptimization
            );

            _log.Info($"Networks created. Primary: {primary.NetworkName}" +
                (secondary != null ? $", Secondary: {secondary.NetworkName}" : "") +
                $", Hybrid: {_orchestrator.IsHybridMode}");
        }

        /// <summary>
        /// Initializes the ad mediation SDK based on the configured mediation type.
        /// </summary>
        public void Initialize(Action initCompleteAction = null)
        {
            if (IAAResponse == null)
            {
                _log.Error("Cannot initialize MediationManager: IAA response is null.");
                return;
            }

            if (_orchestrator == null)
            {
                _log.Error("Cannot initialize MediationManager: orchestrator not created.");
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

            SubscribeToOrchestratorEvents();
            SubscribeToNetworkSpecificEvents();

            // Use orchestrator to initialize both networks concurrently.
            // Primary callback fires when primary SDK is ready → load primary ads.
            // Secondary callback fires when secondary SDK is ready → load secondary ads.
            // Never call Load on a network before its own init callback fires (per AdMob and AppLovin docs).
            _orchestrator.Initialize(
                onPrimaryReady: () =>
                {
                    _log.Info("Primary network initialized: " + _orchestrator.Primary.NetworkName);

                    if (IAAResponse.AdFormat == null && (IAAResponse.Networks == null || IAAResponse.Networks.Count == 0))
                    {
                        _log.Info("No ad format config. Cannot proceed with ad unit ID setup.");
                        initCompleteAction?.Invoke();
                        return;
                    }

                    if (IsAdmob())
                    {
#if UNITY_ADMOB
                        _preloadManager = AdmobAdPreloadManager.Instance;
#endif
                    }

                    SetupAdUnitID(IAAResponse);
                    initCompleteAction?.Invoke();
                },
                onSecondaryReady: () =>
                {
                    var secondary = _orchestrator.Secondary;
                    if (secondary == null || IAAResponse == null) return;

                    _log.Info("Secondary network initialized: " + secondary.NetworkName);
                    SetupSecondaryAdUnits(IAAResponse, secondary);
                    SetupSecondaryAppOpen(IAAResponse, secondary);
                });
        }

        private void SubscribeToOrchestratorEvents()
        {
            if (_adNetworkEventsSubscribed) return;
            _adNetworkEventsSubscribed = true;

            _orchestrator.OnAdDisplayed += () =>
            {
                CloseAdPlaceholder();
                _appOpenAdManager?.SetFullscreenAdShowing(true);
                _onAdDisplayed?.Invoke();
            };

            _orchestrator.OnAdFailedDisplayed += () =>
            {
                CloseAdPlaceholder();
                _onAdFailedDisplayed?.Invoke();
            };

            _orchestrator.OnAdClicked += () => _onAdClicked?.Invoke();
            _orchestrator.OnAdImpressionRecorded += () => _onAdImpressionRecorded?.Invoke();

            _orchestrator.OnAdClosed += () =>
            {
                _appOpenAdManager?.SetFullscreenAdShowing(false);
                _onAdClosed?.Invoke();
            };
        }

        private void SubscribeToNetworkSpecificEvents()
        {
            var primary = _orchestrator.Primary;
            var secondary = _orchestrator.Secondary;

#if UNITY_ADMOB
            SubscribeAdmobRevenueEvents(primary);
            if (secondary != null) SubscribeAdmobRevenueEvents(secondary);
#endif

#if UNITY_APPLOVIN
            SubscribeAppLovinRevenueEvents(primary);
            if (secondary != null) SubscribeAppLovinRevenueEvents(secondary);
#endif
        }

#if UNITY_ADMOB
        private void SubscribeAdmobRevenueEvents(IAdNetwork network)
        {
            if (network.NetworkName != "admob") return;

            network.AdmobOnUserEarnedReward += (reward) => _admobOnUserEarnedReward?.Invoke(reward);
            network.AdmobOnAdRevenuePaid += (adValue, responseInfo) =>
            {
                _revenueTracker.ProcessAdmobRevenue(adValue, responseInfo);
                _admobOnAdRevenuePaid?.Invoke(adValue, responseInfo);
            };
        }
#endif

#if UNITY_APPLOVIN
        private void SubscribeAppLovinRevenueEvents(IAdNetwork network)
        {
            if (network.NetworkName != "applovin") return;

            network.AppLovinOnUserEarnedReward += (reward) => _appLovinOnUserEarnedReward?.Invoke(reward);
            network.AppLovinOnAdRevenuePaid += (adInfo) =>
            {
                _revenueTracker.ProcessAppLovinRevenue(adInfo);
                _appLovinOnAdRevenuePaid?.Invoke(adInfo);
            };
        }
#endif

        /// <summary>
        /// Configures ad unit IDs for all ad formats based on the IAA server response, then loads initial ads.
        /// </summary>
        public void SetupAdUnitID(IAA iAAResponse)
        {
            var primary = _orchestrator.Primary;
            var secondary = _orchestrator.Secondary;

            ResolveAdUnitIDs(iAAResponse, primary.NetworkName);

            primary.SetBannerAdUnitId(_bannerAdUnitID);

            if (IsAdmob() && primary.NetworkName == "admob")
            {
#if UNITY_ADMOB
                // All AdMob fullscreen formats use the Preload API exclusively.
                // Do NOT call primary.SetRewardedInterstitialAdUnitID() / LoadRewardedInterstitialAd()
                // or AppOpenAd.Load() — mixing preload and legacy paths for the same ad unit
                // causes race conditions per AdMob docs.

                // _preloadManager already assigned in Initialize() callback; reuse the same singleton.
                var configs = new List<PreloadConfiguration>
                {
                    _preloadManager.CreateInterstitialPreloadConfig(_interstitialAdUnitID),
                    _preloadManager.CreateRewardedPreloadConfig(_rewardedAdUnitID),
                    _preloadManager.CreateRewardedInterstitialPreloadConfig(_rewardedInterstitialAdUnitID),
                };

                // App Open: buffer=1 is recommended (Google docs: app open shown once per foreground).
                if (!string.IsNullOrEmpty(_appOpenAdUnitID) && _appOpenAdUnitID != "unknown")
                {
                    configs.Add(_preloadManager.CreateAppOpenPreloadConfig(_appOpenAdUnitID, bufferSize: 1));
                }

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
                primary.SetInterstitialAdUnitID(_interstitialAdUnitID);
                primary.SetRewardedAdUnitID(_rewardedAdUnitID);
                primary.LoadInterstitialAd();
                primary.LoadRewardedAd();
            }

            // NOTE: Secondary ad units are NOT loaded here.
            // They are loaded in the onSecondaryReady callback in Initialize(),
            // which only fires after the secondary SDK has fully initialized.

            // Configure App Open ads (primary only; secondary added in SetupSecondaryAppOpen)
            SetupAppOpenAds(iAAResponse);

            _onInitialized?.Invoke();
            _log.Info("Ad Unit IDs set up for mediation type: " + _mediationType);
        }

        private void SetupSecondaryAdUnits(IAA iAAResponse, IAdNetwork secondary)
        {
            string secondaryInterstitial = ResolveAdUnitIdForNetwork(iAAResponse, secondary.NetworkName, "interstitial");
            string secondaryRewarded = ResolveAdUnitIdForNetwork(iAAResponse, secondary.NetworkName, "rewarded");
            string secondaryBanner = ResolveAdUnitIdForNetwork(iAAResponse, secondary.NetworkName, "banner");

            if (secondaryInterstitial != "unknown")
            {
                secondary.SetInterstitialAdUnitID(secondaryInterstitial);
                secondary.LoadInterstitialAd();
            }

            if (secondaryRewarded != "unknown")
            {
                secondary.SetRewardedAdUnitID(secondaryRewarded);
                secondary.LoadRewardedAd();
            }

            if (secondaryBanner != "unknown")
            {
                secondary.SetBannerAdUnitId(secondaryBanner);
            }
        }

        /// <summary>
        /// Sets up App Open ads on the primary network only.
        /// Secondary App Open is wired later in <see cref="SetupSecondaryAppOpen"/> once the secondary SDK is ready.
        /// </summary>
        private void SetupAppOpenAds(IAA iAAResponse)
        {
            string primaryAppOpenId = ResolveAdUnitIdForNetwork(iAAResponse, _orchestrator.Primary.NetworkName, "app_open");

            if (string.IsNullOrEmpty(primaryAppOpenId) || primaryAppOpenId == "unknown")
            {
                _log.Debug("No primary app open ad unit ID configured.");
                return;
            }

            _appOpenAdManager = new AppOpenAdManager(
                primaryNetwork: _orchestrator.Primary,
                secondaryNetwork: _orchestrator.Secondary,
                frequencyManager: _frequencyManager,
                autoShowOnForeground: iAAResponse.AppOpenAutoShow,
                cooldownSeconds: iAAResponse.AppOpenCooldownSeconds > 0 ? iAAResponse.AppOpenCooldownSeconds : 30
            );

            // Only pass primary here; secondary will be added in SetupSecondaryAppOpen after secondary SDK is ready
            _appOpenAdManager.Configure(primaryAppOpenId, null);
        }

        /// <summary>
        /// Adds secondary App Open ad support after the secondary SDK has finished initialization.
        /// Safe to call only from the onSecondaryReady callback.
        /// </summary>
        private void SetupSecondaryAppOpen(IAA iAAResponse, IAdNetwork secondary)
        {
            if (_appOpenAdManager == null) return;

            string secondaryAppOpenId = ResolveAdUnitIdForNetwork(iAAResponse, secondary.NetworkName, "app_open");
            if (!string.IsNullOrEmpty(secondaryAppOpenId) && secondaryAppOpenId != "unknown")
            {
                _appOpenAdManager.ConfigureSecondary(secondaryAppOpenId);
            }
        }

        private void ResolveAdUnitIDs(IAA iAAResponse, string networkName)
        {
            _interstitialAdUnitID = ResolveAdUnitIdForNetwork(iAAResponse, networkName, "interstitial");
            _rewardedAdUnitID = ResolveAdUnitIdForNetwork(iAAResponse, networkName, "rewarded");
            _rewardedInterstitialAdUnitID = ResolveAdUnitIdForNetwork(iAAResponse, networkName, "rewarded_interstitial");
            _bannerAdUnitID = ResolveAdUnitIdForNetwork(iAAResponse, networkName, "banner");
            _appOpenAdUnitID = ResolveAdUnitIdForNetwork(iAAResponse, networkName, "app_open");
        }

        /// <summary>
        /// Resolves ad unit ID for a given network and format.
        /// Priority: test mode → networks block → flat ad_formats → "unknown".
        /// </summary>
        private string ResolveAdUnitIdForNetwork(IAA iAAResponse, string networkName, string format)
        {
            // Test mode: use AdMob test IDs
            if (iAAResponse.TestMode)
            {
                string testId = AdTestUnitIds.GetTestAdUnitId(format, Application.platform);
                if (!string.IsNullOrEmpty(testId))
                {
                    return testId;
                }
            }

            // Try networks block first
            if (iAAResponse.Networks != null &&
                iAAResponse.Networks.TryGetValue(networkName, out var networkConfig) &&
                networkConfig?.AdFormat != null)
            {
                string id = GetAdUnitIdFromFormat(networkConfig.AdFormat, format);
                if (!string.IsNullOrEmpty(id) && id != "unknown")
                {
                    return id;
                }
            }

            // Fallback to flat ad_formats (backward compat)
            if (iAAResponse.AdFormat != null)
            {
                string id = GetAdUnitIdFromFormat(iAAResponse.AdFormat, format);
                if (!string.IsNullOrEmpty(id))
                {
                    return id;
                }
            }

            return "unknown";
        }

        private string GetAdUnitIdFromFormat(AdFormatNoctua adFormat, string format)
        {
            AdUnit adUnit = null;

            switch (format)
            {
                case "interstitial":
                    adUnit = adFormat.Interstitial;
                    break;
                case "rewarded":
                    adUnit = adFormat.Rewarded;
                    break;
                case "rewarded_interstitial":
                    adUnit = adFormat.RewardedInterstitial;
                    break;
                case "banner":
                    adUnit = adFormat.Banner;
                    break;
                case "app_open":
                    adUnit = adFormat.AppOpen;
                    break;
            }

            if (adUnit == null) return "unknown";

#if UNITY_ANDROID
            return string.IsNullOrEmpty(adUnit.Android?.adUnitID) ? "unknown" : adUnit.Android.adUnitID;
#elif UNITY_IPHONE
            return string.IsNullOrEmpty(adUnit.IOS?.adUnitID) ? "unknown" : adUnit.IOS.adUnitID;
#else
            return "unknown";
#endif
        }

        // --- Public ad show methods ---

        /// <summary>Loads an interstitial ad from the ad network.</summary>
        public void LoadInterstitialAd()
        {
            if (_orchestrator == null)
            {
                _log.Warning("Orchestrator not initialized. Cannot load interstitial ad.");
                return;
            }
            _orchestrator.Primary.LoadInterstitialAd();
        }

        /// <summary>Shows a full-screen interstitial ad with a placeholder overlay while loading.</summary>
        public void ShowInterstitial()
        {
            if (_orchestrator == null)
            {
                _log.Warning("Orchestrator not initialized. Cannot show interstitial ad.");
                return;
            }

            if (_frequencyManager != null && !_frequencyManager.CanShowAd("interstitial"))
            {
                _log.Info("Interstitial ad blocked by frequency manager.");
                return;
            }

            if (IsAdmob())
            {
#if UNITY_ADMOB
                ShowAdmobInterstitial();
#endif
            }
            else
            {
                _hasClosedPlaceholder = false;
                ShowAdPlaceholder(AdPlaceholderType.Interstitial);
                _orchestrator.Primary.ShowInterstitial();
            }

            _frequencyManager?.RecordImpression("interstitial");
        }

#if UNITY_ADMOB
        private void ShowAdmobInterstitial()
        {
            _hasClosedPlaceholder = false;
            ShowAdPlaceholder(AdPlaceholderType.Interstitial);

            if (_preloadManager == null)
            {
                _log.Warning("Admob Preload Manager is not initialized. Cannot show interstitial ad.");
                CloseAdPlaceholder();
                return;
            }

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
        }

        private void RegisterCallbackAdInterstitial(InterstitialAd interstitialAd)
        {
            interstitialAd.OnAdFullScreenContentOpened += () =>
            {
                CloseAdPlaceholder();
                _onAdDisplayed?.Invoke();
            };
            interstitialAd.OnAdFullScreenContentFailed += (AdError error) =>
            {
                CloseAdPlaceholder();
                _onAdFailedDisplayed?.Invoke();
                _log.Warning("Interstitial Ad failed to show. Error: " + error);
            };
            interstitialAd.OnAdFullScreenContentClosed += () =>
            {
                _onAdClosed?.Invoke();
            };
            interstitialAd.OnAdClicked += () =>
            {
                _onAdClicked?.Invoke();
            };
            interstitialAd.OnAdImpressionRecorded += () =>
            {
                _onAdImpressionRecorded?.Invoke();
            };
            interstitialAd.OnAdPaid += (AdValue adValue) =>
            {
                _revenueTracker.ProcessAdmobInterstitialRevenue(adValue, interstitialAd.GetResponseInfo());
                _admobOnAdRevenuePaid?.Invoke(adValue, interstitialAd.GetResponseInfo());
            };
        }
#endif

        /// <summary>Loads a rewarded ad from the ad network.</summary>
        public void LoadRewardedAd()
        {
            if (_orchestrator == null)
            {
                _log.Warning("Orchestrator not initialized. Cannot load rewarded ad.");
                return;
            }
            _orchestrator.Primary.LoadRewardedAd();
        }

        /// <summary>Shows a rewarded ad with a placeholder overlay while loading.</summary>
        public void ShowRewardedAd()
        {
            if (_orchestrator == null)
            {
                _log.Warning("Orchestrator not initialized. Cannot show rewarded ad.");
                return;
            }

            if (_frequencyManager != null && !_frequencyManager.CanShowAd("rewarded"))
            {
                _log.Info("Rewarded ad blocked by frequency manager.");
                return;
            }

            if (IsAdmob())
            {
#if UNITY_ADMOB
                ShowAdmobRewarded();
#endif
            }
            else
            {
                _hasClosedPlaceholder = false;
                ShowAdPlaceholder(AdPlaceholderType.Rewarded);
                _orchestrator.Primary.ShowRewardedAd();
            }

            _frequencyManager?.RecordImpression("rewarded");
        }

#if UNITY_ADMOB
        private void ShowAdmobRewarded()
        {
            _hasClosedPlaceholder = false;
            ShowAdPlaceholder(AdPlaceholderType.Rewarded);

            if (_preloadManager == null)
            {
                _log.Warning("Admob Preload Manager is not initialized. Cannot show rewarded ad.");
                CloseAdPlaceholder();
                return;
            }

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
                            _log.Info("User earned reward: " + reward.Type + " - " + reward.Amount);
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
        }

        private void RegisterCallbackAdRewarded(RewardedAd rewardedAd)
        {
            rewardedAd.OnAdFullScreenContentOpened += () =>
            {
                CloseAdPlaceholder();
                _onAdDisplayed?.Invoke();
            };
            rewardedAd.OnAdFullScreenContentFailed += (AdError error) =>
            {
                CloseAdPlaceholder();
                _onAdFailedDisplayed?.Invoke();
                _log.Warning("Rewarded Ad failed to show. Error: " + error);
            };
            rewardedAd.OnAdFullScreenContentClosed += () =>
            {
                _onAdClosed?.Invoke();
            };
            rewardedAd.OnAdClicked += () =>
            {
                _onAdClicked?.Invoke();
            };
            rewardedAd.OnAdImpressionRecorded += () =>
            {
                _onAdImpressionRecorded?.Invoke();
            };
            rewardedAd.OnAdPaid += (AdValue adValue) =>
            {
                _revenueTracker.ProcessAdmobRewardedRevenue(adValue, rewardedAd.GetResponseInfo());
                _admobOnAdRevenuePaid?.Invoke(adValue, rewardedAd.GetResponseInfo());
            };
        }

        /// <summary>Shows a rewarded interstitial ad with a placeholder overlay (AdMob only).</summary>
        public void ShowRewardedInterstitialAd()
        {
            if (_orchestrator == null)
            {
                _log.Warning("Orchestrator not initialized. Cannot show rewarded interstitial ad.");
                return;
            }

            if (IsAdmob())
            {
#if UNITY_ADMOB
                ShowAdmobRewardedInterstitial();
#endif
            }
            else
            {
                _hasClosedPlaceholder = false;
                ShowAdPlaceholder(AdPlaceholderType.Rewarded);
                _orchestrator.Primary.ShowRewardedInterstitialAd();
            }
        }

#if UNITY_ADMOB
        private void ShowAdmobRewardedInterstitial()
        {
            _hasClosedPlaceholder = false;
            ShowAdPlaceholder(AdPlaceholderType.Rewarded);

            if (_preloadManager == null)
            {
                _log.Warning("Admob Preload Manager is not initialized. Cannot show rewarded interstitial ad.");
                CloseAdPlaceholder();
                return;
            }

            if (_preloadManager.IsAdAvailable(_rewardedInterstitialAdUnitID, AdFormat.REWARDED_INTERSTITIAL))
            {
                var ad = _preloadManager.PollRewardedInterstitialAd(_rewardedInterstitialAdUnitID);
                if (ad != null)
                {
                    try
                    {
                        _log.Info("Showing Admob Rewarded Interstitial Ad");
                        RegisterCallbackAdRewardedInterstitial(ad);
                        ad.Show((Reward reward) =>
                        {
                            _log.Info("User earned reward: " + reward.Type + " - " + reward.Amount);
                            _admobOnUserEarnedReward?.Invoke(reward);
                        });
                    }
                    catch (Exception ex)
                    {
                        _log.Error($"Exception showing Admob Rewarded Interstitial Ad: {ex.Message}\n{ex.StackTrace}");
                        CloseAdPlaceholder();
                    }
                }
                else
                {
                    _log.Warning("Admob Rewarded Interstitial Ad poll returned null");
                    CloseAdPlaceholder();
                }
            }
            else
            {
                _log.Info("Admob Rewarded Interstitial Ad not available");
                CloseAdPlaceholder();
            }
        }

        private void RegisterCallbackAdRewardedInterstitial(RewardedInterstitialAd ad)
        {
            ad.OnAdFullScreenContentOpened += () =>
            {
                CloseAdPlaceholder();
                _onAdDisplayed?.Invoke();
            };
            ad.OnAdFullScreenContentFailed += (AdError error) =>
            {
                CloseAdPlaceholder();
                _onAdFailedDisplayed?.Invoke();
                _log.Warning("Rewarded Interstitial Ad failed to show. Error: " + error);
            };
            ad.OnAdFullScreenContentClosed += () =>
            {
                _onAdClosed?.Invoke();
            };
            ad.OnAdClicked += () =>
            {
                _onAdClicked?.Invoke();
            };
            ad.OnAdImpressionRecorded += () =>
            {
                _onAdImpressionRecorded?.Invoke();
            };
            ad.OnAdPaid += (AdValue adValue) =>
            {
                _revenueTracker.ProcessAdmobRevenue(adValue, ad.GetResponseInfo());
                _admobOnAdRevenuePaid?.Invoke(adValue, ad.GetResponseInfo());
            };
        }
#endif

        /// <summary>Shows a banner ad using the configured ad network.</summary>
        public void ShowBannerAd()
        {
            if (_orchestrator == null)
            {
                _log.Warning("Orchestrator not initialized. Cannot show banner ad.");
                return;
            }
            _orchestrator.Primary.ShowBannerAd();
        }

#if UNITY_ADMOB
        /// <summary>Creates a banner ad view with specified size and position (AdMob only).</summary>
        public void CreateBannerViewAdAdmob(AdSize adSize, AdPosition adPosition)
        {
            if (!IsAdmob() || _orchestrator == null) return;
            _orchestrator.Primary.CreateBannerViewAdAdmob(adSize, adPosition);
        }
#endif

#if UNITY_APPLOVIN
        /// <summary>Creates a banner ad view (AppLovin, deprecated).</summary>
        [Obsolete("Use CreateBannerViewAdAppLovin(Color, MaxSdkBase.AdViewPosition) instead.")]
        public void CreateBannerViewAdAppLovin(Color color, MaxSdkBase.BannerPosition bannerPosition)
        {
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.CreateBannerViewAdAppLovin(color, bannerPosition);
        }

        /// <summary>Creates a banner ad view with specified background color and position (AppLovin).</summary>
        public void CreateBannerViewAdAppLovin(Color color, MaxSdkBase.AdViewPosition bannerPosition)
        {
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.CreateBannerViewAdAppLovin(color, bannerPosition);
        }

        /// <summary>Hides the currently displayed AppLovin banner ad.</summary>
        public void HideAppLovinBanner()
        {
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.HideBannerAppLovin();
        }

        /// <summary>Destroys the AppLovin banner ad view and releases resources.</summary>
        public void DestroyBannerAppLovin()
        {
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.DestroyBannerAppLovin();
        }

        /// <summary>Sets the width of the AppLovin banner ad in pixels.</summary>
        public void SetBannerWidth(int width)
        {
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.SetBannerWidth(width);
        }

        /// <summary>Gets the current screen position and size of the AppLovin banner ad.</summary>
        public Rect GetBannerPosition()
        {
            if (!IsAppLovin() || _orchestrator == null) return new Rect();
            return _orchestrator.Primary.GetBannerPosition();
        }

        /// <summary>Stops automatic refresh of the AppLovin banner ad.</summary>
        public void StopBannerAutoRefresh()
        {
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.StopBannerAutoRefresh();
        }

        /// <summary>Starts automatic refresh of the AppLovin banner ad.</summary>
        public void StartBannerAutoRefresh()
        {
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.StartBannerAutoRefresh();
        }

        /// <summary>Mutes or unmutes ad audio (AppLovin only).</summary>
        public void SetMuted(bool muted)
        {
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.SetMuted(muted);
        }

        /// <summary>Sets the placement name for the AppLovin banner ad.</summary>
        public void SetBannerPlacement(string placement)
        {
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.SetBannerPlacement(placement);
        }

        /// <summary>Shows a previously loaded interstitial ad with a placement name (AppLovin).</summary>
        public void ShowInterstitial(string placement)
        {
            if (!IsAppLovin() || _orchestrator == null) return;
            _hasClosedPlaceholder = false;
            ShowAdPlaceholder(AdPlaceholderType.Interstitial);
            _orchestrator.Primary.ShowInterstitial(placement);
        }

        /// <summary>Shows a previously loaded rewarded ad with a placement name (AppLovin).</summary>
        public void ShowRewardedAd(string placement)
        {
            if (!IsAppLovin() || _orchestrator == null) return;
            _hasClosedPlaceholder = false;
            ShowAdPlaceholder(AdPlaceholderType.Rewarded);
            _orchestrator.Primary.ShowRewardedAd(placement);
        }

        /// <summary>Sets the banner auto-refresh interval in seconds (AppLovin). Clamped to 10-120s.</summary>
        public void SetBannerRefreshInterval(int seconds)
        {
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.SetBannerRefreshInterval(seconds);
        }
#endif

        // --- App Open Ad public methods ---

        /// <summary>Shows an app open ad (tries primary then secondary network).</summary>
        public void ShowAppOpenAd()
        {
            if (_appOpenAdManager == null)
            {
                _log.Warning("App Open ad manager not configured.");
                return;
            }
            _appOpenAdManager.ShowAppOpenAd();
        }

        /// <summary>Returns whether an app open ad is ready on any network.</summary>
        public bool IsAppOpenAdReady()
        {
            return _appOpenAdManager?.IsAppOpenAdReady() ?? false;
        }

        /// <summary>Handles app foreground transitions for app open ad auto-show.</summary>
        public void OnApplicationForeground()
        {
            _appOpenAdManager?.OnApplicationForeground();
        }

        // --- Debugger methods ---

        /// <summary>Opens the ad network's creative debugger UI.</summary>
        public void ShowCreativeDebugger()
        {
            if (_orchestrator == null) return;
            _orchestrator.Primary.ShowCreativeDebugger();
        }

        /// <summary>Opens the ad network's mediation debugger UI.</summary>
        public void ShowMediationDebugger()
        {
            if (_orchestrator == null) return;
            _orchestrator.Primary.ShowMediationDebugger();
        }

        // --- Placeholder methods ---

        /// <summary>Shows a loading placeholder overlay while an ad is being prepared.</summary>
        public void ShowAdPlaceholder(AdPlaceholderType adType)
        {
            _log.Info($"Showing ad placeholder for type: {adType}");

            if (_adPlaceholderUI == null)
            {
                _log.Warning("Ad placeholder UI is not initialized.");
                return;
            }

            _adPlaceholderUI.ShowAdPlaceholder(adType);
        }

        /// <summary>Closes the ad loading placeholder overlay. No-op if already closed.</summary>
        public void CloseAdPlaceholder()
        {
            if (_hasClosedPlaceholder) return;

            if (_adPlaceholderUI == null)
            {
                _log.Warning("Ad placeholder UI is not initialized.");
                return;
            }

            _log.Info("Closing ad placeholder");
            _adPlaceholderUI.CloseAdPlaceholder();
            _hasClosedPlaceholder = true;
        }

        private bool IsAppLovin()
        {
            if (_mediationType == "applovin") return true;

            _log.Info("Mediation type is not AppLovin. Current: " + _mediationType);
            return false;
        }

        private bool IsAdmob()
        {
            if (_mediationType == "admob") return true;

            _log.Info("Mediation type is not Admob. Current: " + _mediationType);
            return false;
        }
    }
}
