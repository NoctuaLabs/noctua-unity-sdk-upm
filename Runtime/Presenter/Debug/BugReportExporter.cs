using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Bundles the Inspector's snapshots into a single timestamped ZIP
    /// for handoff to QA / engineering. The export includes the most
    /// recent <see cref="LogsCap"/> log lines, <see cref="EventsCap"/>
    /// tracker events, <see cref="HttpCap"/> HTTP exchanges, a build
    /// sanity report, and a PNG screenshot — captured at the moment the
    /// user taps "Export bug report".
    ///
    /// Sandbox-only by contract: callers (Inspector "Build" tab) only
    /// invoke this method when <see cref="Noctua.IsSandbox"/> is true.
    /// Per the existing privacy posture documented in the verbose-log
    /// stream README, log payloads can carry game-side strings — the
    /// SDK doesn't sanitise them. Tell QA to attach the ZIP to the
    /// bug ticket, not to a public chat channel.
    /// </summary>
    public static class BugReportExporter
    {
        public const int LogsCap = 500;
        public const int EventsCap = 50;
        public const int HttpCap = 20;

        /// <summary>
        /// Build a bug report and write it to
        /// <c>{Application.persistentDataPath}/noctua-bugreport-{ts}.zip</c>.
        ///
        /// Screenshot capture is async — must run as a coroutine. Yields
        /// once for the end-of-frame so Unity has a chance to render the
        /// frame the user sees on screen. Returns via <paramref name="onDone"/>
        /// with the absolute path on success or null on error.
        /// </summary>
        public static IEnumerator Export(
            LogInspectorLedger logLedger,
            TrackerDebugMonitor debugMonitor,
            HttpInspectorLog httpLog,
            BuildSanityInfo build,
            Action<string> onDone)
        {
            // Wait for the end of the current frame so the UI has rendered
            // before we capture — without this the screenshot may grab
            // a half-rendered frame.
            yield return new WaitForEndOfFrame();

            byte[] screenshotPng = null;
            try
            {
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                try { screenshotPng = tex.EncodeToPNG(); }
                finally { UnityEngine.Object.Destroy(tex); }
            }
            catch
            {
                screenshotPng = null;
            }

            string outPath = null;
            try
            {
                var ts = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                outPath = Path.Combine(Application.persistentDataPath, $"noctua-bugreport-{ts}.zip");

                using var fs = File.Open(outPath, FileMode.Create, FileAccess.Write);
                using var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false);

                WriteText(zip, "build.txt",  FormatBuild(build));
                WriteText(zip, "device.txt", FormatDevice());
                WriteText(zip, "logs.txt",   FormatLogs(logLedger));
                WriteText(zip, "events.txt", FormatEvents(debugMonitor));
                WriteText(zip, "http.txt",   FormatHttp(httpLog));
                if (screenshotPng != null)
                {
                    WriteBytes(zip, "screenshot.png", screenshotPng);
                }
            }
            catch
            {
                outPath = null;
            }

            onDone?.Invoke(outPath);
        }

        private static void WriteText(ZipArchive zip, string name, string text)
        {
            var entry = zip.CreateEntry(name, System.IO.Compression.CompressionLevel.Optimal);
            using var stream = entry.Open();
            var bytes = Encoding.UTF8.GetBytes(text ?? "");
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteBytes(ZipArchive zip, string name, byte[] data)
        {
            var entry = zip.CreateEntry(name, System.IO.Compression.CompressionLevel.Optimal);
            using var stream = entry.Open();
            stream.Write(data, 0, data.Length);
        }

        private static string FormatBuild(BuildSanityInfo b)
        {
            if (b == null) return "(no build info)";
            return string.Join("\n", new[]
            {
                $"Unity SDK version       : {b.UnitySdkVersion}",
                $"Native plugin version   : {b.NativeSdkVersion}",
                $"Bundle ID               : {b.BundleId}",
                $"App version             : {b.AppVersion}",
                $"Unity Editor version    : {b.UnityVersion}",
                $"Sandbox mode            : {(b.IsSandbox ? "ENABLED" : "disabled")}",
                $"Region                  : {b.Region}",
                $"noctuagg.json SHA-256   : {b.ConfigChecksum}",
                $"Adjust app token        : {b.AdjustAppTokenMasked}",
                $"Firebase project ID     : {b.FirebaseProjectId}",
                $"GoogleServices file     : {(b.GoogleServicesPresent ? "present" : "MISSING")}",
                $"SKAdNetworks count      : {(b.SkAdNetworksCount      < 0 ? "n/a" : b.SkAdNetworksCount     .ToString())}",
                $"Permissions count       : {(b.AndroidPermissionsCount< 0 ? "n/a" : b.AndroidPermissionsCount.ToString())}",
            });
        }

        private static string FormatDevice()
        {
            return string.Join("\n", new[]
            {
                $"Device model           : {SystemInfo.deviceModel}",
                $"Device name            : {SystemInfo.deviceName}",
                $"Operating system       : {SystemInfo.operatingSystem}",
                $"OS family              : {SystemInfo.operatingSystemFamily}",
                $"Processor              : {SystemInfo.processorType} x{SystemInfo.processorCount}",
                $"System memory (MB)     : {SystemInfo.systemMemorySize}",
                $"Graphics device        : {SystemInfo.graphicsDeviceName}",
                $"Graphics API           : {SystemInfo.graphicsDeviceType}",
                $"Battery status         : {SystemInfo.batteryStatus} ({SystemInfo.batteryLevel:P0})",
                $"Network reachability   : {Application.internetReachability}",
                $"Screen                 : {Screen.width}x{Screen.height} @ {Screen.dpi:F0}dpi",
                $"Locale (system)        : {Application.systemLanguage}",
                $"Captured at (UTC)      : {DateTime.UtcNow:O}",
            });
        }

        private static string FormatLogs(LogInspectorLedger ledger)
        {
            if (ledger == null) return "(logs ledger unavailable)";
            var snapshot = ledger.Snapshot();
            var sb = new StringBuilder(64 * 1024);
            int start = Math.Max(0, snapshot.Count - LogsCap);
            for (int i = start; i < snapshot.Count; i++)
            {
                var e = snapshot[i];
                sb.Append(e.TimestampUtc.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                sb.Append(' ').Append(LevelChar(e.Level));
                sb.Append(' ').Append(e.Source).Append('/').Append(e.Tag);
                sb.Append(": ").AppendLine(e.Message);
                if (!string.IsNullOrEmpty(e.StackTrace))
                {
                    sb.AppendLine(e.StackTrace);
                }
            }
            return sb.ToString();
        }

        private static string FormatEvents(TrackerDebugMonitor monitor)
        {
            if (monitor == null) return "(tracker monitor unavailable)";
            var snapshot = monitor.Snapshot();
            var sb = new StringBuilder(16 * 1024);
            int start = Math.Max(0, snapshot.Count - EventsCap);
            for (int i = start; i < snapshot.Count; i++)
            {
                var e = snapshot[i];
                sb.Append(e.CreatedUtc.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                sb.Append(' ').Append(e.Provider).Append('/').Append(e.EventName);
                sb.Append(" → ").Append(e.Phase);
                if (!string.IsNullOrEmpty(e.Error)) sb.Append("  err=").Append(e.Error);
                sb.AppendLine();
                if (e.Payload != null && e.Payload.Count > 0)
                {
                    sb.Append("    payload: ").AppendLine(SafeSerialize(e.Payload));
                }
            }
            return sb.ToString();
        }

        private static string FormatHttp(HttpInspectorLog httpLog)
        {
            if (httpLog == null) return "(http log unavailable)";
            var snapshot = httpLog.Snapshot();
            var sb = new StringBuilder(16 * 1024);
            int start = Math.Max(0, snapshot.Count - HttpCap);
            for (int i = start; i < snapshot.Count; i++)
            {
                var ex = snapshot[i];
                sb.Append(ex.StartUtc.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                sb.Append(' ').Append(ex.Method).Append(' ').Append(ex.Url);
                sb.Append(" → ").Append(ex.Status);
                sb.Append(" (").Append(ex.State).Append(") ");
                sb.Append(ex.ElapsedMs).Append("ms");
                if (!string.IsNullOrEmpty(ex.Error)) sb.Append("  err=").Append(ex.Error);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static char LevelChar(LogLevel level) => level switch
        {
            LogLevel.Verbose => 'V',
            LogLevel.Debug   => 'D',
            LogLevel.Info    => 'I',
            LogLevel.Warning => 'W',
            LogLevel.Error   => 'E',
            _                => '?',
        };

        private static string SafeSerialize(IReadOnlyDictionary<string, object> dict)
        {
            try
            {
                return JsonConvert.SerializeObject(dict);
            }
            catch
            {
                // Fallback — coerce values via ToString. Avoids crashing the
                // export when a payload value isn't JSON-serializable.
                var sb = new StringBuilder("{");
                bool first = true;
                foreach (var kv in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append('"').Append(kv.Key).Append("\":\"")
                      .Append(kv.Value?.ToString()?.Replace("\"", "\\\"") ?? "")
                      .Append('"');
                }
                sb.Append('}');
                return sb.ToString();
            }
        }
    }
}
