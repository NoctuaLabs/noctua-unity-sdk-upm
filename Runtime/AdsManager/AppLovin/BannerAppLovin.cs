#if UNITY_APPLOVIN
using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

namespace com.noctuagames.sdk.AppLovin
{
    public class BannerAppLovin
    {
        private readonly NoctuaLogger _log = new(typeof(BannerAppLovin));

        private string _adUnitIDBanner;

        public void SetBannerAdUnitId(string adUnitIDBanner) {
            if(adUnitIDBanner == null)
            {
                _log.Error("Ad unit ID banner is empty.");
                return;
            }

            _adUnitIDBanner = adUnitIDBanner;

            _log.Debug("Banner ad loaded for ad unit id : " + adUnitIDBanner);
        }

        public void InitializeBannerAds(Color color, MaxSdkBase.BannerPosition bannerPosition)
        {
            // Banners are automatically sized to 320×50 on phones and 728×90 on tablets
            // You may call the utility method MaxSdkUtils.isTablet() to help with view sizing adjustments
            MaxSdk.CreateBanner(_adUnitIDBanner, bannerPosition);

            // Set background color for banners to be fully functional
            MaxSdk.SetBannerBackgroundColor(_adUnitIDBanner, color);

            MaxSdkCallbacks.Banner.OnAdLoadedEvent      += OnBannerAdLoadedEvent;
            MaxSdkCallbacks.Banner.OnAdLoadFailedEvent  += OnBannerAdLoadFailedEvent;
            MaxSdkCallbacks.Banner.OnAdClickedEvent     += OnBannerAdClickedEvent;
            MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += OnBannerAdRevenuePaidEvent;
            MaxSdkCallbacks.Banner.OnAdExpandedEvent    += OnBannerAdExpandedEvent;
            MaxSdkCallbacks.Banner.OnAdCollapsedEvent   += OnBannerAdCollapsedEvent;
        }

        public void ShowBanner()
        {
            MaxSdk.ShowBanner(_adUnitIDBanner);
        }

        public void HideBanner()
        {
            MaxSdk.HideBanner(_adUnitIDBanner);
        }

        //Destroying Banners
        // You may no longer need an ad instance (for example, if the user purchased ad removal). 
        // If so, call the DestroyBanner() method to free resources. 
        // Do not call DestroyBanner() if you use multiple ad instances with the same Ad Unit ID.
        public void DestroyBanner()
        {
            MaxSdk.DestroyBanner(_adUnitIDBanner);
        }

        //Setting Banner Width
        // To manually set the banner’s width, call SetBannerWidth(). Set the width to a size larger than the minimum value (320 on phones, 728 on tablets).
        //  Banners under this width may not be considered viewable by the advertiser, which will affect your revenue:
        public void SetBannerWidth(int width)
        {
            MaxSdk.SetBannerWidth(_adUnitIDBanner, width);
        }

        // Getting Banner Position
        // To get the banner’s position and size, call GetBannerLayout(). This uses the same Unity coordinate system as explained in Loading a Banner.
        public Rect GetBannerPosition()
        {
            return MaxSdk.GetBannerLayout(_adUnitIDBanner);
        }
        // Stopping and Starting Auto-Refresh
        // You may want to stop auto-refresh for an ad, for instance if you want to manually refresh banner ads. 
        // To stop auto-refresh for a banner, use the following code:
        public void StopBannerAutoRefresh()
        {
            MaxSdk.StopBannerAutoRefresh(_adUnitIDBanner);
        }

        public void StartBannerAutoRefresh()
        {
            MaxSdk.StartBannerAutoRefresh(_adUnitIDBanner);
        }

        private void OnBannerAdLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) {}

        private void OnBannerAdLoadFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo) {}

        private void OnBannerAdClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) {}

        private void OnBannerAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) {}

        private void OnBannerAdExpandedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)  {}

        private void OnBannerAdCollapsedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) {}
    }
}
#endif