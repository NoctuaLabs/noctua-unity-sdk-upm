using System;
using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime
{
    /// <summary>
    /// EditMode NUnit tests for <see cref="UserSegmentManager"/>.
    ///
    /// Covers:
    ///   — <c>GetCountryTier</c> static method (null, empty, T1, T2, T3, case-insensitivity)
    ///   — <c>GetPayerTier</c>   instance method (nonpayer / lowspender / highspender thresholds)
    ///   — <c>GetSessionTier</c> instance method (new / returning / loyal thresholds)
    ///   — <c>GetInstallCohort</c> instance method (d0d1 / d2d7 / d8d30 / d30plus)
    ///   — <c>GetCompositeSegment</c> format contract
    ///   — <c>GetCountryTierKey</c> delegates to <c>GetCountryTier</c>
    ///   — <c>RecordPurchase</c>  increments persisted purchase count
    ///
    /// PlayerPrefs keys are cleared before and after each test to ensure isolation.
    /// Because the <see cref="UserSegmentManager"/> constructor always increments
    /// <c>NoctuaSeg_session_count</c>, tests that control session tier must account
    /// for that +1 when pre-seeding the PlayerPrefs key.
    /// </summary>
    [TestFixture]
    public class UserSegmentManagerTest
    {
        private const string KeyPrefix        = "NoctuaSeg_";
        private const string KeyPurchaseCount = KeyPrefix + "purchase_count";
        private const string KeySessionCount  = KeyPrefix + "session_count";
        private const string KeyInstallTicks  = KeyPrefix + "install_ticks";

        [SetUp]
        public void SetUp()
        {
            // Clean slate before every test.
            PlayerPrefs.DeleteKey(KeyPurchaseCount);
            PlayerPrefs.DeleteKey(KeySessionCount);
            PlayerPrefs.DeleteKey(KeyInstallTicks);
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            // Ensure no state leaks into subsequent tests.
            PlayerPrefs.DeleteKey(KeyPurchaseCount);
            PlayerPrefs.DeleteKey(KeySessionCount);
            PlayerPrefs.DeleteKey(KeyInstallTicks);
            PlayerPrefs.Save();
        }

        // ═══════════════════════════════════════════════════════════════════
        // GetCountryTier (static — no PlayerPrefs)
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void GetCountryTier_Null_ReturnsT3()
        {
            Assert.AreEqual("t3", UserSegmentManager.GetCountryTier(null));
        }

        [Test]
        public void GetCountryTier_Empty_ReturnsT3()
        {
            Assert.AreEqual("t3", UserSegmentManager.GetCountryTier(""));
        }

        [Test]
        public void GetCountryTier_UnknownCode_ReturnsT3()
        {
            Assert.AreEqual("t3", UserSegmentManager.GetCountryTier("ZZ"));
        }

        [Test]
        [TestCase("US")] [TestCase("JP")] [TestCase("DE")]
        [TestCase("AU")] [TestCase("GB")] [TestCase("SG")]
        public void GetCountryTier_Tier1Country_ReturnsT1(string code)
        {
            Assert.AreEqual("t1", UserSegmentManager.GetCountryTier(code));
        }

        [Test]
        public void GetCountryTier_Tier1_CaseInsensitive()
        {
            Assert.AreEqual("t1", UserSegmentManager.GetCountryTier("us"),
                "Country code lookup must be case-insensitive");
        }

        [Test]
        [TestCase("BR")] [TestCase("ID")] [TestCase("TH")]
        [TestCase("PL")] [TestCase("SA")] [TestCase("TW")]
        public void GetCountryTier_Tier2Country_ReturnsT2(string code)
        {
            Assert.AreEqual("t2", UserSegmentManager.GetCountryTier(code));
        }

        [Test]
        public void GetCountryTier_Tier2_CaseInsensitive()
        {
            Assert.AreEqual("t2", UserSegmentManager.GetCountryTier("id"),
                "Case-insensitivity must apply to Tier 2 codes as well");
        }

        // ═══════════════════════════════════════════════════════════════════
        // GetCountryTierKey delegates to GetCountryTier
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void GetCountryTierKey_SameResultAsGetCountryTier()
        {
            // Pre-seed install timestamp so constructor's InitializeInstallTimestamp is a no-op.
            PlayerPrefs.SetString(KeyInstallTicks, DateTime.UtcNow.Ticks.ToString());
            PlayerPrefs.Save();

            var mgr = new UserSegmentManager();

            Assert.AreEqual(UserSegmentManager.GetCountryTier("US"), mgr.GetCountryTierKey("US"));
            Assert.AreEqual(UserSegmentManager.GetCountryTier("ZZ"), mgr.GetCountryTierKey("ZZ"));
            Assert.AreEqual(UserSegmentManager.GetCountryTier(null),  mgr.GetCountryTierKey(null));
        }

        // ═══════════════════════════════════════════════════════════════════
        // GetPayerTier — thresholds: 0=nonpayer, 1–4=lowspender, ≥5=highspender
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void GetPayerTier_NoPurchases_ReturnsNonpayer()
        {
            // purchase_count key absent → 0 purchases
            var mgr = new UserSegmentManager();

            Assert.AreEqual("nonpayer", mgr.GetPayerTier());
        }

        [Test]
        public void GetPayerTier_OnePurchase_ReturnsLowspender()
        {
            PlayerPrefs.SetInt(KeyPurchaseCount, 1);
            PlayerPrefs.Save();

            var mgr = new UserSegmentManager();

            Assert.AreEqual("lowspender", mgr.GetPayerTier());
        }

        [Test]
        public void GetPayerTier_FourPurchases_ReturnsLowspender()
        {
            PlayerPrefs.SetInt(KeyPurchaseCount, 4);
            PlayerPrefs.Save();

            var mgr = new UserSegmentManager();

            Assert.AreEqual("lowspender", mgr.GetPayerTier());
        }

        [Test]
        public void GetPayerTier_FivePurchases_ReturnsHighspender()
        {
            PlayerPrefs.SetInt(KeyPurchaseCount, 5);
            PlayerPrefs.Save();

            var mgr = new UserSegmentManager();

            Assert.AreEqual("highspender", mgr.GetPayerTier());
        }

        // ═══════════════════════════════════════════════════════════════════
        // RecordPurchase — increments purchase count
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void RecordPurchase_IncrementsPurchaseCount()
        {
            PlayerPrefs.SetInt(KeyPurchaseCount, 0);
            PlayerPrefs.Save();

            var mgr = new UserSegmentManager();
            mgr.RecordPurchase();

            Assert.AreEqual(1, PlayerPrefs.GetInt(KeyPurchaseCount, 0));
        }

        [Test]
        public void RecordPurchase_Twice_CountIsTwo()
        {
            var mgr = new UserSegmentManager();
            mgr.RecordPurchase();
            mgr.RecordPurchase();

            Assert.AreEqual(2, PlayerPrefs.GetInt(KeyPurchaseCount, 0));
        }

        [Test]
        public void RecordPurchase_UpgradesPayerTierFromNonpayer()
        {
            var mgr = new UserSegmentManager();
            Assert.AreEqual("nonpayer",   mgr.GetPayerTier());

            mgr.RecordPurchase();
            Assert.AreEqual("lowspender", mgr.GetPayerTier());
        }

        // ═══════════════════════════════════════════════════════════════════
        // GetSessionTier — thresholds: ≤3=new, 4–20=returning, ≥21=loyal
        // Note: constructor always increments session_count by 1.
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void GetSessionTier_FirstSession_ReturnsNew()
        {
            // No key set → constructor reads 0, writes 1 → GetSessionTier reads 1 ≤ 3 → "new"
            var mgr = new UserSegmentManager();

            Assert.AreEqual("new", mgr.GetSessionTier());
        }

        [Test]
        public void GetSessionTier_ThreeSessionsAfterIncrement_ReturnsNew()
        {
            // Pre-seed 2; constructor increments to 3; GetSessionTier reads 3 ≤ 3 → "new"
            PlayerPrefs.SetInt(KeySessionCount, 2);
            PlayerPrefs.Save();

            var mgr = new UserSegmentManager();

            Assert.AreEqual("new", mgr.GetSessionTier());
        }

        [Test]
        public void GetSessionTier_FourSessionsAfterIncrement_ReturnsReturning()
        {
            // Pre-seed 3; constructor increments to 4; GetSessionTier reads 4 ≤ 20 → "returning"
            PlayerPrefs.SetInt(KeySessionCount, 3);
            PlayerPrefs.Save();

            var mgr = new UserSegmentManager();

            Assert.AreEqual("returning", mgr.GetSessionTier());
        }

        [Test]
        public void GetSessionTier_TwentySessionsAfterIncrement_ReturnsReturning()
        {
            // Pre-seed 19; constructor increments to 20; GetSessionTier reads 20 ≤ 20 → "returning"
            PlayerPrefs.SetInt(KeySessionCount, 19);
            PlayerPrefs.Save();

            var mgr = new UserSegmentManager();

            Assert.AreEqual("returning", mgr.GetSessionTier());
        }

        [Test]
        public void GetSessionTier_TwentyOneSessionsAfterIncrement_ReturnsLoyal()
        {
            // Pre-seed 20; constructor increments to 21; GetSessionTier reads 21 > 20 → "loyal"
            PlayerPrefs.SetInt(KeySessionCount, 20);
            PlayerPrefs.Save();

            var mgr = new UserSegmentManager();

            Assert.AreEqual("loyal", mgr.GetSessionTier());
        }

        // ═══════════════════════════════════════════════════════════════════
        // GetInstallCohort — thresholds: ≤1=d0d1, ≤7=d2d7, ≤30=d8d30, >30=d30plus
        // ═══════════════════════════════════════════════════════════════════

        private static void SetInstallDaysAgo(int days)
        {
            long ticks = (DateTime.UtcNow - TimeSpan.FromDays(days)).Ticks;
            PlayerPrefs.SetString(KeyInstallTicks, ticks.ToString());
            PlayerPrefs.Save();
        }

        [Test]
        public void GetInstallCohort_ZeroDays_ReturnsD0D1()
        {
            SetInstallDaysAgo(0);
            var mgr = new UserSegmentManager();
            Assert.AreEqual("d0d1", mgr.GetInstallCohort());
        }

        [Test]
        public void GetInstallCohort_OneDayAgo_ReturnsD0D1()
        {
            SetInstallDaysAgo(1);
            var mgr = new UserSegmentManager();
            Assert.AreEqual("d0d1", mgr.GetInstallCohort());
        }

        [Test]
        public void GetInstallCohort_FiveDaysAgo_ReturnsD2D7()
        {
            SetInstallDaysAgo(5);
            var mgr = new UserSegmentManager();
            Assert.AreEqual("d2d7", mgr.GetInstallCohort());
        }

        [Test]
        public void GetInstallCohort_SevenDaysAgo_ReturnsD2D7()
        {
            SetInstallDaysAgo(7);
            var mgr = new UserSegmentManager();
            Assert.AreEqual("d2d7", mgr.GetInstallCohort());
        }

        [Test]
        public void GetInstallCohort_TenDaysAgo_ReturnsD8D30()
        {
            SetInstallDaysAgo(10);
            var mgr = new UserSegmentManager();
            Assert.AreEqual("d8d30", mgr.GetInstallCohort());
        }

        [Test]
        public void GetInstallCohort_ThirtyDaysAgo_ReturnsD8D30()
        {
            SetInstallDaysAgo(30);
            var mgr = new UserSegmentManager();
            Assert.AreEqual("d8d30", mgr.GetInstallCohort());
        }

        [Test]
        public void GetInstallCohort_ThirtyOneDaysAgo_ReturnsD30Plus()
        {
            SetInstallDaysAgo(31);
            var mgr = new UserSegmentManager();
            Assert.AreEqual("d30plus", mgr.GetInstallCohort());
        }

        [Test]
        public void GetInstallCohort_OneHundredDaysAgo_ReturnsD30Plus()
        {
            SetInstallDaysAgo(100);
            var mgr = new UserSegmentManager();
            Assert.AreEqual("d30plus", mgr.GetInstallCohort());
        }

        // ═══════════════════════════════════════════════════════════════════
        // GetCompositeSegment — format contract
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void GetCompositeSegment_HasFourUnderscoreSeparatedParts()
        {
            SetInstallDaysAgo(0);
            var mgr    = new UserSegmentManager();
            var result = mgr.GetCompositeSegment("US");

            var parts = result.Split('_');
            Assert.AreEqual(4, parts.Length,
                $"Composite segment must have 4 parts separated by '_'; got: '{result}'");
        }

        [Test]
        public void GetCompositeSegment_StartsWithCountryTier()
        {
            SetInstallDaysAgo(0);
            var mgr    = new UserSegmentManager();
            var result = mgr.GetCompositeSegment("US");

            StringAssert.StartsWith("t1_", result,
                "Composite segment must start with the country tier");
        }

        [Test]
        public void GetCompositeSegment_UnknownCountry_StartsWithT3()
        {
            SetInstallDaysAgo(0);
            var mgr    = new UserSegmentManager();
            var result = mgr.GetCompositeSegment("ZZ");

            StringAssert.StartsWith("t3_", result);
        }
    }
}
