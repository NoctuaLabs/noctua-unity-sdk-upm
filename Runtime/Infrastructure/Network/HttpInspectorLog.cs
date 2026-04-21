using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Thread-safe ring buffer of <see cref="HttpExchange"/> captures for
    /// the Inspector UI. Drains from a <see cref="ConcurrentQueue{T}"/> so
    /// writes (network layer) never block reads (UI layer) and vice versa.
    ///
    /// Capped at <see cref="Capacity"/> exchanges — oldest dropped on overflow.
    /// </summary>
    public class HttpInspectorLog : IHttpObserver
    {
        public const int Capacity = 100;

        private readonly Dictionary<Guid, HttpExchange> _byId = new();
        private readonly LinkedList<Guid> _order = new();
        private readonly ConcurrentQueue<Action> _mainThreadWork = new();
        private readonly object _lock = new();

        /// <summary>
        /// Fires whenever an exchange transitions or completes.
        /// Always raised on the main thread by <see cref="Pump"/>.
        /// </summary>
        public event Action<HttpExchange> OnExchange;

        public IReadOnlyList<HttpExchange> Snapshot()
        {
            lock (_lock)
            {
                var list = new List<HttpExchange>(_order.Count);
                foreach (var id in _order)
                {
                    if (_byId.TryGetValue(id, out var ex)) list.Add(ex);
                }
                return list;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _byId.Clear();
                _order.Clear();
            }
        }

        // --- IHttpObserver (called from network thread) ---

        public void OnRequestStart(HttpExchange exchange)
        {
            _mainThreadWork.Enqueue(() => Upsert(exchange));
        }

        public void OnStateChange(Guid exchangeId, HttpExchangeState state)
        {
            _mainThreadWork.Enqueue(() =>
            {
                HttpExchange ex;
                lock (_lock) _byId.TryGetValue(exchangeId, out ex);
                if (ex == null) return;
                ex.State = state;
                OnExchange?.Invoke(ex);
            });
        }

        public void OnRequestEnd(HttpExchange exchange)
        {
            _mainThreadWork.Enqueue(() => Upsert(exchange));
        }

        /// <summary>
        /// Drain pending observer work on the main thread. Must be called
        /// from a <c>MonoBehaviour.Update</c> tick by the Inspector controller.
        /// </summary>
        public void Pump()
        {
            while (_mainThreadWork.TryDequeue(out var action))
            {
                try { action(); } catch { /* swallow */ }
            }
        }

        private void Upsert(HttpExchange ex)
        {
            lock (_lock)
            {
                if (!_byId.ContainsKey(ex.Id))
                {
                    _order.AddLast(ex.Id);
                    if (_order.Count > Capacity)
                    {
                        var first = _order.First;
                        _byId.Remove(first.Value);
                        _order.RemoveFirst();
                    }
                }
                _byId[ex.Id] = ex;
            }
            OnExchange?.Invoke(ex);
        }
    }
}
