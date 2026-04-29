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
            // without exposing the full secret.
            var token = config?.Adjust?.AppToken;
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

            return info;
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
