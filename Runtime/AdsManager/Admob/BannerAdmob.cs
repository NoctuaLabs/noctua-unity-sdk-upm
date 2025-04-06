#if UNITY_ADMOB
using GoogleMobileAds.Api;
using System;

namespace com.noctuagames.sdk.Admob
{
    public class BannerAdmob
    {
        private readonly NoctuaLogger _log = new(typeof(BannerAdmob));
        private BannerView _bannerView;
        private AdSize _adSize;
        private AdPosition _adPosition;
        
        private string _adUnitIdBanner;

        public event Action BannerOnAdDisplayed;
        public event Action BannerOnAdFailedDisplayed;
        public event Action BannerOnAdClicked;
        public event Action BannerOnAdImpressionRecorded;
        public event Action BannerOnAdClosed;
        public event Action<AdValue> AdmobOnAdRevenuePaid;

        public void SetAdUnitId(string adUnitId)
        {
            if(adUnitId == null)
            {
                _log.Error("Ad unit ID Banner is empty.");
                return;
            }

            _adUnitIdBanner = adUnitId;

            _log.Debug("Ad unit ID Banner set to : " + _adUnitIdBanner);
        }

        /// <summary>
        /// Creates a 320x50 banner view at top of the screen.
        /// </summary>
        public void CreateBannerView(AdSize adSize, AdPosition adPosition)
        {
            if(_adUnitIdBanner == null)
            {
                _log.Error("Ad unit ID Banner is empty.");
                return;
            }

            _adSize = adSize;
            _adPosition = adPosition;

            _log.Debug("Creating banner view");

            // If we already have a banner, destroy the old one.
            CleanupAd();

            // Create a 320x50 banner at top of the screen
            _bannerView = new BannerView(_adUnitIdBanner, adSize, adPosition);
        }

        /// <summary>
        /// Creates the banner view and loads a banner ad.
        /// </summary>
        public void LoadAd()
        {
            if(_adUnitIdBanner == null)
            {
                _log.Error("Ad unit ID Banner is empty.");
                return;
            }
            // create an instance of a banner view first.
            if(_bannerView == null)
            {
                CreateBannerView(adSize: _adSize, adPosition: _adPosition);
            }

            // create our request used to load the ad.
            var adRequest = new AdRequest();

            // send the request to load the ad.
            _log.Debug("Loading banner ad.");
            _bannerView.LoadAd(adRequest);

            ListenToAdEvents();
        }

        /// <summary>
        /// listen to events the banner view may raise.
        /// </summary>
        private void ListenToAdEvents()
        {
            // Raised when an ad is loaded into the banner view.
            _bannerView.OnBannerAdLoaded += () =>
            {
                _log.Debug("Banner view loaded an ad with response : "
                    + _bannerView.GetResponseInfo());
                
                BannerOnAdDisplayed?.Invoke();
            };
            // Raised when an ad fails to load into the banner view.
            _bannerView.OnBannerAdLoadFailed += (LoadAdError error) =>
            {
                _log.Error("Banner view failed to load an ad with error : "
                    + error);
                
                BannerOnAdFailedDisplayed?.Invoke();
            };
            // Raised when the ad is estimated to have earned money.
            _bannerView.OnAdPaid += (AdValue adValue) =>
            {
                _log.Debug(String.Format("Banner view paid {0} {1}.",
                    adValue.Value,
                    adValue.CurrencyCode));
                
                AdmobOnAdRevenuePaid?.Invoke(adValue);
            };
            // Raised when an impression is recorded for an ad.
            _bannerView.OnAdImpressionRecorded += () =>
            {
                _log.Debug("Banner view recorded an impression.");

                BannerOnAdImpressionRecorded?.Invoke();
            };
            // Raised when a click is recorded for an ad.
            _bannerView.OnAdClicked += () =>
            {
                _log.Debug("Banner view was clicked.");

                BannerOnAdClicked?.Invoke();
            };
            // Raised when an ad opened full screen content.
            _bannerView.OnAdFullScreenContentOpened += () =>
            {
                _log.Debug("Banner view full screen content opened.");

                BannerOnAdDisplayed?.Invoke();
            };
            // Raised when the ad closed full screen content.
            _bannerView.OnAdFullScreenContentClosed += () =>
            {
                _log.Debug("Banner view full screen content closed.");

                BannerOnAdClosed?.Invoke();
            };
        }

        public void CleanupAd()
        {
            if (_bannerView != null)
            {
                _bannerView.Destroy();
                _bannerView = null;

                _log.Debug("Banner view cleaned up.");
            }
        }
    }
}

#endif