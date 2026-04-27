using System.Collections.Generic;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Static registry for <see cref="ITrackerObserver"/> so that native
    /// bridge code and Unity-layer wrappers both fan out to the same sink
    /// without pulling the View layer into either. Separate from
    /// <see cref="TrackerDebugMonitor"/> — the monitor implements
    /// <see cref="ITrackerObserver"/> and registers itself here.
    /// </summary>
    public static class TrackerObserverRegistry
    {
        private static readonly List<ITrackerObserver> _observers = new();
        private static readonly object _lock = new();

        public static bool HasObservers
        {
            get { lock (_lock) return _observers.Count > 0; }
        }

        public static void Register(ITrackerObserver observer)
        {
            if (observer == null) return;
            lock (_lock)
            {
                if (!_observers.Contains(observer)) _observers.Add(observer);
            }
        }

        public static void Unregister(ITrackerObserver observer)
        {
            if (observer == null) return;
            lock (_lock) _observers.Remove(observer);
        }

        public static void Emit(
            string provider,
            string eventName,
            IReadOnlyDictionary<string, object> payload,
            IReadOnlyDictionary<string, object> extraParams,
            TrackerEventPhase phase,
            string error = null)
        {
            ITrackerObserver[] snapshot;
            lock (_lock)
            {
                if (_observers.Count == 0) return;
                snapshot = _observers.ToArray();
            }
            foreach (var o in snapshot)
            {
                try { o.OnEvent(provider, eventName, payload, extraParams, phase, error); }
                catch { /* swallow */ }
            }
        }
    }
}
