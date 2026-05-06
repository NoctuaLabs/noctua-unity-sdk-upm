using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime.IAA
{
    /// <summary>
    /// Unit tests for ad-format and ad-network constant classes, and <see cref="AdTestUnitIds"/>:
    ///   * <see cref="AdFormatKey"/>    — string constants for ad format routing keys
    ///   * <see cref="AdNetworkName"/>  — string constants for network name identifiers
    ///   * <see cref="AdTestUnitIds"/>  — GetTestAdUnitId for Android / iOS per format
    /// </summary>
    [TestFixture]
    public class AdConstantsTest
    {
        // ─── AdFormatKey constants ────────────────────────────────────────────

        [Test]
        public void AdFormatKey_Interstitial_IsCorrect()
        {
            Assert.AreEqual("interstitial", AdFormatKey.Interstitial);
        }

        [Test]
        public void AdFormatKey_Rewarded_IsCorrect()
        {
            Assert.AreEqual("rewarded", AdFormatKey.Rewarded);
        }

        [Test]
        public void AdFormatKey_RewardedInterstitial_IsCorrect()
        {
            Assert.AreEqual("rewarded_interstitial", AdFormatKey.RewardedInterstitial);
        }

        [Test]
        public void AdFormatKey_Banner_IsCorrect()
        {
            Assert.AreEqual("banner", AdFormatKey.Banner);
        }

        [Test]
        public void AdFormatKey_AppOpen_IsCorrect()
        {
            Assert.AreEqual("app_open", AdFormatKey.AppOpen);
        }

        // ─── AdNetworkName constants ──────────────────────────────────────────

        [Test]
        public void AdNetworkName_Admob_IsCorrect()
        {
            Assert.AreEqual("admob", AdNetworkName.Admob);
        }

        [Test]
        public void AdNetworkName_AppLovin_IsCorrect()
        {
            Assert.AreEqual("applovin", AdNetworkName.AppLovin);
        }

        // ─── AdTestUnitIds.GetTestAdUnitId — Android ──────────────────────────

        [Test]
        public void GetTestAdUnitId_Android_Banner_ReturnsAndroidId()
        {
            var id = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.Banner, RuntimePlatform.Android);
            Assert.IsNotNull(id);
            Assert.IsNotEmpty(id);
            // Starts with the AdMob test publisher prefix
            StringAssert.StartsWith("ca-app-pub-3940256099942544", id);
        }

        [Test]
        public void GetTestAdUnitId_Android_Interstitial_ReturnsAndroidId()
        {
            var id = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.Interstitial, RuntimePlatform.Android);
            Assert.IsNotNull(id);
            StringAssert.StartsWith("ca-app-pub-3940256099942544", id);
        }

        [Test]
        public void GetTestAdUnitId_Android_Rewarded_ReturnsAndroidId()
        {
            var id = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.Rewarded, RuntimePlatform.Android);
            Assert.IsNotNull(id);
            StringAssert.StartsWith("ca-app-pub-3940256099942544", id);
        }

        [Test]
        public void GetTestAdUnitId_Android_RewardedInterstitial_ReturnsAndroidId()
        {
            var id = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.RewardedInterstitial, RuntimePlatform.Android);
            Assert.IsNotNull(id);
            StringAssert.StartsWith("ca-app-pub-3940256099942544", id);
        }

        [Test]
        public void GetTestAdUnitId_Android_AppOpen_ReturnsAndroidId()
        {
            var id = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.AppOpen, RuntimePlatform.Android);
            Assert.IsNotNull(id);
            StringAssert.StartsWith("ca-app-pub-3940256099942544", id);
        }

        [Test]
        public void GetTestAdUnitId_Android_Native_ReturnsAndroidId()
        {
            var id = AdTestUnitIds.GetTestAdUnitId("native", RuntimePlatform.Android);
            Assert.IsNotNull(id);
            StringAssert.StartsWith("ca-app-pub-3940256099942544", id);
        }

        // ─── AdTestUnitIds.GetTestAdUnitId — iOS ─────────────────────────────

        [Test]
        public void GetTestAdUnitId_Ios_Banner_ReturnsIosId()
        {
            var id = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.Banner, RuntimePlatform.IPhonePlayer);
            Assert.IsNotNull(id);
            StringAssert.StartsWith("ca-app-pub-3940256099942544", id);
        }

        [Test]
        public void GetTestAdUnitId_Ios_Interstitial_ReturnsIosId()
        {
            var id = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.Interstitial, RuntimePlatform.IPhonePlayer);
            Assert.IsNotNull(id);
            StringAssert.StartsWith("ca-app-pub-3940256099942544", id);
        }

        [Test]
        public void GetTestAdUnitId_Ios_Rewarded_ReturnsIosId()
        {
            var id = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.Rewarded, RuntimePlatform.IPhonePlayer);
            Assert.IsNotNull(id);
            StringAssert.StartsWith("ca-app-pub-3940256099942544", id);
        }

        [Test]
        public void GetTestAdUnitId_Ios_RewardedInterstitial_ReturnsIosId()
        {
            var id = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.RewardedInterstitial, RuntimePlatform.IPhonePlayer);
            Assert.IsNotNull(id);
            StringAssert.StartsWith("ca-app-pub-3940256099942544", id);
        }

        [Test]
        public void GetTestAdUnitId_Ios_AppOpen_ReturnsIosId()
        {
            var id = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.AppOpen, RuntimePlatform.IPhonePlayer);
            Assert.IsNotNull(id);
            StringAssert.StartsWith("ca-app-pub-3940256099942544", id);
        }

        [Test]
        public void GetTestAdUnitId_Ios_Native_ReturnsIosId()
        {
            var id = AdTestUnitIds.GetTestAdUnitId("native", RuntimePlatform.IPhonePlayer);
            Assert.IsNotNull(id);
            StringAssert.StartsWith("ca-app-pub-3940256099942544", id);
        }

        // ─── AdTestUnitIds — Android and iOS IDs must differ per format ───────

        [Test]
        public void GetTestAdUnitId_Banner_AndroidAndIos_AreDifferent()
        {
            var android = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.Banner, RuntimePlatform.Android);
            var ios     = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.Banner, RuntimePlatform.IPhonePlayer);
            Assert.AreNotEqual(android, ios, "Android and iOS test IDs must be different for banner");
        }

        [Test]
        public void GetTestAdUnitId_Interstitial_AndroidAndIos_AreDifferent()
        {
            var android = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.Interstitial, RuntimePlatform.Android);
            var ios     = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.Interstitial, RuntimePlatform.IPhonePlayer);
            Assert.AreNotEqual(android, ios);
        }

        [Test]
        public void GetTestAdUnitId_Rewarded_AndroidAndIos_AreDifferent()
        {
            var android = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.Rewarded, RuntimePlatform.Android);
            var ios     = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.Rewarded, RuntimePlatform.IPhonePlayer);
            Assert.AreNotEqual(android, ios);
        }

        // ─── AdTestUnitIds — unknown format returns null ──────────────────────

        [Test]
        public void GetTestAdUnitId_UnknownFormat_Android_ReturnsNull()
        {
            var id = AdTestUnitIds.GetTestAdUnitId("unknown_format_xyz", RuntimePlatform.Android);
            Assert.IsNull(id, "Unknown format should return null on Android");
        }

        [Test]
        public void GetTestAdUnitId_UnknownFormat_Ios_ReturnsNull()
        {
            var id = AdTestUnitIds.GetTestAdUnitId("unknown_format_xyz", RuntimePlatform.IPhonePlayer);
            Assert.IsNull(id, "Unknown format should return null on iOS");
        }

        [Test]
        public void GetTestAdUnitId_NullFormat_ReturnsNull()
        {
            var id = AdTestUnitIds.GetTestAdUnitId(null, RuntimePlatform.Android);
            Assert.IsNull(id, "Null format should return null");
        }

        [Test]
        public void GetTestAdUnitId_EmptyFormat_ReturnsNull()
        {
            var id = AdTestUnitIds.GetTestAdUnitId("", RuntimePlatform.Android);
            Assert.IsNull(id, "Empty format should return null");
        }

        // ─── Non-Android platform treated as iOS ──────────────────────────────

        [Test]
        public void GetTestAdUnitId_NonAndroidPlatform_ReturnsSameAsIos()
        {
            // All non-Android platforms fall through to the iOS branch (isAndroid == false)
            var editor = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.Banner, RuntimePlatform.OSXEditor);
            var ios    = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.Banner, RuntimePlatform.IPhonePlayer);
            Assert.AreEqual(ios, editor,
                "Non-Android platforms should return the iOS test unit ID (isAndroid=false path)");
        }
    }
}
