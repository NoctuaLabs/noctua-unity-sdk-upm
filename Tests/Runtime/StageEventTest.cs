using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using com.noctuagames.sdk;
using com.noctuagames.sdk.Events;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Runtime
{
    /// <summary>
    /// Covers the stage_session_id / stage_time_msec grouping across
    /// game_stage_start / game_stage_complete / game_stage_failed events.
    /// </summary>
    public class StageEventTest
    {
        private const string BaseUrl = "http://localhost:7783/api/v1";
        private HttpMockServer _server;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            _server = new HttpMockServer("http://localhost:7783/api/v1/");
            _server.AddHandler(
                "/events",
                _ => @"{""success"":""true"",""data"":{""message"":""events tracked""}}"
            );
            _server.Start();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _server.RemoveHandler("/events");
            _server.Dispose();
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return new WaitForSeconds(1.0f);

            PlayerPrefs.DeleteKey("NoctuaEvents");
            PlayerPrefs.DeleteKey("NoctuaCurrentStageLevel");
            PlayerPrefs.DeleteKey("NoctuaCurrentStageMode");
            PlayerPrefs.Save();

            var eventStorePath = Path.Combine(Application.persistentDataPath, "noctua_events.jsonl");
            if (File.Exists(eventStorePath)) File.Delete(eventStorePath);

            ExperimentManager.Clear();

            var empty = false;
            while (!empty)
            {
                _server.Requests.Clear();
                yield return new WaitForSeconds(0.3f);
                empty = _server.Requests.Count == 0;
            }

            if (File.Exists(eventStorePath)) File.Delete(eventStorePath);
        }

        private EventSender NewSender() => new EventSender(
            new EventSenderConfig
            {
                BaseUrl = BaseUrl,
                ClientId = "test_client_id",
                BatchSize = 1,
                CycleDelay = 100,
                NativePlugin = new DefaultNativePlugin()
            },
            new NoctuaLocale()
        );

        private async UniTask<List<Dictionary<string, object>>> DrainAsync(int timeoutMs = 3000, int settleMs = 600)
        {
            var win = await UniTask.WhenAny(UniTask.Delay(timeoutMs), UniTask.WaitUntil(() => _server.Requests.Count > 0));
            if (win == 0) return new List<Dictionary<string, object>>();
            await UniTask.Delay(settleMs);

            var sb = new StringBuilder();
            while (_server.Requests.TryDequeue(out var req))
                sb.AppendLine(req.Body);

            return sb.ToString().Trim()
                .Split('\n')
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Distinct()
                .Select(JsonConvert.DeserializeObject<Dictionary<string, object>>)
                .ToList();
        }

        [UnityTest]
        public IEnumerator GameStageStart_EmitsStageSessionId() => UniTask.ToCoroutine(async () =>
        {
            var sender = NewSender();
            sender.Send("game_stage_start", new Dictionary<string, System.IConvertible>
            {
                ["level"] = "1",
                ["stage_mode"] = "normal"
            });

            var events = await DrainAsync();
            var start = events.FirstOrDefault(e => e.TryGetValue("event_name", out var n) && n?.ToString() == "game_stage_start");

            Assert.IsNotNull(start, "game_stage_start was not delivered");
            Assert.IsTrue(start.ContainsKey("stage_session_id"), "stage_session_id missing from game_stage_start");
            Assert.AreEqual(32, start["stage_session_id"].ToString().Length, "stage_session_id should be 32-char GUID-N");

            sender.Dispose();
        });

        [UnityTest]
        public IEnumerator StartThenComplete_ShareSessionIdAndEmitTime() => UniTask.ToCoroutine(async () =>
        {
            var sender = NewSender();
            sender.Send("game_stage_start", new Dictionary<string, System.IConvertible> { ["level"] = "1" });

            await UniTask.Delay(250);

            sender.Send("game_stage_complete", new Dictionary<string, System.IConvertible> { ["level"] = "1" });

            var events = await DrainAsync();
            var start = events.FirstOrDefault(e => e["event_name"].ToString() == "game_stage_start");
            var complete = events.FirstOrDefault(e => e["event_name"].ToString() == "game_stage_complete");

            Assert.IsNotNull(start, "game_stage_start missing");
            Assert.IsNotNull(complete, "game_stage_complete missing");

            Assert.AreEqual(start["stage_session_id"], complete["stage_session_id"],
                "start and complete must share stage_session_id");

            Assert.IsTrue(complete.ContainsKey("stage_time_msec"), "stage_time_msec missing from complete");
            Assert.GreaterOrEqual(System.Convert.ToInt64(complete["stage_time_msec"]), 200L);

            sender.Dispose();
        });

        [UnityTest]
        public IEnumerator StartThenFailed_ShareSessionIdAndEmitTime() => UniTask.ToCoroutine(async () =>
        {
            var sender = NewSender();
            sender.Send("game_stage_start", new Dictionary<string, System.IConvertible> { ["level"] = "2" });

            await UniTask.Delay(250);

            sender.Send("game_stage_failed", new Dictionary<string, System.IConvertible> { ["level"] = "2" });

            var events = await DrainAsync();
            var start = events.FirstOrDefault(e => e["event_name"].ToString() == "game_stage_start");
            var failed = events.FirstOrDefault(e => e["event_name"].ToString() == "game_stage_failed");

            Assert.IsNotNull(start, "game_stage_start missing");
            Assert.IsNotNull(failed, "game_stage_failed missing");

            Assert.AreEqual(start["stage_session_id"], failed["stage_session_id"],
                "start and failed must share stage_session_id");

            Assert.IsTrue(failed.ContainsKey("stage_time_msec"), "stage_time_msec missing from failed");
            Assert.GreaterOrEqual(System.Convert.ToInt64(failed["stage_time_msec"]), 200L);

            sender.Dispose();
        });

        [UnityTest]
        public IEnumerator FailedWithoutStart_NoStageFields() => UniTask.ToCoroutine(async () =>
        {
            var sender = NewSender();
            sender.Send("game_stage_failed", new Dictionary<string, System.IConvertible> { ["level"] = "3" });

            var events = await DrainAsync();
            var failed = events.FirstOrDefault(e => e["event_name"].ToString() == "game_stage_failed");

            Assert.IsNotNull(failed, "game_stage_failed missing");
            Assert.IsFalse(failed.ContainsKey("stage_time_msec"),
                "stage_time_msec must not appear when no preceding game_stage_start");
            Assert.IsFalse(failed.ContainsKey("stage_session_id"),
                "stage_session_id must not appear when no preceding game_stage_start");

            sender.Dispose();
        });

        [UnityTest]
        public IEnumerator SecondStart_RegeneratesSessionId() => UniTask.ToCoroutine(async () =>
        {
            var sender = NewSender();
            sender.Send("game_stage_start", new Dictionary<string, System.IConvertible> { ["level"] = "4" });
            await UniTask.Delay(100);
            sender.Send("game_stage_start", new Dictionary<string, System.IConvertible> { ["level"] = "4" });

            var events = await DrainAsync();
            var starts = events.Where(e => e["event_name"].ToString() == "game_stage_start").ToList();

            Assert.AreEqual(2, starts.Count, "expected two game_stage_start events");
            Assert.AreNotEqual(starts[0]["stage_session_id"], starts[1]["stage_session_id"],
                "second game_stage_start must regenerate stage_session_id");

            sender.Dispose();
        });

        [UnityTest]
        public IEnumerator CompleteAfterComplete_DoesNotReuseStaleSessionId() => UniTask.ToCoroutine(async () =>
        {
            var sender = NewSender();
            sender.Send("game_stage_start", new Dictionary<string, System.IConvertible> { ["level"] = "5" });
            await UniTask.Delay(150);
            sender.Send("game_stage_complete", new Dictionary<string, System.IConvertible> { ["level"] = "5" });
            await UniTask.Delay(150);
            sender.Send("game_stage_complete", new Dictionary<string, System.IConvertible> { ["level"] = "5" });

            var events = await DrainAsync();
            var completes = events.Where(e => e["event_name"].ToString() == "game_stage_complete").ToList();
            Assert.AreEqual(2, completes.Count);

            // First complete carries the real id; second must NOT carry the cleared id
            Assert.IsTrue(completes[0].ContainsKey("stage_session_id"));
            Assert.IsFalse(completes[1].ContainsKey("stage_session_id"),
                "second game_stage_complete (no fresh start) must not emit stage_session_id");
            Assert.IsFalse(completes[1].ContainsKey("stage_time_msec"),
                "second game_stage_complete (stopwatch reset) must not emit stage_time_msec");

            sender.Dispose();
        });
    }
}
