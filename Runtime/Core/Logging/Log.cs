using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Xml;
using com.noctuagames.sdk.Events;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// SDK logging interface. All runtime SDK code should use this instead of
    /// <c>UnityEngine.Debug.Log</c> to ensure consistent log formatting and
    /// multi-sink output (file, Sentry, platform-native logcat/os_log).
    /// </summary>
    public interface ILogger
    {
        /// <summary>Logs a debug-level message (verbose, development only).</summary>
        /// <param name="message">The log message.</param>
        /// <param name="caller">Auto-populated caller member name.</param>
        void Debug(string message, [CallerMemberName] string caller = "");

        /// <summary>Logs an informational message.</summary>
        /// <param name="message">The log message.</param>
        /// <param name="caller">Auto-populated caller member name.</param>
        void Info(string message, [CallerMemberName] string caller = "");

        /// <summary>Logs a warning-level message.</summary>
        /// <param name="message">The log message.</param>
        /// <param name="caller">Auto-populated caller member name.</param>
        void Warning(string message, [CallerMemberName] string caller = "");

        /// <summary>Logs an error-level message.</summary>
        /// <param name="message">The log message.</param>
        /// <param name="caller">Auto-populated caller member name.</param>
        void Error(string message, [CallerMemberName] string caller = "");

        /// <summary>Logs an exception with full stack trace at error level.</summary>
        /// <param name="exception">The exception to log.</param>
        /// <param name="caller">Auto-populated caller member name.</param>
        void Exception(Exception exception, [CallerMemberName] string caller = "");
    }
    
    /// <summary>
    /// Default <see cref="ILogger"/> implementation that writes to Serilog sinks
    /// (file, Sentry, and platform-native: Unity console / Android logcat / iOS os_log).
    /// Each instance is scoped to a type name for structured log prefixes.
    /// </summary>
    public class NoctuaLogger : ILogger
    {
        private readonly string _typeName;

        /// <summary>
        /// Initializes the global Serilog logger pipeline with file output,
        /// optional Sentry error reporting, and platform-specific sinks.
        /// Called once during SDK initialization.
        /// </summary>
        /// <param name="globalConfig">The global configuration containing Sentry DSN and other settings.</param>
        public static void Init(GlobalConfig globalConfig)
        {
            var loggerConfig = new LoggerConfiguration();

            if (!string.IsNullOrEmpty(globalConfig?.Noctua?.SentryDsnUrl))
            {
                loggerConfig.WriteTo.Sentry(o =>
                {
                    o.Dsn = globalConfig.Noctua.SentryDsnUrl;
                    o.MinimumEventLevel = LogEventLevel.Error;
                });
            }

            loggerConfig
                .MinimumLevel.Debug()
                .WriteTo.File(
                    Path.Combine(Application.persistentDataPath, $"{Application.productName}-noctua-log.txt"),
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: 4 * 1024 * 1024,
                    retainedFileCountLimit: 8,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] {Message}{NewLine}{Exception}")
        #if UNITY_EDITOR
                .WriteTo.Sink(new UnityLogSink())
        #endif
        #if UNITY_ANDROID && !UNITY_EDITOR
                .WriteTo.Sink(new AndroidLogSink())
        #endif
        #if UNITY_IOS && !UNITY_EDITOR
                .WriteTo.Sink(new IosLogSink())
        #endif
                ;

            Log.Logger = loggerConfig.CreateLogger();
        }

        /// <summary>
        /// Creates a new logger instance scoped to the specified type.
        /// If <paramref name="type"/> is <c>null</c>, the declaring type of the caller is used.
        /// </summary>
        /// <param name="type">The type whose name will prefix all log messages. Defaults to the caller's declaring type.</param>
        public NoctuaLogger(Type type = null)
        {
            if (type == null)
            {
                var stackTrace = new StackTrace();
                var frame = stackTrace.GetFrame(1); // Get the calling method frame
                var method = frame.GetMethod();
                type = method.DeclaringType;
            }
            
            _typeName = type?.Name;
        }

        public void Debug(string message, [CallerMemberName] string memberName = "")
        {
            Log.Debug($"{_typeName}.{memberName}: {message}");
        }

        public void Info(string message, [CallerMemberName] string memberName = "")
        {
            if (message.Length > 800)
            {
                for (int i = 0; i < message.Length; i += 800)
                {
                    var chunk = message.Substring(i, Math.Min(800, message.Length - i));
                    Log.Information($"{_typeName}.{memberName}: {chunk}");
                }
            }
            else
            {
                Log.Information($"{_typeName}.{memberName}: {message}");
            }
        }

        public void Warning(string message, [CallerMemberName] string memberName = "")
        {
            Log.Warning($"{_typeName}.{memberName}: {message}");
        }

        public void Error(string message, [CallerMemberName] string memberName = "")
        {
            Log.Error($"{_typeName}.{memberName}: {message}");
        }
        
        public void Exception(Exception exception, [CallerMemberName] string memberName = "")
        {
            Log.Error(exception, $"{_typeName}.{memberName}: {{ExceptionMessage}}", exception.Message);
        }
    }
    
    /// <summary>
    /// Serilog sink that forwards log events to <c>UnityEngine.Debug.unityLogger</c>.
    /// Used in the Unity Editor only.
    /// </summary>
    public class UnityLogSink : ILogEventSink
    {
        public void Emit(LogEvent logEvent)
        {
            var message = logEvent.RenderMessage();
            var level = logEvent.Level switch
            {
                LogEventLevel.Debug       => LogType.Log,
                LogEventLevel.Information => LogType.Log,
                LogEventLevel.Warning     => LogType.Warning,
                LogEventLevel.Error       => LogType.Error,
                _                         => LogType.Log
            };

            UnityEngine.Debug.unityLogger.Log(level, message);
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>
    /// Serilog sink that writes log events to Android logcat via <c>__android_log_write</c>.
    /// Used on Android devices only.
    /// </summary>
    public class AndroidLogSink : ILogEventSink
    {
        [DllImport("log")]
        private static extern int __android_log_write(int prio, string tag, string msg);

        private const int ANDROID_LOG_DEBUG = 3;
        private const int ANDROID_LOG_INFO = 4;
        private const int ANDROID_LOG_WARN = 5;
        private const int ANDROID_LOG_ERROR = 6;

        public void Emit(LogEvent logEvent)
        {
            var message = logEvent.RenderMessage();
            var level = logEvent.Level switch
            {
                LogEventLevel.Debug       => ANDROID_LOG_DEBUG,
                LogEventLevel.Information => ANDROID_LOG_INFO,
                LogEventLevel.Warning     => ANDROID_LOG_WARN,
                LogEventLevel.Error       => ANDROID_LOG_ERROR,
                _                         => ANDROID_LOG_DEBUG
            };

            __android_log_write(level, "NoctuaSDK", message);
        }
    }
#endif
    
#if UNITY_IOS && !UNITY_EDITOR
    /// <summary>
    /// Serilog sink that writes log events to the Apple unified logging system (os_log).
    /// Used on iOS devices only.
    /// </summary>
    public class IosLogSink : ILogEventSink
    {
        [DllImport("__Internal")]
        public static extern IntPtr os_log_create(string subsystem, string category);

        /// <summary>Apple os_log severity levels.</summary>
        public enum OSLogType : byte
        {
            Default = 0,
            Info = 1,
            Debug = 2,
            Error = 16,
            Fault = 17
        }

        [DllImport("__Internal")]
        public static extern void OsLogWithType(IntPtr log, OSLogType type, string message);    
        
        private readonly IntPtr _log = os_log_create("com.noctuagames.sdk", "NoctuaSDK");


        public void Emit(LogEvent logEvent)
        {
            var message = logEvent.RenderMessage();
            
            var level = logEvent.Level switch
            {
                LogEventLevel.Debug       => OSLogType.Debug,
                LogEventLevel.Information => OSLogType.Info,
                LogEventLevel.Warning     => OSLogType.Default,
                LogEventLevel.Error       => OSLogType.Error,
                _                         => OSLogType.Default
            };
            
            OsLogWithType(_log, level, message);
        }
    }
#endif

    /// <summary>
    /// Subscribes to Unity's exception hooks (main + threaded log messages and
    /// <see cref="AppDomain.UnhandledException"/>) and forwards Warning/Error/Exception
    /// logs to the Noctua event pipeline as a single <c>client_error</c> event.
    /// Also logs exceptions via <see cref="ILogger"/> for Serilog/Sentry sinks.
    /// Safe to call from any thread — forwarding is allocation-light, rate-limited,
    /// deduplicated, and never rethrows.
    /// </summary>
    public class GlobalExceptionLogger : MonoBehaviour
    {
        private readonly ILogger _log = new NoctuaLogger(typeof(GlobalExceptionLogger));
        private IEventSender _eventSender;

        // Dedup + rate-limit state
        private readonly ConcurrentDictionary<string, DedupEntry> _dedup = new();
        private long _windowStartMs;
        private int _windowCount;
        private int _suppressedCounter;

        // Cached main-thread-only values for use from background threads
        private string _cachedScene = "";
        private string _cachedPlatform = "";
        private string _cachedAppVersion = "";

        [ThreadStatic] private static bool _suppressForwarding;

        private const int RateLimitPerMinute = 30;
        private const int DedupWindowMs = 60_000;
        private const int MaxDedupEntries = 256;
        private const int MaxMessageChars = 500;
        private const int MaxStackTraceChars = 4000;
        private const int StackHeadForKey = 200;

        private sealed class DedupEntry
        {
            public long LastSeenMs;
            public int Count;
        }

        /// <summary>
        /// Sets the event sender used to forward errors. Pass <c>null</c> to
        /// disable forwarding (useful as a runtime kill-switch).
        /// </summary>
        public void SetEventSender(IEventSender eventSender)
        {
            _eventSender = eventSender;
        }

        void Awake()
        {
            _cachedScene = SceneManager.GetActiveScene().name;
            _cachedPlatform = Application.platform.ToString();
            _cachedAppVersion = Application.version;

            Application.logMessageReceived += HandleLog;
            AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
            Application.logMessageReceivedThreaded += HandleLogThreaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= HandleLog;
            AppDomain.CurrentDomain.UnhandledException -= HandleUnhandledException;
            Application.logMessageReceivedThreaded -= HandleLogThreaded;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _cachedScene = scene.name;
        }

        /// <summary>
        /// Internal-by-intent: public only because SDK policy forbids
        /// <c>InternalsVisibleTo</c> (see <c>Packages/com.noctuagames.sdk/CLAUDE.md</c>
        /// &ldquo;Class Visibility Rule&rdquo;). The event dispatcher calls
        /// this; external callers should not. Hidden from IntelliSense.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void HandleLog(string logString, string stackTrace, LogType type)
        {
            // Guard set at handler entry so that `_log.Error` below — which in
            // the Editor re-enters `Application.logMessageReceived` via
            // UnityLogSink — can't recurse into another forward.
            if (_suppressForwarding) return;
            _suppressForwarding = true;
            try
            {
                if (!ShouldForward(type)) return;

                if (type == LogType.Exception)
                {
                    _log.Error($"{logString}\n{stackTrace}");
                }

                ForwardToEvents(
                    errorType: ExtractType(logString, type),
                    message: logString,
                    stackTrace: stackTrace,
                    severity: SeverityOf(type),
                    thread: "main",
                    scene: SceneManager.GetActiveScene().name);
            }
            catch { /* never rethrow into Unity's logger */ }
            finally { _suppressForwarding = false; }
        }

        /// <summary>
        /// Internal-by-intent (see <see cref="HandleLog"/> for rationale).
        /// Wired to <see cref="Application.logMessageReceivedThreaded"/>.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void HandleLogThreaded(string logString, string stackTrace, LogType type)
        {
            if (_suppressForwarding) return;
            _suppressForwarding = true;
            try
            {
                if (!ShouldForward(type)) return;

                if (type == LogType.Exception)
                {
                    _log.Error($"{logString}\n{stackTrace}");
                }

                ForwardToEvents(
                    errorType: ExtractType(logString, type),
                    message: logString,
                    stackTrace: stackTrace,
                    severity: SeverityOf(type),
                    thread: "background",
                    scene: _cachedScene);
            }
            catch { /* never rethrow */ }
            finally { _suppressForwarding = false; }
        }

        /// <summary>
        /// Internal-by-intent (see <see cref="HandleLog"/> for rationale).
        /// Wired to <see cref="AppDomain.UnhandledException"/>.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void HandleUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (_suppressForwarding) return;
            _suppressForwarding = true;
            try
            {
                var ex = e.ExceptionObject as Exception;
                if (ex == null) return;

                _log.Exception(ex);

                ForwardToEvents(
                    errorType: ex.GetType().Name,
                    message: ex.Message,
                    stackTrace: ex.StackTrace,
                    severity: "error",
                    thread: "unhandled",
                    scene: _cachedScene);
            }
            catch { /* never rethrow */ }
            finally { _suppressForwarding = false; }
        }

        private static bool ShouldForward(LogType type)
        {
            return type == LogType.Exception
                || type == LogType.Error
                || type == LogType.Warning;
        }

        private static string SeverityOf(LogType type)
        {
            switch (type)
            {
                case LogType.Exception: return "exception";
                case LogType.Error:     return "error";
                case LogType.Warning:   return "warning";
                default:                return "error";
            }
        }

        private void ForwardToEvents(
            string errorType,
            string message,
            string stackTrace,
            string severity,
            string thread,
            string scene)
        {
            if (_eventSender == null) return;
            // Note: re-entrancy guard is applied at the handler entry
            // (HandleLog / HandleLogThreaded / HandleUnhandledException), not
            // here. That way, the handler's own `_log.Error(...)` — which in
            // the Editor re-enters logMessageReceived via UnityLogSink — is
            // also suppressed.

            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Sliding 60s rate-limit window.
            //
            // Race window: the two Exchange calls below are not atomic as a
            // pair, so between them another thread can Increment _windowCount
            // against the fresh _windowStartMs with the stale pre-reset count.
            // Drift is bounded to ≤1 extra increment per concurrent hit, well
            // under the 30/min cap — accepting this in exchange for lock-free
            // fast-path cost. A CAS loop would fix it at the price of retries
            // during exception storms, which is the path we want cheapest.
            var windowStart = Interlocked.Read(ref _windowStartMs);
            if (nowMs - windowStart > 60_000)
            {
                Interlocked.Exchange(ref _windowStartMs, nowMs);
                Interlocked.Exchange(ref _windowCount, 0);
            }

            if (Interlocked.Increment(ref _windowCount) > RateLimitPerMinute)
            {
                Interlocked.Increment(ref _suppressedCounter);
                return;
            }

            // Dedup by (errorType, message, stack-head) within a 60s window.
            //
            // Eviction is intentionally blunt: on overflow we wipe the whole
            // dict, which means a subsequent crash-storm with >256 distinct
            // error signatures loses dedup state for the recently-seen subset
            // and re-emits each once more. That's acceptable here because the
            // 30/min rate-limit above is the dominant throttle — a true LRU
            // would add bookkeeping cost to the hot path for a scenario
            // rate-limit already bounds. Revisit if observed event volume
            // during storms exceeds the rate-limit cap.
            if (_dedup.Count > MaxDedupEntries) _dedup.Clear();

            var stack = stackTrace ?? "";
            var keyStackHead = stack.Length <= StackHeadForKey
                ? stack
                : stack.Substring(0, StackHeadForKey);
            var key = errorType + "|" + message + "|" + keyStackHead;

            var dedupCountOnFirst = 1;
            var isFirstInWindow = false;
            _dedup.AddOrUpdate(
                key,
                _ =>
                {
                    isFirstInWindow = true;
                    return new DedupEntry { LastSeenMs = nowMs, Count = 1 };
                },
                (_, existing) =>
                {
                    if (nowMs - existing.LastSeenMs < DedupWindowMs)
                    {
                        existing.Count++;
                        existing.LastSeenMs = nowMs;
                        return existing;
                    }

                    // Window expired — the accumulated count from the previous
                    // window is reported on this emission, and the entry resets.
                    isFirstInWindow = true;
                    dedupCountOnFirst = existing.Count;
                    existing.LastSeenMs = nowMs;
                    existing.Count = 1;
                    return existing;
                });

            if (!isFirstInWindow) return;

            var suppressed = Interlocked.Exchange(ref _suppressedCounter, 0);

            var payload = new Dictionary<string, IConvertible>
            {
                { "source", "managed" },
                { "error_type", errorType ?? "Unknown" },
                { "message", Truncate(message, MaxMessageChars) },
                { "stack_trace", Truncate(stack, MaxStackTraceChars) },
                { "severity", severity },
                { "thread", thread },
                { "scene", scene ?? "" },
                { "platform", _cachedPlatform },
                { "app_version", _cachedAppVersion },
                { "timestamp_utc", DateTime.UtcNow.ToString("o") },
                { "dedup_count", dedupCountOnFirst },
                { "suppressed_count", suppressed }
            };

            try
            {
                _eventSender.Send("client_error", payload);
            }
            catch (Exception sendEx)
            {
                // Use Debug-level to avoid re-entering logMessageReceived via
                // an error-level log.
                _log.Debug("Failed to forward client_error event: " + sendEx.Message);
            }
        }

        /// <summary>
        /// Extracts the exception class name from Unity's exception log format.
        /// Example: <c>"NullReferenceException: Object reference..."</c>
        /// → <c>"NullReferenceException"</c>. Falls back to the LogType name.
        /// </summary>
        private static string ExtractType(string logString, LogType type)
        {
            if (type == LogType.Exception && !string.IsNullOrEmpty(logString))
            {
                var colonIdx = logString.IndexOf(':');
                if (colonIdx > 0 && colonIdx < 128)
                {
                    var head = logString.Substring(0, colonIdx);
                    // Guard: reject multi-word heads (likely not a type name)
                    if (!head.Contains(" ")) return head;
                }
            }

            switch (type)
            {
                case LogType.Exception: return "Exception";
                case LogType.Error:     return "Error";
                case LogType.Warning:   return "Warning";
                default:                return "Log";
            }
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max);
        }
    }
}