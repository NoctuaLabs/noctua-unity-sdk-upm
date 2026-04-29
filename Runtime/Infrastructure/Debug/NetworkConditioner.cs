using System;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Static fault-injection layer between <see cref="HttpRequest"/>
    /// and <c>UnityWebRequest.SendWebRequest()</c>. Sandbox-only —
    /// the Inspector's HTTP tab toggles modes; production builds never
    /// touch the API and the fast-path adds one read of an enum field.
    ///
    /// Modes:
    ///   * <see cref="NetworkMode.Normal"/>   — passthrough (default).
    ///   * <see cref="NetworkMode.Slow3G"/>   — adds <see cref="Slow3GLatencyMs"/>
    ///     pre-request latency, simulates a flaky-but-not-broken network.
    ///   * <see cref="NetworkMode.Offline"/>  — every request fails fast
    ///     with a connection error (no real network call).
    ///   * <see cref="NetworkMode.PacketLoss"/> — drops a configurable
    ///     percentage of requests (<see cref="PacketLossPercent"/>);
    ///     the rest pass through unmodified.
    ///
    /// All public state is `volatile` / interlocked so the network
    /// thread can read without locking. The conditioner does not retain
    /// any per-request state — it is a pure decision oracle.
    /// </summary>
    public static class NetworkConditioner
    {
        /// <summary>Current simulation mode. Defaults to <see cref="NetworkMode.Normal"/>.</summary>
        public static volatile NetworkMode Mode = NetworkMode.Normal;

        /// <summary>Latency injected per request in <see cref="NetworkMode.Slow3G"/>. Default 200 ms.</summary>
        public static volatile int Slow3GLatencyMs = 200;

        /// <summary>Drop probability in <see cref="NetworkMode.PacketLoss"/>. Default 30 %.</summary>
        public static volatile int PacketLossPercent = 30;

        // Thread-safe RNG for drop decisions. ThreadLocal because System.Random
        // isn't thread-safe and the network layer fires from worker threads.
        private static readonly System.Threading.ThreadLocal<Random> _rng =
            new(() => new Random(Environment.TickCount ^ System.Threading.Thread.CurrentThread.ManagedThreadId));

        /// <summary>
        /// Apply pre-flight fault injection. Called by HTTP layer
        /// immediately before <c>SendWebRequest()</c>. Throws to short-
        /// circuit when the conditioner decides a request should fail.
        /// </summary>
        public static async UniTask ApplyAsync()
        {
            switch (Mode)
            {
                case NetworkMode.Normal:
                    return;

                case NetworkMode.Offline:
                    // Throw the same exception shape Http.cs converts to
                    // RequestConnectionError so callers see real-world
                    // offline behaviour, not a synthetic SDK error.
                    throw new NetworkConditionerException("offline (Inspector simulator)");

                case NetworkMode.Slow3G:
                    var ms = Slow3GLatencyMs;
                    if (ms > 0) await UniTask.Delay(ms);
                    return;

                case NetworkMode.PacketLoss:
                    var pct = PacketLossPercent;
                    if (pct > 0 && _rng.Value.Next(100) < pct)
                    {
                        throw new NetworkConditionerException(
                            $"packet-loss drop (Inspector simulator, {pct}%)");
                    }
                    return;
            }
        }
    }

    public enum NetworkMode
    {
        Normal     = 0,
        Slow3G     = 1,
        Offline    = 2,
        PacketLoss = 3,
    }

    /// <summary>
    /// Thrown by the conditioner to short-circuit a request. Caught by
    /// <see cref="HttpRequest"/> which surfaces it as a connection error
    /// — same shape callers see from real network failures.
    /// </summary>
    public sealed class NetworkConditionerException : Exception
    {
        public NetworkConditionerException(string message) : base(message) { }
    }
}
