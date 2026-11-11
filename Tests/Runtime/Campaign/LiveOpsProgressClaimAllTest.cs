using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using com.noctuagames.sdk;
using com.noctuagames.sdk.LiveOpsCampaign;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine.TestTools;
using static Tests.Runtime.Campaign.LiveOpsProgressTestKit;

namespace Tests.Runtime.Campaign
{
    /// <summary>
    /// Claim All: every completed, unclaimed mission of one campaign is claimed in one call, each
    /// as its own tracker claim.
    /// </summary>
    [TestFixture]
    public class LiveOpsProgressClaimAllTest
    {
        private const string BaseUrl = "http://localhost:7788/api/v1";
        private const string ServerUrl = "http://localhost:7788/api/v1/";
        private const string FixturePath = "Packages/com.noctuagames.sdk/Tests/Runtime/Campaign/Fixtures/daily-missions.json";
        private const string Scope = "season";
        private const string List = "/progress/42";
        private static readonly string Stm = $"/progress/42/{Scope}.use_stm/claim";
        private static readonly string Badge = $"/progress/42/{Scope}.craft_badge/claim";
        private static readonly string Enemies = $"/progress/42/{Scope}.defeat_enemies/claim";

        private HttpMockServer _server;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _server = new HttpMockServer(ServerUrl);
            _server.Start();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() => _server.Dispose();

        [SetUp]
        public void SetUp()
        {
            ResetServer(_server, new[] { List, Stm, Badge, Enemies });
            ClearSaved();
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            DestroyUiRoots();
            ClearSaved();
        }

        private static CampaignItem Missions()
        {
            var item = JsonConvert.DeserializeObject<CampaignItem>(File.ReadAllText(Path.GetFullPath(FixturePath)));
            item.Id = "season_4";
            item.ProgressScope = Scope;
            item.AutoShow = false;
            item.Frequency = null;
            item.Data["m_use_stm_reward_id"] = "gem";
            return item;
        }

        private NoctuaLiveOpsCampaign Facade(string baseUrl = BaseUrl) =>
            NewFacade(baseUrl, new CampaignConfig { SchemaVersion = 2, Campaigns = new List<CampaignItem> { Missions() } });

        /// <summary>A facade whose MissionRewards handler records what it is given.</summary>
        private NoctuaLiveOpsCampaign Rewarded(List<MissionReward> delivered)
        {
            var facade = Facade();
            facade.Configure(new CampaignHandlers
            {
                MissionRewards = rewards => { delivered.AddRange(rewards); return UniTask.CompletedTask; },
            });
            return facade;
        }

        private static string[] Keys(IEnumerable<MissionReward> rewards) => rewards.Select(r => r.MissionKey).ToArray();

        private void Claimed(string path, string key) =>
            _server.AddHandler(path, _ => Envelope(Row(key, 20, 20, completed: true, claimed: true)));

        [Test]
        public void ClaimableMissions_OnlyCompletedUnclaimedDeclaredMissions()
        {
            var progress = new List<LiveOpsProgress>
            {
                new() { Key = $"{Scope}.use_stm", Value = 20, Max = 20, Completed = true },
                new() { Key = $"{Scope}.craft_badge", Value = 1, Max = 1, Completed = true, Claimed = true },
                new() { Key = $"{Scope}.defeat_enemies", Value = 3, Max = 2000 },
                new() { Key = $"{Scope}.not_in_popup", Value = 1, Max = 1, Completed = true },
                new() { Key = "other.use_stm", Value = 20, Max = 20, Completed = true },
            };

            CollectionAssert.AreEqual(new[] { "use_stm" }, NoctuaLiveOpsCampaign.ClaimableMissions(Missions(), progress));
        }

        [Test]
        public void PlayerData_CountsClaimableMissions()
        {
            var data = NoctuaLiveOpsCampaign.ToDailyMissionsPlayerData(new List<LiveOpsProgress>
            {
                new() { Key = "use_stm", Value = 20, Max = 20, Completed = true },
                new() { Key = "craft_badge", Value = 1, Max = 1, Completed = true },
                new() { Key = "defeat_enemies", Value = 3, Max = 2000 },
            });

            Assert.AreEqual("2", data[NoctuaLiveOpsCampaign.ClaimableCountKey]);
        }

        [UnityTest]
        public IEnumerator ClaimAll_ClaimsEveryCompletedMission_AndReportsThem() => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler(List, _ => Envelope(Rows(
                Row($"{Scope}.use_stm", 20, 20, completed: true),
                Row($"{Scope}.craft_badge", 1, 1, completed: true),
                Row($"{Scope}.defeat_enemies", 3, 2000))));
            Claimed(Stm, $"{Scope}.use_stm");
            Claimed(Badge, $"{Scope}.craft_badge");
            var delivered = new List<MissionReward>();
            var facade = Rewarded(delivered);

            var claimed = await facade.ClaimAllMissionsAsync("season_4");
            await facade.DeliverMissionRewardsAsync();

            CollectionAssert.AreEquivalent(new[] { "use_stm", "craft_badge" }, Keys(claimed));
            CollectionAssert.AreEquivalent(Keys(claimed), Keys(delivered));
            var stm = delivered.Single(r => r.MissionKey == "use_stm");
            Assert.AreEqual("gem", stm.RewardId, "the reward the admin set");
            Assert.AreEqual("25", stm.Quantity);
            Assert.AreEqual($"{Scope}.use_stm", stm.ClaimId);
            Assert.IsEmpty(facade.UndeliveredMissionRewards, "delivered rewards are dropped");
            CollectionAssert.DoesNotContain(Sent(_server), "POST /api/v1" + Enemies, "an unfinished mission is not claimed");
        });

        [UnityTest]
        public IEnumerator ClaimAll_SkipsAMissionClaimedElsewhere() => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler(List, _ => Envelope(Rows(
                Row($"{Scope}.use_stm", 20, 20, completed: true),
                Row($"{Scope}.craft_badge", 1, 1, completed: true))));
            _server.AddHandler(Stm, _ => (409, Error(2400, "Progress already claimed")));
            Claimed(Badge, $"{Scope}.craft_badge");

            var claimed = await Facade().ClaimAllMissionsAsync("season_4");

            CollectionAssert.AreEqual(new[] { "craft_badge" }, Keys(claimed), "only rewards claimed now are granted");
        });

        [UnityTest]
        public IEnumerator ClaimAll_NothingToClaim_ReturnsEmpty_AndFiresNothing() => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler(List, _ => Envelope(Rows(Row($"{Scope}.use_stm", 3, 20))));
            var delivered = new List<MissionReward>();
            var facade = Rewarded(delivered);

            Assert.IsEmpty(await facade.ClaimAllMissionsAsync("season_4"));
            Assert.IsEmpty(delivered);
        });

        [UnityTest]
        public IEnumerator ClaimAll_UnknownCampaign_Throws() => UniTask.ToCoroutine(async () =>
        {
            var e = await Throws(async () => await Facade().ClaimAllMissionsAsync("nope"));
            Assert.AreEqual((int)NoctuaErrorCode.Application, e.ErrorCode);
        });

        [UnityTest]
        public IEnumerator ClaimAll_TrackerDown_Throws() => UniTask.ToCoroutine(async () =>
        {
            var e = await Throws(async () => await Facade(UnreachableBaseUrl).ClaimAllMissionsAsync("season_4"));
            Assert.AreEqual((int)NoctuaErrorCode.Networking, e.ErrorCode);
        });

        // ---- one mission + reward delivery ---------------------------------

        [UnityTest]
        public IEnumerator ClaimMission_DeliversItsReward() => UniTask.ToCoroutine(async () =>
        {
            Claimed(Stm, $"{Scope}.use_stm");
            var delivered = new List<MissionReward>();

            var reward = await Rewarded(delivered).ClaimMissionRewardAsync("season_4", "use_stm");

            Assert.AreEqual("gem", reward.RewardId);
            CollectionAssert.AreEqual(new[] { "use_stm" }, Keys(delivered));
        });

        [UnityTest]
        public IEnumerator ClaimMission_AlreadyClaimed_Throws_AndDeliversNothing() => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler(Stm, _ => (409, Error(2400, "Progress already claimed")));
            var delivered = new List<MissionReward>();

            var e = await Throws(async () => await Rewarded(delivered).ClaimMissionRewardAsync("season_4", "use_stm"));

            Assert.AreEqual((int)NoctuaErrorCode.LiveOpsProgressAlreadyClaimed, e.ErrorCode);
            Assert.IsEmpty(delivered);
        });

        [UnityTest]
        public IEnumerator Reward_HandlerThrows_IsKept_AndDeliveredInTheNextSession() => UniTask.ToCoroutine(async () =>
        {
            Claimed(Stm, $"{Scope}.use_stm");
            var first = Facade();
            first.Configure(new CampaignHandlers
            {
                MissionRewards = _ => throw new System.InvalidOperationException("inventory not loaded"),
            });

            await first.ClaimMissionRewardAsync("season_4", "use_stm");
            await first.DeliverMissionRewardsAsync();
            Assert.AreEqual(1, first.UndeliveredMissionRewards.Count);
            StringAssert.Contains("undelivered_rewards", Saved());

            // The app restarts: a new module for the same player delivers it.
            var delivered = new List<MissionReward>();
            var next = Rewarded(delivered);
            await next.DeliverMissionRewardsAsync();
            CollectionAssert.AreEqual(new[] { $"{Scope}.use_stm" }, delivered.Select(r => r.ClaimId).ToArray());

            // Delivered once: a later session gets nothing.
            var later = new List<MissionReward>();
            await Rewarded(later).DeliverMissionRewardsAsync();
            Assert.IsEmpty(later);
        });

        [UnityTest]
        public IEnumerator Reward_WithNoHandlerYet_WaitsForOne() => UniTask.ToCoroutine(async () =>
        {
            Claimed(Stm, $"{Scope}.use_stm");
            var facade = Facade();
            await facade.ClaimMissionRewardAsync("season_4", "use_stm");
            Assert.AreEqual(1, facade.UndeliveredMissionRewards.Count);

            var delivered = new List<MissionReward>();
            facade.Configure(new CampaignHandlers
            {
                MissionRewards = rewards => { delivered.AddRange(rewards); return UniTask.CompletedTask; },
            });
            await facade.DeliverMissionRewardsAsync();

            CollectionAssert.AreEqual(new[] { "use_stm" }, Keys(delivered));
        });

        [UnityTest]
        public IEnumerator Reward_IsPerPlayer() => UniTask.ToCoroutine(async () =>
        {
            Claimed(Stm, $"{Scope}.use_stm");
            await Facade().ClaimMissionRewardAsync("season_4", "use_stm");

            StringAssert.Contains("undelivered_rewards", Saved());
            StringAssert.DoesNotContain("use_stm", Saved(OtherPlayerId), "another account on this phone never gets it");
        });
    }
}
