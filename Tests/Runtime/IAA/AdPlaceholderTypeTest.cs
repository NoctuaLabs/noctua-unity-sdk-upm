using com.noctuagames.sdk.AdPlaceholder;
using NUnit.Framework;

namespace Tests.Runtime.IAA
{
    /// <summary>
    /// EditMode NUnit tests for <see cref="AdPlaceholderType"/>.
    ///
    /// Verifies the enum ordinal contract between the Unity SDK and the native ad-placeholder
    /// asset pipeline. These ordinal values are used as dictionary keys and switch indices;
    /// they must not drift as new values are added.
    /// </summary>
    [TestFixture]
    public class AdPlaceholderTypeTest
    {
        [Test]
        public void Interstitial_OrdinalIsZero() =>
            Assert.AreEqual(0, (int)AdPlaceholderType.Interstitial,
                "AdPlaceholderType.Interstitial must have ordinal 0");

        [Test]
        public void Rewarded_OrdinalIsOne() =>
            Assert.AreEqual(1, (int)AdPlaceholderType.Rewarded,
                "AdPlaceholderType.Rewarded must have ordinal 1");

        [Test]
        public void RewardedInterstitial_OrdinalIsTwo() =>
            Assert.AreEqual(2, (int)AdPlaceholderType.RewardedInterstitial,
                "AdPlaceholderType.RewardedInterstitial must have ordinal 2");

        [Test]
        public void Banner_OrdinalIsThree() =>
            Assert.AreEqual(3, (int)AdPlaceholderType.Banner,
                "AdPlaceholderType.Banner must have ordinal 3");

        [Test]
        public void EnumHasExactlyFourValues()
        {
            var values = System.Enum.GetValues(typeof(AdPlaceholderType));
            Assert.AreEqual(4, values.Length,
                "AdPlaceholderType must have exactly 4 values — adding new values requires a native ABI review");
        }
    }
}
