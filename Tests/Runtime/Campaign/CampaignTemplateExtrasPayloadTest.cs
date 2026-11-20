using System.Collections.Generic;
using System.IO;
using System.Linq;
using com.noctuagames.sdk.LiveOpsCampaign;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Tests.Runtime.Campaign
{
    /// <summary>
    /// Contract test against a fixed template with extra elements, as noctua-admin emits it (golden
    /// fixture <c>single_offer_with_extra_elements</c>, copied into Fixtures/): the extras are normal
    /// view nodes appended to the template's root, copy under <c>x_&lt;id&gt;</c> data tokens, and an
    /// extra Buy button with no label shows its SKU's store price. No SDK change is needed for them.
    /// </summary>
    [TestFixture]
    public class CampaignTemplateExtrasPayloadTest
    {
        private const string FixturePath =
            "Packages/com.noctuagames.sdk/Tests/Runtime/Campaign/Fixtures/single-offer-extras.json";

        private const string BonusSku = "com.example.game.bonus";

        private CampaignItem _item;
        private CampaignRenderer _renderer;
        private RecordingActions _actions;
        private CampaignRuntimeController _controller;

        [SetUp]
        public void SetUp()
        {
            _item = JsonConvert.DeserializeObject<CampaignItem>(File.ReadAllText(Path.GetFullPath(FixturePath)));
            _actions = new RecordingActions();
            _renderer = new CampaignRenderer(_actions.Dispatch, new FakeImageSource(), new FakeFontSource().GetFont);
            _controller = new CampaignRuntimeController();
        }

        [TearDown]
        public void TearDown() => _controller.Dispose();

        private VisualElement Render()
        {
            var root = _renderer.RenderCampaign(CampaignPlayerData.Merge(_item, null), _controller, 1);
            Assert.NotNull(root, "a template with extra elements must render");
            return root;
        }

        [Test]
        public void Payload_Validates_AsSchemaV1()
        {
            Assert.IsTrue(CampaignValidator.TryValidate(_item, out var error), error);
            Assert.AreEqual(1, _item.EffectiveSchemaVersion(1), "plain extras reach every SDK");
        }

        [Test]
        public void Extras_AreAppendedAfterTheTemplateNodes()
        {
            var children = _item.View.Children;
            Assert.AreEqual(6, children.Count, "3 template nodes + 3 extras");
            CollectionAssert.AreEqual(
                new[] { CampaignNode.TypeImage, CampaignNode.TypeText, CampaignNode.TypeButton },
                children.Skip(3).Select(c => c.Type));
        }

        [Test]
        public void Renders_TheExtraCopy_AndTheExtraBuyButtonsPrice()
        {
            var root = Render();
            var texts = root.Query<TextElement>().ToList().Select(t => t.text).ToList();

            CollectionAssert.Contains(texts, "BEST VALUE");
            CollectionAssert.Contains(texts, "$1.99", "the label-less Buy button shows its store price");
            Assert.AreEqual(2, root.Query<VisualElement>("campaign-image").ToList().Count,
                "the template background and the extra ribbon");
        }

        [Test]
        public void ExtraBuyButton_IsOneOfTheCampaignsSkus()
        {
            // Ownership, Purchased rendering and buy-once / once-per-day see the extra SKU too.
            CollectionAssert.Contains(NoctuaLiveOpsCampaign.PurchaseSkus(_item), BonusSku);
        }

        [Test]
        public void ExtraBuyButton_DispatchesAPurchaseOfItsSku()
        {
            var root = Render();
            var bonus = root.Query<Button>().ToList().Single(b => b.text == "$1.99");

            using (var fixture = new CampaignPanelFixture())
            {
                fixture.Add(root);
                using var click = ClickEvent.GetPooled();
                click.target = bonus;
                bonus.SendEvent(click);
            }

            Assert.AreEqual(BonusSku, _actions.Calls.Single().action.ProductId);
        }
    }
}
