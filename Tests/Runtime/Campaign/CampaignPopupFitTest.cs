using System.Collections.Generic;
using com.noctuagames.sdk.LiveOpsCampaign;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tests.Runtime.Campaign
{
    /// <summary>
    /// Orientation handling for campaign popups.
    ///
    /// Rotation is locked per game, so a campaign authored in the wrong orientation renders
    /// that way for its entire run — there is no rotation to rescue it. These cover the two
    /// halves of that: <see cref="CampaignPopupFit"/>, which keeps a mismatched design whole
    /// instead of letting the card clip it, and the renderer's <c>responsive</c> key, whose
    /// landscape branch previously had no test because an EditMode panel always measures
    /// portrait.
    /// </summary>
    [TestFixture]
    public class CampaignPopupFitTest
    {
        private const float Tolerance = 0.0005f;

        // --- ScaleFor -----------------------------------------------------

        [Test]
        public void ScaleFor_DesignFitsAvailable_NoScaling()
        {
            // A portrait popup on a portrait game: the authored box already fits.
            Assert.AreEqual(1f, CampaignPopupFit.ScaleFor(300f, 500f, 307f, 680f), Tolerance);
        }

        [Test]
        public void ScaleFor_SmallerThanAvailable_NeverUpscales()
        {
            // Blowing a design up would soften its art and betray its pixel positions.
            Assert.AreEqual(1f, CampaignPopupFit.ScaleFor(300f, 500f, 900f, 1500f), Tolerance);
        }

        [Test]
        public void ScaleFor_LandscapeDesignOnPortraitCard_ShrinksToWidth()
        {
            // The mismatch that matters: a 500x300 campaign on a portrait-locked game. Width
            // is the binding constraint, and without this the card would clip the CTA.
            var scale = CampaignPopupFit.ScaleFor(500f, 300f, 307f, 680f);

            Assert.AreEqual(307f / 500f, scale, Tolerance);
            Assert.Less(scale * 500f, 307f + Tolerance, "scaled width must fit the card");
        }

        [Test]
        public void ScaleFor_PortraitDesignOnLandscapeCard_ShrinksToHeight()
        {
            // The mirror case: a 300x500 campaign on a landscape-locked game, where height binds.
            var scale = CampaignPopupFit.ScaleFor(300f, 500f, 700f, 293f);

            Assert.AreEqual(293f / 500f, scale, Tolerance);
            Assert.Less(scale * 500f, 293f + Tolerance, "scaled height must fit the card");
        }

        [Test]
        public void ScaleFor_UsesTheTighterAxis()
        {
            // Both axes overflow; the smaller ratio wins so neither is clipped.
            Assert.AreEqual(0.25f, CampaignPopupFit.ScaleFor(400f, 400f, 200f, 100f), Tolerance);
        }

        [TestCase(0f, 500f, 300f, 500f)]
        [TestCase(300f, 0f, 300f, 500f)]
        [TestCase(300f, 500f, 0f, 500f)]
        [TestCase(300f, 500f, 300f, 0f)]
        [TestCase(300f, 500f, -10f, 500f)]
        [TestCase(float.NaN, 500f, 300f, 500f)]
        [TestCase(300f, 500f, float.NaN, 500f)]
        public void ScaleFor_UnusableInput_LeavesTheCreativeAlone(float dw, float dh, float aw, float ah)
        {
            // A panel that has not been laid out yet reports zero / NaN. Wait for the next
            // geometry pass rather than collapsing the popup to nothing.
            Assert.AreEqual(1f, CampaignPopupFit.ScaleFor(dw, dh, aw, ah), Tolerance);
        }

        // --- TryDesignSize ------------------------------------------------

        [Test]
        public void TryDesignSize_PixelBox_Read()
        {
            var style = new CampaignStyleProps { Width = "500", Height = "300" };

            Assert.IsTrue(CampaignPopupFit.TryDesignSize(style, out var w, out var h));
            Assert.AreEqual(500f, w, Tolerance);
            Assert.AreEqual(300f, h, Tolerance);
        }

        [Test]
        public void TryDesignSize_PixelSuffix_Read()
        {
            var style = new CampaignStyleProps { Width = "500px", Height = "300px" };

            Assert.IsTrue(CampaignPopupFit.TryDesignSize(style, out var w, out var h));
            Assert.AreEqual(500f, w, Tolerance);
            Assert.AreEqual(300f, h, Tolerance);
        }

        [Test]
        public void TryDesignSize_PercentageBox_Rejected()
        {
            // Already elastic — it sizes itself to the card, so there is nothing to fit.
            var style = new CampaignStyleProps { Width = "100%", Height = "100%" };

            Assert.IsFalse(CampaignPopupFit.TryDesignSize(style, out _, out _));
        }

        [Test]
        public void TryDesignSize_MissingOrUnparseable_Rejected()
        {
            Assert.IsFalse(CampaignPopupFit.TryDesignSize(null, out _, out _));
            Assert.IsFalse(CampaignPopupFit.TryDesignSize(new CampaignStyleProps { Width = "500" }, out _, out _));
            Assert.IsFalse(CampaignPopupFit.TryDesignSize(
                new CampaignStyleProps { Width = "wide", Height = "300" }, out _, out _));
            Assert.IsFalse(CampaignPopupFit.TryDesignSize(
                new CampaignStyleProps { Width = "0", Height = "300" }, out _, out _));
        }

        // --- responsive orientation key -----------------------------------

        [Test]
        public void ResolveOrientationKey_WiderThanTall_IsLandscape()
        {
            Assert.AreEqual("landscape", CampaignRenderer.ResolveOrientationKey(800f, 360f));
        }

        [Test]
        public void ResolveOrientationKey_TallerThanWide_IsPortrait()
        {
            Assert.AreEqual("portrait", CampaignRenderer.ResolveOrientationKey(360f, 800f));
        }

        [Test]
        public void ResolveOrientationKey_Square_IsPortrait()
        {
            // Matches the admin's default orientation, so a square viewport is not a third case.
            Assert.AreEqual("portrait", CampaignRenderer.ResolveOrientationKey(500f, 500f));
        }

        [Test]
        public void Responsive_LandscapeOverride_Applied()
        {
            // The branch an EditMode panel could never reach: it takes its size from the Game
            // view, so orientation always resolved to portrait.
            var ve = RenderWithViewport(800f, 360f, new Dictionary<string, CampaignStyleProps>
            {
                { "portrait", new CampaignStyleProps { Width = "77" } },
                { "landscape", new CampaignStyleProps { Width = "480" } },
            });

            Assert.AreEqual(480f, ve.style.width.value.value, Tolerance);
        }

        [Test]
        public void Responsive_PortraitOverride_StillApplied()
        {
            var ve = RenderWithViewport(360f, 800f, new Dictionary<string, CampaignStyleProps>
            {
                { "portrait", new CampaignStyleProps { Width = "77" } },
                { "landscape", new CampaignStyleProps { Width = "480" } },
            });

            Assert.AreEqual(77f, ve.style.width.value.value, Tolerance);
        }

        [Test]
        public void Responsive_NoMatchingKey_KeepsBaseStyle()
        {
            var ve = RenderWithViewport(800f, 360f, new Dictionary<string, CampaignStyleProps>
            {
                { "portrait", new CampaignStyleProps { Width = "77" } },
            });

            Assert.AreEqual(100f, ve.style.width.value.value, Tolerance);
        }

        /// <summary>
        /// Renders a width-100 container carrying <paramref name="responsive"/> against a fixed
        /// viewport. Goes through <c>RenderCampaign</c> rather than <c>Render</c> because only
        /// the former seeds the per-campaign node budget.
        /// </summary>
        private static VisualElement RenderWithViewport(float viewportWidth, float viewportHeight,
            Dictionary<string, CampaignStyleProps> responsive)
        {
            var renderer = new CampaignRenderer(new RecordingActions(), new FakeImageSource(),
                viewportSize: () => new Vector2(viewportWidth, viewportHeight));

            using (var controller = new CampaignRuntimeController())
            {
                var node = CampaignFactory.Node(CampaignNode.TypeContainer,
                    style: new CampaignStyleProps { Width = "100" });
                node.Responsive = responsive;

                var ve = renderer.RenderCampaign(
                    CampaignFactory.Item("fit-test", CampaignItem.EngagementPurchase, node), controller);

                Assert.IsNotNull(ve, "RenderCampaign returned null");
                return ve;
            }
        }
    }
}
