using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Assembles a <see cref="BuildSanityInfo"/> snapshot for the
    /// Inspector "Build" tab. Pulls config-time data from the loaded
    /// <see cref="GlobalConfig"/>, runtime metadata from `Application`,
    /// and platform-specific values from the native bridge interfaces.
    ///
    /// Stateless and thread-safe. Cheap enough to call per-render —
    /// the SHA-256 of `noctuagg.json` is the only non-trivial work
    /// and the Inspector tab caches the result via the controller's
    /// dirty-flag rendering.
    /// </summary>
    public static class BuildSanityProvider
    {
        /// <param name="config">The loaded <see cref="GlobalConfig"/>; may be null.</param>
        /// <param name="rawConfigJson">Raw `noctuagg.json` bytes for checksum. Optional.</param>
        /// <param name="buildInfo">Native bridge for platform-specific metadata. Optional.</param>
        /// <param name="googleServicesProbe">Optional override for the GoogleServices probe (tests).</param>
        public static BuildSanityInfo Snapshot(
            GlobalConfig config,
            string rawConfigJson = null,
            INativeBuildInfo buildInfo = null,
            Func<bool> googleServicesProbe = null)
        {
            var info = new BuildSanityInfo
            {
                UnitySdkVersion = ReadUnitySdkVersion(),
                BundleId        = SafeApplicationField(() => Application.identifier),
                AppVersion      = SafeApplicationField(() => Application.version),
                UnityVersion    = SafeApplicationField(() => Application.unityVersion),
                IsSandbox       = config?.Noctua?.IsSandbox == true,
                Region          = config?.Noctua?.Region ?? "",
            };

            // Adjust app token masking — show only last 6 chars so the
            // Inspector reveals "this build is using token ending …4f7q"
            // without exposing the full secret. AdjustConfig has separate
            // Android / iOS sub-configs; pick the platform-active one.
            var token = ResolveAdjustAppToken(config);
            info.AdjustAppTokenMasked = string.IsNullOrEmpty(token)
                ? ""
                : (token.Length <= 6 ? "…" + token : "…" + token.Substring(token.Length - 6));

            // Native-side metadata (runs only on iOS / Android device builds;
            // Editor stub returns sentinels).
            if (buildInfo != null)
            {
                try
                {
                    info.NativeSdkVersion         = buildInfo.GetNativeSdkVersion() ?? "";
                    info.FirebaseProjectId        = buildInfo.GetFirebaseProjectId() ?? "";
                    info.SkAdNetworksCount        = buildInfo.GetSkAdNetworksCount();
                    info.AndroidPermissionsCount  = buildInfo.GetAndroidPermissionsCount();
                }
                catch { /* swallow — defaults remain */ }
            }

            // GoogleServices probe — Android-relevant. Tested in StreamingAssets
            // because Unity's google-services Gradle plugin reads it from there.
            try
            {
                info.GoogleServicesPresent = googleServicesProbe != null
                    ? googleServicesProbe()
                    : DefaultGoogleServicesPresent();
            }
            catch { info.GoogleServicesPresent = false; }

            // Config checksum — SHA-256 of the raw JSON bytes. If the
            // caller didn't capture them at load time, we re-read from
            // StreamingAssets (best-effort; may fail on Android device
            // builds where the path isn't a regular filesystem path).
            try
            {
                info.ConfigChecksum = ComputeChecksum(rawConfigJson);
            }
            catch { info.ConfigChecksum = ""; }

            // Raw config text — pretty-printed for the Build tab's
            // "noctuagg.json" section. Trade-off: doing this on every
            // BuildSanity() call costs a JSON parse + serialize, but the
            // Build tab is sandbox-only and rendered at most a few times
            // per session, so it's not on a hot path. If the caller didn't
            // pass raw text, leave empty (the tab just hides the section).
            try
            {
                info.RawConfigJson = PrettyPrintJson(rawConfigJson);
            }
            catch { info.RawConfigJson = rawConfigJson ?? ""; }

            return info;
        }

        /// <summary>
        /// Round-trips raw JSON through Newtonsoft to indent it with
        /// 2-space nesting. Returns the input verbatim if it's empty
        /// or fails to parse — better to show the raw blob than nothing.
        /// </summary>
        private static string PrettyPrintJson(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            try
            {
                var parsed = Newtonsoft.Json.Linq.JToken.Parse(raw);
                return parsed.ToString(Newtonsoft.Json.Formatting.Indented);
            }
            catch
            {
                return raw;
            }
        }

        /// <summary>
        /// Returns the Adjust app token for the active platform — Android
        /// when running on Android, iOS when running on iOS, and prefers
        /// whichever is non-empty in Editor builds. AdjustConfig stores
        /// platform-specific tokens because Android and iOS are different
        /// apps in Adjust's data model.
        /// </summary>
        public static string ResolveAdjustAppToken(GlobalConfig config)
        {
            if (config?.Adjust == null) return "";
#if UNITY_ANDROID && !UNITY_EDITOR
            return config.Adjust.Android?.AppToken ?? "";
#elif UNITY_IOS && !UNITY_EDITOR
            return config.Adjust.Ios?.AppToken ?? "";
#else
            // Editor — show whichever is configured. Prefer Android since
            // most game studios bundle that token first; fall back to iOS.
            return string.IsNullOrEmpty(config.Adjust.Android?.AppToken)
                ? (config.Adjust.Ios?.AppToken ?? "")
                : config.Adjust.Android.AppToken;
#endif
        }

        /// <summary>
        /// Returns the Adjust event map for the active platform. Used by
        /// the Inspector "Adjust event mapping" panel to surface the
        /// game-event-name → Adjust-callback-token relationship.
        /// </summary>
        public static System.Collections.Generic.Dictionary<string, string> ResolveAdjustEventMap(GlobalConfig config)
        {
            if (config?.Adjust == null) return null;
#if UNITY_ANDROID && !UNITY_EDITOR
            return config.Adjust.Android?.EventMap;
#elif UNITY_IOS && !UNITY_EDITOR
            return config.Adjust.Ios?.EventMap;
#else
            return config.Adjust.Android?.EventMap?.Count > 0
                ? config.Adjust.Android.EventMap
                : config.Adjust.Ios?.EventMap;
#endif
        }

        private static string ReadUnitySdkVersion()
        {
            try
            {
                var asm = typeof(BuildSanityProvider).Assembly;
                var attr = asm.GetCustomAttribute<AssemblyVersionAttribute>();
                if (attr != null && !string.IsNullOrEmpty(attr.Version)) return attr.Version;
                var name = asm.GetName();
                return name.Version != null ? name.Version.ToString() : "";
            }
            catch { return ""; }
        }

        private static string SafeApplicationField(Func<string> fn)
        {
            try { return fn() ?? ""; } catch { return ""; }
        }

        private static bool DefaultGoogleServicesPresent()
        {
            // On Android device, StreamingAssets is inside the APK so File.Exists
            // returns false even when the file is shipped. Best-effort: only treat
            // a regular filesystem path as authoritative; everything else returns
            // false (the panel will show "checking…" semantics on Android). For a
            // perfectly-correct Android answer the caller should pass a probe
            // backed by `UnityWebRequest`.
            try
            {
                var path = Path.Combine(Application.streamingAssetsPath, "google-services.json");
                return File.Exists(path);
            }
            catch { return false; }
        }

        private static string ComputeChecksum(string rawJson)
        {
            // If caller captured the raw text at load time we use that.
            // Otherwise re-read from disk (best-effort).
            byte[] data = null;
            if (!string.IsNullOrEmpty(rawJson))
            {
                data = Encoding.UTF8.GetBytes(rawJson);
            }
            else
            {
                try
                {
                    var path = Path.Combine(Application.streamingAssetsPath, "noctuagg.json");
                    if (File.Exists(path)) data = File.ReadAllBytes(path);
                }
                catch { /* fall through */ }
            }
            if (data == null) return "";

            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(data);
            // Hex encode — short enough to fit one Inspector row.
            var sb = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
