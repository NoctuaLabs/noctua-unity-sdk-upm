using System.Collections.Generic;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Static registry for <see cref="ILogObserver"/> implementations. Mirrors
    /// <see cref="HttpInspectorHooks"/> — emitters call <see cref="Emit"/>,
    /// observers (the ledger, optional Sentry sink, etc.) register here and
    /// fan out to any number of consumers without coupling layers.
    ///
    /// Every method is thread-safe; <see cref="Emit"/> may be called from any
    /// thread (Unity log callback, native bridge worker, etc.).
    /// </summary>
    public static class LogInspectorHooks
    {
        private static readonly List<ILogObserver> _observers = new();
        private static readonly object _lock = new();

        public static bool HasObservers
        {
            get { lock (_lock) return _observers.Count > 0; }
        }

        public static void RegisterObserver(ILogObserver observer)
        {
            if (observer == null) return;
            lock (_lock)
            {
                if (!_observers.Contains(observer)) _observers.Add(observer);
            }
        }

        public static void UnregisterObserver(ILogObserver observer)
        {
            if (observer == null) return;
            lock (_lock) _observers.Remove(observer);
        }

        public static void Emit(LogEntry entry)
        {
            if (entry == null) return;
            ILogObserver[] snapshot;
            lock (_lock)
            {
                if (_observers.Count == 0) return;
                snapshot = _observers.ToArray();
            }
            foreach (var o in snapshot)
            {
                try { o.OnLog(entry); } catch { /* swallow */ }
            }
        }
    }
}
