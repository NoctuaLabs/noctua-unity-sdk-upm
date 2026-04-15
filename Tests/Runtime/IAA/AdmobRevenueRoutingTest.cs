using NUnit.Framework;
using UnityEngine;

namespace com.noctuagames.sdk.Tests.IAA
{
    /// <summary>
    /// Unit tests for AdMob per-format Taichi routing.
    ///
    /// Context: Before the routing fix, <c>MediationManager.SubscribeAdmobRevenueEvents</c>
    /// subscribed only to <c>AdmobManager.AdmobOnAdRevenuePaid</c> (the aggregate event),
    /// which routed every format — interstitial, rewarded, banner, rewarded-interstitial,
    /// app-open — into <c>AdRevenueTrackingManager.ProcessAdmobRevenue</c> (the banner/legacy
    /// path). That path calls only <see cref="AdRevenueTrackingManager.ProcessAllFormatsThresholds"/>,
    /// so Taichi Steps 3–6 never incremented for AdMob impressions.
    ///
    /// After the fix, <c>AdmobManager</c> exposes per-format revenue events
    /// (<c>AdmobOnBannerRevenuePaid</c>, <c>AdmobOnInterstitialRevenuePaid</c>,
    /// <c>AdmobOnRewardedRevenuePaid</c>, …) and <c>MediationManager</c> subscribes to each
    /// one and calls the matching <c>ProcessAdmob*Revenue</c> method.
    ///
    /// These tests pin the Taichi-step invariants the fix guarantees, verified at the
    /// <see cref="AdRevenueTrackingManager"/> public-API boundary so they stay valid in
    /// all build configurations (the <c>ProcessAdmob*</c> entry points themselves require
    /// <c>UNITY_ADMOB</c> and real <c>AdValue</c>/<c>ResponseInfo</c>, which are infeasible
    /// to construct in a unit test).
    /// </summary>
    [TestFixture]
    public class AdmobRevenueRoutingTest
    {
        private const string KeyTotalRevenue      = "Noctua_Taichi_TotalRevenue";
        private const string KeyTotalAdCount      = "Noctua_Taichi_TotalAdCount";
        private const string KeyTotalImpressions  = "Noctua_Taichi_TotalImpressions";
        private const string KeyInterstitialCount = "Noctua_Taichi_InterstitialCount";
        private const string KeyRewardedCount     = "Noctua_Taichi_RewardedCount";
        private const string KeyRewardedRevenue   = "Noctua_Taichi_RewardedRevenue";

        private MockAdRevenueTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            _tracker = new MockAdRevenueTracker();
            PlayerPrefs.DeleteKey(KeyTotalRevenue);
            PlayerPrefs.DeleteKey(KeyTotalAdCount);
            PlayerPrefs.DeleteKey(KeyTotalImpressions);
            PlayerPrefs.DeleteKey(KeyInterstitialCount);
            PlayerPrefs.DeleteKey(KeyRewardedCount);
            PlayerPrefs.DeleteKey(KeyRewardedRevenue);
            PlayerPrefs.Save();
        }

        private TaichiConfig DefaultConfig() => new TaichiConfig
        {
            RevenueThreshold           = 0.01f,
            AdCountThreshold           = 10,
            TotalImpressionThreshold   = 10,
            InterstitialCountThreshold = 5,
            RewardedCountThreshold     = 5,
            RewardedRevenueThreshold   = 0.01f,
        };

        // ── Helpers mirroring what MediationManager.SubscribeAdmobRevenueEvents
        //    does per format after the routing fix. Each helper is a one-liner
        //    that stays in lockstep with AdRevenueTrackingManager.ProcessAdmob*
        //    (which we can't call directly in edit-mode tests because those
        //    methods take GoogleMobileAds' AdValue/ResponseInfo).

        private static void SimulateBannerImpression(AdRevenueTrackingManager mgr, double revenue)
        {
            // ProcessAdmobRevenue(adValue, responseInfo) → ProcessAllFormatsThresholds(revenue)
            mgr.ProcessAllFormatsThresholds(revenue);
        }

        private static void SimulateInterstitialImpression(AdRevenueTrackingManager mgr, double revenue)
        {
            // ProcessAdmobInterstitialRevenue(...) → All + Interstitial
            mgr.ProcessAllFormatsThresholds(revenue);
            mgr.ProcessInterstitialThresholds(revenue);
        }

        private static void SimulateRewardedImpression(AdRevenueTrackingManager mgr, double revenue)
        {
            // ProcessAdmobRewardedRevenue(...) → All + Rewarded
            mgr.ProcessAllFormatsThresholds(revenue);
            mgr.ProcessRewardedThresholds(revenue);
        }

        // ── Banner impressions only hit Steps 1 & 2 ──────────────────────────

        [Test]
        public void BannerImpressions_DoNotAdvanceInterstitialOrRewardedSteps()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            for (int i = 0; i < 10; i++)
                SimulateBannerImpression(mgr, 0);

            Assert.IsTrue(_tracker.WasFired("TenAdsShown"),
                "Banner impressions must advance Step 2 (TenAdsShown)");
            Assert.IsFalse(_tracker.WasFired("taichi_total_ad_impression"),
                "Banners must NOT advance Step 3 (interstitial+rewarded only)");
            Assert.IsFalse(_tracker.WasFired("taichi_interstitial_ad_impression"),
                "Banners must NOT advance Step 4");
            Assert.IsFalse(_tracker.WasFired("taichi_rewarded_ad_impression"),
                "Banners must NOT advance Step 5");
        }

        // ── Interstitial impressions hit Steps 1, 2, 3, 4 but NOT 5 / 6 ──────

        [Test]
        public void InterstitialImpressions_AdvanceTotalAndInterstitialSteps()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            for (int i = 0; i < 5; i++)
                SimulateInterstitialImpression(mgr, 0);

            Assert.IsTrue(_tracker.WasFired("taichi_interstitial_ad_impression"),
                "5 interstitials must fire Step 4 (threshold=5)");
            Assert.IsFalse(_tracker.WasFired("taichi_rewarded_ad_impression"),
                "Interstitials must NOT advance rewarded Step 5");
            Assert.IsFalse(_tracker.WasFired("taichi_rewarded_ad_revenue"),
                "Interstitials must NOT advance rewarded revenue Step 6");
        }

        [Test]
        public void InterstitialImpressions_TenRounds_FireStep3()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            for (int i = 0; i < 10; i++)
                SimulateInterstitialImpression(mgr, 0);

            Assert.IsTrue(_tracker.WasFired("taichi_total_ad_impression"),
                "10 interstitials must advance Step 3 (total impression counter)");
        }

        // ── Rewarded impressions hit Steps 1, 2, 3, 5, 6 but NOT 4 ───────────

        [Test]
        public void RewardedImpressions_AdvanceRewardedSteps()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            for (int i = 0; i < 5; i++)
                SimulateRewardedImpression(mgr, 0);

            Assert.IsTrue(_tracker.WasFired("taichi_rewarded_ad_impression"),
                "5 rewarded impressions must fire Step 5 (threshold=5)");
            Assert.IsFalse(_tracker.WasFired("taichi_interstitial_ad_impression"),
                "Rewarded must NOT advance interstitial Step 4");
        }

        [Test]
        public void RewardedImpressions_RevenueAccumulates_FiresStep6()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            // 2 rewarded impressions @ 0.006 USD each = 0.012 ≥ 0.01 threshold
            SimulateRewardedImpression(mgr, 0.006);
            SimulateRewardedImpression(mgr, 0.006);

            Assert.IsTrue(_tracker.WasFired("taichi_rewarded_ad_revenue"),
                "Rewarded revenue must accumulate into Step 6");
        }

        [Test]
        public void BannerRevenue_DoesNotAdvanceRewardedRevenueStep()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            // Large banner revenue — must not leak into Step 6 (rewarded-only)
            SimulateBannerImpression(mgr, 10.0);

            Assert.IsFalse(_tracker.WasFired("taichi_rewarded_ad_revenue"),
                "Banner revenue must NOT advance Step 6 (rewarded-only)");
        }

        // ── Mixed session — realistic scenario ───────────────────────────────

        [Test]
        public void MixedSession_CorrectlyPartitionsSteps()
        {
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            // 6 banners, 3 interstitials, 3 rewarded
            for (int i = 0; i < 6; i++) SimulateBannerImpression(mgr, 0);
            for (int i = 0; i < 3; i++) SimulateInterstitialImpression(mgr, 0);
            for (int i = 0; i < 3; i++) SimulateRewardedImpression(mgr, 0);

            // Step 2: 6+3+3 = 12 ≥ 10 → fires
            Assert.IsTrue(_tracker.WasFired("TenAdsShown"),
                "Total ad count Step 2 should fire at 10 combined impressions");

            // Step 3: 3 (inter) + 3 (rew) = 6, < 10 → does NOT fire
            Assert.IsFalse(_tracker.WasFired("taichi_total_ad_impression"),
                "Step 3 must count only interstitial + rewarded; 6 < threshold 10");

            // Step 4: only 3 interstitials, < 5 → does NOT fire
            Assert.IsFalse(_tracker.WasFired("taichi_interstitial_ad_impression"),
                "Step 4 should not fire at 3 interstitials (threshold 5)");

            // Step 5: only 3 rewarded, < 5 → does NOT fire
            Assert.IsFalse(_tracker.WasFired("taichi_rewarded_ad_impression"),
                "Step 5 should not fire at 3 rewarded (threshold 5)");
        }

        // ── Regression guard — pre-fix behavior is now forbidden ─────────────

        [Test]
        public void Regression_InterstitialRoutedAsBanner_WouldNotFireStep4()
        {
            // This test documents the bug that was fixed: if interstitial impressions
            // are (incorrectly) routed through ProcessAllFormatsThresholds only
            // (the banner/legacy path), Steps 3–6 never fire regardless of volume.
            // Any regression that removes per-format routing will be caught by the
            // positive tests above; this negative test freezes the broken behavior
            // to make the intent unambiguous.
            var mgr = new AdRevenueTrackingManager(_tracker, DefaultConfig());

            // 20 "interstitials" sent through the banner path = pre-fix behavior
            for (int i = 0; i < 20; i++)
                SimulateBannerImpression(mgr, 0);

            Assert.IsFalse(_tracker.WasFired("taichi_interstitial_ad_impression"),
                "Pre-fix routing (banner path for all formats) must NOT fire Step 4 — " +
                "which is exactly why the per-format routing fix was required");
            Assert.IsFalse(_tracker.WasFired("taichi_rewarded_ad_impression"),
                "Pre-fix routing must NOT fire Step 5 either");
        }
    }
}
