using com.noctuagames.sdk;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// EditMode NUnit tests for <see cref="NetworkConditioner"/> — the static
    /// fault-injection shim between <c>HttpRequest</c> and <c>UnityWebRequest</c>.
    ///
    /// All 5 <c>[UnityTest]</c> methods in <c>NetworkConditionerTests</c> call
    /// <c>ApplyAsync()</c> via <c>UniTask.ToCoroutine</c> with <c>await UniTask.Yield()</c>
    /// despite exercising code paths that complete without any real async delay:
    ///
    ///   * <see cref="NetworkMode.Normal"/>      — returns immediately (no await in body)
    ///   * <see cref="NetworkMode.Offline"/>     — throws before the first await
    ///   * <see cref="NetworkMode.PacketLoss"/>  — throws or returns before any await
    ///   * <see cref="NetworkMode.Slow3G"/> with <c>Slow3GLatencyMs = 0</c>
    ///     — <c>if (ms > 0)</c> is false so <c>UniTask.Delay</c> is skipped entirely
    ///
    /// These plain <c>[Test]</c> equivalents call <c>.GetAwaiter().GetResult()</c>
    /// directly — when a <c>UniTask</c> completes synchronously the awaiter is already
    /// done and <c>GetResult()</c> returns (or rethrows) immediately without entering
    /// the UniTask scheduler.
    /// </summary>
    [TestFixture]
    public class NetworkConditionerEditModeTest
    {
        [SetUp]
        public void SetUp()
        {
            NetworkConditioner.Mode                = NetworkMode.Normal;
            NetworkConditioner.Slow3GLatencyMs     = 200;
            NetworkConditioner.PacketLossPercent   = 30;
        }

        [TearDown]
        public void TearDown()
        {
            NetworkConditioner.Mode = NetworkMode.Normal;
        }

        // ─── NormalMode ───────────────────────────────────────────────────────

        [Test]
        public void NormalMode_ApplyAsync_CompletesWithoutException()
        {
            NetworkConditioner.Mode = NetworkMode.Normal;

            // Normal returns before the first await — GetResult() returns immediately.
            Assert.DoesNotThrow(() =>
                NetworkConditioner.ApplyAsync().GetAwaiter().GetResult());
        }

        // ─── OfflineMode ──────────────────────────────────────────────────────

        [Test]
        public void OfflineMode_ApplyAsync_ThrowsNetworkConditionerException()
        {
            NetworkConditioner.Mode = NetworkMode.Offline;

            // Offline throws before any real await — the UniTask is faulted synchronously.
            Assert.Throws<NetworkConditionerException>(() =>
                NetworkConditioner.ApplyAsync().GetAwaiter().GetResult());
        }

        [Test]
        public void OfflineMode_ApplyAsync_ExceptionMessageContainsOffline()
        {
            NetworkConditioner.Mode = NetworkMode.Offline;

            NetworkConditionerException caught = null;
            try
            {
                NetworkConditioner.ApplyAsync().GetAwaiter().GetResult();
            }
            catch (NetworkConditionerException ex)
            {
                caught = ex;
            }

            Assert.IsNotNull(caught);
            StringAssert.Contains("offline", caught.Message);
        }

        // ─── PacketLoss ───────────────────────────────────────────────────────

        [Test]
        public void PacketLoss_100Percent_ApplyAsync_AlwaysThrows()
        {
            NetworkConditioner.Mode              = NetworkMode.PacketLoss;
            NetworkConditioner.PacketLossPercent = 100;

            // 100 % loss — the random check always succeeds and throws before any await.
            Assert.Throws<NetworkConditionerException>(() =>
                NetworkConditioner.ApplyAsync().GetAwaiter().GetResult());
        }

        [Test]
        public void PacketLoss_0Percent_ApplyAsync_NeverThrows()
        {
            NetworkConditioner.Mode              = NetworkMode.PacketLoss;
            NetworkConditioner.PacketLossPercent = 0;

            // 0 % loss — the condition `pct > 0` is false; method returns before any await.
            for (int i = 0; i < 10; i++)
            {
                Assert.DoesNotThrow(() =>
                    NetworkConditioner.ApplyAsync().GetAwaiter().GetResult());
            }
        }

        // ─── Slow3G ───────────────────────────────────────────────────────────

        [Test]
        public void Slow3G_ZeroLatency_ApplyAsync_CompletesWithoutException()
        {
            NetworkConditioner.Mode            = NetworkMode.Slow3G;
            NetworkConditioner.Slow3GLatencyMs = 0;

            // `if (ms > 0)` is false — UniTask.Delay is never called; returns synchronously.
            Assert.DoesNotThrow(() =>
                NetworkConditioner.ApplyAsync().GetAwaiter().GetResult());
        }

        // ─── Static properties ────────────────────────────────────────────────

        [Test]
        public void NetworkMode_EnumValues_MatchExpectedOrdinals()
        {
            Assert.AreEqual(0, (int)NetworkMode.Normal);
            Assert.AreEqual(1, (int)NetworkMode.Slow3G);
            Assert.AreEqual(2, (int)NetworkMode.Offline);
            Assert.AreEqual(3, (int)NetworkMode.PacketLoss);
        }

        [Test]
        public void NetworkConditionerException_PreservesMessage()
        {
            var ex = new NetworkConditionerException("test msg");
            Assert.AreEqual("test msg", ex.Message);
        }

        [Test]
        public void DefaultProperties_AfterSetUp_AreNormalMode()
        {
            Assert.AreEqual(NetworkMode.Normal, NetworkConditioner.Mode);
            Assert.AreEqual(200, NetworkConditioner.Slow3GLatencyMs);
            Assert.AreEqual(30,  NetworkConditioner.PacketLossPercent);
        }

        [Test]
        public void Mode_CanBeAssigned_AllValues()
        {
            foreach (NetworkMode m in System.Enum.GetValues(typeof(NetworkMode)))
            {
                NetworkConditioner.Mode = m;
                Assert.AreEqual(m, NetworkConditioner.Mode);
            }
        }

        [Test]
        public void Slow3GLatencyMs_CanBeAssigned()
        {
            NetworkConditioner.Slow3GLatencyMs = 500;
            Assert.AreEqual(500, NetworkConditioner.Slow3GLatencyMs);
        }

        [Test]
        public void PacketLossPercent_CanBeAssigned()
        {
            NetworkConditioner.PacketLossPercent = 75;
            Assert.AreEqual(75, NetworkConditioner.PacketLossPercent);
        }
    }
}
