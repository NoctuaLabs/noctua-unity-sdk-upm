using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace com.noctuagames.sdk
{
    public partial class Noctua
    {
        /// <summary>
        /// Get Firebase Installation ID asynchronously using native plugin where supported.
        /// </summary>
        /// <returns>A task that resolves to the Firebase Installation ID or empty string when not available.</returns>
        public static Task<string> GetFirebaseInstallationID()
        {
        #if UNITY_ANDROID || UNITY_IOS
            var tcs = new TaskCompletionSource<string>();

            try
            {
                if (Instance.Value._nativePlugin != null)
                {
                    Instance.Value._nativePlugin.GetFirebaseInstallationID((id) =>
                    {
                        // Normalize null to empty string
                        var safeId = id ?? string.Empty;
                        tcs.TrySetResult(safeId);
                    });
                }
                else
                {
                    Instance.Value._log.Warning("Native plugin is null");
                    tcs.TrySetResult(string.Empty);
                }
            }
            catch (Exception ex)
            {
                Instance.Value._log.Warning("exception: " + ex.Message);

                tcs.TrySetResult(string.Empty);

            }

            return tcs.Task;
        #else
            return Task.FromResult(string.Empty);
        #endif
        }

        /// <summary>
        /// Get Firebase Analytics session ID asynchronously using native plugin where supported.
        /// </summary>
        /// <returns>A task that resolves to the Firebase Analytics session ID or empty string when not available.</returns>
        public static Task<string> GetFirebaseAnalyticsSessionID()
        {
        #if UNITY_ANDROID || UNITY_IOS
            var tcs = new TaskCompletionSource<string>();

            try
            {
                if (Instance.Value._nativePlugin != null)
                {
                    Instance.Value._nativePlugin.GetFirebaseAnalyticsSessionID((id) =>
                    {
                        var safeId = id ?? string.Empty;
                        tcs.TrySetResult(safeId);
                    });
                }
                else
                {
                    Instance.Value._log.Warning("Native plugin is null");
                    tcs.TrySetResult(string.Empty);
                }
            }
            catch (Exception ex)
            {
                Instance.Value._log.Warning("exception: " + ex.Message);

                tcs.TrySetResult(string.Empty);
            }

            return tcs.Task;
        #else
            return Task.FromResult(string.Empty);
        #endif
        }

        /// <summary>
        /// Get the current Firebase Cloud Messaging (FCM) registration token asynchronously.
        /// </summary>
        /// <remarks>
        /// The token is minted once the APNs (iOS) / Firebase registration handshake completes.
        /// Calling this too early after <c>Noctua.InitAsync()</c> may return an empty string —
        /// on iOS the token is available only after the delegate callback
        /// <c>messaging:didReceiveRegistrationToken:</c> fires, which in turn requires the user
        /// to grant notification permission and the APNs device token to be forwarded
        /// (handled automatically by <c>CustomAppController</c>).
        /// Recommended pattern for game code:
        /// <code>
        /// var token = await Noctua.GetFirebaseMessagingToken();
        /// if (string.IsNullOrEmpty(token))
        /// {
        ///     // Retry after a short delay — the APNs handshake usually finishes within
        ///     // a few seconds of the user granting notification permission.
        /// }
        /// </code>
        /// On Android the token is typically available immediately after SDK init.
        /// On Editor / unsupported platforms returns an empty string.
        /// </remarks>
        /// <returns>A task that resolves to the FCM token, or empty string when unavailable.</returns>
        public static Task<string> GetFirebaseMessagingToken()
        {
        #if UNITY_ANDROID || UNITY_IOS
            var tcs = new TaskCompletionSource<string>();

            try
            {
                if (Instance.Value._nativePlugin != null)
                {
                    Instance.Value._nativePlugin.GetFirebaseMessagingToken((token) =>
                    {
                        tcs.TrySetResult(token ?? string.Empty);
                    });
                }
                else
                {
                    Instance.Value._log.Warning("Native plugin is null");
                    tcs.TrySetResult(string.Empty);
                }
            }
            catch (Exception ex)
            {
                Instance.Value._log.Warning($"GetFirebaseMessagingToken exception: {ex.Message}");
                tcs.TrySetResult(string.Empty);
            }

            return tcs.Task;
        #else
            return Task.FromResult(string.Empty);
        #endif
        }

        /// <summary>
        /// Get a string value from Firebase Remote Config asynchronously using native plugin where supported.
        /// </summary>
        /// <param name="key">The Remote Config key to retrieve.</param>
        /// <returns>A task that resolves to the string value or empty string when not available.</returns>
        public static Task<string> GetFirebaseRemoteConfigString(string key)
        {
        #if UNITY_ANDROID || UNITY_IOS
            var tcs = new TaskCompletionSource<string>();

            try
            {
                if (Instance.Value._nativePlugin != null)
                {
                    Instance.Value._nativePlugin.GetFirebaseRemoteConfigString(key, (value) =>
                    {
                        var safeValue = value ?? string.Empty;
                        tcs.TrySetResult(safeValue);
                    });
                }
                else
                {
                    Instance.Value._log.Warning("Native plugin is null");
                    tcs.TrySetResult(string.Empty);
                }
            }
            catch (Exception ex)
            {
                Instance.Value._log.Warning($"GetFirebaseRemoteConfigString exception: {ex.Message}");
                tcs.TrySetResult(string.Empty);
            }

            return tcs.Task;
        #else
            return Task.FromResult(string.Empty);
        #endif
        }

        /// <summary>
        /// Get a boolean value from Firebase Remote Config asynchronously using native plugin where supported.
        /// </summary>
        /// <param name="key">The Remote Config key to retrieve.</param>
        /// <returns>A task that resolves to the boolean value or false when not available.</returns>
        public static Task<bool> GetFirebaseRemoteConfigBoolean(string key)
        {
        #if UNITY_ANDROID || UNITY_IOS
            var tcs = new TaskCompletionSource<bool>();

            try
            {
                if (Instance.Value._nativePlugin != null)
                {
                    Instance.Value._nativePlugin.GetFirebaseRemoteConfigBoolean(key, (value) =>
                    {
                        tcs.TrySetResult(value);
                    });
                }
                else
                {
                    Instance.Value._log.Warning("Native plugin is null");
                    tcs.TrySetResult(false);
                }
            }
            catch (Exception ex)
            {
                Instance.Value._log.Warning($"GetFirebaseRemoteConfigBoolean exception: {ex.Message}");
                tcs.TrySetResult(false);
            }

            return tcs.Task;
        #else
            return Task.FromResult(false);
        #endif
        }

        /// <summary>
        /// Get a double value from Firebase Remote Config asynchronously using native plugin where supported.
        /// </summary>
        /// <param name="key">The Remote Config key to retrieve.</param>
        /// <returns>A task that resolves to the double value or 0.0 when not available.</returns>
        public static Task<double> GetFirebaseRemoteConfigDouble(string key)
        {
        #if UNITY_ANDROID || UNITY_IOS
            var tcs = new TaskCompletionSource<double>();

            try
            {
                if (Instance.Value._nativePlugin != null)
                {
                    Instance.Value._nativePlugin.GetFirebaseRemoteConfigDouble(key, (value) =>
                    {
                        tcs.TrySetResult(value);
                    });
                }
                else
                {
                    Instance.Value._log.Warning("Native plugin is null");
                    tcs.TrySetResult(0.0);
                }
            }
            catch (Exception ex)
            {
                Instance.Value._log.Warning($"GetFirebaseRemoteConfigDouble exception: {ex.Message}");
                tcs.TrySetResult(0.0);
            }

            return tcs.Task;
        #else
            return Task.FromResult(0.0);
        #endif
        }

        /// <summary>
        /// Get a long value from Firebase Remote Config asynchronously using native plugin where supported.
        /// </summary>
        /// <param name="key">The Remote Config key to retrieve.</param>
        /// <returns>A task that resolves to the long value or 0L when not available.</returns>
        public static Task<long> GetFirebaseRemoteConfigLong(string key)
        {
        #if UNITY_ANDROID || UNITY_IOS
            var tcs = new TaskCompletionSource<long>();

            try
            {
                if (Instance.Value._nativePlugin != null)
                {
                    Instance.Value._nativePlugin.GetFirebaseRemoteConfigLong(key, (value) =>
                    {
                        tcs.TrySetResult(value);
                    });
                }
                else
                {
                    Instance.Value._log.Warning("Native plugin is null");
                    tcs.TrySetResult(0L);
                }
            }
            catch (Exception ex)
            {
                Instance.Value._log.Warning($"GetFirebaseRemoteConfigLong exception: {ex.Message}");
                tcs.TrySetResult(0L);
            }

            return tcs.Task;
        #else
            return Task.FromResult(0L);
        #endif
        }


        /// <summary>
        /// Gets the Adjust attribution data asynchronously using the native plugin.
        /// </summary>
        /// <returns>A task that resolves to the attribution data, or a default instance when not available.</returns>
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
                        var attribution = NoctuaAdjustAttribution.FromJson(result);
                        tcs.TrySetResult(attribution);
                    });
                }
                else
                {
                    Instance.Value._log.Warning("Native plugin is null");
                    tcs.TrySetResult(new NoctuaAdjustAttribution());
                    return tcs.Task;
                }
            }
            catch (Exception ex)
            {
                Instance.Value._log.Warning("exception: " + ex.Message);

                tcs.TrySetResult(new NoctuaAdjustAttribution());
                return tcs.Task;
            }

            return tcs.Task;
        #else
            return Task.FromResult(new NoctuaAdjustAttribution());
        #endif
        }
    }
}
