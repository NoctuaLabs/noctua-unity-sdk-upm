using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// P/Invoke bridge to <c>NoctuaCrashReporter.m</c>. Registers an
    /// MXMetricManagerSubscriber (iOS 14+) and surfaces each MXCrashDiagnostic
    /// as a UTF-8 JSON payload on the managed side.
    /// </summary>
    /// <remarks>
    /// MetricKit delivers payloads asynchronously — typically on the NEXT app
    /// launch after a crash. The callback is a single static field (see
    /// MonoPInvokeCallback pitfall in <c>IosPlugin.cs</c>); only one subscriber
    /// can receive diagnostics at a time. For this SDK, that subscriber is
    /// always <see cref="NativeCrashForwarder"/>.
    /// </remarks>
    public static class IosCrashReporter
    {
        /// <summary>Delegate matching <c>NoctuaNativeCrashCallback</c> in the ObjC header.</summary>
        public delegate void NativeCrashCallbackDelegate(string jsonPayload);

        private static NativeCrashCallbackDelegate _managedCallback;

        // Thread-safe buffer: MetricKit can deliver on a background queue.
        // The C ABI callback fans into this queue; the managed forwarder drains
        // it on the main thread inside its polling loop.
        private static readonly ConcurrentQueue<string> _pendingPayloads = new();

        /// <summary>
        /// Registers the MetricKit subscriber. Safe to call multiple times —
        /// later calls replace the managed callback.
        /// </summary>
        public static void Start(NativeCrashCallbackDelegate managedCallback)
        {
            _managedCallback = managedCallback;

#if UNITY_IOS && !UNITY_EDITOR
            noctuaStartNativeCrashReporter(StaticTrampoline);
#endif
        }

        /// <summary>Unregisters the MetricKit subscriber.</summary>
        public static void Stop()
        {
            _managedCallback = null;

#if UNITY_IOS && !UNITY_EDITOR
            noctuaStopNativeCrashReporter();
#endif
        }

        /// <summary>
        /// Drains any diagnostic payloads that arrived on background threads.
        /// Call from the main thread (e.g. <c>MonoBehaviour.Update</c>).
        /// </summary>
        public static void DrainPending()
        {
            while (_pendingPayloads.TryDequeue(out var json))
            {
                _managedCallback?.Invoke(json);
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        [AOT.MonoPInvokeCallback(typeof(CTrampoline))]
        private static void StaticTrampoline(string jsonPayload)
        {
            try
            {
                if (string.IsNullOrEmpty(jsonPayload)) return;
                _pendingPayloads.Enqueue(jsonPayload);
            }
            catch
            {
                // Never propagate — MetricKit may call from background queues.
            }
        }

        private delegate void CTrampoline(string jsonPayload);

        [DllImport("__Internal")]
        private static extern void noctuaStartNativeCrashReporter(CTrampoline callback);

        [DllImport("__Internal")]
        private static extern void noctuaStopNativeCrashReporter();
#else
        private static void StaticTrampoline(string jsonPayload)
        {
            // Editor/non-iOS stub — exists so [AOT.MonoPInvokeCallback] attribute
            // compiles on all platforms.
        }
#endif
    }
}
