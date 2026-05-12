using System;
using System.Collections;
using System.Threading.Tasks;
using com.noctuagames.sdk;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.Runtime
{
    public class NetworkConditionerTests
    {
        [SetUp]
        public void ResetToNormal()
        {
            NetworkConditioner.Mode = NetworkMode.Normal;
            NetworkConditioner.Slow3GLatencyMs = 200;
            NetworkConditioner.PacketLossPercent = 30;
        }

        [TearDown]
        public void TearDown()
        {
            NetworkConditioner.Mode = NetworkMode.Normal;
        }

        [Test]
        [Timeout(5000)]
        public async Task NormalMode_ApplyAsync_DoesNotThrow()
        {
            NetworkConditioner.Mode = NetworkMode.Normal;
            Assert.DoesNotThrow(() => NetworkConditioner.ApplyAsync().Forget());
            await UniTask.Yield();
        }

        [Test]
        [Timeout(5000)]
        public async Task OfflineMode_ApplyAsync_ThrowsNetworkConditionerException()
        {
            NetworkConditioner.Mode = NetworkMode.Offline;
            var threw = false;
            try
            {
                await NetworkConditioner.ApplyAsync();
            }
            catch (NetworkConditionerException)
            {
                threw = true;
            }
            Assert.IsTrue(threw, "Offline mode must throw NetworkConditionerException");
        }

        [Test]
        [Timeout(5000)]
        public async Task PacketLoss_100Percent_AlwaysThrows()
        {
            NetworkConditioner.Mode = NetworkMode.PacketLoss;
            NetworkConditioner.PacketLossPercent = 100;
            var threw = false;
            try
            {
                await NetworkConditioner.ApplyAsync();
            }
            catch (NetworkConditionerException)
            {
                threw = true;
            }
            Assert.IsTrue(threw, "100% packet loss must always throw");
        }

        [Test]
        [Timeout(5000)]
        public async Task PacketLoss_0Percent_NeverThrows()
        {
            NetworkConditioner.Mode = NetworkMode.PacketLoss;
            NetworkConditioner.PacketLossPercent = 0;
            for (int i = 0; i < 10; i++)
            {
                Assert.DoesNotThrowAsync(async () => await NetworkConditioner.ApplyAsync());
            }
            await UniTask.Yield();
        }

        [Test]
        [Timeout(5000)]
        public async Task Slow3G_ZeroLatency_CompletesWithoutDelay()
        {
            NetworkConditioner.Mode = NetworkMode.Slow3G;
            NetworkConditioner.Slow3GLatencyMs = 0;
            var threw = false;
            try
            {
                await NetworkConditioner.ApplyAsync();
            }
            catch
            {
                threw = true;
            }
            Assert.IsFalse(threw, "Slow3G with 0ms latency must not throw");
        }

        [Test]
        public void NetworkMode_EnumValues_Stable()
        {
            Assert.AreEqual(0, (int)NetworkMode.Normal);
            Assert.AreEqual(1, (int)NetworkMode.Slow3G);
            Assert.AreEqual(2, (int)NetworkMode.Offline);
            Assert.AreEqual(3, (int)NetworkMode.PacketLoss);
        }

        [Test]
        public void NetworkConditionerException_PreservesMessage()
        {
            var ex = new NetworkConditionerException("test message");
            Assert.AreEqual("test message", ex.Message);
        }

        [Test]
        public void DefaultValues_AreNormalMode()
        {
            // After SetUp resets to Normal, verify defaults are sensible
            Assert.AreEqual(NetworkMode.Normal, NetworkConditioner.Mode);
            Assert.AreEqual(200, NetworkConditioner.Slow3GLatencyMs);
            Assert.AreEqual(30, NetworkConditioner.PacketLossPercent);
        }
    }
}
