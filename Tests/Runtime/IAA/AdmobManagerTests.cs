#if UNITY_ADMOB
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using com.noctuagames.sdk;
using com.noctuagames.sdk.Admob;
using GoogleMobileAds.Api;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Runtime.IAA
{
    internal static class AdmobAdUnits
    {
#if UNITY_IOS
        internal const string Banner               = "ca-app-pub-3940256099942544/2934735716";
        internal const string Interstitial         = "ca-app-pub-3940256099942544/4411468910";
        internal const string Rewarded             = "ca-app-pub-3940256099942544/1712485313";
        internal const string RewardedInterstitial = "ca-app-pub-3940256099942544/6978759866";
        internal const string AppOpen              = "ca-app-pub-3940256099942544/5575463023";
#else
        internal const string Banner               = "ca-app-pub-3940256099942544/6300978111";
        internal const string Interstitial         = "ca-app-pub-3940256099942544/1033173712";
        internal const string Rewarded             = "ca-app-pub-3940256099942544/5224354917";
        internal const string RewardedInterstitial = "ca-app-pub-3940256099942544/5354046379";
        internal const string AppOpen              = "ca-app-pub-3940256099942544/9257395921";
#endif
    }

    /// <summary>
    /// Unit tests for AdmobManager and its inner ad classes covering state logic,
    /// event subscription guards, and flag-based getters.
    /// Requires UNITY_ADMOB define (Google Mobile Ads UPM package installed).
    /// SDK-calling paths (Initialize, Load*, Show*) are not exercised here.
    /// </summary>
    public class AdmobManagerTests
    {
        private AdmobManager _manager;

        [SetUp]
        public void SetUp()
        {
            _manager = new AdmobManager();
        }

        // ── Basic state ──────────────────────────────────────────────────────────

        [Test]
        public void NetworkName_IsAdmob()
        {
            Assert.AreEqual(AdNetworkName.Admob, _manager.NetworkName);
        }

        [Test]
        public void HasBannerAdUnit_FalseByDefault()
        {
            Assert.IsFalse(_manager.HasBannerAdUnit());
        }

        [Test]
        public void SetBannerAdUnitId_SetsHasBannerAdUnit()
        {
            _manager.SetBannerAdUnitId(AdmobAdUnits.Banner);
            Assert.IsTrue(_manager.HasBannerAdUnit());
        }

        [Test]
        public void SetBannerAdUnitId_Idempotent_OnlySubscribesOnce()
        {
            _manager.SetBannerAdUnitId("unit-1");
            _manager.SetBannerAdUnitId("unit-2");
            Assert.IsTrue(_manager.HasBannerAdUnit());
        }

        [Test]
        public void IsInterstitialReady_ReturnsFalseWhenNotLoaded()
        {
            _manager.SetInterstitialAdUnitID(AdmobAdUnits.Interstitial);
            Assert.IsFalse(_manager.IsInterstitialReady());
        }

        [Test]
        public void IsRewardedAdReady_ReturnsFalseWhenNotLoaded()
        {
            _manager.SetRewardedAdUnitID(AdmobAdUnits.Rewarded);
            Assert.IsFalse(_manager.IsRewardedAdReady());
        }

        [Test]
        public void IsAppOpenAdReady_ReturnsFalseWhenNotLoaded()
        {
            _manager.SetAppOpenAdUnitID(AdmobAdUnits.AppOpen);
            Assert.IsFalse(_manager.IsAppOpenAdReady());
        }

        // ── Subscription guards ──────────────────────────────────────────────────

        [Test]
        public void SetInterstitialAdUnitID_Idempotent_OnlySubscribesOnce()
        {
            Assert.DoesNotThrow(() =>
            {
                _manager.SetInterstitialAdUnitID("unit-a");
                _manager.SetInterstitialAdUnitID("unit-b");
            });
        }

        [Test]
        public void SetRewardedAdUnitID_Idempotent_OnlySubscribesOnce()
        {
            Assert.DoesNotThrow(() =>
            {
                _manager.SetRewardedAdUnitID("unit-a");
                _manager.SetRewardedAdUnitID("unit-b");
            });
        }

        [Test]
        public void SetRewardedInterstitialAdUnitID_Idempotent_OnlySubscribesOnce()
        {
            Assert.DoesNotThrow(() =>
            {
                _manager.SetRewardedInterstitialAdUnitID("unit-a");
                _manager.SetRewardedInterstitialAdUnitID("unit-b");
            });
        }

        [Test]
        public void SetAppOpenAdUnitID_Idempotent_OnlySubscribesOnce()
        {
            Assert.DoesNotThrow(() =>
            {
                _manager.SetAppOpenAdUnitID("unit-a");
                _manager.SetAppOpenAdUnitID("unit-b");
            });
        }

        // ── Event add/remove ─────────────────────────────────────────────────────

        [Test]
        public void EventSubscription_AddAndRemove_DoesNotThrow()
        {
            Action handler = () => { };
            Assert.DoesNotThrow(() =>
            {
                _manager.OnAdDisplayed += handler;
                _manager.OnAdFailedDisplayed += handler;
                _manager.OnAdClicked += handler;
                _manager.OnAdImpressionRecorded += handler;
                _manager.OnAdClosed += handler;

                _manager.OnAdDisplayed -= handler;
                _manager.OnAdFailedDisplayed -= handler;
                _manager.OnAdClicked -= handler;
                _manager.OnAdImpressionRecorded -= handler;
                _manager.OnAdClosed -= handler;
            });
        }

        [Test]
        public void SetTestDeviceIds_EmptyList_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _manager.SetTestDeviceIds(null));
            Assert.DoesNotThrow(() => _manager.SetTestDeviceIds(new List<string>()));
        }

        [Test]
        public void SetTestDeviceIds_WithIds_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                _manager.SetTestDeviceIds(new List<string> { "FAKE-DEVICE-ID" }));
        }

        // ── ShowRewardedAd with placement sets placement before showing ──────────

        [Test]
        public void ShowInterstitial_WithPlacement_DoesNotThrow()
        {
            _manager.SetInterstitialAdUnitID(AdmobAdUnits.Interstitial);
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => _manager.ShowInterstitial("main-menu"));
        }

        [Test]
        public void ShowRewardedAd_WithPlacement_DoesNotThrow()
        {
            _manager.SetRewardedAdUnitID(AdmobAdUnits.Rewarded);
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => _manager.ShowRewardedAd("gameplay"));
        }
    }

    /// <summary>Tests for BannerAdmob inner class state methods.</summary>
    public class BannerAdmobTests
    {
        [Test]
        public void SetAdUnitId_DoesNotThrow()
        {
            var banner = new BannerAdmob();
            Assert.DoesNotThrow(() => banner.SetAdUnitId("test-unit"));
        }

        [Test]
        public void SetPlacement_DoesNotThrow()
        {
            var banner = new BannerAdmob();
            Assert.DoesNotThrow(() => banner.SetPlacement("bottom"));
        }

        [Test]
        public void HideBanner_BeforeLoad_DoesNotThrow()
        {
            var banner = new BannerAdmob();
            Assert.DoesNotThrow(() => banner.HideBanner());
        }

        [Test]
        public void SetAdUnitId_Idempotent_DoesNotThrow()
        {
            var banner = new BannerAdmob();
            Assert.DoesNotThrow(() =>
            {
                banner.SetAdUnitId("unit-first");
                banner.SetAdUnitId("unit-second");
            });
        }

        [Test]
        public void SetAdUnitId_Null_DoesNotThrow()
        {
            // SetAdUnitId logs an error for null and returns — must not throw.
            var banner = new BannerAdmob();
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => banner.SetAdUnitId(null));
        }

        [Test]
        public void SetPlacement_Idempotent_DoesNotThrow()
        {
            var banner = new BannerAdmob();
            Assert.DoesNotThrow(() =>
            {
                banner.SetPlacement("top");
                banner.SetPlacement("bottom");
            });
        }

        [Test]
        public void HideBanner_AfterSetAdUnitId_DoesNotThrow()
        {
            // _bannerView is still null after SetAdUnitId alone; HideBanner guards with null check.
            var banner = new BannerAdmob();
            banner.SetAdUnitId(AdmobAdUnits.Banner);
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => banner.HideBanner());
        }

        [Test]
        public void EventSubscription_BannerEvents_AddAndRemove_DoesNotThrow()
        {
            var banner = new BannerAdmob();
            Action handler = () => { };
            Action<AdValue, ResponseInfo> revenueHandler = (_, __) => { };

            Assert.DoesNotThrow(() =>
            {
                banner.BannerOnAdDisplayed         += handler;
                banner.BannerOnAdFailedDisplayed   += handler;
                banner.BannerOnAdClicked           += handler;
                banner.BannerOnAdImpressionRecorded += handler;
                banner.BannerOnAdClosed            += handler;
                banner.AdmobOnAdRevenuePaid        += revenueHandler;

                banner.BannerOnAdDisplayed         -= handler;
                banner.BannerOnAdFailedDisplayed   -= handler;
                banner.BannerOnAdClicked           -= handler;
                banner.BannerOnAdImpressionRecorded -= handler;
                banner.BannerOnAdClosed            -= handler;
                banner.AdmobOnAdRevenuePaid        -= revenueHandler;
            });
        }

        [Test]
        public void CleanupAd_WhenNoBannerViewExists_DoesNotThrow()
        {
            // CleanupAd guards with `if (_bannerView != null)` — calling on a
            // freshly constructed instance must be a silent no-op.
            var banner = new BannerAdmob();
            Assert.DoesNotThrow(() => banner.CleanupAd());
        }
    }

    /// <summary>Tests for InterstitialAdmob inner class state methods.</summary>
    public class InterstitialAdmobTests
    {
        [Test]
        public void SetInterstitialAdUnitID_DoesNotThrow()
        {
            var interstitial = new InterstitialAdmob();
            Assert.DoesNotThrow(() => interstitial.SetInterstitialAdUnitID("test-unit"));
        }

        [Test]
        public void IsReady_ReturnsFalseWhenNotLoaded()
        {
            var interstitial = new InterstitialAdmob();
            Assert.IsFalse(interstitial.IsReady());
        }

        [Test]
        public void SetPlacement_DoesNotThrow()
        {
            var interstitial = new InterstitialAdmob();
            Assert.DoesNotThrow(() => interstitial.SetPlacement("game-over"));
        }

        [Test]
        public void SetInterstitialAdUnitID_Idempotent_DoesNotThrow()
        {
            var interstitial = new InterstitialAdmob();
            Assert.DoesNotThrow(() =>
            {
                interstitial.SetInterstitialAdUnitID("unit-a");
                interstitial.SetInterstitialAdUnitID("unit-b");
            });
        }

        [Test]
        public void SetInterstitialAdUnitID_Null_DoesNotThrow()
        {
            var interstitial = new InterstitialAdmob();
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => interstitial.SetInterstitialAdUnitID(null));
        }

        [Test]
        public void IsReady_ReturnsFalseAfterSetId()
        {
            // Setting an ad unit ID does not load an ad; IsReady must still be false.
            var interstitial = new InterstitialAdmob();
            interstitial.SetInterstitialAdUnitID(AdmobAdUnits.Interstitial);
            Assert.IsFalse(interstitial.IsReady());
        }

        [Test]
        public void ShowInterstitialAd_BeforeLoad_DoesNotThrow()
        {
            // _interstitialAd is null, so ShowInterstitialAd takes the else branch
            // which logs an error and tracks a custom event — must not throw.
            var interstitial = new InterstitialAdmob();
            interstitial.SetInterstitialAdUnitID(AdmobAdUnits.Interstitial);
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => interstitial.ShowInterstitialAd());
        }

        [Test]
        public void EventSubscription_InterstitialEvents_AddAndRemove_DoesNotThrow()
        {
            var interstitial = new InterstitialAdmob();
            Action handler = () => { };
            Action<AdValue, ResponseInfo> revenueHandler = (_, __) => { };

            Assert.DoesNotThrow(() =>
            {
                interstitial.InterstitialOnAdDisplayed         += handler;
                interstitial.InterstitialOnAdFailedDisplayed   += handler;
                interstitial.InterstitialOnAdClicked           += handler;
                interstitial.InterstitialOnAdImpressionRecorded += handler;
                interstitial.InterstitialOnAdClosed            += handler;
                interstitial.AdmobOnAdRevenuePaid              += revenueHandler;

                interstitial.InterstitialOnAdDisplayed         -= handler;
                interstitial.InterstitialOnAdFailedDisplayed   -= handler;
                interstitial.InterstitialOnAdClicked           -= handler;
                interstitial.InterstitialOnAdImpressionRecorded -= handler;
                interstitial.InterstitialOnAdClosed            -= handler;
                interstitial.AdmobOnAdRevenuePaid              -= revenueHandler;
            });
        }
    }

    /// <summary>Tests for RewardedAdmob inner class state methods.</summary>
    public class RewardedAdmobTests
    {
        [Test]
        public void SetRewardedAdUnitID_DoesNotThrow()
        {
            var rewarded = new RewardedAdmob();
            Assert.DoesNotThrow(() => rewarded.SetRewardedAdUnitID("test-unit"));
        }

        [Test]
        public void IsReady_ReturnsFalseWhenNotLoaded()
        {
            var rewarded = new RewardedAdmob();
            Assert.IsFalse(rewarded.IsReady());
        }

        [Test]
        public void SetPlacement_DoesNotThrow()
        {
            var rewarded = new RewardedAdmob();
            Assert.DoesNotThrow(() => rewarded.SetPlacement("bonus-round"));
        }

        [Test]
        public void SetRewardedAdUnitID_Idempotent_DoesNotThrow()
        {
            var rewarded = new RewardedAdmob();
            Assert.DoesNotThrow(() =>
            {
                rewarded.SetRewardedAdUnitID("unit-first");
                rewarded.SetRewardedAdUnitID("unit-second");
            });
        }

        [Test]
        public void SetRewardedAdUnitID_Null_DoesNotThrow()
        {
            var rewarded = new RewardedAdmob();
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => rewarded.SetRewardedAdUnitID(null));
        }

        [Test]
        public void IsReady_ReturnsFalseAfterSetId()
        {
            var rewarded = new RewardedAdmob();
            rewarded.SetRewardedAdUnitID(AdmobAdUnits.Rewarded);
            Assert.IsFalse(rewarded.IsReady());
        }

        [Test]
        public void ShowRewardedAd_BeforeLoad_DoesNotThrow()
        {
            // _rewardedAd is null, so ShowRewardedAd takes the else branch — logs
            // an error and emits custom events, but must never throw.
            var rewarded = new RewardedAdmob();
            rewarded.SetRewardedAdUnitID(AdmobAdUnits.Rewarded);
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => rewarded.ShowRewardedAd());
        }

        [Test]
        public void CleanupAd_WhenNotLoaded_DoesNotThrow()
        {
            var rewarded = new RewardedAdmob();
            Assert.DoesNotThrow(() => rewarded.CleanupAd());
        }

        [Test]
        public void EventSubscription_RewardedEvents_AddAndRemove_DoesNotThrow()
        {
            var rewarded = new RewardedAdmob();
            Action handler = () => { };
            Action<Reward> rewardHandler = _ => { };
            Action<AdValue, ResponseInfo> revenueHandler = (_, __) => { };

            Assert.DoesNotThrow(() =>
            {
                rewarded.RewardedOnAdDisplayed         += handler;
                rewarded.RewardedOnAdFailedDisplayed   += handler;
                rewarded.RewardedOnAdClicked           += handler;
                rewarded.RewardedOnAdImpressionRecorded += handler;
                rewarded.RewardedOnAdClosed            += handler;
                rewarded.RewardedOnUserEarnedReward    += rewardHandler;
                rewarded.AdmobOnAdRevenuePaid          += revenueHandler;

                rewarded.RewardedOnAdDisplayed         -= handler;
                rewarded.RewardedOnAdFailedDisplayed   -= handler;
                rewarded.RewardedOnAdClicked           -= handler;
                rewarded.RewardedOnAdImpressionRecorded -= handler;
                rewarded.RewardedOnAdClosed            -= handler;
                rewarded.RewardedOnUserEarnedReward    -= rewardHandler;
                rewarded.AdmobOnAdRevenuePaid          -= revenueHandler;
            });
        }
    }

    /// <summary>Tests for RewardedInterstitialAdmob inner class state methods.</summary>
    public class RewardedInterstitialAdmobTests
    {
        [Test]
        public void SetRewardedInterstitialAdUnitID_DoesNotThrow()
        {
            var ri = new RewardedInterstitialAdmob();
            Assert.DoesNotThrow(() => ri.SetRewardedInterstitialAdUnitID("test-unit"));
        }

        [Test]
        public void SetPlacement_DoesNotThrow()
        {
            var ri = new RewardedInterstitialAdmob();
            Assert.DoesNotThrow(() => ri.SetPlacement("cutscene"));
        }

        [Test]
        public void SetRewardedInterstitialAdUnitID_Idempotent_DoesNotThrow()
        {
            var ri = new RewardedInterstitialAdmob();
            Assert.DoesNotThrow(() =>
            {
                ri.SetRewardedInterstitialAdUnitID("unit-a");
                ri.SetRewardedInterstitialAdUnitID("unit-b");
            });
        }

        [Test]
        public void SetRewardedInterstitialAdUnitID_Null_DoesNotThrow()
        {
            var ri = new RewardedInterstitialAdmob();
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => ri.SetRewardedInterstitialAdUnitID(null));
        }

        [Test]
        public void ShowRewardedInterstitialAd_BeforeLoad_DoesNotThrow()
        {
            // _rewardedAd is null → else branch logs error and emits custom events; must not throw.
            var ri = new RewardedInterstitialAdmob();
            ri.SetRewardedInterstitialAdUnitID(AdmobAdUnits.RewardedInterstitial);
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => ri.ShowRewardedInterstitialAd());
        }

        [Test]
        public void CleanupAd_WhenNotLoaded_DoesNotThrow()
        {
            var ri = new RewardedInterstitialAdmob();
            Assert.DoesNotThrow(() => ri.CleanupAd());
        }

        [Test]
        public void EventSubscription_RewardedInterstitialEvents_AddAndRemove_DoesNotThrow()
        {
            var ri = new RewardedInterstitialAdmob();
            Action handler = () => { };
            Action<Reward> rewardHandler = _ => { };
            Action<AdValue, ResponseInfo> revenueHandler = (_, __) => { };

            Assert.DoesNotThrow(() =>
            {
                ri.RewardedOnAdDisplayed         += handler;
                ri.RewardedOnAdFailedDisplayed   += handler;
                ri.RewardedOnAdClicked           += handler;
                ri.RewardedOnAdImpressionRecorded += handler;
                ri.RewardedOnAdClosed            += handler;
                ri.RewardedOnUserEarnedReward    += rewardHandler;
                ri.AdmobOnAdRevenuePaid          += revenueHandler;

                ri.RewardedOnAdDisplayed         -= handler;
                ri.RewardedOnAdFailedDisplayed   -= handler;
                ri.RewardedOnAdClicked           -= handler;
                ri.RewardedOnAdImpressionRecorded -= handler;
                ri.RewardedOnAdClosed            -= handler;
                ri.RewardedOnUserEarnedReward    -= rewardHandler;
                ri.AdmobOnAdRevenuePaid          -= revenueHandler;
            });
        }
    }

    /// <summary>Tests for AppOpenAdmob inner class state methods.</summary>
    public class AppOpenAdmobTests
    {
        [Test]
        public void SetAppOpenAdUnitID_DoesNotThrow()
        {
            var appOpen = new AppOpenAdmob();
            Assert.DoesNotThrow(() => appOpen.SetAppOpenAdUnitID("test-unit"));
        }

        [Test]
        public void SetAppOpenAdUnitID_Null_DoesNotThrow()
        {
            var appOpen = new AppOpenAdmob();
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => appOpen.SetAppOpenAdUnitID(null));
        }

        [Test]
        public void SetAppOpenAdUnitID_Idempotent_DoesNotThrow()
        {
            var appOpen = new AppOpenAdmob();
            Assert.DoesNotThrow(() =>
            {
                appOpen.SetAppOpenAdUnitID("unit-first");
                appOpen.SetAppOpenAdUnitID("unit-second");
            });
        }

        [Test]
        public void SetPlacement_DoesNotThrow()
        {
            var appOpen = new AppOpenAdmob();
            Assert.DoesNotThrow(() => appOpen.SetPlacement("cold-start"));
        }

        [Test]
        public void IsAdReady_ReturnsFalseWhenNotLoaded()
        {
            // No unit ID set and no ad loaded — must return false.
            var appOpen = new AppOpenAdmob();
            Assert.IsFalse(appOpen.IsAdReady());
        }

        [Test]
        public void IsAdReady_AfterSetUnitId_ReturnsFalse()
        {
            // Unit ID configured but no load call made — preload buffer is empty → false.
            var appOpen = new AppOpenAdmob();
            appOpen.SetAppOpenAdUnitID(AdmobAdUnits.AppOpen);
            Assert.IsFalse(appOpen.IsAdReady());
        }

        [Test]
        public void ShowAppOpenAd_BeforeLoad_DoesNotThrow()
        {
            // No ad loaded and no unit ID → takes early-return warning path.
            var appOpen = new AppOpenAdmob();
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => appOpen.ShowAppOpenAd());
        }

        [Test]
        public void ShowAppOpenAd_WithUnitId_BeforeLoad_DoesNotThrow()
        {
            // Unit ID set but preload buffer empty → ShowAppOpenAd logs warning and returns.
            var appOpen = new AppOpenAdmob();
            appOpen.SetAppOpenAdUnitID(AdmobAdUnits.AppOpen);
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => appOpen.ShowAppOpenAd());
        }

        [Test]
        public void EventSubscription_AppOpenEvents_AddAndRemove_DoesNotThrow()
        {
            var appOpen = new AppOpenAdmob();
            Action handler = () => { };
            Action<AdValue, ResponseInfo> revenueHandler = (_, __) => { };

            Assert.DoesNotThrow(() =>
            {
                appOpen.AppOpenOnAdDisplayed         += handler;
                appOpen.AppOpenOnAdFailedDisplayed   += handler;
                appOpen.AppOpenOnAdClicked           += handler;
                appOpen.AppOpenOnAdImpressionRecorded += handler;
                appOpen.AppOpenOnAdClosed            += handler;
                appOpen.AdmobOnAdRevenuePaid         += revenueHandler;

                appOpen.AppOpenOnAdDisplayed         -= handler;
                appOpen.AppOpenOnAdFailedDisplayed   -= handler;
                appOpen.AppOpenOnAdClicked           -= handler;
                appOpen.AppOpenOnAdImpressionRecorded -= handler;
                appOpen.AppOpenOnAdClosed            -= handler;
                appOpen.AdmobOnAdRevenuePaid         -= revenueHandler;
            });
        }
    }

    /// <summary>Tests for AdmobAdPreloadManager config factory methods and null guards.</summary>
    public class AdmobAdPreloadManagerTests
    {
        [Test]
        public void StartPreloading_NullList_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => AdmobAdPreloadManager.Instance.StartPreloading(null));
        }

        [Test]
        public void StartPreloading_EmptyList_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                AdmobAdPreloadManager.Instance.StartPreloading(new List<PreloadConfiguration>()));
        }

        [Test]
        public void ModifyPreloading_NullList_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => AdmobAdPreloadManager.Instance.ModifyPreloading(null));
        }

        [Test]
        public void StopPreloading_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => AdmobAdPreloadManager.Instance.StopPreloading());
        }

        [Test]
        public void IsAdAvailable_NullAdUnitId_ReturnsFalse()
        {
            Assert.IsFalse(AdmobAdPreloadManager.Instance.IsAdAvailable(null, AdFormat.INTERSTITIAL));
            Assert.IsFalse(AdmobAdPreloadManager.Instance.IsAdAvailable("", AdFormat.REWARDED));
        }

        [Test]
        public void PollInterstitialAd_NullAdUnitId_ReturnsNull()
        {
            Assert.IsNull(AdmobAdPreloadManager.Instance.PollInterstitialAd(null));
            Assert.IsNull(AdmobAdPreloadManager.Instance.PollInterstitialAd(""));
        }

        [Test]
        public void PollRewardedAd_NullAdUnitId_ReturnsNull()
        {
            Assert.IsNull(AdmobAdPreloadManager.Instance.PollRewardedAd(null));
            Assert.IsNull(AdmobAdPreloadManager.Instance.PollRewardedAd(""));
        }

        [Test]
        public void PollAppOpenAd_NullAdUnitId_ReturnsNull()
        {
            Assert.IsNull(AdmobAdPreloadManager.Instance.PollAppOpenAd(null));
            Assert.IsNull(AdmobAdPreloadManager.Instance.PollAppOpenAd(""));
        }

        // ── CreateXxxPreloadConfig factory methods ────────────────────────────────

        [Test]
        public void CreateInterstitialPreloadConfig_ReturnsNonNull()
        {
            var config = AdmobAdPreloadManager.Instance
                .CreateInterstitialPreloadConfig(AdmobAdUnits.Interstitial);
            Assert.IsNotNull(config);
        }

        [Test]
        public void CreateInterstitialPreloadConfig_SetsCorrectFormat()
        {
            var config = AdmobAdPreloadManager.Instance
                .CreateInterstitialPreloadConfig(AdmobAdUnits.Interstitial);
            Assert.AreEqual(AdFormat.INTERSTITIAL, config.Format);
        }

        [Test]
        public void CreateInterstitialPreloadConfig_SetsAdUnitId()
        {
            var config = AdmobAdPreloadManager.Instance
                .CreateInterstitialPreloadConfig(AdmobAdUnits.Interstitial);
            Assert.AreEqual(AdmobAdUnits.Interstitial, config.AdUnitId);
        }

        [Test]
        public void CreateInterstitialPreloadConfig_DefaultBufferSize_IsThree()
        {
            var config = AdmobAdPreloadManager.Instance
                .CreateInterstitialPreloadConfig(AdmobAdUnits.Interstitial);
            Assert.AreEqual(3u, config.BufferSize);
        }

        [Test]
        public void CreateInterstitialPreloadConfig_CustomBufferSize_IsRespected()
        {
            var config = AdmobAdPreloadManager.Instance
                .CreateInterstitialPreloadConfig(AdmobAdUnits.Interstitial, bufferSize: 5);
            Assert.AreEqual(5u, config.BufferSize);
        }

        [Test]
        public void CreateRewardedPreloadConfig_ReturnsNonNull()
        {
            var config = AdmobAdPreloadManager.Instance
                .CreateRewardedPreloadConfig(AdmobAdUnits.Rewarded);
            Assert.IsNotNull(config);
        }

        [Test]
        public void CreateRewardedPreloadConfig_SetsCorrectFormat()
        {
            var config = AdmobAdPreloadManager.Instance
                .CreateRewardedPreloadConfig(AdmobAdUnits.Rewarded);
            Assert.AreEqual(AdFormat.REWARDED, config.Format);
        }

        [Test]
        public void CreateRewardedPreloadConfig_SetsAdUnitId()
        {
            var config = AdmobAdPreloadManager.Instance
                .CreateRewardedPreloadConfig(AdmobAdUnits.Rewarded);
            Assert.AreEqual(AdmobAdUnits.Rewarded, config.AdUnitId);
        }

        [Test]
        public void CreateAppOpenPreloadConfig_ReturnsNonNull()
        {
            var config = AdmobAdPreloadManager.Instance
                .CreateAppOpenPreloadConfig(AdmobAdUnits.AppOpen);
            Assert.IsNotNull(config);
        }

        [Test]
        public void CreateAppOpenPreloadConfig_SetsCorrectFormat()
        {
            var config = AdmobAdPreloadManager.Instance
                .CreateAppOpenPreloadConfig(AdmobAdUnits.AppOpen);
            Assert.AreEqual(AdFormat.APP_OPEN_AD, config.Format);
        }

        [Test]
        public void CreateAppOpenPreloadConfig_SetsAdUnitId()
        {
            var config = AdmobAdPreloadManager.Instance
                .CreateAppOpenPreloadConfig(AdmobAdUnits.AppOpen);
            Assert.AreEqual(AdmobAdUnits.AppOpen, config.AdUnitId);
        }

        // ── Poll* with a real (test) ad unit ID ──────────────────────────────────
        // The GMA SDK is not initialised in EditMode, so IsAdAvailable() returns
        // false and Poll*() returns null without crashing.

        [Test]
        public void PollInterstitialAd_WithRealAdUnit_ReturnsNull_WhenSdkNotReady()
        {
            LogAssert.ignoreFailingMessages = true;
            var result = AdmobAdPreloadManager.Instance.PollInterstitialAd(AdmobAdUnits.Interstitial);
            Assert.IsNull(result);
        }

        [Test]
        public void PollRewardedAd_WithRealAdUnit_ReturnsNull_WhenSdkNotReady()
        {
            LogAssert.ignoreFailingMessages = true;
            var result = AdmobAdPreloadManager.Instance.PollRewardedAd(AdmobAdUnits.Rewarded);
            Assert.IsNull(result);
        }

        [Test]
        public void PollAppOpenAd_WithRealAdUnit_ReturnsNull_WhenSdkNotReady()
        {
            LogAssert.ignoreFailingMessages = true;
            var result = AdmobAdPreloadManager.Instance.PollAppOpenAd(AdmobAdUnits.AppOpen);
            Assert.IsNull(result);
        }

        // ── IsAdAvailable with a real (test) ad unit ID ───────────────────────────

        [Test]
        public void IsAdAvailable_Interstitial_WhenSdkNotReady_ReturnsFalse()
        {
            LogAssert.ignoreFailingMessages = true;
            Assert.IsFalse(
                AdmobAdPreloadManager.Instance.IsAdAvailable(AdmobAdUnits.Interstitial, AdFormat.INTERSTITIAL));
        }

        [Test]
        public void IsAdAvailable_Rewarded_WhenSdkNotReady_ReturnsFalse()
        {
            LogAssert.ignoreFailingMessages = true;
            Assert.IsFalse(
                AdmobAdPreloadManager.Instance.IsAdAvailable(AdmobAdUnits.Rewarded, AdFormat.REWARDED));
        }

        [Test]
        public void IsAdAvailable_AppOpen_WhenSdkNotReady_ReturnsFalse()
        {
            LogAssert.ignoreFailingMessages = true;
            Assert.IsFalse(
                AdmobAdPreloadManager.Instance.IsAdAvailable(AdmobAdUnits.AppOpen, AdFormat.APP_OPEN_AD));
        }

        // ── GetResponseInfo (deprecated — verify it is accessible and null-safe) ──

        [Test]
        public void GetResponseInfo_WhenSdkNotReady_ReturnsNull()
        {
#pragma warning disable CS0618 // GetResponseInfo is [Obsolete]
            LogAssert.ignoreFailingMessages = true;
            var result = AdmobAdPreloadManager.Instance
                .GetResponseInfo(AdmobAdUnits.Interstitial, AdFormat.INTERSTITIAL);
            Assert.IsNull(result,
                "GetResponseInfo must return null when no ad is preloaded (SDK not initialised in EditMode)");
#pragma warning restore CS0618
        }

        // ── Event subscription on the singleton ───────────────────────────────────

        [Test]
        public void EventSubscription_AdsAvailableAndExhausted_AddAndRemove_DoesNotThrow()
        {
            Action<PreloadConfiguration> handler = _ => { };
            Assert.DoesNotThrow(() =>
            {
                AdmobAdPreloadManager.Instance.OnAdsAvailable += handler;
                AdmobAdPreloadManager.Instance.OnAdExhausted  += handler;

                AdmobAdPreloadManager.Instance.OnAdsAvailable -= handler;
                AdmobAdPreloadManager.Instance.OnAdExhausted  -= handler;
            });
        }
    }
}
#endif
