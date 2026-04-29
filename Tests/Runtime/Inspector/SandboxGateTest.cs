using System;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime.Inspector
{
    /// <summary>
    /// Sanity-checks the sandbox-gating contract for the new Inspector
    /// monitors (Logs / Performance / Memory). These do NOT boot the
    /// full <see cref="Noctua"/> singleton — booting it requires a real
    /// `noctuagg.json` and would couple the test to network availability
    /// and global side effects. Instead we verify the *building blocks*
    /// that <see cref="Noctua.Initialization"/> wires together:
    ///
    ///   * <see cref="LogInspectorHooks.Emit"/> is a no-op fast-path when
    ///     no observer is registered (the production-build state).
    ///   * <see cref="LogInspectorLedger"/> drains nothing if it never
    ///     received entries.
    ///   * <see cref="MemoryMonitor"/> with no native provider falls back
    ///     to an Empty <see cref="DeviceMetricsSnapshot"/> instead of
    ///     throwing.
    ///   * <see cref="DeviceMetricsSnapshot.Empty"/> populates sentinel
    ///     -1 / Unknown values that the UI knows how to render as "—".
    ///
    /// The integration "sandboxEnabled=false ⇒ Noctua.LogLedger == null"
    /// assertion is exercised by the sample app's smoke test, not here.
    /// </summary>
    public class SandboxGateTest
    {
        [Test]
        public void Hooks_emit_is_noop_with_no_observers()
        {
            // Construct an entry; no observers registered → no exception,
            // no work, no allocation past the LogEntry itself.
            var entry = new LogEntry(DateTime.UtcNow, LogLevel.Info, "Unity", "tag", "msg");
            Assert.DoesNotThrow(() => LogInspectorHooks.Emit(entry));
        }

        [Test]
        public void Ledger_snapshot_is_empty_until_pump_runs()
        {
            var ledger = new LogInspectorLedger();
            ledger.OnLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Unity", "", "x"));
            // Production-build path: nobody calls Pump because the
            // controller GameObject doesn't exist. Snapshot stays empty.
            Assert.AreEqual(0, ledger.Snapshot().Count);
        }

        [Test]
        public void DeviceMetricsSnapshot_Empty_uses_sentinels()
        {
            var s = DeviceMetricsSnapshot.Empty(DateTime.UtcNow);
            Assert.AreEqual(-1L, s.PhysFootprintBytes);
            Assert.AreEqual(-1L, s.AvailableBytes);
            Assert.AreEqual(-1L, s.SystemTotalBytes);
            Assert.IsFalse(s.LowMemory);
            Assert.AreEqual(ThermalState.Unknown, s.Thermal);
        }

        [Test]
        public void DeviceMetricsAdapter_with_null_native_returns_Empty()
        {
            var adapter = new NoctuaDeviceMetricsAdapter(native: null);
            var s = adapter.Snapshot();
            Assert.AreEqual(-1L, s.PhysFootprintBytes);
            Assert.AreEqual(ThermalState.Unknown, s.Thermal);
        }

        [Test]
        public void Ledger_capacity_constructor_clamps_invalid_values()
        {
            var zeroCap = new LogInspectorLedger(capacity: 0);
            // Should fall back to default rather than reject all entries.
            zeroCap.OnLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Unity", "", "a"));
            zeroCap.Pump();
            Assert.AreEqual(1, zeroCap.Snapshot().Count);
        }
    }
}
