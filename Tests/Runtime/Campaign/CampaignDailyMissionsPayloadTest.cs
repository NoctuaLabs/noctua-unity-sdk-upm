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
    /// Contract test against the payload noctua-admin actually emits for a Daily Missions
    /// campaign (its golden fixture, copied into Fixtures/). Proves the SDK parses, validates
    /// and renders each mission state from game-supplied player data — the end-to-end path the
    /// template exists for.
    /// </summary>
    [TestFixture]
    public class CampaignDailyMissionsPayloadTest
    {
        private const string FixturePath =
            "Packages/com.noctuagames.sdk/Tests/Runtime/Campaign/Fixtures/daily-missions.json";

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
            var merged = CampaignPlayerData.Merge(_item, playerData);
            var root = _renderer.RenderCampaign(merged, _controller, 1);
            Assert.NotNull(root, "the admin's daily missions payload must render");
            return root;
        }

        private static List<string> Texts<T>(VisualElement root) where T : TextElement =>
            root.Query<T>().ToList().Select(e => e.text).ToList();

        [Test]
        public void Payload_DeclaresSchemaV2AndPlayerData()
        {
            Assert.AreEqual(2, _item.SchemaVersion);
            Assert.LessOrEqual(_item.SchemaVersion, CampaignRenderer.SupportedSchemaVersion);
            Assert.IsTrue(_item.PlayerData.ContainsKey("m_use_stm_progress"));
            Assert.IsFalse(_item.Borderless);
        }

        [Test]
        public void Payload_Validates()
        {
            Assert.IsTrue(CampaignValidator.TryValidate(_item, out var error), error);
        }

        [Test]
        public void NotStarted_ShowsGoForEveryMission_AndNoClaim()
        {
            var buttons = Texts<Button>(RenderWith(null));

            Assert.AreEqual(3, buttons.Count(t => t == "Go"));
            Assert.IsFalse(buttons.Contains("Claim"));
            Assert.IsFalse(Texts<Label>(RenderWith(null)).Contains("Claimed"));
        }

        [Test]
        public void TargetReached_ShowsClaim_WithKeepOpen()
        {
            var root = RenderWith(new Dictionary<string, string> { { "m_use_stm_progress", "20" } });
            var buttons = Texts<Button>(root);

            Assert.AreEqual(1, buttons.Count(t => t == "Claim"));
            Assert.AreEqual(2, buttons.Count(t => t == "Go"));

            var claim = _item.View;
            var claimAction = Flatten(claim).Select(n => n.Action)
                .First(a => a != null && a.Deeplink == "missions/claim/use_stm");
            Assert.IsTrue(claimAction.KeepOpen);
        }

        [Test]
        public void Claimed_ShowsClaimedLabel_AndNoButtonsForThatMission()
        {
            var root = RenderWith(new Dictionary<string, string>
            {
                { "m_use_stm_progress", "20" }, { "m_use_stm_claimed", "true" },
            });

            Assert.AreEqual(2, Texts<Button>(root).Count(t => t == "Go"));
            Assert.IsFalse(Texts<Button>(root).Contains("Claim"));
            Assert.IsTrue(Texts<Label>(root).Contains("Claimed"));
        }

        [Test]
        public void ProgressBars_ShowPlayerProgress()
        {
            var root = RenderWith(new Dictionary<string, string> { { "m_defeat_enemies_progress", "1500" } });
            var bar = root.Query<ProgressBar>().ToList().First(pb => pb.title == "1500/2000");

            Assert.AreEqual(2000f, bar.highValue);
            Assert.AreEqual(1500f, bar.value);
        }

        [Test]
        public void ResetCountdown_HiddenUntilGameSuppliesResetAt()
        {
            Assert.AreEqual(0, RenderWith(null).Query<Label>("campaign-countdown").ToList().Count);

            var withReset = RenderWith(new Dictionary<string, string> { { "reset_at", "2099-01-01T00:00:00Z" } });
            Assert.AreEqual(1, withReset.Query<Label>("campaign-countdown").ToList().Count);
        }

        [Test]
        public void MilestoneChest_BecomesClaimableAtItsPoints()
        {
            var locked = CountMilestoneActions(RenderWith(new Dictionary<string, string> { { "points", "10" } }));
            var reached = CountMilestoneActions(RenderWith(new Dictionary<string, string> { { "points", "50" } }));

            Assert.AreEqual(0, locked);
            Assert.AreEqual(2, reached, "chests at 25 and 50 are claimable at 50 points");
        }

        private int CountMilestoneActions(VisualElement root)
        {
            // Claimable chests are the only milestone nodes carrying an action; each wires one
            // click callback, so count dispatches by clicking every image.
            _actions.Calls.Clear();
            using (var fixture = new CampaignPanelFixture())
            {
                fixture.Add(root);
                foreach (var image in root.Query<VisualElement>("campaign-image").ToList())
                {
                    using var click = ClickEvent.GetPooled();
                    click.target = image;
                    image.SendEvent(click);
                }
            }
            return _actions.Calls.Count(c => c.action.Deeplink.StartsWith("missions/milestone/"));
        }

        private static IEnumerable<CampaignNode> Flatten(CampaignNode node)
        {
            if (node == null) yield break;
            yield return node;
            if (node.Children == null) yield break;
            foreach (var child in node.Children)
                foreach (var n in Flatten(child)) yield return n;
        }
    }
}
