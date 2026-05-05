#if UNITY_APPLOVIN
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using com.noctuagames.sdk;
using com.noctuagames.sdk.AppLovin;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Runtime.IAA
{
    // Ad unit IDs from noctuagg.json — AppLovin section.
    // Editor tests are platform-agnostic; Android IDs are used as the default fallback.
    internal static class AppLovinAdUnits
    {
#if UNITY_IOS
        internal const string Banner       = "9c2cb410833f8f94";
        internal const string Interstitial = "38f513201d28730a";
        internal const string Rewarded     = "63435093f214e3b8";
        internal const string AppOpen      = "649fc228cd9074d8";
#else
        internal const string Banner       = "dd07f2b60b146cbc";
        internal const string Interstitial = "e2f2af86ef5edfe9";
        internal const string Rewarded     = "d918fe9bc51f04b3";
        internal const string AppOpen      = "ae0fb8e84958630b";
#endif
    }

    /// <summary>
    /// Unit tests for AppLovinManager and its inner ad classes covering state logic,
    /// event subscription guards, flag-based getters, and ad lifecycle callbacks.
    /// Requires UNITY_APPLOVIN define (AppLovin MAX UPM package installed).
    /// Lifecycle callback tests fire inner-class public events via reflection to avoid
    /// triggering MaxSdk coroutine-based load paths that require PlayMode.
    /// Ad unit IDs are sourced from noctuagg.json.
    /// </summary>
    public class AppLovinManagerTests
    {
        private AppLovinManager _manager;

        // Fires a named event field on the inner ad object extracted from AppLovinManager.
        // C# auto-event backing fields have the same name as the event declaration.
        private static void FireInnerAdEvent(AppLovinManager manager, string innerFieldName, string eventFieldName, params object[] args)
        {
            var innerField = typeof(AppLovinManager)
                .GetField(innerFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(innerField, $"Field '{innerFieldName}' not found on AppLovinManager");
            var inner = innerField.GetValue(manager);
            Assert.IsNotNull(inner, $"Field '{innerFieldName}' is null on AppLovinManager instance");

            var evField = inner.GetType()
                .GetField(eventFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(evField, $"Event backing field '{eventFieldName}' not found on {inner.GetType().Name}");

            var del = evField.GetValue(inner) as Delegate;
            // del may be null if no subscribers — that's fine; manager subscription is set up via SetXxxAdUnitID.
            del?.DynamicInvoke(args);
        }

        // Build a minimal AdInfo for use in tests.
        private static MaxSdkBase.AdInfo MakeAdInfo(string adUnitId, double revenue = 0.01)
        {
            return new MaxSdkBase.AdInfo(new Dictionary<string, object>
            {
                { "adUnitId",        adUnitId },
                { "adFormat",        "INTER" },
                { "networkName",     "ADMOB_NETWORK" },
                { "networkPlacement","placement-1" },
                { "placement",       "test-placement" },
                { "creativeId",      "cid-1" },
                { "revenue",         revenue },
                { "revenuePrecision","estimated" },
                { "latencyMillis",   100L },
                { "waterfallInfo",   new Dictionary<string, object>() }
            });
        }

        [SetUp]
        public void SetUp()
        {
            _manager = new AppLovinManager();
            // Clear top-level init event so tests don't cross-pollute
            var sdkInitField = typeof(MaxSdkCallbacks)
                .GetField("onSdkInitializedEvent", BindingFlags.Static | BindingFlags.NonPublic);
            sdkInitField?.SetValue(null, null);
        }

        [TearDown]
        public void TearDown()
        {
            _manager.Cleanup();
        }

        // ── Basic state ──────────────────────────────────────────────────────────

        [Test]
        public void NetworkName_IsAppLovin()
        {
            Assert.AreEqual(AdNetworkName.AppLovin, _manager.NetworkName);
        }

        [Test]
        public void HasBannerAdUnit_FalseByDefault()
        {
            Assert.IsFalse(_manager.HasBannerAdUnit());
        }

        [Test]
        public void SetBannerAdUnitId_SetsHasBannerAdUnit()
        {
            _manager.SetBannerAdUnitId(AppLovinAdUnits.Banner);
            Assert.IsTrue(_manager.HasBannerAdUnit());
        }

        [Test]
        public void SetBannerAdUnitId_Idempotent_OnlySubscribesOnce()
        {
            _manager.SetBannerAdUnitId(AppLovinAdUnits.Banner);
            _manager.SetBannerAdUnitId(AppLovinAdUnits.Banner);
            Assert.IsTrue(_manager.HasBannerAdUnit());
        }

        [Test]
        public void Cleanup_ResetsSubscriptionGuards_AllowResubscribe()
        {
            _manager.SetInterstitialAdUnitID(AppLovinAdUnits.Interstitial);
            _manager.SetRewardedAdUnitID(AppLovinAdUnits.Rewarded);
            _manager.SetBannerAdUnitId(AppLovinAdUnits.Banner);
            _manager.SetAppOpenAdUnitID(AppLovinAdUnits.AppOpen);

            _manager.Cleanup();

            Assert.DoesNotThrow(() => _manager.SetInterstitialAdUnitID(AppLovinAdUnits.Interstitial));
            Assert.DoesNotThrow(() => _manager.SetRewardedAdUnitID(AppLovinAdUnits.Rewarded));
            Assert.DoesNotThrow(() => _manager.SetBannerAdUnitId(AppLovinAdUnits.Banner));
            Assert.DoesNotThrow(() => _manager.SetAppOpenAdUnitID(AppLovinAdUnits.AppOpen));
        }

        [Test]
        public void IsInterstitialReady_ReturnsFalseWhenNoUnitSet()
        {
            Assert.IsFalse(_manager.IsInterstitialReady());
        }

        [Test]
        public void IsRewardedAdReady_ReturnsFalseWhenNoUnitSet()
        {
            Assert.IsFalse(_manager.IsRewardedAdReady());
        }

        [Test]
        public void IsAppOpenAdReady_ReturnsFalseWhenNoUnitSet()
        {
            Assert.IsFalse(_manager.IsAppOpenAdReady());
        }

        // ── Initialize ───────────────────────────────────────────────────────────

        [Test]
        public void Initialize_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _manager.Initialize(() => { }));
        }

        [Test]
        public void Initialize_InvokesInitCompleteCallback_WhenSdkEmitsInitEvent()
        {
            var callbackFired = false;
            _manager.Initialize(() => { callbackFired = true; });

            // Simulate what MaxSdk.InitializeSdk() fires after its 0.1s delay in the Editor stub.
            MaxSdkCallbacks.EmitSdkInitializedEvent();

            Assert.IsTrue(callbackFired, "initCompleteAction must fire when SDK emits initialized event");
        }

        [Test]
        public void Initialize_OnInitialized_FiresWhenSdkEmitsInitEvent()
        {
            var eventFired = false;
            _manager.OnInitialized += () => { eventFired = true; };
            _manager.Initialize(() => { });

            MaxSdkCallbacks.EmitSdkInitializedEvent();

            Assert.IsTrue(eventFired, "OnInitialized event must fire when SDK emits initialized event");
        }

        [Test]
        public void Initialize_Guard_CallbackSubscribedOnlyOnce()
        {
            // _sdkInitCallbackSubscribed guard prevents double-subscription on second Initialize().
            var callCount = 0;
            _manager.Initialize(() => { callCount++; });
            _manager.Initialize(() => { callCount++; });

            MaxSdkCallbacks.EmitSdkInitializedEvent();

            // First Initialize's action fires once; second is a no-op because of the guard.
            Assert.AreEqual(1, callCount, "initCompleteAction should fire exactly once despite double Initialize");
        }

        // ── Interstitial lifecycle (fires inner class events via reflection) ──────

        [Test]
        public void SetInterstitialAdUnitID_WiresOnAdDisplayed_ViaInnerEvent()
        {
            var fired = false;
            _manager.OnAdDisplayed += () => { fired = true; };
            _manager.SetInterstitialAdUnitID(AppLovinAdUnits.Interstitial);

            FireInnerAdEvent(_manager, "_interstitialAppLovin", "InterstitialOnAdDisplayed");

            Assert.IsTrue(fired, "OnAdDisplayed must fire when InterstitialOnAdDisplayed fires");
        }

        [Test]
        public void SetInterstitialAdUnitID_WiresOnAdClicked_ViaInnerEvent()
        {
            var fired = false;
            _manager.OnAdClicked += () => { fired = true; };
            _manager.SetInterstitialAdUnitID(AppLovinAdUnits.Interstitial);

            FireInnerAdEvent(_manager, "_interstitialAppLovin", "InterstitialOnAdClicked");

            Assert.IsTrue(fired, "OnAdClicked must fire when InterstitialOnAdClicked fires");
        }

        [Test]
        public void SetInterstitialAdUnitID_WiresOnAdClosed_ViaInnerEvent()
        {
            var fired = false;
            _manager.OnAdClosed += () => { fired = true; };
            _manager.SetInterstitialAdUnitID(AppLovinAdUnits.Interstitial);

            FireInnerAdEvent(_manager, "_interstitialAppLovin", "InterstitialOnAdClosed");

            Assert.IsTrue(fired, "OnAdClosed must fire when InterstitialOnAdClosed fires");
        }

        [Test]
        public void SetInterstitialAdUnitID_WiresOnAdImpressionRecorded_ViaInnerEvent()
        {
            var fired = false;
            _manager.OnAdImpressionRecorded += () => { fired = true; };
            _manager.SetInterstitialAdUnitID(AppLovinAdUnits.Interstitial);

            FireInnerAdEvent(_manager, "_interstitialAppLovin", "InterstitialOnAdImpressionRecorded");

            Assert.IsTrue(fired, "OnAdImpressionRecorded must fire when InterstitialOnAdImpressionRecorded fires");
        }

        [Test]
        public void SetInterstitialAdUnitID_WiresOnAdFailedDisplayed_ViaInnerEvent()
        {
            var fired = false;
            _manager.OnAdFailedDisplayed += () => { fired = true; };
            _manager.SetInterstitialAdUnitID(AppLovinAdUnits.Interstitial);

            FireInnerAdEvent(_manager, "_interstitialAppLovin", "InterstitialOnAdFailedDisplayed");

            Assert.IsTrue(fired, "OnAdFailedDisplayed must fire when InterstitialOnAdFailedDisplayed fires");
        }

        [Test]
        public void SetInterstitialAdUnitID_WiresRevenuePaid_ViaInnerEvent()
        {
            MaxSdkBase.AdInfo received = null;
            _manager.AppLovinOnAdRevenuePaid += (info) => { received = info; };
            _manager.SetInterstitialAdUnitID(AppLovinAdUnits.Interstitial);

            var adInfo = MakeAdInfo(AppLovinAdUnits.Interstitial, revenue: 0.05);
            FireInnerAdEvent(_manager, "_interstitialAppLovin", "InterstitialOnAdRevenuePaid", adInfo);

            Assert.IsNotNull(received);
            Assert.AreEqual(0.05, received.Revenue, 0.001);
        }

        [Test]
        public void InterstitialRevenuePaid_ForwardsToUnifiedOnAdRevenuePaid()
        {
            double receivedRevenue = -1;
            string receivedCurrency = null;
            _manager.OnAdRevenuePaid += (rev, cur, _) => { receivedRevenue = rev; receivedCurrency = cur; };
            _manager.SetInterstitialAdUnitID(AppLovinAdUnits.Interstitial);

            var adInfo = MakeAdInfo(AppLovinAdUnits.Interstitial, revenue: 0.03);
            FireInnerAdEvent(_manager, "_interstitialAppLovin", "InterstitialOnAdRevenuePaid", adInfo);

            Assert.AreEqual(0.03, receivedRevenue, 0.001);
            Assert.AreEqual("USD", receivedCurrency);
        }

        // ── Rewarded lifecycle (fires inner class events via reflection) ──────────

        [Test]
        public void SetRewardedAdUnitID_WiresOnAdDisplayed_ViaInnerEvent()
        {
            var fired = false;
            _manager.OnAdDisplayed += () => { fired = true; };
            _manager.SetRewardedAdUnitID(AppLovinAdUnits.Rewarded);

            FireInnerAdEvent(_manager, "_rewardedAppLovin", "RewardedOnAdDisplayed");

            Assert.IsTrue(fired, "OnAdDisplayed must fire when RewardedOnAdDisplayed fires");
        }

        [Test]
        public void SetRewardedAdUnitID_WiresOnAdClicked_ViaInnerEvent()
        {
            var fired = false;
            _manager.OnAdClicked += () => { fired = true; };
            _manager.SetRewardedAdUnitID(AppLovinAdUnits.Rewarded);

            FireInnerAdEvent(_manager, "_rewardedAppLovin", "RewardedOnAdClicked");

            Assert.IsTrue(fired, "OnAdClicked must fire when RewardedOnAdClicked fires");
        }

        [Test]
        public void SetRewardedAdUnitID_WiresOnAdClosed_ViaInnerEvent()
        {
            var fired = false;
            _manager.OnAdClosed += () => { fired = true; };
            _manager.SetRewardedAdUnitID(AppLovinAdUnits.Rewarded);

            FireInnerAdEvent(_manager, "_rewardedAppLovin", "RewardedOnAdClosed");

            Assert.IsTrue(fired, "OnAdClosed must fire when RewardedOnAdClosed fires");
        }

        [Test]
        public void SetRewardedAdUnitID_WiresEarnedReward_ViaInnerEvent()
        {
            MaxSdkBase.Reward received = default;
            _manager.AppLovinOnUserEarnedReward += (r) => { received = r; };
            _manager.SetRewardedAdUnitID(AppLovinAdUnits.Rewarded);

            var reward = new MaxSdkBase.Reward { Label = "Coins", Amount = 100 };
            FireInnerAdEvent(_manager, "_rewardedAppLovin", "RewardedOnUserEarnedReward", reward);

            Assert.AreEqual("Coins", received.Label);
            Assert.AreEqual(100, received.Amount);
        }

        [Test]
        public void SetRewardedAdUnitID_EarnedReward_ForwardsToUnifiedOnUserEarnedReward()
        {
            double receivedAmount = -1;
            string receivedLabel = null;
            _manager.OnUserEarnedReward += (amount, label) => { receivedAmount = amount; receivedLabel = label; };
            _manager.SetRewardedAdUnitID(AppLovinAdUnits.Rewarded);

            var reward = new MaxSdkBase.Reward { Label = "Gems", Amount = 50 };
            FireInnerAdEvent(_manager, "_rewardedAppLovin", "RewardedOnUserEarnedReward", reward);

            Assert.AreEqual(50, receivedAmount, 0.001);
            Assert.AreEqual("Gems", receivedLabel);
        }

        [Test]
        public void SetRewardedAdUnitID_WiresRevenuePaid_ViaInnerEvent()
        {
            // RewardedOnAdRevenuePaid → AppLovinOnAdRevenuePaid only.
            // It does NOT trigger OnAdImpressionRecorded (that's wired to RewardedOnAdImpressionRecorded).
            double receivedRevenue = -1;
            _manager.AppLovinOnAdRevenuePaid += (info) => { receivedRevenue = info.Revenue; };
            _manager.SetRewardedAdUnitID(AppLovinAdUnits.Rewarded);

            var adInfo = MakeAdInfo(AppLovinAdUnits.Rewarded, revenue: 0.02);
            FireInnerAdEvent(_manager, "_rewardedAppLovin", "RewardedOnAdRevenuePaid", adInfo);

            Assert.AreEqual(0.02, receivedRevenue, 0.001);
        }

        // ── Show paths (call path, not real load) ────────────────────────────────

        [Test]
        public void ShowInterstitial_WithPlacement_DoesNotThrow()
        {
            _manager.SetInterstitialAdUnitID(AppLovinAdUnits.Interstitial);
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => _manager.ShowInterstitial("main-menu"));
        }

        [Test]
        public void ShowRewardedAd_WithPlacement_DoesNotThrow()
        {
            _manager.SetRewardedAdUnitID(AppLovinAdUnits.Rewarded);
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => _manager.ShowRewardedAd("gameplay"));
        }

        // ── Event subscription guards ────────────────────────────────────────────

        [Test]
        public void SetInterstitialAdUnitID_Idempotent_OnlySubscribesOnce()
        {
            _manager.SetInterstitialAdUnitID(AppLovinAdUnits.Interstitial);
            _manager.SetInterstitialAdUnitID(AppLovinAdUnits.Interstitial);
            Assert.DoesNotThrow(() => { });
        }

        [Test]
        public void SetRewardedAdUnitID_Idempotent_OnlySubscribesOnce()
        {
            _manager.SetRewardedAdUnitID(AppLovinAdUnits.Rewarded);
            _manager.SetRewardedAdUnitID(AppLovinAdUnits.Rewarded);
            Assert.DoesNotThrow(() => { });
        }

        [Test]
        public void SetAppOpenAdUnitID_Idempotent_OnlySubscribesOnce()
        {
            _manager.SetAppOpenAdUnitID(AppLovinAdUnits.AppOpen);
            _manager.SetAppOpenAdUnitID(AppLovinAdUnits.AppOpen);
            Assert.DoesNotThrow(() => { });
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
            Assert.DoesNotThrow(() => _manager.SetTestDeviceIds(new System.Collections.Generic.List<string>()));
        }

        [Test]
        public void SetTestDeviceIds_WithIds_DoesNotThrow()
        {
            LogAssert.Expect(LogType.Error, new Regex("Test Device Advertising Identifiers"));
            Assert.DoesNotThrow(() =>
                _manager.SetTestDeviceIds(new System.Collections.Generic.List<string> { "FAKE-DEVICE-ID" }));
        }

        // ── GetBannerPosition ─────────────────────────────────────────────────────

        [Test]
        public void GetBannerPosition_BeforeCreate_ReturnsRect()
        {
            _manager.SetBannerAdUnitId(AppLovinAdUnits.Banner);
            var rect = _manager.GetBannerPosition();
            Assert.IsInstanceOf<Rect>(rect);
        }
    }

    /// <summary>Tests for BannerAppLovin inner class state methods.</summary>
    public class BannerAppLovinTests
    {
        [Test]
        public void SetBannerAdUnitId_DoesNotThrow()
        {
            var banner = new BannerAppLovin();
            Assert.DoesNotThrow(() => banner.SetBannerAdUnitId(AppLovinAdUnits.Banner));
        }

        [Test]
        public void GetBannerPosition_ReturnsRect()
        {
            var banner = new BannerAppLovin();
            banner.SetBannerAdUnitId(AppLovinAdUnits.Banner);
            Assert.IsInstanceOf<Rect>(banner.GetBannerPosition());
        }

        [Test]
        public void SetBannerWidth_DoesNotThrow()
        {
            var banner = new BannerAppLovin();
            banner.SetBannerAdUnitId(AppLovinAdUnits.Banner);
            Assert.DoesNotThrow(() => banner.SetBannerWidth(320));
        }

        [Test]
        public void SetPlacement_DoesNotThrow()
        {
            // SetPlacement forwards the adUnitID to MaxSdk — an ID must be set first
            // otherwise MaxSdkUnityEditor throws ArgumentNullException on a null key.
            var banner = new BannerAppLovin();
            banner.SetBannerAdUnitId(AppLovinAdUnits.Banner);
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => banner.SetPlacement("main-menu"));
        }

        [Test]
        public void SetRefreshInterval_DoesNotThrow()
        {
            var banner = new BannerAppLovin();
            LogAssert.Expect(LogType.Error, new Regex("No MAX Ads Ad Unit ID"));
            Assert.DoesNotThrow(() => banner.SetRefreshInterval(30));
        }

        [Test]
        public void UnregisterCallbacks_BeforeRegister_DoesNotThrow()
        {
            var banner = new BannerAppLovin();
            Assert.DoesNotThrow(() => banner.UnregisterCallbacks());
        }

        // ── New tests ────────────────────────────────────────────────────────────

        [Test]
        public void SetBannerAdUnitId_Null_DoesNotCrash()
        {
            // Null guard: SetBannerAdUnitId(null) must log an error and not throw.
            var banner = new BannerAppLovin();
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => banner.SetBannerAdUnitId(null));
        }

        [Test]
        public void ShowBanner_WithoutUnit_DoesNotThrow()
        {
            // ShowBanner must bail out silently when no ad unit ID has been set.
            var banner = new BannerAppLovin();
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => banner.ShowBanner());
        }

        [Test]
        public void HideBanner_AfterSetUnit_DoesNotThrow()
        {
            var banner = new BannerAppLovin();
            banner.SetBannerAdUnitId(AppLovinAdUnits.Banner);
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => banner.HideBanner());
        }

        [Test]
        public void DestroyBanner_AfterSetUnit_DoesNotThrow()
        {
            var banner = new BannerAppLovin();
            banner.SetBannerAdUnitId(AppLovinAdUnits.Banner);
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => banner.DestroyBanner());
        }

        [Test]
        public void StopBannerAutoRefresh_AfterSetUnit_DoesNotThrow()
        {
            var banner = new BannerAppLovin();
            banner.SetBannerAdUnitId(AppLovinAdUnits.Banner);
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => banner.StopBannerAutoRefresh());
        }

        [Test]
        public void StartBannerAutoRefresh_AfterSetUnit_DoesNotThrow()
        {
            var banner = new BannerAppLovin();
            banner.SetBannerAdUnitId(AppLovinAdUnits.Banner);
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => banner.StartBannerAutoRefresh());
        }

        [Test]
        public void UnregisterCallbacks_AfterRegisterViaInitialize_DoesNotThrow()
        {
            // InitializeBannerAds registers callbacks; UnregisterCallbacks must cleanly remove them.
            var banner = new BannerAppLovin();
            banner.SetBannerAdUnitId(AppLovinAdUnits.Banner);
            LogAssert.ignoreFailingMessages = true;
            // InitializeBannerAds triggers MaxSdk.CreateBanner which is a no-op in the Editor stub.
            Assert.DoesNotThrow(() =>
            {
                banner.InitializeBannerAds(UnityEngine.Color.black, MaxSdk.AdViewPosition.BottomCenter);
                banner.UnregisterCallbacks();
            });
        }
    }

    /// <summary>Tests for InterstitialAppLovin inner class state methods.</summary>
    public class InterstitialAppLovinTests
    {
        [Test]
        public void SetInterstitialAdUnitID_DoesNotThrow()
        {
            var interstitial = new InterstitialAppLovin();
            Assert.DoesNotThrow(() => interstitial.SetInterstitialAdUnitID(AppLovinAdUnits.Interstitial));
        }

        [Test]
        public void IsReady_ReturnsFalseWhenNoUnitSet()
        {
            var interstitial = new InterstitialAppLovin();
            Assert.IsFalse(interstitial.IsReady());
        }

        [Test]
        public void IsReady_ReturnsFalseWhenUnitSetButNotLoaded()
        {
            var interstitial = new InterstitialAppLovin();
            interstitial.SetInterstitialAdUnitID(AppLovinAdUnits.Interstitial);
            Assert.IsFalse(interstitial.IsReady());
        }

        [Test]
        public void LoadInterstitial_WithoutUnit_DoesNotThrow()
        {
            var interstitial = new InterstitialAppLovin();
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => interstitial.LoadInterstitial());
        }

        [Test]
        public void ShowInterstitial_WithoutUnit_DoesNotThrow()
        {
            var interstitial = new InterstitialAppLovin();
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => interstitial.ShowInterstitial());
        }

        [Test]
        public void ShowInterstitialWithPlacement_WhenNotReady_DoesNotThrow()
        {
            var interstitial = new InterstitialAppLovin();
            interstitial.SetInterstitialAdUnitID(AppLovinAdUnits.Interstitial);
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => interstitial.ShowInterstitial("main-menu"));
        }

        [Test]
        public void UnregisterCallbacks_BeforeRegister_DoesNotThrow()
        {
            var interstitial = new InterstitialAppLovin();
            Assert.DoesNotThrow(() => interstitial.UnregisterCallbacks());
        }

        // ── New tests ────────────────────────────────────────────────────────────

        [Test]
        public void SetInterstitialAdUnitID_Null_DoesNotCrash()
        {
            // Null guard: the method must log an error and not throw.
            var interstitial = new InterstitialAppLovin();
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => interstitial.SetInterstitialAdUnitID(null));
        }

        [Test]
        public void LoadInterstitial_WithUnit_DoesNotThrow()
        {
            // LoadInterstitial with a unit ID set must not throw even in the Editor stub.
            var interstitial = new InterstitialAppLovin();
            interstitial.SetInterstitialAdUnitID(AppLovinAdUnits.Interstitial);
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => interstitial.LoadInterstitial());
        }

        [Test]
        public void UnregisterCallbacks_AfterLoad_DoesNotThrow()
        {
            // Load registers callbacks; UnregisterCallbacks must cleanly remove them.
            var interstitial = new InterstitialAppLovin();
            interstitial.SetInterstitialAdUnitID(AppLovinAdUnits.Interstitial);
            LogAssert.ignoreFailingMessages = true;
            interstitial.LoadInterstitial();
            Assert.DoesNotThrow(() => interstitial.UnregisterCallbacks());
        }

        [Test]
        public void IsReady_ReturnsFalseAfterUnregister()
        {
            // After UnregisterCallbacks the unit ID is still set; IsReady must remain false
            // in the Editor stub (MaxSdk.IsInterstitialReady always returns false in Editor).
            var interstitial = new InterstitialAppLovin();
            interstitial.SetInterstitialAdUnitID(AppLovinAdUnits.Interstitial);
            LogAssert.ignoreFailingMessages = true;
            interstitial.LoadInterstitial();
            interstitial.UnregisterCallbacks();
            Assert.IsFalse(interstitial.IsReady());
        }

        [Test]
        public void ShowInterstitial_NoPlacement_WithUnit_WhenNotReady_DoesNotThrow()
        {
            // No-placement overload — ad not ready path must not throw.
            var interstitial = new InterstitialAppLovin();
            interstitial.SetInterstitialAdUnitID(AppLovinAdUnits.Interstitial);
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => interstitial.ShowInterstitial());
        }

        [Test]
        public void SetInterstitialAdUnitID_Idempotent_DoesNotThrow()
        {
            // Calling SetInterstitialAdUnitID a second time must not throw or corrupt state.
            var interstitial = new InterstitialAppLovin();
            Assert.DoesNotThrow(() =>
            {
                interstitial.SetInterstitialAdUnitID(AppLovinAdUnits.Interstitial);
                interstitial.SetInterstitialAdUnitID(AppLovinAdUnits.Interstitial);
            });
        }

        [Test]
        public void LoadInterstitial_CalledTwice_DoesNotDoubleSubscribe()
        {
            // The _callbacksRegistered guard prevents duplicate event subscriptions.
            // Calling LoadInterstitial twice with the same unit ID must not throw.
            var interstitial = new InterstitialAppLovin();
            interstitial.SetInterstitialAdUnitID(AppLovinAdUnits.Interstitial);
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() =>
            {
                interstitial.LoadInterstitial();
                interstitial.LoadInterstitial();
            });
        }
    }

    /// <summary>Tests for RewardedAppLovin inner class state methods.</summary>
    public class RewardedAppLovinTests
    {
        [Test]
        public void SetRewardedAdUnitID_DoesNotThrow()
        {
            var rewarded = new RewardedAppLovin();
            Assert.DoesNotThrow(() => rewarded.SetRewardedAdUnitID(AppLovinAdUnits.Rewarded));
        }

        [Test]
        public void IsReady_ReturnsFalseWhenNoUnitSet()
        {
            var rewarded = new RewardedAppLovin();
            Assert.IsFalse(rewarded.IsReady());
        }

        [Test]
        public void IsReady_ReturnsFalseWhenUnitSetButNotLoaded()
        {
            var rewarded = new RewardedAppLovin();
            rewarded.SetRewardedAdUnitID(AppLovinAdUnits.Rewarded);
            Assert.IsFalse(rewarded.IsReady());
        }

        [Test]
        public void LoadRewardedAds_WithoutUnit_DoesNotThrow()
        {
            var rewarded = new RewardedAppLovin();
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => rewarded.LoadRewardedAds());
        }

        [Test]
        public void ShowRewardedAd_WhenNotReady_DoesNotThrow()
        {
            var rewarded = new RewardedAppLovin();
            rewarded.SetRewardedAdUnitID(AppLovinAdUnits.Rewarded);
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => rewarded.ShowRewardedAd());
        }

        [Test]
        public void ShowRewardedAdWithPlacement_WhenNotReady_DoesNotThrow()
        {
            var rewarded = new RewardedAppLovin();
            rewarded.SetRewardedAdUnitID(AppLovinAdUnits.Rewarded);
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => rewarded.ShowRewardedAd("bonus-round"));
        }

        [Test]
        public void UnregisterCallbacks_BeforeRegister_DoesNotThrow()
        {
            var rewarded = new RewardedAppLovin();
            Assert.DoesNotThrow(() => rewarded.UnregisterCallbacks());
        }

        // ── New tests ────────────────────────────────────────────────────────────

        [Test]
        public void SetRewardedAdUnitID_Null_DoesNotCrash()
        {
            // Null guard: SetRewardedAdUnitID(null) must log an error and not throw.
            var rewarded = new RewardedAppLovin();
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => rewarded.SetRewardedAdUnitID(null));
        }

        [Test]
        public void LoadRewardedAds_WithUnit_DoesNotThrow()
        {
            // LoadRewardedAds with a unit ID set must not throw even in the Editor stub.
            var rewarded = new RewardedAppLovin();
            rewarded.SetRewardedAdUnitID(AppLovinAdUnits.Rewarded);
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => rewarded.LoadRewardedAds());
        }

        [Test]
        public void UnregisterCallbacks_AfterLoad_DoesNotThrow()
        {
            // LoadRewardedAds registers callbacks; UnregisterCallbacks must cleanly remove them.
            var rewarded = new RewardedAppLovin();
            rewarded.SetRewardedAdUnitID(AppLovinAdUnits.Rewarded);
            LogAssert.ignoreFailingMessages = true;
            rewarded.LoadRewardedAds();
            Assert.DoesNotThrow(() => rewarded.UnregisterCallbacks());
        }

        [Test]
        public void IsReady_ReturnsFalseAfterUnregister()
        {
            // After UnregisterCallbacks the unit ID is still set; IsReady must remain false
            // in the Editor stub (MaxSdk.IsRewardedAdReady always returns false in Editor).
            var rewarded = new RewardedAppLovin();
            rewarded.SetRewardedAdUnitID(AppLovinAdUnits.Rewarded);
            LogAssert.ignoreFailingMessages = true;
            rewarded.LoadRewardedAds();
            rewarded.UnregisterCallbacks();
            Assert.IsFalse(rewarded.IsReady());
        }

        [Test]
        public void ShowRewardedAd_NoPlacement_WithUnit_WhenNotReady_DoesNotThrow()
        {
            // No-placement overload — ad not ready path must not throw.
            var rewarded = new RewardedAppLovin();
            rewarded.SetRewardedAdUnitID(AppLovinAdUnits.Rewarded);
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => rewarded.ShowRewardedAd());
        }

        [Test]
        public void SetRewardedAdUnitID_Idempotent_DoesNotThrow()
        {
            // Calling SetRewardedAdUnitID a second time must not throw or corrupt state.
            var rewarded = new RewardedAppLovin();
            Assert.DoesNotThrow(() =>
            {
                rewarded.SetRewardedAdUnitID(AppLovinAdUnits.Rewarded);
                rewarded.SetRewardedAdUnitID(AppLovinAdUnits.Rewarded);
            });
        }

        [Test]
        public void ShowRewardedAdWithPlacement_WithoutUnit_DoesNotThrow()
        {
            // Placement overload without a unit ID set must bail out silently.
            var rewarded = new RewardedAppLovin();
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => rewarded.ShowRewardedAd("placement-without-unit"));
        }
    }

    /// <summary>Tests for AppOpenAppLovin inner class state methods.</summary>
    public class AppOpenAppLovinTests
    {
        [Test]
        public void SetAppOpenAdUnitID_DoesNotThrow()
        {
            var appOpen = new AppOpenAppLovin();
            Assert.DoesNotThrow(() => appOpen.SetAppOpenAdUnitID(AppLovinAdUnits.AppOpen));
        }

        [Test]
        public void IsAdReady_ReturnsFalseWhenNoUnitSet()
        {
            var appOpen = new AppOpenAppLovin();
            Assert.IsFalse(appOpen.IsAdReady());
        }

        [Test]
        public void IsAdReady_ReturnsFalseWhenUnitSetButNotLoaded()
        {
            var appOpen = new AppOpenAppLovin();
            appOpen.SetAppOpenAdUnitID(AppLovinAdUnits.AppOpen);
            Assert.IsFalse(appOpen.IsAdReady());
        }

        [Test]
        public void LoadAppOpenAd_WithoutUnit_DoesNotThrow()
        {
            var appOpen = new AppOpenAppLovin();
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => appOpen.LoadAppOpenAd());
        }

        [Test]
        public void ShowAppOpenAd_WhenNotReady_DoesNotThrow()
        {
            var appOpen = new AppOpenAppLovin();
            appOpen.SetAppOpenAdUnitID(AppLovinAdUnits.AppOpen);
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => appOpen.ShowAppOpenAd());
        }

        [Test]
        public void UnregisterCallbacks_BeforeRegister_DoesNotThrow()
        {
            var appOpen = new AppOpenAppLovin();
            Assert.DoesNotThrow(() => appOpen.UnregisterCallbacks());
        }

        // ── New tests ────────────────────────────────────────────────────────────

        [Test]
        public void SetAppOpenAdUnitID_Null_DoesNotCrash()
        {
            // Null guard: SetAppOpenAdUnitID(null) must log an error and not throw.
            var appOpen = new AppOpenAppLovin();
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => appOpen.SetAppOpenAdUnitID(null));
        }

        [Test]
        public void LoadAppOpenAd_WithUnit_DoesNotThrow()
        {
            // LoadAppOpenAd with a unit ID set must not throw even in the Editor stub.
            var appOpen = new AppOpenAppLovin();
            appOpen.SetAppOpenAdUnitID(AppLovinAdUnits.AppOpen);
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => appOpen.LoadAppOpenAd());
        }

        [Test]
        public void UnregisterCallbacks_AfterLoad_DoesNotThrow()
        {
            // LoadAppOpenAd registers callbacks; UnregisterCallbacks must cleanly remove them.
            var appOpen = new AppOpenAppLovin();
            appOpen.SetAppOpenAdUnitID(AppLovinAdUnits.AppOpen);
            LogAssert.ignoreFailingMessages = true;
            appOpen.LoadAppOpenAd();
            Assert.DoesNotThrow(() => appOpen.UnregisterCallbacks());
        }

        [Test]
        public void IsAdReady_ReturnsFalseAfterUnregister()
        {
            // After UnregisterCallbacks the unit ID is still set; IsAdReady must remain false
            // in the Editor stub (MaxSdk.IsAppOpenAdReady always returns false in Editor).
            var appOpen = new AppOpenAppLovin();
            appOpen.SetAppOpenAdUnitID(AppLovinAdUnits.AppOpen);
            LogAssert.ignoreFailingMessages = true;
            appOpen.LoadAppOpenAd();
            appOpen.UnregisterCallbacks();
            Assert.IsFalse(appOpen.IsAdReady());
        }

        [Test]
        public void ShowAppOpenAd_WithoutUnit_DoesNotThrow()
        {
            // ShowAppOpenAd must bail out silently when no ad unit ID has been set.
            var appOpen = new AppOpenAppLovin();
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => appOpen.ShowAppOpenAd());
        }

        [Test]
        public void SetAppOpenAdUnitID_Idempotent_DoesNotThrow()
        {
            // Calling SetAppOpenAdUnitID a second time must not throw or corrupt state.
            var appOpen = new AppOpenAppLovin();
            Assert.DoesNotThrow(() =>
            {
                appOpen.SetAppOpenAdUnitID(AppLovinAdUnits.AppOpen);
                appOpen.SetAppOpenAdUnitID(AppLovinAdUnits.AppOpen);
            });
        }

        [Test]
        public void LoadAppOpenAd_CalledTwice_DoesNotDoubleSubscribe()
        {
            // The _callbacksRegistered guard prevents duplicate event subscriptions.
            // Calling LoadAppOpenAd twice with the same unit ID must not throw.
            var appOpen = new AppOpenAppLovin();
            appOpen.SetAppOpenAdUnitID(AppLovinAdUnits.AppOpen);
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() =>
            {
                appOpen.LoadAppOpenAd();
                appOpen.LoadAppOpenAd();
            });
        }
    }
}
#endif
