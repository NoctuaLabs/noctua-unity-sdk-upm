using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Parsed remote-push payload delivered to <see cref="Noctua.OnRemoteNotificationReceived"/>
    /// and <see cref="Noctua.OnNotificationTapped"/>. Wraps the raw JSON string that comes from
    /// the iOS <c>userInfo</c> dictionary (and the equivalent Android <c>RemoteMessage</c> data)
    /// with convenience accessors for the common fields (title, body, deeplink, custom data).
    /// </summary>
    public class NoctuaNotificationPayload
    {
        /// <summary>Raw JSON string of the full push payload.</summary>
        public string RawJson { get; private set; }

        /// <summary>Top-level <c>aps</c> dictionary on iOS. <c>null</c> on Android.</summary>
        public JObject Aps { get; private set; }

        /// <summary>Custom fields outside the <c>aps</c>/<c>notification</c> envelope.</summary>
        public JObject Custom { get; private set; }

        /// <summary>Convenience: APS <c>alert.title</c> (iOS) or notification title (Android).</summary>
        public string Title { get; private set; }

        /// <summary>Convenience: APS <c>alert.body</c> (iOS) or notification body (Android).</summary>
        public string Body { get; private set; }

        /// <summary>
        /// Convenience: reads a custom deeplink URL from the most common field names
        /// (<c>deeplink</c>, <c>noctua_deeplink</c>, <c>route</c>). Empty string when absent.
        /// Games that use a different field name should read <see cref="Custom"/> directly.
        /// </summary>
        public string Deeplink { get; private set; }

        /// <summary>
        /// Reads a custom string field by name. Returns empty string when missing.
        /// </summary>
        public string GetCustomString(string key)
        {
            if (Custom == null || !Custom.TryGetValue(key, out var token)) return string.Empty;
            return token?.ToString() ?? string.Empty;
        }

        internal static NoctuaNotificationPayload FromJson(string json)
        {
            var payload = new NoctuaNotificationPayload { RawJson = json ?? "{}", Custom = new JObject() };
            if (string.IsNullOrEmpty(json)) return payload;

            try
            {
                var root = JObject.Parse(json);

                // iOS: { "aps": {"alert": {"title":..., "body":...}, ...}, "deeplink": ... }
                if (root["aps"] is JObject aps)
                {
                    payload.Aps = aps;
                    if (aps["alert"] is JObject alert)
                    {
                        payload.Title = alert["title"]?.ToString() ?? string.Empty;
                        payload.Body  = alert["body"]?.ToString()  ?? string.Empty;
                    }
                    else if (aps["alert"] is JValue alertString)
                    {
                        // iOS simple alert form: "aps": {"alert": "hello"}
                        payload.Body = alertString.ToString();
                    }
                }

                // Android FCM: { "notification": {"title":..., "body":...}, "data": {...} }
                if (string.IsNullOrEmpty(payload.Title) && root["notification"] is JObject notif)
                {
                    payload.Title = notif["title"]?.ToString() ?? string.Empty;
                    payload.Body  = notif["body"]?.ToString()  ?? string.Empty;
                }

                // Collect remaining top-level keys into Custom (excludes aps / notification envelopes).
                foreach (var prop in root.Properties())
                {
                    if (prop.Name == "aps" || prop.Name == "notification") continue;
                    payload.Custom[prop.Name] = prop.Value;
                }

                // Well-known deeplink fields — first non-empty match wins.
                foreach (var key in new[] { "deeplink", "noctua_deeplink", "route", "link", "url" })
                {
                    var value = payload.GetCustomString(key);
                    if (!string.IsNullOrEmpty(value)) { payload.Deeplink = value; break; }
                }
                payload.Deeplink ??= string.Empty;
            }
            catch (Exception)
            {
                // Malformed JSON — preserve RawJson for debugging; callers still get a valid object.
            }

            return payload;
        }
    }

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
        /// <summary>
        /// Fires when a remote push notification arrives (foreground OR background).
        /// The delivered <see cref="NoctuaNotificationPayload"/> exposes parsed
        /// title/body + custom deeplink field + raw JSON. Subscribe once during SDK init.
        /// </summary>
        public static event Action<NoctuaNotificationPayload> OnRemoteNotificationReceived
        {
            add    => _pushHandlers.OnReceived += value;
            remove => _pushHandlers.OnReceived -= value;
        }

        /// <summary>
        /// Fires when the user taps a notification — primary deeplink hook. Inspect
        /// <see cref="NoctuaNotificationPayload.Deeplink"/> or
        /// <see cref="NoctuaNotificationPayload.GetCustomString(string)"/> to read the
        /// game-specific route field and navigate to the matching scene.
        /// </summary>
        public static event Action<NoctuaNotificationPayload> OnNotificationTapped
        {
            add    => _pushHandlers.OnTapped += value;
            remove => _pushHandlers.OnTapped -= value;
        }

        /// <summary>
        /// Fires when Firebase Cloud Messaging rotates the FCM registration token
        /// (reinstall, app-data clear, device restore, periodic refresh). Re-register
        /// the new value with your backend push service.
        /// </summary>
        public static event Action<string> OnFirebaseMessagingTokenRefresh
        {
            add    => _pushHandlers.OnTokenRefresh += value;
            remove => _pushHandlers.OnTokenRefresh -= value;
        }

        // Single shared handlers container so we register with the native plugin exactly once
        // regardless of how many managed subscribers attach to the events above.
        private static readonly PushHandlers _pushHandlers = new PushHandlers();

        private class PushHandlers
        {
            private bool _registered;
            public event Action<NoctuaNotificationPayload> OnReceived;
            public event Action<NoctuaNotificationPayload> OnTapped;
            public event Action<string>                    OnTokenRefresh;

            public PushHandlers()
            {
                // Lazy-register on first subscriber attach via EnsureRegistered() — but we
                // also support the pattern where Noctua.InitAsync() already completed and a
                // subscriber attaches later. Hooking the native side eagerly at construction
                // would force the static field to run before InitAsync — risky — so we defer.
            }

            private void EnsureRegistered()
            {
                if (_registered) return;
                _registered = true;

                try
                {
                    if (Instance.Value?._nativePlugin == null) { _registered = false; return; }

                    Instance.Value._nativePlugin.SetRemoteNotificationReceivedHandler(json =>
                        OnReceived?.Invoke(NoctuaNotificationPayload.FromJson(json)));
                    Instance.Value._nativePlugin.SetNotificationTappedHandler(json =>
                        OnTapped?.Invoke(NoctuaNotificationPayload.FromJson(json)));
                    Instance.Value._nativePlugin.SetFirebaseMessagingTokenRefreshHandler(token =>
                        OnTokenRefresh?.Invoke(token ?? string.Empty));
                }
                catch (Exception ex)
                {
                    Instance.Value?._log?.Warning($"PushHandlers registration failed: {ex.Message}");
                    _registered = false;
                }
            }

            public static void Poke(PushHandlers self) => self.EnsureRegistered();
        }

        /// <summary>Internal hook — call after InitAsync to register the native push bridges.</summary>
        internal static void RegisterPushHandlers() => PushHandlers.Poke(_pushHandlers);

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
