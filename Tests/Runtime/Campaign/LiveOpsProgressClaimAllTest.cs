using System.Collections;
using System.Collections.Generic;
using System.IO;
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
            return item;
        }

        private NoctuaLiveOpsCampaign Facade(string baseUrl = BaseUrl) =>
            NewFacade(baseUrl, new CampaignConfig { SchemaVersion = 2, Campaigns = new List<CampaignItem> { Missions() } });

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
            var facade = Facade();
            IReadOnlyList<string> reported = null;
            facade.OnMissionsClaimed += (_, missions) => reported = missions;

            var claimed = await facade.ClaimAllCompletedAsync("season_4");

            CollectionAssert.AreEquivalent(new[] { "use_stm", "craft_badge" }, claimed);
            CollectionAssert.AreEquivalent(claimed, reported);
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

            var claimed = await Facade().ClaimAllCompletedAsync("season_4");

            CollectionAssert.AreEqual(new[] { "craft_badge" }, claimed, "only rewards claimed now are granted");
        });

        [UnityTest]
        public IEnumerator ClaimAll_NothingToClaim_ReturnsEmpty_AndFiresNothing() => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler(List, _ => Envelope(Rows(Row($"{Scope}.use_stm", 3, 20))));
            var facade = Facade();
            var fired = false;
            facade.OnMissionsClaimed += (_, __) => fired = true;

            Assert.IsEmpty(await facade.ClaimAllCompletedAsync("season_4"));
            Assert.IsFalse(fired);
        });

        [UnityTest]
        public IEnumerator ClaimAll_UnknownCampaign_Throws() => UniTask.ToCoroutine(async () =>
        {
            var e = await Throws(async () => await Facade().ClaimAllCompletedAsync("nope"));
            Assert.AreEqual((int)NoctuaErrorCode.Application, e.ErrorCode);
        });

        [UnityTest]
        public IEnumerator ClaimAll_TrackerDown_Throws() => UniTask.ToCoroutine(async () =>
        {
            var e = await Throws(async () => await Facade(UnreachableBaseUrl).ClaimAllCompletedAsync("season_4"));
            Assert.AreEqual((int)NoctuaErrorCode.Networking, e.ErrorCode);
        });
    }
}
