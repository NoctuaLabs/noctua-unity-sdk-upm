using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// All SDK presenters share one PanelSettings, so UIDocument.sortingOrder alone decides which
    /// SDK screen is visible. Regression: the offline Retry dialog drew under an open campaign
    /// popup, so a purchase from the popup showed only a spinner and waited on a hidden dialog.
    /// </summary>
    [TestFixture]
    public class UISortingOrderTest
    {
        [Test]
        public void Modals_DrawAboveTheCampaignPopup()
        {
            Assert.Greater(UIUtility.ModalSortingOrder, UIUtility.CampaignPopupSortingOrder);
        }

        [Test]
        public void CampaignPopup_DrawsAboveAdPlaceholders()
        {
            Assert.Greater(UIUtility.CampaignPopupSortingOrder, UIUtility.CrossPromoSortingOrder);
            Assert.Greater(UIUtility.CampaignPopupSortingOrder, UIUtility.BaseSortingOrder);
        }

        [Test]
        public void CrossPromo_DrawsAboveTheBanner()
        {
            Assert.Greater(UIUtility.CrossPromoSortingOrder, UIUtility.BaseSortingOrder);
        }

        [Test]
        public void ExistingLayers_KeepTheirPreviousValues()
        {
            // Unchanged from before the modal layer was added — games never saw a different order.
            Assert.AreEqual(1, UIUtility.BaseSortingOrder);
            Assert.AreEqual(2, UIUtility.CrossPromoSortingOrder);
            Assert.AreEqual(3, UIUtility.CampaignPopupSortingOrder);
        }
    }
}
