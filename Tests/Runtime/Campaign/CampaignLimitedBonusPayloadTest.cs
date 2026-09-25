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
    /// Contract test against the "Limited Bonus" payload noctua-admin's multi_offer template
    /// emits with its extras (golden fixture, copied into Fixtures/): hero art, a countdown,
    /// three cards across with item icons and discount badges, and a per-card purchase limit
    /// driven by <c>player_data</c> (<c>bought.&lt;sku&gt;</c>) that swaps Buy for "Sold out".
    /// </summary>
    [TestFixture]
    public class CampaignLimitedBonusPayloadTest
    {
        private const string FixturePath =
            "Packages/com.noctuagames.sdk/Tests/Runtime/Campaign/Fixtures/limited-bonus.json";

        private const string PremiumBought = "bought.com.example.bonus.premium";
        private const string LuxuryBought = "bought.com.example.bonus.luxury";

        private CampaignItem _item;
        private CampaignRenderer _renderer;
        private CampaignRuntimeController _controller;

        [SetUp]
        public void SetUp()
        {
            _item = JsonConvert.DeserializeObject<CampaignItem>(File.ReadAllText(Path.GetFullPath(FixturePath)));
            _renderer = new CampaignRenderer(new RecordingActions(), new FakeImageSource(), new FakeFontSource());
            _controller = new CampaignRuntimeController();
        }

        [TearDown]
        public void TearDown() => _controller.Dispose();

        private VisualElement Render(Dictionary<string, string> playerData)
        {
            var root = _renderer.RenderCampaign(CampaignPlayerData.Merge(_item, playerData), _controller, 2);
            Assert.NotNull(root, "the limited bonus payload must render");
            return root;
        }

        private static List<string> Texts(VisualElement root) =>
            root.Query<TextElement>().ToList()
                .Where(t => t.resolvedStyle.display != DisplayStyle.None && IsShown(t))
                .Select(t => t.text).ToList();

        // visible_if removes hidden nodes from the tree rather than hiding them, but check
        // ancestors too in case a future renderer toggles display instead.
        private static bool IsShown(VisualElement element)
        {
            for (var e = element; e != null; e = e.parent)
            {
                if (e.style.display == DisplayStyle.None) return false;
            }
            return true;
        }

        [Test]
        public void Payload_Validates_AsSchemaV2WithDeclaredCounts()
        {
            Assert.IsTrue(CampaignValidator.TryValidate(_item, out var error), error);
            Assert.AreEqual(2, _item.SchemaVersion);
            CollectionAssert.AreEquivalent(new[] { PremiumBought, LuxuryBought }, _item.PlayerData.Keys);
            Assert.IsTrue(_item.Borderless, "self-framed hero art, no SDK card");
        }

        [Test]
        public void Renders_HeroCountdownItemsAndDiscounts()
        {
            var root = Render(null);
            var texts = Texts(root);

            CollectionAssert.IsSubsetOf(
                new[] { "Limited Bonus", "Premium Pack", "Luxury Pack", "Ultimate Pack", "80", "10,000", "500%", "240%" },
                texts);
            Assert.IsTrue(texts.Any(t => t.StartsWith("Time left: ")), "countdown with its prefix");
            Assert.AreEqual(1, root.Query<Label>("campaign-countdown").ToList().Count);
            // USD fallback prices on the unlabeled buy buttons.
            CollectionAssert.IsSubsetOf(new[] { "$11.99", "$22.99", "$58.99" }, texts);
        }

        [Test]
        public void Limit_ShowsCount_AndBuyUntilReached()
        {
            var texts = Texts(Render(new Dictionary<string, string> { { PremiumBought, "0" }, { LuxuryBought, "2" } }));

            CollectionAssert.Contains(texts, "Limit: 0/1");
            CollectionAssert.Contains(texts, "Limit: 2/3");
            CollectionAssert.Contains(texts, "$11.99");
            CollectionAssert.Contains(texts, "$22.99");
            CollectionAssert.DoesNotContain(texts, "Sold out");
        }

        [Test]
        public void Limit_Reached_SwapsBuyForSoldOut_OnlyOnThatCard()
        {
            var texts = Texts(Render(new Dictionary<string, string> { { PremiumBought, "1" }, { LuxuryBought, "1" } }));

            CollectionAssert.Contains(texts, "Limit: 1/1");
            Assert.AreEqual(1, texts.Count(t => t == "Sold out"));
            CollectionAssert.DoesNotContain(texts, "$11.99", "premium's buy button is gone");
            CollectionAssert.Contains(texts, "$22.99", "luxury is still under its limit");
            CollectionAssert.Contains(texts, "$58.99", "ultimate has no limit");
        }
    }
}
