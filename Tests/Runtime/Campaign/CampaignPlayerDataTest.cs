using System.Collections.Generic;
using com.noctuagames.sdk.LiveOpsCampaign;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Tests.Runtime.Campaign
{
    [TestFixture]
    public class CampaignPlayerDataTest
    {
        private static CampaignItem Item() => new CampaignItem
        {
            Id = "missions",
            EngagementType = CampaignItem.EngagementEvent,
            Data = new Dictionary<string, string> { { "title", "DAILY" } },
            PlayerData = new Dictionary<string, string> { { "progress", "0" }, { "claimed", "false" } },
        };

        [Test]
        public void PlayerData_DeserializesFromJson()
        {
            var item = JsonConvert.DeserializeObject<CampaignItem>(
                "{ \"id\": \"c\", \"player_data\": { \"progress\": \"0\" } }");
            Assert.AreEqual("0", item.PlayerData["progress"]);
        }

        [Test]
        public void Merge_LayersDataThenDefaultsThenGameValues()
        {
            var merged = CampaignPlayerData.Merge(Item(), new Dictionary<string, string> { { "progress", "7" } });

            Assert.AreEqual("DAILY", merged.Data["title"]);
            Assert.AreEqual("7", merged.Data["progress"]);
            Assert.AreEqual("false", merged.Data["claimed"]);
        }

        [Test]
        public void Merge_NullGameValues_UsesDefaults()
        {
            var merged = CampaignPlayerData.Merge(Item(), null);
            Assert.AreEqual("0", merged.Data["progress"]);
        }

        [Test]
        public void Merge_DropsUndeclaredKeys_AndReportsThem()
        {
            var dropped = new List<string>();
            var merged = CampaignPlayerData.Merge(Item(),
                new Dictionary<string, string> { { "title", "HACKED" }, { "other", "x" } },
                dropped.Add);

            Assert.AreEqual("DAILY", merged.Data["title"], "game values must not override LiveOps copy");
            Assert.IsFalse(merged.Data.ContainsKey("other"));
            CollectionAssert.AreEquivalent(new[] { "title", "other" }, dropped);
        }

        [Test]
        public void Merge_NeverMutatesTheStoredItem()
        {
            var item = Item();
            var merged = CampaignPlayerData.Merge(item, new Dictionary<string, string> { { "progress", "9" } });

            Assert.AreNotSame(item, merged);
            Assert.IsFalse(item.Data.ContainsKey("progress"));
            Assert.AreEqual("0", item.PlayerData["progress"]);
            Assert.AreEqual("missions", merged.Id);
        }

        [Test]
        public void Merge_FoldsDeclarationIntoData()
        {
            // The merged copy is validated and rendered again; a leftover declaration would
            // collide with the keys now living in Data.
            Assert.IsNull(CampaignPlayerData.Merge(Item(), null).PlayerData);
        }

        [Test]
        public void ValidationData_IncludesDefaults()
        {
            var data = CampaignPlayerData.ValidationData(Item());
            Assert.AreEqual("0", data["progress"]);
            Assert.AreEqual("DAILY", data["title"]);
        }

        [Test]
        public void Validator_AcceptsDeclaredRuntimeTokens()
        {
            var item = Item();
            item.View = CampaignFactory.Node(CampaignNode.TypeText,
                new Dictionary<string, object> { { "text", "{{progress}}/20" } });

            Assert.IsTrue(CampaignValidator.TryValidate(item, out var error), error);
        }

        [Test]
        public void Validator_StillRejectsUndeclaredTokens()
        {
            var item = Item();
            item.View = CampaignFactory.Node(CampaignNode.TypeText,
                new Dictionary<string, object> { { "text", "{{unknown}}" } });

            Assert.IsFalse(CampaignValidator.TryValidate(item, out _));
        }

        [Test]
        public void Validator_RejectsPlayerDataCollidingWithData()
        {
            var item = Item();
            item.PlayerData["title"] = "x";
            item.View = CampaignFactory.Node(CampaignNode.TypeSpacer);

            Assert.IsFalse(CampaignValidator.TryValidate(item, out var error));
            StringAssert.Contains("title", error);
        }

        [Test]
        public void Validator_SkipsHiddenSubtrees()
        {
            // A countdown with a blank end is invalid — but not while its condition hides it.
            var item = Item();
            item.PlayerData["reset_at"] = "";
            var countdown = CampaignFactory.Node(CampaignNode.TypeCountdown,
                new Dictionary<string, object> { { "end_timestamp", "{{reset_at}}" } });
            countdown.VisibleIf = new CampaignCondition
            {
                All = new List<CampaignConditionClause>
                {
                    new CampaignConditionClause { Left = "{{reset_at}}", Op = "ne", Right = "" },
                },
            };
            item.View = CampaignFactory.Node(CampaignNode.TypeContainer, children: new[] { countdown });

            Assert.IsTrue(CampaignValidator.TryValidate(item, out var error), error);
        }

        [Test]
        public void Validator_RejectsMalformedVisibleIf()
        {
            var item = Item();
            item.View = CampaignFactory.Node(CampaignNode.TypeSpacer);
            item.View.VisibleIf = new CampaignCondition
            {
                All = new List<CampaignConditionClause>
                {
                    new CampaignConditionClause { Left = "a", Op = "between", Right = "b" },
                },
            };

            Assert.IsFalse(CampaignValidator.TryValidate(item, out _));
        }
    }
}
