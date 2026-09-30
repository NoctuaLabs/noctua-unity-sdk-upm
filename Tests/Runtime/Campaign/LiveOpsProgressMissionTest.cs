using System.Collections;
using System.Collections.Generic;
using System.IO;
using com.noctuagames.sdk;
using com.noctuagames.sdk.LiveOpsCampaign;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static Tests.Runtime.Campaign.LiveOpsProgressTestKit;

namespace Tests.Runtime.Campaign
{
    /// <summary>
    /// Daily missions keep progress per campaign: tracker key <c>&lt;progress_scope&gt;.&lt;mission&gt;</c>.
    /// The game names the mission; the SDK finds the campaigns that have it.
    /// </summary>
    [TestFixture]
    public class LiveOpsProgressMissionTest
    {
        private const string BaseUrl = "http://localhost:7786/api/v1";
        private const string ServerUrl = "http://localhost:7786/api/v1/";
        private const string FixturePath = "Packages/com.noctuagames.sdk/Tests/Runtime/Campaign/Fixtures/daily-missions.json";

        private const string ScopeA = "cmtlflaet00040k01femx9wyi";
        private const string ScopeB = "cmu0000000000000000000000";
        private const string List = "/progress/42";
        private static readonly string KeyA = $"/progress/42/{ScopeA}.use_stm";
        private static readonly string KeyB = $"/progress/42/{ScopeB}.use_stm";
        private const string Unscoped = "/progress/42/use_stm";

        /// <summary>The mock records full request paths, base URL path included.</summary>
        private const string BaseUrlPath = "/api/v1";

        private HttpMockServer _server;
        private PanelSettings _panel;
        private NoctuaLocale _locale;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _server = new HttpMockServer(ServerUrl);
            _server.Start();
            _locale = new NoctuaLocale();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() => _server.Dispose();

        [SetUp]
        public void SetUp()
        {
            ResetServer(_server, new[] { List, KeyA, KeyB, Unscoped, KeyA + "/claim", KeyB + "/claim" });
            ClearSaved();
            _panel = ScriptableObject.CreateInstance<PanelSettings>();
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            DestroyUiRoots();
            Object.DestroyImmediate(_panel);
            ClearSaved();
        }

        private static CampaignItem Missions(string id, string scope)
        {
            var item = JsonConvert.DeserializeObject<CampaignItem>(File.ReadAllText(Path.GetFullPath(FixturePath)));
            item.Id = id;
            item.ProgressScope = scope;
            item.AutoShow = false;
            item.Frequency = null;
            return item;
        }

        private NoctuaLiveOpsCampaign Facade(params CampaignItem[] campaigns) =>
            NewFacade(
                BaseUrl,
                new CampaignConfig { SchemaVersion = 2, Campaigns = new List<CampaignItem>(campaigns) },
                panel: _panel,
                locale: _locale);

        private void Up(string path, string body) => _server.AddHandler(path, _ => Envelope(body));

        private void Refuse(string path) =>
            _server.AddHandler(path, _ => (404, Error(2300, "Progress key is not configured for this game")));

        [Test]
        public void TrackerKey_IsScopedByTheCampaign()
        {
            Assert.AreEqual($"{ScopeA}.use_stm", NoctuaLiveOpsCampaign.TrackerKey(Missions("a", ScopeA), "use_stm"));
            Assert.AreEqual("use_stm", NoctuaLiveOpsCampaign.TrackerKey(Missions("a", null), "use_stm"),
                "older payloads without a scope keep the mission key");
            Assert.IsTrue(NoctuaLiveOpsCampaign.HasMission(Missions("a", ScopeA), "use_stm"));
            Assert.IsFalse(NoctuaLiveOpsCampaign.HasMission(Missions("a", ScopeA), "unknown"));
        }

        [UnityTest]
        public IEnumerator Popup_ShowsOnlyItsOwnCampaignsProgress() => UniTask.ToCoroutine(async () =>
        {
            Up(List, Rows(
                Row($"{ScopeA}.use_stm", 7, 20),
                Row($"{ScopeB}.use_stm", 15, 20),
                Row("use_stm", 1, 20)));
            var facade = Facade(Missions("season_a", ScopeA), Missions("season_b", ScopeB));

            facade.ShowPopup("season_a");
            await UniTask.WaitUntil(() => facade.ShownCampaignId != null).Timeout(System.TimeSpan.FromSeconds(5));

            Assert.AreEqual("7", facade.ShownPlayerData["m_use_stm_progress"]);
        });

        [UnityTest]
        public IEnumerator SetMission_UpdatesEveryCampaignWithTheMission() => UniTask.ToCoroutine(async () =>
        {
            Up(KeyA, Row($"{ScopeA}.use_stm", 3, 20));
            Up(KeyB, Row($"{ScopeB}.use_stm", 3, 20));
            var facade = Facade(Missions("season_a", ScopeA), Missions("season_b", ScopeB));

            var stored = await facade.SetMissionProgressAsync("use_stm", 3);

            Assert.AreEqual(2, stored.Count);
            CollectionAssert.AreEquivalent(new[] { "PUT " + BaseUrlPath + KeyA, "PUT " + BaseUrlPath + KeyB }, Sent(_server));
            Assert.IsFalse(facade.HasPendingProgress);
        });

        [UnityTest]
        public IEnumerator SetMission_OneCampaignRefused_TheOthersStillUpdate() => UniTask.ToCoroutine(async () =>
        {
            Refuse(KeyA);
            Up(KeyB, Row($"{ScopeB}.use_stm", 3, 20));
            var facade = Facade(Missions("season_a", ScopeA), Missions("season_b", ScopeB));

            var stored = await facade.SetMissionProgressAsync("use_stm", 3);

            Assert.AreEqual(1, stored.Count);
            Assert.AreEqual($"{ScopeB}.use_stm", stored[0].Key);
            Assert.IsFalse(facade.HasPendingProgress, "a refused key is dropped, never retried");
        });

        [UnityTest]
        public IEnumerator SetMission_EveryCampaignRefused_Throws() => UniTask.ToCoroutine(async () =>
        {
            Refuse(KeyA);
            var facade = Facade(Missions("season_a", ScopeA));

            var e = await Throws(() => facade.SetMissionProgressAsync("use_stm", 3));

            Assert.AreEqual((int)NoctuaErrorCode.LiveOpsProgressKeyNotFound, e.ErrorCode);
        });

        [UnityTest]
        public IEnumerator SetMission_NoCampaignHasIt_SendsNothing() => UniTask.ToCoroutine(async () =>
        {
            var facade = Facade(Missions("season_a", ScopeA));

            var stored = await facade.SetMissionProgressAsync("not_a_mission", 3);

            Assert.IsEmpty(stored);
            Assert.AreEqual(0, _server.Requests.Count + _server.Unmatched.Count);
        });

        [UnityTest]
        public IEnumerator SetMission_WithoutAScope_UsesTheMissionKey() => UniTask.ToCoroutine(async () =>
        {
            Up(Unscoped, Row("use_stm", 3, 20));
            var facade = Facade(Missions("season_a", null));

            var stored = await facade.SetMissionProgressAsync("use_stm", 3);

            Assert.AreEqual("use_stm", stored[0].Key);
            CollectionAssert.AreEqual(new[] { "PUT " + BaseUrlPath + Unscoped }, Sent(_server));
        });

        [UnityTest]
        public IEnumerator ClaimMission_ClaimsThatCampaignsKey() => UniTask.ToCoroutine(async () =>
        {
            Up(KeyB + "/claim", Row($"{ScopeB}.use_stm", 20, 20, completed: true, claimed: true));
            var facade = Facade(Missions("season_a", ScopeA), Missions("season_b", ScopeB));

            var claimed = await facade.ClaimMissionAsync("season_b", "use_stm");

            Assert.IsTrue(claimed.Claimed);
            CollectionAssert.AreEqual(new[] { "POST " + BaseUrlPath + KeyB + "/claim" }, Sent(_server));
        });

        [Test]
        public void ClaimMission_UnknownCampaignOrMission_Throws()
        {
            var facade = Facade(Missions("season_a", ScopeA));

            var unknownCampaign = Assert.Throws<NoctuaException>(() => facade.ClaimMissionAsync("nope", "use_stm"));
            var unknownMission = Assert.Throws<NoctuaException>(() => facade.ClaimMissionAsync("season_a", "nope"));

            Assert.AreEqual((int)NoctuaErrorCode.Application, unknownCampaign.ErrorCode);
            Assert.AreEqual((int)NoctuaErrorCode.Application, unknownMission.ErrorCode);
            Assert.AreEqual(0, _server.Requests.Count + _server.Unmatched.Count);
        }
    }
}
