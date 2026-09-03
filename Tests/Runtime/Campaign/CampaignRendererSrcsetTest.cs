using System.Collections.Generic;
using com.noctuagames.sdk.Campaign;
using NUnit.Framework;

namespace Tests.Runtime.Campaign
{
    [TestFixture]
    public class CampaignRendererSrcsetTest
    {
        private static CampaignImageSrc S(int w, string url = null) =>
            new CampaignImageSrc { Width = w, Url = url ?? $"u{w}" };

        // ---- PickSrcset --------------------------------------------------

        [Test]
        public void Pick_ReturnsSmallestTierAtOrAboveTarget()
        {
            var set = new[] { S(480), S(960), S(1440), S(2732) };
            Assert.AreEqual(960, CampaignRenderer.PickSrcset(set, 800).Width);
            Assert.AreEqual(960, CampaignRenderer.PickSrcset(set, 960).Width);  // exact match
            Assert.AreEqual(480, CampaignRenderer.PickSrcset(set, 1).Width);
        }

        [Test]
        public void Pick_ReturnsWidestWhenTargetExceedsAll()
        {
            var set = new[] { S(480), S(960), S(1440) };
            Assert.AreEqual(1440, CampaignRenderer.PickSrcset(set, 5000).Width);
        }

        [Test]
        public void Pick_HandlesUnsortedInput()
        {
            var set = new[] { S(1440), S(480), S(2732), S(960) };
            Assert.AreEqual(1440, CampaignRenderer.PickSrcset(set, 1000).Width);
            Assert.AreEqual(2732, CampaignRenderer.PickSrcset(set, 9000).Width);
        }

        [Test]
        public void Pick_NullOrEmpty_ReturnsNull()
        {
            Assert.IsNull(CampaignRenderer.PickSrcset(null, 100));
            Assert.IsNull(CampaignRenderer.PickSrcset(new CampaignImageSrc[0], 100));
        }

        [Test]
        public void Pick_SkipsNullEntries()
        {
            var set = new[] { null, S(960), null, S(480) };
            Assert.AreEqual(960, CampaignRenderer.PickSrcset(set, 700).Width);
        }

        // ---- CampaignNode.PropSrcset ----------------------------------------

        private static CampaignNode ImageNode(object srcset) => new CampaignNode
        {
            Type = CampaignNode.TypeImage,
            Props = new Dictionary<string, object> { { "url", "fallback.png" }, { "srcset", srcset } },
        };

        [Test]
        public void PropSrcset_ParsesArray_SortedAscending()
        {
            var node = ImageNode(new List<object>
            {
                new Dictionary<string, object> { { "url", "c.png" }, { "w", 1440 } },
                new Dictionary<string, object> { { "url", "a.png" }, { "w", 480 } },
                new Dictionary<string, object> { { "url", "b.png" }, { "w", 960 } },
            });

            var set = node.PropSrcset();

            CollectionAssert.AreEqual(new[] { 480, 960, 1440 }, set.ConvertAll(e => e.Width));
            Assert.AreEqual("a.png", set[0].Url);
        }

        [Test]
        public void PropSrcset_DropsInvalidEntries()
        {
            var node = ImageNode(new List<object>
            {
                new Dictionary<string, object> { { "url", "ok.png" }, { "w", 960 } },
                new Dictionary<string, object> { { "url", "" }, { "w", 480 } },       // empty url
                new Dictionary<string, object> { { "url", "zero.png" }, { "w", 0 } }, // non-positive width
                new Dictionary<string, object> { { "url", "neg.png" }, { "w", -5 } },
            });

            var set = node.PropSrcset();

            Assert.AreEqual(1, set.Count);
            Assert.AreEqual("ok.png", set[0].Url);
        }

        [Test]
        public void PropSrcset_MissingOrGarbage_ReturnsEmpty()
        {
            Assert.IsEmpty(new CampaignNode { Type = CampaignNode.TypeImage }.PropSrcset());
            Assert.IsEmpty(new CampaignNode
            {
                Type = CampaignNode.TypeImage,
                Props = new Dictionary<string, object> { { "srcset", "not-an-array" } },
            }.PropSrcset());
            Assert.IsEmpty(ImageNode(null).PropSrcset());
        }
    }
}
