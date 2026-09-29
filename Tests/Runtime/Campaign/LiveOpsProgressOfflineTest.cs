using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using com.noctuagames.sdk;
using com.noctuagames.sdk.LiveOpsCampaign;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using static Tests.Runtime.Campaign.LiveOpsProgressTestKit;

namespace Tests.Runtime.Campaign
{
    /// <summary>
    /// Live ops progress when the tracker is down: failure classification (A), loading with and
    /// without a saved copy (B), queued updates (C), online-only claims (D) and lifecycle (F).
    /// Letters and numbers match the behaviour matrix in the SDK MR.
    /// </summary>
    [TestFixture]
    public class LiveOpsProgressOfflineTest
    {
        private const string BaseUrl = "http://localhost:7784/api/v1";
        private const string ServerUrl = "http://localhost:7784/api/v1/";
        private const string List = "/progress/42";
        private const string Key = "/progress/42/k";
        private const string Claim = "/progress/42/k/claim";
        private const string KeyB = "/progress/42/b";

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
            ResetServer(_server, new[] { List, Key, Claim, KeyB, "/progress/43", "/progress/43/k" });
            ClearSaved();
            // Http logs every failed request as an error; those are expected here.
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            DestroyUiRoots();
            ClearSaved();
        }

        private void Up(string path, string body) => _server.AddHandler(path, _ => Envelope(body));
        private void Down(string path) => _server.AddHandler(path, LiveOpsProgressTestKit.Down);
        private void Answer(string path, int status, string body) => _server.AddHandler(path, _ => (status, body));

        /// <summary>A copy of k = 3/20 saved on disk, fetched at the clock's time.</summary>
        private async UniTask<NoctuaLiveOpsCampaign> WithSavedCopy(FakeClock clock, long value = 3)
        {
            Up(List, Rows(Row("k", value, 20)));
            var campaign = NewFacade(BaseUrl, clock: clock);
            await campaign.GetProgressAsync();
            _server.RemoveHandler(List);
            ResetServer(_server, Array.Empty<string>());
            return campaign;
        }

        // ---- A. what counts as "down" -------------------------------------------------

        [UnityTest]
        public IEnumerator A1_NoConnection_IsDown() => UniTask.ToCoroutine(async () =>
        {
            var campaign = NewFacade(UnreachableBaseUrl);

            var e = await Throws(() => campaign.GetProgressAsync());
            var queued = await campaign.SetProgressAsync("k", 1);

            Assert.AreEqual((int)NoctuaErrorCode.Networking, e.ErrorCode);
            Assert.IsTrue(queued.Pending, "an update while unreachable is queued, not thrown");
        });

        [UnityTest]
        public IEnumerator A2_SlowerThanTheTimeout_IsDown_AndReturnsQuickly() => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler(List, _ =>
            {
                Thread.Sleep(2500);
                return (200, Envelope("[]"));
            });
            var campaign = NewFacade(BaseUrl);
            var watch = Stopwatch.StartNew();

            var e = await Throws(() => campaign.GetProgressAsync());

            Assert.AreEqual((int)NoctuaErrorCode.Networking, e.ErrorCode);
            Assert.Less(watch.Elapsed.TotalSeconds, 2.4, "gives up at the 1 s test timeout, not the 20 s default");
        });

        [UnityTest]
        public IEnumerator A3_ServerErrorsAndNonEnvelopeBodies_AreDown() => UniTask.ToCoroutine(async () =>
        {
            var answers = new (int status, string body)[]
            {
                (503, "Service Unavailable"),
                (429, ""),
                (408, ""),
                (502, "<html>Bad Gateway</html>"),
                (200, "<html>captive portal</html>"),
            };

            foreach (var (status, body) in answers)
            {
                ClearSaved();
                Answer(Key, status, body);

                var progress = await NewFacade(BaseUrl).SetProgressAsync("k", 1);

                Assert.IsTrue(progress.Pending, $"HTTP {status} '{body}' should queue the update");
            }
        });

        [UnityTest]
        public IEnumerator A4_AuthErrors_AreNotDown_AndNeverShowTheSavedCopy() => UniTask.ToCoroutine(async () =>
        {
            var clock = new FakeClock();
            await WithSavedCopy(clock);
            var campaign = NewFacade(BaseUrl, clock: clock);

            foreach (var (status, code) in new[] { (401, 2100), (403, 2200) })
            {
                Answer(List, status, Error(code, "no"));
                var e = await Throws(() => campaign.GetProgressAsync());
                Assert.AreEqual(code, e.ErrorCode);
                Assert.IsFalse(campaign.IsProgressStale);
            }
        });

        [UnityTest]
        public IEnumerator A5_Refusals_AreNotDown() => UniTask.ToCoroutine(async () =>
        {
            foreach (var (status, code) in new[] { (404, 2300), (410, 2402), (400, 2001) })
            {
                ClearSaved();
                Answer(Key, status, Error(code, "refused"));
                var e = await Throws(() => NewFacade(BaseUrl).SetProgressAsync("k", 1));
                Assert.AreEqual(code, e.ErrorCode);
            }

            foreach (var (status, code) in new[] { (409, 2400), (422, 2401) })
            {
                Answer(Claim, status, Error(code, "refused"));
                var e = await Throws(() => NewFacade(BaseUrl).ClaimProgressAsync("k"));
                Assert.AreEqual(code, e.ErrorCode);
            }
        });

        // ---- B. loading ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator B1_Up_ReturnsFreshProgress_AndSavesACopy() => UniTask.ToCoroutine(async () =>
        {
            var clock = new FakeClock();
            Up(List, Rows(Row("k", 3, 20)));
            var campaign = NewFacade(BaseUrl, clock: clock);

            var progress = await campaign.GetProgressAsync();

            Assert.AreEqual(3, progress.Single().Value);
            Assert.IsFalse(campaign.IsProgressStale);
            Assert.That(campaign.ProgressFetchedAtUtc, Is.EqualTo(clock.Now));
            StringAssert.Contains("fetched_at", Saved());
        });

        [UnityTest]
        public IEnumerator B2_EmptyList_IsARealAnswer_NotDown() => UniTask.ToCoroutine(async () =>
        {
            var clock = new FakeClock();
            Up(List, "[]");
            var campaign = NewFacade(BaseUrl, clock: clock);
            Assert.AreEqual(0, (await campaign.GetProgressAsync()).Count);

            Down(List);
            var later = await campaign.GetProgressAsync();

            Assert.AreEqual(0, later.Count, "the saved empty copy is used, no throw");
            Assert.IsTrue(campaign.IsProgressStale);
        });

        [UnityTest]
        public IEnumerator B3_Down_WithACopyUnder24h_ReturnsItAsStale() => UniTask.ToCoroutine(async () =>
        {
            var clock = new FakeClock();
            await WithSavedCopy(clock);
            clock.Now = clock.Now.AddHours(23);
            Down(List);
            var campaign = NewFacade(BaseUrl, clock: clock);

            var progress = await campaign.GetProgressAsync();

            Assert.AreEqual(3, progress.Single().Value);
            Assert.IsTrue(campaign.IsProgressStale);
        });

        [UnityTest]
        public IEnumerator B4_Down_WithACopyOver24h_Throws() => UniTask.ToCoroutine(async () =>
        {
            var clock = new FakeClock();
            await WithSavedCopy(clock);
            clock.Now = clock.Now.AddHours(25);
            Down(List);

            var e = await Throws(() => NewFacade(BaseUrl, clock: clock).GetProgressAsync());

            Assert.AreEqual((int)NoctuaErrorCode.Networking, e.ErrorCode);
        });

        [UnityTest]
        public IEnumerator B5_Down_WithNoCopy_ThrowsNetworking() => UniTask.ToCoroutine(async () =>
        {
            Down(List);

            var e = await Throws(() => NewFacade(BaseUrl).GetProgressAsync());

            Assert.AreEqual((int)NoctuaErrorCode.Networking, e.ErrorCode);
        });

        [UnityTest]
        public IEnumerator B6_AuthError_LeavesTheSavedCopyUntouched() => UniTask.ToCoroutine(async () =>
        {
            var clock = new FakeClock();
            await WithSavedCopy(clock);
            var before = Saved();
            Answer(List, 401, Error(2100, "expired"));
            var campaign = NewFacade(BaseUrl, clock: clock);

            await Throws(() => campaign.GetProgressAsync());

            Assert.AreEqual(before, Saved());
            Assert.AreEqual(3, campaign.Progress.Single().Value);
        });

        [UnityTest]
        public IEnumerator B7_PendingUpdates_ShowOnTopOfTheCopy() => UniTask.ToCoroutine(async () =>
        {
            var clock = new FakeClock();
            await WithSavedCopy(clock);
            var campaign = NewFacade(BaseUrl, clock: clock);
            Down(Key);
            Down(List);

            await campaign.SetProgressAsync("k", 7);
            var progress = await campaign.GetProgressAsync();

            Assert.AreEqual(7, progress.Single().Value);
            Assert.IsTrue(progress.Single().Pending);
            Assert.AreEqual(20, progress.Single().Max);
        });

        [UnityTest]
        public IEnumerator B8_WaitingUpdates_GoOutBeforeTheLoad() => UniTask.ToCoroutine(async () =>
        {
            Down(Key);
            var campaign = NewFacade(BaseUrl);
            await campaign.SetProgressAsync("k", 5);
            ResetServer(_server, new[] { Key });
            Up(Key, Row("k", 5, 20));
            Up(List, Rows(Row("k", 5, 20)));

            await campaign.GetProgressAsync();

            CollectionAssert.AreEqual(new[] { "PUT /api/v1/progress/42/k", "GET /api/v1/progress/42" }, Sent(_server));
            Assert.IsFalse(campaign.HasPendingProgress);
        });

        // ---- C. updates -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator C1_Up_StoresTheServerValue() => UniTask.ToCoroutine(async () =>
        {
            Up(Key, Row("k", 7, 20));
            var campaign = NewFacade(BaseUrl);

            var progress = await campaign.SetProgressAsync("k", 7);

            Assert.AreEqual(7, progress.Value);
            Assert.IsFalse(progress.Pending);
            Assert.IsFalse(campaign.HasPendingProgress);
            StringAssert.Contains("\"pending\":{}", Saved());
        });

        [UnityTest]
        public IEnumerator C2_Down_QueuesOnDisk_WithoutThrowing() => UniTask.ToCoroutine(async () =>
        {
            Down(Key);
            var campaign = NewFacade(BaseUrl);

            var progress = await campaign.SetProgressAsync("k", 5);

            Assert.AreEqual(5, progress.Value);
            Assert.IsTrue(progress.Pending);
            Assert.IsTrue(campaign.HasPendingProgress);
            StringAssert.Contains("\"pending\":{\"k\":5}", Saved());
        });

        [UnityTest]
        public IEnumerator C3_SeveralUpdatesWhileDown_SendOnlyTheHighest() => UniTask.ToCoroutine(async () =>
        {
            Down(Key);
            var campaign = NewFacade(BaseUrl);
            await campaign.SetProgressAsync("k", 3);
            await campaign.SetProgressAsync("k", 5);
            await campaign.SetProgressAsync("k", 4);
            ResetServer(_server, new[] { Key });
            Up(Key, Row("k", 5, 20));

            var remaining = await campaign.FlushPendingProgressAsync();

            Assert.AreEqual(0, remaining);
            Assert.AreEqual(1, _server.Requests.Count);
            Assert.IsTrue(_server.Requests.TryDequeue(out var put));
            StringAssert.Contains("\"value\":5", System.Text.RegularExpressions.Regex.Replace(put.Body, "\\s", ""));
        });

        [UnityTest]
        public IEnumerator C4_FlushDropsWhatTheServerHas_AndKeepsTheRest() => UniTask.ToCoroutine(async () =>
        {
            Down(Key);
            Down(KeyB);
            var campaign = NewFacade(BaseUrl);
            await campaign.SetProgressAsync("k", 5);
            await campaign.SetProgressAsync("b", 9);
            Up(Key, Row("k", 5, 20));

            var remaining = await campaign.FlushPendingProgressAsync();

            Assert.AreEqual(1, remaining);
            Assert.IsFalse(campaign.Progress.Single(p => p.Key == "k").Pending);
            Assert.IsTrue(campaign.Progress.Single(p => p.Key == "b").Pending);
        });

        [UnityTest]
        public IEnumerator C5_RefusedOnFlush_IsDropped() => UniTask.ToCoroutine(async () =>
        {
            Down(Key);
            var campaign = NewFacade(BaseUrl);
            await campaign.SetProgressAsync("k", 5);
            Answer(Key, 410, Error(2402, "Progress key is not active"));

            var remaining = await campaign.FlushPendingProgressAsync();

            Assert.AreEqual(0, remaining);
            Assert.IsFalse(campaign.HasPendingProgress);
        });

        [UnityTest]
        public IEnumerator C6_AuthError_KeepsTheUpdateForLater() => UniTask.ToCoroutine(async () =>
        {
            Answer(Key, 401, Error(2100, "expired"));
            var campaign = NewFacade(BaseUrl);

            var progress = await campaign.SetProgressAsync("k", 5);
            Assert.IsTrue(progress.Pending, "no throw, still waiting");

            Up(Key, Row("k", 5, 20));
            Assert.AreEqual(0, await campaign.FlushPendingProgressAsync());
        });

        [UnityTest]
        public IEnumerator C7_ALowerValue_SendsNothing() => UniTask.ToCoroutine(async () =>
        {
            var clock = new FakeClock();
            await WithSavedCopy(clock, value: 10);
            var campaign = NewFacade(BaseUrl, clock: clock);

            var progress = await campaign.SetProgressAsync("k", 4);

            Assert.AreEqual(10, progress.Value);
            Assert.AreEqual(0, _server.Requests.Count + _server.Unmatched.Count);
        });

        [UnityTest]
        public IEnumerator C8_RetriesBackOff() => UniTask.ToCoroutine(async () =>
        {
            var expected = new[] { 30, 60, 120, 240, 480, 600, 600 };
            for (var attempt = 0; attempt < expected.Length; attempt++)
            {
                Assert.AreEqual(expected[attempt], NoctuaLiveOpsCampaign.ProgressRetryDelay(attempt).TotalSeconds);
            }

            var retries = new List<TimeSpan>();
            Down(Key);
            await NewFacade(BaseUrl, retries: retries).SetProgressAsync("k", 1);

            CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(30) }, retries, "one retry scheduled, not one per failure");
        });

        // ---- D. claims ---------------------------------------------------------------------

        [UnityTest]
        public IEnumerator D1_Up_Claims() => UniTask.ToCoroutine(async () =>
        {
            Up(Claim, Row("k", 20, 20, completed: true, claimed: true));
            var campaign = NewFacade(BaseUrl);

            var progress = await campaign.ClaimProgressAsync("k");

            Assert.IsTrue(progress.Claimed);
            Assert.IsTrue(campaign.Progress.Single().Claimed);
        });

        [UnityTest]
        public IEnumerator D2_AWaitingUpdate_IsSentBeforeTheClaim() => UniTask.ToCoroutine(async () =>
        {
            Down(Key);
            var campaign = NewFacade(BaseUrl);
            await campaign.SetProgressAsync("k", 20);
            ResetServer(_server, new[] { Key });
            Up(Key, Row("k", 20, 20, completed: true));
            Up(Claim, Row("k", 20, 20, completed: true, claimed: true));

            var progress = await campaign.ClaimProgressAsync("k");

            Assert.IsTrue(progress.Claimed);
            CollectionAssert.AreEqual(
                new[] { "PUT /api/v1/progress/42/k", "POST /api/v1/progress/42/k/claim" }, Sent(_server));
        });

        [UnityTest]
        public IEnumerator D3_AlreadyClaimed_ThrowsAndMarksItClaimed() => UniTask.ToCoroutine(async () =>
        {
            var clock = new FakeClock();
            await WithSavedCopy(clock, value: 20);
            var campaign = NewFacade(BaseUrl, clock: clock);
            Answer(Claim, 409, Error(2400, "Progress already claimed"));

            var e = await Throws(() => campaign.ClaimProgressAsync("k"));

            Assert.AreEqual((int)NoctuaErrorCode.LiveOpsProgressAlreadyClaimed, e.ErrorCode);
            Assert.IsTrue(campaign.Progress.Single().Claimed);
        });

        [UnityTest]
        public IEnumerator D4_NotCompleted_Throws() => UniTask.ToCoroutine(async () =>
        {
            Answer(Claim, 422, Error(2401, "Progress is not completed yet"));

            var e = await Throws(() => NewFacade(BaseUrl).ClaimProgressAsync("k"));

            Assert.AreEqual((int)NoctuaErrorCode.LiveOpsProgressNotCompleted, e.ErrorCode);
        });

        [UnityTest]
        public IEnumerator D5_Down_ThrowsAndQueuesNothing() => UniTask.ToCoroutine(async () =>
        {
            Down(Claim);
            var campaign = NewFacade(BaseUrl);

            var e = await Throws(() => campaign.ClaimProgressAsync("k"));
            ResetServer(_server, new[] { Claim });
            Up(Claim, Row("k", 20, 20, completed: true, claimed: true));
            await campaign.FlushPendingProgressAsync();

            Assert.AreEqual((int)NoctuaErrorCode.Networking, e.ErrorCode);
            Assert.IsFalse(campaign.HasPendingProgress);
            Assert.AreEqual(0, _server.Requests.Count, "a failed claim is never retried by the SDK");
        });

        [UnityTest]
        public IEnumerator D6_AStaleView_StillAsksTheServer() => UniTask.ToCoroutine(async () =>
        {
            var clock = new FakeClock();
            await WithSavedCopy(clock, value: 20);
            var campaign = NewFacade(BaseUrl, clock: clock);
            Down(List);
            await campaign.GetProgressAsync();
            Assert.IsTrue(campaign.IsProgressStale);
            Up(Claim, Row("k", 20, 20, completed: true, claimed: true));

            await campaign.ClaimProgressAsync("k");

            Assert.IsTrue(Sent(_server).Contains("POST /api/v1/progress/42/k/claim"));
        });

        // ---- F. lifecycle ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator F1_ANewInstance_LoadsCopyAndPending_WithoutARequest() => UniTask.ToCoroutine(async () =>
        {
            var clock = new FakeClock();
            var first = await WithSavedCopy(clock);
            Down(Key);
            await first.SetProgressAsync("k", 8);
            ResetServer(_server, new[] { Key });

            var second = NewFacade(BaseUrl, clock: clock);

            Assert.AreEqual(8, second.Progress.Single().Value);
            Assert.IsTrue(second.HasPendingProgress);
            Assert.AreEqual(0, _server.Requests.Count + _server.Unmatched.Count);
        });

        [UnityTest]
        public IEnumerator F2_AccountSwitch_ShowsOnlyTheNewPlayer_AndKeepsTheOldOnDisk() => UniTask.ToCoroutine(async () =>
        {
            var clock = new FakeClock();
            await WithSavedCopy(clock);
            long? player = PlayerId;
            var campaign = NewFacade(BaseUrl, clock: clock, playerId: () => player);
            Assert.AreEqual(1, campaign.Progress.Count);

            player = OtherPlayerId;
            await campaign.OnAccountChangedAsync();

            Assert.AreEqual(0, campaign.Progress.Count);
            StringAssert.Contains("\"k\"", Saved(PlayerId));
        });

        [UnityTest]
        public IEnumerator F3_SwitchingBack_FlushesThatPlayersUpdatesWithTheirToken() => UniTask.ToCoroutine(async () =>
        {
            long? player = PlayerId;
            var tokens = new StubTokens { Token = "token-42" };
            var campaign = NewFacade(BaseUrl, tokens: tokens, playerId: () => player);
            Down(Key);
            await campaign.SetProgressAsync("k", 6);

            player = OtherPlayerId;
            tokens.Token = "token-43";
            await campaign.OnAccountChangedAsync();
            ResetServer(_server, new[] { Key });
            Up(Key, Row("k", 6, 20));

            player = PlayerId;
            tokens.Token = "token-42";
            await campaign.OnAccountChangedAsync();

            Assert.IsTrue(_server.Requests.TryDequeue(out var put));
            Assert.AreEqual("/api/v1/progress/42/k", put.Path);
            Assert.AreEqual("Bearer token-42", put.Headers["Authorization"]);
            Assert.IsFalse(campaign.HasPendingProgress);
        });

        [UnityTest]
        public IEnumerator F4_BackOnline_FlushesAndRefreshesAStaleCopy() => UniTask.ToCoroutine(async () =>
        {
            var clock = new FakeClock();
            await WithSavedCopy(clock);
            var campaign = NewFacade(BaseUrl, clock: clock);
            Down(Key);
            Down(List);
            await campaign.SetProgressAsync("k", 9);
            await campaign.GetProgressAsync();
            Assert.IsTrue(campaign.IsProgressStale);
            ResetServer(_server, new[] { Key, List });
            Up(Key, Row("k", 9, 20));
            Up(List, Rows(Row("k", 9, 20)));

            await campaign.OnConnectivityRestoredAsync();

            Assert.IsFalse(campaign.HasPendingProgress);
            Assert.IsFalse(campaign.IsProgressStale);
            CollectionAssert.AreEqual(new[] { "PUT /api/v1/progress/42/k", "GET /api/v1/progress/42" }, Sent(_server));
        });

        [UnityTest]
        public IEnumerator F5_PendingSurvivesAKilledApp() => UniTask.ToCoroutine(async () =>
        {
            Down(Key);
            await NewFacade(BaseUrl).SetProgressAsync("k", 4);
            ResetServer(_server, new[] { Key });
            Up(Key, Row("k", 4, 20));

            var afterRestart = NewFacade(BaseUrl);
            var remaining = await afterRestart.FlushPendingProgressAsync();

            Assert.AreEqual(0, remaining);
            Assert.AreEqual(1, _server.Requests.Count);
        });
    }
}
