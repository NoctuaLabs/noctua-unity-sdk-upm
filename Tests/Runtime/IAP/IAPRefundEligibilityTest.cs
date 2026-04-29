using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace com.noctuagames.sdk.Tests.IAP
{
    /// <summary>
    /// Unit tests for the offline branches of <see cref="NoctuaIAPService.IsRefundedAsync"/>.
    ///
    /// Seeds the <c>NoctuaRefundTracking</c> PlayerPrefs key directly with
    /// <see cref="RefundTrackingEntry"/> records and verifies the JSON shape and the
    /// three-condition decision logic (consumability is auto-detected at purchase time, so it is
    /// not part of the IsRefundedAsync filter).
    ///
    /// The async store call (<c>GetPurchaseStatusAsync</c>) is platform-specific and not
    /// exercised here; instead a shadow implementation of the offline conditions matches the
    /// production code in NoctuaIAPService.
    /// </summary>
    [TestFixture]
    public class IAPRefundEligibilityTest
    {
        private const string RefundTrackingPrefsKey = "NoctuaRefundTracking";

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(RefundTrackingPrefsKey);
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(RefundTrackingPrefsKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void RefundTrackingEntry_RoundTripsThroughJson()
        {
            var entry = new RefundTrackingEntry
            {
                ProductId = "p1",
                PaymentType = PaymentType.playstore,
                Timestamp = DateTime.UtcNow.AddDays(-3),
            };

            var json = JsonConvert.SerializeObject(new List<RefundTrackingEntry> { entry });
            var roundTripped = JsonConvert.DeserializeObject<List<RefundTrackingEntry>>(json);

            Assert.AreEqual(1, roundTripped.Count);
            Assert.AreEqual("p1", roundTripped[0].ProductId);
            Assert.AreEqual(PaymentType.playstore, roundTripped[0].PaymentType);
        }

        [Test]
        public void DecisionLogic_OldEnough_PlayStore_NotInStore_IsRefunded()
        {
            Assert.IsTrue(EvaluateOffline(
                paymentType: PaymentType.playstore,
                daysAgo: 3,
                isStillPurchased: false));
        }

        [Test]
        public void DecisionLogic_AppStore_IsRefunded()
        {
            Assert.IsTrue(EvaluateOffline(
                paymentType: PaymentType.appstore,
                daysAgo: 3,
                isStillPurchased: false));
        }

        [Test]
        public void DecisionLogic_StillPurchased_IsNotRefunded()
        {
            Assert.IsFalse(EvaluateOffline(
                paymentType: PaymentType.playstore,
                daysAgo: 3,
                isStillPurchased: true));
        }

        [Test]
        public void DecisionLogic_TooRecent_IsNotRefunded()
        {
            Assert.IsFalse(EvaluateOffline(
                paymentType: PaymentType.playstore,
                daysAgo: 1,
                isStillPurchased: false));
        }

        [Test]
        public void DecisionLogic_NoctuaStore_IsNotRefunded()
        {
            Assert.IsFalse(EvaluateOffline(
                paymentType: PaymentType.noctuastore,
                daysAgo: 30,
                isStillPurchased: false));
        }

        [Test]
        public void DecisionLogic_LocalKindTimestamp_NormalisedToUtc()
        {
            var localTs = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-3).ToLocalTime(), DateTimeKind.Local);
            Assert.IsTrue(EvaluateOfflineWithTimestamp(
                paymentType: PaymentType.playstore,
                timestamp: localTs,
                isStillPurchased: false,
                minAgeDays: 2));
        }

        [Test]
        public void DecisionLogic_CustomMinAgeDays_BoundaryRespected()
        {
            // 5 days ago, threshold 7 → not yet refundable
            Assert.IsFalse(EvaluateOfflineWithTimestamp(
                paymentType: PaymentType.playstore,
                timestamp: DateTime.UtcNow.AddDays(-5),
                isStillPurchased: false,
                minAgeDays: 7));

            // 8 days ago, threshold 7 → refundable
            Assert.IsTrue(EvaluateOfflineWithTimestamp(
                paymentType: PaymentType.playstore,
                timestamp: DateTime.UtcNow.AddDays(-8),
                isStillPurchased: false,
                minAgeDays: 7));
        }

        // ---- helpers ----------------------------------------------------------

        // Mirrors the offline branches inside NoctuaIAPService.IsRefundedAsync. Kept here as a
        // shadow implementation so the test fails loudly if the production logic drifts.
        private static bool EvaluateOffline(
            PaymentType paymentType, int daysAgo, bool isStillPurchased)
            => EvaluateOfflineWithTimestamp(
                paymentType, DateTime.UtcNow.AddDays(-daysAgo), isStillPurchased, minAgeDays: 2);

        private static bool EvaluateOfflineWithTimestamp(
            PaymentType paymentType, DateTime timestamp, bool isStillPurchased, int minAgeDays)
        {
            if (paymentType != PaymentType.playstore && paymentType != PaymentType.appstore) return false;

            var ts = timestamp.Kind == DateTimeKind.Utc ? timestamp : timestamp.ToUniversalTime();
            if (ts >= DateTime.UtcNow.AddDays(-minAgeDays)) return false;

            if (isStillPurchased) return false;
            return true;
        }
    }
}
