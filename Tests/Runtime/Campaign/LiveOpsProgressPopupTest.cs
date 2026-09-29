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
    /// Section E of the behaviour matrix: a daily missions popup is never shown with made-up
    /// progress. Renders real popups (daily missions and multi offer fixtures) on a real panel.
    /// </summary>
    [TestFixture]
    public class LiveOpsProgressPopupTest
    {
        private const string BaseUrl = "http://localhost:7785/api/v1";
        private const string ServerUrl = "http://localhost:7785/api/v1/";
        private const string List = "/progress/42";
        private const string UseStm = "/progress/42/use_stm";
        private const string FixtureDir = "Packages/com.noctuagames.sdk/Tests/Runtime/Campaign/Fixtures/";

        private const string MissionsId = "daily_missions_season_4";
        private const string OfferId = "monthly_pass";

        private HttpMockServer _server;
        private PanelSettings _panel;
        private NoctuaLocale _locale;
        private List<string> _notices;

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
            ResetServer(_server, new[] { List, UseStm });
            ClearSaved();
            _panel = ScriptableObject.CreateInstance<PanelSettings>();
            _notices = new List<string>();
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

        private static CampaignItem Fixture(string name, bool autoShow, int priority)
        {
            var item = JsonConvert.DeserializeObject<CampaignItem>(
                File.ReadAllText(Path.GetFullPath(FixtureDir + name)));
            item.AutoShow = autoShow;
            item.Priority = priority;
            item.Frequency = null;
            return item;
        }

        private NoctuaLiveOpsCampaign Facade(
            bool missionsAutoShow = false,
            bool offerAutoShow = false,
            bool withOffer = true,
            System.Func<long?> playerId = null)
        {
            var campaigns = new List<CampaignItem> { Fixture("daily-missions.json", missionsAutoShow, 10) };
            if (withOffer) campaigns.Add(Fixture("multi-offer.json", offerAutoShow, 1));

            return NewFacade(
                BaseUrl,
                new CampaignConfig { SchemaVersion = 2, Campaigns = campaigns },
                playerId: playerId,
                notice: _notices.Add,
                panel: _panel,
                locale: _locale);
        }

        private void Up(string path, string body) => _server.AddHandler(path, _ => Envelope(body));
        private void Down(string path) => _server.AddHandler(path, LiveOpsProgressTestKit.Down);

        /// <summary>A saved copy for player 42: use_stm = 3/20.</summary>
        private async UniTask SaveCopy()
        {
            Up(List, Rows(Row("use_stm", 3, 20)));
            await NewFacade(BaseUrl).GetProgressAsync();
            ResetServer(_server, new[] { List });
            DestroyUiRoots();
        }

        [UnityTest]
        public IEnumerator E1_AutoShow_TrackerUp_ShowsRealProgress() => UniTask.ToCoroutine(async () =>
        {
            Up(List, Rows(Row("use_stm", 12, 20)));
            var facade = Facade(missionsAutoShow: true);

            Assert.IsTrue(await facade.RunAutoShowAsync());

            Assert.AreEqual(MissionsId, facade.ShownCampaignId);
            Assert.AreEqual("12", facade.ShownPlayerData["m_use_stm_progress"]);
            Assert.AreEqual("false", facade.ShownPlayerData["m_use_stm_claimed"]);
        });

        [UnityTest]
        public IEnumerator E2_AutoShow_Down_WithACopy_ShowsTheCopy() => UniTask.ToCoroutine(async () =>
        {
            await SaveCopy();
            Down(List);
            var facade = Facade(missionsAutoShow: true);

            Assert.IsTrue(await facade.RunAutoShowAsync());

            Assert.AreEqual(MissionsId, facade.ShownCampaignId);
            Assert.AreEqual("3", facade.ShownPlayerData["m_use_stm_progress"]);
        });

        [UnityTest]
        public IEnumerator E3_AutoShow_Down_NoCopy_SkipsToTheNextCampaign() => UniTask.ToCoroutine(async () =>
        {
            Down(List);
            var facade = Facade(missionsAutoShow: true, offerAutoShow: true);

            Assert.IsTrue(await facade.RunAutoShowAsync());

            Assert.AreEqual(OfferId, facade.ShownCampaignId, "missions skipped silently, the offer shows");
            Assert.IsEmpty(_notices, "an auto-show never explains itself");
        });

        [UnityTest]
        public IEnumerator E3b_AutoShow_BeforeLogin_RetriesAfterLogin() => UniTask.ToCoroutine(async () =>
        {
            Up(List, Rows(Row("use_stm", 5, 20)));
            long? player = null;
            var facade = Facade(missionsAutoShow: true, withOffer: false, playerId: () => player);

            Assert.IsFalse(await facade.RunAutoShowAsync(), "nobody logged in yet: nothing shown");
            Assert.IsNull(facade.ShownCampaignId);

            player = PlayerId;
            await facade.OnAccountChangedAsync();

            Assert.AreEqual(MissionsId, facade.ShownCampaignId);
            Assert.AreEqual("5", facade.ShownPlayerData["m_use_stm_progress"]);
        });

        [UnityTest]
        public IEnumerator E4_OpenedByThePlayer_Down_NoCopy_ExplainsInsteadOfShowing() => UniTask.ToCoroutine(async () =>
        {
            Down(List);
            var facade = Facade();
            var unavailable = new List<string>();
            facade.OnCampaignUnavailable += unavailable.Add;

            facade.ShowPopup(MissionsId);
            await UniTask.WaitUntil(() => unavailable.Count > 0).Timeout(System.TimeSpan.FromSeconds(5));

            Assert.IsNull(facade.ShownCampaignId, "no popup with made-up zeros");
            CollectionAssert.AreEqual(new[] { MissionsId }, unavailable);
            Assert.AreEqual(1, _notices.Count);
            StringAssert.Contains("unavailable", _notices[0].ToLower());
        });

        [UnityTest]
        public IEnumerator E5_AnOpenPopup_RefreshesInPlace() => UniTask.ToCoroutine(async () =>
        {
            Up(List, Rows(Row("use_stm", 3, 20)));
            var facade = Facade();
            facade.ShowPopup(MissionsId);
            await UniTask.WaitUntil(() => facade.ShownCampaignId != null).Timeout(System.TimeSpan.FromSeconds(5));
            Assert.AreEqual("3", facade.ShownPlayerData["m_use_stm_progress"]);
            Up(UseStm, Row("use_stm", 8, 20));

            await facade.SetProgressAsync("use_stm", 8);

            Assert.AreEqual(MissionsId, facade.ShownCampaignId, "still the same popup");
            Assert.AreEqual("8", facade.ShownPlayerData["m_use_stm_progress"]);
        });

        [UnityTest]
        public IEnumerator E6_TheGamesOwnValues_Win() => UniTask.ToCoroutine(async () =>
        {
            Up(List, Rows(Row("use_stm", 3, 20)));
            var facade = Facade();

            facade.ShowPopup(MissionsId, new Dictionary<string, string> { { "m_use_stm_progress", "19" } });
            await UniTask.WaitUntil(() => facade.ShownCampaignId != null).Timeout(System.TimeSpan.FromSeconds(5));

            Assert.AreEqual("19", facade.ShownPlayerData["m_use_stm_progress"]);
            Assert.AreEqual("false", facade.ShownPlayerData["m_use_stm_claimed"], "tracker fills what the game left out");
        });

        [UnityTest]
        public IEnumerator E7_OtherCampaigns_NeverTouchTheTracker() => UniTask.ToCoroutine(async () =>
        {
            Down(List);
            var facade = Facade();

            facade.ShowPopup(OfferId);
            await UniTask.Yield();

            Assert.AreEqual(OfferId, facade.ShownCampaignId);
            Assert.AreEqual(0, _server.Requests.Count + _server.Unmatched.Count);
        });

        [Test]
        public void ProgressBacked_MeansDeclaringMissionProgressKeys()
        {
            Assert.IsTrue(NoctuaLiveOpsCampaign.IsProgressBacked(Fixture("daily-missions.json", false, 0)));
            Assert.IsFalse(NoctuaLiveOpsCampaign.IsProgressBacked(Fixture("multi-offer.json", false, 0)));
            Assert.IsFalse(NoctuaLiveOpsCampaign.IsProgressBacked(null));
        }
    }
}
