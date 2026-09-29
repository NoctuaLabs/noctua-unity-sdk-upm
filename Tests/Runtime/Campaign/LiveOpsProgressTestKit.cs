using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using com.noctuagames.sdk;
using com.noctuagames.sdk.LiveOpsCampaign;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tests.Runtime.Campaign
{
    /// <summary>Shared pieces for the live ops progress tests: stubs, JSON bodies and the facade.</summary>
    public static class LiveOpsProgressTestKit
    {
        public const long PlayerId = 42;
        public const long OtherPlayerId = 43;

        /// <summary>A port nothing listens on — a tracker with no connection at all.</summary>
        public const string UnreachableBaseUrl = "http://localhost:7799/api/v1";

        public class StubTokens : IAccessTokenProvider
        {
            public bool Authenticated = true;
            public string Token = "player-token";
            public string AccessToken => Authenticated ? Token : throw new InvalidOperationException();
            public bool IsAuthenticated => Authenticated;
        }

        /// <summary>A clock the test moves by hand.</summary>
        public class FakeClock
        {
            public DateTime Now = new DateTime(2026, 9, 29, 8, 0, 0, DateTimeKind.Utc);
            public DateTime UtcNow() => Now;
        }

        public static string Envelope(string data) => "{\"success\":true,\"data\":" + data + "}";

        public static string Row(string key, long value, long max, bool completed = false, bool claimed = false) =>
            $"{{\"key\":\"{key}\",\"value\":{value},\"max\":{max}," +
            $"\"completed\":{completed.ToString().ToLower()},\"claimed\":{claimed.ToString().ToLower()}}}";

        public static string Rows(params string[] rows) => "[" + string.Join(",", rows) + "]";

        public static string Error(int code, string message) =>
            $"{{\"success\":false,\"error_code\":{code},\"error_message\":\"{message}\"}}";

        /// <summary>A handler answering 503 — the tracker is up but failing.</summary>
        public static (int, string) Down(System.Net.HttpListenerRequest _) => (503, "Service Unavailable");

        public static NoctuaLiveOpsCampaign NewFacade(
            string baseUrl,
            CampaignConfig config = null,
            StubTokens tokens = null,
            Func<long?> playerId = null,
            FakeClock clock = null,
            List<TimeSpan> retries = null,
            Action<string> notice = null,
            PanelSettings panel = null,
            NoctuaLocale locale = null) =>
            new(config ?? new CampaignConfig(), panel, locale, new MockEventSender(), () => Array.Empty<string>(),
                progress: new NoctuaLiveOpsCampaign.ProgressTrackerOptions
                {
                    BaseUrl = baseUrl,
                    AccessTokens = tokens ?? new StubTokens(),
                    PlayerId = playerId ?? (() => PlayerId),
                    ShowNotice = notice,
                    TimeoutSeconds = 1,
                    UtcNow = (clock ?? new FakeClock()).UtcNow,
                    // Retries are recorded but never fire on their own; tests flush by hand.
                    RetryDelay = delay =>
                    {
                        retries?.Add(delay);
                        return UniTask.Never(CancellationToken.None);
                    },
                });

        public static async UniTask<NoctuaException> Throws(Func<UniTask> call)
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

        public static void ClearSaved()
        {
            PlayerPrefs.DeleteKey(NoctuaLiveOpsCampaign.ProgressPrefsKey(PlayerId));
            PlayerPrefs.DeleteKey(NoctuaLiveOpsCampaign.ProgressPrefsKey(OtherPlayerId));
            PlayerPrefs.Save();
        }

        public static string Saved(long playerId = PlayerId) =>
            PlayerPrefs.GetString(NoctuaLiveOpsCampaign.ProgressPrefsKey(playerId), "");

        /// <summary>Each facade parks a UI root in DontDestroyOnLoad.</summary>
        public static void DestroyUiRoots()
        {
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                // Destroying a root takes its children (the popup) with it; skip those.
                if (go != null && go.name == "NoctuaLiveOpsCampaignUI") UnityEngine.Object.DestroyImmediate(go);
            }
        }

        public static void ResetServer(HttpMockServer server, IEnumerable<string> paths)
        {
            foreach (var path in paths) server.RemoveHandler(path);
            while (server.Requests.TryDequeue(out _)) { }
            while (server.Unmatched.TryDequeue(out _)) { }
        }

        /// <summary>"METHOD path" of every request the mock answered, oldest first.</summary>
        public static List<string> Sent(HttpMockServer server) =>
            server.Requests.Select(r => $"{r.Method} {r.Path}").ToList();
    }
}
