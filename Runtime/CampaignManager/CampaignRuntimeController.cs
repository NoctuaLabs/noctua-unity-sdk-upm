using System;
using System.Collections.Generic;
using System.Threading;

namespace com.noctuagames.sdk.Campaign
{
    /// <summary>
    /// Owns the per-render runtime bits of a campaign view — countdown tickers, carousel
    /// auto-advance loops, and responsive re-layout callbacks. The presenter creates one per
    /// <c>Show</c> and calls <see cref="Dispose"/> on <c>Close</c>, which cancels every loop
    /// and unregisters every callback so nothing leaks after the popup is gone.
    /// </summary>
    public sealed class CampaignRuntimeController : IDisposable
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly List<Action> _teardowns = new List<Action>();
        private bool _disposed;

        /// <summary>Cancellation token every timer loop should observe.</summary>
        public CancellationToken Token => _cts.Token;

        /// <summary>True once <see cref="Dispose"/> has run.</summary>
        public bool IsDisposed => _disposed;

        /// <summary>Registers a teardown callback run (once) on <see cref="Dispose"/>.</summary>
        public void OnDispose(Action teardown)
        {
            if (teardown == null) return;

            if (_disposed)
            {
                SafeInvoke(teardown);
                return;
            }

            _teardowns.Add(teardown);
        }

        /// <summary>Cancels all loops and runs all registered teardowns. Idempotent.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try { _cts.Cancel(); } catch { /* ignore */ }

            for (var i = _teardowns.Count - 1; i >= 0; i--)
            {
                SafeInvoke(_teardowns[i]);
            }

            _teardowns.Clear();
            _cts.Dispose();
        }

        private static void SafeInvoke(Action a)
        {
            try { a(); } catch { /* teardown must never throw */ }
        }
    }
}
