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
    /// Unit tests for the value and currency reported by <c>NoctuaIAPService.TrackTaichiIAP</c>.
    ///
    /// <para>Contract under test (two paths, decided by whether the backend supplied a USD value):</para>
    /// <list type="bullet">
    ///   <item><b>Unconverted</b> — when <c>OrderRequest.LocalPriceInUsd &lt;= 0</c> (no exchange rate),
    ///   each purchase fires <c>taichi_iap_revenue_unconverted</c> per-order (no accumulator).
    ///   <c>value</c> is the raw local <c>OrderRequest.Price</c> and <c>currency</c> follows
    ///   <c>OrderRequest.Currency</c>, falling back to <c>"USD"</c> when blank.</item>
    ///   <item><b>Converted</b> — when <c>LocalPriceInUsd &gt; 0</c>, revenue accumulates in USD against
    ///   the (USD) <c>RevenueThreshold</c>; <c>taichi_iap_revenue</c> fires once the threshold is
    ///   crossed, with the cumulative USD <c>value</c> and <c>currency = "USD"</c>, then resets.</item>
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

        // ─── Unconverted path: no USD rate from backend (local_price_in_usd <= 0) ─────────────
        // Each purchase fires taichi_iap_revenue_unconverted per-order (no accumulator); value is the
        // raw local price and currency follows the order (USD fallback). The backend converts later.

        [Test]
        public void TrackTaichiIAP_NoUsdRate_FiresUnconvertedWithLocalCurrencyAndValue()
        {
            var plugin = new CapturingNativePlugin();
            var svc    = CreateService(plugin);
            svc.SetIAPTaichiConfig(new IAPTaichiConfig { RevenueThreshold = 10000 });

            // No local_price_in_usd / rate → routed to taichi_iap_revenue_unconverted.
            InvokeTrackTaichiIAP(svc, price: 15000m, currency: "IDR");

            var evt = AssertSingleTaichiEvent(plugin, "taichi_iap_revenue_unconverted");
            Assert.AreEqual("IDR", evt.Payload["currency"],
                "currency must follow the product's OrderRequest.Currency");
            Assert.AreEqual(15000.0, Convert.ToDouble(evt.Payload["value"]),
                "value must be the raw local price (OrderRequest.Price)");
        }

        [Test]
        public void TrackTaichiIAP_Unconverted_WhenOrderCurrencyNull_FallsBackToUsd()
        {
            var plugin = new CapturingNativePlugin();
            var svc    = CreateService(plugin);
            svc.SetIAPTaichiConfig(new IAPTaichiConfig { RevenueThreshold = 0.99 });

            InvokeTrackTaichiIAP(svc, price: 1.00m, currency: null);

            var evt = AssertSingleTaichiEvent(plugin, "taichi_iap_revenue_unconverted");
            Assert.AreEqual("USD", evt.Payload["currency"],
                "A null order currency must fall back to USD");
        }

        [Test]
        public void TrackTaichiIAP_Unconverted_WhenOrderCurrencyBlank_FallsBackToUsd()
        {
            var plugin = new CapturingNativePlugin();
            var svc    = CreateService(plugin);
            svc.SetIAPTaichiConfig(new IAPTaichiConfig { RevenueThreshold = 0.99 });

            InvokeTrackTaichiIAP(svc, price: 1.00m, currency: "   ");

            var evt = AssertSingleTaichiEvent(plugin, "taichi_iap_revenue_unconverted");
            Assert.AreEqual("USD", evt.Payload["currency"],
                "A blank/whitespace order currency must fall back to USD");
        }

        // ─── Converted path: backend supplies local_price_in_usd ──────────────────────────────
        // Revenue accumulates in USD against the (USD) threshold; taichi_iap_revenue fires with the
        // cumulative USD value and currency "USD" when the threshold is crossed, then resets.

        [Test]
        public void TrackTaichiIAP_WithUsdRate_AccumulatesUsdUntilThresholdCrossed()
        {
            var plugin = new CapturingNativePlugin();
            var svc    = CreateService(plugin);
            svc.SetIAPTaichiConfig(new IAPTaichiConfig { RevenueThreshold = 25000 });

            // First order: USD 15000 < 25000 → no fire, accumulates.
            InvokeTrackTaichiIAP(svc, price: 15000m, currency: "IDR", localPriceInUsd: 15000m, currencyToUsdRate: 1m);
            Assert.AreEqual(0, plugin.CustomEvents.Count, "Below threshold must not fire");

            // Second order: cumulative USD 30000 >= 25000 → fires.
            InvokeTrackTaichiIAP(svc, price: 15000m, currency: "IDR", localPriceInUsd: 15000m, currencyToUsdRate: 1m);

            var evt = AssertSingleTaichiEvent(plugin, "taichi_iap_revenue");
            Assert.AreEqual("USD", evt.Payload["currency"], "converted revenue is always reported in USD");
            Assert.AreEqual(30000.0, Convert.ToDouble(evt.Payload["value"]),
                "value must be the cumulative USD revenue across both orders");
        }

        [Test]
        public void TrackTaichiIAP_WithUsdRate_AccumulatesAcrossOrderCurrenciesInUsd()
        {
            var plugin = new CapturingNativePlugin();
            var svc    = CreateService(plugin);
            svc.SetIAPTaichiConfig(new IAPTaichiConfig { RevenueThreshold = 20000 });

            // Different order currencies, but each carries its own USD value — accumulation is in USD.
            InvokeTrackTaichiIAP(svc, price: 15000m, currency: "IDR", localPriceInUsd: 15000m, currencyToUsdRate: 1m);
            Assert.AreEqual(0, plugin.CustomEvents.Count);

            InvokeTrackTaichiIAP(svc, price: 6000m, currency: "USD", localPriceInUsd: 6000m, currencyToUsdRate: 1m);

            var evt = AssertSingleTaichiEvent(plugin, "taichi_iap_revenue");
            Assert.AreEqual("USD", evt.Payload["currency"], "converted revenue is always reported in USD");
            Assert.AreEqual(21000.0, Convert.ToDouble(evt.Payload["value"]),
                "USD values are summed across orders regardless of original currency");
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

        private static void InvokeTrackTaichiIAP(
            NoctuaIAPService svc, decimal price, string currency,
            decimal localPriceInUsd = 0m, decimal currencyToUsdRate = 0m)
        {
            var method = typeof(NoctuaIAPService).GetMethod(
                "TrackTaichiIAP", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "TrackTaichiIAP private method not found via reflection");
            var order = new OrderRequest
            {
                Id = 1,
                Price = price,
                Currency = currency,
                LocalPriceInUsd = localPriceInUsd,
                CurrencyToUsdRate = currencyToUsdRate,
            };
            method.Invoke(svc, new object[] { order });
        }

        private static CapturingNativePlugin.CustomEvent AssertSingleTaichiEvent(
            CapturingNativePlugin plugin, string expectedName)
        {
            Assert.AreEqual(1, plugin.CustomEvents.Count, "Exactly one taichi event expected");
            var evt = plugin.CustomEvents[0];
            Assert.AreEqual(expectedName, evt.Name);
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
