using System;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Severity of a captured log line. Integer values are stable across the
    /// C ABI / JNI so native bridges can pass them directly. Mirrors logcat's
    /// priority numbering on Android (Verbose=2…Error=6) — kept the same so
    /// untranslated native priorities slot in without a lookup table.
    /// </summary>
    public enum LogLevel
    {
        Verbose = 2,
        Debug   = 3,
        Info    = 4,
        Warning = 5,
        Error   = 6,
    }

    /// <summary>
    /// One captured log line shown on the Inspector "Logs" tab. Sources:
    ///   * Unity — fed by <see cref="UnityLogStream"/> from
    ///     <c>Application.logMessageReceivedThreaded</c>.
    ///   * iOS / Android — fed by the native log-tail bridge (when enabled
    ///     via <c>INativeLogStream.SetLogStreamEnabled</c>).
    ///
    /// Pure data — no behaviour. Constructed on whichever thread emitted the
    /// line; safe to read from the main thread because all fields are
    /// immutable after construction (<c>readonly</c> via init-only setters).
    /// </summary>
    public sealed class LogEntry
    {
        public Guid     Id          { get; }
        public DateTime TimestampUtc { get; }
        public LogLevel Level       { get; }
        public string   Source      { get; } // "Unity" | "iOS" | "Android" | "Firebase" | "Adjust" | "Facebook" | "Noctua"
        public string   Tag         { get; }
        public string   Message     { get; }
        public string   StackTrace  { get; }

        public LogEntry(
            DateTime timestampUtc,
            LogLevel level,
            string source,
            string tag,
            string message,
            string stackTrace = null)
        {
            Id           = Guid.NewGuid();
            TimestampUtc = timestampUtc;
            Level        = level;
            Source       = source ?? "";
            Tag          = tag ?? "";
            Message      = message ?? "";
            StackTrace   = stackTrace;
        }
    }
}
