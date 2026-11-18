using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using com.noctuagames.sdk.AdPlaceholder;
using com.noctuagames.sdk.Events;

#if UNITY_ADMOB
using GoogleMobileAds.Api;
using static GoogleMobileAds.Api.AdValue;
using com.noctuagames.sdk.Admob;
#endif

namespace com.noctuagames.sdk
{
    public partial class NoctuaIAAService
    {
        /// <summary>Shows a banner ad using the configured ad network, falling back to secondary if needed.</summary>
        public void ShowBannerAd()
        {
            _log.Info($"{LogTag} show_banner - show banner ad");
            if (_routing == null)
            {
                _log.Warning("Ad routing not initialized. Cannot show banner ad.");
                return;
            }

            // New game-initiated request — re-arm the cross-promo (clears any dismiss from a prior request).
            _hasClosedPlaceholder = false;

            if (_frequencyCap != null && !_frequencyCap.CanShowAd(AdFormatKey.Banner))
            {
                _log.Info("Banner ad blocked by frequency/enabled config.");
                NotifyAdNotAvailable(AdFormatKey.Banner);
                return;
            }

            var preferred = _routing.GetNetworkForFormat(AdFormatKey.Banner);
            if (preferred.HasBannerAdUnit())
            {
                preferred.ShowBannerAd();
                return;
            }

            var fallback = preferred == _routing.Primary ? _routing.Secondary : _routing.Primary;
            if (fallback != null && fallback.HasBannerAdUnit())
            {
                _log.Info($"{preferred.NetworkName} has no banner unit. Falling back to {fallback.NetworkName}.");
                fallback.ShowBannerAd();
                return;
            }

            _log.Warning("No banner ad unit configured on any network.");
            NotifyAdNotAvailable(AdFormatKey.Banner);
        }

        /// <summary>
        /// Network-agnostic banner hide. Hides the currently displayed banner on both
        /// Primary and Secondary networks (whichever one is actually showing it), without
        /// destroying the underlying banner instance. Prefer this over
        /// <see cref="HideAppLovinBanner"/> when you want mediation-agnostic behavior,
        /// e.g. after a successful "remove ads" IAP on a game that may run either AdMob
        /// or AppLovin as its primary network.
        /// Safe no-op when the routing is not yet initialized.
        /// </summary>
        public void HideBannerAd()
        {
            _log.Debug($"{LogTag} hide_banner - hide banner ad");
            if (_routing == null)
            {
                _log.Warning("Ad routing not initialized. Cannot hide banner ad.");
                return;
            }

            _routing.Primary?.HideBannerAd();
            _routing.Secondary?.HideBannerAd();
        }

#if UNITY_ADMOB
        /// <summary>Creates a banner ad view with specified size and position (AdMob only).</summary>
        public void CreateBannerViewAdAdmob(AdSize adSize, AdPosition adPosition)
        {
            _log.Debug($"{LogTag} create_banner_admob - create AdMob banner view");
            if (!IsAdmob() || _routing == null) return;
            _routing.Primary.CreateBannerViewAdAdmob(adSize, adPosition);
        }
#endif

#if UNITY_APPLOVIN
        /// <summary>Creates a banner ad view (AppLovin, deprecated).</summary>
        [Obsolete("Use CreateBannerViewAdAppLovin(Color, MaxSdkBase.AdViewPosition) instead.")]
        public void CreateBannerViewAdAppLovin(Color color, MaxSdkBase.BannerPosition bannerPosition)
        {
            _log.Debug($"{LogTag} create_banner_applovin_banner_pos - create AppLovin banner view (banner pos)");
            if (!IsAppLovin() || _routing == null) return;
            _routing.Primary.CreateBannerViewAdAppLovin(color, bannerPosition);
        }

        /// <summary>Creates a banner ad view with specified background color and position (AppLovin).</summary>
        public void CreateBannerViewAdAppLovin(Color color, MaxSdkBase.AdViewPosition bannerPosition)
        {
            _log.Debug($"{LogTag} create_banner_applovin_adview_pos - create AppLovin banner view (adview pos)");
            if (!IsAppLovin() || _routing == null) return;
            _routing.Primary.CreateBannerViewAdAppLovin(color, bannerPosition);
        }

        /// <summary>Hides the currently displayed AppLovin banner ad.</summary>
        public void HideAppLovinBanner()
        {
            _log.Debug($"{LogTag} hide_applovin_banner - hide AppLovin banner");
            if (!IsAppLovin() || _routing == null) return;
            _routing.Primary.HideBannerAppLovin();
        }

        /// <summary>Destroys the AppLovin banner ad view and releases resources.</summary>
        public void DestroyBannerAppLovin()
        {
            _log.Debug($"{LogTag} destroy_applovin_banner - destroy AppLovin banner");
            if (!IsAppLovin() || _routing == null) return;
            _routing.Primary.DestroyBannerAppLovin();
        }

        /// <summary>Sets the width of the AppLovin banner ad in pixels.</summary>
        public void SetBannerWidth(int width)
        {
            _log.Debug($"{LogTag} set_banner_width - set banner width");
            if (!IsAppLovin() || _routing == null) return;
            _routing.Primary.SetBannerWidth(width);
        }

        /// <summary>Gets the current screen position and size of the AppLovin banner ad.</summary>
        public Rect GetBannerPosition()
        {
            _log.Debug($"{LogTag} get_banner_position - get banner position rect");
            if (!IsAppLovin() || _routing == null) return new Rect();
            return _routing.Primary.GetBannerPosition();
        }

        /// <summary>Stops automatic refresh of the AppLovin banner ad.</summary>
        public void StopBannerAutoRefresh()
        {
            _log.Debug($"{LogTag} stop_banner_auto_refresh - stop banner auto-refresh");
            if (!IsAppLovin() || _routing == null) return;
            _routing.Primary.StopBannerAutoRefresh();
        }

        /// <summary>Starts automatic refresh of the AppLovin banner ad.</summary>
        public void StartBannerAutoRefresh()
        {
            _log.Debug($"{LogTag} start_banner_auto_refresh - start banner auto-refresh");
            if (!IsAppLovin() || _routing == null) return;
            _routing.Primary.StartBannerAutoRefresh();
        }

        /// <summary>Mutes or unmutes ad audio (AppLovin only).</summary>
        public void SetMuted(bool muted)
        {
            _log.Debug($"{LogTag} set_muted - set muted state");
            if (!IsAppLovin() || _routing == null) return;
            _routing.Primary.SetMuted(muted);
        }

        /// <summary>Sets the placement name for the AppLovin banner ad.</summary>
        public void SetBannerPlacement(string placement)
        {
            _log.Debug($"{LogTag} set_banner_placement - set banner placement");
            if (!IsAppLovin() || _routing == null) return;
            _routing.Primary.SetBannerPlacement(placement);
        }

        /// <summary>Sets the banner auto-refresh interval in seconds (AppLovin). Clamped to 10-120s.</summary>
        public void SetBannerRefreshInterval(int seconds)
        {
            _log.Debug($"{LogTag} set_banner_refresh_interval - set banner refresh interval");
            if (!IsAppLovin() || _routing == null) return;
            _routing.Primary.SetBannerRefreshInterval(seconds);
        }
#endif
    }
}
