using System.Collections.Generic;
using com.noctuagames.sdk.LiveOpsCampaign;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tests.Runtime.Campaign
{
    [TestFixture]
    public class CampaignRendererTest
    {
        private CampaignRenderer _renderer;
        private RecordingActions _actions;
        private FakeImageSource _images;
        private FakeFontSource _fonts;
        private CampaignRuntimeController _controller;

        [SetUp]
        public void SetUp()
        {
            _actions = new RecordingActions();
            _images = new FakeImageSource();
            _fonts = new FakeFontSource();
            _renderer = new CampaignRenderer(_actions, _images, _fonts);
            _controller = new CampaignRuntimeController();
        }

        [TearDown]
        public void TearDown() => _controller.Dispose();

        private VisualElement Render(CampaignNode node, CampaignItem item = null)
            => _renderer.Render(node, item, _controller);

        [Test]
        public void Container_Rendered_AsVisualElementWithChildren()
        {
            var node = CampaignFactory.Node(CampaignNode.TypeContainer, children: new[]
            {
                CampaignFactory.Node(CampaignNode.TypeText, new Dictionary<string, object> { { "text", "hi" } }),
                CampaignFactory.Node(CampaignNode.TypeDivider),
            });

            var ve = Render(node);

            Assert.IsInstanceOf<VisualElement>(ve);
            Assert.AreEqual(2, ve.childCount);
        }

        [Test]
        public void Text_Rendered_AsLabelWithResolvedText()
        {
            var item = CampaignFactory.Item("c", CampaignItem.EngagementPurchase, null,
                new Dictionary<string, string> { { "name", "Ayu" } });
            var node = CampaignFactory.Node(CampaignNode.TypeText,
                new Dictionary<string, object> { { "text", "Hello {{name}}!" } });

            var label = Render(node, item) as Label;

            Assert.NotNull(label);
            Assert.AreEqual("Hello Ayu!", label.text);
        }

        [Test]
        public void Image_Rendered_RequestsUrlFromImageSource()
        {
            var node = CampaignFactory.Node(CampaignNode.TypeImage,
                new Dictionary<string, object> { { "url", "https://cdn.example/x.png" } });

            var ve = Render(node);

            Assert.NotNull(ve);
            CollectionAssert.Contains(_images.Requested, "https://cdn.example/x.png");
        }

        [Test]
        public void RenderCampaign_PinsImages_AndUnpinsOnControllerDispose()
        {
            var root = CampaignFactory.Node(CampaignNode.TypeContainer, children: new[]
            {
                CampaignFactory.Node(CampaignNode.TypeImage, new Dictionary<string, object> { { "url", "https://cdn/a.png" } }),
                CampaignFactory.Node(CampaignNode.TypeImage, new Dictionary<string, object> { { "url", "https://cdn/b.png" } }),
            });
            var item = CampaignFactory.Item("c", CampaignItem.EngagementPurchase, root);
            var controller = new CampaignRuntimeController();

            _renderer.RenderCampaign(item, controller);
            CollectionAssert.AreEquivalent(new[] { "https://cdn/a.png", "https://cdn/b.png" }, _images.Pinned);
            Assert.IsEmpty(_images.Unpinned);

            controller.Dispose();
            CollectionAssert.AreEquivalent(new[] { "https://cdn/a.png", "https://cdn/b.png" }, _images.Unpinned);
        }

        [Test]
        public void Button_Rendered_AsButtonAndClickDispatchesAction()
        {
            var action = new CampaignAction { TypeRaw = "dismiss" };
            var node = CampaignFactory.Node(CampaignNode.TypeButton,
                new Dictionary<string, object> { { "text", "OK" } }, action: action);

            var button = Render(node) as Button;

            Assert.NotNull(button);
            Assert.AreEqual("OK", button.text);

            using (var fixture = new CampaignPanelFixture())
            {
                fixture.Add(button);
                using var e = ClickEvent.GetPooled();
                button.SendEvent(e); // SendEvent sets e.target to the receiver when unset
            }

            Assert.AreEqual(1, _actions.Calls.Count);
            Assert.AreSame(action, _actions.Calls[0].action);
        }

        [Test]
        public void List_Rendered_AsColumn()
        {
            var node = CampaignFactory.Node(CampaignNode.TypeList, children: new[]
            {
                CampaignFactory.Node(CampaignNode.TypeText),
                CampaignFactory.Node(CampaignNode.TypeText),
                CampaignFactory.Node(CampaignNode.TypeText),
            });

            var ve = Render(node);

            Assert.AreEqual(3, ve.childCount);
            Assert.AreEqual(FlexDirection.Column, ve.style.flexDirection.value);
        }

        [Test]
        public void Carousel_Rendered_WithOnePagePerChild()
        {
            var node = CampaignFactory.Node(CampaignNode.TypeCarousel, children: new[]
            {
                CampaignFactory.Node(CampaignNode.TypeText),
                CampaignFactory.Node(CampaignNode.TypeText),
            });

            var ve = Render(node);
            var track = ve.Q("campaign-carousel-track");
            var dots = ve.Q("campaign-carousel-dots");

            Assert.NotNull(track);
            Assert.AreEqual(2, track.childCount);
            Assert.NotNull(dots);
            Assert.AreEqual(2, dots.childCount);
        }

        [Test]
        public void ProgressBar_Rendered_WithValueMinMax()
        {
            var node = CampaignFactory.Node(CampaignNode.TypeProgressBar, new Dictionary<string, object>
            {
                { "min", 0 }, { "max", 200 }, { "value", 50 },
            });

            var pb = Render(node) as ProgressBar;

            Assert.NotNull(pb);
            Assert.AreEqual(0f, pb.lowValue);
            Assert.AreEqual(200f, pb.highValue);
            Assert.AreEqual(50f, pb.value);
        }

        [Test]
        public void Countdown_Rendered_WithInitialFormattedLabel()
        {
            var end = System.DateTime.UtcNow.AddSeconds(65).ToString("o");
            var node = CampaignFactory.Node(CampaignNode.TypeCountdown, new Dictionary<string, object>
            {
                { "end_ts", end }, { "prefix", "Ends in " },
            });

            var label = Render(node) as Label;

            Assert.NotNull(label);
            StringAssert.StartsWith("Ends in 00:0", label.text); // ~00:01:0x
        }

        [Test]
        public void UnknownType_Rendered_AsNull_NoThrow()
        {
            var node = CampaignFactory.Node("hologram");
            Assert.IsNull(Render(node));
        }

        [Test]
        public void DeeplyNested_Tree_DoesNotStackOverflow_AndTruncates()
        {
            var root = CampaignFactory.Node(CampaignNode.TypeContainer);
            var cur = root;
            for (var i = 0; i < 500; i++)
            {
                var child = CampaignFactory.Node(CampaignNode.TypeContainer);
                cur.Children = new List<CampaignNode> { child };
                cur = child;
            }

            var item = CampaignFactory.Item("deep", CampaignItem.EngagementPurchase, root);
            using var controller = new CampaignRuntimeController();

            VisualElement ve = null;
            Assert.DoesNotThrow(() => ve = _renderer.RenderCampaign(item, controller));
            Assert.NotNull(ve); // top levels render; the tail past MaxDepth is dropped

            int Depth(VisualElement e)
            {
                var d = 0;
                while (e.childCount > 0) { e = e[0]; d++; }
                return d;
            }
            Assert.LessOrEqual(Depth(ve), 40);
        }

        [Test]
        public void WideTree_Truncates_AtNodeBudget()
        {
            var root = CampaignFactory.Node(CampaignNode.TypeContainer);
            root.Children = new List<CampaignNode>();
            for (var i = 0; i < 2000; i++)
                root.Children.Add(CampaignFactory.Node(CampaignNode.TypeText,
                    new Dictionary<string, object> { { "text", "x" } }));

            var item = CampaignFactory.Item("wide", CampaignItem.EngagementPurchase, root);
            using var controller = new CampaignRuntimeController();

            var ve = _renderer.RenderCampaign(item, controller);

            Assert.NotNull(ve);
            Assert.Less(ve.childCount, 2000);     // budget-capped
            Assert.Greater(ve.childCount, 100);   // but a useful prefix rendered
        }

        [Test]
        public void Style_Mapping_LengthPercentAndHexColor()
        {
            var node = CampaignFactory.Node(CampaignNode.TypeContainer, style: new CampaignStyleProps
            {
                Width = "50%",
                Height = "120",
                BackgroundColor = "#112233",
            });

            var ve = Render(node);

            Assert.AreEqual(LengthUnit.Percent, ve.style.width.value.unit);
            Assert.AreEqual(50f, ve.style.width.value.value);
            Assert.AreEqual(120f, ve.style.height.value.value);
            Assert.AreEqual(new Color(0x11 / 255f, 0x22 / 255f, 0x33 / 255f, 1f), ve.style.backgroundColor.value);
        }

        [Test]
        public void Style_UnknownProp_Ignored_NoThrow()
        {
            var node = CampaignFactory.Node(CampaignNode.TypeContainer, style: new CampaignStyleProps
            {
                Width = "not-a-length",
                FlexDirection = "diagonal",
            });

            Assert.DoesNotThrow(() => Render(node));
        }

        [Test]
        public void Responsive_PortraitOverride_Applied()
        {
            var node = CampaignFactory.Node(CampaignNode.TypeContainer, style: new CampaignStyleProps { Width = "100" });
            node.Responsive = new Dictionary<string, CampaignStyleProps>
            {
                { "portrait", new CampaignStyleProps { Width = "77" } },
            };

            var ve = Render(node);

            // No panel in EditMode → orientation resolves to portrait (Screen defaults).
            Assert.AreEqual(77f, ve.style.width.value.value);
        }

        [Test]
        public void Action_ProductId_TokenResolvedBeforeDispatch()
        {
            var item = CampaignFactory.Item("c", CampaignItem.EngagementPurchase, null,
                new Dictionary<string, string> { { "sku", "noctua.pack7" } });
            var node = CampaignFactory.Node(CampaignNode.TypeButton,
                new Dictionary<string, object> { { "text", "Buy" } },
                action: new CampaignAction { TypeRaw = "purchase", ProductId = "{{sku}}" });

            var button = Render(node, item) as Button;
            using (var fixture = new CampaignPanelFixture())
            {
                fixture.Add(button);
                using var e = ClickEvent.GetPooled();
                button.SendEvent(e);
            }

            Assert.AreEqual(1, _actions.Calls.Count);
            Assert.AreEqual("noctua.pack7", _actions.Calls[0].action.ProductId);
        }

        [Test]
        public void MissingToken_ResolvesToEmpty_NoThrow()
        {
            var item = CampaignFactory.Item("c", CampaignItem.EngagementPurchase, null, new Dictionary<string, string>());
            var node = CampaignFactory.Node(CampaignNode.TypeText,
                new Dictionary<string, object> { { "text", "x{{gone}}y" } });

            var label = Render(node, item) as Label;

            Assert.AreEqual("xy", label.text);
        }

        [Test]
        public void FontFamily_OnText_QueriesFontSource_WithFamily()
        {
            var node = CampaignFactory.Node(CampaignNode.TypeText,
                new Dictionary<string, object> { { "text", "hi" } },
                style: new CampaignStyleProps { FontFamily = "Poppins" });

            Render(node);

            CollectionAssert.Contains(_fonts.Requested, "Poppins");
        }

        [Test]
        public void FontFamily_OnContainer_QueriesFontSource_InheritancePath()
        {
            var node = CampaignFactory.Node(CampaignNode.TypeContainer,
                style: new CampaignStyleProps { FontFamily = "Poppins" },
                children: new[] { CampaignFactory.Node(CampaignNode.TypeText,
                    new Dictionary<string, object> { { "text", "hi" } }) });

            Render(node);

            CollectionAssert.Contains(_fonts.Requested, "Poppins");
        }

        [Test]
        public void FontFamily_SourceReturnsNull_NoThrow_ElementStillBuilt()
        {
            _fonts.Next = null; // unknown / failed / offline
            var node = CampaignFactory.Node(CampaignNode.TypeText,
                new Dictionary<string, object> { { "text", "hi" } },
                style: new CampaignStyleProps { FontFamily = "Missing" });

            VisualElement ve = null;
            Assert.DoesNotThrow(() => ve = Render(node));
            Assert.IsInstanceOf<Label>(ve);
        }

        [Test]
        public void NoFontFamily_FontSourceNeverQueried()
        {
            var node = CampaignFactory.Node(CampaignNode.TypeText,
                new Dictionary<string, object> { { "text", "hi" } });

            Render(node);

            Assert.IsEmpty(_fonts.Requested);
        }

        [Test]
        public void Style_TextOutline_WidthAndColor_Applied()
        {
            var node = CampaignFactory.Node(CampaignNode.TypeText,
                new Dictionary<string, object> { { "text", "hi" } },
                style: new CampaignStyleProps { FontOutlineWidth = "2", FontOutlineColor = "#112233" });

            var ve = Render(node);

            Assert.AreEqual(2f, ve.style.unityTextOutlineWidth.value);
            Assert.AreEqual(new Color(0x11 / 255f, 0x22 / 255f, 0x33 / 255f, 1f), ve.style.unityTextOutlineColor.value);
        }

        [Test]
        public void Style_TextOutline_UnparseableWidth_Ignored_NoThrow()
        {
            var node = CampaignFactory.Node(CampaignNode.TypeText,
                new Dictionary<string, object> { { "text", "hi" } },
                style: new CampaignStyleProps { FontOutlineWidth = "not-a-number" });

            Assert.DoesNotThrow(() => Render(node));
        }

        [Test]
        public void RenderCampaign_InvalidItem_ReturnsNull()
        {
            // image node with a token that has no data entry → unresolved required 'url'
            var root = CampaignFactory.Node(CampaignNode.TypeImage,
                new Dictionary<string, object> { { "url", "{{missing}}" } });
            var item = CampaignFactory.Item("bad", CampaignItem.EngagementPurchase, root, new Dictionary<string, string>());

            using var controller = new CampaignRuntimeController();
            Assert.IsNull(_renderer.RenderCampaign(item, controller));
        }
    }
}
