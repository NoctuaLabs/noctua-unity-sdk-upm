using System;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// EditMode NUnit tests for the debug-sample value types:
    ///   * <see cref="PerformanceSample"/> — constructor, property accessors, GPU/CPU defaults
    ///   * <see cref="MemorySample"/>      — constructor, property accessors
    ///   * <see cref="DeviceMetricsSnapshot"/> — constructor, Empty factory, property accessors
    ///   * <see cref="ThermalState"/>      — enum ordinals (native ABI contract)
    ///   * <see cref="BuildSanityInfo"/>   — default values, property round-trips
    ///
    /// All types are pure data (readonly structs / sealed classes) with no
    /// Unity runtime or network dependencies — plain <c>[Test]</c> suffices.
    /// </summary>
    [TestFixture]
    public class DebugSamplesTest
    {
        private static readonly DateTime _ts = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        // ═══════════════════════════════════════════════════════════════════
        // PerformanceSample
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void PerformanceSample_Constructor_AllFieldsAssigned()
        {
            var s = new PerformanceSample(
                timestampUtc:      _ts,
                deltaSeconds:      0.016f,
                fpsInstant:        62.5f,
                fpsAvg1s:          60f,
                fpsAvg5s:          58f,
                frameTimeMs:       16f,
                frameTimeP95Ms:    18f,
                droppedFrames30Hz: 2,
                droppedFrames60Hz: 5,
                gpuFrameTimeMs:    8f,
                cpuMainThreadMs:   7f,
                cpuRenderThreadMs: 3f);

            Assert.AreEqual(_ts,    s.TimestampUtc);
            Assert.AreEqual(0.016f, s.DeltaSeconds,      delta: 0.0001f);
            Assert.AreEqual(62.5f,  s.FpsInstant,        delta: 0.001f);
            Assert.AreEqual(60f,    s.FpsAvg1s,          delta: 0.001f);
            Assert.AreEqual(58f,    s.FpsAvg5s,          delta: 0.001f);
            Assert.AreEqual(16f,    s.FrameTimeMs,       delta: 0.001f);
            Assert.AreEqual(18f,    s.FrameTimeP95Ms,    delta: 0.001f);
            Assert.AreEqual(2,      s.DroppedFrames30Hz);
            Assert.AreEqual(5,      s.DroppedFrames60Hz);
            Assert.AreEqual(8f,     s.GpuFrameTimeMs,    delta: 0.001f);
            Assert.AreEqual(7f,     s.CpuMainThreadMs,   delta: 0.001f);
            Assert.AreEqual(3f,     s.CpuRenderThreadMs, delta: 0.001f);
        }

        [Test]
        public void PerformanceSample_GpuCpuDefaults_AreMinusOne()
        {
            // GPU/CPU params omitted → should default to -1 (platform unavailable)
            var s = new PerformanceSample(_ts, 0.016f, 60f, 60f, 60f, 16f, 18f, 0, 0);

            Assert.AreEqual(-1f, s.GpuFrameTimeMs,    delta: 0.001f,
                "GPU time must default to -1 when platform doesn't expose it");
            Assert.AreEqual(-1f, s.CpuMainThreadMs,   delta: 0.001f);
            Assert.AreEqual(-1f, s.CpuRenderThreadMs, delta: 0.001f);
        }

        [Test]
        public void PerformanceSample_IsReadOnlyStruct_CannotMutateFields()
        {
            // Compile-time guarantee: PerformanceSample is readonly struct.
            // This test just ensures the default value is a valid struct, not null.
            var s = default(PerformanceSample);
            Assert.AreEqual(default(DateTime), s.TimestampUtc);
        }

        // ═══════════════════════════════════════════════════════════════════
        // MemorySample
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void MemorySample_Constructor_AllFieldsAssigned()
        {
            var native = new DeviceMetricsSnapshot(_ts, 50_000_000L, 200_000_000L, 4_000_000_000L, false, ThermalState.Nominal);
            var s = new MemorySample(
                timestampUtc:        _ts,
                monoUsedBytes:       10_000_000L,
                monoHeapBytes:       20_000_000L,
                unityAllocatedBytes: 30_000_000L,
                unityReservedBytes:  40_000_000L,
                gcTotalBytes:        15_000_000L,
                assetCacheBytes:     5_000_000L,
                native:              native);

            Assert.AreEqual(_ts,            s.TimestampUtc);
            Assert.AreEqual(10_000_000L,    s.MonoUsedBytes);
            Assert.AreEqual(20_000_000L,    s.MonoHeapBytes);
            Assert.AreEqual(30_000_000L,    s.UnityAllocatedBytes);
            Assert.AreEqual(40_000_000L,    s.UnityReservedBytes);
            Assert.AreEqual(15_000_000L,    s.GcTotalBytes);
            Assert.AreEqual(5_000_000L,     s.AssetCacheBytes);
            Assert.AreEqual(ThermalState.Nominal, s.Native.Thermal);
        }

        [Test]
        public void MemorySample_Default_HasZeroValues()
        {
            var s = default(MemorySample);
            Assert.AreEqual(0L, s.MonoUsedBytes);
            Assert.AreEqual(0L, s.GcTotalBytes);
        }

        // ═══════════════════════════════════════════════════════════════════
        // DeviceMetricsSnapshot
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void DeviceMetricsSnapshot_Constructor_AllFieldsAssigned()
        {
            var s = new DeviceMetricsSnapshot(_ts, 100L, 200L, 300L, true, ThermalState.Serious);

            Assert.AreEqual(_ts,               s.TimestampUtc);
            Assert.AreEqual(100L,              s.PhysFootprintBytes);
            Assert.AreEqual(200L,              s.AvailableBytes);
            Assert.AreEqual(300L,              s.SystemTotalBytes);
            Assert.IsTrue(s.LowMemory);
            Assert.AreEqual(ThermalState.Serious, s.Thermal);
        }

        [Test]
        public void DeviceMetricsSnapshot_Empty_ReturnsMinusOneForNumericFields()
        {
            var s = DeviceMetricsSnapshot.Empty(_ts);

            Assert.AreEqual(_ts, s.TimestampUtc);
            Assert.AreEqual(-1L, s.PhysFootprintBytes, "Empty footprint must be -1");
            Assert.AreEqual(-1L, s.AvailableBytes,     "Empty available must be -1");
            Assert.AreEqual(-1L, s.SystemTotalBytes,   "Empty systemTotal must be -1");
        }

        [Test]
        public void DeviceMetricsSnapshot_Empty_LowMemoryIsFalse()
        {
            var s = DeviceMetricsSnapshot.Empty(_ts);
            Assert.IsFalse(s.LowMemory);
        }

        [Test]
        public void DeviceMetricsSnapshot_Empty_ThermalIsUnknown()
        {
            var s = DeviceMetricsSnapshot.Empty(_ts);
            Assert.AreEqual(ThermalState.Unknown, s.Thermal);
        }

        // ═══════════════════════════════════════════════════════════════════
        // ThermalState enum ordinals — native ABI contract
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void ThermalState_Unknown_OrdinalIsMinusOne()  => Assert.AreEqual(-1, (int)ThermalState.Unknown);

        [Test]
        public void ThermalState_Nominal_OrdinalIsZero()      => Assert.AreEqual(0,  (int)ThermalState.Nominal);

        [Test]
        public void ThermalState_Fair_OrdinalIsOne()          => Assert.AreEqual(1,  (int)ThermalState.Fair);

        [Test]
        public void ThermalState_Serious_OrdinalIsTwo()       => Assert.AreEqual(2,  (int)ThermalState.Serious);

        [Test]
        public void ThermalState_Critical_OrdinalIsThree()    => Assert.AreEqual(3,  (int)ThermalState.Critical);

        // ═══════════════════════════════════════════════════════════════════
        // BuildSanityInfo
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void BuildSanityInfo_DefaultStringProperties_AreEmpty()
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
        }

        [Test]
        public void BuildSanityInfo_DefaultIntSentinels_AreMinusOne()
        {
            var info = new BuildSanityInfo();

            Assert.AreEqual(-1, info.SkAdNetworksCount,      "SkAdNetworksCount sentinel must be -1");
            Assert.AreEqual(-1, info.AndroidPermissionsCount,"AndroidPermissionsCount sentinel must be -1");
        }

        [Test]
        public void BuildSanityInfo_DefaultBoolProperties_AreFalse()
        {
            var info = new BuildSanityInfo();

            Assert.IsFalse(info.GoogleServicesPresent);
            Assert.IsFalse(info.IsSandbox);
        }

        [Test]
        public void BuildSanityInfo_PropertyRoundTrip_AllFields()
        {
            var info = new BuildSanityInfo
            {
                UnitySdkVersion         = "1.2.3",
                NativeSdkVersion        = "4.5.6",
                BundleId                = "com.test.app",
                AppVersion              = "2.0.0",
                UnityVersion            = "2022.3.0f1",
                ConfigChecksum          = "abc123",
                AdjustAppTokenMasked    = "…xyz789",
                FirebaseProjectId       = "my-project",
                GoogleServicesPresent   = true,
                SkAdNetworksCount       = 42,
                AndroidPermissionsCount = 7,
                IsSandbox               = true,
                Region                  = "ID",
                RawConfigJson           = "{\"clientId\":\"x\"}"
            };

            Assert.AreEqual("1.2.3",              info.UnitySdkVersion);
            Assert.AreEqual("4.5.6",              info.NativeSdkVersion);
            Assert.AreEqual("com.test.app",       info.BundleId);
            Assert.AreEqual("2.0.0",              info.AppVersion);
            Assert.AreEqual("2022.3.0f1",         info.UnityVersion);
            Assert.AreEqual("abc123",             info.ConfigChecksum);
            Assert.AreEqual("…xyz789",            info.AdjustAppTokenMasked);
            Assert.AreEqual("my-project",         info.FirebaseProjectId);
            Assert.IsTrue(info.GoogleServicesPresent);
            Assert.AreEqual(42,                   info.SkAdNetworksCount);
            Assert.AreEqual(7,                    info.AndroidPermissionsCount);
            Assert.IsTrue(info.IsSandbox);
            Assert.AreEqual("ID",                 info.Region);
            Assert.AreEqual("{\"clientId\":\"x\"}", info.RawConfigJson);
        }

        [Test]
        public void BuildSanityInfo_AdjustAppTokenMasked_StartsWithEllipsis()
        {
            // By convention the provider pre-masks the token with "…" prefix.
            // Verify the field can store and retrieve that character.
            var info = new BuildSanityInfo { AdjustAppTokenMasked = "…abc456" };
            StringAssert.StartsWith("…", info.AdjustAppTokenMasked);
        }
    }
}
