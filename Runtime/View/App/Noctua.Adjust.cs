using System;
using System.Threading.Tasks;

namespace com.noctuagames.sdk
{
    public partial class Noctua
    {
        /// <summary>
        /// Gets the Adjust attribution data asynchronously.
        /// </summary>
        /// <returns>Attribution data, or a default instance when not available.</returns>
        public static Task<NoctuaAdjustAttribution> GetAdjustAttributionAsync()
        {
        #if UNITY_ANDROID || UNITY_IOS
            var tcs = new TaskCompletionSource<NoctuaAdjustAttribution>();

            try
            {
                if (Instance.Value._nativePlugin != null)
                {
                    Instance.Value._nativePlugin.GetAdjustAttribution((result) =>
                    {
                        tcs.TrySetResult(NoctuaAdjustAttribution.FromJson(result));
                    });
                }
                else
                {
                    Instance.Value._log.Warning("Native plugin is null");
                    tcs.TrySetResult(new NoctuaAdjustAttribution());
                }
            }
            catch (Exception ex)
            {
                Instance.Value._log.Warning("GetAdjustAttributionAsync exception: " + ex.Message);
                tcs.TrySetResult(new NoctuaAdjustAttribution());
            }

            return tcs.Task;
        #else
            return Task.FromResult(new NoctuaAdjustAttribution());
        #endif
        }

        /// <summary>
        /// Gets the Adjust Device Identifier (ADID) asynchronously.
        /// Available on both iOS and Android.
        /// </summary>
        public static Task<string> GetAdjustAdidAsync()
        {
        #if UNITY_ANDROID || UNITY_IOS
            return NativeStringCallAsync(
                plugin => plugin.GetAdjustAdid,
                "GetAdjustAdidAsync"
            );
        #else
            return Task.FromResult(string.Empty);
        #endif
        }

        /// <summary>
        /// Gets the ID For Advertisers (IDFA) asynchronously.
        /// iOS only — returns empty string on Android.
        /// </summary>
        public static Task<string> GetAdjustIdfaAsync()
        {
        #if UNITY_ANDROID || UNITY_IOS
            return NativeStringCallAsync(
                plugin => plugin.GetAdjustIdfa,
                "GetAdjustIdfaAsync"
            );
        #else
            return Task.FromResult(string.Empty);
        #endif
        }

        /// <summary>
        /// Gets the ID For Vendors (IDFV) asynchronously.
        /// iOS only — returns empty string on Android.
        /// </summary>
        public static Task<string> GetAdjustIdfvAsync()
        {
        #if UNITY_ANDROID || UNITY_IOS
            return NativeStringCallAsync(
                plugin => plugin.GetAdjustIdfv,
                "GetAdjustIdfvAsync"
            );
        #else
            return Task.FromResult(string.Empty);
        #endif
        }

        /// <summary>
        /// Gets the Google Play Advertising ID asynchronously.
        /// Android only — returns empty string on iOS.
        /// </summary>
        public static Task<string> GetAdjustGoogleAdIdAsync()
        {
        #if UNITY_ANDROID || UNITY_IOS
            return NativeStringCallAsync(
                plugin => plugin.GetAdjustGoogleAdId,
                "GetAdjustGoogleAdIdAsync"
            );
        #else
            return Task.FromResult(string.Empty);
        #endif
        }

        /// <summary>
        /// Gets the Amazon Advertiser ID asynchronously.
        /// Android only — returns empty string on iOS.
        /// </summary>
        public static Task<string> GetAdjustAmazonAdIdAsync()
        {
        #if UNITY_ANDROID || UNITY_IOS
            return NativeStringCallAsync(
                plugin => plugin.GetAdjustAmazonAdId,
                "GetAdjustAmazonAdIdAsync"
            );
        #else
            return Task.FromResult(string.Empty);
        #endif
        }

        /// <summary>
        /// Gets the Adjust SDK version string asynchronously.
        /// Available on both iOS and Android.
        /// </summary>
        public static Task<string> GetAdjustSdkVersionAsync()
        {
        #if UNITY_ANDROID || UNITY_IOS
            return NativeStringCallAsync(
                plugin => plugin.GetAdjustSdkVersion,
                "GetAdjustSdkVersionAsync"
            );
        #else
            return Task.FromResult(string.Empty);
        #endif
        }

        // Shared helper — reduces boilerplate for all single-string native callbacks.
        private static Task<string> NativeStringCallAsync(
            Func<INativePlugin, Action<Action<string>>> methodSelector,
            string methodName)
        {
            var tcs = new TaskCompletionSource<string>();

            try
            {
                if (Instance.Value._nativePlugin != null)
                {
                    methodSelector(Instance.Value._nativePlugin)(result =>
                        tcs.TrySetResult(result ?? string.Empty));
                }
                else
                {
                    Instance.Value._log.Warning($"{methodName}: native plugin is null");
                    tcs.TrySetResult(string.Empty);
                }
            }
            catch (Exception ex)
            {
                Instance.Value._log.Warning($"{methodName} exception: {ex.Message}");
                tcs.TrySetResult(string.Empty);
            }

            return tcs.Task;
        }
    }
}
