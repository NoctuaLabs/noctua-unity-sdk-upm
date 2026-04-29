using System;
using UnityEngine;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Bridges Unity's <see cref="Application.logMessageReceivedThreaded"/>
    /// callback to the Inspector log pipeline. Subscribes once on
    /// <see cref="Start"/>, unsubscribes on <see cref="Stop"/>. Called from
    /// any thread by Unity, so emissions go straight into
    /// <see cref="LogInspectorHooks"/> which re-marshals on the main thread.
    ///
    /// Lives in Infrastructure because it depends only on UnityEngine and
    /// emits via the static hooks — no Presenter / View references.
    /// </summary>
    public sealed class UnityLogStream : IDisposable
    {
        private bool _started;

        public void Start()
        {
            if (_started) return;
            Application.logMessageReceivedThreaded += OnUnityLog;
            _started = true;
        }

        public void Stop()
        {
            if (!_started) return;
            Application.logMessageReceivedThreaded -= OnUnityLog;
            _started = false;
        }

        public void Dispose() => Stop();

        private static void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            // Bail early if no observer is listening — saves the GC churn of
            // a LogEntry allocation per Debug.Log in non-sandbox builds where
            // the ledger was never registered.
            if (!LogInspectorHooks.HasObservers) return;

            var entry = new LogEntry(
                timestampUtc: DateTime.UtcNow,
                level:        ToLogLevel(type),
                source:       "Unity",
                tag:          "",
                message:      condition,
                stackTrace:   string.IsNullOrEmpty(stackTrace) ? null : stackTrace);

            LogInspectorHooks.Emit(entry);
        }

        private static LogLevel ToLogLevel(LogType type) => type switch
        {
            LogType.Log       => LogLevel.Info,
            LogType.Warning   => LogLevel.Warning,
            LogType.Error     => LogLevel.Error,
            LogType.Exception => LogLevel.Error,
            LogType.Assert    => LogLevel.Error,
            _                 => LogLevel.Debug,
        };
    }
}
