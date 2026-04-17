using System;
using UnityEngine;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Manages App Open ad lifecycle: loading, foreground auto-show with cooldown,
    /// and coordination with the ad frequency manager to prevent conflicts with other fullscreen ads.
    ///
    /// All cooldown and frequency-cap decisions are delegated exclusively to
    /// <see cref="AdFrequencyManager"/> (keyed by <see cref="AdFormatKey.AppOpen"/>).
    /// The app-open-specific cooldown is merged into <see cref="CooldownConfig.AppOpen"/>
    /// by <see cref="MediationManager"/> before the frequency manager is constructed,
    /// so there is no dual-source conflict.
    /// </summary>
    public class AppOpenAdManager
    {
        private readonly NoctuaLogger _log = new(typeof(AppOpenAdManager));

        private readonly IAdNetwork _primaryNetwork;
        private readonly IAdNetwork _secondaryNetwork;
        private readonly AdFrequencyManager _frequencyManager;
        private readonly bool _autoShowOnForeground;

        /// <summary>
        /// Optional network name override from <see cref="IAA.AdFormatOverrides"/> for "app_open".
        /// When set, <see cref="ShowAppOpenAd"/> tries this network first instead of primary.
        /// </summary>
        private readonly string _preferredNetworkName;

        private readonly Action<string> _onAdNotAvailable;

        private bool _isFullscreenAdShowing;
        private bool _appOpenAdUnitConfigured;

        // Timestamp (seconds since app start) of the most recent fullscreen-ad close
        // (interstitial / rewarded / rewarded interstitial). Blocks app-open auto-show
        // for <see cref="FullscreenCloseGraceSeconds"/> after close — prevents the race
        // where Unity's OnApplicationPause(false) fires AFTER OnAdClosed has already
        // cleared _isFullscreenAdShowing, causing app-open to pop immediately after
        // every rewarded / interstitial ad.
        private float _lastFullscreenClosedAtRealtime = -1f;
        private const float FullscreenCloseGraceSeconds = 3f;

        /// <summary>
        /// Creates a new AppOpenAdManager.
        /// </summary>
        /// <param name="primaryNetwork">The primary ad network for app open ads.</param>
        /// <param name="secondaryNetwork">Optional secondary ad network for fallback.</param>
        /// <param name="frequencyManager">
        /// The frequency manager for enforcing caps and cooldowns.
        /// Its <see cref="CooldownConfig.AppOpen"/> must already include the desired
        /// minimum seconds between impressions (set by <see cref="MediationManager"/>).
        /// </param>
        /// <param name="autoShowOnForeground">Whether to auto-show on app foreground.</param>
        /// <param name="preferredNetworkName">
        /// Optional network name from <see cref="IAA.AdFormatOverrides"/> for the "app_open" format.
        /// When set, that network is tried first on show. Defaults to null (primary always first).
        /// </param>
        /// <param name="onAdNotAvailable">
        /// Optional callback invoked with <see cref="AdFormatKey.AppOpen"/> when a show request is
        /// silently dropped (cooldown active, frequency cap reached, or no inventory on any network).
        /// </param>
        public AppOpenAdManager(
            IAdNetwork primaryNetwork,
            IAdNetwork secondaryNetwork = null,
            AdFrequencyManager frequencyManager = null,
            bool autoShowOnForeground = false,
            string preferredNetworkName = null,
            Action<string> onAdNotAvailable = null)
        {
            _primaryNetwork = primaryNetwork;
            _secondaryNetwork = secondaryNetwork;
            _frequencyManager = frequencyManager;
            _autoShowOnForeground = autoShowOnForeground;
            _preferredNetworkName = preferredNetworkName;
            _onAdNotAvailable = onAdNotAvailable;
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

            if (IsInFullscreenCloseGrace())
            {
                _log.Info($"Fullscreen ad closed < {FullscreenCloseGraceSeconds}s ago. " +
                          "Skipping app-open auto-show to avoid stacking on top of a just-closed rewarded/interstitial.");
                return;
            }

            if (!IsReadyToShow())
            {
                _log.Debug("App open ad is not ready to show (cooldown, frequency cap, or no inventory).");
                return;
            }

            ShowAppOpenAd();
        }

        /// <summary>
        /// Manually shows an app open ad.
        /// Cooldown and frequency caps are enforced by <see cref="AdFrequencyManager"/>.
        /// If <see cref="_preferredNetworkName"/> is set (from <see cref="IAA.AdFormatOverrides"/>),
        /// that network is tried first; otherwise primary is tried first.
        /// </summary>
        public void ShowAppOpenAd()
        {
            if (_isFullscreenAdShowing)
            {
                _log.Info("Another fullscreen ad is currently showing. Skipping app open ad.");
                _onAdNotAvailable?.Invoke(AdFormatKey.AppOpen);
                return;
            }

            if (IsInFullscreenCloseGrace())
            {
                _log.Info($"Fullscreen ad closed < {FullscreenCloseGraceSeconds}s ago. " +
                          "Skipping app-open show to avoid stacking on a just-closed rewarded/interstitial.");
                _onAdNotAvailable?.Invoke(AdFormatKey.AppOpen);
                return;
            }

            if (_frequencyManager != null && !_frequencyManager.CanShowAd(AdFormatKey.AppOpen))
            {
                _log.Info("App open ad blocked by frequency/cooldown manager.");
                _onAdNotAvailable?.Invoke(AdFormatKey.AppOpen);
                return;
            }

            // Resolve preferred vs fallback networks based on format override.
            IAdNetwork preferred = _primaryNetwork;
            IAdNetwork fallback = _secondaryNetwork;

            if (_preferredNetworkName != null &&
                _secondaryNetwork != null &&
                _preferredNetworkName == _secondaryNetwork.NetworkName)
            {
                preferred = _secondaryNetwork;
                fallback = _primaryNetwork;
            }

            if (preferred.IsAppOpenAdReady())
            {
                _log.Info($"Showing app open ad from preferred network ({preferred.NetworkName}).");
                preferred.ShowAppOpenAd();
                RecordShow();
                return;
            }

            if (fallback != null && fallback.IsAppOpenAdReady())
            {
                _log.Info($"Preferred not ready. Showing app open ad from fallback network ({fallback.NetworkName}).");
                fallback.ShowAppOpenAd();
                RecordShow();
                return;
            }

            _log.Warning("No app open ad is ready on any network.");
            _onAdNotAvailable?.Invoke(AdFormatKey.AppOpen);
        }

        /// <summary>
        /// Returns whether an app open ad is ready to show: inventory loaded on at least one network
        /// AND frequency cap / cooldown allow it. Consistent with <see cref="MediationManager.IsInterstitialReady"/>.
        /// </summary>
        public bool IsAppOpenAdReady()
        {
            if (_frequencyManager != null && !_frequencyManager.CanShowAd(AdFormatKey.AppOpen))
                return false;

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
            if (!isShowing)
            {
                // Capture close time so OnApplicationForeground / ShowAppOpenAd can block
                // the auto-pop race for a short grace window.
                _lastFullscreenClosedAtRealtime = Time.realtimeSinceStartup;
            }
        }

        /// <summary>
        /// Returns true when a fullscreen ad closed less than
        /// <see cref="FullscreenCloseGraceSeconds"/> ago. Used to block the race where
        /// Unity's OnApplicationPause(false) fires after OnAdClosed has already cleared
        /// <see cref="_isFullscreenAdShowing"/>, causing app-open to pop instantly after
        /// every rewarded / interstitial.
        /// </summary>
        private bool IsInFullscreenCloseGrace()
        {
            if (_lastFullscreenClosedAtRealtime < 0f) return false;
            return Time.realtimeSinceStartup - _lastFullscreenClosedAtRealtime < FullscreenCloseGraceSeconds;
        }

        // ─────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────

        // Delegates to IsAppOpenAdReady(), which already includes the frequency/cooldown check.
        private bool IsReadyToShow() => IsAppOpenAdReady();

        private void RecordShow()
        {
            _frequencyManager?.RecordImpression(AdFormatKey.AppOpen);
        }
    }
}
