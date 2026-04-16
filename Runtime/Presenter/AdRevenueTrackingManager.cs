using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_ADMOB
using GoogleMobileAds.Api;
using static GoogleMobileAds.Api.AdValue;
#endif

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Handles ad revenue tracking and Taichi tROAS threshold processing.
    /// Extracted from MediationManager to reduce its size and isolate revenue concerns.
    ///
    /// Taichi Steps implemented:
    ///   Step 1: Total_Ads_Revenue_001    — cumulative total revenue &gt;= taichi.revenue_threshold
    ///   Step 2: TenAdsShown             — cumulative total impressions &gt;= taichi.ad_count_threshold
    ///   Step 3: taichi_total_ad_impression   — interstitial+rewarded combined &gt;= taichi.total_impression_threshold
    ///   Step 4: taichi_interstitial_ad_impression — interstitial only &gt;= taichi.interstitial_count_threshold
    ///   Step 5: taichi_rewarded_ad_impression    — rewarded only &gt;= taichi.rewarded_count_threshold
    ///   Step 6: taichi_rewarded_ad_revenue       — rewarded-only revenue &gt;= taichi.rewarded_revenue_threshold
    /// </summary>
    public class AdRevenueTrackingManager
    {
        private readonly NoctuaLogger _log = new(typeof(AdRevenueTrackingManager));

        private IAdRevenueTracker _adRevenueTracker;
        private TaichiConfig _taichiConfig;

        // Cached on the main thread at construction time — SystemInfo.deviceUniqueIdentifier
        // cannot be called from background threads (AdMob revenue callbacks fire from JNI thread).
        private readonly string _deviceId;

        // Counts events dropped because _adRevenueTracker was null — visible in logs as [REVENUE LOST #N].
        private int _droppedEventCount;

        // PlayerPrefs keys — prefixed to avoid collisions
        private const string KeyTotalRevenue          = "Noctua_Taichi_TotalRevenue";
        private const string KeyTotalAdCount          = "Noctua_Taichi_TotalAdCount";
        private const string KeyTotalImpressions      = "Noctua_Taichi_TotalImpressions";
        private const string KeyInterstitialCount     = "Noctua_Taichi_InterstitialCount";
        private const string KeyRewardedCount         = "Noctua_Taichi_RewardedCount";
        private const string KeyRewardedRevenue       = "Noctua_Taichi_RewardedRevenue";

        /// <summary>
        /// Creates a new AdRevenueTrackingManager.
        /// </summary>
        /// <param name="adRevenueTracker">The tracker implementation for sending revenue events.</param>
        /// <param name="taichiConfig">
        /// Taichi threshold configuration. Pass <c>null</c> to disable Taichi tracking entirely.
        /// </param>
        public AdRevenueTrackingManager(IAdRevenueTracker adRevenueTracker = null, TaichiConfig taichiConfig = null)
        {
            _adRevenueTracker = adRevenueTracker;
            _taichiConfig = taichiConfig;
            _deviceId = SystemInfo.deviceUniqueIdentifier;

            if (_adRevenueTracker == null)
                _log.Warning("AdRevenueTrackingManager created with null tracker — ad revenue will not be tracked until SetAdRevenueTracker() is called");
            else
                _log.Info("AdRevenueTrackingManager created with tracker wired");

            if (_taichiConfig == null)
                _log.Info("Taichi config is null — Taichi threshold tracking disabled");
            else
                _log.Info($"Taichi config: revenueThreshold={_taichiConfig.RevenueThreshold} adCount={_taichiConfig.AdCountThreshold} totalImpressions={_taichiConfig.TotalImpressionThreshold}");
        }

        /// <summary>
        /// Sets or replaces the ad revenue tracker implementation.
        /// </summary>
        public void SetAdRevenueTracker(IAdRevenueTracker tracker)
        {
            bool wasNull = _adRevenueTracker == null;
            bool isNull  = tracker == null;

            if (isNull)
                _log.Warning("SetAdRevenueTracker(null) called — future ad revenue will not be tracked");
            else if (wasNull)
                _log.Info("SetAdRevenueTracker: tracker successfully wired (was previously null)");
            else
                _log.Info("SetAdRevenueTracker: tracker replaced");

            _adRevenueTracker = tracker;
        }

        /// <summary>
        /// Updates the Taichi configuration (e.g. after remote config refresh).
        /// Pass <c>null</c> to disable Taichi tracking.
        /// </summary>
        public void SetTaichiConfig(TaichiConfig config)
        {
            _taichiConfig = config;
        }

#if UNITY_ADMOB
        /// <summary>
        /// Processes AdMob banner ad revenue. Only total revenue / ad count thresholds apply.
        /// </summary>
        public void ProcessAdmobRevenue(AdValue adValue, ResponseInfo responseInfo)
        {
            double revenue = TrackAdmobRevenue(adValue, responseInfo);
            ProcessAllFormatsThresholds(revenue);
        }

        /// <summary>
        /// Processes AdMob interstitial ad revenue and fires interstitial-specific Taichi steps.
        /// </summary>
        public void ProcessAdmobInterstitialRevenue(AdValue adValue, ResponseInfo responseInfo)
        {
            double revenue = TrackAdmobRevenue(adValue, responseInfo);
            ProcessAllFormatsThresholds(revenue);
            ProcessInterstitialThresholds(revenue);
        }

        /// <summary>
        /// Processes AdMob rewarded ad revenue and fires rewarded-specific Taichi steps.
        /// </summary>
        public void ProcessAdmobRewardedRevenue(AdValue adValue, ResponseInfo responseInfo)
        {
            double revenue = TrackAdmobRevenue(adValue, responseInfo);
            ProcessAllFormatsThresholds(revenue);
            ProcessRewardedThresholds(revenue);
        }

        private double TrackAdmobRevenue(AdValue adValue, ResponseInfo responseInfo)
        {
            long valueMicros = adValue.Value;
            string currencyCode = adValue.CurrencyCode;
            PrecisionType precision = adValue.Precision;

            string responseId = responseInfo?.GetResponseId() ?? "empty";
            AdapterResponseInfo loadedAdapterResponseInfo = responseInfo?.GetLoadedAdapterResponseInfo();

            string adSourceId           = loadedAdapterResponseInfo?.AdSourceId ?? "empty";
            string adSourceInstanceId   = loadedAdapterResponseInfo?.AdSourceInstanceId ?? "empty";
            string adSourceInstanceName = loadedAdapterResponseInfo?.AdSourceInstanceName ?? "empty";
            string adSourceName         = loadedAdapterResponseInfo?.AdSourceName ?? "empty";
            string adapterClassName     = loadedAdapterResponseInfo?.AdapterClassName ?? "empty";
            long latencyMillis          = loadedAdapterResponseInfo?.LatencyMillis ?? 0;

            Dictionary<string, string> extras = responseInfo?.GetResponseExtras();
            string mediationGroupName     = extras != null && extras.ContainsKey("mediation_group_name")     ? extras["mediation_group_name"]     : "empty";
            string mediationABTestName    = extras != null && extras.ContainsKey("mediation_ab_test_name")    ? extras["mediation_ab_test_name"]    : "empty";
            string mediationABTestVariant = extras != null && extras.ContainsKey("mediation_ab_test_variant") ? extras["mediation_ab_test_variant"] : "empty";

            double revenue = valueMicros / 1_000_000.0;

            _log.Debug($"Admob Ad Revenue: value micros: {adValue.Value} / converted: {revenue}, {currencyCode} " +
                $"Precision: {precision} Ad Source: {adSourceName}, Adapter: {adapterClassName}");

            if (_adRevenueTracker == null)
            {
                _droppedEventCount++;
                _log.Error($"[REVENUE LOST #{_droppedEventCount}] AdRevenueTracker is null — dropping AdMob revenue: {revenue} {currencyCode} " +
                    $"(source: {adSourceName}, adapter: {adapterClassName}). Call SetAdRevenueTracker() before ads are shown.");
            }
            else
            {
                _log.Info($"Tracking AdMob revenue: {revenue:F6} {currencyCode} source={adSourceName} adapter={adapterClassName}");
                _adRevenueTracker.TrackAdRevenue("admob_sdk", revenue, currencyCode, new Dictionary<string, IConvertible>
                {
                    { "ad_source_id",             adSourceId },
                    { "ad_source_instance_id",    adSourceInstanceId },
                    { "ad_source_instance_name",  adSourceInstanceName },
                    { "ad_source_name",           adSourceName },
                    { "adapter_class_name",       adapterClassName },
                    { "latency_millis",           latencyMillis },
                    { "response_id",              responseId },
                    { "mediation_group_name",     mediationGroupName },
                    { "mediation_ab_test_name",   mediationABTestName },
                    { "mediation_ab_test_variant",mediationABTestVariant },
                    { "ad_user_id",               _deviceId }
                });
            }

            return revenue;
        }
#endif

#if UNITY_APPLOVIN
        /// <summary>
        /// Processes AppLovin ad revenue, auto-routing to format-specific Taichi steps
        /// based on <c>adInfo.AdFormat</c>.
        /// </summary>
        public void ProcessAppLovinRevenue(MaxSdkBase.AdInfo adInfo)
        {
            double revenue = TrackAppLovinRevenue(adInfo);
            ProcessAllFormatsThresholds(revenue);

            string format = adInfo.AdFormat ?? "";

            if (IsInterstitialFormat(format))
            {
                ProcessInterstitialThresholds(revenue);
            }
            else if (IsRewardedFormat(format))
            {
                ProcessRewardedThresholds(revenue);
            }
        }

        private double TrackAppLovinRevenue(MaxSdkBase.AdInfo adInfo)
        {
            double revenue = adInfo.Revenue;

            string countryCode       = MaxSdk.GetSdkConfiguration().CountryCode;
            string networkName       = adInfo.NetworkName;
            string adUnitIdentifier  = adInfo.AdUnitIdentifier;
            string placement         = adInfo.Placement;
            string networkPlacement  = adInfo.NetworkPlacement;
            string revenuePrecision  = adInfo.RevenuePrecision;
            string adFormat          = adInfo.AdFormat;
            string dspName           = adInfo.DspName ?? "";

            _log.Debug($"AppLovin Ad Revenue: revenue: {revenue}, USD, " +
                $"country: {countryCode}, network: {networkName}, format: {adFormat}, " +
                $"ad unit: {adUnitIdentifier}, placement: {placement}");

            if (_adRevenueTracker == null)
            {
                _droppedEventCount++;
                _log.Error($"[REVENUE LOST #{_droppedEventCount}] AdRevenueTracker is null — dropping AppLovin revenue: {revenue:F6} USD " +
                    $"(network: {networkName}, format: {adFormat}, placement: {placement}). Call SetAdRevenueTracker() before ads are shown.");
            }
            else
            {
                _log.Info($"Tracking AppLovin revenue: {revenue:F6} USD network={networkName} format={adFormat} placement={placement}");
                _adRevenueTracker.TrackAdRevenue("applovin_max_sdk", revenue, "USD", new Dictionary<string, IConvertible>
                {
                    { "country_code",      countryCode },
                    { "network_name",      networkName },
                    { "ad_unit_identifier",adUnitIdentifier },
                    { "placement",         placement },
                    { "network_placement", networkPlacement },
                    { "revenue_precision", revenuePrecision },
                    { "ad_format",         adFormat },
                    { "dsp_name",          dspName },
                    { "ad_user_id",        _deviceId }
                });
            }

            return revenue;
        }

        private static bool IsInterstitialFormat(string adFormat)
        {
            string fmt = adFormat.ToUpperInvariant();
            return fmt == "INTER" || fmt == "INTERSTITIAL";
        }

        private static bool IsRewardedFormat(string adFormat)
        {
            string fmt = adFormat.ToUpperInvariant();
            return fmt == "REWARDED" || fmt == "REWARDED_VIDEO" || fmt == "REWARDEDVIDEO";
        }
#endif

        // ─────────────────────────────────────────────────────────────────────
        // Taichi threshold processing
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Step 1 + Step 2: Total revenue threshold and total ad count threshold.
        /// Call this for every ad impression regardless of format.
        /// </summary>
        public void ProcessAllFormatsThresholds(double impressionRevenue)
        {
            if (_taichiConfig == null)
            {
                _log.Info("Taichi: ProcessAllFormatsThresholds skipped — TaichiConfig is null");
                return;
            }

            float currentRevenue = PlayerPrefs.GetFloat(KeyTotalRevenue, 0f);
            int   currentCount   = PlayerPrefs.GetInt(KeyTotalAdCount, 0);
            _log.Info($"Taichi: ProcessAllFormatsThresholds called — impressionRevenue={impressionRevenue:F6} USD | " +
                $"Step 1 revenue {currentRevenue:F4}/{_taichiConfig.RevenueThreshold} | " +
                $"Step 2 count {currentCount}/{_taichiConfig.AdCountThreshold}");

            // Step 1: Total_Ads_Revenue_001
            float prevRevenue     = currentRevenue;
            float updatedRevenue  = prevRevenue + (float)impressionRevenue;

            if (updatedRevenue >= _taichiConfig.RevenueThreshold)
            {
                _log.Info($"Taichi Step 1: Total_Ads_Revenue_001 crossed ({updatedRevenue:F4} >= {_taichiConfig.RevenueThreshold})");
                _adRevenueTracker?.TrackCustomEvent("Total_Ads_Revenue_001", new Dictionary<string, IConvertible>
                {
                    { "value",    (double)updatedRevenue },
                    { "currency", "USD" }
                });
                PlayerPrefs.SetFloat(KeyTotalRevenue, 0f);
            }
            else
            {
                PlayerPrefs.SetFloat(KeyTotalRevenue, updatedRevenue);
                _log.Info($"Taichi Step 1: Total_Ads_Revenue_001 progress {updatedRevenue:F4}/{_taichiConfig.RevenueThreshold} USD (not fired)");
            }

            // Step 2: TenAdsShown
            int prevCount    = currentCount;
            int updatedCount = prevCount + 1;

            if (updatedCount >= _taichiConfig.AdCountThreshold)
            {
                _log.Info($"Taichi Step 2: TenAdsShown crossed ({updatedCount} >= {_taichiConfig.AdCountThreshold})");
                _adRevenueTracker?.TrackCustomEvent("TenAdsShown", new Dictionary<string, IConvertible>
                {
                    { "value",    (double)updatedRevenue },
                    { "currency", "USD" }
                });
                PlayerPrefs.SetInt(KeyTotalAdCount, 0);
            }
            else
            {
                PlayerPrefs.SetInt(KeyTotalAdCount, updatedCount);
                _log.Info($"Taichi Step 2: TenAdsShown progress {updatedCount}/{_taichiConfig.AdCountThreshold} (not fired)");
            }

            PlayerPrefs.Save();
        }

        /// <summary>
        /// Step 3 + Step 4: Combined interstitial+rewarded total, and interstitial-only count.
        /// Call this only for interstitial impressions.
        /// </summary>
        public void ProcessInterstitialThresholds(double impressionRevenue)
        {
            if (_taichiConfig == null)
            {
                _log.Info("Taichi: ProcessInterstitialThresholds skipped — TaichiConfig is null");
                return;
            }

            int currentTotal        = PlayerPrefs.GetInt(KeyTotalImpressions, 0);
            int currentInterstitial = PlayerPrefs.GetInt(KeyInterstitialCount, 0);
            _log.Info($"Taichi: ProcessInterstitialThresholds called — impressionRevenue={impressionRevenue:F6} USD | " +
                $"Step 3 total {currentTotal}/{_taichiConfig.TotalImpressionThreshold} | " +
                $"Step 4 interstitial {currentInterstitial}/{_taichiConfig.InterstitialCountThreshold}");

            // Step 3: taichi_total_ad_impression (shared interstitial+rewarded counter)
            IncrementAndFireIfReady(
                KeyTotalImpressions,
                _taichiConfig.TotalImpressionThreshold,
                "taichi_total_ad_impression",
                impressionRevenue,
                "Taichi Step 3"
            );

            // Step 4: taichi_interstitial_ad_impression
            IncrementAndFireIfReady(
                KeyInterstitialCount,
                _taichiConfig.InterstitialCountThreshold,
                "taichi_interstitial_ad_impression",
                impressionRevenue,
                "Taichi Step 4"
            );

            PlayerPrefs.Save();
        }

        /// <summary>
        /// Step 3 + Step 5 + Step 6: Combined total, rewarded-only count, and rewarded-only revenue.
        /// Call this only for rewarded impressions.
        /// </summary>
        public void ProcessRewardedThresholds(double impressionRevenue)
        {
            if (_taichiConfig == null)
            {
                _log.Info("Taichi: ProcessRewardedThresholds skipped — TaichiConfig is null");
                return;
            }

            int   currentTotal           = PlayerPrefs.GetInt(KeyTotalImpressions, 0);
            int   currentRewarded        = PlayerPrefs.GetInt(KeyRewardedCount, 0);
            float currentRewardedRevenue = PlayerPrefs.GetFloat(KeyRewardedRevenue, 0f);
            _log.Info($"Taichi: ProcessRewardedThresholds called — impressionRevenue={impressionRevenue:F6} USD | " +
                $"Step 3 total {currentTotal}/{_taichiConfig.TotalImpressionThreshold} | " +
                $"Step 5 rewarded {currentRewarded}/{_taichiConfig.RewardedCountThreshold} | " +
                $"Step 6 revenue {currentRewardedRevenue:F4}/{_taichiConfig.RewardedRevenueThreshold}");

            // Step 3: taichi_total_ad_impression (shared interstitial+rewarded counter)
            IncrementAndFireIfReady(
                KeyTotalImpressions,
                _taichiConfig.TotalImpressionThreshold,
                "taichi_total_ad_impression",
                impressionRevenue,
                "Taichi Step 3"
            );

            // Step 5: taichi_rewarded_ad_impression
            IncrementAndFireIfReady(
                KeyRewardedCount,
                _taichiConfig.RewardedCountThreshold,
                "taichi_rewarded_ad_impression",
                impressionRevenue,
                "Taichi Step 5"
            );

            // Step 6: taichi_rewarded_ad_revenue
            float prevRewardedRevenue    = currentRewardedRevenue;
            float updatedRewardedRevenue = prevRewardedRevenue + (float)impressionRevenue;

            if (updatedRewardedRevenue >= _taichiConfig.RewardedRevenueThreshold)
            {
                _log.Info($"Taichi Step 6: taichi_rewarded_ad_revenue crossed ({updatedRewardedRevenue:F4} >= {_taichiConfig.RewardedRevenueThreshold})");
                _adRevenueTracker?.TrackCustomEvent("taichi_rewarded_ad_revenue", new Dictionary<string, IConvertible>
                {
                    { "value",    (double)updatedRewardedRevenue },
                    { "currency", "USD" }
                });
                PlayerPrefs.SetFloat(KeyRewardedRevenue, 0f);
            }
            else
            {
                PlayerPrefs.SetFloat(KeyRewardedRevenue, updatedRewardedRevenue);
                _log.Info($"Taichi Step 6: taichi_rewarded_ad_revenue progress {updatedRewardedRevenue:F4}/{_taichiConfig.RewardedRevenueThreshold} USD (not fired)");
            }

            PlayerPrefs.Save();
        }

        private void IncrementAndFireIfReady(string key, int threshold, string eventName, double revenue, string logLabel)
        {
            int prev    = PlayerPrefs.GetInt(key, 0);
            int updated = prev + 1;

            if (updated >= threshold)
            {
                _log.Info($"{logLabel}: {eventName} crossed ({updated} >= {threshold})");
                _adRevenueTracker?.TrackCustomEvent(eventName, new Dictionary<string, IConvertible>
                {
                    { "value",    revenue },
                    { "currency", "USD" }
                });
                PlayerPrefs.SetInt(key, 0);
            }
            else
            {
                PlayerPrefs.SetInt(key, updated);
                _log.Info($"{logLabel}: {eventName} progress {updated}/{threshold} (not fired)");
            }
        }
    }
}
