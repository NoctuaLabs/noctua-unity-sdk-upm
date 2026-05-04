using NUnit.Framework;

namespace com.noctuagames.sdk.Tests.IAA
{
    /// <summary>
    /// Exhaustive 16-case matrix covering <see cref="MediationManager.ResolveMediationSelection"/>
    /// across every combination of:
    ///
    ///   - primary  ∈ { applovin, admob }
    ///   - secondary ∈ { applovin, admob }
    ///   - applovinEnabled (UNITY_APPLOVIN compiled in) ∈ { true, false }
    ///   - admobEnabled    (UNITY_ADMOB    compiled in) ∈ { true, false }
    ///
    /// Why this lives in a pure unit test, not an end-to-end CreateNetworks
    /// test: the SDK availability flags are compile-time defines, so they
    /// can't be toggled per test case at runtime. ResolveMediationSelection
    /// takes them as parameters precisely so this matrix can be exercised
    /// in a single build.
    ///
    /// Edge-case categories highlighted by the matrix:
    ///   #3 / #6              — primary SDK missing, must promote secondary
    ///   #2 / #7              — secondary SDK missing, must continue single-network
    ///   #4 / #8 / #12 / #16  — neither SDK available, must return (null, null)
    ///   #9–#11 / #13–#15     — same network in both slots, must dedup
    /// </summary>
    [TestFixture]
    public class MediationSelectionMatrixTest
    {
        // Argument order matches the user-facing matrix:
        //   primary, secondary, applovinEnabled, admobEnabled, expectedPrimary, expectedSecondary
        // expectedPrimary / expectedSecondary use null to mean "no network selected".

        // #1 — both networks distinct, both SDKs available → hybrid
        [TestCase("applovin", "admob",    true,  true,  "applovin", "admob",    TestName = "01_applovin_admob_both_sdks")]

        // #2 — primary available, secondary SDK missing → single-network primary
        [TestCase("applovin", "admob",    true,  false, "applovin", null,       TestName = "02_applovin_admob_admobMissing_secondaryDropped")]

        // #3 — primary SDK missing, secondary available → promote secondary
        [TestCase("applovin", "admob",    false, true,  "admob",    null,       TestName = "03_applovin_admob_applovinMissing_promoted")]

        // #4 — neither SDK → no selection
        [TestCase("applovin", "admob",    false, false, null,       null,       TestName = "04_applovin_admob_noSDK")]

        // #5 — distinct, both SDKs (mirror of #1)
        [TestCase("admob",    "applovin", true,  true,  "admob",    "applovin", TestName = "05_admob_applovin_both_sdks")]

        // #6 — primary SDK missing, secondary available → promote secondary
        [TestCase("admob",    "applovin", true,  false, "applovin", null,       TestName = "06_admob_applovin_admobMissing_promoted")]

        // #7 — primary available, secondary SDK missing → single-network primary
        [TestCase("admob",    "applovin", false, true,  "admob",    null,       TestName = "07_admob_applovin_applovinMissing_secondaryDropped")]

        // #8 — neither SDK
        [TestCase("admob",    "applovin", false, false, null,       null,       TestName = "08_admob_applovin_noSDK")]

        // #9 — duplicate name, primary's SDK available → dedup, single-network
        [TestCase("applovin", "applovin", true,  true,  "applovin", null,       TestName = "09_applovin_applovin_dedup_both_sdks")]

        // #10 — duplicate name, AppLovin only → dedup, single-network
        [TestCase("applovin", "applovin", true,  false, "applovin", null,       TestName = "10_applovin_applovin_dedup_applovinOnly")]

        // #11 — duplicate name, AppLovin SDK missing → no fill (no AdMob fallback for "applovin")
        [TestCase("applovin", "applovin", false, true,  null,       null,       TestName = "11_applovin_applovin_dedup_applovinMissing_noFill")]

        // #12 — duplicate name, neither SDK
        [TestCase("applovin", "applovin", false, false, null,       null,       TestName = "12_applovin_applovin_dedup_noSDK")]

        // #13 — duplicate name, primary's SDK available → dedup, single-network
        [TestCase("admob",    "admob",    true,  true,  "admob",    null,       TestName = "13_admob_admob_dedup_both_sdks")]

        // #14 — duplicate name, AdMob SDK missing → no fill
        [TestCase("admob",    "admob",    true,  false, null,       null,       TestName = "14_admob_admob_dedup_admobMissing_noFill")]

        // #15 — duplicate name, AdMob only → dedup, single-network
        [TestCase("admob",    "admob",    false, true,  "admob",    null,       TestName = "15_admob_admob_dedup_admobOnly")]

        // #16 — duplicate name, neither SDK
        [TestCase("admob",    "admob",    false, false, null,       null,       TestName = "16_admob_admob_dedup_noSDK")]

        public void ResolveMediationSelection_Matrix(
            string primary,
            string secondary,
            bool   applovinEnabled,
            bool   admobEnabled,
            string expectedPrimary,
            string expectedSecondary)
        {
            var (resolvedPrimary, resolvedSecondary) =
                MediationManager.ResolveMediationSelection(
                    primaryRaw:        primary,
                    secondaryRaw:      secondary,
                    admobAvailable:    admobEnabled,
                    applovinAvailable: applovinEnabled);

            Assert.AreEqual(expectedPrimary,   resolvedPrimary,
                $"primary mismatch for ({primary}, {secondary}, applovin={applovinEnabled}, admob={admobEnabled})");
            Assert.AreEqual(expectedSecondary, resolvedSecondary,
                $"secondary mismatch for ({primary}, {secondary}, applovin={applovinEnabled}, admob={admobEnabled})");
        }

        // ─── Robustness cases ──────────────────────────────────────────────────
        // Adjacent to the matrix: the resolver must also handle dirty input
        // (whitespace, mixed case, unknown names, null) without crashing.

        [Test]
        public void ResolveMediationSelection_MixedCaseAndWhitespace_NormalisedAndDeduped()
        {
            // "AdMob" and " admob " should normalise to the same name and dedup.
            var (p, s) = MediationManager.ResolveMediationSelection(
                primaryRaw: "AdMob",
                secondaryRaw: " admob ",
                admobAvailable: true,
                applovinAvailable: true);

            Assert.AreEqual("admob", p);
            Assert.IsNull(s, "Mixed-case duplicate must dedup just like exact-case duplicate");
        }

        [Test]
        public void ResolveMediationSelection_UnknownName_TreatedAsUnavailable()
        {
            // Typo / unsupported name resolves to null (no crash); secondary promoted if able.
            var (p, s) = MediationManager.ResolveMediationSelection(
                primaryRaw: "applovin_max",
                secondaryRaw: "admob",
                admobAvailable: true,
                applovinAvailable: true);

            Assert.AreEqual("admob", p, "Unknown primary should let secondary be promoted");
            Assert.IsNull(s);
        }

        [Test]
        public void ResolveMediationSelection_NullInputs_ReturnsNoSelection()
        {
            var (p, s) = MediationManager.ResolveMediationSelection(
                primaryRaw: null,
                secondaryRaw: null,
                admobAvailable: true,
                applovinAvailable: true);

            Assert.IsNull(p);
            Assert.IsNull(s);
        }
    }
}
