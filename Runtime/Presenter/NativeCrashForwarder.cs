using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using com.noctuagames.sdk.Events;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Forwards OS-reported native crashes (SIGSEGV, ANR, abort, OOM kills)
    /// into the Noctua event pipeline as <c>client_error</c> with
    /// <c>source=native</c>. Complements <see cref="GlobalExceptionLogger"/>
    /// which covers managed C# exceptions.
    /// </summary>
    /// <remarks>
    /// Data sources:
    /// <list type="bullet">
    /// <item><b>iOS 14+</b>: MetricKit <c>MXCrashDiagnostic</c> (via <see cref="IosCrashReporter"/>).</item>
    /// <item><b>Android 11+</b>: <c>ActivityManager.getHistoricalProcessExitReasons()</c>
    /// (via <see cref="AndroidNativeCrashReporter"/>).</item>
    /// </list>
    /// Crashes always surface on the NEXT app launch — the OS-provided APIs
    /// deliberately run after the process restarts. Older OS versions are
    /// silently unsupported (no signal-handler fallback).
    ///
    /// Last-seen timestamps are persisted per-platform in PlayerPrefs so the
    /// same crash is never forwarded twice, even across many relaunches.
    /// </remarks>
    public sealed class NativeCrashForwarder
    {
        private static readonly ILogger _log = new NoctuaLogger(typeof(NativeCrashForwarder));

        private const string PrefKeyAndroidLastTsMs = "noctua.nativecrash.android.lastTsMs";
        private const string PrefKeyIosSeenReportIds = "noctua.nativecrash.ios.seenIds";
        private const int MaxStoredIosIds = 64;       // cap PlayerPrefs blob size
        private const int IosDrainIntervalSeconds = 5;

        private readonly IEventSender _eventSender;
        private readonly string _platform;
        private readonly string _appVersion;
        private volatile bool _started;
        private CancellationTokenSource _stopCts;

        public NativeCrashForwarder(IEventSender eventSender)
        {
            _eventSender = eventSender ?? throw new ArgumentNullException(nameof(eventSender));
            _platform = Application.platform.ToString();
            _appVersion = Application.version;
        }

        /// <summary>
        /// Starts forwarding. Idempotent. Safe to call during SDK
        /// composition-root wiring — all OS calls are deferred to a
        /// background task.
        /// </summary>
        public void Start()
        {
            if (_started) return;
            _started = true;
            _stopCts = new CancellationTokenSource();

            UniTask.Void(async () =>
            {
                try
                {
                    await PollAndroidOnceAsync();
                    StartIosSubscription();
                }
                catch (Exception e)
                {
                    _log.Warning($"NativeCrashForwarder.Start failed: {e.Message}");
                }
            });
        }

        /// <summary>Disables forwarding. Used as a runtime kill-switch.</summary>
        public void Stop()
        {
            if (!_started) return;
            _started = false;

            try { _stopCts?.Cancel(); } catch { /* best-effort */ }
            try { _stopCts?.Dispose(); } catch { /* best-effort */ }
            _stopCts = null;

            try { IosCrashReporter.Stop(); } catch { /* best-effort */ }
        }

        // ─── Android ────────────────────────────────────────────────

        private UniTask PollAndroidOnceAsync()
        {
            if (Application.platform != RuntimePlatform.Android) return UniTask.CompletedTask;

            // Run entirely on main thread:
            //   • PlayerPrefs is not thread-safe per Unity docs.
            //   • AndroidJavaClass / JNI is safest on main thread.
            //   • The query is cheap (a few ms) — no benefit to a thread hop.

            long sinceMs;
            try
            {
                var raw = PlayerPrefs.GetString(PrefKeyAndroidLastTsMs, "0");
                if (!long.TryParse(raw, out sinceMs)) sinceMs = 0;
            }
            catch { sinceMs = 0; }

            List<AndroidNativeCrashReporter.NativeCrashRecord> records;
            try
            {
                records = AndroidNativeCrashReporter.PollRecentCrashes(sinceMs);
            }
            catch (Exception e)
            {
                _log.Warning($"Android crash poll failed: {e.Message}");
                return UniTask.CompletedTask;
            }

            if (records == null || records.Count == 0) return UniTask.CompletedTask;

            long maxTs = sinceMs;
            foreach (var r in records)
            {
                if (r.TimestampMs > maxTs) maxTs = r.TimestampMs;
                ForwardAndroidRecord(r);
            }

            try
            {
                PlayerPrefs.SetString(PrefKeyAndroidLastTsMs, maxTs.ToString());
                PlayerPrefs.Save();
            }
            catch { /* best-effort */ }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Internal-by-intent: public only because SDK policy forbids
        /// <c>InternalsVisibleTo</c> (see <c>CLAUDE.md</c> Class Visibility
        /// Rule). Builds a <c>client_error</c> payload from an Android
        /// ApplicationExitInfo record. External callers should not invoke
        /// this — it's the dispatch target for the Android poll loop and
        /// unit tests. Hidden from IntelliSense.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void ForwardAndroidRecord(AndroidNativeCrashReporter.NativeCrashRecord r)
        {
            var payload = new Dictionary<string, IConvertible>
            {
                { "source", "native" },
                { "severity", "crash" },
                { "error_type", r.ErrorType ?? "UnknownCrash" },
                { "message", Truncate(r.Message, 500) },
                { "stack_trace", Truncate(r.StackTrace, 4000) },
                { "thread", "native" },
                { "platform", _platform },
                { "app_version", _appVersion },
                { "timestamp_utc", UnixMsToIso8601(r.TimestampMs) },
                { "os_report_id", r.OsReportId ?? "" },
                { "reported_at_launch", true },
                { "native_pid", r.Pid },
                { "native_exit_status", r.ExitStatus },
                { "native_process", r.ProcessName ?? "" },
                { "dedup_count", 1 },
                { "suppressed_count", 0 }
            };

            SafeSend(payload);
        }

        // ─── iOS ────────────────────────────────────────────────────

        private void StartIosSubscription()
        {
            if (Application.platform != RuntimePlatform.IPhonePlayer) return;

            try
            {
                IosCrashReporter.Start(OnIosCrashPayload);
            }
            catch (Exception e)
            {
                _log.Warning($"IosCrashReporter.Start failed: {e.Message}");
                return;
            }

            // MetricKit dispatches on a background queue; trampoline buffers
            // into a queue that must be drained on the main thread. Loop exits
            // immediately on Stop() via the cancellation token — no 5-second
            // lag between kill-switch and loop teardown.
            var token = _stopCts?.Token ?? CancellationToken.None;
            UniTask.Void(async () =>
            {
                while (_started && !token.IsCancellationRequested)
                {
                    try { IosCrashReporter.DrainPending(); }
                    catch (Exception e) { _log.Debug($"Ios drain error: {e.Message}"); }

                    try
                    {
                        await UniTask.Delay(
                            TimeSpan.FromSeconds(IosDrainIntervalSeconds),
                            cancellationToken: token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            });
        }

        /// <summary>
        /// Internal-by-intent (see <see cref="ForwardAndroidRecord"/> for
        /// rationale). Parses a MetricKit payload JSON string (same shape
        /// emitted by <c>NoctuaCrashReporter.m</c>), dedups against
        /// PlayerPrefs-stored IDs, and forwards as <c>client_error</c>.
        /// Hidden from IntelliSense.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void OnIosCrashPayload(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            JObject obj;
            try { obj = JObject.Parse(json); }
            catch (Exception e)
            {
                _log.Warning($"Ios crash payload parse failed: {e.Message}");
                return;
            }

            var reportId = obj.Value<string>("os_report_id") ?? "";
            if (string.IsNullOrEmpty(reportId))
            {
                reportId = Guid.NewGuid().ToString("N");
            }

            if (HasSeenIosReportId(reportId)) return;
            RememberIosReportId(reportId);

            var payload = new Dictionary<string, IConvertible>
            {
                { "source", "native" },
                { "severity", "crash" },
                { "error_type", obj.Value<string>("error_type") ?? "UnknownCrash" },
                { "message", Truncate(obj.Value<string>("message"), 500) },
                { "stack_trace", Truncate(obj.Value<string>("stack_trace"), 4000) },
                { "thread", "native" },
                { "platform", _platform },
                { "app_version", _appVersion },
                { "timestamp_utc", obj.Value<string>("timestamp_utc") ?? DateTime.UtcNow.ToString("o") },
                { "os_report_id", reportId },
                { "reported_at_launch", true },
                { "native_signal", obj.Value<int?>("signal") ?? 0 },
                { "native_exception_type", obj.Value<int?>("exception_type") ?? 0 },
                { "native_exception_code", obj.Value<int?>("exception_code") ?? 0 },
                { "dedup_count", 1 },
                { "suppressed_count", 0 }
            };

            SafeSend(payload);
        }

        private bool HasSeenIosReportId(string id)
        {
            try
            {
                var stored = PlayerPrefs.GetString(PrefKeyIosSeenReportIds, "");
                if (string.IsNullOrEmpty(stored)) return false;
                // Invariant: iOS os_report_id uses '|' as its internal field
                // separator (see NoctuaCrashReporter.m serializeCrash:) so
                // ',' is safe as the PlayerPrefs list separator here. If the
                // ObjC-side format ever changes, BOTH sides must change.
                // Cheap O(n) scan is fine at n ≤ MaxStoredIosIds.
                var parts = stored.Split(',');
                foreach (var p in parts) if (p == id) return true;
            }
            catch { /* fall through — not seen */ }
            return false;
        }

        private void RememberIosReportId(string id)
        {
            try
            {
                var stored = PlayerPrefs.GetString(PrefKeyIosSeenReportIds, "");
                var list = string.IsNullOrEmpty(stored)
                    ? new List<string>()
                    : new List<string>(stored.Split(','));

                list.Add(id);
                if (list.Count > MaxStoredIosIds)
                {
                    list.RemoveRange(0, list.Count - MaxStoredIosIds);
                }

                PlayerPrefs.SetString(PrefKeyIosSeenReportIds, string.Join(",", list));
                PlayerPrefs.Save();
            }
            catch { /* best-effort */ }
        }

        // ─── Common ─────────────────────────────────────────────────

        private void SafeSend(Dictionary<string, IConvertible> payload)
        {
            try
            {
                _eventSender.Send("client_error", payload);
            }
            catch (Exception e)
            {
                _log.Debug($"client_error (native) send failed: {e.Message}");
            }
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max);
        }

        private static string UnixMsToIso8601(long ms)
        {
            if (ms <= 0) return DateTime.UtcNow.ToString("o");
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime.ToString("o");
        }
    }
}
