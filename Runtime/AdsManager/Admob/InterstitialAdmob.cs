#if UNITY_ADMOB
using GoogleMobileAds.Api;
using UnityEngine;
using System;

namespace com.noctuagames.sdk.Admob
{
    public class InterstitialAdmob 
    {
        private readonly NoctuaLogger _log = new(typeof(InterstitialAdmob));
        private string _adUnitIDInterstitial;

        public event Action InterstitialOnAdClicked;
        public event Action InterstitialOnAdFullScreenContentOpened;
        public event Action InterstitialOnAdFullScreenContentClosed;

        private InterstitialAd _interstitialAd;

        /// <summary>
        /// Loads the interstitial ad.
        /// </summary>
        public void LoadInterstitialAd(string adUnitID)
        {
            if(adUnitID == null)
            {
                _log.Error("Ad unit ID Interstitial is empty.");
                return;
            }

            _adUnitIDInterstitial = adUnitID;
            // Clean up the old ad before loading a new one.
            CleanupAd();

            _log.Debug("Loading the interstitial ad.");

            // create our request used to load the ad.
            var adRequest = new AdRequest();

            // send the request to load the ad.
            InterstitialAd.Load(adUnitID, adRequest,
                (InterstitialAd ad, LoadAdError error) =>
                {
                    // if error is not null, the load request failed.
                    if (error != null || ad == null)
                    {
                        _log.Error("interstitial ad failed to load an ad " +
                                        "with error : " + error);
                        return;
                    }

                    _log.Debug("Interstitial ad loaded with response : "
                                + ad.GetResponseInfo());

                    _interstitialAd = ad;

                    RegisterEventHandlers(ad);
                });
        }

        /// <summary>
        /// Shows the interstitial ad.
        /// </summary>
        public void ShowInterstitialAd()
        {

            if (_interstitialAd != null && _interstitialAd.CanShowAd())
            {
                _log.Debug("Showing interstitial ad.");
                _interstitialAd.Show();
            }
            else
            {
                _log.Error("Interstitial ad is not ready yet.");
            }
        }

        private void RegisterEventHandlers(InterstitialAd interstitialAd)
        {
            // Raised when the ad is estimated to have earned money.
            interstitialAd.OnAdPaid += (AdValue adValue) =>
            {
                _log.Debug(String.Format("Interstitial ad paid {0} {1}.",
                    adValue.Value,
                    adValue.CurrencyCode));
            };
            // Raised when an impression is recorded for an ad.
            interstitialAd.OnAdImpressionRecorded += () =>
            {
                _log.Debug("Interstitial ad recorded an impression.");
            };
            // Raised when a click is recorded for an ad.
            interstitialAd.OnAdClicked += () =>
            {
                _log.Debug("Interstitial ad was clicked.");

                InterstitialOnAdClicked?.Invoke();
            };
            // Raised when an ad opened full screen content.
            interstitialAd.OnAdFullScreenContentOpened += () =>
            {
                _log.Debug("Interstitial ad full screen content opened.");

                InterstitialOnAdFullScreenContentOpened?.Invoke();
            };
            // Raised when the ad closed full screen content.
            interstitialAd.OnAdFullScreenContentClosed += () =>
            {
                _log.Debug("Interstitial ad full screen content closed.");

                LoadInterstitialAd(_adUnitIDInterstitial);

                InterstitialOnAdFullScreenContentClosed?.Invoke();
            };
            // Raised when the ad failed to open full screen content.
            interstitialAd.OnAdFullScreenContentFailed += (AdError error) =>
            {
                _log.Error("Interstitial ad failed to open full screen content " +
                            "with error : " + error);

                LoadInterstitialAd(_adUnitIDInterstitial);

                // InterstitialOnAdFullScreenContentFailed?.Invoke(error);
            };
        }

        private void CleanupAd()
        {
            if (_interstitialAd != null)
            {
                _interstitialAd.Destroy();
                _interstitialAd = null;

                _log.Debug("Interstitial ad cleaned up.");

                InterstitialOnAdClicked = null;
                InterstitialOnAdFullScreenContentOpened = null;
                InterstitialOnAdFullScreenContentClosed = null;
            }
        }
    }
}

#endif