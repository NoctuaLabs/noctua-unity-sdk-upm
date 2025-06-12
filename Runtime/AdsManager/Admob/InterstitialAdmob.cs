#if UNITY_ADMOB
using GoogleMobileAds.Api;
using UnityEngine;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

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
        private readonly long _timeoutThreshold = 5000; // milliseconds,
        
        public void SetInterstitialAdUnitID(string adUnitID)
        {
            if (adUnitID == null)
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
            TrackAdCustomEventInterstitial("wf_interstitial_request_start");
            
            if (_adUnitIDInterstitial == null)
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

                    if (ad.GetResponseInfo() != null)
                    {
                        AdapterResponseInfo loadedAdapterResponseInfo = ad.GetResponseInfo().GetLoadedAdapterResponseInfo();

                        long latencyMillis = loadedAdapterResponseInfo?.LatencyMillis ?? 0;
                        
                        if (latencyMillis > _timeoutThreshold)
                        {
                            _log.Warning($"Interstitial ad request took too long: {latencyMillis} ms, exceeding threshold of {_timeoutThreshold} ms.");

                            TrackAdCustomEventInterstitial("wf_interstitial_request_adunit_timeout");
                        }
                    }

                    // if error is not null, the load request failed.
                    if (error != null || ad == null)
                    {
                        _log.Error("interstitial ad failed to load an ad " +
                                        "with error : " + error);

                        var extraPayload = new Dictionary<string, IConvertible>
                        {
                            { "error_code", error.GetCode() },
                            { "error_message", error.GetMessage() },
                            { "domain", error.GetDomain() },
                            { "ad_unit_id", _adUnitIDInterstitial ?? "unknown" }
                        };

                        TrackAdCustomEventInterstitial("wf_interstitial_request_adunit_failed", extraPayload);
                        TrackAdCustomEventInterstitial("wf_interstitial_request_finished_failed	", extraPayload);
                        return;
                    }

                    _log.Debug("Interstitial ad loaded with response : "
                                + ad.GetResponseInfo());

                    TrackAdCustomEventInterstitial("ad_loaded");
                    TrackAdCustomEventInterstitial("wf_interstitial_request_adunit_success");
                    TrackAdCustomEventInterstitial("wf_interstitial_request_finished_success");

                    _interstitialAd = ad;

                    RegisterEventHandlers(ad);
                });
        }

        /// <summary>
        /// Shows the interstitial ad.
        /// </summary>
        public void ShowInterstitialAd()
        {
            TrackAdCustomEventInterstitial("wf_interstitial_started_playing");

            if (_interstitialAd != null && _interstitialAd.CanShowAd())
            {
                _log.Debug("Showing interstitial ad.");
                _interstitialAd.Show();
            }
            else
            {
                TrackAdCustomEventInterstitial("wf_interstitial_show_not_ready");
                TrackAdCustomEventInterstitial("wf_interstitial_show_failed_null");

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
                TrackAdCustomEventInterstitial("ad_impression");
                TrackAdCustomEventInterstitial("ad_impression_interstitial");

                InterstitialOnAdImpressionRecorded?.Invoke();
            };
            // Raised when a click is recorded for an ad.
            interstitialAd.OnAdClicked += () =>
            {
                _log.Debug("Interstitial ad was clicked.");

                TrackAdCustomEventInterstitial("ad_clicked");
                TrackAdCustomEventInterstitial("wf_interstitial_clicked");

                InterstitialOnAdClicked?.Invoke();
            };
            // Raised when an ad opened full screen content.
            interstitialAd.OnAdFullScreenContentOpened += () =>
            {
                _log.Debug("Interstitial ad full screen content opened.");

                TrackAdCustomEventInterstitial("ad_shown");
                TrackAdCustomEventInterstitial("wf_interstitial_show_sdk");

                InterstitialOnAdDisplayed?.Invoke();
            };
            // Raised when the ad closed full screen content.
            interstitialAd.OnAdFullScreenContentClosed += () =>
            {
                _log.Debug("Interstitial ad full screen content closed.");

                LoadInterstitialAd();

                TrackAdCustomEventInterstitial("ad_closed");
                TrackAdCustomEventInterstitial("wf_interstitial_closed");

                InterstitialOnAdClosed?.Invoke();
            };
            // Raised when the ad failed to open full screen content.
            interstitialAd.OnAdFullScreenContentFailed += (AdError error) =>
            {
                _log.Error("Interstitial ad failed to open full screen content " +
                            "with error : " + error);

                LoadInterstitialAd();

                var extraPayload = new Dictionary<string, IConvertible>
                {
                    { "error_code", error.GetCode() },
                    { "error_message", error.GetMessage() },
                    { "domain", error.GetDomain() },
                    { "ad_unit_id", _adUnitIDInterstitial ?? "unknown" }
                };

                TrackAdCustomEventInterstitial("ad_show_failed", extraPayload);
                TrackAdCustomEventInterstitial("wf_interstitial_show_sdk_failed", extraPayload);

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
            try
            {
                _log.Debug("Tracking custom event for interstitial ad: " + eventName);

                extraPayload ??= new Dictionary<string, IConvertible>();

                // Add basic information that doesn't require the ad instance
                extraPayload.Add("ad_format", "interstitial");
                extraPayload.Add("mediation_service", "admob");
                
                // Only add ad-specific information if the ad instance exists
                if (_interstitialAd != null)
                {
                    var responseInfo = _interstitialAd.GetResponseInfo();
                    if (responseInfo != null)
                    {
                        AdapterResponseInfo loadedAdapterResponseInfo = responseInfo.GetLoadedAdapterResponseInfo();
                        if (loadedAdapterResponseInfo != null)
                        {
                            string adSourceName = "empty";
                            string adapterClassName = "empty";
                            long latencyMillis = 0;

                            try { adSourceName = loadedAdapterResponseInfo.AdSourceName ?? "empty"; } catch {}
                            try { adapterClassName = loadedAdapterResponseInfo.AdapterClassName ?? "empty"; } catch {}
                            try { latencyMillis = loadedAdapterResponseInfo.LatencyMillis; } catch {}

                            extraPayload.Add("ad_network", adSourceName);
                            extraPayload.Add("ntw", adapterClassName);
                            extraPayload.Add("latency_millis", latencyMillis);
                        }
                        else
                        {
                            extraPayload.Add("ad_network", "unknown");
                        }
                    }
                    else
                    {
                        extraPayload.Add("ad_network", "unknown");
                    }

                    extraPayload.Add("ad_unit_id", _interstitialAd.GetAdUnitID());
                }
                else
                {
                    extraPayload.Add("ad_network", "unknown");
                    extraPayload.Add("ad_unit_id", _adUnitIDInterstitial ?? "unknown");
                }

                string properties = "";
                foreach (var (key, value) in extraPayload)
                {
                    properties += $"{key}={value}, ";
                }

                _log.Debug($"Event name: {eventName}, Event properties: {properties}");
            
                Noctua.Event.TrackCustomEvent(eventName, extraPayload);
            }
            catch (Exception ex)
            {
                _log.Error($"Error tracking interstitial ad event '{eventName}': {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}

#endif