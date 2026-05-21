#if UNITY_ADMOB
using Cysharp.Threading.Tasks;
using GoogleMobileAds.Api;
using System;

namespace com.noctuagames.sdk.Admob
{
    /// <summary>
    /// Shared helper for dispatching AdMob ad-revenue tracking to the main thread.
    /// All five AdMob ad-format classes (Interstitial, Rewarded, RewardedInterstitial,
    /// Banner, AppOpen) perform the identical UniTask.Void / SwitchToMainThread /
    /// TrackAdRevenue sequence; this utility eliminates that duplication.
    /// </summary>
    public static class AdmobRevenueHelper
    {
        /// <summary>
        /// Switches to the Unity main thread and calls
        /// <see cref="Noctua.Event.TrackAdRevenue"/> with the supplied AdMob ad value.
        /// Revenue is converted from micros (<c>adValue.Value / 1_000_000.0</c>) inside
        /// the helper. Any exception is caught and logged so the caller's flow is never
        /// interrupted.
        /// </summary>
        /// <param name="adValue">AdMob <see cref="AdValue"/> from the OnAdPaid callback.</param>
        /// <param name="responseInfo">Ad response info captured before the async hop.</param>
        /// <param name="impressionId">Impression ID already emitted in the canonical ad_impression event.</param>
        /// <param name="deviceId">Cached <c>SystemInfo.deviceUniqueIdentifier</c> (must be read on main thread beforehand).</param>
        /// <param name="log">Caller's logger instance, used for error reporting.</param>
        /// <param name="adFormat">Ad format label for the error message; use an <see cref="AdFormatKey"/> constant.</param>
        public static void TrackRevenueOnMainThread(
            AdValue adValue,
            ResponseInfo responseInfo,
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
                    var revenue    = adValue.Value / 1_000_000.0;
                    var revPayload = IAAPayloadBuilder.BuildAdmobRevenuePayload(adValue, responseInfo, deviceId);
                    revPayload["sdk_impression_id"] = impressionId;
                    revPayload["sdk_revenue_id"]    = Guid.NewGuid().ToString("N");
                    Noctua.Event.TrackAdRevenue("admob_sdk", revenue, adValue.CurrencyCode, revPayload);
                }
                catch (Exception ex)
                {
                    log.Error($"Error tracking AdMob {adFormat} revenue: {ex.Message}\n{ex.StackTrace}");
                }
            });
        }
    }
}
#endif
