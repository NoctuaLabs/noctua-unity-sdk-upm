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
    /// Contract test against the "Monthly Pass" payload noctua-admin's multi_offer template emits
    /// (its golden fixture, copied into Fixtures/): two offer cards, each buying its own SKU, a
    /// deeplink button, and <c>{{store_price.&lt;sku&gt;}}</c> prices with USD fallbacks.
    /// </summary>
    [TestFixture]
    public class CampaignMultiOfferPayloadTest
    {
        private const string FixturePath =
            "Packages/com.noctuagames.sdk/Tests/Runtime/Campaign/Fixtures/multi-offer.json";

        private CampaignItem _item;
        private CampaignRenderer _renderer;
        private RecordingActions _actions;
        private CampaignRuntimeController _controller;

        [SetUp]
        public void SetUp()
        {
            _item = JsonConvert.DeserializeObject<CampaignItem>(File.ReadAllText(Path.GetFullPath(FixturePath)));
            _actions = new RecordingActions();
            _renderer = new CampaignRenderer(_actions, new FakeImageSource(), new FakeFontSource());
            _controller = new CampaignRuntimeController();
        }

        [TearDown]
        public void TearDown() => _controller.Dispose();

        private VisualElement Render(CampaignItem item)
        {
            var root = _renderer.RenderCampaign(item, _controller, 1);
            Assert.NotNull(root, "the multi-offer payload must render");
            return root;
        }

        private static List<string> Texts(VisualElement root) =>
            root.Query<TextElement>().ToList().Select(t => t.text).ToList();

        [Test]
        public void Payload_Validates()
        {
            Assert.IsTrue(CampaignValidator.TryValidate(_item, out var error), error);
            Assert.IsTrue(CampaignStorePrices.Uses(_item));
        }

        [Test]
        public void Renders_BothOffers_WithUsdFallbackPrices()
        {
            var texts = Texts(Render(_item));

            CollectionAssert.IsSubsetOf(
                new[] { "Monthly Pass", "Normal Pass", "Gold Pass", "Five Benefits", "Buy", "Details" }, texts);
            // Normal's price tag and Gold's unlabeled buy button show the fallback prices.
            CollectionAssert.Contains(texts, "$4.99");
            CollectionAssert.Contains(texts, "$9.99");
        }

        [Test]
        public void LocalPrices_ReplaceTheFallbacks()
        {
            var prices = new Dictionary<string, string>
            {
                { "com.example.pass.normal", "Rp80.500" },
                { "com.example.pass.gold", "Rp161.000" },
            };
            var local = new CampaignStorePrices(() => Cysharp.Threading.Tasks.UniTask.FromResult<IReadOnlyDictionary<string, string>>(prices));
            Assert.IsTrue(local.EnsureFetchedAsync().GetAwaiter().GetResult());

            var texts = Texts(Render(local.Apply(_item)));
            CollectionAssert.Contains(texts, "Rp80.500");
            CollectionAssert.Contains(texts, "Rp161.000");
            CollectionAssert.DoesNotContain(texts, "$9.99");
        }

        [Test]
        public void EachButton_DispatchesItsOwnAction()
        {
            var root = Render(_item);
            var buttons = root.Query<Button>().ToList();
            Assert.AreEqual(3, buttons.Count);

            using (var fixture = new CampaignPanelFixture())
            {
                fixture.Add(root);
                foreach (var button in buttons)
                {
                    using var click = ClickEvent.GetPooled();
                    click.target = button;
                    button.SendEvent(click);
                }
            }

            var dispatched = _actions.Calls.Select(c => c.action).ToList();
            Assert.AreEqual(CampaignActionType.Purchase, dispatched[0].Type);
            Assert.AreEqual("com.example.pass.normal", dispatched[0].ProductId);
            Assert.AreEqual("offer_0_btn_0", dispatched[0].Id);
            Assert.AreEqual("com.example.pass.gold", dispatched[1].ProductId);
            Assert.AreEqual("offer_1_btn_0", dispatched[1].Id);
            Assert.AreEqual(CampaignActionType.Deeplink, dispatched[2].Type);
            Assert.AreEqual("pass/details/gold", dispatched[2].Deeplink);
        }
    }
}
