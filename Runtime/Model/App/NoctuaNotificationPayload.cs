using System;
using System.Collections.Generic;
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
        /// <summary>Built-in event sent to the Noctua backend when a push is received while the app is running.</summary>
        public const string ReceivedEventName = "live_ops_push_notification_received";

        /// <summary>Built-in event sent to the Noctua backend when the user taps a push notification.</summary>
        public const string OpenedEventName = "live_ops_push_notification_opened";

        private const string IosMessageIdKey     = "gcm.message_id";
        private const string AndroidMessageIdKey = "google.message_id";

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
        /// FCM message id (<c>gcm.message_id</c> on iOS, <c>google.message_id</c> on Android).
        /// Empty string when absent.
        /// </summary>
        public string MessageId { get; private set; } = string.Empty;

        /// <summary>
        /// Properties attached to the built-in <see cref="ReceivedEventName"/> /
        /// <see cref="OpenedEventName"/> events.
        /// </summary>
        public Dictionary<string, IConvertible> ToTrackingProperties()
        {
            var props = new Dictionary<string, IConvertible>
            {
                { "message_id", MessageId ?? string.Empty },
                { "title",      Title     ?? string.Empty },
                { "deeplink",   Deeplink  ?? string.Empty },
            };

            AddAttribution(props);
            return props;
        }

        /// <summary>Built-in event sent when this device's FCM token is first seen or rotates.</summary>
        public const string TokenUpdatedEventName = "live_ops_push_token_updated";

        /// <summary>
        /// <c>change</c> for <see cref="TokenUpdatedEventName"/>: <c>"first"</c> when no token was
        /// recorded on this install, <c>"rotated"</c> when it differs from the recorded one, or
        /// <c>null</c> when it is unchanged (or empty) and nothing should be sent.
        /// </summary>
        public static string TokenChangeKind(string previousHash, string currentHash)
        {
            if (string.IsNullOrEmpty(currentHash)) return null;
            if (string.IsNullOrEmpty(previousHash)) return "first";
            return string.Equals(previousHash, currentHash, StringComparison.Ordinal) ? null : "rotated";
        }

        /// <summary>
        /// A short SHA-256 fingerprint of an FCM token — what is persisted to detect a rotation,
        /// so the token itself is never written to local storage. Empty for an empty token.
        /// </summary>
        public static string HashToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return string.Empty;
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(token));
            return BitConverter.ToString(bytes, 0, 16).Replace("-", string.Empty).ToLowerInvariant();
        }

        /// <summary>Custom key the push sender sets to tie a push to a campaign.</summary>
        public const string CampaignIdKey = "campaign_id";

        /// <summary>Custom keys with this prefix are copied onto the push events as-is.</summary>
        public const string TrackedKeyPrefix = "noctua_";

        /// <summary>At most this many <see cref="TrackedKeyPrefix"/> keys are copied per push.</summary>
        public const int MaxTrackedKeys = 10;

        /// <summary>Longer values are cut to this many characters.</summary>
        public const int MaxTrackedValueLength = 100;

        // Only an allowlist (campaign_id + noctua_*) — a push can carry arbitrary game data, which
        // must not leak into analytics. Absent keys add nothing, so a plain push keeps the old
        // three-property payload.
        private void AddAttribution(Dictionary<string, IConvertible> props)
        {
            if (Custom == null) return;

            var campaignId = GetCustomString(CampaignIdKey);
            if (!string.IsNullOrEmpty(campaignId)) props[CampaignIdKey] = Clip(campaignId);

            var copied = 0;
            foreach (var prop in Custom.Properties())
            {
                if (copied >= MaxTrackedKeys) break;
                if (!prop.Name.StartsWith(TrackedKeyPrefix, StringComparison.Ordinal)) continue;
                // Already reported as "deeplink"; reserved keys never get overwritten.
                if (prop.Name == "noctua_deeplink" || props.ContainsKey(prop.Name)) continue;
                if (prop.Value is JContainer) continue; // scalars only

                var value = prop.Value?.ToString();
                if (string.IsNullOrEmpty(value)) continue;

                props[prop.Name] = Clip(value);
                copied++;
            }
        }

        private static string Clip(string value) =>
            value.Length <= MaxTrackedValueLength ? value : value.Substring(0, MaxTrackedValueLength);

        /// <summary>
        /// Reads a custom string field by name. Returns empty string when missing.
        /// </summary>
        public string GetCustomString(string key)
        {
            if (Custom == null || !Custom.TryGetValue(key, out var token)) return string.Empty;
            return token?.ToString() ?? string.Empty;
        }

        public static NoctuaNotificationPayload FromJson(string json)
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

                var messageId = payload.GetCustomString(IosMessageIdKey);
                payload.MessageId = string.IsNullOrEmpty(messageId)
                    ? payload.GetCustomString(AndroidMessageIdKey)
                    : messageId;
            }
            catch (Exception)
            {
                // Malformed JSON — preserve RawJson for debugging; callers still get a valid object.
            }

            return payload;
        }
    }
}
