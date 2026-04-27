using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Accumulator of <see cref="TrackerEmission"/> entries for the Inspector
    /// Trackers tab. Subscribes to <see cref="TrackerObserverRegistry"/>,
    /// correlates transitions by <c>(provider, eventName)</c>, and maintains
    /// a bounded ring buffer of the most recent emissions.
    ///
    /// Thread-safe — native bridge callbacks come in on worker threads; the
    /// monitor enqueues transitions on a <see cref="ConcurrentQueue{T}"/>
    /// that must be drained by <see cref="Pump"/> on the Unity main thread.
    /// </summary>
    public class TrackerDebugMonitor : ITrackerObserver
    {
        public const int DefaultCapacity = 200;

        private readonly int _capacity;
        private readonly LinkedList<TrackerEmission> _order = new();
        private readonly Dictionary<string, Queue<TrackerEmission>> _pendingByKey = new();
        private readonly object _lock = new();
        private readonly ConcurrentQueue<Action> _mainThreadWork = new();

        public event Action<TrackerEmission> OnEmission;

        public TrackerDebugMonitor(int capacity = DefaultCapacity)
        {
            _capacity = capacity;
        }

        public IReadOnlyList<TrackerEmission> Snapshot(string providerFilter = null)
        {
            lock (_lock)
            {
                IEnumerable<TrackerEmission> seq = _order;
                if (!string.IsNullOrEmpty(providerFilter))
                {
                    seq = seq.Where(e => e.Provider == providerFilter);
                }
                return seq.ToList();
            }
        }

        public void Clear()
        {
            lock (_lock) { _order.Clear(); _pendingByKey.Clear(); }
        }

        // --- ITrackerObserver: may be called from any thread ---

        public void OnEvent(
            string provider,
            string eventName,
            IReadOnlyDictionary<string, object> payload,
            IReadOnlyDictionary<string, object> extraParams,
            TrackerEventPhase phase,
            string error)
        {
            _mainThreadWork.Enqueue(() => Apply(provider, eventName, payload, extraParams, phase, error));
        }

        /// <summary>Drain pending observer work — call from <c>MonoBehaviour.Update</c>.</summary>
        public void Pump()
        {
            while (_mainThreadWork.TryDequeue(out var action))
            {
                try { action(); } catch { /* swallow */ }
            }
        }

        private void Apply(
            string provider,
            string eventName,
            IReadOnlyDictionary<string, object> payload,
            IReadOnlyDictionary<string, object> extraParams,
            TrackerEventPhase phase,
            string error)
        {
            var key = $"{provider}:{eventName}";
            TrackerEmission target = null;
            lock (_lock)
            {
                if (phase == TrackerEventPhase.Queued)
                {
                    target = NewEmission(provider, eventName, payload, extraParams, phase);
                    AppendLocked(target);
                    EnqueuePendingLocked(key, target);
                }
                else
                {
                    target = TakePendingLocked(key);
                    if (target == null)
                    {
                        // No prior Queued recorded — create a synthetic entry so
                        // e.g. standalone Acknowledged signals from Adjust still
                        // show up instead of being dropped.
                        target = NewEmission(provider, eventName, payload, extraParams, phase);
                        AppendLocked(target);
                    }
                    target.Phase = phase;
                    target.Error = error;
                    target.History.Add(new TrackerPhaseTransition { Phase = phase, AtUtc = DateTime.UtcNow });
                    if (!phase.IsTerminal()) EnqueuePendingLocked(key, target);
                }
            }
            try { OnEmission?.Invoke(target); } catch { /* swallow */ }
        }

        private TrackerEmission NewEmission(
            string provider, string eventName,
            IReadOnlyDictionary<string, object> payload,
            IReadOnlyDictionary<string, object> extraParams,
            TrackerEventPhase phase)
        {
            var now = DateTime.UtcNow;
            var em = new TrackerEmission
            {
                Id = Guid.NewGuid(),
                Provider = provider ?? "",
                EventName = eventName ?? "",
                CreatedUtc = now,
                Phase = phase,
                Payload = payload,
                ExtraParams = extraParams,
            };
            em.History.Add(new TrackerPhaseTransition { Phase = phase, AtUtc = now });
            return em;
        }

        private void AppendLocked(TrackerEmission em)
        {
            _order.AddLast(em);
            while (_order.Count > _capacity)
            {
                var first = _order.First;
                _order.RemoveFirst();
                // Drop any pending pointer referencing the evicted entry.
                foreach (var kv in _pendingByKey)
                {
                    while (kv.Value.Count > 0 && kv.Value.Peek() == first.Value) kv.Value.Dequeue();
                }
            }
        }

        private void EnqueuePendingLocked(string key, TrackerEmission em)
        {
            if (!_pendingByKey.TryGetValue(key, out var q))
            {
                q = new Queue<TrackerEmission>();
                _pendingByKey[key] = q;
            }
            q.Enqueue(em);
        }

        private TrackerEmission TakePendingLocked(string key)
        {
            if (!_pendingByKey.TryGetValue(key, out var q) || q.Count == 0) return null;
            var em = q.Dequeue();
            if (q.Count == 0) _pendingByKey.Remove(key);
            return em;
        }
    }
}
