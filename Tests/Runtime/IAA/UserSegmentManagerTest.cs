using System;
using NUnit.Framework;
using UnityEngine;

namespace com.noctuagames.sdk.Tests.IAA
{
    /// <summary>
    /// Unit tests for <see cref="UserSegmentManager"/>.
    /// Covers: country tier classification, payer tier, session tier, install cohort,
    /// composite segment format, PlayerPrefs persistence, and RecordPurchase.
    /// </summary>
    [TestFixture]
    public class UserSegmentManagerTest
    {
        private const string PrefsPrefix = "NoctuaSeg_";

        [SetUp]
        public void SetUp()
        {
            // Clear all segment PlayerPrefs before each test for isolation
            PlayerPrefs.DeleteKey($"{PrefsPrefix}install_ticks");
            PlayerPrefs.DeleteKey($"{PrefsPrefix}session_count");
            PlayerPrefs.DeleteKey($"{PrefsPrefix}purchase_count");
            PlayerPrefs.Save();
        }

        // ─── GetCountryTier ────────────────────────────────────────────────────

        [Test]
        [TestCase("US", "t1")]
        [TestCase("CA", "t1")]
        [TestCase("AU", "t1")]
        [TestCase("JP", "t1")]
        [TestCase("KR", "t1")]
        [TestCase("DE", "t1")]
        [TestCase("GB", "t1")]
        [TestCase("SG", "t1")]
        public void GetCountryTier_T1Countries_ReturnsT1(string country, string expected)
        {
            Assert.AreEqual(expected, UserSegmentManager.GetCountryTier(country));
        }

        [Test]
        [TestCase("BR", "t2")]
        [TestCase("MX", "t2")]
        [TestCase("PH", "t2")]
        [TestCase("ID", "t2")]
        [TestCase("TH", "t2")]
        [TestCase("TR", "t2")]
        public void GetCountryTier_T2Countries_ReturnsT2(string country, string expected)
        {
            Assert.AreEqual(expected, UserSegmentManager.GetCountryTier(country));
        }

        [Test]
        [TestCase("VN", "t3")]
        [TestCase("MM", "t3")]
        [TestCase("KH", "t3")]
        [TestCase("ZZ", "t3")]
        public void GetCountryTier_UnknownCountries_ReturnsT3(string country, string expected)
        {
            Assert.AreEqual(expected, UserSegmentManager.GetCountryTier(country));
        }

        [Test]
        public void GetCountryTier_NullOrEmpty_ReturnsT3()
        {
            Assert.AreEqual("t3", UserSegmentManager.GetCountryTier(null));
            Assert.AreEqual("t3", UserSegmentManager.GetCountryTier(""));
        }

        [Test]
        public void GetCountryTier_LowercaseInput_StillMatches()
        {
            Assert.AreEqual("t1", UserSegmentManager.GetCountryTier("us"));
            Assert.AreEqual("t2", UserSegmentManager.GetCountryTier("br"));
        }

        // ─── GetSessionTier ────────────────────────────────────────────────────

        [Test]
        public void GetSessionTier_FirstSession_ReturnsNew()
        {
            // Constructor increments session_count from 0 → 1
            var mgr = new UserSegmentManager();

            Assert.AreEqual("new", mgr.GetSessionTier());
        }

        [Test]
        public void GetSessionTier_ThirdSession_ReturnsNew()
        {
            PlayerPrefs.SetInt($"{PrefsPrefix}session_count", 2); // pre-set to 2
            var mgr = new UserSegmentManager(); // increments to 3

            Assert.AreEqual("new", mgr.GetSessionTier());
        }

        [Test]
        public void GetSessionTier_FourthSession_ReturnsReturning()
        {
            PlayerPrefs.SetInt($"{PrefsPrefix}session_count", 3);
            var mgr = new UserSegmentManager(); // increments to 4

            Assert.AreEqual("returning", mgr.GetSessionTier());
        }

        [Test]
        public void GetSessionTier_TwentiethSession_ReturnsReturning()
        {
            PlayerPrefs.SetInt($"{PrefsPrefix}session_count", 19);
            var mgr = new UserSegmentManager(); // increments to 20

            Assert.AreEqual("returning", mgr.GetSessionTier());
        }

        [Test]
        public void GetSessionTier_TwentyFirstSession_ReturnsLoyal()
        {
            PlayerPrefs.SetInt($"{PrefsPrefix}session_count", 20);
            var mgr = new UserSegmentManager(); // increments to 21

            Assert.AreEqual("loyal", mgr.GetSessionTier());
        }

        // ─── GetInstallCohort ──────────────────────────────────────────────────

        [Test]
        public void GetInstallCohort_FirstLaunch_ReturnsD0D1()
        {
            // No install timestamp in prefs → constructor writes now → 0 days elapsed
            var mgr = new UserSegmentManager();

            Assert.AreEqual("d0d1", mgr.GetInstallCohort());
        }

        [Test]
        public void GetInstallCohort_3DaysAgo_ReturnsD2D7()
        {
            long ticks = DateTime.UtcNow.AddDays(-3).Ticks;
            PlayerPrefs.SetString($"{PrefsPrefix}install_ticks", ticks.ToString());
            var mgr = new UserSegmentManager();

            Assert.AreEqual("d2d7", mgr.GetInstallCohort());
        }

        [Test]
        public void GetInstallCohort_15DaysAgo_ReturnsD8D30()
        {
            long ticks = DateTime.UtcNow.AddDays(-15).Ticks;
            PlayerPrefs.SetString($"{PrefsPrefix}install_ticks", ticks.ToString());
            var mgr = new UserSegmentManager();

            Assert.AreEqual("d8d30", mgr.GetInstallCohort());
        }

        [Test]
        public void GetInstallCohort_35DaysAgo_ReturnsD30Plus()
        {
            long ticks = DateTime.UtcNow.AddDays(-35).Ticks;
            PlayerPrefs.SetString($"{PrefsPrefix}install_ticks", ticks.ToString());
            var mgr = new UserSegmentManager();

            Assert.AreEqual("d30plus", mgr.GetInstallCohort());
        }

        // ─── GetPayerTier ──────────────────────────────────────────────────────

        [Test]
        public void GetPayerTier_NoPurchases_ReturnsNonpayer()
        {
            var mgr = new UserSegmentManager();

            Assert.AreEqual("nonpayer", mgr.GetPayerTier());
        }

        [Test]
        public void GetPayerTier_OnePurchase_ReturnsLowspender()
        {
            PlayerPrefs.SetInt($"{PrefsPrefix}purchase_count", 1);
            var mgr = new UserSegmentManager();

            Assert.AreEqual("lowspender", mgr.GetPayerTier());
        }

        [Test]
        public void GetPayerTier_FourPurchases_ReturnsLowspender()
        {
            PlayerPrefs.SetInt($"{PrefsPrefix}purchase_count", 4);
            var mgr = new UserSegmentManager();

            Assert.AreEqual("lowspender", mgr.GetPayerTier());
        }

        [Test]
        public void GetPayerTier_FivePurchases_ReturnsHighspender()
        {
            PlayerPrefs.SetInt($"{PrefsPrefix}purchase_count", 5);
            var mgr = new UserSegmentManager();

            Assert.AreEqual("highspender", mgr.GetPayerTier());
        }

        // ─── RecordPurchase ────────────────────────────────────────────────────

        [Test]
        public void RecordPurchase_FiveTimesYieldsHighspender()
        {
            var mgr = new UserSegmentManager();
            Assert.AreEqual("nonpayer", mgr.GetPayerTier());

            for (int i = 0; i < 5; i++)
                mgr.RecordPurchase();

            Assert.AreEqual("highspender", mgr.GetPayerTier());
        }

        [Test]
        public void RecordPurchase_IncrementsPersistedCount()
        {
            var mgr = new UserSegmentManager();
            mgr.RecordPurchase();
            mgr.RecordPurchase();

            int stored = PlayerPrefs.GetInt($"{PrefsPrefix}purchase_count", 0);
            Assert.AreEqual(2, stored);
        }

        // ─── InstallTimestamp persistence ──────────────────────────────────────

        [Test]
        public void InstallTimestamp_PersistedAcrossConstructions()
        {
            var mgr1 = new UserSegmentManager();
            string ticks1 = PlayerPrefs.GetString($"{PrefsPrefix}install_ticks", "");

            var mgr2 = new UserSegmentManager();
            string ticks2 = PlayerPrefs.GetString($"{PrefsPrefix}install_ticks", "");

            Assert.AreEqual(ticks1, ticks2, "Install timestamp must not be overwritten on subsequent launches");
        }

        // ─── GetCompositeSegment ───────────────────────────────────────────────

        [Test]
        public void GetCompositeSegment_CorrectFormat()
        {
            var mgr = new UserSegmentManager();
            string segment = mgr.GetCompositeSegment("US");

            // Expected: "t1_nonpayer_new_d0d1" (first session, no purchases, fresh install)
            Assert.AreEqual("t1_nonpayer_new_d0d1", segment);
        }

        [Test]
        public void GetCompositeSegment_UnknownCountry_UsesT3()
        {
            var mgr = new UserSegmentManager();
            string segment = mgr.GetCompositeSegment("ZZ");

            StringAssert.StartsWith("t3_", segment);
        }

        [Test]
        public void GetCompositeSegment_NullCountry_UsesT3()
        {
            var mgr = new UserSegmentManager();
            string segment = mgr.GetCompositeSegment(null);

            StringAssert.StartsWith("t3_", segment);
        }

        // ─── GetCountryTierKey ─────────────────────────────────────────────────

        [Test]
        public void GetCountryTierKey_ReturnsCorrectTier()
        {
            var mgr = new UserSegmentManager();

            Assert.AreEqual("t1", mgr.GetCountryTierKey("JP"));
            Assert.AreEqual("t2", mgr.GetCountryTierKey("BR"));
            Assert.AreEqual("t3", mgr.GetCountryTierKey("VN"));
        }
    }
}
