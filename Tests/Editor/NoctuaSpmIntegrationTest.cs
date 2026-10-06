using com.noctuagames.sdk.Editor.Build;
using NUnit.Framework;

namespace com.noctuagames.sdk.Editor.Tests
{
    public class NoctuaSpmIntegrationTest
    {
        private const string Podfile =
            "source 'https://cdn.cocoapods.org/'\n" +
            "platform :ios, '15.0'\n\n" +
            "target 'UnityFramework' do\n" +
            "  pod 'AppLovinSDK', '13.6.1'\n" +
            "  pod 'NoctuaSDK', '0.40.1'\n" +
            "  pod 'NoctuaSDK/Adjust', '0.40.1'\n" +
            "  pod \"NoctuaSDK/FirebaseAnalytics\", '0.40.1'\n" +
            "  pod 'Google-Mobile-Ads-SDK', '~> 13.2.0'\n" +
            "end\n";

        private const string CleanLock =
            "PODS:\n" +
            "  - AppLovinSDK (13.6.1)\n" +
            "  - FBAudienceNetwork (6.21.0)\n" +
            "  - Google-Mobile-Ads-SDK (13.2.0):\n" +
            "    - GoogleUserMessagingPlatform (>= 1.1)\n" +
            "  - GoogleUserMessagingPlatform (3.1.0)\n" +
            "\nDEPENDENCIES:\n" +
            "  - FirebaseCore (from somewhere)\n";

        private const string ClashingLock =
            "PODS:\n" +
            "  - Adjust/Adjust (5.6.2):\n" +
            "    - AdjustSignature (= 3.67.0)\n" +
            "  - FirebaseCore (12.2.0):\n" +
            "    - GoogleUtilities/Logger (~> 8.1)\n" +
            "  - FBSDKCoreKit (18.0.0)\n" +
            "  - GoogleUtilities/Logger (8.1.0)\n" +
            "  - \"nanopb/decode (3.30910.0)\"\n" +
            "  - NoctuaSDK/Core (0.40.1)\n" +
            "  - UnityAds (4.12.0)\n" +
            "\nDEPENDENCIES:\n";

        [Test]
        public void StripNoctuaPods_RemovesOnlyNoctuaLines()
        {
            var result = NoctuaSpmIntegration.StripNoctuaPods(Podfile);

            StringAssert.DoesNotContain("NoctuaSDK", result);
            StringAssert.Contains("pod 'AppLovinSDK', '13.6.1'\n", result);
            StringAssert.Contains("pod 'Google-Mobile-Ads-SDK', '~> 13.2.0'\n", result);
            StringAssert.Contains("target 'UnityFramework' do\n", result);
            StringAssert.EndsWith("end\n", result);
        }

        [Test]
        public void StripNoctuaPods_LeavesPodfileWithoutNoctuaUnchanged()
        {
            const string podfile = "target 'UnityFramework' do\n  pod 'AppLovinSDK', '13.6.1'\nend\n";
            Assert.AreEqual(podfile, NoctuaSpmIntegration.StripNoctuaPods(podfile));
        }

        [Test]
        public void FindSpmProvidedPods_CleanLock_ReturnsNothing()
        {
            // FBAudienceNetwork is an ad SDK, not Facebook Core; DEPENDENCIES entries are ignored.
            CollectionAssert.IsEmpty(NoctuaSpmIntegration.FindSpmProvidedPods(CleanLock));
        }

        [Test]
        public void FindSpmProvidedPods_ClashingLock_ReturnsRootPodNames()
        {
            CollectionAssert.AreEqual(
                new[] { "Adjust", "FBSDKCoreKit", "FirebaseCore", "GoogleUtilities", "NoctuaSDK", "nanopb" },
                NoctuaSpmIntegration.FindSpmProvidedPods(ClashingLock));
        }

        [Test]
        public void FindSpmProvidedPods_EmptyOrMalformed_ReturnsNothing()
        {
            CollectionAssert.IsEmpty(NoctuaSpmIntegration.FindSpmProvidedPods(null));
            CollectionAssert.IsEmpty(NoctuaSpmIntegration.FindSpmProvidedPods("not a lockfile"));
        }

        [TestCase(null, null, false)]
        [TestCase(null, "{\"iosDependencyManager\":\"spm\"}", true)]
        [TestCase(null, "{\"iosDependencyManager\":\"cocoapods\"}", false)]
        [TestCase("SPM", "{\"iosDependencyManager\":\"cocoapods\"}", true)]
        [TestCase("cocoapods", "{\"iosDependencyManager\":\"spm\"}", false)]
        [TestCase("bogus", "{\"iosDependencyManager\":\"spm\"}", true)]
        [TestCase(null, "{\"iosDependencyManager\":\"carthage\"}", false)]
        [TestCase(null, "{ not json", false)]
        public void ResolveSpm_EnvOverridesFile_UnknownMeansCocoaPods(string env, string json, bool expected)
        {
            Assert.AreEqual(expected, NoctuaSpmIntegration.ResolveSpm(env, json));
        }
    }
}
