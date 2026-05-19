using System;
using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime.IAA
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

        [Test]
        public void GetCountryTierKey_EmptyCountry_ReturnsT3()
        {
            // GetCountryTier: if (string.IsNullOrEmpty(isoCountryCode)) return "t3"; — empty branch
            var mgr = new UserSegmentManager();

            Assert.AreEqual("t3", mgr.GetCountryTierKey(""));
        }

        // ─── InitializeInstallTimestamp — "already has key" path ──────────────

        [Test]
        public void InitializeInstallTimestamp_KeyAlreadyExists_DoesNotOverwrite()
        {
            // First construction writes the key; second construction must not overwrite it.
            // This exercises the if (!PlayerPrefs.HasKey(key)) == false branch.
            var mgr1 = new UserSegmentManager();
            string ticksAfterFirst = PlayerPrefs.GetString($"{PrefsPrefix}install_ticks", "");

            var mgr2 = new UserSegmentManager();
            string ticksAfterSecond = PlayerPrefs.GetString($"{PrefsPrefix}install_ticks", "");

            Assert.IsFalse(string.IsNullOrEmpty(ticksAfterFirst), "Install ticks must be written on first construction");
            Assert.AreEqual(ticksAfterFirst, ticksAfterSecond,
                "Second construction must not overwrite existing install timestamp");
        }

        // ─── GetDaysSinceInstall — empty stored ticks branch ──────────────────

        [Test]
        public void GetInstallCohort_EmptyStoredTicks_ReturnsD0D1()
        {
            // string.IsNullOrEmpty(stored) branch in GetDaysSinceInstall — distinct from
            // the !long.TryParse branch exercised by the "not-a-number" test.
            // Set an empty string explicitly to hit the IsNullOrEmpty guard.
            PlayerPrefs.SetString($"{PrefsPrefix}install_ticks", "");
            var mgr = new UserSegmentManager();

            // GetDaysSinceInstall returns 0 → "d0d1"
            Assert.AreEqual("d0d1", mgr.GetInstallCohort(),
                "Empty install_ticks string must result in d0d1 cohort (0 days)");
        }

        // ─── GetCompositeSegment — null country hits t3 branch ────────────────

        [Test]
        public void GetCompositeSegment_NullCountry_StartsWithT3()
        {
            // Ensures GetCountryTier(null) path is exercised through GetCompositeSegment.
            var mgr = new UserSegmentManager();
            string segment = mgr.GetCompositeSegment(null);

            StringAssert.StartsWith("t3_", segment,
                "null country code must produce a t3_... composite segment");
        }
    }

    /// <summary>
    /// Extended edge-case tests for <see cref="UserSegmentManager"/>.
    /// Covers: boundary conditions for session/payer/install cohort tiers,
    /// composite segment format for all dimension combinations, additional
    /// T1/T2 countries, PlayerPrefs isolation, and RecordPurchase boundary values.
    /// </summary>
    [TestFixture]
    public class UserSegmentManagerEdgeCaseTest
    {
        private const string PrefsPrefix = "NoctuaSeg_";

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey($"{PrefsPrefix}install_ticks");
            PlayerPrefs.DeleteKey($"{PrefsPrefix}session_count");
            PlayerPrefs.DeleteKey($"{PrefsPrefix}purchase_count");
            PlayerPrefs.Save();
        }

        // ─── GetCountryTier — additional T1 countries ──────────────────────────

        [Test]
        [TestCase("FR", "t1")]
        [TestCase("NL", "t1")]
        [TestCase("SE", "t1")]
        [TestCase("NO", "t1")]
        [TestCase("DK", "t1")]
        [TestCase("FI", "t1")]
        [TestCase("CH", "t1")]
        [TestCase("AT", "t1")]
        [TestCase("HK", "t1")]
        [TestCase("NZ", "t1")]
        [TestCase("IE", "t1")]
        [TestCase("BE", "t1")]
        [TestCase("IT", "t1")]
        [TestCase("ES", "t1")]
        [TestCase("PT", "t1")]
        [TestCase("LU", "t1")]
        [TestCase("IS", "t1")]
        [TestCase("AE", "t1")]
        public void GetCountryTier_AdditionalT1Countries_ReturnsT1(string country, string expected)
        {
            Assert.AreEqual(expected, UserSegmentManager.GetCountryTier(country));
        }

        [Test]
        [TestCase("AR", "t2")]
        [TestCase("CO", "t2")]
        [TestCase("CL", "t2")]
        [TestCase("PE", "t2")]
        [TestCase("PL", "t2")]
        [TestCase("CZ", "t2")]
        [TestCase("HU", "t2")]
        [TestCase("RO", "t2")]
        [TestCase("SA", "t2")]
        [TestCase("ZA", "t2")]
        [TestCase("MY", "t2")]
        [TestCase("UA", "t2")]
        [TestCase("RU", "t2")]
        [TestCase("GR", "t2")]
        [TestCase("IL", "t2")]
        [TestCase("QA", "t2")]
        [TestCase("KW", "t2")]
        [TestCase("EG", "t2")]
        [TestCase("NG", "t2")]
        [TestCase("MA", "t2")]
        [TestCase("TW", "t2")]
        [TestCase("MO", "t2")]
        [TestCase("BH", "t2")]
        public void GetCountryTier_AdditionalT2Countries_ReturnsT2(string country, string expected)
        {
            Assert.AreEqual(expected, UserSegmentManager.GetCountryTier(country));
        }

        // ─── GetCountryTier — case insensitivity ──────────────────────────────

        [Test]
        [TestCase("FR", "t1")]
        [TestCase("fr", "t1")]
        [TestCase("Fr", "t1")]
        [TestCase("BR", "t2")]
        [TestCase("br", "t2")]
        [TestCase("VN", "t3")]
        [TestCase("vn", "t3")]
        public void GetCountryTier_CaseInsensitive_ReturnsCorrectTier(string country, string expected)
        {
            Assert.AreEqual(expected, UserSegmentManager.GetCountryTier(country));
        }

        // ─── GetSessionTier — exact boundary values ────────────────────────────

        [Test]
        public void GetSessionTier_Session1_ReturnsNew()
        {
            // Fresh prefs → constructor sets count to 1
            var mgr = new UserSegmentManager();

            Assert.AreEqual("new", mgr.GetSessionTier());
        }

        [Test]
        public void GetSessionTier_Session2_ReturnsNew()
        {
            PlayerPrefs.SetInt($"{PrefsPrefix}session_count", 1);
            var mgr = new UserSegmentManager(); // increments to 2

            Assert.AreEqual("new", mgr.GetSessionTier());
        }

        [Test]
        public void GetSessionTier_Session4_ReturnsReturning()
        {
            PlayerPrefs.SetInt($"{PrefsPrefix}session_count", 3);
            var mgr = new UserSegmentManager(); // increments to 4

            Assert.AreEqual("returning", mgr.GetSessionTier());
        }

        [Test]
        public void GetSessionTier_Session21_ReturnsLoyal()
        {
            PlayerPrefs.SetInt($"{PrefsPrefix}session_count", 20);
            var mgr = new UserSegmentManager(); // increments to 21

            Assert.AreEqual("loyal", mgr.GetSessionTier());
        }

        [Test]
        public void GetSessionTier_HighSessionCount_ReturnsLoyal()
        {
            PlayerPrefs.SetInt($"{PrefsPrefix}session_count", 999);
            var mgr = new UserSegmentManager(); // increments to 1000

            Assert.AreEqual("loyal", mgr.GetSessionTier());
        }

        // ─── GetInstallCohort — exact boundary values ──────────────────────────

        [Test]
        public void GetInstallCohort_ExactlyDay1_ReturnsD0D1()
        {
            long ticks = DateTime.UtcNow.AddDays(-1).Ticks;
            PlayerPrefs.SetString($"{PrefsPrefix}install_ticks", ticks.ToString());
            var mgr = new UserSegmentManager();

            Assert.AreEqual("d0d1", mgr.GetInstallCohort());
        }

        [Test]
        public void GetInstallCohort_ExactlyDay2_ReturnsD2D7()
        {
            long ticks = DateTime.UtcNow.AddDays(-2).Ticks;
            PlayerPrefs.SetString($"{PrefsPrefix}install_ticks", ticks.ToString());
            var mgr = new UserSegmentManager();

            Assert.AreEqual("d2d7", mgr.GetInstallCohort());
        }

        [Test]
        public void GetInstallCohort_ExactlyDay7_ReturnsD2D7()
        {
            long ticks = DateTime.UtcNow.AddDays(-7).Ticks;
            PlayerPrefs.SetString($"{PrefsPrefix}install_ticks", ticks.ToString());
            var mgr = new UserSegmentManager();

            Assert.AreEqual("d2d7", mgr.GetInstallCohort());
        }

        [Test]
        public void GetInstallCohort_ExactlyDay8_ReturnsD8D30()
        {
            long ticks = DateTime.UtcNow.AddDays(-8).Ticks;
            PlayerPrefs.SetString($"{PrefsPrefix}install_ticks", ticks.ToString());
            var mgr = new UserSegmentManager();

            Assert.AreEqual("d8d30", mgr.GetInstallCohort());
        }

        [Test]
        public void GetInstallCohort_ExactlyDay30_ReturnsD8D30()
        {
            long ticks = DateTime.UtcNow.AddDays(-30).Ticks;
            PlayerPrefs.SetString($"{PrefsPrefix}install_ticks", ticks.ToString());
            var mgr = new UserSegmentManager();

            Assert.AreEqual("d8d30", mgr.GetInstallCohort());
        }

        [Test]
        public void GetInstallCohort_ExactlyDay31_ReturnsD30Plus()
        {
            long ticks = DateTime.UtcNow.AddDays(-31).Ticks;
            PlayerPrefs.SetString($"{PrefsPrefix}install_ticks", ticks.ToString());
            var mgr = new UserSegmentManager();

            Assert.AreEqual("d30plus", mgr.GetInstallCohort());
        }

        [Test]
        public void GetInstallCohort_InvalidPersistedValue_ReturnsFreshD0D1()
        {
            PlayerPrefs.SetString($"{PrefsPrefix}install_ticks", "not-a-number");
            var mgr = new UserSegmentManager();

            // GetDaysSinceInstall returns 0 on parse failure → "d0d1"
            Assert.AreEqual("d0d1", mgr.GetInstallCohort());
        }

        // ─── GetPayerTier — exact boundary values ──────────────────────────────

        [Test]
        public void GetPayerTier_ZeroPurchases_ReturnsNonpayer()
        {
            var mgr = new UserSegmentManager();

            Assert.AreEqual("nonpayer", mgr.GetPayerTier());
        }

        [Test]
        public void GetPayerTier_ExactlyOnePurchase_ReturnsLowspender()
        {
            PlayerPrefs.SetInt($"{PrefsPrefix}purchase_count", 1);
            var mgr = new UserSegmentManager();

            Assert.AreEqual("lowspender", mgr.GetPayerTier());
        }

        [Test]
        public void GetPayerTier_ExactlyFourPurchases_ReturnsLowspender()
        {
            PlayerPrefs.SetInt($"{PrefsPrefix}purchase_count", 4);
            var mgr = new UserSegmentManager();

            Assert.AreEqual("lowspender", mgr.GetPayerTier());
        }

        [Test]
        public void GetPayerTier_ExactlyFivePurchases_ReturnsHighspender()
        {
            PlayerPrefs.SetInt($"{PrefsPrefix}purchase_count", 5);
            var mgr = new UserSegmentManager();

            Assert.AreEqual("highspender", mgr.GetPayerTier());
        }

        [Test]
        public void GetPayerTier_HighPurchaseCount_ReturnsHighspender()
        {
            PlayerPrefs.SetInt($"{PrefsPrefix}purchase_count", 1000);
            var mgr = new UserSegmentManager();

            Assert.AreEqual("highspender", mgr.GetPayerTier());
        }

        // ─── RecordPurchase — boundary at 4 → 5 ──────────────────────────────

        [Test]
        public void RecordPurchase_FourthPurchase_StillLowspender()
        {
            PlayerPrefs.SetInt($"{PrefsPrefix}purchase_count", 3);
            var mgr = new UserSegmentManager();
            mgr.RecordPurchase(); // 3 → 4

            Assert.AreEqual("lowspender", mgr.GetPayerTier());
        }

        [Test]
        public void RecordPurchase_FifthPurchase_TransitionsToHighspender()
        {
            PlayerPrefs.SetInt($"{PrefsPrefix}purchase_count", 4);
            var mgr = new UserSegmentManager();
            mgr.RecordPurchase(); // 4 → 5

            Assert.AreEqual("highspender", mgr.GetPayerTier());
        }

        [Test]
        public void RecordPurchase_PersistsAcrossNewInstance()
        {
            var mgr1 = new UserSegmentManager();
            mgr1.RecordPurchase();
            mgr1.RecordPurchase();
            mgr1.RecordPurchase();

            // New instance reads from PlayerPrefs
            var mgr2 = new UserSegmentManager();

            Assert.AreEqual("lowspender", mgr2.GetPayerTier());
        }

        // ─── GetCompositeSegment — format and dimension combinations ──────────

        [Test]
        public void GetCompositeSegment_HasFourUnderscore_DelimitedParts()
        {
            var mgr = new UserSegmentManager();
            string segment = mgr.GetCompositeSegment("US");
            string[] parts = segment.Split('_');

            // Format: {tier}_{payer}_{session}_{cohort} — 4 parts
            Assert.AreEqual(4, parts.Length, $"Composite segment should have 4 underscore-separated parts: '{segment}'");
        }

        [Test]
        public void GetCompositeSegment_T2Country_StartsWithT2()
        {
            var mgr = new UserSegmentManager();
            string segment = mgr.GetCompositeSegment("BR");

            StringAssert.StartsWith("t2_", segment);
        }

        [Test]
        public void GetCompositeSegment_T3Country_StartsWithT3()
        {
            var mgr = new UserSegmentManager();
            string segment = mgr.GetCompositeSegment("VN");

            StringAssert.StartsWith("t3_", segment);
        }

        [Test]
        public void GetCompositeSegment_WithHighspender_ContainsHighspender()
        {
            PlayerPrefs.SetInt($"{PrefsPrefix}purchase_count", 5);
            var mgr = new UserSegmentManager();
            string segment = mgr.GetCompositeSegment("US");

            StringAssert.Contains("highspender", segment);
        }

        [Test]
        public void GetCompositeSegment_WithLowspender_ContainsLowspender()
        {
            PlayerPrefs.SetInt($"{PrefsPrefix}purchase_count", 2);
            var mgr = new UserSegmentManager();
            string segment = mgr.GetCompositeSegment("US");

            StringAssert.Contains("lowspender", segment);
        }

        [Test]
        public void GetCompositeSegment_OldInstall_ContainsD30Plus()
        {
            long ticks = DateTime.UtcNow.AddDays(-60).Ticks;
            PlayerPrefs.SetString($"{PrefsPrefix}install_ticks", ticks.ToString());
            var mgr = new UserSegmentManager();
            string segment = mgr.GetCompositeSegment("US");

            StringAssert.Contains("d30plus", segment);
        }

        [Test]
        public void GetCompositeSegment_LoyalUser_ContainsLoyal()
        {
            PlayerPrefs.SetInt($"{PrefsPrefix}session_count", 21);
            var mgr = new UserSegmentManager(); // increments to 22

            string segment = mgr.GetCompositeSegment("JP");

            StringAssert.Contains("loyal", segment);
        }

        [Test]
        public void GetCompositeSegment_AllDimensionValues_AreFromExpectedSet()
        {
            var validTiers    = new[] { "t1", "t2", "t3" };
            var validPayers   = new[] { "nonpayer", "lowspender", "highspender" };
            var validSessions = new[] { "new", "returning", "loyal" };
            var validCohorts  = new[] { "d0d1", "d2d7", "d8d30", "d30plus" };

            var mgr = new UserSegmentManager();
            string segment = mgr.GetCompositeSegment("US");
            string[] parts = segment.Split('_', 4);

            Assert.AreEqual(4, parts.Length);
            CollectionAssert.Contains(validTiers,    parts[0], $"Unexpected tier: '{parts[0]}'");
            CollectionAssert.Contains(validPayers,   parts[1], $"Unexpected payer: '{parts[1]}'");
            CollectionAssert.Contains(validSessions, parts[2], $"Unexpected session: '{parts[2]}'");
            CollectionAssert.Contains(validCohorts,  parts[3], $"Unexpected cohort: '{parts[3]}'");
        }

        // ─── GetCountryTierKey — same as GetCountryTier ───────────────────────

        [Test]
        public void GetCountryTierKey_DelegatesToGetCountryTier()
        {
            var mgr = new UserSegmentManager();

            Assert.AreEqual(UserSegmentManager.GetCountryTier("US"), mgr.GetCountryTierKey("US"));
            Assert.AreEqual(UserSegmentManager.GetCountryTier("BR"), mgr.GetCountryTierKey("BR"));
            Assert.AreEqual(UserSegmentManager.GetCountryTier("VN"), mgr.GetCountryTierKey("VN"));
            Assert.AreEqual(UserSegmentManager.GetCountryTier(null), mgr.GetCountryTierKey(null));
        }

        // ─── SessionCount increment per construction ──────────────────────────

        [Test]
        public void Constructor_AlwaysIncrementsSessionCount()
        {
            var mgr1 = new UserSegmentManager();
            int count1 = PlayerPrefs.GetInt($"{PrefsPrefix}session_count", 0);

            var mgr2 = new UserSegmentManager();
            int count2 = PlayerPrefs.GetInt($"{PrefsPrefix}session_count", 0);

            Assert.AreEqual(count1 + 1, count2, "Each UserSegmentManager construction must increment session_count by 1");
        }
    }
}
