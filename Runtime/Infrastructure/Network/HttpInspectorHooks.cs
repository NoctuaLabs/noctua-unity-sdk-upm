using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Static observer registry fanned out to from <see cref="HttpRequest"/>.
    /// Separate from <see cref="HttpRequest"/> itself so:
    /// <list type="bullet">
    /// <item>Presenter-layer observers (the Inspector) can register without
    ///   pulling Infrastructure into their compile graph; this class is in
    ///   Infrastructure and merely stores delegates.</item>
    /// <item>Unit tests can substitute observers freely.</item>
    /// <item>The hot path in <see cref="HttpRequest.Send{T}"/> is a single
    ///   `_observers.IsEmpty` check when no observer is registered — zero
    ///   allocation, no serialisation work.</item>
    /// </list>
    /// Safe to call before SDK init; observers registered before
    /// <c>Noctua.InitAsync</c> will receive the first requests.
    /// </summary>
    public static class HttpInspectorHooks
    {
        // CopyOnWrite via ConcurrentBag is overkill for a handful of observers;
        // a simple lock + List gives deterministic order and cheap enumeration.
        private static readonly List<IHttpObserver> _observers = new();
        private static readonly object _lock = new();

        public static bool HasObservers
        {
            get { lock (_lock) return _observers.Count > 0; }
        }

        public static void RegisterObserver(IHttpObserver observer)
        {
            if (observer == null) return;
            lock (_lock)
            {
                if (!_observers.Contains(observer)) _observers.Add(observer);
            }
        }

        public static void UnregisterObserver(IHttpObserver observer)
        {
            if (observer == null) return;
            lock (_lock) _observers.Remove(observer);
        }

        public static void FireStart(HttpExchange exchange)
        {
            IHttpObserver[] snapshot = SnapshotOrNull();
            if (snapshot == null) return;
            foreach (var o in snapshot) try { o.OnRequestStart(exchange); } catch { /* swallow */ }
        }

        public static void FireStateChange(Guid id, HttpExchangeState state)
        {
            IHttpObserver[] snapshot = SnapshotOrNull();
            if (snapshot == null) return;
            foreach (var o in snapshot) try { o.OnStateChange(id, state); } catch { /* swallow */ }
        }

        public static void FireEnd(HttpExchange exchange)
        {
            IHttpObserver[] snapshot = SnapshotOrNull();
            if (snapshot == null) return;
            foreach (var o in snapshot) try { o.OnRequestEnd(exchange); } catch { /* swallow */ }
        }

        private static IHttpObserver[] SnapshotOrNull()
        {
            lock (_lock)
            {
                if (_observers.Count == 0) return null;
                return _observers.ToArray();
            }
        }
    }
}
