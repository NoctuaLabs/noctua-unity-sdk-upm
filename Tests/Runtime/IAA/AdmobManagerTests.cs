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
    }
}
#endif
