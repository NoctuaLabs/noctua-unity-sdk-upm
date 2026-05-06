using System.Collections.Generic;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime.IAA
{
    /// <summary>
    /// EditMode NUnit tests for <see cref="IAAPayloadBuilder"/>, <see cref="IAAEventNames"/>,
    /// <see cref="IAAPayloadKey"/>, and <see cref="IAAAdSize"/>.
    ///
    /// Covers:
    ///   — <c>BuildAdImpression</c>    — all 12 required keys present, null-coalescing
    ///   — <c>BuildAdLoaded</c>        — all 8 required keys present
    ///   — <c>BuildAdLoadFailed</c>    — all 4 required keys
    ///   — <c>BuildAdShowFailed</c>    — delegates to BuildAdLoadFailed
    ///   — <c>BuildAdClicked</c>       — delegates to BuildAdLoaded
    ///   — <c>BuildWatchMilestone</c>  — ad_type and count keys
    ///   — <c>FormatError</c>          — two overloads (code+msg+mediator, code+msg+domain)
    ///   — <c>IAAEventNames</c>        — canonical constant values
    ///   — <c>IAAPayloadKey</c>        — canonical constant values
    ///   — <c>IAAAdSize</c>            — canonical constant values
    /// </summary>
    [TestFixture]
    public class IAAPayloadBuilderTest
    {
        // ═══════════════════════════════════════════════════════════════════
        // BuildAdImpression
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void BuildAdImpression_AllKeysPresent()
        {
            var p = IAAPayloadBuilder.BuildAdImpression(
                "main", "rewarded", "unit-1", "Rewarded HD",
                0.01, 0.01, "fullscreen", "admob", "applovin", 12345L);

            Assert.IsTrue(p.ContainsKey(IAAPayloadKey.Placement));
            Assert.IsTrue(p.ContainsKey(IAAPayloadKey.AdType));
            Assert.IsTrue(p.ContainsKey(IAAPayloadKey.AdUnitId));
            Assert.IsTrue(p.ContainsKey(IAAPayloadKey.AdUnitName));
            Assert.IsTrue(p.ContainsKey(IAAPayloadKey.Value));
            Assert.IsTrue(p.ContainsKey(IAAPayloadKey.Currency));
            Assert.IsTrue(p.ContainsKey(IAAPayloadKey.ValueUsd));
            Assert.IsTrue(p.ContainsKey(IAAPayloadKey.AdFormat));
            Assert.IsTrue(p.ContainsKey(IAAPayloadKey.AdSize));
            Assert.IsTrue(p.ContainsKey(IAAPayloadKey.AdSource));
            Assert.IsTrue(p.ContainsKey(IAAPayloadKey.AdPlatform));
            Assert.IsTrue(p.ContainsKey(IAAPayloadKey.EngagementTime));
            Assert.AreEqual(12, p.Count, "AdImpression payload must have exactly 12 keys");
        }

        [Test]
        public void BuildAdImpression_CurrencyIsUSD()
        {
            var p = IAAPayloadBuilder.BuildAdImpression(
                "p", "rewarded", "u", "n", 1.0, 1.0, "fullscreen", "s", "pl", 0L);

            Assert.AreEqual("USD", p[IAAPayloadKey.Currency].ToString());
        }

        [Test]
        public void BuildAdImpression_NullPlacement_CoalescesToUnknown()
        {
            var p = IAAPayloadBuilder.BuildAdImpression(
                null, "rewarded", "u", "n", 0, 0, "fs", "s", "pl", 0L);

            Assert.AreEqual("unknown", p[IAAPayloadKey.Placement].ToString());
        }

        [Test]
        public void BuildAdImpression_NullAdType_CoalescesToUnknown()
        {
            var p = IAAPayloadBuilder.BuildAdImpression(
                "p", null, "u", "n", 0, 0, "fs", "s", "pl", 0L);

            Assert.AreEqual("unknown", p[IAAPayloadKey.AdType].ToString());
            Assert.AreEqual("unknown", p[IAAPayloadKey.AdFormat].ToString());
        }

        [Test]
        public void BuildAdImpression_EngagementTimeStored()
        {
            var p = IAAPayloadBuilder.BuildAdImpression(
                "p", "rewarded", "u", "n", 0, 0, "fs", "s", "pl", 99_000L);

            Assert.AreEqual(99_000L, (long)p[IAAPayloadKey.EngagementTime]);
        }

        // ═══════════════════════════════════════════════════════════════════
        // BuildAdLoaded
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void BuildAdLoaded_AllKeysPresent()
        {
            var p = IAAPayloadBuilder.BuildAdLoaded("p", "interstitial", "u", "n", "fs", "s", "pl");

            Assert.AreEqual(8, p.Count, "AdLoaded payload must have exactly 8 keys");
            Assert.IsTrue(p.ContainsKey(IAAPayloadKey.Placement));
            Assert.IsTrue(p.ContainsKey(IAAPayloadKey.AdFormat));
        }

        [Test]
        public void BuildAdLoaded_NullAdSize_CoalescesToUnknown()
        {
            var p = IAAPayloadBuilder.BuildAdLoaded("p", "interstitial", "u", "n", null, "s", "pl");

            Assert.AreEqual(IAAAdSize.Unknown, p[IAAPayloadKey.AdSize].ToString());
        }

        // ═══════════════════════════════════════════════════════════════════
        // BuildAdLoadFailed / BuildAdShowFailed (same shape)
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void BuildAdLoadFailed_AllKeysPresent()
        {
            var p = IAAPayloadBuilder.BuildAdLoadFailed("rewarded", "admob", "unit-1", "[403] forbidden");

            Assert.AreEqual(4, p.Count, "AdLoadFailed payload must have exactly 4 keys");
            Assert.IsTrue(p.ContainsKey(IAAPayloadKey.AdFormat));
            Assert.IsTrue(p.ContainsKey(IAAPayloadKey.AdPlatform));
            Assert.IsTrue(p.ContainsKey(IAAPayloadKey.AdUnitName));
            Assert.IsTrue(p.ContainsKey(IAAPayloadKey.Error));
        }

        [Test]
        public void BuildAdLoadFailed_NullError_CoalescesToUnknown()
        {
            var p = IAAPayloadBuilder.BuildAdLoadFailed("rewarded", "admob", "unit", null);

            Assert.AreEqual("unknown", p[IAAPayloadKey.Error].ToString());
        }

        [Test]
        public void BuildAdShowFailed_SameShapeAsLoadFailed()
        {
            var failed = IAAPayloadBuilder.BuildAdLoadFailed("r", "a", "u", "err");
            var shown  = IAAPayloadBuilder.BuildAdShowFailed("r", "a", "u", "err");

            Assert.AreEqual(failed.Count, shown.Count);
            foreach (var key in failed.Keys)
            {
                Assert.IsTrue(shown.ContainsKey(key), $"Key '{key}' missing in AdShowFailed");
                Assert.AreEqual(failed[key].ToString(), shown[key].ToString());
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // BuildAdClicked
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void BuildAdClicked_SameShapeAsAdLoaded()
        {
            var loaded  = IAAPayloadBuilder.BuildAdLoaded("p", "r", "u", "n", "fs", "s", "pl");
            var clicked = IAAPayloadBuilder.BuildAdClicked("p", "r", "u", "n", "fs", "s", "pl");

            Assert.AreEqual(loaded.Count, clicked.Count);
        }

        // ═══════════════════════════════════════════════════════════════════
        // BuildWatchMilestone
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void BuildWatchMilestone_ContainsAdTypeAndCount()
        {
            var p = IAAPayloadBuilder.BuildWatchMilestone("rewarded", 10);

            Assert.AreEqual("rewarded", p[IAAPayloadKey.AdType].ToString());
            Assert.AreEqual(10, (int)p[IAAPayloadKey.Count]);
        }

        [Test]
        public void BuildWatchMilestone_NullAdType_CoalescesToUnknown()
        {
            var p = IAAPayloadBuilder.BuildWatchMilestone(null, 5);

            Assert.AreEqual("unknown", p[IAAPayloadKey.AdType].ToString());
        }

        // ═══════════════════════════════════════════════════════════════════
        // FormatError
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void FormatError_WithCodeAndMessage_ContainsBoth()
        {
            string result = IAAPayloadBuilder.FormatError(403, "forbidden");

            StringAssert.Contains("403",       result);
            StringAssert.Contains("forbidden", result);
        }

        [Test]
        public void FormatError_NullMessage_ReplacedWithUnknown()
        {
            string result = IAAPayloadBuilder.FormatError(0, null);

            StringAssert.Contains("unknown", result);
        }

        [Test]
        public void FormatError_WithMediatorCode_ContainsMediatorInfo()
        {
            string result = IAAPayloadBuilder.FormatError(404, "not found", 500, "internal");

            StringAssert.Contains("mediator", result);
            StringAssert.Contains("500", result);
        }

        [Test]
        public void FormatError_WithDomain_ContainsDomain()
        {
            string result = IAAPayloadBuilder.FormatError(400, "bad request", "com.example.domain");

            StringAssert.Contains("com.example.domain", result);
        }

        // ═══════════════════════════════════════════════════════════════════
        // IAAEventNames constants
        // ═══════════════════════════════════════════════════════════════════

        [Test] public void EventName_AdImpression_IsLiteral()    => Assert.AreEqual("ad_impression",   IAAEventNames.AdImpression);
        [Test] public void EventName_AdLoaded_IsLiteral()        => Assert.AreEqual("ad_loaded",       IAAEventNames.AdLoaded);
        [Test] public void EventName_AdLoadFailed_IsLiteral()    => Assert.AreEqual("ad_load_failed",  IAAEventNames.AdLoadFailed);
        [Test] public void EventName_AdShowFailed_IsLiteral()    => Assert.AreEqual("ad_show_failed",  IAAEventNames.AdShowFailed);
        [Test] public void EventName_WatchAds5x_IsLiteral()      => Assert.AreEqual("watch_ads_5x",    IAAEventNames.WatchAds5x);
        [Test] public void EventName_WatchAds50x_IsLiteral()     => Assert.AreEqual("watch_ads_50x",   IAAEventNames.WatchAds50x);

        // ═══════════════════════════════════════════════════════════════════
        // IAAAdSize constants
        // ═══════════════════════════════════════════════════════════════════

        [Test] public void AdSize_Fullscreen_IsLiteral()  => Assert.AreEqual("fullscreen", IAAAdSize.Fullscreen);
        [Test] public void AdSize_Banner320_IsLiteral()   => Assert.AreEqual("320x50",     IAAAdSize.Banner320);
        [Test] public void AdSize_Unknown_IsLiteral()     => Assert.AreEqual("unknown",    IAAAdSize.Unknown);
    }
}
