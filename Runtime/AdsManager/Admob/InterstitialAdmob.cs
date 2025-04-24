#if UNITY_ADMOB
using GoogleMobileAds.Api;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace com.noctuagames.sdk.Admob
{
    public class InterstitialAdmob 
    {
        private readonly NoctuaLogger _log = new(typeof(InterstitialAdmob));
        private string _adUnitIDInterstitial;

        //public event handler
        public event Action InterstitialOnAdDisplayed;
        public event Action InterstitialOnAdFailedDisplayed;
        public event Action InterstitialOnAdClicked;
        public event Action InterstitialOnAdImpressionRecorded;
        public event Action InterstitialOnAdClosed;
        public event Action<AdValue, ResponseInfo> AdmobOnAdRevenuePaid;

        private InterstitialAd _interstitialAd;

        public void SetInterstitialAdUnitID(string adUnitID)
        {
            if(adUnitID == null)
            {
                _log.Error("Ad unit ID Interstitial is empty.");
                return;
            }

            _adUnitIDInterstitial = adUnitID;

            _log.Debug("Ad unit ID Interstitial set to : " + _adUnitIDInterstitial);
        }

        /// <summary>
        /// Loads the interstitial ad.
        /// </summary>
        public void LoadInterstitialAd()
        {
            if(_adUnitIDInterstitial == null)
            {
                _log.Error("Ad unit ID Interstitial is empty.");
                return;
            }

            // Clean up the old ad before loading a new one.
            CleanupAd();

            _log.Debug("Loading the interstitial ad.");

            // create our request used to load the ad.
            var adRequest = new AdRequest();

            // send the request to load the ad.
            InterstitialAd.Load(_adUnitIDInterstitial, adRequest,
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

                    TrackAdCustomEventInterstitial("ad_loaded");

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

                AdmobOnAdRevenuePaid?.Invoke(adValue, interstitialAd.GetResponseInfo());
                
            };
            // Raised when an impression is recorded for an ad.
            interstitialAd.OnAdImpressionRecorded += () =>
            {
                _log.Debug("Interstitial ad recorded an impression.");

                InterstitialOnAdImpressionRecorded?.Invoke();
            };
            // Raised when a click is recorded for an ad.
            interstitialAd.OnAdClicked += () =>
            {
                _log.Debug("Interstitial ad was clicked.");

                TrackAdCustomEventInterstitial("ad_clicked");

                InterstitialOnAdClicked?.Invoke();
            };
            // Raised when an ad opened full screen content.
            interstitialAd.OnAdFullScreenContentOpened += () =>
            {
                _log.Debug("Interstitial ad full screen content opened.");

                TrackAdCustomEventInterstitial("ad_shown");

                InterstitialOnAdDisplayed?.Invoke();
            };
            // Raised when the ad closed full screen content.
            interstitialAd.OnAdFullScreenContentClosed += () =>
            {
                _log.Debug("Interstitial ad full screen content closed.");

                LoadInterstitialAd();

                TrackAdCustomEventInterstitial("ad_closed");

                InterstitialOnAdClosed?.Invoke();
            };
            // Raised when the ad failed to open full screen content.
            interstitialAd.OnAdFullScreenContentFailed += (AdError error) =>
            {
                _log.Error("Interstitial ad failed to open full screen content " +
                            "with error : " + error);

                LoadInterstitialAd();

                TrackAdCustomEventInterstitial("ad_show_error", new Dictionary<string, IConvertible>()
                {
                    { "error_code", error.GetCode() },
                    { "error_message", error.GetMessage() },
                    { "domain", error.GetDomain() }
                });

                InterstitialOnAdFailedDisplayed?.Invoke();
            };
        }

        private void CleanupAd()
        {
            if (_interstitialAd != null)
            {
                _interstitialAd.Destroy();
                _interstitialAd = null;

                _log.Debug("Interstitial ad cleaned up.");
            }
        }

        private void TrackAdCustomEventInterstitial(string eventName, Dictionary<string, IConvertible> extraPayload = null)
        {
            _log.Debug("Tracking custom event for interstitial ad: " + eventName);

            extraPayload ??= new Dictionary<string, IConvertible>();

            AdapterResponseInfo loadedAdapterResponseInfo = _interstitialAd.GetResponseInfo().GetLoadedAdapterResponseInfo();
            string adSourceName = loadedAdapterResponseInfo?.AdSourceName ?? "empty";

            extraPayload.Add("ad_unit_id", _interstitialAd.GetAdUnitID());
            extraPayload.Add("ad_format", "interstitial");
            extraPayload.Add("ad_network", adSourceName);
            extraPayload.Add("mediation_service", "admob");
            extraPayload.Add("sdk_version", MobileAds.GetVersion().ToString());

            string properties = "";
            foreach (var (key, value) in extraPayload)
            {
                properties += $"{key}={value}, ";
            }

            _log.Debug($"Event name: {eventName}, Event properties: {properties}");
        
            Noctua.Event.TrackCustomEvent(eventName, extraPayload);
        }
    }
}

#endif