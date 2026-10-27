using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tests.Runtime.Campaign
{
    /// <summary>
    /// The popup's scroll host: a tall creative still scrolls by drag, but no scrollbar is ever
    /// drawn over it — not even when the creative ends up a pixel wider than the card.
    /// </summary>
    [TestFixture]
    public class CampaignPopupScrollTest
    {
        private static ScrollView MountScroll()
        {
            var asset = Resources.Load<VisualTreeAsset>("CampaignPopup");
            Assert.IsNotNull(asset, "CampaignPopup.uxml not found in Resources");
            var scroll = asset.CloneTree().Q<ScrollView>("MountScroll");
            Assert.IsNotNull(scroll, "MountScroll missing from CampaignPopup.uxml");
            return scroll;
        }

        [Test]
        public void MountScroll_ScrollsVertically_WithNoVisibleScrollbars()
        {
            var scroll = MountScroll();

            Assert.AreEqual(ScrollViewMode.Vertical, scroll.mode);
            Assert.AreEqual(ScrollerVisibility.Hidden, scroll.horizontalScrollerVisibility);
            Assert.AreEqual(ScrollerVisibility.Hidden, scroll.verticalScrollerVisibility);
        }
    }
}
