using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Queries Android's <c>ActivityManager.getHistoricalProcessExitReasons()</c>
    /// (API 30+) to discover previous-session native crashes, ANRs, and abnormal
    /// exits. Pure JNI wrapper — no custom AAR required.
    /// </summary>
    /// <remarks>
    /// Call <see cref="PollRecentCrashes(long)"/> once on app launch, passing the
    /// highest <c>timestamp_ms</c> observed from a previous poll. Returns crash
    /// records strictly newer than that timestamp. The caller is responsible for
    /// persisting the returned high-water timestamp (e.g. PlayerPrefs) and for
    /// forwarding each record to the event pipeline.
    ///
    /// Android SDK constants reproduced here to avoid a custom AAR:
    /// <list type="bullet">
    /// <item><c>REASON_CRASH=4</c> — managed process crash (covered by C# hooks; skipped)</item>
    /// <item><c>REASON_CRASH_NATIVE=5</c> — native-layer crash (SIGSEGV, SIGABRT, …)</item>
    /// <item><c>REASON_ANR=6</c> — Application Not Responding</item>
    /// <item><c>REASON_SIGNALED=9</c> — killed by a signal</item>
    /// <item><c>REASON_LOW_MEMORY=3</c> — killed under low-memory pressure</item>
    /// <item><c>REASON_EXCESSIVE_RESOURCE_USAGE=13</c></item>
    /// </list>
    /// </remarks>
    public static class AndroidNativeCrashReporter
    {
        private static readonly ILogger _log = new NoctuaLogger(typeof(AndroidNativeCrashReporter));

        private const int REASON_LOW_MEMORY = 3;
        private const int REASON_CRASH_NATIVE = 5;
        private const int REASON_ANR = 6;
        private const int REASON_SIGNALED = 9;
        private const int REASON_EXCESSIVE_RESOURCE_USAGE = 13;

        /// <summary>
        /// A single native-crash / abnormal-exit record produced by the Android
        /// OS. Plain DTO — no behavior.
        /// </summary>
        public sealed class NativeCrashRecord
        {
            public string ErrorType;       // e.g. "CRASH_NATIVE", "ANR", "SIGNALED"
            public string Severity;        // "crash"
            public string Message;         // getDescription()
            public string StackTrace;      // getTraceInputStream() first N KB (ANR only)
            public long TimestampMs;       // getTimestamp()
            public int Pid;
            public int ExitStatus;         // getStatus()
            public string ProcessName;
            public string OsReportId;      // stable id for dedup
        }

        /// <summary>
        /// Returns crash / abnormal-exit records newer than <paramref name="sinceTimestampMs"/>.
        /// </summary>
        public static List<NativeCrashRecord> PollRecentCrashes(long sinceTimestampMs)
        {
            var results = new List<NativeCrashRecord>();

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (GetAndroidApiLevel() < 30)
                {
                    return results;  // ApplicationExitInfo requires API 30+
                }

                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                if (activity == null) return results;

                using var context = activity.Call<AndroidJavaObject>("getApplicationContext");
                using var contextClass = new AndroidJavaClass("android.content.Context");
                string activityServiceName = contextClass.GetStatic<string>("ACTIVITY_SERVICE");

                using var activityManager = context.Call<AndroidJavaObject>(
                    "getSystemService", activityServiceName);
                if (activityManager == null) return results;

                string packageName = context.Call<string>("getPackageName");

                // Query last 50 exits — most crash storms collapse into a few
                // unique records after dedup. 0 = all available.
                using var exitInfoList = activityManager.Call<AndroidJavaObject>(
                    "getHistoricalProcessExitReasons", packageName, 0, 50);
                if (exitInfoList == null) return results;

                int size = exitInfoList.Call<int>("size");
                for (int i = 0; i < size; i++)
                {
                    using var info = exitInfoList.Call<AndroidJavaObject>("get", i);
                    if (info == null) continue;

                    long ts = info.Call<long>("getTimestamp");
                    if (ts <= sinceTimestampMs) continue;

                    int reason = info.Call<int>("getReason");
                    if (!IsCrashReason(reason)) continue;

                    var record = new NativeCrashRecord
                    {
                        ErrorType = ReasonName(reason),
                        Severity = "crash",
                        TimestampMs = ts,
                        Pid = info.Call<int>("getPid"),
                        ExitStatus = info.Call<int>("getStatus"),
                    };

                    try { record.ProcessName = info.Call<string>("getProcessName"); }
                    catch { record.ProcessName = ""; }

                    try { record.Message = info.Call<string>("getDescription"); }
                    catch { record.Message = ""; }

                    // Trace stream is only populated for ANRs and a few other
                    // reasons. Best-effort read; truncate to avoid huge payloads.
                    record.StackTrace = TryReadTraceStream(info);

                    record.OsReportId = BuildOsReportId(record);

                    results.Add(record);
                }
            }
            catch (Exception e)
            {
                _log.Warning($"Failed to query ApplicationExitInfo: {e.Message}");
            }
#endif

            return results;
        }

        private static bool IsCrashReason(int reason)
        {
            switch (reason)
            {
                case REASON_CRASH_NATIVE:
                case REASON_ANR:
                case REASON_SIGNALED:
                case REASON_LOW_MEMORY:
                case REASON_EXCESSIVE_RESOURCE_USAGE:
                    return true;
                default:
                    return false;
            }
        }

        private static string ReasonName(int reason)
        {
            switch (reason)
            {
                case REASON_CRASH_NATIVE: return "CRASH_NATIVE";
                case REASON_ANR: return "ANR";
                case REASON_SIGNALED: return "SIGNALED";
                case REASON_LOW_MEMORY: return "LOW_MEMORY";
                case REASON_EXCESSIVE_RESOURCE_USAGE: return "EXCESSIVE_RESOURCE_USAGE";
                default: return "REASON_" + reason;
            }
        }

        private static string BuildOsReportId(NativeCrashRecord r)
        {
            // Stable across relaunches: timestamp + pid + reason fully identifies
            // an ApplicationExitInfo entry per Android contract.
            return r.TimestampMs + "-" + r.Pid + "-" + r.ErrorType;
        }

        private static int GetAndroidApiLevel()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var versionClass = new AndroidJavaClass("android.os.Build$VERSION");
                return versionClass.GetStatic<int>("SDK_INT");
            }
            catch { return 0; }
#else
            return 0;
#endif
        }

        private static string TryReadTraceStream(AndroidJavaObject info)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var stream = info.Call<AndroidJavaObject>("getTraceInputStream");
                if (stream == null) return "";

                // Read up to 8 KB. InputStream.read(byte[]) in JNI is awkward; use
                // available() + read byte-by-byte via helper. Simpler: use
                // java.util.Scanner to slurp the stream as text.
                using var scanner = new AndroidJavaObject("java.util.Scanner", stream);
                // `Scanner.useDelimiter` returns `this` — wrapping it in another
                // `using` would double-dispose the same Java global ref.
                scanner.Call<AndroidJavaObject>("useDelimiter", "\\A")?.Dispose();

                bool hasNext = scanner.Call<bool>("hasNext");
                if (!hasNext) return "";

                string text = scanner.Call<string>("next");
                if (string.IsNullOrEmpty(text)) return "";

                return text.Length > 8000 ? text.Substring(0, 8000) : text;
            }
            catch
            {
                return "";
            }
#else
            return "";
#endif
        }
    }
}
