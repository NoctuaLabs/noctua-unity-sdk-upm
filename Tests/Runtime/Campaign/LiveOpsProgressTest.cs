using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using com.noctuagames.sdk;
using com.noctuagames.sdk.LiveOpsCampaign;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Runtime.Campaign
{
    /// <summary>
    /// Live ops progress on <see cref="NoctuaLiveOpsCampaign"/> against a mock of the progress tracker:
    /// paths, auth, the tracker's error codes (including 409 / 410 / 422), the local guards and
    /// the daily missions player data helper.
    /// </summary>
    [TestFixture]
    public class LiveOpsProgressTest
    {
        private const string BaseUrl = "http://localhost:7783/api/v1";
        private const string ServerUrl = "http://localhost:7783/api/v1/";
        private const long PlayerId = 42;

        private HttpMockServer _server;

        private class StubTokens : IAccessTokenProvider
        {
            public bool Authenticated = true;
            public string AccessToken => Authenticated ? "player-token" : throw new InvalidOperationException();
            public bool IsAuthenticated => Authenticated;
        }

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
            foreach (var path in new[] { "/progress/42", "/progress/42/mission_1", "/progress/42/mission_1/claim" })
            {
                _server.RemoveHandler(path);
            }
            while (_server.Requests.TryDequeue(out _)) { }
            while (_server.Unmatched.TryDequeue(out _)) { }
            LiveOpsProgressTestKit.ClearSaved();
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            // Each facade parks an empty UI root in DontDestroyOnLoad.
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go.name == "NoctuaLiveOpsCampaignUI") UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static NoctuaLiveOpsCampaign Client(
            string baseUrl = BaseUrl, StubTokens tokens = null, long? playerId = PlayerId) =>
            LiveOpsProgressTestKit.NewFacade(
                baseUrl,
                tokens: new LiveOpsProgressTestKit.StubTokens { Authenticated = tokens?.Authenticated ?? true },
                playerId: () => playerId);

        private static string Envelope(string data) => "{\"success\":true,\"data\":" + data + "}";

        private static string Row(string key, long value, long max, bool completed = false, bool claimed = false) =>
            $"{{\"key\":\"{key}\",\"value\":{value},\"max\":{max}," +
            $"\"completed\":{completed.ToString().ToLower()},\"claimed\":{claimed.ToString().ToLower()}}}";

        private static string Error(int code, string message) =>
            $"{{\"success\":false,\"error_code\":{code},\"error_message\":\"{message}\"}}";

        private static async UniTask<NoctuaException> Throws(Func<UniTask> call)
        {
            try
            {
                await call();
            }
            catch (NoctuaException e)
            {
                return e;
            }

            Assert.Fail("Expected a NoctuaException");
            return null;
        }

        // ---- reads and writes ------------------------------------------------

        [UnityTest]
        public IEnumerator GetAll_ReturnsSortedProgress_WithBearerToken() => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/progress/42", _ =>
                Envelope("[" + Row("mission_2", 1, 5) + "," + Row("mission_1", 20, 20, completed: true) + "]"));
            var client = Client();

            var all = await client.GetProgressAsync();

            Assert.AreEqual(new[] { "mission_1", "mission_2" }, all.Select(p => p.Key).ToArray());
            Assert.IsTrue(all[0].Completed);
            Assert.AreEqual(20, all[0].Max);
            Assert.AreEqual(2, client.Progress.Count);

            Assert.IsTrue(_server.Requests.TryDequeue(out var request));
            Assert.AreEqual("GET", request.Method);
            Assert.AreEqual("Bearer player-token", request.Headers["Authorization"]);
        });

        [UnityTest]
        public IEnumerator GetAll_EmptyForANewPlayer() => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/progress/42", _ => Envelope("[]"));

            Assert.AreEqual(0, (await Client().GetProgressAsync()).Count);
            Assert.IsEmpty(_server.Unmatched, "every request should reach a mock handler");
        });

        [UnityTest]
        public IEnumerator Set_SendsTheAbsoluteValue_AndUpdatesCurrent() => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/progress/42/mission_1", _ => Envelope(Row("mission_1", 7, 20)));
            var client = Client();
            IReadOnlyList<LiveOpsProgress> notified = null;
            client.OnProgressChanged += list => notified = list;

            var progress = await client.SetProgressAsync("mission_1", 7);

            Assert.AreEqual(7, progress.Value);
            Assert.AreEqual(7, client.Progress.Single().Value);
            Assert.AreEqual(1, notified.Count);

            Assert.IsTrue(_server.Requests.TryDequeue(out var request));
            Assert.AreEqual("PUT", request.Method);
            StringAssert.Contains("\"value\":7", System.Text.RegularExpressions.Regex.Replace(request.Body, "\\s", ""));
        });

        [UnityTest]
        public IEnumerator Claim_PostsToTheClaimPath() => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/progress/42/mission_1/claim", _ =>
                Envelope(Row("mission_1", 20, 20, completed: true, claimed: true)));

            var progress = await Client().ClaimProgressAsync("mission_1");

            Assert.IsTrue(progress.Claimed);
            Assert.IsTrue(_server.Requests.TryDequeue(out var request));
            Assert.AreEqual("POST", request.Method);
        });

        // ---- the tracker's refusals keep their codes ------------------------

        [UnityTest]
        public IEnumerator Claim_TwiceIsAlreadyClaimed_Even_On409() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.ignoreFailingMessages = true;
            _server.AddHandler("/progress/42/mission_1/claim", _ => (409, Error(2400, "Progress already claimed")));

            var e = await Throws(() => Client().ClaimProgressAsync("mission_1"));

            Assert.AreEqual((int)NoctuaErrorCode.LiveOpsProgressAlreadyClaimed, e.ErrorCode);
        });

        [UnityTest]
        public IEnumerator Claim_BeforeCompleteIsNotCompleted_On422() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.ignoreFailingMessages = true;
            _server.AddHandler("/progress/42/mission_1/claim", _ => (422, Error(2401, "Progress is not completed yet")));

            var e = await Throws(() => Client().ClaimProgressAsync("mission_1"));

            Assert.AreEqual((int)NoctuaErrorCode.LiveOpsProgressNotCompleted, e.ErrorCode);
        });

        [UnityTest]
        public IEnumerator Set_OnAnExpiredKeyIsInactive_On410() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.ignoreFailingMessages = true;
            _server.AddHandler("/progress/42/mission_1", _ => (410, Error(2402, "Progress key is not active")));

            var e = await Throws(() => Client().SetProgressAsync("mission_1", 1));

            Assert.AreEqual((int)NoctuaErrorCode.LiveOpsProgressKeyInactive, e.ErrorCode);
        });

        [UnityTest]
        public IEnumerator Set_OnAnUnknownKeyIsKeyNotFound_On404() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.ignoreFailingMessages = true;
            _server.AddHandler("/progress/42/mission_1", _ => (404, Error(2300, "Progress key is not configured for this game")));

            var e = await Throws(() => Client().SetProgressAsync("mission_1", 1));

            Assert.AreEqual((int)NoctuaErrorCode.LiveOpsProgressKeyNotFound, e.ErrorCode);
        });

        [UnityTest]
        public IEnumerator A409WithoutAnEnvelope_StaysANetworkingError() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.ignoreFailingMessages = true;
            _server.AddHandler("/progress/42/mission_1/claim", _ => (409, "conflict"));

            var e = await Throws(() => Client().ClaimProgressAsync("mission_1"));

            Assert.AreEqual((int)NoctuaErrorCode.Networking, e.ErrorCode);
        });

        // ---- guards: nothing is sent ----------------------------------------

        [UnityTest]
        public IEnumerator NotConfigured_ThrowsWithoutARequest() => UniTask.ToCoroutine(async () =>
        {
            var client = Client(baseUrl: " ");

            Assert.IsFalse(client.IsProgressConfigured);
            var e = await Throws(() => client.GetProgressAsync());
            StringAssert.Contains("progressTrackerBaseUrl", e.Message);
            Assert.AreEqual(0, _server.Requests.Count);
        });

        [UnityTest]
        public IEnumerator NotLoggedIn_Throws() => UniTask.ToCoroutine(async () =>
        {
            var noToken = await Throws(() => Client(tokens: new StubTokens { Authenticated = false }).GetProgressAsync());
            var noPlayer = await Throws(() => Client(playerId: null).GetProgressAsync());

            Assert.AreEqual((int)NoctuaErrorCode.Authentication, noToken.ErrorCode);
            Assert.AreEqual((int)NoctuaErrorCode.Authentication, noPlayer.ErrorCode);
            Assert.AreEqual(0, _server.Requests.Count);
        });

        [UnityTest]
        public IEnumerator InvalidKeysAndValues_ThrowWithoutARequest() => UniTask.ToCoroutine(async () =>
        {
            var client = Client();

            await Throws(() => client.SetProgressAsync("", 1));
            await Throws(() => client.SetProgressAsync("__complete", 1));
            await Throws(() => client.SetProgressAsync(new string('k', NoctuaLiveOpsCampaign.MaxProgressKeyLength + 1), 1));
            await Throws(() => client.SetProgressAsync("mission_1", -1));
            await Throws(() => client.ClaimProgressAsync("__complete"));

            Assert.AreEqual(0, _server.Requests.Count);
        });

        // ---- local state ----------------------------------------------------

        [UnityTest]
        public IEnumerator Clear_ForgetsMemory_KeepsTheSavedCopy() => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/progress/42", _ => Envelope("[" + Row("mission_1", 3, 20) + "]"));
            var client = Client();
            await client.GetProgressAsync();
            var notified = -1;
            client.OnProgressChanged += list => notified = list.Count;

            client.ClearProgress();

            Assert.AreEqual(0, notified, "listeners see the in-memory view emptied");
            StringAssert.Contains("mission_1", LiveOpsProgressTestKit.Saved(), "the player's copy stays on disk");
            Assert.AreEqual(3, client.Progress.Single().Value, "and is loaded again on next access");
        });

        [UnityTest]
        public IEnumerator AThrowingHandler_DoesNotFailTheCall() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.ignoreFailingMessages = true;
            _server.AddHandler("/progress/42/mission_1", _ => Envelope(Row("mission_1", 2, 20)));
            var client = Client();
            client.OnProgressChanged += _ => throw new InvalidOperationException("game bug");

            Assert.AreEqual(2, (await client.SetProgressAsync("mission_1", 2)).Value);
        });

        // ---- daily missions helper ------------------------------------------

        [Test]
        public void DailyMissionsPlayerData_UsesTheKeyAsMissionId()
        {
            var data = NoctuaLiveOpsCampaign.ToDailyMissionsPlayerData(new[]
            {
                new LiveOpsProgress { Key = "use_stm", Value = 12, Max = 20 },
                new LiveOpsProgress { Key = "login", Value = 1, Max = 1, Completed = true, Claimed = true },
            });

            Assert.AreEqual("12", data["m_use_stm_progress"]);
            Assert.AreEqual("false", data["m_use_stm_claimed"]);
            Assert.AreEqual("1", data["m_login_progress"]);
            Assert.AreEqual("true", data["m_login_claimed"]);
            Assert.AreEqual(4, data.Count);
        }

        [Test]
        public void DailyMissionsPlayerData_MapsOrSkipsKeys()
        {
            var data = NoctuaLiveOpsCampaign.ToDailyMissionsPlayerData(
                new[]
                {
                    new LiveOpsProgress { Key = "daily.use_stm", Value = 3 },
                    new LiveOpsProgress { Key = "weekly.boss", Value = 1 },
                },
                key => key.StartsWith("daily.") ? key.Substring("daily.".Length) : null);

            Assert.AreEqual("3", data["m_use_stm_progress"]);
            Assert.AreEqual(2, data.Count);
            Assert.AreEqual(0, NoctuaLiveOpsCampaign.ToDailyMissionsPlayerData(null).Count);
        }
    }
}
