using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using com.noctuagames.sdk;
using com.noctuagames.sdk.AdPlaceholder;
using NUnit.Framework;
using UnityEngine.TestTools;
using IAAConfig = com.noctuagames.sdk.IAA;

namespace Tests.Runtime.IAA
{
    /// <summary>
    /// Tests for the <c>ad_rewarded_complete</c> analytics event emitted by
    /// <see cref="MediationManager"/> when a user earns a rewarded-ad reward.
    ///
    /// <para>
    /// The subscription logic lives in <c>SubscribeRewardCompletionEvent</c>, which
    /// is <c>private</c> — it is exercised via reflection, following the same pattern
    /// used for <c>PostToMainThread</c> in <see cref="MediationManagerUtilityApiTest"/>.
    /// </para>
    ///
    /// <para>
    /// <b>Main-thread dispatch:</b> In EditMode the captured
    /// <c>_mainThreadContext</c> is non-null (Unity provides a sync context). We
    /// force it to <c>null</c> via reflection so <c>PostToMainThread</c> executes
    /// the action inline and synchronously — without this the tracker would be
    /// unfired at assertion time.
    /// </para>
    ///
    /// <para>
    /// <b>Thread-safety coverage:</b>
    /// Both AppLovin MAX (background thread) and AdMob may fire
    /// <c>OnUserEarnedReward</c> off the Unity main thread.
    /// <c>SubscribeRewardCompletionEvent</c> always wraps the callback in
    /// <c>PostToMainThread</c>, so the tracking call is deferred to the main thread.
    /// </para>
    /// </summary>
    [TestFixture]
    public class AdRewardedCompleteEventTest
    {
        // ── Reflection handles ─────────────────────────────────────────────────────

        private static readonly MethodInfo SubscribeRewardCompletionMethod =
            typeof(MediationManager).GetMethod(
                "SubscribeRewardCompletionEvent",
                BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo MainThreadContextField =
            typeof(MediationManager).GetField(
                "_mainThreadContext",
                BindingFlags.NonPublic | BindingFlags.Instance);

        // ── Noop UI implementation ──────────────────────────────────────────────────

        private class NoopAdPlaceholderUI : IAdPlaceholderUI
        {
            public void ShowAdPlaceholder(AdPlaceholderType adType, CrossPromotionEntry entry) { }
            public void PreloadAdPlaceholder(CrossPromotionConfig config) { }
            public void SetPlaceholderClosedCallback(System.Action onClosed) { }
            public void SetPlaceholderClickedCallback(System.Action onClicked) { }
            public void SetPlaceholderShownCallback(System.Action onShown) { }
            public void SetPlaceholderFailedCallback(System.Action onFailed) { }
            public void CloseAdPlaceholder() { }
            public bool IsAssetCached(string assetUrl) => false;
        }

        // ── Test helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Sanity check: verify reflection handles are not null so tests fail
        /// with a meaningful message if the private member is renamed.
        /// </summary>
        [OneTimeSetUp]
        public void VerifyReflectionHandles()
        {
            Assert.IsNotNull(SubscribeRewardCompletionMethod,
                "Reflection could not find MediationManager.SubscribeRewardCompletionEvent — " +
                "was the private method renamed?");
            Assert.IsNotNull(MainThreadContextField,
                "Reflection could not find MediationManager._mainThreadContext — " +
                "was the private field renamed?");
        }

        /// <summary>
        /// Creates a <see cref="MediationManager"/> with the supplied tracker wired,
        /// then forces <c>_mainThreadContext</c> to <c>null</c> so
        /// <c>PostToMainThread</c> executes inline and synchronously in EditMode.
        /// </summary>
        private (MediationManager mgr, MockAdRevenueTracker tracker) CreateManager()
        {
            LogAssert.ignoreFailingMessages = true;
            var tracker = new MockAdRevenueTracker();
            var mgr     = new MediationManager(new NoopAdPlaceholderUI(), null, adRevenueTracker: tracker);
            // Force inline (synchronous) PostToMainThread execution in EditMode
            MainThreadContextField.SetValue(mgr, null);
            return (mgr, tracker);
        }

        /// <summary>
        /// Calls the private <c>SubscribeRewardCompletionEvent</c> on
        /// <paramref name="mgr"/> for <paramref name="network"/> via reflection.
        /// </summary>
        private static void Subscribe(MediationManager mgr, IAdNetwork network)
            => SubscribeRewardCompletionMethod.Invoke(mgr, new object[] { network });

        // ── Tests — single network ─────────────────────────────────────────────────

        [Test]
        public void RewardEarned_PrimaryNetwork_FiresAdRewardedCompleteEvent()
        {
            var (mgr, tracker) = CreateManager();
            var network = new MockAdNetwork { NetworkName = "admob" };
            Subscribe(mgr, network);

            network.TriggerUserEarnedReward(10.0, "coins");

            Assert.IsTrue(tracker.WasFired("ad_rewarded_complete"),
                "ad_rewarded_complete must be tracked when the primary network fires OnUserEarnedReward");
        }

        [Test]
        public void RewardEarned_Payload_ContainsCorrectNetworkName()
        {
            var (mgr, tracker) = CreateManager();
            var network = new MockAdNetwork { NetworkName = "applovin" };
            Subscribe(mgr, network);

            network.TriggerUserEarnedReward(5.0, "gems");

            var ev = tracker.Events.Find(e => e.EventName == "ad_rewarded_complete");
            Assert.IsNotNull(ev, "ad_rewarded_complete event must be recorded");
            Assert.AreEqual("applovin", ev.Params["network"].ToString(),
                "network field must match the network's NetworkName property");
        }

        [Test]
        public void RewardEarned_Payload_ContainsCorrectRewardAmount()
        {
            var (mgr, tracker) = CreateManager();
            var network = new MockAdNetwork { NetworkName = "admob" };
            Subscribe(mgr, network);

            network.TriggerUserEarnedReward(25.5, "gold");

            var ev = tracker.Events.Find(e => e.EventName == "ad_rewarded_complete");
            Assert.IsNotNull(ev, "ad_rewarded_complete event must be recorded");
            Assert.AreEqual(25.5, Convert.ToDouble(ev.Params["reward_amount"]), 0.001,
                "reward_amount must match the value passed by the ad network");
        }

        [Test]
        public void RewardEarned_Payload_ContainsCorrectRewardType()
        {
            var (mgr, tracker) = CreateManager();
            var network = new MockAdNetwork { NetworkName = "admob" };
            Subscribe(mgr, network);

            network.TriggerUserEarnedReward(1.0, "life");

            var ev = tracker.Events.Find(e => e.EventName == "ad_rewarded_complete");
            Assert.IsNotNull(ev, "ad_rewarded_complete event must be recorded");
            Assert.AreEqual("life", ev.Params["reward_type"].ToString(),
                "reward_type must match the reward type string passed by the ad network");
        }

        [Test]
        public void RewardEarned_NullRewardType_RewardTypeIsEmptyString()
        {
            var (mgr, tracker) = CreateManager();
            var network = new MockAdNetwork { NetworkName = "admob" };
            Subscribe(mgr, network);

            network.TriggerUserEarnedReward(1.0, null);

            var ev = tracker.Events.Find(e => e.EventName == "ad_rewarded_complete");
            Assert.IsNotNull(ev, "ad_rewarded_complete event must be recorded even when reward type is null");
            Assert.AreEqual("", ev.Params["reward_type"].ToString(),
                "reward_type must be empty string when the ad network passes null (null-safe coalescing)");
        }

        [Test]
        public void RewardEarned_FiredMultipleTimes_TrackedOnEachOccurrence()
        {
            var (mgr, tracker) = CreateManager();
            var network = new MockAdNetwork { NetworkName = "admob" };
            Subscribe(mgr, network);

            network.TriggerUserEarnedReward(1.0, "a");
            network.TriggerUserEarnedReward(2.0, "b");
            network.TriggerUserEarnedReward(3.0, "c");

            Assert.AreEqual(3, tracker.CountFired("ad_rewarded_complete"),
                "ad_rewarded_complete must be tracked once for each reward earned, with no de-duplication");
        }

        // ── Tests — primary + secondary (hybrid) ───────────────────────────────────

        [Test]
        public void RewardEarned_SecondaryNetwork_FiresAdRewardedCompleteEvent()
        {
            var (mgr, tracker) = CreateManager();
            var primary   = new MockAdNetwork { NetworkName = "admob" };
            var secondary = new MockAdNetwork { NetworkName = "applovin" };
            Subscribe(mgr, primary);
            Subscribe(mgr, secondary);

            secondary.TriggerUserEarnedReward(3.0, "stars");

            Assert.IsTrue(tracker.WasFired("ad_rewarded_complete"),
                "ad_rewarded_complete must fire when the secondary (hybrid) network triggers the reward");
        }

        [Test]
        public void RewardEarned_SecondaryNetwork_Payload_ContainsSecondaryNetworkName()
        {
            var (mgr, tracker) = CreateManager();
            var primary   = new MockAdNetwork { NetworkName = "admob" };
            var secondary = new MockAdNetwork { NetworkName = "applovin" };
            Subscribe(mgr, primary);
            Subscribe(mgr, secondary);

            secondary.TriggerUserEarnedReward(7.0, "tokens");

            var ev = tracker.Events.Find(e =>
                e.EventName == "ad_rewarded_complete" &&
                e.Params["network"].ToString() == "applovin");

            Assert.IsNotNull(ev,
                "ad_rewarded_complete from the secondary network must carry the secondary network's name, not the primary's");
        }

        [Test]
        public void RewardEarned_BothNetworks_EachRewardTrackedSeparately()
        {
            var (mgr, tracker) = CreateManager();
            var primary   = new MockAdNetwork { NetworkName = "admob" };
            var secondary = new MockAdNetwork { NetworkName = "applovin" };
            Subscribe(mgr, primary);
            Subscribe(mgr, secondary);

            primary.TriggerUserEarnedReward(1.0, "a");
            secondary.TriggerUserEarnedReward(2.0, "b");

            Assert.AreEqual(2, tracker.CountFired("ad_rewarded_complete"),
                "ad_rewarded_complete must fire once per network reward — both primary and secondary");
        }

        // ── Tests — null-safety ────────────────────────────────────────────────────

        [Test]
        public void RewardEarned_NullTracker_DoesNotThrow()
        {
            // Construct with null tracker — the null-conditional ?. operator in the
            // callback must swallow the call without throwing.
            LogAssert.ignoreFailingMessages = true;
            var mgr = new MediationManager(new NoopAdPlaceholderUI(), null, adRevenueTracker: null);
            MainThreadContextField.SetValue(mgr, null);

            var network = new MockAdNetwork { NetworkName = "admob" };
            Subscribe(mgr, network);

            Assert.DoesNotThrow(() => network.TriggerUserEarnedReward(1.0, "coins"),
                "SubscribeRewardCompletionEvent must not throw when _adRevenueTracker is null " +
                "— the null-conditional operator must silently skip the call");
        }
    }
}
