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
    /// Contract test against the payload noctua-admin's free-form popup builder emits (its
    /// golden fixture, copied into Fixtures/): an image card, an image card with a caption
    /// overlay, a player-data progress bar, a claim button gated by visible_if with keep_open,
    /// and a tinted backdrop. Proves the SDK parses, validates and renders it.
    /// </summary>
    [TestFixture]
    public class CampaignCustomBuilderPayloadTest
    {
        private const string FixturePath =
            "Packages/com.noctuagames.sdk/Tests/Runtime/Campaign/Fixtures/custom-cards.json";

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

        private VisualElement RenderWith(Dictionary<string, string> playerData)
        {
            var root = _renderer.RenderCampaign(CampaignPlayerData.Merge(_item, playerData), _controller, 1);
            Assert.NotNull(root, "the builder's payload must render");
            return root;
        }

        [Test]
        public void Payload_Validates()
        {
            Assert.IsTrue(CampaignValidator.TryValidate(_item, out var error), error);
        }

        [Test]
        public void Payload_CarriesBackdropAndFrame()
        {
            Assert.IsTrue(CampaignPopupStyle.TryResolveBackdrop(_item, out var backdrop));
            Assert.AreEqual(0xD9 / 255f, backdrop.a, 0.001f);
            Assert.IsFalse(_item.Borderless);
            Assert.IsTrue(CampaignPopupStyle.TryResolveFrame(_item, out _));
        }

        [Test]
        public void Renders_CopyFromDataTokens_AndBothImageCards()
        {
            var root = RenderWith(null);
            var labels = root.Query<Label>().ToList().Select(l => l.text).ToList();

            CollectionAssert.IsSubsetOf(
                new[] { "EVENT HUB", "Weekend Tournament", "Top 100 win gems", "Season 4 is live" }, labels);
            Assert.AreEqual(2, root.Query<VisualElement>("campaign-image").ToList().Count);
        }

        [Test]
        public void ClaimButton_HiddenUntilPointsReached_ThenKeepsPopupOpen()
        {
            Assert.AreEqual(0, RenderWith(null).Query<Button>().ToList().Count);

            var root = RenderWith(new Dictionary<string, string> { { "points", "100" } });
            var claim = root.Query<Button>().ToList().Single();
            Assert.AreEqual("Claim chest", claim.text);

            _actions.Calls.Clear();
            using (var fixture = new CampaignPanelFixture())
            {
                fixture.Add(root);
                using var click = ClickEvent.GetPooled();
                click.target = claim;
                claim.SendEvent(click);
            }
            Assert.AreEqual("missions/milestone/100", _actions.Calls.Single().action.Deeplink);
            Assert.IsTrue(_actions.Calls.Single().action.KeepOpen);
        }

        [Test]
        public void ProgressBar_ReadsPlayerPoints()
        {
            var bar = RenderWith(new Dictionary<string, string> { { "points", "40" } })
                .Query<ProgressBar>().ToList().Single();

            Assert.AreEqual(40f, bar.value);
            Assert.AreEqual("40/100", bar.title);
        }
    }
}
