using System;
using UnityEngine;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Manages App Open ad lifecycle: loading, foreground auto-show with cooldown,
    /// and coordination with the ad frequency manager to prevent conflicts with other fullscreen ads.
    /// </summary>
    public class AppOpenAdManager
    {
        private readonly NoctuaLogger _log = new(typeof(AppOpenAdManager));

        private readonly IAdNetwork _primaryNetwork;
        private readonly IAdNetwork _secondaryNetwork;
        private readonly AdFrequencyManager _frequencyManager;
        private readonly bool _autoShowOnForeground;
        private readonly int _cooldownSeconds;

        private DateTime _lastShowTime = DateTime.MinValue;
        private bool _isFullscreenAdShowing;
        private bool _appOpenAdUnitConfigured;

        /// <summary>
        /// Creates a new AppOpenAdManager.
        /// </summary>
        /// <param name="primaryNetwork">The primary ad network for app open ads.</param>
        /// <param name="secondaryNetwork">Optional secondary ad network for fallback.</param>
        /// <param name="frequencyManager">The frequency manager for enforcing caps.</param>
        /// <param name="autoShowOnForeground">Whether to auto-show on app foreground.</param>
        /// <param name="cooldownSeconds">Minimum seconds between app open ad impressions.</param>
        public AppOpenAdManager(
            IAdNetwork primaryNetwork,
            IAdNetwork secondaryNetwork = null,
            AdFrequencyManager frequencyManager = null,
            bool autoShowOnForeground = false,
            int cooldownSeconds = 30)
        {
            _primaryNetwork = primaryNetwork;
            _secondaryNetwork = secondaryNetwork;
            _frequencyManager = frequencyManager;
            _autoShowOnForeground = autoShowOnForeground;
            _cooldownSeconds = cooldownSeconds > 0 ? cooldownSeconds : 30;
        }

        /// <summary>
        /// Configures and loads app open ads on the primary (and optionally secondary) network.
        /// </summary>
        /// <param name="primaryAdUnitId">Ad unit ID for the primary network.</param>
        /// <param name="secondaryAdUnitId">Optional ad unit ID for the secondary network.</param>
        public void Configure(string primaryAdUnitId, string secondaryAdUnitId = null)
        {
            if (string.IsNullOrEmpty(primaryAdUnitId) || primaryAdUnitId == "unknown")
            {
                _log.Warning("Primary app open ad unit ID is not configured.");
                return;
            }

            _primaryNetwork.SetAppOpenAdUnitID(primaryAdUnitId);
            _primaryNetwork.LoadAppOpenAd();
            _appOpenAdUnitConfigured = true;

            _log.Info($"App Open ad configured on primary network ({_primaryNetwork.NetworkName}) with unit: {primaryAdUnitId}");

            if (_secondaryNetwork != null && !string.IsNullOrEmpty(secondaryAdUnitId) && secondaryAdUnitId != "unknown")
            {
                _secondaryNetwork.SetAppOpenAdUnitID(secondaryAdUnitId);
                _secondaryNetwork.LoadAppOpenAd();
                _log.Info($"App Open ad configured on secondary network ({_secondaryNetwork.NetworkName}) with unit: {secondaryAdUnitId}");
            }
        }

        /// <summary>
        /// Call this when the app transitions to the foreground (e.g., from OnApplicationPause(false)).
        /// Checks whether an app open ad should be shown based on cooldown, frequency caps, and availability.
        /// </summary>
        public void OnApplicationForeground()
        {
            if (!_autoShowOnForeground)
            {
                _log.Debug("App open auto-show is disabled.");
                return;
            }

            if (!_appOpenAdUnitConfigured)
            {
                _log.Debug("App open ad unit not configured yet.");
                return;
            }

            if (_isFullscreenAdShowing)
            {
                _log.Debug("Another fullscreen ad is currently showing. Skipping app open ad.");
                return;
            }

            if (!IsReadyToShow())
            {
                _log.Debug("App open ad is not ready to show (cooldown or frequency cap).");
                return;
            }

            ShowAppOpenAd();
        }

        /// <summary>
        /// Manually shows an app open ad, trying primary first then secondary.
        /// </summary>
        public void ShowAppOpenAd()
        {
            if (_frequencyManager != null && !_frequencyManager.CanShowAd("app_open"))
            {
                _log.Info("App open ad blocked by frequency manager.");
                return;
            }

            if (_primaryNetwork.IsAppOpenAdReady())
            {
                _log.Info($"Showing app open ad from primary network ({_primaryNetwork.NetworkName}).");
                _primaryNetwork.ShowAppOpenAd();
                RecordShow();
                return;
            }

            if (_secondaryNetwork != null && _secondaryNetwork.IsAppOpenAdReady())
            {
                _log.Info($"Primary not ready. Showing app open ad from secondary network ({_secondaryNetwork.NetworkName}).");
                _secondaryNetwork.ShowAppOpenAd();
                RecordShow();
                return;
            }

            _log.Warning("No app open ad is ready on any network.");
        }

        /// <summary>
        /// Returns whether an app open ad is loaded and ready on any network.
        /// </summary>
        public bool IsAppOpenAdReady()
        {
            return _primaryNetwork.IsAppOpenAdReady() ||
                   (_secondaryNetwork != null && _secondaryNetwork.IsAppOpenAdReady());
        }

        /// <summary>
        /// Configures and loads app open ads on the secondary network.
        /// Call this only after the secondary SDK has finished initialization.
        /// </summary>
        /// <param name="secondaryAdUnitId">Ad unit ID for the secondary network.</param>
        public void ConfigureSecondary(string secondaryAdUnitId)
        {
            if (_secondaryNetwork == null)
            {
                _log.Warning("ConfigureSecondary called but no secondary network is set.");
                return;
            }

            if (string.IsNullOrEmpty(secondaryAdUnitId) || secondaryAdUnitId == "unknown")
            {
                _log.Warning("Secondary app open ad unit ID is not configured.");
                return;
            }

            _secondaryNetwork.SetAppOpenAdUnitID(secondaryAdUnitId);
            _secondaryNetwork.LoadAppOpenAd();
            _log.Info($"App Open ad configured on secondary network ({_secondaryNetwork.NetworkName}) with unit: {secondaryAdUnitId}");
        }

        /// <summary>
        /// Loads app open ads on all configured networks.
        /// </summary>
        public void LoadAppOpenAd()
        {
            if (_appOpenAdUnitConfigured)
            {
                _primaryNetwork.LoadAppOpenAd();
                _secondaryNetwork?.LoadAppOpenAd();
            }
        }

        /// <summary>
        /// Notifies the manager that a fullscreen ad (interstitial/rewarded) is showing.
        /// Prevents app open ads from interrupting.
        /// </summary>
        public void SetFullscreenAdShowing(bool isShowing)
        {
            _isFullscreenAdShowing = isShowing;
        }

        private bool IsReadyToShow()
        {
            double elapsed = (DateTime.UtcNow - _lastShowTime).TotalSeconds;
            if (elapsed < _cooldownSeconds)
            {
                _log.Debug($"App open ad cooldown active: {elapsed:F0}s / {_cooldownSeconds}s.");
                return false;
            }

            if (_frequencyManager != null && !_frequencyManager.CanShowAd("app_open"))
            {
                return false;
            }

            return IsAppOpenAdReady();
        }

        private void RecordShow()
        {
            _lastShowTime = DateTime.UtcNow;
            _frequencyManager?.RecordImpression("app_open");
        }
    }
}
