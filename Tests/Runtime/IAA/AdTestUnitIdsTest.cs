using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime.IAA
{
    /// <summary>
    /// EditMode NUnit tests for <see cref="AdTestUnitIds"/>.
    ///
    /// Covers <c>GetTestAdUnitId</c> for every supported format on both
    /// Android and iOS, plus the unknown-format null guard.
    ///
    /// Also covers enum ordinals for <see cref="NoctuaProductType"/> and
    /// <see cref="NoctuaConsumableType"/> (native ABI contract — must not drift).
    /// </summary>
    [TestFixture]
    public class AdTestUnitIdsTest
    {
        // ═══════════════════════════════════════════════════════════════════
        // Android format routing
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void GetTestAdUnitId_Android_Banner_ReturnsNonNull()
        {
            var id = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.Banner, RuntimePlatform.Android);
            Assert.IsNotNull(id);
            StringAssert.StartsWith("ca-app-pub-", id);
        }

        [Test]
        public void GetTestAdUnitId_Android_Interstitial_ReturnsNonNull()
        {
            var id = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.Interstitial, RuntimePlatform.Android);
            Assert.IsNotNull(id);
            StringAssert.StartsWith("ca-app-pub-", id);
        }

        [Test]
        public void GetTestAdUnitId_Android_Rewarded_ReturnsNonNull()
        {
            var id = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.Rewarded, RuntimePlatform.Android);
            Assert.IsNotNull(id);
            StringAssert.StartsWith("ca-app-pub-", id);
        }

        [Test]
        public void GetTestAdUnitId_Android_RewardedInterstitial_ReturnsNonNull()
        {
            var id = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.RewardedInterstitial, RuntimePlatform.Android);
            Assert.IsNotNull(id);
            StringAssert.StartsWith("ca-app-pub-", id);
        }

        [Test]
        public void GetTestAdUnitId_Android_AppOpen_ReturnsNonNull()
        {
            var id = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.AppOpen, RuntimePlatform.Android);
            Assert.IsNotNull(id);
            StringAssert.StartsWith("ca-app-pub-", id);
        }

        [Test]
        public void GetTestAdUnitId_Android_Native_ReturnsNonNull()
        {
            var id = AdTestUnitIds.GetTestAdUnitId("native", RuntimePlatform.Android);
            Assert.IsNotNull(id);
            StringAssert.StartsWith("ca-app-pub-", id);
        }

        // ═══════════════════════════════════════════════════════════════════
        // iOS format routing
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void GetTestAdUnitId_iOS_Banner_ReturnsNonNull()
        {
            var id = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.Banner, RuntimePlatform.IPhonePlayer);
            Assert.IsNotNull(id);
            StringAssert.StartsWith("ca-app-pub-", id);
        }

        [Test]
        public void GetTestAdUnitId_iOS_Interstitial_ReturnsNonNull()
        {
            var id = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.Interstitial, RuntimePlatform.IPhonePlayer);
            Assert.IsNotNull(id);
            StringAssert.StartsWith("ca-app-pub-", id);
        }

        [Test]
        public void GetTestAdUnitId_iOS_Rewarded_ReturnsNonNull()
        {
            var id = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.Rewarded, RuntimePlatform.IPhonePlayer);
            Assert.IsNotNull(id);
            StringAssert.StartsWith("ca-app-pub-", id);
        }

        [Test]
        public void GetTestAdUnitId_iOS_RewardedInterstitial_ReturnsNonNull()
        {
            var id = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.RewardedInterstitial, RuntimePlatform.IPhonePlayer);
            Assert.IsNotNull(id);
            StringAssert.StartsWith("ca-app-pub-", id);
        }

        [Test]
        public void GetTestAdUnitId_iOS_AppOpen_ReturnsNonNull()
        {
            var id = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.AppOpen, RuntimePlatform.IPhonePlayer);
            Assert.IsNotNull(id);
            StringAssert.StartsWith("ca-app-pub-", id);
        }

        [Test]
        public void GetTestAdUnitId_iOS_Native_ReturnsNonNull()
        {
            var id = AdTestUnitIds.GetTestAdUnitId("native", RuntimePlatform.IPhonePlayer);
            Assert.IsNotNull(id);
            StringAssert.StartsWith("ca-app-pub-", id);
        }

        // ═══════════════════════════════════════════════════════════════════
        // Android vs iOS IDs are different
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void GetTestAdUnitId_AndroidAndIos_BannerIds_AreDifferent()
        {
            var android = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.Banner, RuntimePlatform.Android);
            var ios     = AdTestUnitIds.GetTestAdUnitId(AdFormatKey.Banner, RuntimePlatform.IPhonePlayer);

            Assert.AreNotEqual(android, ios,
                "Android and iOS test ad unit IDs must be distinct");
        }

        // ═══════════════════════════════════════════════════════════════════
        // Unknown format guard
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void GetTestAdUnitId_UnknownFormat_ReturnsNull()
        {
            var id = AdTestUnitIds.GetTestAdUnitId("unknown_format", RuntimePlatform.Android);
            Assert.IsNull(id, "Unknown format must return null");
        }

        [Test]
        public void GetTestAdUnitId_NullFormat_ReturnsNull()
        {
            var id = AdTestUnitIds.GetTestAdUnitId(null, RuntimePlatform.Android);
            Assert.IsNull(id, "Null format must return null");
        }
    }

    /// <summary>
    /// Verifies the native ABI contract for <see cref="NoctuaProductType"/>
    /// and <see cref="NoctuaConsumableType"/> enum ordinals.
    /// These must match the values declared in the Android/iOS native SDKs.
    /// </summary>
    [TestFixture]
    public class NoctuaIapEnumOrdinalTest
    {
        // ─── NoctuaProductType ──────────────────────────────────────────────

        [Test] public void ProductType_InApp_OrdinalIsZero() =>
            Assert.AreEqual(0, (int)NoctuaProductType.InApp);

        [Test] public void ProductType_Subs_OrdinalIsOne() =>
            Assert.AreEqual(1, (int)NoctuaProductType.Subs);

        // ─── NoctuaConsumableType ───────────────────────────────────────────

        [Test] public void ConsumableType_Consumable_OrdinalIsZero() =>
            Assert.AreEqual(0, (int)NoctuaConsumableType.Consumable);

        [Test] public void ConsumableType_NonConsumable_OrdinalIsOne() =>
            Assert.AreEqual(1, (int)NoctuaConsumableType.NonConsumable);

        [Test] public void ConsumableType_Subscription_OrdinalIsTwo() =>
            Assert.AreEqual(2, (int)NoctuaConsumableType.Subscription);
    }
}
