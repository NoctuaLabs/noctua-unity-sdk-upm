#if UNITY_APPLOVIN
using Cysharp.Threading.Tasks;
using System;

namespace com.noctuagames.sdk.AppLovin
{
    /// <summary>
    /// Shared helper for dispatching AppLovin ad-revenue tracking to the main thread.
    /// All four AppLovin ad-format classes (Interstitial, Rewarded, Banner, AppOpen)
    /// perform the identical UniTask.Void / SwitchToMainThread / TrackAdRevenue sequence;
    /// this utility eliminates that duplication.
    /// </summary>
    public static class AppLovinRevenueHelper
    {
        /// <summary>
        /// Switches to the Unity main thread and calls
        /// <see cref="Noctua.Event.TrackAdRevenue"/> with the supplied ad info.
        /// Any exception is caught and logged via <paramref name="log"/> so the
        /// caller's flow is never interrupted.
        /// </summary>
        /// <param name="adInfo">AppLovin ad-info object from the revenue-paid callback.</param>
        /// <param name="revenue">Revenue value in USD.</param>
        /// <param name="impressionId">Impression ID already emitted in the canonical ad_impression event.</param>
        /// <param name="deviceId">Cached <c>SystemInfo.deviceUniqueIdentifier</c> (must be read on main thread beforehand).</param>
        /// <param name="log">Caller's logger instance, used for error reporting.</param>
        /// <param name="adFormat">Ad format label for the error message; use an <see cref="AdFormatKey"/> constant.</param>
        public static void TrackRevenueOnMainThread(
            MaxSdkBase.AdInfo adInfo,
            double revenue,
            string impressionId,
            string deviceId,
            ILogger log,
            string adFormat)
        {
            UniTask.Void(async () =>
            {
                await UniTask.SwitchToMainThread();
                try
                {
                    var countryCode = MaxSdk.GetSdkConfiguration().CountryCode;
                    var revPayload = IAAPayloadBuilder.BuildAppLovinRevenuePayload(adInfo, deviceId, countryCode);
                    revPayload["sdk_impression_id"] = impressionId;
                    revPayload["sdk_revenue_id"]    = Guid.NewGuid().ToString("N");
                    Noctua.Event.TrackAdRevenue("applovin_max_sdk", revenue, "USD", revPayload);
                }
                catch (Exception ex)
                {
                    log.Error($"Error tracking AppLovin {adFormat} revenue: {ex.Message}\n{ex.StackTrace}");
                }
            });
        }
    }
}
#endif
