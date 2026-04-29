using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Thread-safe ring buffer of <see cref="LogEntry"/> entries for the
    /// Inspector "Logs" tab. Same observer-+-Pump pattern as
    /// <see cref="HttpInspectorLog"/> and <see cref="TrackerDebugMonitor"/>:
    /// log producers (Unity callback, native bridge) call into the registry,
    /// the ledger drains its work queue on the main thread via <see cref="Pump"/>.
    ///
    /// Capped at <see cref="DefaultCapacity"/> entries — oldest dropped on
    /// overflow. The cap is intentionally generous (5000) because games can
    /// produce thousands of log lines per minute and a too-small buffer
    /// makes the tab worthless during a real bug repro.
    /// </summary>
    public class LogInspectorLedger : ILogObserver
    {
        public const int DefaultCapacity = 5000;

        private readonly int _capacity;
        private readonly LinkedList<LogEntry> _order = new();
        private readonly object _lock = new();
        private readonly ConcurrentQueue<LogEntry> _mainThreadWork = new();

        /// <summary>
        /// Fires whenever a new entry is admitted. Always raised on the main
        /// thread by <see cref="Pump"/>. UI throttles its own redraw — this
        /// event fires per-entry, the renderer batches.
        /// </summary>
        public event Action<LogEntry> OnEntry;

        /// <summary>
        /// When false, native log-stream bridge is silenced at the source.
        /// Unity logs continue to flow because they're free; only the
        /// high-volume native logcat/os_log pipe respects this flag.
        /// Defaults true — gated by <see cref="Noctua.IsSandbox"/> upstream.
        /// </summary>
        public bool NativeStreamEnabled { get; set; } = false;

        public LogInspectorLedger(int capacity = DefaultCapacity)
        {
            _capacity = capacity > 0 ? capacity : DefaultCapacity;
        }

        public IReadOnlyList<LogEntry> Snapshot()
        {
            lock (_lock) return _order.ToList();
        }

        public void Clear()
        {
            lock (_lock) _order.Clear();
        }

        // --- ILogObserver: may be called from any thread ---

        public void OnLog(LogEntry entry)
        {
            if (entry == null) return;
            _mainThreadWork.Enqueue(entry);
        }

        /// <summary>Drain pending entries — call from <c>MonoBehaviour.Update</c>.</summary>
        public void Pump()
        {
            while (_mainThreadWork.TryDequeue(out var entry))
            {
                LogEntry admitted = null;
                lock (_lock)
                {
                    _order.AddLast(entry);
                    while (_order.Count > _capacity)
                    {
                        _order.RemoveFirst();
                    }
                    admitted = entry;
                }
                try { OnEntry?.Invoke(admitted); } catch { /* swallow */ }
            }
        }
    }
}
