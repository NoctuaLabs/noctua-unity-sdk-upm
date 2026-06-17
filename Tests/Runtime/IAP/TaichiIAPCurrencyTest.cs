using System;
using System.Collections.Generic;
using System.Reflection;
using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Runtime.IAP
{
    /// <summary>
    /// Unit tests for the value and currency reported by <c>NoctuaIAPService.TrackTaichiIAP</c> in the
    /// <c>taichi_iap_revenue</c> event payload.
    ///
    /// <para>Contract under test:</para>
    /// <list type="bullet">
    ///   <item>Revenue is accumulated in the product's own (local) currency from
    ///   <c>OrderRequest.Price</c>; the same local total is compared against the configured threshold
    ///   and reported as <c>value</c>.</item>
    ///   <item>The reported <c>currency</c> follows the product (<c>OrderRequest.Currency</c>), falling
    ///   back to <c>"USD"</c> when the order carries none.</item>
    ///   <item>When a later purchase arrives in a different currency than what is already accumulated,
    ///   the service logs a warning but still appends (verified behaviourally — see note on the
    ///   mismatch test).</item>
    /// </list>
    ///
    /// <para>
    /// <c>TrackTaichiIAP</c> is private and only emits via <c>INativePlugin.TrackCustomEvent</c>, so the
    /// test invokes it through reflection and captures the forwarded payload with a
    /// <see cref="CapturingNativePlugin"/> (a <see cref="DefaultNativePlugin"/> subclass that
    /// re-implements <c>TrackCustomEvent</c>). No HTTP / live backend needed.
    /// </para>
    /// </summary>
    [TestFixture]
    public class TaichiIAPCurrencyTest
    {
        private const string KeyIAPTotalRevenue   = "Noctua_Taichi_IAPTotalRevenue";
        private const string KeyIAPRevenueCurrency = "Noctua_Taichi_IAPRevenueCurrency";

        [SetUp]
        public void SetUp()
        {
            // NoctuaLogger routes through Serilog, which is silent in EditMode — ignore so the
            // [taichi] log lines (including the mismatch warning) don't fail the runner.
            LogAssert.ignoreFailingMessages = true;
            PlayerPrefs.DeleteKey(KeyIAPTotalRevenue);
            PlayerPrefs.DeleteKey(KeyIAPRevenueCurrency);
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            PlayerPrefs.DeleteKey(KeyIAPTotalRevenue);
            PlayerPrefs.DeleteKey(KeyIAPRevenueCurrency);
            PlayerPrefs.Save();
        }

        [Test]
        public void TrackTaichiIAP_ReportsProductCurrencyAndLocalValue()
        {
            var plugin = new CapturingNativePlugin();
            var svc    = CreateService(plugin);
            svc.SetIAPTaichiConfig(new IAPTaichiConfig { RevenueThreshold = 10000 });

            // Local price 15000 IDR >= threshold (10000) → fires with the local value + currency.
            InvokeTrackTaichiIAP(svc, price: 15000m, currency: "IDR");

            var evt = AssertSingleTaichiEvent(plugin);
            Assert.AreEqual("IDR", evt.Payload["currency"],
                "currency must follow the product's OrderRequest.Currency");
            Assert.AreEqual(15000.0, Convert.ToDouble(evt.Payload["value"]),
                "value must be the accumulated local price (OrderRequest.Price)");
        }

        [Test]
        public void TrackTaichiIAP_WhenOrderCurrencyNull_FallsBackToUsd()
        {
            var plugin = new CapturingNativePlugin();
            var svc    = CreateService(plugin);
            svc.SetIAPTaichiConfig(new IAPTaichiConfig { RevenueThreshold = 0.99 });

            InvokeTrackTaichiIAP(svc, price: 1.00m, currency: null);

            var evt = AssertSingleTaichiEvent(plugin);
            Assert.AreEqual("USD", evt.Payload["currency"],
                "A null order currency must fall back to USD");
        }

        [Test]
        public void TrackTaichiIAP_WhenOrderCurrencyBlank_FallsBackToUsd()
        {
            var plugin = new CapturingNativePlugin();
            var svc    = CreateService(plugin);
            svc.SetIAPTaichiConfig(new IAPTaichiConfig { RevenueThreshold = 0.99 });

            InvokeTrackTaichiIAP(svc, price: 1.00m, currency: "   ");

            var evt = AssertSingleTaichiEvent(plugin);
            Assert.AreEqual("USD", evt.Payload["currency"],
                "A blank/whitespace order currency must fall back to USD");
        }

        [Test]
        public void TrackTaichiIAP_AccumulatesLocalValueUntilThresholdCrossed()
        {
            var plugin = new CapturingNativePlugin();
            var svc    = CreateService(plugin);
            svc.SetIAPTaichiConfig(new IAPTaichiConfig { RevenueThreshold = 25000 });

            // First order: 15000 < 25000 → no fire, accumulates.
            InvokeTrackTaichiIAP(svc, price: 15000m, currency: "IDR");
            Assert.AreEqual(0, plugin.CustomEvents.Count, "Below threshold must not fire");

            // Second order: 30000 >= 25000 → fires with the cumulative local value.
            InvokeTrackTaichiIAP(svc, price: 15000m, currency: "IDR");

            var evt = AssertSingleTaichiEvent(plugin);
            Assert.AreEqual("IDR", evt.Payload["currency"]);
            Assert.AreEqual(30000.0, Convert.ToDouble(evt.Payload["value"]),
                "value must be the cumulative local price across both orders");
        }

        [Test]
        public void TrackTaichiIAP_MixedCurrencies_StillAppendsAndReportsLatestCurrency()
        {
            // The currency-mismatch case: the warning itself isn't asserted because NoctuaLogger is
            // silent in EditMode (Serilog is only configured after InitAsync). This documents the
            // append-anyway behaviour and that the latest order's currency is reported.
            var plugin = new CapturingNativePlugin();
            var svc    = CreateService(plugin);
            svc.SetIAPTaichiConfig(new IAPTaichiConfig { RevenueThreshold = 20000 });

            InvokeTrackTaichiIAP(svc, price: 15000m, currency: "IDR"); // accumulates, no fire
            Assert.AreEqual(0, plugin.CustomEvents.Count);

            InvokeTrackTaichiIAP(svc, price: 6000m, currency: "USD");  // different currency → warns, appends → 21000

            var evt = AssertSingleTaichiEvent(plugin);
            Assert.AreEqual("USD", evt.Payload["currency"], "Latest order's currency is reported");
            Assert.AreEqual(21000.0, Convert.ToDouble(evt.Payload["value"]),
                "Raw local amounts are summed even across currencies (mismatch is warned, not blocked)");
        }

        // ─── Helpers ───────────────────────────────────────────────────────────

        private static NoctuaIAPService CreateService(INativePlugin nativePlugin)
        {
            var config = new NoctuaIAPService.Config
            {
                BaseUrl  = "https://api.example.com",
                ClientId = "test-client-id"
            };

            return new NoctuaIAPService(
                config:              config,
                accessTokenProvider: null,
                paymentUI:           null,
                nativePlugin:        nativePlugin
            );
        }

        private static void InvokeTrackTaichiIAP(NoctuaIAPService svc, decimal price, string currency)
        {
            var method = typeof(NoctuaIAPService).GetMethod(
                "TrackTaichiIAP", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "TrackTaichiIAP private method not found via reflection");
            var order = new OrderRequest { Id = 1, Price = price, Currency = currency };
            method.Invoke(svc, new object[] { order });
        }

        private static CapturingNativePlugin.CustomEvent AssertSingleTaichiEvent(CapturingNativePlugin plugin)
        {
            Assert.AreEqual(1, plugin.CustomEvents.Count, "Exactly one taichi event expected");
            var evt = plugin.CustomEvents[0];
            Assert.AreEqual("taichi_iap_revenue", evt.Name);
            Assert.IsTrue(evt.Payload.ContainsKey("currency"), "Payload must contain a currency key");
            Assert.IsTrue(evt.Payload.ContainsKey("value"), "Payload must contain a value key");
            return evt;
        }

        // ─── Capturing native plugin ─────────────────────────────────────────────

        /// <summary>
        /// Subclasses the editor stub <see cref="DefaultNativePlugin"/> and re-implements
        /// <c>INativePlugin.TrackCustomEvent</c> (interface re-implementation via <c>new</c> +
        /// re-listing the interface) so taichi events are captured while every other native
        /// member keeps its no-op editor behavior.
        /// </summary>
        private sealed class CapturingNativePlugin : DefaultNativePlugin, INativePlugin
        {
            public sealed class CustomEvent
            {
                public string Name;
                public Dictionary<string, IConvertible> Payload;
            }

            public List<CustomEvent> CustomEvents { get; } = new List<CustomEvent>();

            public new void TrackCustomEvent(string name, Dictionary<string, IConvertible> extraPayload = null)
            {
                var snapshot = extraPayload != null
                    ? new Dictionary<string, IConvertible>(extraPayload)
                    : new Dictionary<string, IConvertible>();
                CustomEvents.Add(new CustomEvent { Name = name, Payload = snapshot });
            }
        }
    }
}
