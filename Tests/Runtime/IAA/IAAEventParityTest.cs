using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace com.noctuagames.sdk.Tests.IAA
{
    /// <summary>
    /// Drift-prevention parity test for canonical IAA event names.
    /// Asserts that every mediation source file (AppLovin + AdMob) references the
    /// canonical event-name constants from <see cref="IAAEventNames"/>.
    ///
    /// This is a static, source-level check — it does not exercise mediation runtime
    /// (which would require live SDKs). It exists to catch the kind of silent
    /// drift the audit flagged: e.g. AdMob never emitting <c>ad_load_failed</c>, or
    /// AppLovin emitting the wrong name <c>ad_shown_failed</c> for banner load failures.
    ///
    /// If a future change deletes one of these emissions, this test fails loudly.
    /// </summary>
    [TestFixture]
    public class IAAEventParityTest
    {
        // Event constants every full-screen mediation file must reference.
        private static readonly string[] FullScreenRequiredConstants =
        {
            nameof(IAAEventNames.AdImpression),
            nameof(IAAEventNames.AdLoaded),
            nameof(IAAEventNames.AdLoadFailed),
            nameof(IAAEventNames.AdShowFailed),
            nameof(IAAEventNames.AdClicked),
            nameof(IAAEventNames.AdShown),
        };

        // Banner has no separate "show failed" callback — load-failure is the only failure path.
        // Banner also emits expand/collapse on full-screen open/close.
        private static readonly string[] BannerRequiredConstants =
        {
            nameof(IAAEventNames.AdImpression),
            nameof(IAAEventNames.AdLoaded),
            nameof(IAAEventNames.AdLoadFailed),
            nameof(IAAEventNames.AdClicked),
            nameof(IAAEventNames.AdShown),
            nameof(IAAEventNames.AdCollapsed),
            nameof(IAAEventNames.AdExpanded),
        };

        // Relative paths from the package root.
        private const string PackageRel = "Packages/com.noctuagames.sdk/Runtime/AdsManager";

        // ─── Per-file parity ─────────────────────────────────────────────────

        [Test] public void AppLovin_Banner_EmitsAllCanonicalEventsForBanner()
            => AssertFileReferences("AppLovin/BannerAppLovin.cs", BannerRequiredConstants);

        [Test] public void AppLovin_Interstitial_EmitsAllCanonicalEventsForFullScreen()
            => AssertFileReferences("AppLovin/InterstitialAppLovin.cs", FullScreenRequiredConstants);

        [Test] public void AppLovin_Rewarded_EmitsAllCanonicalEventsForFullScreen()
            => AssertFileReferences("AppLovin/RewardedAppLovin.cs", FullScreenRequiredConstants);

        [Test] public void AppLovin_AppOpen_EmitsAllCanonicalEventsForFullScreen()
            => AssertFileReferences("AppLovin/AppOpenAppLovin.cs", FullScreenRequiredConstants);

        [Test] public void Admob_Banner_EmitsAllCanonicalEventsForBanner()
            => AssertFileReferences("Admob/BannerAdmob.cs", BannerRequiredConstants);

        [Test] public void Admob_Interstitial_EmitsAllCanonicalEventsForFullScreen()
            => AssertFileReferences("Admob/InterstitialAdmob.cs", FullScreenRequiredConstants);

        [Test] public void Admob_Rewarded_EmitsAllCanonicalEventsForFullScreen()
            => AssertFileReferences("Admob/RewardedAdmob.cs", FullScreenRequiredConstants);

        [Test] public void Admob_RewardedInterstitial_EmitsAllCanonicalEventsForFullScreen()
            => AssertFileReferences("Admob/RewardedInterstitialAdmob.cs", FullScreenRequiredConstants);

        [Test] public void Admob_AppOpen_EmitsAllCanonicalEventsForFullScreen()
            => AssertFileReferences("Admob/AppOpenAdmob.cs", FullScreenRequiredConstants);

        // ─── Watch-milestone tracker is wired to canonical names ─────────────

        [Test]
        public void AdWatchMilestoneTracker_ReferencesAllFourMilestoneConstants()
        {
            AssertFileReferences("AdWatchMilestoneTracker.cs", new[]
            {
                nameof(IAAEventNames.WatchAds5x),
                nameof(IAAEventNames.WatchAds10x),
                nameof(IAAEventNames.WatchAds25x),
                nameof(IAAEventNames.WatchAds50x),
            });
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        private static void AssertFileReferences(string relativePath, IEnumerable<string> requiredConstants)
        {
            var fullPath = ResolvePackagePath(relativePath);
            Assert.IsTrue(File.Exists(fullPath), $"Source file not found: {fullPath}");

            var source = File.ReadAllText(fullPath);
            var missing = new List<string>();
            foreach (var c in requiredConstants)
            {
                // We require the canonical reference style — `IAAEventNames.<Name>` —
                // not the raw string literal. This keeps drift detection meaningful:
                // a literal `"ad_loaded"` slipping in would pass a literal-grep check
                // but defeat the purpose of having a single source of truth.
                if (!source.Contains("IAAEventNames." + c))
                {
                    missing.Add(c);
                }
            }

            Assert.IsEmpty(missing,
                $"{relativePath} is missing canonical event references: {string.Join(", ", missing)}. " +
                $"Every mediation must emit via IAAEventNames.<Name> — no raw string literals.");
        }

        /// <summary>
        /// Resolve a path inside the SDK package. Handles two layouts:
        /// (1) sample-app project where the package is consumed as a submodule at
        ///     <c>Packages/com.noctuagames.sdk/...</c>; (2) standalone package CI
        ///     where the package itself is the project root.
        /// </summary>
        private static string ResolvePackagePath(string relativeFromAdsManager)
        {
            // Application.dataPath = <project>/Assets — go up to project root.
            var projectRoot = Path.GetDirectoryName(Application.dataPath) ?? "";

            var candidate1 = Path.Combine(projectRoot, PackageRel, relativeFromAdsManager);
            if (File.Exists(candidate1)) return candidate1;

            // Standalone: project root IS the package.
            var candidate2 = Path.Combine(projectRoot, "Runtime/AdsManager", relativeFromAdsManager);
            if (File.Exists(candidate2)) return candidate2;

            // PackageCache path (immutable UPM install).
            var candidate3 = Path.Combine(projectRoot, "Library/PackageCache");
            if (Directory.Exists(candidate3))
            {
                foreach (var dir in Directory.EnumerateDirectories(candidate3, "com.noctuagames.sdk*"))
                {
                    var p = Path.Combine(dir, "Runtime/AdsManager", relativeFromAdsManager);
                    if (File.Exists(p)) return p;
                }
            }

            return candidate1; // fall through — caller asserts existence with a clear failure message.
        }
    }
}
