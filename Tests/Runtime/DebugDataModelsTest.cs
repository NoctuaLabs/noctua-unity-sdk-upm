using System;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// Unit tests for debug/inspector pure-data types:
    ///   * <see cref="LogEntry"/> — constructor, field access, null coercion
    ///   * <see cref="LogLevel"/> — enum ordinals
    ///   * <see cref="PerformanceSample"/> — readonly struct constructor
    ///   * <see cref="MemorySample"/> — readonly struct constructor
    ///   * <see cref="DeviceMetricsSnapshot"/> — constructor, Empty() factory, ThermalState enum
    ///   * <see cref="BuildSanityInfo"/> — default values, property setters
    /// </summary>
    [TestFixture]
    public class DebugDataModelsTest
    {
        // ─── LogLevel enum ────────────────────────────────────────────────────

        [Test]
        public void LogLevel_Ordinals_MatchLogcatPriorities()
        {
            Assert.AreEqual(2, (int)LogLevel.Verbose);
            Assert.AreEqual(3, (int)LogLevel.Debug);
            Assert.AreEqual(4, (int)LogLevel.Info);
            Assert.AreEqual(5, (int)LogLevel.Warning);
            Assert.AreEqual(6, (int)LogLevel.Error);
        }

        [Test]
        public void LogLevel_Count_IsFive()
        {
            Assert.AreEqual(5, Enum.GetValues(typeof(LogLevel)).Length);
        }

        // ─── LogEntry ─────────────────────────────────────────────────────────

        [Test]
        public void LogEntry_Ctor_SetsAllFields()
        {
            var ts = new DateTime(2024, 3, 15, 10, 0, 0, DateTimeKind.Utc);

            var entry = new LogEntry(
                timestampUtc: ts,
                level:        LogLevel.Warning,
                source:       "Unity",
                tag:          "NoctuaAuth",
                message:      "token refresh failed",
                stackTrace:   "at Foo.Bar()");

            Assert.AreEqual(ts,                     entry.TimestampUtc);
            Assert.AreEqual(LogLevel.Warning,        entry.Level);
            Assert.AreEqual("Unity",                entry.Source);
            Assert.AreEqual("NoctuaAuth",           entry.Tag);
            Assert.AreEqual("token refresh failed", entry.Message);
            Assert.AreEqual("at Foo.Bar()",         entry.StackTrace);
        }

        [Test]
        public void LogEntry_Id_IsUniquePerInstance()
        {
            var ts = DateTime.UtcNow;
            var a  = new LogEntry(ts, LogLevel.Info, "src", "tag", "msg");
            var b  = new LogEntry(ts, LogLevel.Info, "src", "tag", "msg");

            Assert.AreNotEqual(a.Id, b.Id, "Each LogEntry should have a unique GUID");
        }

        [Test]
        public void LogEntry_NullSourceAndTag_CoercedToEmpty()
        {
            var entry = new LogEntry(DateTime.UtcNow, LogLevel.Debug, null, null, "msg");

            Assert.AreEqual("", entry.Source);
            Assert.AreEqual("", entry.Tag);
        }

        [Test]
        public void LogEntry_NullMessage_CoercedToEmpty()
        {
            var entry = new LogEntry(DateTime.UtcNow, LogLevel.Error, "src", "tag", null);

            Assert.AreEqual("", entry.Message);
        }

        [Test]
        public void LogEntry_NullStackTrace_RemainsNull()
        {
            var entry = new LogEntry(DateTime.UtcNow, LogLevel.Info, "src", "tag", "msg");

            Assert.IsNull(entry.StackTrace, "Optional stack trace should remain null when not provided");
        }

        [Test]
        public void LogEntry_WithStackTrace_Stored()
        {
            const string trace = "at SomeClass.Method() in SomeFile.cs:42";
            var entry = new LogEntry(DateTime.UtcNow, LogLevel.Error, "src", "tag", "err", trace);

            Assert.AreEqual(trace, entry.StackTrace);
        }

        // ─── PerformanceSample ────────────────────────────────────────────────

        [Test]
        public void PerformanceSample_Ctor_SetsAllFields()
        {
            var ts = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var sample = new PerformanceSample(
                timestampUtc:       ts,
                deltaSeconds:       0.016667f,
                fpsInstant:         60f,
                fpsAvg1s:           59.5f,
                fpsAvg5s:           58f,
                frameTimeMs:        16.667f,
                frameTimeP95Ms:     18f,
                droppedFrames30Hz:  2,
                droppedFrames60Hz:  5,
                gpuFrameTimeMs:     12f,
                cpuMainThreadMs:    4f,
                cpuRenderThreadMs:  2.5f);

            Assert.AreEqual(ts,      sample.TimestampUtc);
            Assert.AreEqual(0.016667f, sample.DeltaSeconds, delta: 0.00001f);
            Assert.AreEqual(60f,     sample.FpsInstant,     delta: 0.01f);
            Assert.AreEqual(59.5f,   sample.FpsAvg1s,       delta: 0.01f);
            Assert.AreEqual(58f,     sample.FpsAvg5s,       delta: 0.01f);
            Assert.AreEqual(16.667f, sample.FrameTimeMs,    delta: 0.001f);
            Assert.AreEqual(18f,     sample.FrameTimeP95Ms, delta: 0.01f);
            Assert.AreEqual(2,       sample.DroppedFrames30Hz);
            Assert.AreEqual(5,       sample.DroppedFrames60Hz);
            Assert.AreEqual(12f,     sample.GpuFrameTimeMs,      delta: 0.01f);
            Assert.AreEqual(4f,      sample.CpuMainThreadMs,     delta: 0.01f);
            Assert.AreEqual(2.5f,    sample.CpuRenderThreadMs,   delta: 0.01f);
        }

        [Test]
        public void PerformanceSample_DefaultGpuCpu_IsMinusOne()
        {
            var sample = new PerformanceSample(
                DateTime.UtcNow, 0.033f, 30f, 30f, 30f, 33f, 40f, 0, 0);

            Assert.AreEqual(-1f, sample.GpuFrameTimeMs,    delta: 0.001f,
                "GPU frame time should default to -1 when platform doesn't expose it");
            Assert.AreEqual(-1f, sample.CpuMainThreadMs,   delta: 0.001f);
            Assert.AreEqual(-1f, sample.CpuRenderThreadMs, delta: 0.001f);
        }

        // ─── ThermalState enum ────────────────────────────────────────────────

        [Test]
        public void ThermalState_Ordinals_AreCorrect()
        {
            Assert.AreEqual(-1, (int)ThermalState.Unknown);
            Assert.AreEqual(0,  (int)ThermalState.Nominal);
            Assert.AreEqual(1,  (int)ThermalState.Fair);
            Assert.AreEqual(2,  (int)ThermalState.Serious);
            Assert.AreEqual(3,  (int)ThermalState.Critical);
        }

        // ─── DeviceMetricsSnapshot ────────────────────────────────────────────

        [Test]
        public void DeviceMetricsSnapshot_Ctor_SetsAllFields()
        {
            var ts = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);

            var snap = new DeviceMetricsSnapshot(
                timestampUtc:       ts,
                physFootprintBytes: 150_000_000L,
                availableBytes:     2_000_000_000L,
                systemTotalBytes:   4_000_000_000L,
                lowMemory:          false,
                thermal:            ThermalState.Fair);

            Assert.AreEqual(ts,                  snap.TimestampUtc);
            Assert.AreEqual(150_000_000L,        snap.PhysFootprintBytes);
            Assert.AreEqual(2_000_000_000L,      snap.AvailableBytes);
            Assert.AreEqual(4_000_000_000L,      snap.SystemTotalBytes);
            Assert.IsFalse(snap.LowMemory);
            Assert.AreEqual(ThermalState.Fair,   snap.Thermal);
        }

        [Test]
        public void DeviceMetricsSnapshot_Empty_HasSentinelValues()
        {
            var now  = DateTime.UtcNow;
            var snap = DeviceMetricsSnapshot.Empty(now);

            Assert.AreEqual(now,                snap.TimestampUtc);
            Assert.AreEqual(-1L,                snap.PhysFootprintBytes);
            Assert.AreEqual(-1L,                snap.AvailableBytes);
            Assert.AreEqual(-1L,                snap.SystemTotalBytes);
            Assert.IsFalse(snap.LowMemory);
            Assert.AreEqual(ThermalState.Unknown, snap.Thermal);
        }

        [Test]
        public void DeviceMetricsSnapshot_LowMemoryTrue_Stored()
        {
            var snap = new DeviceMetricsSnapshot(
                DateTime.UtcNow, -1, -1, -1, lowMemory: true, ThermalState.Critical);

            Assert.IsTrue(snap.LowMemory);
            Assert.AreEqual(ThermalState.Critical, snap.Thermal);
        }

        // ─── MemorySample ─────────────────────────────────────────────────────

        [Test]
        public void MemorySample_Ctor_SetsAllFields()
        {
            var ts     = new DateTime(2024, 9, 10, 8, 0, 0, DateTimeKind.Utc);
            var native = DeviceMetricsSnapshot.Empty(ts);

            var sample = new MemorySample(
                timestampUtc:        ts,
                monoUsedBytes:       20_000_000L,
                monoHeapBytes:       40_000_000L,
                unityAllocatedBytes: 80_000_000L,
                unityReservedBytes:  100_000_000L,
                gcTotalBytes:        18_000_000L,
                assetCacheBytes:     5_000_000L,
                native:              native);

            Assert.AreEqual(ts,              sample.TimestampUtc);
            Assert.AreEqual(20_000_000L,     sample.MonoUsedBytes);
            Assert.AreEqual(40_000_000L,     sample.MonoHeapBytes);
            Assert.AreEqual(80_000_000L,     sample.UnityAllocatedBytes);
            Assert.AreEqual(100_000_000L,    sample.UnityReservedBytes);
            Assert.AreEqual(18_000_000L,     sample.GcTotalBytes);
            Assert.AreEqual(5_000_000L,      sample.AssetCacheBytes);
            Assert.AreEqual(native.TimestampUtc, sample.Native.TimestampUtc);
        }

        // ─── BuildSanityInfo ──────────────────────────────────────────────────

        [Test]
        public void BuildSanityInfo_Defaults_AreEmptyStringsAndSentinels()
        {
            var info = new BuildSanityInfo();

            Assert.AreEqual("", info.UnitySdkVersion);
            Assert.AreEqual("", info.NativeSdkVersion);
            Assert.AreEqual("", info.BundleId);
            Assert.AreEqual("", info.AppVersion);
            Assert.AreEqual("", info.UnityVersion);
            Assert.AreEqual("", info.ConfigChecksum);
            Assert.AreEqual("", info.AdjustAppTokenMasked);
            Assert.AreEqual("", info.FirebaseProjectId);
            Assert.AreEqual("", info.Region);
            Assert.AreEqual("", info.RawConfigJson);
            Assert.IsFalse(info.GoogleServicesPresent);
            Assert.IsFalse(info.IsSandbox);
            Assert.AreEqual(-1, info.SkAdNetworksCount,        "Should default to -1 (not applicable)");
            Assert.AreEqual(-1, info.AndroidPermissionsCount,   "Should default to -1 (not applicable)");
        }

        [Test]
        public void BuildSanityInfo_PropertySetters_Work()
        {
            var info = new BuildSanityInfo
            {
                UnitySdkVersion         = "0.92.0",
                NativeSdkVersion        = "1.2.3",
                BundleId                = "com.example.game",
                AppVersion              = "2.0.1",
                UnityVersion            = "2021.3.29f1",
                ConfigChecksum          = "abc123",
                AdjustAppTokenMasked    = "…efgh12",
                FirebaseProjectId       = "my-firebase-project",
                GoogleServicesPresent   = true,
                SkAdNetworksCount       = 15,
                AndroidPermissionsCount = 8,
                IsSandbox               = true,
                Region                  = "sg",
                RawConfigJson           = "{\"clientId\":\"test\"}",
            };

            Assert.AreEqual("0.92.0",               info.UnitySdkVersion);
            Assert.AreEqual("1.2.3",                info.NativeSdkVersion);
            Assert.AreEqual("com.example.game",     info.BundleId);
            Assert.AreEqual("2.0.1",                info.AppVersion);
            Assert.AreEqual("2021.3.29f1",          info.UnityVersion);
            Assert.AreEqual("abc123",               info.ConfigChecksum);
            Assert.AreEqual("…efgh12",              info.AdjustAppTokenMasked);
            Assert.AreEqual("my-firebase-project",  info.FirebaseProjectId);
            Assert.IsTrue(info.GoogleServicesPresent);
            Assert.AreEqual(15,                     info.SkAdNetworksCount);
            Assert.AreEqual(8,                      info.AndroidPermissionsCount);
            Assert.IsTrue(info.IsSandbox);
            Assert.AreEqual("sg",                   info.Region);
            Assert.AreEqual("{\"clientId\":\"test\"}", info.RawConfigJson);
        }
    }
}
