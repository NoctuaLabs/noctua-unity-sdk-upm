using System;
using System.Collections.Generic;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace com.noctuagames.sdk.Tests.IAA
{
    /// <summary>
    /// Unit tests for the IAA canonical constant classes and payload builder:
    ///   * <see cref="IAAEventNames"/>   — string constant values
    ///   * <see cref="IAAPayloadKey"/>   — payload key string constants
    ///   * <see cref="IAAAdSize"/>       — ad-size string constants
    ///   * <see cref="IAAPayloadBuilder"/> — BuildAdImpression, BuildAdLoaded,
    ///     BuildAdLoadFailed, BuildAdShowFailed, BuildAdClicked, BuildWatchMilestone,
    ///     FormatError (both overloads)
    /// </summary>
    [TestFixture]
    public class IAAPayloadBuilderTest
    {
        // ─── IAAEventNames constants ──────────────────────────────────────────

        [Test]
        public void IAAEventNames_AdImpression_IsCorrect()
        {
            Assert.AreEqual("ad_impression", IAAEventNames.AdImpression);
        }

        [Test]
        public void IAAEventNames_AdLoaded_IsCorrect()
        {
            Assert.AreEqual("ad_loaded", IAAEventNames.AdLoaded);
        }

        [Test]
        public void IAAEventNames_AdLoadFailed_IsCorrect()
        {
            Assert.AreEqual("ad_load_failed", IAAEventNames.AdLoadFailed);
        }

        [Test]
        public void IAAEventNames_AdShowFailed_IsCorrect()
        {
            Assert.AreEqual("ad_show_failed", IAAEventNames.AdShowFailed);
        }

        [Test]
        public void IAAEventNames_AdClicked_IsCorrect()
        {
            Assert.AreEqual("ad_clicked", IAAEventNames.AdClicked);
        }

        [Test]
        public void IAAEventNames_AdShown_IsCorrect()
        {
            Assert.AreEqual("ad_shown", IAAEventNames.AdShown);
        }

        [Test]
        public void IAAEventNames_AdClosed_IsCorrect()
        {
            Assert.AreEqual("ad_closed", IAAEventNames.AdClosed);
        }

        [Test]
        public void IAAEventNames_RewardEarned_IsCorrect()
        {
            Assert.AreEqual("reward_earned", IAAEventNames.RewardEarned);
        }

        [Test]
        public void IAAEventNames_BannerLifecycle_AreCorrect()
        {
            Assert.AreEqual("ad_collapsed", IAAEventNames.AdCollapsed);
            Assert.AreEqual("ad_expanded",  IAAEventNames.AdExpanded);
        }

        [Test]
        public void IAAEventNames_WatchMilestones_AreCorrect()
        {
            Assert.AreEqual("watch_ads_5x",  IAAEventNames.WatchAds5x);
            Assert.AreEqual("watch_ads_10x", IAAEventNames.WatchAds10x);
            Assert.AreEqual("watch_ads_25x", IAAEventNames.WatchAds25x);
            Assert.AreEqual("watch_ads_50x", IAAEventNames.WatchAds50x);
        }

        [Test]
        public void IAAEventNames_DeprecatedAlias_IsCorrect()
        {
            Assert.AreEqual("ad_shown_failed", IAAEventNames.AdShownFailedLegacy);
        }

        // ─── IAAPayloadKey constants ──────────────────────────────────────────

        [Test]
        public void IAAPayloadKey_AllKeys_AreCorrect()
        {
            Assert.AreEqual("placement",       IAAPayloadKey.Placement);
            Assert.AreEqual("ad_type",         IAAPayloadKey.AdType);
            Assert.AreEqual("ad_unit_id",      IAAPayloadKey.AdUnitId);
            Assert.AreEqual("ad_unit_name",    IAAPayloadKey.AdUnitName);
            Assert.AreEqual("value",           IAAPayloadKey.Value);
            Assert.AreEqual("currency",        IAAPayloadKey.Currency);
            Assert.AreEqual("value_usd",       IAAPayloadKey.ValueUsd);
            Assert.AreEqual("ad_format",       IAAPayloadKey.AdFormat);
            Assert.AreEqual("ad_size",         IAAPayloadKey.AdSize);
            Assert.AreEqual("ad_source",       IAAPayloadKey.AdSource);
            Assert.AreEqual("ad_platform",     IAAPayloadKey.AdPlatform);
            Assert.AreEqual("engagement_time", IAAPayloadKey.EngagementTime);
            Assert.AreEqual("error",           IAAPayloadKey.Error);
            Assert.AreEqual("count",           IAAPayloadKey.Count);
        }

        // ─── IAAAdSize constants ──────────────────────────────────────────────

        [Test]
        public void IAAAdSize_AllSizes_AreCorrect()
        {
            Assert.AreEqual("fullscreen", IAAAdSize.Fullscreen);
            Assert.AreEqual("320x50",     IAAAdSize.Banner320);
            Assert.AreEqual("728x90",     IAAAdSize.Banner728);
            Assert.AreEqual("300x250",    IAAAdSize.MRec300);
            Assert.AreEqual("unknown",    IAAAdSize.Unknown);
        }

        // ─── BuildAdImpression ────────────────────────────────────────────────

        [Test]
        public void BuildAdImpression_PopulatesAllKeys()
        {
            var payload = IAAPayloadBuilder.BuildAdImpression(
                placement:       "banner_top",
                adType:          "banner",
                adUnitId:        "unit-001",
                adUnitName:      "Banner Top",
                value:           0.005,
                valueUsd:        0.005,
                adSize:          IAAAdSize.Banner320,
                adSource:        "AppLovin",
                adPlatform:      "applovin_max",
                engagementTimeMs: 3000L);

            Assert.AreEqual("banner_top",       payload[IAAPayloadKey.Placement]);
            Assert.AreEqual("banner",           payload[IAAPayloadKey.AdType]);
            Assert.AreEqual("unit-001",         payload[IAAPayloadKey.AdUnitId]);
            Assert.AreEqual("Banner Top",       payload[IAAPayloadKey.AdUnitName]);
            Assert.AreEqual(0.005,              (double)payload[IAAPayloadKey.Value],   delta: 0.0001);
            Assert.AreEqual("USD",              payload[IAAPayloadKey.Currency]);
            Assert.AreEqual(0.005,              (double)payload[IAAPayloadKey.ValueUsd], delta: 0.0001);
            Assert.AreEqual("banner",           payload[IAAPayloadKey.AdFormat]);  // mirrors adType
            Assert.AreEqual(IAAAdSize.Banner320, payload[IAAPayloadKey.AdSize]);
            Assert.AreEqual("AppLovin",         payload[IAAPayloadKey.AdSource]);
            Assert.AreEqual("applovin_max",     payload[IAAPayloadKey.AdPlatform]);
            Assert.AreEqual(3000L,              payload[IAAPayloadKey.EngagementTime]);
        }

        [Test]
        public void BuildAdImpression_NullStrings_CoercedToUnknown()
        {
            var payload = IAAPayloadBuilder.BuildAdImpression(
                placement: null, adType: null, adUnitId: null, adUnitName: null,
                value: 0.0, valueUsd: 0.0,
                adSize: null, adSource: null, adPlatform: null,
                engagementTimeMs: 0L);

            Assert.AreEqual("unknown", payload[IAAPayloadKey.Placement]);
            Assert.AreEqual("unknown", payload[IAAPayloadKey.AdType]);
            Assert.AreEqual("unknown", payload[IAAPayloadKey.AdUnitId]);
            Assert.AreEqual("unknown", payload[IAAPayloadKey.AdUnitName]);
            Assert.AreEqual("unknown", payload[IAAPayloadKey.AdSize]);
            Assert.AreEqual("unknown", payload[IAAPayloadKey.AdSource]);
            Assert.AreEqual("unknown", payload[IAAPayloadKey.AdPlatform]);
        }

        [Test]
        public void BuildAdImpression_CurrencyAlwaysUsd()
        {
            var payload = IAAPayloadBuilder.BuildAdImpression(
                "p", "interstitial", "uid", "uname",
                1.23, 1.23, IAAAdSize.Fullscreen, "src", "platform", 1000L);

            Assert.AreEqual("USD", payload[IAAPayloadKey.Currency]);
        }

        [Test]
        public void BuildAdImpression_AdFormatMirrorsAdType()
        {
            var payload = IAAPayloadBuilder.BuildAdImpression(
                "p", "rewarded", "uid", "uname",
                0.0, 0.0, IAAAdSize.Fullscreen, "src", "platform", 0L);

            // ad_format must equal adType
            Assert.AreEqual(payload[IAAPayloadKey.AdType], payload[IAAPayloadKey.AdFormat]);
        }

        [Test]
        public void BuildAdImpression_HasTwelveKeys()
        {
            var payload = IAAPayloadBuilder.BuildAdImpression(
                "p", "t", "uid", "uname",
                0.0, 0.0, "320x50", "src", "plat", 0L);

            Assert.AreEqual(12, payload.Count, "BuildAdImpression must produce exactly 12 canonical keys");
        }

        // ─── BuildAdLoaded ────────────────────────────────────────────────────

        [Test]
        public void BuildAdLoaded_PopulatesEightKeys()
        {
            var payload = IAAPayloadBuilder.BuildAdLoaded(
                "rewarded_mid", "rewarded", "unit-002", "Rewarded Mid",
                IAAAdSize.Fullscreen, "AdMob", "admob");

            Assert.AreEqual(8, payload.Count, "BuildAdLoaded must produce exactly 8 canonical keys");
            Assert.AreEqual("rewarded_mid",      payload[IAAPayloadKey.Placement]);
            Assert.AreEqual("rewarded",          payload[IAAPayloadKey.AdType]);
            Assert.AreEqual("unit-002",          payload[IAAPayloadKey.AdUnitId]);
            Assert.AreEqual("Rewarded Mid",      payload[IAAPayloadKey.AdUnitName]);
            Assert.AreEqual("rewarded",          payload[IAAPayloadKey.AdFormat]);
            Assert.AreEqual(IAAAdSize.Fullscreen, payload[IAAPayloadKey.AdSize]);
            Assert.AreEqual("AdMob",             payload[IAAPayloadKey.AdSource]);
            Assert.AreEqual("admob",             payload[IAAPayloadKey.AdPlatform]);
        }

        [Test]
        public void BuildAdLoaded_NullStrings_CoercedToUnknown()
        {
            var payload = IAAPayloadBuilder.BuildAdLoaded(null, null, null, null, null, null, null);

            foreach (var key in new[] {
                IAAPayloadKey.Placement, IAAPayloadKey.AdType, IAAPayloadKey.AdUnitId,
                IAAPayloadKey.AdUnitName, IAAPayloadKey.AdFormat, IAAPayloadKey.AdSize,
                IAAPayloadKey.AdSource, IAAPayloadKey.AdPlatform })
            {
                Assert.AreEqual("unknown", payload[key], $"Key '{key}' should be 'unknown' for null input");
            }
        }

        [Test]
        public void BuildAdLoaded_NullAdSize_UsesIAAAdSizeUnknown()
        {
            var payload = IAAPayloadBuilder.BuildAdLoaded("p", "t", "u", "n", null, "src", "plat");
            Assert.AreEqual(IAAAdSize.Unknown, payload[IAAPayloadKey.AdSize]);
        }

        // ─── BuildAdLoadFailed ────────────────────────────────────────────────

        [Test]
        public void BuildAdLoadFailed_PopulatesFourKeys()
        {
            var payload = IAAPayloadBuilder.BuildAdLoadFailed(
                "rewarded", "applovin_max", "Rewarded Mid", "[404] no fill");

            Assert.AreEqual(4, payload.Count);
            Assert.AreEqual("rewarded",       payload[IAAPayloadKey.AdFormat]);
            Assert.AreEqual("applovin_max",   payload[IAAPayloadKey.AdPlatform]);
            Assert.AreEqual("Rewarded Mid",   payload[IAAPayloadKey.AdUnitName]);
            Assert.AreEqual("[404] no fill",  payload[IAAPayloadKey.Error]);
        }

        [Test]
        public void BuildAdLoadFailed_NullStrings_CoercedToUnknown()
        {
            var payload = IAAPayloadBuilder.BuildAdLoadFailed(null, null, null, null);

            Assert.AreEqual("unknown", payload[IAAPayloadKey.AdFormat]);
            Assert.AreEqual("unknown", payload[IAAPayloadKey.AdPlatform]);
            Assert.AreEqual("unknown", payload[IAAPayloadKey.AdUnitName]);
            Assert.AreEqual("unknown", payload[IAAPayloadKey.Error]);
        }

        // ─── BuildAdShowFailed (delegates to BuildAdLoadFailed) ───────────────

        [Test]
        public void BuildAdShowFailed_SameShapeAsAdLoadFailed()
        {
            var failed = IAAPayloadBuilder.BuildAdLoadFailed("banner", "admob", "Banner", "err");
            var show   = IAAPayloadBuilder.BuildAdShowFailed("banner", "admob", "Banner", "err");

            // Both must produce identical key sets and values
            Assert.AreEqual(failed.Count, show.Count);
            foreach (var kv in failed)
                Assert.AreEqual(kv.Value, show[kv.Key], $"Key '{kv.Key}' differs");
        }

        [Test]
        public void BuildAdShowFailed_NullStrings_CoercedToUnknown()
        {
            var payload = IAAPayloadBuilder.BuildAdShowFailed(null, null, null, null);
            Assert.AreEqual("unknown", payload[IAAPayloadKey.AdFormat]);
            Assert.AreEqual("unknown", payload[IAAPayloadKey.Error]);
        }

        // ─── BuildAdClicked (delegates to BuildAdLoaded) ─────────────────────

        [Test]
        public void BuildAdClicked_SameShapeAsAdLoaded()
        {
            var loaded  = IAAPayloadBuilder.BuildAdLoaded("p", "banner", "uid", "uname", "320x50", "src", "plat");
            var clicked = IAAPayloadBuilder.BuildAdClicked("p", "banner", "uid", "uname", "320x50", "src", "plat");

            Assert.AreEqual(loaded.Count, clicked.Count);
            foreach (var kv in loaded)
                Assert.AreEqual(kv.Value, clicked[kv.Key], $"Key '{kv.Key}' differs");
        }

        [Test]
        public void BuildAdClicked_NullStrings_CoercedToUnknown()
        {
            var payload = IAAPayloadBuilder.BuildAdClicked(null, null, null, null, null, null, null);
            Assert.AreEqual("unknown", payload[IAAPayloadKey.Placement]);
            Assert.AreEqual("unknown", payload[IAAPayloadKey.AdType]);
        }

        // ─── BuildWatchMilestone ──────────────────────────────────────────────

        [Test]
        public void BuildWatchMilestone_PopulatesTwoKeys()
        {
            var payload = IAAPayloadBuilder.BuildWatchMilestone("rewarded", 5);

            Assert.AreEqual(2, payload.Count);
            Assert.AreEqual("rewarded", payload[IAAPayloadKey.AdType]);
            Assert.AreEqual(5,          payload[IAAPayloadKey.Count]);
        }

        [Test]
        public void BuildWatchMilestone_NullAdType_CoercedToUnknown()
        {
            var payload = IAAPayloadBuilder.BuildWatchMilestone(null, 10);
            Assert.AreEqual("unknown", payload[IAAPayloadKey.AdType]);
            Assert.AreEqual(10,        payload[IAAPayloadKey.Count]);
        }

        [Test]
        public void BuildWatchMilestone_ZeroCount_Stored()
        {
            var payload = IAAPayloadBuilder.BuildWatchMilestone("interstitial", 0);
            Assert.AreEqual(0, payload[IAAPayloadKey.Count]);
        }

        // ─── FormatError (code + message overload) ────────────────────────────

        [Test]
        public void FormatError_CodeAndMessage_FormatsCorrectly()
        {
            var result = IAAPayloadBuilder.FormatError(404, "no fill");
            Assert.AreEqual("[404] no fill", result);
        }

        [Test]
        public void FormatError_NullMessage_UsesUnknown()
        {
            var result = IAAPayloadBuilder.FormatError(500, null);
            Assert.AreEqual("[500] unknown", result);
        }

        [Test]
        public void FormatError_EmptyMessage_UsesUnknown()
        {
            var result = IAAPayloadBuilder.FormatError(0, "");
            Assert.AreEqual("[0] unknown", result);
        }

        [Test]
        public void FormatError_WithMediatorCode_FormatsFullString()
        {
            var result = IAAPayloadBuilder.FormatError(2, "timeout", 99, "mediation fail");
            Assert.AreEqual("[2] timeout | mediator [99] mediation fail", result);
        }

        [Test]
        public void FormatError_WithMediatorCode_NullMediatorMessage_UsesUnknown()
        {
            var result = IAAPayloadBuilder.FormatError(3, "err", 7, null);
            Assert.AreEqual("[3] err | mediator [7] unknown", result);
        }

        [Test]
        public void FormatError_WithMediatorCode_EmptyMediatorMessage_UsesUnknown()
        {
            var result = IAAPayloadBuilder.FormatError(3, "err", 7, "");
            Assert.AreEqual("[3] err | mediator [7] unknown", result);
        }

        [Test]
        public void FormatError_NullMessageWithMediatorCode_BothUnknown()
        {
            var result = IAAPayloadBuilder.FormatError(1, null, 42, null);
            Assert.AreEqual("[1] unknown | mediator [42] unknown", result);
        }

        [Test]
        public void FormatError_NoMediatorCode_DoesNotContainMediatorText()
        {
            var result = IAAPayloadBuilder.FormatError(10, "some error");
            StringAssert.DoesNotContain("mediator", result);
        }

        // ─── FormatError (code + message + domain overload) ──────────────────

        [Test]
        public void FormatError_WithDomain_FormatsCorrectly()
        {
            var result = IAAPayloadBuilder.FormatError(403, "access denied", "com.google.ads");
            Assert.AreEqual("[403] access denied (domain=com.google.ads)", result);
        }

        [Test]
        public void FormatError_WithDomain_NullMessage_UsesUnknown()
        {
            var result = IAAPayloadBuilder.FormatError(1, null, "some.domain");
            Assert.AreEqual("[1] unknown (domain=some.domain)", result);
        }

        [Test]
        public void FormatError_WithDomain_NullDomain_UsesUnknown()
        {
            var result = IAAPayloadBuilder.FormatError(2, "msg", (string)null);
            Assert.AreEqual("[2] msg (domain=unknown)", result);
        }

        [Test]
        public void FormatError_WithDomain_EmptyDomain_UsesUnknown()
        {
            var result = IAAPayloadBuilder.FormatError(2, "msg", "");
            Assert.AreEqual("[2] msg (domain=unknown)", result);
        }

        [Test]
        public void FormatError_WithDomain_BothNullMessageAndDomain_AllUnknown()
        {
            var result = IAAPayloadBuilder.FormatError(0, null, (string)null);
            Assert.AreEqual("[0] unknown (domain=unknown)", result);
        }

        // ─── Cross-cutting: NoctuaConfig constants and defaults ───────────────

        [Test]
        public void NoctuaConfig_DefaultTrackerUrl_IsCorrect()
        {
            Assert.AreEqual(
                "https://sdk-tracker.noctuaprojects.com/api/v1",
                NoctuaConfig.DefaultTrackerUrl);
        }

        [Test]
        public void NoctuaConfig_DefaultBaseUrl_IsCorrect()
        {
            Assert.AreEqual(
                "https://sdk-api-v2.noctuaprojects.com/api/v1",
                NoctuaConfig.DefaultBaseUrl);
        }

        [Test]
        public void NoctuaConfig_DefaultSandboxBaseUrl_IsCorrect()
        {
            Assert.AreEqual(
                "https://sandbox-sdk-api-v2.noctuaprojects.com/api/v1",
                NoctuaConfig.DefaultSandboxBaseUrl);
        }

        [Test]
        public void NoctuaConfig_DefaultFieldValues_AreCorrect()
        {
            var cfg = new NoctuaConfig();

            Assert.AreEqual(NoctuaConfig.DefaultTrackerUrl,          cfg.TrackerUrl);
            Assert.AreEqual(NoctuaConfig.DefaultBaseUrl,             cfg.BaseUrl);
            Assert.AreEqual(NoctuaConfig.DefaultAnnouncementBaseUrl, cfg.AnnouncementBaseUrl);
            Assert.AreEqual(NoctuaConfig.DefaultRewardBaseUrl,       cfg.RewardBaseUrl);
            Assert.AreEqual(NoctuaConfig.DefaultSocialMediaBaseUrl,  cfg.SocialMediaBaseUrl);
            Assert.AreEqual(NoctuaConfig.DefaultCustomerServiceBaseUrl, cfg.CustomerServiceBaseUrl);
            Assert.AreEqual("",      cfg.SentryDsnUrl);
            Assert.AreEqual(20u,     cfg.TrackerBatchSize);
            Assert.AreEqual(60_000u, cfg.TrackerBatchPeriodMs);
            Assert.AreEqual(60_000u, cfg.SessionHeartbeatPeriodMs);
            Assert.AreEqual(900_000u, cfg.SessionTimeoutMs);
            Assert.IsFalse(cfg.IsSandbox);
            Assert.IsFalse(cfg.welcomeToastDisabled);
            Assert.IsFalse(cfg.isIAAEnabled);
            Assert.IsFalse(cfg.isIAPDisabled);
            Assert.IsFalse(cfg.IsOfflineFirst);
            Assert.IsNull(cfg.Region);
            Assert.IsNull(cfg.RemoteFeatureFlags);
        }
    }
}
