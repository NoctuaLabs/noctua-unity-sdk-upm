using NUnit.Framework;

namespace com.noctuagames.sdk.Tests.IAA
{
    /// <summary>
    /// Unit tests for ad fallback and secondary-network logic introduced in the
    /// ad-fallback fix (fix/ad-fallback-banner-interstitial-rewarded).
    ///
    /// Covers:
    ///   - MockAdNetwork.HasBannerAdUnit() / BannerAdUnitSet / ShowBannerCallCount
    ///   - HybridAdOrchestrator.ShowWithFallback banner, interstitial, and rewarded paths
    ///   - AdRevenueTrackingManager constructor does not crash (SystemInfo.deviceUniqueIdentifier
    ///     must be cached on the main thread, not called from JNI callbacks)
    /// </summary>
    [TestFixture]
    public class AdFallbackTest
    {
        private MockAdNetwork _primary;
        private MockAdNetwork _secondary;

        [SetUp]
        public void SetUp()
        {
            _primary   = new MockAdNetwork { NetworkName = "applovin" };
            _secondary = new MockAdNetwork { NetworkName = "admob" };
        }

        // ─── HasBannerAdUnit / BannerAdUnitSet ───────────────────────────────

        [Test]
        public void HasBannerAdUnit_DefaultFalse()
        {
            Assert.IsFalse(_primary.HasBannerAdUnit(),
                "HasBannerAdUnit must return false before any banner unit is configured");
        }

        [Test]
        public void HasBannerAdUnit_TrueAfterSetBannerAdUnitId()
        {
            _primary.SetBannerAdUnitId("ca-app-pub-test~1234567890");

            Assert.IsTrue(_primary.HasBannerAdUnit(),
                "HasBannerAdUnit must return true after SetBannerAdUnitId is called");
        }

        [Test]
        public void HasBannerAdUnit_TrueWhenBannerAdUnitSetAssignedDirectly()
        {
            _primary.BannerAdUnitSet = true;

            Assert.IsTrue(_primary.HasBannerAdUnit());
        }

        [Test]
        public void ShowBannerAd_IncrementsShowBannerCallCount()
        {
            Assert.AreEqual(0, _primary.ShowBannerCallCount);

            _primary.ShowBannerAd();
            _primary.ShowBannerAd();

            Assert.AreEqual(2, _primary.ShowBannerCallCount);
        }

        // ─── HybridAdOrchestrator: banner fallback ───────────────────────────

        [Test]
        public void ShowWithFallback_Banner_PrimaryHasUnit_ShowsFromPrimary()
        {
            _primary.BannerAdUnitSet = true;
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            IAdNetwork shown = null;
            orc.ShowWithFallback(AdFormatKey.Banner, n => shown = n, n => n.HasBannerAdUnit());

            Assert.AreSame(_primary, shown,
                "Primary has a banner unit — it should be preferred");
            Assert.AreEqual(0, _secondary.ShowBannerCallCount,
                "Secondary must not be called when primary has a banner unit");
        }

        [Test]
        public void ShowWithFallback_Banner_PrimaryNoUnit_SecondaryHasUnit_ShowsFromSecondary()
        {
            // Primary has NO banner unit; secondary has one.
            _secondary.BannerAdUnitSet = true;
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            IAdNetwork shown = null;
            orc.ShowWithFallback(AdFormatKey.Banner, n => shown = n, n => n.HasBannerAdUnit());

            Assert.AreSame(_secondary, shown,
                "When primary has no banner unit, the orchestrator should fall back to secondary");
        }

        [Test]
        public void ShowWithFallback_Banner_NeitherNetworkHasUnit_FiresOnAdFailedDisplayed()
        {
            // Neither network has a banner unit.
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            bool failedFired = false;
            orc.OnAdFailedDisplayed += () => failedFired = true;

            orc.ShowWithFallback(AdFormatKey.Banner, n => { }, n => n.HasBannerAdUnit());

            Assert.IsTrue(failedFired,
                "OnAdFailedDisplayed must fire when no network has a banner unit");
        }

        [Test]
        public void ShowWithFallback_Banner_PrimaryOnly_NoUnit_FiresOnAdFailedDisplayed()
        {
            // Single-network setup (no secondary), primary has no banner unit.
            var orc = new HybridAdOrchestrator(_primary);

            bool failedFired = false;
            orc.OnAdFailedDisplayed += () => failedFired = true;

            orc.ShowWithFallback(AdFormatKey.Banner, n => { }, n => n.HasBannerAdUnit());

            Assert.IsTrue(failedFired);
        }

        // ─── HybridAdOrchestrator: interstitial fallback ─────────────────────

        [Test]
        public void ShowWithFallback_Interstitial_PrimaryReady_ShowsFromPrimary()
        {
            _primary.InterstitialReady   = true;
            _secondary.InterstitialReady = true;
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            IAdNetwork shown = null;
            orc.ShowWithFallback(
                AdFormatKey.Interstitial,
                n => shown = n,
                n => n.IsInterstitialReady());

            Assert.AreSame(_primary, shown,
                "Primary is ready — it should be shown first");
        }

        [Test]
        public void ShowWithFallback_Interstitial_PrimaryNotReady_FallsBackToSecondary()
        {
            _primary.InterstitialReady   = false;
            _secondary.InterstitialReady = true;
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            IAdNetwork shown = null;
            orc.ShowWithFallback(
                AdFormatKey.Interstitial,
                n => shown = n,
                n => n.IsInterstitialReady());

            Assert.AreSame(_secondary, shown,
                "Primary is not ready — the orchestrator should fall back to secondary");
        }

        [Test]
        public void ShowWithFallback_Interstitial_BothNotReady_FiresOnAdFailedDisplayed()
        {
            _primary.InterstitialReady   = false;
            _secondary.InterstitialReady = false;
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            bool failedFired = false;
            orc.OnAdFailedDisplayed += () => failedFired = true;

            orc.ShowWithFallback(
                AdFormatKey.Interstitial,
                n => { },
                n => n.IsInterstitialReady());

            Assert.IsTrue(failedFired,
                "OnAdFailedDisplayed must fire when neither network has an interstitial ready");
        }

        [Test]
        public void ShowWithFallback_Interstitial_PrimaryOnly_NotReady_FiresOnAdFailedDisplayed()
        {
            _primary.InterstitialReady = false;
            var orc = new HybridAdOrchestrator(_primary); // no secondary

            bool failedFired = false;
            orc.OnAdFailedDisplayed += () => failedFired = true;

            orc.ShowWithFallback(
                AdFormatKey.Interstitial,
                n => { },
                n => n.IsInterstitialReady());

            Assert.IsTrue(failedFired);
        }

        // ─── HybridAdOrchestrator: rewarded fallback ─────────────────────────

        [Test]
        public void ShowWithFallback_Rewarded_PrimaryReady_ShowsFromPrimary()
        {
            _primary.RewardedReady   = true;
            _secondary.RewardedReady = true;
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            IAdNetwork shown = null;
            orc.ShowWithFallback(
                AdFormatKey.Rewarded,
                n => shown = n,
                n => n.IsRewardedAdReady());

            Assert.AreSame(_primary, shown);
        }

        [Test]
        public void ShowWithFallback_Rewarded_PrimaryNotReady_FallsBackToSecondary()
        {
            _primary.RewardedReady   = false;
            _secondary.RewardedReady = true;
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            IAdNetwork shown = null;
            orc.ShowWithFallback(
                AdFormatKey.Rewarded,
                n => shown = n,
                n => n.IsRewardedAdReady());

            Assert.AreSame(_secondary, shown,
                "Primary is not ready — the orchestrator should fall back to secondary for rewarded");
        }

        [Test]
        public void ShowWithFallback_Rewarded_BothNotReady_FiresOnAdFailedDisplayed()
        {
            _primary.RewardedReady   = false;
            _secondary.RewardedReady = false;
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            bool failedFired = false;
            orc.OnAdFailedDisplayed += () => failedFired = true;

            orc.ShowWithFallback(
                AdFormatKey.Rewarded,
                n => { },
                n => n.IsRewardedAdReady());

            Assert.IsTrue(failedFired);
        }

        // ─── AdRevenueTrackingManager constructor thread-safety ──────────────

        [Test]
        public void AdRevenueTrackingManager_Constructor_DoesNotThrow()
        {
            // SystemInfo.deviceUniqueIdentifier is now cached in the constructor.
            // The Unity Test Runner runs on the main thread, so this must not throw
            // a UnityException ("can only be called from the main thread").
            Assert.DoesNotThrow(
                () => { var _ = new AdRevenueTrackingManager(); },
                "AdRevenueTrackingManager constructor must not throw — deviceId is cached on construction");
        }

        [Test]
        public void AdRevenueTrackingManager_NullParams_Constructor_DoesNotThrow()
        {
            Assert.DoesNotThrow(
                () => { var _ = new AdRevenueTrackingManager(adRevenueTracker: null, taichiConfig: null); },
                "Constructor with explicit null params must not throw");
        }
    }
}
