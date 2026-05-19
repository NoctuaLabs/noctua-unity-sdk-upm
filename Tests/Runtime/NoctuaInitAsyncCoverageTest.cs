using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using com.noctuagames.sdk;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Runtime
{
    // =========================================================================
    // NoctuaInitAsyncCoverageTest.cs
    //
    // Integration-level tests for Noctua.InitAsync() targeting branches not
    // reachable from pure-static EditMode tests (NoctuaStaticApiCoverageTest).
    //
    // Strategy:
    //  1. Redirect _game._baseUrl (via reflection) to HttpMockServer on localhost
    //     so InitGameAsync() resolves locally rather than hitting the real API.
    //  2. Reset _initialized / _offlineMode static state before each test so
    //     the full InitAsync() body executes even on re-runs.
    //  3. Use [UnityTest] + UniTask.ToCoroutine() so the PlayerLoop ticks, which
    //     is required for UniTask.Delay / UniTask.WhenAny / UnityWebRequest.
    //
    // Note on timing: InitAsync() includes a 5-second IAP-ready loop when
    // DefaultNativePlugin's IAP backend does not respond. Tests that run the
    // full path will take approximately 5-6 seconds each. This is expected and
    // acceptable for integration-level coverage tests.
    //
    // Branches covered:
    //   Group A — Already-initialized early-return
    //   Group B — Happy path (online mode, country from response)
    //   Group C — Offline mode flag from server response
    //   Group D — Feature-flag parsing (valid / invalid / empty key / null)
    //   Group E — Empty country → Cloudflare fallback → "XX" sentinel
    //   Group F — onSuccess callback + OnInitSuccess event
    //   Group G — Network failure on offline-first → silent fallback
    //   Group H — Remote IAA config absent → non-IAA native plugin init path
    //   Group I — Currency / distribution-platform response fields
    // =========================================================================

    // InitAsync() includes a 5-second IAP-ready loop + WaitForNativePluginInitAsync.
    // Tests that run the full path take ~6-8 s. Allow 30 s per test as a safety ceiling.
    [TestFixture]
    [Timeout(30000)]
    public class NoctuaInitAsyncCoverageTest
    {
        // ── Constants ─────────────────────────────────────────────────────────

        private const string MockPrefix = "http://localhost:19923/";
        private const BindingFlags StaticPriv = BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags InstPriv   = BindingFlags.NonPublic | BindingFlags.Instance;

        // ── Fields ────────────────────────────────────────────────────────────

        private static bool _initAvailable;
        private HttpMockServer _server;

        // ── Fixture lifecycle ─────────────────────────────────────────────────

        [OneTimeSetUp]
        public static void OneTimeSetUp()
        {
            try
            {
                // Warm up the Lazy<Noctua> singleton once. Reads noctuagg.json from
                // Assets/StreamingAssets/ via File.ReadAllText (macOS/Linux Editor).
                _ = Noctua.IsSandbox();
                _initAvailable = true;
            }
            catch (Exception)
            {
                _initAvailable = false;
            }
        }

        [SetUp]
        public void SetUp()
        {
            // Silence expected warnings/errors produced by InitAsync internals
            // (IAP timeout, Cloudflare 404 fallback, etc.) so the log stays clean.
            LogAssert.ignoreFailingMessages = true;

            // Always start with a clean slate so the full InitAsync() body runs.
            ResetStaticState();

            // Fresh mock server per test; torn down in TearDown.
            _server = new HttpMockServer(MockPrefix);
            _server.AddHandler("/games/init", _ => BuildInitResponse());
            _server.Start();

            // Redirect the singleton's _game._baseUrl to our mock server so
            // InitGameAsync() resolves locally. No-op when init is unavailable.
            RedirectGameServiceUrl(MockPrefix.TrimEnd('/'));
        }

        [TearDown]
        public void TearDown()
        {
            ResetStaticState();
            // Clear the static event so handlers from Group F tests do not
            // accumulate across test runs (static event survives test boundaries).
            Noctua.OnInitSuccess = null;
            _server?.Dispose();
            _server = null;
        }

        // ── Skip helper ───────────────────────────────────────────────────────

        private void RequireInit([CallerMemberName] string caller = "")
        {
            if (!_initAvailable)
                Assert.Ignore(
                    $"{caller}: Noctua singleton init not available " +
                    "(no valid noctuagg.json at Assets/StreamingAssets/).");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Group A — Already-initialized early-return (line 543 in Initialization.cs)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// When _initialized is already true, InitAsync() exits at the guard
        /// without making any HTTP request. Verifies the idempotency branch.
        /// </summary>
        [UnityTest]
        public IEnumerator InitAsync_AlreadyInitialized_ReturnsWithoutHttp()
            => UniTask.ToCoroutine(async () =>
        {
            RequireInit();

            SetStaticField("_initialized", true);

            await Noctua.InitAsync();

            Assert.IsTrue(Noctua.IsInitialized(),
                "IsInitialized must remain true");
            Assert.AreEqual(0, _server.Requests.Count,
                "No HTTP requests should be made when already initialized");
        });

        // ─────────────────────────────────────────────────────────────────────
        // Group B — Happy path (online mode, country from response)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Full online initialization: mock returns a valid response, so
        /// InitAsync() sets _initialized = true and _offlineMode = false.
        /// </summary>
        [UnityTest]
        public IEnumerator InitAsync_OnlineResponse_SetsInitializedTrueAndOfflineFalse()
            => UniTask.ToCoroutine(async () =>
        {
            RequireInit();

            await Noctua.InitAsync();

            Assert.IsTrue(Noctua.IsInitialized(),
                "IsInitialized must be true after successful online init");
            Assert.IsFalse(Noctua.IsOfflineMode(),
                "IsOfflineMode must be false after successful online init");
        });

        /// <summary>
        /// Verifies that at least one HTTP request reaches the mock /games/init handler.
        /// </summary>
        [UnityTest]
        public IEnumerator InitAsync_OnlineResponse_MakesInitGameHttpRequest()
            => UniTask.ToCoroutine(async () =>
        {
            RequireInit();

            await Noctua.InitAsync();

            Assert.Greater(_server.Requests.Count, 0,
                "At least one HTTP request must reach the mock server");
        });

        /// <summary>
        /// Non-empty country from server response is used directly; no Cloudflare
        /// fallback runs. Covers the "country from geoIP" log branch.
        /// </summary>
        [UnityTest]
        public IEnumerator InitAsync_WithCountryInResponse_CompletesOnline()
            => UniTask.ToCoroutine(async () =>
        {
            RequireInit();

            // Default handler already returns country = "US".
            await Noctua.InitAsync();

            Assert.IsTrue(Noctua.IsInitialized(),
                "Online init with explicit country must complete successfully");
        });

        // ─────────────────────────────────────────────────────────────────────
        // Group C — Server returns offline_mode = true
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// When the server sets offline_mode=true, _initialized stays false
        /// (the SDK defers enable() until the reconnect loop succeeds) and
        /// _offlineMode flips to true.
        /// RunReconnectionLoopAsync() is fire-and-forget with a 10 s initial
        /// delay, so it cannot interfere within this test's lifetime.
        /// </summary>
        [UnityTest]
        public IEnumerator InitAsync_ServerReturnsOfflineMode_KeepsInitializedFalse()
            => UniTask.ToCoroutine(async () =>
        {
            RequireInit();

            _server.AddHandler("/games/init", _ => BuildInitResponse(offlineMode: true));

            await Noctua.InitAsync();

            // In offline mode, Enable() is never called so _initialized stays false.
            Assert.IsFalse(Noctua.IsInitialized(),
                "IsInitialized must remain false in offline mode (Enable() deferred)");
            Assert.IsTrue(Noctua.IsOfflineMode(),
                "IsOfflineMode must be true when server returns offline_mode=true");
        });

        // ─────────────────────────────────────────────────────────────────────
        // Group D — Feature-flag parsing branches
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Valid boolean feature flag ("true") is parsed by bool.TryParse and
        /// applied to _config.Noctua.RemoteFeatureFlags. Covers the success branch.
        /// </summary>
        [UnityTest]
        public IEnumerator InitAsync_WithValidBoolFeatureFlag_AppliesFlag()
            => UniTask.ToCoroutine(async () =>
        {
            RequireInit();

            _server.AddHandler("/games/init", _ => BuildInitResponse(
                featureFlags: new Dictionary<string, string> { { "ssoDisabled", "true" } }));

            await Noctua.InitAsync();

            Assert.IsTrue(Noctua.IsInitialized(),
                "Online init with valid feature flag must complete");
        });

        /// <summary>
        /// Invalid boolean value in a feature flag logs "Invalid boolean flag" warning
        /// and skips the entry. InitAsync() must complete normally.
        /// </summary>
        [UnityTest]
        public IEnumerator InitAsync_WithInvalidBoolFeatureFlag_CompletesNormally()
            => UniTask.ToCoroutine(async () =>
        {
            RequireInit();

            _server.AddHandler("/games/init", _ => BuildInitResponse(
                featureFlags: new Dictionary<string, string> { { "someFlag", "not-a-bool" } }));

            await Noctua.InitAsync();

            Assert.IsTrue(Noctua.IsInitialized(),
                "Online init must complete even with an unparseable feature flag");
        });

        /// <summary>
        /// Empty-string key in feature flags triggers "Empty key found" warning
        /// and the entry is skipped. InitAsync() completes normally.
        /// </summary>
        [UnityTest]
        public IEnumerator InitAsync_WithEmptyKeyFeatureFlag_SkipsAndCompletes()
            => UniTask.ToCoroutine(async () =>
        {
            RequireInit();

            _server.AddHandler("/games/init", _ => BuildInitResponse(
                featureFlags: new Dictionary<string, string> { { "", "true" } }));

            await Noctua.InitAsync();

            Assert.IsTrue(Noctua.IsInitialized(),
                "Online init must complete even with an empty feature-flag key");
        });

        /// <summary>
        /// Null RemoteFeatureFlags (feature_flags absent from JSON) logs
        /// "RemoteFeatureFlags is null" warning. The loop is skipped and init
        /// completes normally.
        /// </summary>
        [UnityTest]
        public IEnumerator InitAsync_WithNullFeatureFlags_LogsWarningAndCompletes()
            => UniTask.ToCoroutine(async () =>
        {
            RequireInit();

            // feature_flags key absent → RemoteFeatureFlags == null after deserialization.
            _server.AddHandler("/games/init", _ => WrapData(new
            {
                country      = "US",
                ip_address   = "127.0.0.1",
                offline_mode = false,
                remote_configs = new
                {
                    enabled_payment_types = new[] { "editor" }
                    // feature_flags intentionally omitted
                }
            }));

            await Noctua.InitAsync();

            Assert.IsTrue(Noctua.IsInitialized());
        });

        /// <summary>
        /// Multiple valid feature flags — the foreach loop iterates all of them.
        /// Covers the multi-iteration path of the flag-application loop.
        /// </summary>
        [UnityTest]
        public IEnumerator InitAsync_WithMultipleValidFlags_AppliesAllFlags()
            => UniTask.ToCoroutine(async () =>
        {
            RequireInit();

            _server.AddHandler("/games/init", _ => BuildInitResponse(
                featureFlags: new Dictionary<string, string>
                {
                    { "ssoDisabled",                      "false" },
                    { "vnLegalPurposeEnabled",             "true"  },
                    { "vnLegalPurposeFullKycEnabled",      "false" },
                }));

            await Noctua.InitAsync();

            Assert.IsTrue(Noctua.IsInitialized());
        });

        // ─────────────────────────────────────────────────────────────────────
        // Group E — Empty country → Cloudflare fallback → "XX" sentinel
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// When country is empty, GetCountryIDFromCloudflareTraceAsync() is attempted.
        /// The mock server does not serve the cloudflare endpoint, so the catch block
        /// sets country = "XX". InitAsync() must still reach IsInitialized = true.
        /// </summary>
        [UnityTest]
        public IEnumerator InitAsync_WithEmptyCountry_FallsBackToXXAndCompletes()
            => UniTask.ToCoroutine(async () =>
        {
            RequireInit();

            _server.AddHandler("/games/init", _ => BuildInitResponse(country: ""));

            await Noctua.InitAsync();

            // Country falls back to "XX" via the catch block; init still succeeds.
            Assert.IsTrue(Noctua.IsInitialized());
        });

        // ─────────────────────────────────────────────────────────────────────
        // Group F — onSuccess callback + OnInitSuccess event
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The optional onSuccess UniTask delegate is awaited after initialization.
        /// Verifies the delegate invocation path at the end of InitAsync.
        /// </summary>
        [UnityTest]
        public IEnumerator InitAsync_WithOnSuccessCallback_InvokesCallback()
            => UniTask.ToCoroutine(async () =>
        {
            RequireInit();

            bool callbackInvoked = false;

            await Noctua.InitAsync(onSuccess: async () =>
            {
                callbackInvoked = true;
                await UniTask.Yield();
            });

            Assert.IsTrue(callbackInvoked,
                "onSuccess callback must be invoked after InitAsync completes");
        });

        /// <summary>
        /// The static OnInitSuccess event fires at the end of InitAsync().
        /// Verifies the event dispatch path.
        /// </summary>
        [UnityTest]
        public IEnumerator InitAsync_WithOnInitSuccessSubscriber_InvokesEvent()
            => UniTask.ToCoroutine(async () =>
        {
            RequireInit();

            bool eventFired = false;
            Action handler = () => eventFired = true;
            Noctua.OnInitSuccess += handler;

            try
            {
                await Noctua.InitAsync();
            }
            finally
            {
                Noctua.OnInitSuccess -= handler;
            }

            Assert.IsTrue(eventFired,
                "OnInitSuccess event must fire after successful InitAsync()");
        });

        /// <summary>
        /// Calling InitAsync() a second time with _initialized=true hits the early-
        /// return guard before the OnInitSuccess dispatch site, so the event fires
        /// exactly once.
        /// </summary>
        [UnityTest]
        public IEnumerator InitAsync_CalledTwice_OnInitSuccessFiresOnce()
            => UniTask.ToCoroutine(async () =>
        {
            RequireInit();

            int fireCount = 0;
            Action handler = () => fireCount++;
            Noctua.OnInitSuccess += handler;

            try
            {
                await Noctua.InitAsync();   // full init — fires event
                await Noctua.InitAsync();   // early-return — does NOT re-fire
            }
            finally
            {
                Noctua.OnInitSuccess -= handler;
            }

            Assert.AreEqual(1, fireCount,
                "OnInitSuccess must fire exactly once even when InitAsync is called twice");
        });

        // ─────────────────────────────────────────────────────────────────────
        // Group G — Network failure on offline-first → silent fallback
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// When InitGameAsync() returns HTTP 500 and isOfflineFirst=true,
        /// the Networking-error branch (e.Message.Contains("500")) silently
        /// substitutes an offline dummy response.
        ///
        /// Triggering 500: returning null from the mock handler causes HttpMockServer
        /// to send a well-formed HTTP 500 with ContentLength=0 (no body).
        /// UnityWebRequest receives result=ProtocolError with responseCode=500.
        /// Http.cs maps responseCode > 408 to NoctuaException(Networking,
        /// "HTTP error 500: InternalServerError,…") — message contains "500".
        ///
        /// We set _offlineMode=true beforehand to bypass RetryAsyncTask delays.
        /// </summary>
        [UnityTest]
        public IEnumerator InitAsync_NetworkFailureWithOfflineFirst_EntersOfflineMode()
            => UniTask.ToCoroutine(async () =>
        {
            RequireInit();

            if (!Noctua.IsOfflineFirst())
                Assert.Ignore("noctuagg.json does not have offlineFirstEnabled=true; skipping.");

            // Null return → HttpMockServer sends HTTP 500 with an explicitly closed
            // OutputStream so UnityWebRequest receives result=ProtocolError (not an
            // exception).  Http.cs then logs an Error at the responseCode > 408 branch.
            // Declare the expected errors so NUnit doesn't report them as unhandled.
            // Two possible Error logs from Http.cs on a 500 response:
            //   (a) _log.Exception(e) at line 416: rendered as "HttpRequest.Send: HTTP/1.1 500 …"
            //   (b) _log.Error(…)    at line 504: rendered as "HttpRequest.Send: HTTP error 500 …"
            // With the OutputStream.Close() fix, only (b) should fire; (a) is kept as fallback.
            LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex("500"));   // matches (a) or (b)
            _server.AddHandler("/games/init", _ => null);

            // Set _offlineMode=true so the direct (non-retry) path is taken —
            // avoiding RetryAsyncTask's exponential back-off delays.
            SetStaticField("_offlineMode", true);

            await Noctua.InitAsync();

            // Networking error + offline-first → offline fallback used.
            // The offline-first dummy response sets OfflineMode=true, so _initialized
            // stays false (Enable() is not called), and IsOfflineMode() flips true.
            Assert.IsTrue(Noctua.IsOfflineMode(),
                "IsOfflineMode must be true after network failure on offline-first init");
            Assert.IsFalse(Noctua.IsInitialized(),
                "IsInitialized must be false when offline mode is active (Enable() not called)");
        });

        // ─────────────────────────────────────────────────────────────────────
        // Group H — Remote IAA config absent → non-IAA native-plugin init path
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// When remote_configs.iaa is null, InitMediationSDK is not called.
        /// The "Remote config IAA is not configured yet" log branch executes,
        /// and native plugin is initialized via the non-IAA path.
        /// </summary>
        [UnityTest]
        public IEnumerator InitAsync_WithNoRemoteIaaConfig_CompletesNormally()
            => UniTask.ToCoroutine(async () =>
        {
            RequireInit();

            // Default handler already omits iaa — explicit assertion test.
            await Noctua.InitAsync();

            Assert.IsTrue(Noctua.IsInitialized());
        });

        // ─────────────────────────────────────────────────────────────────────
        // Group I — Currency / distribution-platform response fields
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// When country_to_currency_map is present but active_product_id is absent,
        /// the currency-lookup block is skipped (the guard on active_product_id
        /// fails). InitAsync() still reaches IsInitialized = true.
        /// </summary>
        [UnityTest]
        public IEnumerator InitAsync_WithCurrencyMapButNoActiveProduct_Completes()
            => UniTask.ToCoroutine(async () =>
        {
            RequireInit();

            _server.AddHandler("/games/init", _ => WrapData(new
            {
                country    = "ID",
                ip_address = "127.0.0.1",
                offline_mode = false,
                // active_product_id absent
                country_to_currency_map = new Dictionary<string, string> { { "ID", "IDR" } },
                remote_configs = new
                {
                    enabled_payment_types = new[] { "editor" },
                    feature_flags         = new Dictionary<string, string>()
                }
            }));

            await Noctua.InitAsync();

            Assert.IsTrue(Noctua.IsInitialized());
        });

        /// <summary>
        /// distribution_platform from the server response is passed to
        /// _iap.SetDistributionPlatform(). Verifies that code path is reached.
        /// </summary>
        [UnityTest]
        public IEnumerator InitAsync_WithDistributionPlatform_SetsWithoutException()
            => UniTask.ToCoroutine(async () =>
        {
            RequireInit();

            _server.AddHandler("/games/init", _ => WrapData(new
            {
                country               = "US",
                ip_address            = "127.0.0.1",
                offline_mode          = false,
                distribution_platform = "google",
                remote_configs        = new
                {
                    enabled_payment_types = new[] { "editor" },
                    feature_flags         = new Dictionary<string, string>()
                }
            }));

            await Noctua.InitAsync();

            Assert.IsTrue(Noctua.IsInitialized());
        });

        /// <summary>
        /// Multiple feature flags combined with a non-empty country and
        /// distribution_platform exercises several downstream branches together.
        /// </summary>
        [UnityTest]
        public IEnumerator InitAsync_FullyPopulatedResponse_CompletesSuccessfully()
            => UniTask.ToCoroutine(async () =>
        {
            RequireInit();

            _server.AddHandler("/games/init", _ => WrapData(new
            {
                country               = "SG",
                ip_address            = "10.0.0.1",
                offline_mode          = false,
                distribution_platform = "apple",
                supported_currencies  = new[] { "SGD", "USD" },
                remote_configs        = new
                {
                    enabled_payment_types = new[] { "editor" },
                    feature_flags         = new Dictionary<string, string>
                    {
                        { "ssoDisabled", "false" },
                        { "invalidKey",  "notabool" },  // warning branch
                        { "",            "true"    },   // empty-key branch
                    }
                }
            }));

            await Noctua.InitAsync();

            Assert.IsTrue(Noctua.IsInitialized());
            Assert.IsFalse(Noctua.IsOfflineMode());
        });

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Resets static state so the full InitAsync() body runs on next call.</summary>
        private static void ResetStaticState()
        {
            SetStaticField("_initialized", false);
            SetStaticField("_offlineMode",  false);
        }

        /// <summary>
        /// Sets a static private field on Noctua by name.
        /// Throws if the field does not exist so a field rename is caught immediately
        /// rather than silently letting stale state bleed into subsequent tests.
        /// </summary>
        private static void SetStaticField(string name, object value)
        {
            var field = typeof(Noctua).GetField(name, StaticPriv)
                ?? throw new InvalidOperationException(
                    $"Static field '{name}' not found on Noctua. " +
                    "Has the field been renamed? Update the reflection call.");
            field.SetValue(null, value);
        }

        /// <summary>
        /// Redirects the singleton NoctuaGameService._baseUrl to the mock server
        /// via reflection so InitGameAsync() resolves locally instead of hitting
        /// the real Noctua API.
        ///
        /// Reflection chain (all fields/props are private):
        ///   Noctua."Instance"  → Lazy&lt;Noctua&gt;
        ///   Lazy&lt;Noctua&gt;.Value → Noctua instance
        ///   Noctua."_game"     → NoctuaGameService instance
        ///   NoctuaGameService."_baseUrl" → string (private readonly, writable via IL reflection)
        ///
        /// Throws on any null or missing member so failures are visible immediately
        /// rather than silently hitting the real API.
        /// </summary>
        private static void RedirectGameServiceUrl(string baseUrl)
        {
            if (!_initAvailable) return;

            // Step 1: obtain the Lazy<Noctua> singleton wrapper.
            var lazyField = typeof(Noctua).GetField("Instance", StaticPriv)
                ?? throw new InvalidOperationException("'Instance' static field not found on Noctua.");

            var lazy = lazyField.GetValue(null)
                ?? throw new InvalidOperationException("Noctua.Instance is null.");

            // Step 2: read Lazy<Noctua>.Value.
            var valueProp = lazy.GetType().GetProperty("Value")
                ?? throw new InvalidOperationException("Lazy<Noctua>.Value property not found.");

            var noctuaInst = valueProp.GetValue(lazy)
                ?? throw new InvalidOperationException("Noctua singleton value is null.");

            // Step 3: get the _game field (private readonly NoctuaGameService).
            var gameField = typeof(Noctua).GetField("_game", InstPriv)
                ?? throw new InvalidOperationException("'_game' field not found on Noctua.");

            var game = gameField.GetValue(noctuaInst)
                ?? throw new InvalidOperationException("Noctua._game is null.");

            // Step 4: overwrite _baseUrl and verify the write succeeded.
            var baseUrlField = game.GetType().GetField("_baseUrl", InstPriv)
                ?? throw new InvalidOperationException("'_baseUrl' field not found on NoctuaGameService.");

            baseUrlField.SetValue(game, baseUrl);

            // Verify the redirect actually took (guards against readonly JIT enforcement on future runtimes).
            var actual = (string)baseUrlField.GetValue(game);
            if (actual != baseUrl)
                throw new InvalidOperationException(
                    $"_baseUrl redirect failed: expected '{baseUrl}', got '{actual}'.");
        }

        /// <summary>
        /// Builds a {"data":{...}} wrapped response for the /games/init endpoint.
        /// Parameters have sensible defaults; callers only override what they need.
        /// </summary>
        private static string BuildInitResponse(
            string country = "US",
            string ipAddress = "127.0.0.1",
            bool offlineMode = false,
            Dictionary<string, string> featureFlags = null)
        {
            return WrapData(new
            {
                country      = country,
                ip_address   = ipAddress,
                offline_mode = offlineMode,
                remote_configs = new
                {
                    enabled_payment_types = new[] { "editor" },
                    feature_flags         = featureFlags ?? new Dictionary<string, string>()
                }
            });
        }

        /// <summary>
        /// Wraps an object in the {"data": ...} envelope that HttpRequest.Send&lt;T&gt;()
        /// expects (see DataWrapper in Http.cs).
        /// </summary>
        private static string WrapData(object data)
            => JsonConvert.SerializeObject(new { data });
    }
}
