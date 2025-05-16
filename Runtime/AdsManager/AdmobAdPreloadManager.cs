#if UNITY_ADMOB
using GoogleMobileAds.Api;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Manages preloading of AdMob ads to improve user experience by having ads ready to show instantly.
    /// Based on Google's Ad Preloading documentation: https://developers.google.com/admob/unity/ad-preloading
    /// </summary>
    public class AdmobAdPreloadManager
    {
        private readonly NoctuaLogger _log = new(typeof(AdmobAdPreloadManager));
        
        // Dictionary to track preload configurations by ad format and ad unit ID
        private Dictionary<string, PreloadConfiguration> _preloadConfigurations = new Dictionary<string, PreloadConfiguration>();
        
        // Event handlers for preloading notifications
        public event Action<PreloadConfiguration> OnAdsAvailable;
        public event Action<PreloadConfiguration> OnAdExhausted;
        
        // Singleton instance
        private static AdmobAdPreloadManager _instance;
        public static AdmobAdPreloadManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new AdmobAdPreloadManager();
                }
                return _instance;
            }
        }
        
        /// <summary>
        /// Starts preloading ads with the specified configurations
        /// </summary>
        /// <param name="preloadConfigs">List of PreloadConfiguration objects specifying ad units to preload</param>
        public void StartPreloading(List<PreloadConfiguration> preloadConfigs)
        {
            if (preloadConfigs == null || preloadConfigs.Count == 0)
            {
                _log.Warning("No preload configurations provided. Preloading will not start.");
                return;
            }
            
            _log.Info($"Starting preloading for {preloadConfigs.Count} ad configurations");
            
            // Store configurations for later reference
            foreach (var config in preloadConfigs)
            {
                string key = GetConfigKey(config);
                _preloadConfigurations[key] = config;
            }
            
            // Start preloading
            MobileAds.Preload(preloadConfigs, OnAdsAvailableHandler, OnAdExhaustedHandler);
        }
        
        /// <summary>
        /// Modifies existing preload configurations or adds new ones
        /// </summary>
        /// <param name="preloadConfigs">Updated list of PreloadConfiguration objects</param>
        public void ModifyPreloading(List<PreloadConfiguration> preloadConfigs)
        {
            if (preloadConfigs == null || preloadConfigs.Count == 0)
            {
                _log.Warning("No preload configurations provided for modification.");
                return;
            }
            
            _log.Info($"Modifying preloading for {preloadConfigs.Count} ad configurations");
            
            // Update stored configurations
            foreach (var config in preloadConfigs)
            {
                string key = GetConfigKey(config);
                _preloadConfigurations[key] = config;
            }
            
            // Update preloading
            MobileAds.Preload(preloadConfigs, OnAdsAvailableHandler, OnAdExhaustedHandler);
        }
        
        /// <summary>
        /// Stops preloading for all ad configurations
        /// </summary>
        public void StopPreloading()
        {
            _log.Info("Stopping all ad preloading");
            MobileAds.Preload(new List<PreloadConfiguration>(), null, null);
            _preloadConfigurations.Clear();
        }
        
        /// <summary>
        /// Checks if a preloaded ad is available for the specified ad unit ID and format
        /// </summary>
        /// <param name="adUnitId">Ad unit ID to check</param>
        /// <param name="adFormat">Ad format to check</param>
        /// <returns>True if a preloaded ad is available</returns>
        public bool IsAdAvailable(string adUnitId, AdFormat adFormat)
        {
            if (string.IsNullOrEmpty(adUnitId))
            {
                _log.Error("Ad unit ID is null or empty");
                return false;
            }
            
            bool isAvailable = false;
            
            switch (adFormat)
            {
                case AdFormat.INTERSTITIAL:
                    isAvailable = InterstitialAd.IsAdAvailable(adUnitId);
                    break;
                case AdFormat.REWARDED:
                    isAvailable = RewardedAd.IsAdAvailable(adUnitId);
                    break;
                case AdFormat.APP_OPEN_AD:
                    isAvailable = AppOpenAd.IsAdAvailable(adUnitId);
                    break;
                default:
                    _log.Error($"Unsupported ad format: {adFormat}");
                    break;
            }
            
            return isAvailable;
        }
        
        /// <summary>
        /// Polls for and returns a preloaded interstitial ad
        /// </summary>
        /// <param name="adUnitId">Ad unit ID to poll</param>
        /// <returns>InterstitialAd if available, null otherwise</returns>
        public InterstitialAd PollInterstitialAd(string adUnitId)
        {
            if (string.IsNullOrEmpty(adUnitId))
            {
                _log.Error("Ad unit ID is null or empty");
                return null;
            }
            
            if (!InterstitialAd.IsAdAvailable(adUnitId))
            {
                _log.Warning($"No preloaded interstitial ad available for ad unit ID: {adUnitId}");
                return null;
            }
            
            _log.Info($"Polling interstitial ad for ad unit ID: {adUnitId}");
            return InterstitialAd.PollAd(adUnitId);
        }
        
        /// <summary>
        /// Polls for and returns a preloaded rewarded ad
        /// </summary>
        /// <param name="adUnitId">Ad unit ID to poll</param>
        /// <returns>RewardedAd if available, null otherwise</returns>
        public RewardedAd PollRewardedAd(string adUnitId)
        {
            if (string.IsNullOrEmpty(adUnitId))
            {
                _log.Error("Ad unit ID is null or empty");
                return null;
            }
            
            if (!RewardedAd.IsAdAvailable(adUnitId))
            {
                _log.Warning($"No preloaded rewarded ad available for ad unit ID: {adUnitId}");
                return null;
            }
            
            _log.Info($"Polling rewarded ad for ad unit ID: {adUnitId}");
            return RewardedAd.PollAd(adUnitId);
        }
        
        /// <summary>
        /// Polls for and returns a preloaded app open ad
        /// </summary>
        /// <param name="adUnitId">Ad unit ID to poll</param>
        /// <returns>AppOpenAd if available, null otherwise</returns>
        public AppOpenAd PollAppOpenAd(string adUnitId)
        {
            if (string.IsNullOrEmpty(adUnitId))
            {
                _log.Error("Ad unit ID is null or empty");
                return null;
            }
            
            if (!AppOpenAd.IsAdAvailable(adUnitId))
            {
                _log.Warning($"No preloaded app open ad available for ad unit ID: {adUnitId}");
                return null;
            }
            
            _log.Info($"Polling app open ad for ad unit ID: {adUnitId}");
            return AppOpenAd.PollAd(adUnitId);
        }
        
        /// <summary>
        /// Creates a preload configuration for an interstitial ad
        /// </summary>
        /// <param name="adUnitId">Ad unit ID</param>
        /// <param name="bufferSize">Optional buffer size (default: 2)</param>
        /// <param name="adRequest">Optional custom ad request</param>
        /// <returns>PreloadConfiguration for interstitial ad</returns>
        public PreloadConfiguration CreateInterstitialPreloadConfig(string adUnitId, uint bufferSize = 3, AdRequest adRequest = null)
        {
            return new PreloadConfiguration
            {
                Format = AdFormat.INTERSTITIAL,
                AdUnitId = adUnitId,
                BufferSize = bufferSize,
                Request = adRequest
            };
        }
        
        /// <summary>
        /// Creates a preload configuration for a rewarded ad
        /// </summary>
        /// <param name="adUnitId">Ad unit ID</param>
        /// <param name="bufferSize">Optional buffer size (default: 2)</param>
        /// <param name="adRequest">Optional custom ad request</param>
        /// <returns>PreloadConfiguration for rewarded ad</returns>
        public PreloadConfiguration CreateRewardedPreloadConfig(string adUnitId, uint bufferSize = 3, AdRequest adRequest = null)
        {
            return new PreloadConfiguration
            {
                Format = AdFormat.REWARDED,
                AdUnitId = adUnitId,
                BufferSize = bufferSize,
                Request = adRequest
            };
        }
        
        /// <summary>
        /// Creates a preload configuration for a rewarded interstitial ad
        /// </summary>
        /// <param name="adUnitId">Ad unit ID</param>
        /// <param name="bufferSize">Optional buffer size (default: 2)</param>
        /// <param name="adRequest">Optional custom ad request</param>
        /// <returns>PreloadConfiguration for rewarded interstitial ad</returns>
        public PreloadConfiguration CreateRewardedInterstitialPreloadConfig(string adUnitId, uint bufferSize = 3, AdRequest adRequest = null)
        {
            return new PreloadConfiguration
            {
                Format = AdFormat.REWARDED_INTERSTITIAL,
                AdUnitId = adUnitId,
                BufferSize = bufferSize,
                Request = adRequest
            };
        }
        
        /// <summary>
        /// Creates a preload configuration for an app open ad
        /// </summary>
        /// <param name="adUnitId">Ad unit ID</param>
        /// <param name="bufferSize">Optional buffer size (default: 2)</param>
        /// <param name="adRequest">Optional custom ad request</param>
        /// <returns>PreloadConfiguration for app open ad</returns>
        public PreloadConfiguration CreateAppOpenPreloadConfig(string adUnitId, uint bufferSize = 3, AdRequest adRequest = null)
        {
            return new PreloadConfiguration
            {
                Format = AdFormat.APP_OPEN_AD,
                AdUnitId = adUnitId,
                BufferSize = bufferSize,
                Request = adRequest
            };
        }
        
        /// <summary>
        /// Gets the response info for a preloaded ad
        /// </summary>
        /// <param name="adUnitId">Ad unit ID</param>
        /// <param name="adFormat">Ad format</param>
        /// <returns>ResponseInfo if available, null otherwise</returns>
        public ResponseInfo GetResponseInfo(string adUnitId, AdFormat adFormat)
        {
            ResponseInfo responseInfo = null;
            
            switch (adFormat)
            {
                case AdFormat.INTERSTITIAL:
                    var interstitialAd = PollInterstitialAd(adUnitId);
                    if (interstitialAd != null)
                    {
                        responseInfo = interstitialAd.GetResponseInfo();
                    }
                    break;
                case AdFormat.REWARDED:
                    var rewardedAd = PollRewardedAd(adUnitId);
                    if (rewardedAd != null)
                    {
                        responseInfo = rewardedAd.GetResponseInfo();
                    }
                    break;
                case AdFormat.APP_OPEN_AD:
                    var appOpenAd = PollAppOpenAd(adUnitId);
                    if (appOpenAd != null)
                    {
                        responseInfo = appOpenAd.GetResponseInfo();
                    }
                    break;
                default:
                    _log.Error($"Unsupported ad format: {adFormat}");
                    break;
            }
            
            return responseInfo;
        }
        
        /// <summary>
        /// Handler for ads available notification
        /// </summary>
        /// <param name="preloadConfig">Preload configuration for which ads are available</param>
        private void OnAdsAvailableHandler(PreloadConfiguration preloadConfig)
        {
            string key = GetConfigKey(preloadConfig);
            _log.Info($"Preloaded ad available for {preloadConfig.Format}, ad unit ID: {preloadConfig.AdUnitId}");
            
            // Invoke the event
            OnAdsAvailable?.Invoke(preloadConfig);
        }
        
        /// <summary>
        /// Handler for ads exhausted notification
        /// </summary>
        /// <param name="preloadConfig">Preload configuration for which ads are exhausted</param>
        private void OnAdExhaustedHandler(PreloadConfiguration preloadConfig)
        {
            string key = GetConfigKey(preloadConfig);
            _log.Warning($"Preloaded ads exhausted for {preloadConfig.Format}, ad unit ID: {preloadConfig.AdUnitId}");
            
            // Invoke the event
            OnAdExhausted?.Invoke(preloadConfig);
        }
        
        /// <summary>
        /// Gets a unique key for a preload configuration
        /// </summary>
        /// <param name="config">Preload configuration</param>
        /// <returns>Unique key string</returns>
        private string GetConfigKey(PreloadConfiguration config)
        {
            return $"{config.Format}_{config.AdUnitId}";
        }
    }
}
#endif