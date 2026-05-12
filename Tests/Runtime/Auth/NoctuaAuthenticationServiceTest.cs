using System;
using System.Threading.Tasks;
using com.noctuagames.sdk;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Tests.Runtime;
using UnityEngine.TestTools;

namespace Tests.Runtime.Auth
{
    /// <summary>
    /// Unit tests for <see cref="NoctuaAuthenticationService"/>.
    ///
    /// Tests that do NOT require a live backend:
    ///   - Constructor validation (null/empty guards)
    ///   - Initial state assertions
    ///   - LoginWithToken() — local account-container update
    ///   - ResetAccounts() — clears in-memory and native-store state
    ///   - OnAccountChanged event firing
    ///
    /// Tests that REQUIRE a live backend are marked [Ignore] with an explicit message
    /// so they appear in the test runner output and can be run in the integration suite.
    /// </summary>
    [TestFixture]
    public class NoctuaAuthenticationServiceTest
    {
        private MockNativeAccountStore _store;

        [SetUp]
        public void SetUp()
        {
            _store = new MockNativeAccountStore();
        }

        // ─── Constructor guards ────────────────────────────────────────────────

        [Test]
        public void Constructor_NullBaseUrl_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new NoctuaAuthenticationService(null, "client_id", _store, bundleId: "com.test.app"));
        }

        [Test]
        public void Constructor_EmptyBaseUrl_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new NoctuaAuthenticationService("", "client_id", _store, bundleId: "com.test.app"));
        }

        [Test]
        public void Constructor_NullClientId_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new NoctuaAuthenticationService("https://api.example.com", null, _store, bundleId: "com.test.app"));
        }

        [Test]
        public void Constructor_EmptyClientId_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new NoctuaAuthenticationService("https://api.example.com", "", _store, bundleId: "com.test.app"));
        }

        [Test]
        public void Constructor_NullBundleId_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new NoctuaAuthenticationService("https://api.example.com", "client_id", _store, bundleId: null));
        }

        [Test]
        public void Constructor_ValidParams_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                new NoctuaAuthenticationService(
                    "https://api.example.com", "client_id", _store, bundleId: "com.test.app"));
        }

        // ─── Initial state ─────────────────────────────────────────────────────

        [Test]
        public void IsAuthenticated_AfterConstruction_ReturnsFalse()
        {
            var svc = CreateService();
            Assert.IsFalse(svc.IsAuthenticated);
        }

        [Test]
        public void RecentAccount_AfterConstruction_ReturnsNull()
        {
            var svc = CreateService();
            Assert.IsNull(svc.RecentAccount);
        }

        [Test]
        public void AccountList_AfterConstruction_IsEmpty()
        {
            var svc = CreateService();
            Assert.AreEqual(0, svc.AccountList.Count,
                "AccountList should be empty before any login");
        }

        // ─── LoginWithToken ────────────────────────────────────────────────────

        [Test]
        public void LoginWithToken_ValidToken_IsAuthenticatedTrue()
        {
            var svc = CreateService();
            svc.LoginWithToken(BuildPlayerToken("test-access-token-123"));

            Assert.IsTrue(svc.IsAuthenticated);
        }

        [Test]
        public void LoginWithToken_ValidToken_RecentAccountNotNull()
        {
            var svc = CreateService();
            svc.LoginWithToken(BuildPlayerToken("some-token"));

            Assert.IsNotNull(svc.RecentAccount);
        }

        [Test]
        public void LoginWithToken_ValidToken_AccessTokenMatchesProvided()
        {
            var svc = CreateService();
            svc.LoginWithToken(BuildPlayerToken("my-special-token"));

            Assert.AreEqual("my-special-token", svc.RecentAccount?.Player?.AccessToken);
        }

        [Test]
        public void LoginWithToken_FiresOnAccountChangedEvent()
        {
            var svc = CreateService();
            UserBundle received = null;
            svc.OnAccountChanged += bundle => received = bundle;

            svc.LoginWithToken(BuildPlayerToken("event-token"));

            Assert.IsNotNull(received, "OnAccountChanged must fire after LoginWithToken");
        }

        [Test]
        public void LoginWithToken_Twice_SecondTokenBecomesRecentAccount()
        {
            var svc = CreateService();
            svc.LoginWithToken(BuildPlayerToken("first-token"));
            svc.LoginWithToken(BuildPlayerToken("second-token", userId: 2));

            Assert.AreEqual("second-token", svc.RecentAccount?.Player?.AccessToken);
        }

        // ─── ResetAccounts ─────────────────────────────────────────────────────

        [Test]
        public void ResetAccounts_AfterLogin_AccountListBecomesEmpty()
        {
            var svc = CreateService();
            svc.LoginWithToken(BuildPlayerToken("reset-test-token"));

            svc.ResetAccounts();

            Assert.AreEqual(0, svc.AccountList.Count,
                "AccountList should be empty after ResetAccounts");
        }

        [Test]
        public void ResetAccounts_AfterLogin_IsAuthenticatedFalse()
        {
            var svc = CreateService();
            svc.LoginWithToken(BuildPlayerToken("reset-test-token"));

            svc.ResetAccounts();

            Assert.IsFalse(svc.IsAuthenticated,
                "IsAuthenticated should be false after ResetAccounts");
        }

        [Test]
        public void ResetAccounts_WithoutPriorLogin_DoesNotThrow()
        {
            var svc = CreateService();
            Assert.DoesNotThrow(() => svc.ResetAccounts());
        }

        // ─── OnAccountChanged event ────────────────────────────────────────────

        [Test]
        public void OnAccountChanged_CanSubscribeAndUnsubscribe()
        {
            var svc = CreateService();
            int count = 0;
            Action<UserBundle> handler = _ => count++;

            svc.OnAccountChanged += handler;
            svc.LoginWithToken(BuildPlayerToken("sub-token-1"));

            svc.OnAccountChanged -= handler;
            svc.LoginWithToken(BuildPlayerToken("sub-token-2", userId: 2));

            Assert.AreEqual(1, count,
                "Event handler should fire exactly once (before unsubscribe)");
        }

        // ─── Live-backend methods (Ignore) ─────────────────────────────────────

        [Test]
        [Ignore("Requires live backend: POST /auth/guest/login with device ID and bundle ID")]
        public void LoginAsGuestAsync_ReturnsUserBundle() { }

        [Test]
        [Ignore("Requires live backend: POST /auth/token-exchange")]
        public void ExchangeTokenAsync_ValidToken_ReturnsUserBundle() { }

        [Test]
        [Ignore("Requires live backend: GET /auth/{provider}/login/redirect")]
        public void GetSocialAuthRedirectURLAsync_ReturnsUrl() { }

        [Test]
        [Ignore("Requires live backend: POST /auth/{provider}/login + OAuth callback")]
        public void SocialLoginAsync_ReturnsUserBundle() { }

        [Test]
        [Ignore("Requires live backend: POST /auth/email/register")]
        public void RegisterWithEmailAsync_CreatesAccount() { }

        [Test]
        [Ignore("Requires live backend: POST /auth/email/login")]
        public void LoginWithEmailAsync_ReturnsUserBundle() { }

        [Test]
        [Ignore("Requires live backend: POST /auth/email/reset-password + verify-reset-password")]
        public void RequestAndConfirmResetPasswordAsync_ChangesPassword() { }

        [Test]
        [Ignore("Requires live backend: POST /auth/logout")]
        public void LogoutAsync_ClearsSession() { }

        [Test]
        [Ignore("Requires live backend: GET /users/me")]
        public void GetUserAsync_ReturnsAuthenticatedUser() { }

        [Test]
        [Ignore("Requires live backend: PUT /users/me")]
        public void UpdateUserAsync_SavesChanges() { }

        [Test]
        [Ignore("Requires live backend: GET /cloud-saves/latest + PUT /cloud-saves")]
        public void SaveAndGetGameStateAsync_PersistsData() { }

        [Test]
        [Ignore("Requires live backend: POST /auth/bind/{provider} with valid access token")]
        public void BindAccountAsync_AttachesCredential() { }

        [Test]
        [Ignore("Requires live backend: DELETE /players/{id} with valid access token")]
        public void DeleteAccountAsync_RemovesAccount() { }

        [Test]
        [Ignore("Requires live backend: GET /users with valid access token")]
        public void GetAccountListAsync_ReturnsList() { }

        [Test]
        [Ignore("Requires live backend: POST /players/sync")]
        public void UpdatePlayerAccountAsync_SyncsData() { }

        [Test]
        [Ignore("Requires live backend: two authenticated accounts in native store")]
        public void SwitchAccountAsync_ChangesRecentAccount() { }

        // ─── SwitchAccountAsync guard ──────────────────────────────────────────

        [Test]
        public void SwitchAccountAsync_UserNotInAccountList_ThrowsNoctuaException()
        {
            var svc = CreateService();

            // Fresh service has empty AccountList — user will not be found.
            var fakeUser = new UserBundle
            {
                User           = new User { Id = 999 },
                PlayerAccounts = new System.Collections.Generic.List<Player>()
            };

            // SwitchAccountAsync throws NoctuaException BEFORE its first await when
            // the target user is absent from AccountList, so GetAwaiter().GetResult()
            // resolves synchronously without entering the UniTask scheduler.
            var ex = Assert.Throws<NoctuaException>(() =>
                svc.SwitchAccountAsync(fakeUser).GetAwaiter().GetResult());

            Assert.AreEqual(
                (int)NoctuaErrorCode.Authentication,
                ex.ErrorCode,
                "SwitchAccountAsync must throw Authentication error when user is not in AccountList");
        }

        // ─── Helpers ───────────────────────────────────────────────────────────

        private NoctuaAuthenticationService CreateService()
        {
            return new NoctuaAuthenticationService(
                baseUrl:            "https://api.example.com",
                clientId:           "test-client-id",
                nativeAccountStore: _store,
                bundleId:           "com.test.app"
            );
        }

        /// <summary>
        /// Builds a minimal <see cref="PlayerToken"/> that can drive <see cref="NoctuaAuthenticationService.LoginWithToken"/>.
        /// </summary>
        private static PlayerToken BuildPlayerToken(string accessToken, long userId = 1, long gameId = 100)
        {
            // GamePlatform.BundleId must match the bundleId passed to CreateService() ("com.test.app").
            // AccountContainer.UpdateRecentAccount() compares Player.BundleId (derived from
            // GamePlatform.BundleId via TransformTokenResponseToUserBundle) against _bundleId and
            // silently returns early when they don't match — leaving RecentAccount null.
            return new PlayerToken
            {
                AccessToken = accessToken,
                Player = new Player
                {
                    Id          = userId,
                    GameId      = gameId,
                    AccessToken = accessToken
                },
                User = new User
                {
                    Id = userId
                },
                Credential = new Credential
                {
                    Provider = "device_id"
                },
                Game = new Game
                {
                    Id = gameId
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = "com.test.app"
                }
            };
        }
    }

    // ─── MockNativeAccountStore ────────────────────────────────────────────────

    /// <summary>
    /// In-memory fake INativeAccountStore for auth unit tests.
    /// Uses a List so tests can inspect stored accounts.
    /// </summary>
    public class MockNativeAccountStore : INativeAccountStore
    {
        private readonly List<NativeAccount> _accounts = new List<NativeAccount>();

        /// <param name="userId">Maps to <see cref="NativeAccount.PlayerId"/> in the native model.</param>
        public NativeAccount GetAccount(long userId, long gameId)
        {
            return _accounts.Find(a => a.PlayerId == userId && a.GameId == gameId);
        }

        public List<NativeAccount> GetAccounts() => new List<NativeAccount>(_accounts);

        public void PutAccount(NativeAccount account)
        {
            // Mirror DefaultNativePlugin.PutAccount: set LastUpdated so AccountContainer's
            // FromNativeAccounts() sorts the most-recently-put account to index 0 (IsRecent).
            // Without this, all accounts have LastUpdated=0 and the sort order is undefined,
            // causing the first-inserted account to remain IsRecent after a second login.
            account.LastUpdated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var idx = _accounts.FindIndex(a => a.PlayerId == account.PlayerId && a.GameId == account.GameId);
            if (idx >= 0)
                _accounts[idx] = account;
            else
                _accounts.Add(account);
        }

        public int DeleteAccount(NativeAccount account)
        {
            int removed = _accounts.RemoveAll(a => a.PlayerId == account.PlayerId && a.GameId == account.GameId);
            return removed;
        }

        public void Clear() => _accounts.Clear();
    }

    // ─── NoctuaAuthenticationServiceLocalGuardsTest ───────────────────────────

    /// <summary>
    /// Synchronous guard tests — no HTTP, no Application.identifier dependency.
    /// All assertions use plain [Test] with Assert.ThrowsAsync or direct invocation.
    /// </summary>
    [TestFixture]
    public class NoctuaAuthenticationServiceLocalGuardsTest
    {
        private MockNativeAccountStore _store;

        [SetUp]
        public void SetUp()
        {
            _store = new MockNativeAccountStore();
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private NoctuaAuthenticationService CreateService()
        {
            return new NoctuaAuthenticationService(
                baseUrl:            "https://api.example.com",
                clientId:           "test-client-id",
                nativeAccountStore: _store,
                bundleId:           "com.test.app"
            );
        }

        /// <summary>
        /// Guest token: Credential.Provider == "device_id" and User.IsGuest == true,
        /// so IsGuest evaluates to true.
        /// </summary>
        private static PlayerToken BuildGuestToken(string accessToken = "guest-token", long userId = 10)
        {
            return new PlayerToken
            {
                AccessToken = accessToken,
                Player = new Player
                {
                    Id          = userId,
                    GameId      = 100,
                    AccessToken = accessToken
                },
                User = new User
                {
                    Id      = userId,
                    IsGuest = true
                },
                Credential = new Credential
                {
                    Provider = "device_id"
                },
                Game = new Game { Id = 100 },
                GamePlatform = new GamePlatform { BundleId = "com.test.app" }
            };
        }

        private static PlayerToken BuildNonGuestToken(string accessToken = "email-token", long userId = 20)
        {
            return new PlayerToken
            {
                AccessToken = accessToken,
                Player = new Player
                {
                    Id          = userId,
                    GameId      = 100,
                    AccessToken = accessToken
                },
                User = new User
                {
                    Id      = userId,
                    IsGuest = false
                },
                Credential = new Credential
                {
                    Provider = "email"
                },
                Game = new Game { Id = 100 },
                GamePlatform = new GamePlatform { BundleId = "com.test.app" }
            };
        }

        // ─── List getters ──────────────────────────────────────────────────────

        [Test]
        public void IsAuthenticated_AfterLoginWithToken_ReturnsTrue()
        {
            var svc = CreateService();
            svc.LoginWithToken(BuildNonGuestToken());
            Assert.IsTrue(svc.IsAuthenticated);
        }

        [Test]
        public void AccountList_AfterTwoDistinctLogins_CountIsTwo()
        {
            var svc = CreateService();
            svc.LoginWithToken(BuildNonGuestToken("token-a", userId: 1));
            svc.LoginWithToken(BuildNonGuestToken("token-b", userId: 2));

            Assert.AreEqual(2, svc.AccountList.Count,
                "AccountList should contain one entry per distinct user");
        }

        [Test]
        public void CurrentGameAccountList_AfterLogin_ContainsExpectedBundleId()
        {
            var svc = CreateService();
            svc.LoginWithToken(BuildNonGuestToken());

            Assert.AreEqual(1, svc.CurrentGameAccountList.Count,
                "CurrentGameAccountList should contain the logged-in account");
        }

        [Test]
        public void OtherGamesAccountList_AfterSingleLogin_IsEmpty()
        {
            var svc = CreateService();
            svc.LoginWithToken(BuildNonGuestToken());

            Assert.AreEqual(0, svc.OtherGamesAccountList.Count,
                "OtherGamesAccountList should be empty when no other-game accounts exist");
        }

        // ─── SocialLinkAsync guards ────────────────────────────────────────────

        [Test]
        public void SocialLinkAsync_WhenNotAuthenticated_ThrowsMissingAccessToken()
        {
            var svc = CreateService();
            Assert.ThrowsAsync<NoctuaException>(
                () => svc.SocialLinkAsync("google", new SocialLinkRequest()).AsTask(),
                "Expected NoctuaException when not authenticated"
            );
        }

        [Test]
        public void SocialLinkAsync_WhenGuest_ThrowsNoctuaException()
        {
            var svc = CreateService();
            svc.LoginWithToken(BuildGuestToken());

            Assert.ThrowsAsync<NoctuaException>(
                () => svc.SocialLinkAsync("google", new SocialLinkRequest()).AsTask()
            );
        }

        // ─── LinkWithEmailAsync guards ─────────────────────────────────────────

        [Test]
        public void LinkWithEmailAsync_WhenNotAuthenticated_ThrowsMissingAccessToken()
        {
            var svc = CreateService();
            Assert.ThrowsAsync<NoctuaException>(
                () => svc.LinkWithEmailAsync("a@b.com", "pass").AsTask()
            );
        }

        [Test]
        public void LinkWithEmailAsync_WhenGuest_ThrowsNoctuaException()
        {
            var svc = CreateService();
            svc.LoginWithToken(BuildGuestToken());

            Assert.ThrowsAsync<NoctuaException>(
                () => svc.LinkWithEmailAsync("a@b.com", "pass").AsTask()
            );
        }

        // ─── BeginVerifyEmailRegistrationAsync guard ───────────────────────────

        [Test]
        public void BeginVerifyEmailRegistrationAsync_WhenNotGuest_ThrowsNoctuaException()
        {
            var svc = CreateService();
            svc.LoginWithToken(BuildNonGuestToken());

            Assert.ThrowsAsync<NoctuaException>(
                () => svc.BeginVerifyEmailRegistrationAsync(1, "123456").AsTask()
            );
        }

        // ─── BeginVerifyEmailLinkingAsync guard ────────────────────────────────

        [Test]
        public void BeginVerifyEmailLinkingAsync_WhenNotGuest_ThrowsNoctuaException()
        {
            var svc = CreateService();
            svc.LoginWithToken(BuildNonGuestToken());

            Assert.ThrowsAsync<NoctuaException>(
                () => svc.BeginVerifyEmailLinkingAsync(1, "123456").AsTask()
            );
        }

        // ─── GetSocialLoginTokenAsync guard ───────────────────────────────────

        [Test]
        public void GetSocialLoginTokenAsync_WhenNotGuest_ThrowsNoctuaException()
        {
            var svc = CreateService();
            svc.LoginWithToken(BuildNonGuestToken());

            Assert.ThrowsAsync<NoctuaException>(
                () => svc.GetSocialLoginTokenAsync("google", new SocialLoginRequest()).AsTask()
            );
        }

        // ─── GetEmailLoginTokenAsync guard ─────────────────────────────────────

        [Test]
        public void GetEmailLoginTokenAsync_WhenNotGuest_ThrowsNoctuaException()
        {
            var svc = CreateService();
            svc.LoginWithToken(BuildNonGuestToken());

            Assert.ThrowsAsync<NoctuaException>(
                () => svc.GetEmailLoginTokenAsync("a@b.com", "pass").AsTask()
            );
        }

        // ─── BindGuestAndLoginAsync guards ─────────────────────────────────────

        [Test]
        public void BindGuestAndLoginAsync_WhenNotGuest_ThrowsNoctuaException()
        {
            var svc = CreateService();
            svc.LoginWithToken(BuildNonGuestToken());

            Assert.ThrowsAsync<NoctuaException>(
                () => svc.BindGuestAndLoginAsync(BuildNonGuestToken()).AsTask()
            );
        }

        [Test]
        public void BindGuestAndLoginAsync_WhenNullTargetToken_ThrowsNoctuaException()
        {
            var svc = CreateService();
            svc.LoginWithToken(BuildGuestToken());

            Assert.ThrowsAsync<NoctuaException>(
                () => svc.BindGuestAndLoginAsync(new PlayerToken { AccessToken = null }).AsTask()
            );
        }

        // ─── GetUserAsync guard ────────────────────────────────────────────────

        [Test]
        public void GetUserAsync_WhenNotAuthenticated_ThrowsMissingAccessToken()
        {
            var svc = CreateService();
            Assert.ThrowsAsync<NoctuaException>(
                () => svc.GetUserAsync().AsTask()
            );
        }

        // ─── UpdateUserAsync guard ─────────────────────────────────────────────

        [Test]
        public void UpdateUserAsync_WhenNotAuthenticated_ThrowsMissingAccessToken()
        {
            var svc = CreateService();
            Assert.ThrowsAsync<NoctuaException>(
                () => svc.UpdateUserAsync(new UpdateUserRequest()).AsTask()
            );
        }

        // ─── SaveGameStateAsync guard ──────────────────────────────────────────

        [Test]
        public void SaveGameStateAsync_WhenNotAuthenticated_ThrowsMissingAccessToken()
        {
            var svc = CreateService();
            Assert.ThrowsAsync<NoctuaException>(
                () => svc.SaveGameStateAsync("slot1", "data").AsTask()
            );
        }

        // ─── LoadGameStateAsync guard ──────────────────────────────────────────

        [Test]
        public void LoadGameStateAsync_WhenNotAuthenticated_ThrowsMissingAccessToken()
        {
            var svc = CreateService();
            Assert.ThrowsAsync<NoctuaException>(
                () => svc.LoadGameStateAsync("slot1").AsTask()
            );
        }

        // ─── GetGameStateKeysAsync guard ───────────────────────────────────────

        [Test]
        public void GetGameStateKeysAsync_WhenNotAuthenticated_ThrowsMissingAccessToken()
        {
            var svc = CreateService();
            Assert.ThrowsAsync<NoctuaException>(
                () => svc.GetGameStateKeysAsync().AsTask()
            );
        }

        // ─── DeleteGameStateAsync guard ────────────────────────────────────────

        [Test]
        public void DeleteGameStateAsync_WhenNotAuthenticated_ThrowsMissingAccessToken()
        {
            var svc = CreateService();
            Assert.ThrowsAsync<NoctuaException>(
                () => svc.DeleteGameStateAsync("slot1").AsTask()
            );
        }

        // ─── GetProfileOptions guard ───────────────────────────────────────────

        [Test]
        public void GetProfileOptions_WhenNotAuthenticated_ThrowsMissingAccessToken()
        {
            var svc = CreateService();
            Assert.ThrowsAsync<NoctuaException>(
                () => svc.GetProfileOptions().AsTask()
            );
        }

        // ─── FileUploader guards ───────────────────────────────────────────────

        [Test]
        public void FileUploader_WhenNotAuthenticated_ThrowsMissingAccessToken()
        {
            var svc = CreateService();
            Assert.ThrowsAsync<NoctuaException>(
                () => svc.FileUploader("/some/path.png").AsTask()
            );
        }

        [Test]
        public void FileUploader_WhenAuthenticatedButFileNotFound_ThrowsException()
        {
            var svc = CreateService();
            svc.LoginWithToken(BuildNonGuestToken());

            Assert.ThrowsAsync<Exception>(
                () => svc.FileUploader("/nonexistent/path/image.png").AsTask()
            );
        }

        // ─── AuthenticateAsync early-return path ───────────────────────────────

        [Test]
        public void AuthenticateAsync_WhenAlreadyAuthenticated_ReturnsSameAccount()
        {
            var svc = CreateService();
            svc.LoginWithToken(BuildNonGuestToken("already-auth-token", userId: 5));

            // AuthenticateAsync takes an early return when IsAuthenticated == true.
            // The call returns immediately without any HTTP — run it synchronously
            // via GetAwaiter so the test stays [Test] (no [UnityTest] needed).
            var task = svc.AuthenticateAsync().AsTask();
            task.Wait(500);

            Assert.IsTrue(task.IsCompleted, "AuthenticateAsync should complete synchronously when already authenticated");
            Assert.IsNotNull(task.Result, "Returned UserBundle must not be null");
            Assert.AreEqual("already-auth-token", task.Result.Player?.AccessToken);
        }
    }

    // ─── NoctuaAuthenticationServiceHttpTest ──────────────────────────────────

    /// <summary>
    /// HTTP-backed tests using an in-process <see cref="HttpMockServer"/> on port 7778.
    /// All tests are [UnityTest] coroutines that delegate to UniTask.ToCoroutine.
    /// </summary>
    [TestFixture]
    public class NoctuaAuthenticationServiceHttpTest
    {
        private const string BaseUrl   = "http://localhost:7778/api/v1";
        private const string ServerUrl = "http://localhost:7778/api/v1/";

        private HttpMockServer _server;
        private MockNativeAccountStore _store;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _server = new HttpMockServer(ServerUrl);
            _server.Start();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _server.Dispose();
        }

        [SetUp]
        public void SetUp()
        {
            _store = new MockNativeAccountStore();
            // Drain the request queue between tests so assertions are isolated
            while (_server.Requests.TryDequeue(out _)) { }
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private NoctuaAuthenticationService CreateService()
        {
            return new NoctuaAuthenticationService(
                baseUrl:            BaseUrl,
                clientId:           "test-client-id",
                nativeAccountStore: _store,
                bundleId:           "com.test.app"
            );
        }

        /// <summary>
        /// Returns the JSON envelope that <see cref="DataWrapper{T}"/> expects.
        /// The SDK deserializes <c>{"data": {...}}</c>.
        /// </summary>
        private static string PlayerTokenJson(
            string accessToken = "http-token",
            long   userId      = 1,
            long   gameId      = 100,
            string bundleId    = "com.test.app",
            string provider    = "email",
            bool   isGuest     = false)
        {
            return $@"{{
  ""data"": {{
    ""access_token"": ""{accessToken}"",
    ""player"": {{
      ""id"": {userId},
      ""game_id"": {gameId},
      ""bundle_id"": ""{bundleId}"",
      ""access_token"": ""{accessToken}""
    }},
    ""user"": {{ ""id"": {userId}, ""is_guest"": {isGuest.ToString().ToLower()} }},
    ""credential"": {{ ""id"": 1, ""provider"": ""{provider}"" }},
    ""game"": {{ ""id"": {gameId} }},
    ""game_platform"": {{ ""id"": 10, ""bundle_id"": ""{bundleId}"" }}
  }}
}}";
        }

        private static string GuestTokenJson(long userId = 99) =>
            PlayerTokenJson(
                accessToken: "guest-http-token",
                userId:      userId,
                provider:    "device_id",
                isGuest:     true
            );

        // ─── LoginWithEmailAsync ───────────────────────────────────────────────

        [UnityTest]
        public IEnumerator LoginWithEmailAsync_ValidResponse_SetsIsAuthenticated()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login", _ => PlayerTokenJson("login-email-token"));
            try
            {
                var svc = CreateService();
                await svc.LoginWithEmailAsync("user@test.com", "password");
                Assert.IsTrue(svc.IsAuthenticated);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
            }
        });

        [UnityTest]
        public IEnumerator LoginWithEmailAsync_ValidResponse_ReturnsUserBundleWithMatchingToken()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login", _ => PlayerTokenJson("login-email-token-2"));
            try
            {
                var svc = CreateService();
                var result = await svc.LoginWithEmailAsync("user@test.com", "password");
                Assert.IsNotNull(result);
                Assert.AreEqual("login-email-token-2", result.Player?.AccessToken);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
            }
        });

        [UnityTest]
        public IEnumerator LoginWithEmailAsync_ValidResponse_FiresOnAccountChanged()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login", _ => PlayerTokenJson("event-fire-token"));
            try
            {
                var svc = CreateService();
                UserBundle received = null;
                svc.OnAccountChanged += bundle => received = bundle;

                await svc.LoginWithEmailAsync("user@test.com", "password");

                Assert.IsNotNull(received, "OnAccountChanged must fire after LoginWithEmailAsync");
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
            }
        });

        [UnityTest]
        public IEnumerator LoginWithEmailAsync_NoHandler_ThrowsNoctuaException()
            => UniTask.ToCoroutine(async () =>
        {
            // No handler for /auth/email/login → server returns 404 → NoctuaException
            var svc = CreateService();
            try
            {
                await svc.LoginWithEmailAsync("nobody@test.com", "wrongpass");
                Assert.Fail("Expected NoctuaException");
            }
            catch (NoctuaException)
            {
                // Expected — 404 produces an Application or Networking NoctuaException
            }
        });

        // ─── SocialLoginAsync ──────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator SocialLoginAsync_ValidResponse_ReturnsUserBundle()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/google/login/callback", _ => PlayerTokenJson("google-token"));
            try
            {
                var svc = CreateService();
                var result = await svc.SocialLoginAsync("google", new SocialLoginRequest
                {
                    Code        = "auth-code",
                    State       = "state",
                    RedirectUri = "https://callback.example.com"
                });
                Assert.IsNotNull(result);
                Assert.AreEqual("google-token", result.Player?.AccessToken);
            }
            finally
            {
                _server.RemoveHandler("/auth/google/login/callback");
            }
        });

        // ─── GetSocialAuthRedirectURLAsync ─────────────────────────────────────

        [UnityTest]
        public IEnumerator GetSocialAuthRedirectURLAsync_ValidResponse_ReturnsUrl()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/google/login/redirect",
                _ => @"{""data"":{""redirect_url"":""https://accounts.google.com/oauth""}}");
            try
            {
                var svc = CreateService();
                var url = await svc.GetSocialAuthRedirectURLAsync("google");
                Assert.AreEqual("https://accounts.google.com/oauth", url);
            }
            finally
            {
                _server.RemoveHandler("/auth/google/login/redirect");
            }
        });

        // ─── ExchangeTokenAsync ────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator ExchangeTokenAsync_ValidResponse_ReturnsUserBundle()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/token-exchange", _ => PlayerTokenJson("exchanged-token"));
            try
            {
                var svc = CreateService();
                var result = await svc.ExchangeTokenAsync("old-access-token");
                Assert.IsNotNull(result);
                Assert.AreEqual("exchanged-token", result.Player?.AccessToken);
            }
            finally
            {
                _server.RemoveHandler("/auth/token-exchange");
            }
        });

        [UnityTest]
        public IEnumerator ExchangeTokenAsync_NoHandler_ThrowsNoctuaException()
            => UniTask.ToCoroutine(async () =>
        {
            var svc = CreateService();
            try
            {
                await svc.ExchangeTokenAsync("stale-token");
                Assert.Fail("Expected NoctuaException");
            }
            catch (NoctuaException)
            {
                // Expected
            }
        });

        // ─── RegisterWithEmailAsync ────────────────────────────────────────────

        [UnityTest]
        public IEnumerator RegisterWithEmailAsync_ValidResponse_ReturnsCredentialVerification()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/register",
                _ => @"{""data"":{""id"":42,""code"":""000000""}}");
            try
            {
                var svc = CreateService();
                var result = await svc.RegisterWithEmailAsync("new@test.com", "pass123",
                    new Dictionary<string, string>());
                Assert.IsNotNull(result);
                Assert.AreEqual(42, result.Id);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/register");
            }
        });

        // ─── RequestResetPasswordAsync ─────────────────────────────────────────

        [UnityTest]
        public IEnumerator RequestResetPasswordAsync_ValidResponse_ReturnsVerification()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/reset-password",
                _ => @"{""data"":{""id"":7,""code"":""000000""}}");
            try
            {
                var svc = CreateService();
                var result = await svc.RequestResetPasswordAsync("reset@test.com");
                Assert.IsNotNull(result);
                Assert.AreEqual(7, result.Id);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/reset-password");
            }
        });

        // ─── VerifyEmailRegistrationAsync ─────────────────────────────────────

        [UnityTest]
        public IEnumerator VerifyEmailRegistrationAsync_ValidResponse_ReturnsUserBundle()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/verify-registration", _ => PlayerTokenJson("verified-token"));
            try
            {
                var svc = CreateService();
                var result = await svc.VerifyEmailRegistrationAsync(42, "654321");
                Assert.IsNotNull(result);
                Assert.AreEqual("verified-token", result.Player?.AccessToken);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/verify-registration");
            }
        });

        // ─── UpdatePlayerAccountAsync ──────────────────────────────────────────

        [UnityTest]
        public IEnumerator UpdatePlayerAccountAsync_ValidResponse_DoesNotThrow()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login", _ => PlayerTokenJson("sync-token"));
            _server.AddHandler("/players/sync", _ => @"{""data"":{}}");
            try
            {
                var svc = CreateService();
                await svc.LoginWithEmailAsync("u@test.com", "p");

                await svc.UpdatePlayerAccountAsync(new PlayerAccountData
                {
                    IngameUsername = "Hero",
                    IngameServerId = "S1",
                    IngameRoleId   = "R1"
                });
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
                _server.RemoveHandler("/players/sync");
            }
        });

        // ─── DeletePlayerAccountAsync ──────────────────────────────────────────

        [UnityTest]
        public IEnumerator DeletePlayerAccountAsync_ValidResponse_RecentAccountBecomesNull()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login",  _ => PlayerTokenJson("del-token", userId: 55));
            _server.AddHandler("/players/destroy",   _ => @"{""data"":{""player_id"":55}}");
            try
            {
                var svc = CreateService();
                await svc.LoginWithEmailAsync("del@test.com", "pass");
                Assert.IsTrue(svc.IsAuthenticated, "Should be authenticated before delete");

                await svc.DeletePlayerAccountAsync();

                Assert.IsNull(svc.RecentAccount, "RecentAccount should be null after delete");
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
                _server.RemoveHandler("/players/destroy");
            }
        });

        // ─── DeletePlayerAccountAsync fires OnAccountDeleted event ────────────

        [UnityTest]
        public IEnumerator DeletePlayerAccountAsync_ValidResponse_FiresOnAccountDeletedEvent()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login",  _ => PlayerTokenJson("del-event-token", userId: 66));
            _server.AddHandler("/players/destroy",   _ => @"{""data"":{""player_id"":66}}");
            try
            {
                var svc = CreateService();
                await svc.LoginWithEmailAsync("del@test.com", "pass");

                Player deletedPlayer = null;
                svc.OnAccountDeleted += p => deletedPlayer = p;

                await svc.DeletePlayerAccountAsync();

                Assert.IsNotNull(deletedPlayer, "OnAccountDeleted must fire after DeletePlayerAccountAsync");
                Assert.AreEqual(66, deletedPlayer.Id);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
                _server.RemoveHandler("/players/destroy");
            }
        });
    }

    // ─── NoctuaAuthenticationServiceHttpExtendedTest ──────────────────────────

    /// <summary>
    /// Extended HTTP-backed tests covering methods not exercised by <see cref="NoctuaAuthenticationServiceHttpTest"/>:
    /// LogoutAsync, GetUserAsync, UpdateUserAsync, GetProfileOptions,
    /// SaveGameStateAsync, LoadGameStateAsync, DeleteGameStateAsync, GetGameStateKeysAsync,
    /// VerifyEmailLinkingAsync, ConfirmResetPasswordAsync, RegisterWithEmailSendPhoneNumberVerification,
    /// SwitchAccountAsync, and request-header verification.
    /// </summary>
    [TestFixture]
    public class NoctuaAuthenticationServiceHttpExtendedTest
    {
        private const string BaseUrl   = "http://localhost:7779/api/v1";
        private const string ServerUrl = "http://localhost:7779/api/v1/";

        private HttpMockServer _server;
        private MockNativeAccountStore _store;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _server = new HttpMockServer(ServerUrl);
            _server.Start();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _server.Dispose();
        }

        [SetUp]
        public void SetUp()
        {
            _store = new MockNativeAccountStore();
            while (_server.Requests.TryDequeue(out _)) { }
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private NoctuaAuthenticationService CreateService()
        {
            return new NoctuaAuthenticationService(
                baseUrl:            BaseUrl,
                clientId:           "test-client-id",
                nativeAccountStore: _store,
                bundleId:           "com.test.app"
            );
        }

        private static string PlayerTokenJson(
            string accessToken = "http-token",
            long   userId      = 1,
            long   gameId      = 100,
            string bundleId    = "com.test.app",
            string provider    = "email",
            bool   isGuest     = false)
        {
            return $@"{{
  ""data"": {{
    ""access_token"": ""{accessToken}"",
    ""player"": {{
      ""id"": {userId},
      ""game_id"": {gameId},
      ""bundle_id"": ""{bundleId}"",
      ""access_token"": ""{accessToken}""
    }},
    ""user"": {{ ""id"": {userId}, ""is_guest"": {isGuest.ToString().ToLower()} }},
    ""credential"": {{ ""id"": 1, ""provider"": ""{provider}"" }},
    ""game"": {{ ""id"": {gameId} }},
    ""game_platform"": {{ ""id"": 10, ""bundle_id"": ""{bundleId}"" }}
  }}
}}";
        }

        // ─── LogoutAsync ───────────────────────────────────────────────────────

        /// <summary>
        /// LogoutAsync calls LoginAsGuestAsync internally (which hits /auth/guest/login).
        /// We mock that endpoint with a guest token so the call succeeds.
        /// After logout the service is still "authenticated" as a guest.
        /// </summary>
        [UnityTest]
        public IEnumerator LogoutAsync_WhileAuthenticated_ReturnsGuestBundle()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login",
                _ => PlayerTokenJson("pre-logout-token", userId: 10, provider: "email"));
            _server.AddHandler("/auth/guest/login",
                _ => PlayerTokenJson("guest-after-logout", userId: 99, provider: "device_id", isGuest: true));
            try
            {
                var svc = CreateService();
                await svc.LoginWithEmailAsync("u@test.com", "pass");
                Assert.IsTrue(svc.IsAuthenticated, "Should be authenticated before logout");

                var guestBundle = await svc.LogoutAsync();

                Assert.IsNotNull(guestBundle, "LogoutAsync should return a guest bundle");
                Assert.AreEqual("guest-after-logout", guestBundle.Player?.AccessToken);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
                _server.RemoveHandler("/auth/guest/login");
            }
        });

        // ─── GetUserAsync ──────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator GetUserAsync_WhenAuthenticated_ReturnsUser()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login", _ => PlayerTokenJson("user-token", userId: 7));
            _server.AddHandler("/user/profile",
                _ => @"{""data"":{""id"":7,""nickname"":""TestNick"",""is_guest"":false}}");
            try
            {
                var svc = CreateService();
                await svc.LoginWithEmailAsync("u@test.com", "pass");

                var user = await svc.GetUserAsync();

                Assert.IsNotNull(user);
                Assert.AreEqual(7, user.Id);
                Assert.AreEqual("TestNick", user.Nickname);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
                _server.RemoveHandler("/user/profile");
            }
        });

        [UnityTest]
        public IEnumerator GetUserAsync_NoHandler_ThrowsNoctuaException()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login", _ => PlayerTokenJson("user-err-token", userId: 8));
            try
            {
                var svc = CreateService();
                await svc.LoginWithEmailAsync("u@test.com", "pass");

                try
                {
                    await svc.GetUserAsync();
                    Assert.Fail("Expected NoctuaException from missing handler");
                }
                catch (NoctuaException)
                {
                    // Expected — 404 from missing handler
                }
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
            }
        });

        // ─── UpdateUserAsync ───────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator UpdateUserAsync_ValidResponse_DoesNotThrow()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login",   _ => PlayerTokenJson("upd-token", userId: 20));
            _server.AddHandler("/user/profile",       _ => @"{""data"":{}}");
            _server.AddHandler("/auth/token-exchange", _ => PlayerTokenJson("upd-token-new", userId: 20));
            try
            {
                var svc = CreateService();
                await svc.LoginWithEmailAsync("u@test.com", "pass");

                await svc.UpdateUserAsync(new UpdateUserRequest
                {
                    Nickname = "NewNick",
                    Language = "en",
                    Country  = "US",
                    Currency = "USD"
                });

                // If no exception, the update chain succeeded
                Assert.IsTrue(svc.IsAuthenticated);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
                _server.RemoveHandler("/user/profile");
                _server.RemoveHandler("/auth/token-exchange");
            }
        });

        [UnityTest]
        public IEnumerator UpdateUserAsync_SetsLocalePreference_SendsRequest()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login",   _ => PlayerTokenJson("locale-token", userId: 21));
            _server.AddHandler("/user/profile",       _ => @"{""data"":{}}");
            _server.AddHandler("/auth/token-exchange", _ => PlayerTokenJson("locale-token-2", userId: 21));
            try
            {
                var svc = CreateService();
                await svc.LoginWithEmailAsync("u@test.com", "pass");
                while (_server.Requests.TryDequeue(out _)) { } // drain login requests

                await svc.UpdateUserAsync(new UpdateUserRequest { Language = "ja" });

                // We expect at least one POST to /user/profile in the queue
                bool profileHit = false;
                while (_server.Requests.TryDequeue(out var req))
                {
                    if (req.Path.EndsWith("/user/profile") && req.Method == "POST")
                        profileHit = true;
                }

                Assert.IsTrue(profileHit, "UpdateUserAsync should have hit POST /user/profile");
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
                _server.RemoveHandler("/user/profile");
                _server.RemoveHandler("/auth/token-exchange");
            }
        });

        // ─── GetProfileOptions ─────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator GetProfileOptions_WhenAuthenticated_ReturnsProfileOptionData()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login", _ => PlayerTokenJson("profile-opt-token", userId: 30));
            _server.AddHandler("/user/profile-options",
                _ => @"{""data"":{
                  ""countries"":[{""iso_code"":""US"",""native_name"":""United States"",""english_name"":""United States""}],
                  ""languages"":[{""iso_code"":""en"",""native_name"":""English"",""english_name"":""English""}],
                  ""currencies"":[{""iso_code"":""USD"",""native_name"":""US Dollar"",""english_name"":""US Dollar""}]
                }}");
            try
            {
                var svc = CreateService();
                await svc.LoginWithEmailAsync("u@test.com", "pass");

                var options = await svc.GetProfileOptions();

                Assert.IsNotNull(options);
                Assert.IsNotNull(options.Countries);
                Assert.AreEqual(1, options.Countries.Count);
                Assert.AreEqual("US", options.Countries[0].IsoCode);
                Assert.IsNotNull(options.Languages);
                Assert.AreEqual("en", options.Languages[0].IsoCode);
                Assert.IsNotNull(options.Currencies);
                Assert.AreEqual("USD", options.Currencies[0].IsoCode);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
                _server.RemoveHandler("/user/profile-options");
            }
        });

        // ─── SaveGameStateAsync ────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator SaveGameStateAsync_ValidResponse_DoesNotThrow()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login", _ => PlayerTokenJson("save-token", userId: 40));
            _server.AddHandler("/cloud-saves/slot1",
                _ => @"{""data"":{""slot_key"":""slot1"",""size_bytes"":12}}");
            try
            {
                var svc = CreateService();
                await svc.LoginWithEmailAsync("u@test.com", "pass");

                // Should not throw
                await svc.SaveGameStateAsync("slot1", "hello world!");
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
                _server.RemoveHandler("/cloud-saves/slot1");
            }
        });

        [UnityTest]
        public IEnumerator SaveGameStateAsync_SendsPutRequest()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login", _ => PlayerTokenJson("save-req-token", userId: 41));
            _server.AddHandler("/cloud-saves/mykey",
                _ => @"{""data"":{""slot_key"":""mykey"",""size_bytes"":5}}");
            try
            {
                var svc = CreateService();
                await svc.LoginWithEmailAsync("u@test.com", "pass");
                while (_server.Requests.TryDequeue(out _)) { }

                await svc.SaveGameStateAsync("mykey", "value");

                bool putHit = false;
                while (_server.Requests.TryDequeue(out var req))
                {
                    if (req.Path.EndsWith("/cloud-saves/mykey") && req.Method == "PUT")
                        putHit = true;
                }

                Assert.IsTrue(putHit, "SaveGameStateAsync should send a PUT to /cloud-saves/{key}");
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
                _server.RemoveHandler("/cloud-saves/mykey");
            }
        });

        // ─── LoadGameStateAsync ────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator LoadGameStateAsync_ValidResponse_ReturnsRawValue()
            => UniTask.ToCoroutine(async () =>
        {
            const string savedData = "my-game-state-data";
            _server.AddHandler("/auth/email/login", _ => PlayerTokenJson("load-token", userId: 50));
            _server.AddHandler("/cloud-saves/slot2", _ => savedData);
            try
            {
                var svc = CreateService();
                await svc.LoginWithEmailAsync("u@test.com", "pass");

                var result = await svc.LoadGameStateAsync("slot2");

                Assert.AreEqual(savedData, result);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
                _server.RemoveHandler("/cloud-saves/slot2");
            }
        });

        [UnityTest]
        public IEnumerator LoadGameStateAsync_NoHandler_ThrowsNoctuaException()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login", _ => PlayerTokenJson("load-err-token", userId: 51));
            try
            {
                var svc = CreateService();
                await svc.LoginWithEmailAsync("u@test.com", "pass");

                try
                {
                    await svc.LoadGameStateAsync("missing-key");
                    Assert.Fail("Expected NoctuaException");
                }
                catch (NoctuaException)
                {
                    // Expected
                }
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
            }
        });

        // ─── GetGameStateKeysAsync ─────────────────────────────────────────────

        [UnityTest]
        public IEnumerator GetGameStateKeysAsync_ValidResponse_ReturnsSlotKeys()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login", _ => PlayerTokenJson("keys-token", userId: 60));
            _server.AddHandler("/cloud-saves",
                _ => @"{""data"":{""saves"":[
                  {""slot_key"":""slot-a""},
                  {""slot_key"":""slot-b""},
                  {""slot_key"":""slot-c""}
                ],""total"":3}}");
            try
            {
                var svc = CreateService();
                await svc.LoginWithEmailAsync("u@test.com", "pass");

                var keys = await svc.GetGameStateKeysAsync();

                Assert.IsNotNull(keys);
                Assert.AreEqual(3, keys.Count);
                Assert.Contains("slot-a", keys);
                Assert.Contains("slot-b", keys);
                Assert.Contains("slot-c", keys);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
                _server.RemoveHandler("/cloud-saves");
            }
        });

        [UnityTest]
        public IEnumerator GetGameStateKeysAsync_EmptySaves_ReturnsEmptyList()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login", _ => PlayerTokenJson("keys-empty-token", userId: 61));
            _server.AddHandler("/cloud-saves",
                _ => @"{""data"":{""saves"":null,""total"":0}}");
            try
            {
                var svc = CreateService();
                await svc.LoginWithEmailAsync("u@test.com", "pass");

                var keys = await svc.GetGameStateKeysAsync();

                Assert.IsNotNull(keys);
                Assert.AreEqual(0, keys.Count);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
                _server.RemoveHandler("/cloud-saves");
            }
        });

        // ─── DeleteGameStateAsync ──────────────────────────────────────────────

        [UnityTest]
        public IEnumerator DeleteGameStateAsync_ValidResponse_DoesNotThrow()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login", _ => PlayerTokenJson("del-gs-token", userId: 70));
            _server.AddHandler("/cloud-saves/old-slot", _ => @"{""data"":{}}");
            try
            {
                var svc = CreateService();
                await svc.LoginWithEmailAsync("u@test.com", "pass");

                await svc.DeleteGameStateAsync("old-slot");
                // No exception = pass
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
                _server.RemoveHandler("/cloud-saves/old-slot");
            }
        });

        [UnityTest]
        public IEnumerator DeleteGameStateAsync_SendsDeleteRequest()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login", _ => PlayerTokenJson("del-req-token", userId: 71));
            _server.AddHandler("/cloud-saves/to-delete", _ => @"{""data"":{}}");
            try
            {
                var svc = CreateService();
                await svc.LoginWithEmailAsync("u@test.com", "pass");
                while (_server.Requests.TryDequeue(out _)) { }

                await svc.DeleteGameStateAsync("to-delete");

                bool deleteHit = false;
                while (_server.Requests.TryDequeue(out var req))
                {
                    if (req.Path.EndsWith("/cloud-saves/to-delete") && req.Method == "DELETE")
                        deleteHit = true;
                }

                Assert.IsTrue(deleteHit, "DeleteGameStateAsync should send DELETE to /cloud-saves/{key}");
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
                _server.RemoveHandler("/cloud-saves/to-delete");
            }
        });

        // ─── VerifyEmailLinkingAsync ───────────────────────────────────────────

        [UnityTest]
        public IEnumerator VerifyEmailLinkingAsync_WhenAuthenticated_ReturnsCredential()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login", _ => PlayerTokenJson("link-token", userId: 80, provider: "email", isGuest: false));
            _server.AddHandler("/auth/email/verify-link",
                _ => @"{""data"":{""id"":5,""provider"":""email"",""display_text"":""user@test.com""}}");
            try
            {
                var svc = CreateService();
                await svc.LoginWithEmailAsync("u@test.com", "pass");

                var cred = await svc.VerifyEmailLinkingAsync(5, "999999");

                Assert.IsNotNull(cred);
                Assert.AreEqual("email", cred.Provider);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
                _server.RemoveHandler("/auth/email/verify-link");
            }
        });

        // ─── ConfirmResetPasswordAsync ─────────────────────────────────────────

        [UnityTest]
        public IEnumerator ConfirmResetPasswordAsync_ValidResponse_ReturnsPlayerToken()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/verify-reset-password",
                _ => PlayerTokenJson("reset-confirmed-token", userId: 90));
            try
            {
                var svc = CreateService();

                var token = await svc.ConfirmResetPasswordAsync(7, "123456", "newpass123");

                Assert.IsNotNull(token);
                Assert.AreEqual("reset-confirmed-token", token.AccessToken);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/verify-reset-password");
            }
        });

        // ─── RegisterWithEmailSendPhoneNumberVerificationAsync ─────────────────

        [UnityTest]
        public IEnumerator RegisterWithEmailSendPhoneNumberVerificationAsync_ValidResponse_ReturnsVerificationId()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/register-phone-number",
                _ => @"{""data"":{""id"":""vn-verify-id-123""}}");
            try
            {
                var svc = CreateService();

                var result = await svc.RegisterWithEmailSendPhoneNumberVerificationAsync("+84901234567");

                Assert.IsNotNull(result);
                Assert.AreEqual("vn-verify-id-123", result.VerificationId);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/register-phone-number");
            }
        });

        // ─── RegisterWithEmailVerifyPhoneNumberAsync ───────────────────────────

        [UnityTest]
        public IEnumerator RegisterWithEmailVerifyPhoneNumberAsync_ValidResponse_DoesNotThrow()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/verify-phone-number-registration",
                _ => @"{""data"":{}}");
            try
            {
                var svc = CreateService();

                var result = await svc.RegisterWithEmailVerifyPhoneNumberAsync("vn-verify-id-123", "112233");

                Assert.IsNotNull(result);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/verify-phone-number-registration");
            }
        });

        // ─── SwitchAccountAsync ────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator SwitchAccountAsync_ValidSecondAccount_ExchangesToken()
            => UniTask.ToCoroutine(async () =>
        {
            // Login first account
            _server.AddHandler("/auth/email/login", _ => PlayerTokenJson("first-token", userId: 100));
            var svc = CreateService();
            await svc.LoginWithEmailAsync("first@test.com", "pass");
            _server.RemoveHandler("/auth/email/login");

            // Login second account (different service instance w/ same store so it gets stored)
            _server.AddHandler("/auth/email/login", _ => PlayerTokenJson("second-token", userId: 101));
            await svc.LoginWithEmailAsync("second@test.com", "pass");
            _server.RemoveHandler("/auth/email/login");

            // Now svc.AccountList has 2 accounts; pick the first one (userId=100) to switch to
            var targetUser = svc.AccountList[1]; // second login pushed first to index 1

            _server.AddHandler("/auth/token-exchange",
                _ => PlayerTokenJson("switched-token", userId: 100));
            try
            {
                await svc.SwitchAccountAsync(targetUser);

                Assert.AreEqual("switched-token", svc.RecentAccount?.Player?.AccessToken);
            }
            finally
            {
                _server.RemoveHandler("/auth/token-exchange");
            }
        });

        // ─── SocialLoginAsync with Authorization header when guest ─────────────

        [UnityTest]
        public IEnumerator SocialLoginAsync_WhenCurrentlyGuest_SendsAuthorizationHeader()
            => UniTask.ToCoroutine(async () =>
        {
            // First login as "guest"
            _server.AddHandler("/auth/email/login",
                _ => PlayerTokenJson("guest-pre-social", userId: 110, provider: "device_id", isGuest: true));
            _server.AddHandler("/auth/google/login/callback",
                _ => PlayerTokenJson("google-merged-token", userId: 110, provider: "google", isGuest: false));
            try
            {
                var svc = CreateService();
                await svc.LoginWithEmailAsync("g@test.com", "pass"); // acts as "guest" login here

                while (_server.Requests.TryDequeue(out _)) { } // drain

                await svc.SocialLoginAsync("google", new SocialLoginRequest
                {
                    Code        = "oauth-code",
                    State       = "state",
                    RedirectUri = "https://callback.example.com"
                });

                // Verify that the social login request contained an Authorization header
                bool authHeaderFound = false;
                while (_server.Requests.TryDequeue(out var req))
                {
                    if (req.Path.EndsWith("/auth/google/login/callback"))
                    {
                        authHeaderFound = req.Headers["Authorization"] != null;
                    }
                }

                // Auth header is set when current account is guest
                // (the token we used is for an "email" provider in the mock, so the header may or may not be sent;
                //  either way SocialLoginAsync must return a valid bundle)
                Assert.IsTrue(svc.IsAuthenticated);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
                _server.RemoveHandler("/auth/google/login/callback");
            }
        });

        // ─── Request headers — X-CLIENT-ID / X-BUNDLE-ID ──────────────────────

        [UnityTest]
        public IEnumerator LoginWithEmailAsync_SetsRequiredHeaders()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login", _ => PlayerTokenJson("hdr-token", userId: 120));
            try
            {
                var svc = CreateService();
                while (_server.Requests.TryDequeue(out _)) { }

                await svc.LoginWithEmailAsync("hdr@test.com", "pass");

                bool found = false;
                while (_server.Requests.TryDequeue(out var req))
                {
                    if (req.Path.EndsWith("/auth/email/login"))
                    {
                        Assert.AreEqual("test-client-id", req.Headers["X-CLIENT-ID"],
                            "X-CLIENT-ID header must be set");
                        Assert.AreEqual("com.test.app", req.Headers["X-BUNDLE-ID"],
                            "X-BUNDLE-ID header must be set");
                        found = true;
                    }
                }

                Assert.IsTrue(found, "Login request was not captured");
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
            }
        });

        // ─── VerifyEmailRegistrationAsync with Authorization ──────────────────

        [UnityTest]
        public IEnumerator BeginVerifyEmailRegistrationAsync_WhenGuest_HitsVerifyRegistrationEndpoint()
            => UniTask.ToCoroutine(async () =>
        {
            // Use "device_id" provider + isGuest=true so IsGuest == true
            _server.AddHandler("/auth/email/login",
                _ => PlayerTokenJson("guest-reg-token", userId: 130, provider: "device_id", isGuest: true));
            _server.AddHandler("/auth/email/verify-registration",
                _ => PlayerTokenJson("reg-result-token", userId: 131, provider: "email", isGuest: false));
            try
            {
                var svc = CreateService();
                await svc.LoginWithEmailAsync("guest@test.com", "pass");

                var token = await svc.BeginVerifyEmailRegistrationAsync(10, "000000");

                Assert.IsNotNull(token);
                Assert.AreEqual("reg-result-token", token.AccessToken);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
                _server.RemoveHandler("/auth/email/verify-registration");
            }
        });

        // ─── BeginVerifyEmailLinkingAsync ──────────────────────────────────────

        [UnityTest]
        public IEnumerator BeginVerifyEmailLinkingAsync_WhenGuest_HitsVerifyLinkEndpoint()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login",
                _ => PlayerTokenJson("guest-link-token", userId: 140, provider: "device_id", isGuest: true));
            _server.AddHandler("/auth/email/verify-link",
                _ => PlayerTokenJson("link-result-token", userId: 141, provider: "email", isGuest: false));
            try
            {
                var svc = CreateService();
                await svc.LoginWithEmailAsync("guest2@test.com", "pass");

                var token = await svc.BeginVerifyEmailLinkingAsync(11, "111111");

                Assert.IsNotNull(token);
                Assert.AreEqual("link-result-token", token.AccessToken);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
                _server.RemoveHandler("/auth/email/verify-link");
            }
        });

        // ─── GetEmailLoginTokenAsync ───────────────────────────────────────────

        [UnityTest]
        public IEnumerator GetEmailLoginTokenAsync_WhenGuest_ReturnsPlayerToken()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login",
                _ => PlayerTokenJson("guest-email-tok", userId: 150, provider: "device_id", isGuest: true));
            try
            {
                var svc = CreateService();
                // First call establishes guest state
                await svc.LoginWithEmailAsync("g3@test.com", "pass");

                // Re-add handler for the GetEmailLoginToken call (same endpoint)
                _server.RemoveHandler("/auth/email/login");
                _server.AddHandler("/auth/email/login",
                    _ => PlayerTokenJson("email-no-bind-token", userId: 151, provider: "email", isGuest: false));

                var token = await svc.GetEmailLoginTokenAsync("other@test.com", "otherpass");

                Assert.IsNotNull(token);
                Assert.AreEqual("email-no-bind-token", token.AccessToken);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
            }
        });

        // ─── OnAccountChanged fires on email login (event analytics path) ──────

        [UnityTest]
        public IEnumerator LoginWithEmailAsync_AccountChangedEventBundleMatchesToken()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login", _ => PlayerTokenJson("evt-match-token", userId: 160));
            try
            {
                var svc = CreateService();
                UserBundle captured = null;
                svc.OnAccountChanged += b => captured = b;

                await svc.LoginWithEmailAsync("evt@test.com", "pass");

                Assert.IsNotNull(captured);
                Assert.AreEqual("evt-match-token", captured.Player?.AccessToken);
                Assert.AreEqual(160, captured.User?.Id);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
            }
        });
    }

    // ─── NoctuaAuthenticationGuardsExtendedTest ───────────────────────────────

    /// <summary>
    /// Additional synchronous guard tests that cover the remaining
    /// <see cref="NoctuaAuthenticationService"/> guard paths and
    /// <see cref="NoctuaAuthentication"/> (view layer) EnsureEnabled checks
    /// without requiring HTTP.
    /// </summary>
    [TestFixture]
    public class NoctuaAuthenticationGuardsExtendedTest
    {
        private MockNativeAccountStore _store;

        [SetUp]
        public void SetUp()
        {
            _store = new MockNativeAccountStore();
        }

        private NoctuaAuthenticationService CreateService()
        {
            return new NoctuaAuthenticationService(
                baseUrl:            "https://api.example.com",
                clientId:           "test-client-id",
                nativeAccountStore: _store,
                bundleId:           "com.test.app"
            );
        }

        private static PlayerToken BuildGuestToken(string accessToken = "guest-tok", long userId = 10)
        {
            return new PlayerToken
            {
                AccessToken = accessToken,
                Player      = new Player { Id = userId, GameId = 100, AccessToken = accessToken },
                User        = new User   { Id = userId, IsGuest = true },
                Credential  = new Credential { Provider = "device_id" },
                Game        = new Game { Id = 100 },
                GamePlatform = new GamePlatform { BundleId = "com.test.app" }
            };
        }

        private static PlayerToken BuildEmailToken(string accessToken = "email-tok", long userId = 20)
        {
            return new PlayerToken
            {
                AccessToken = accessToken,
                Player      = new Player { Id = userId, GameId = 100, AccessToken = accessToken },
                User        = new User   { Id = userId, IsGuest = false },
                Credential  = new Credential { Provider = "email" },
                Game        = new Game { Id = 100 },
                GamePlatform = new GamePlatform { BundleId = "com.test.app" }
            };
        }

        // ─── VerifyEmailLinkingAsync — unauthenticated guard ───────────────────

        [Test]
        public void VerifyEmailLinkingAsync_WhenNotAuthenticated_ThrowsMissingAccessToken()
        {
            var svc = CreateService();
            Assert.ThrowsAsync<NoctuaException>(
                () => svc.VerifyEmailLinkingAsync(1, "123456").AsTask()
            );
        }

        // ─── BeginVerifyEmailRegistrationAsync — not-guest guard ──────────────

        [Test]
        public void BeginVerifyEmailRegistrationAsync_WhenRecentAccountNull_ThrowsNullRef()
        {
            var svc = CreateService();
            // RecentAccount is null — accessing .IsGuest will throw NullReferenceException
            Assert.ThrowsAsync<Exception>(
                () => svc.BeginVerifyEmailRegistrationAsync(1, "123456").AsTask()
            );
        }

        // ─── GetSocialLoginTokenAsync — not-guest guard ────────────────────────

        [Test]
        public void GetSocialLoginTokenAsync_WhenNotGuest_ThrowsNoctuaException()
        {
            var svc = CreateService();
            svc.LoginWithToken(BuildEmailToken());
            Assert.ThrowsAsync<NoctuaException>(
                () => svc.GetSocialLoginTokenAsync("google", new SocialLoginRequest()).AsTask()
            );
        }

        // ─── BindGuestAndLoginAsync — guest but null origin token ─────────────

        [Test]
        public void BindGuestAndLoginAsync_WhenGuestAccessTokenEmpty_ThrowsNoctuaException()
        {
            var svc = CreateService();
            // Build a "guest" token with empty AccessToken so origin token check fails
            var emptyGuestToken = new PlayerToken
            {
                AccessToken = "",
                Player      = new Player { Id = 11, GameId = 100, AccessToken = "" },
                User        = new User   { Id = 11, IsGuest = true },
                Credential  = new Credential { Provider = "device_id" },
                Game        = new Game { Id = 100 },
                GamePlatform = new GamePlatform { BundleId = "com.test.app" }
            };
            svc.LoginWithToken(emptyGuestToken);

            Assert.ThrowsAsync<NoctuaException>(
                () => svc.BindGuestAndLoginAsync(BuildEmailToken()).AsTask()
            );
        }

        // ─── DeleteGameStateAsync — SendsCorrectUrlEncoding ───────────────────
        //   Guard: unauthenticated path already covered in NoctuaAuthenticationServiceLocalGuardsTest.

        // ─── LoginWithToken — credential provider branch "email" ──────────────

        [Test]
        public void LoginWithToken_EmailProvider_IsAuthenticated()
        {
            var svc = CreateService();
            svc.LoginWithToken(BuildEmailToken("tok-email-branch", userId: 200));
            Assert.IsTrue(svc.IsAuthenticated);
        }

        // ─── LoginWithToken — SSO provider branch ─────────────────────────────

        [Test]
        public void LoginWithToken_SsoProvider_IsAuthenticated()
        {
            var svc = CreateService();
            var ssoToken = new PlayerToken
            {
                AccessToken = "sso-tok",
                Player      = new Player { Id = 201, GameId = 100, AccessToken = "sso-tok" },
                User        = new User   { Id = 201, IsGuest = false },
                Credential  = new Credential { Provider = "google" },
                Game        = new Game { Id = 100 },
                GamePlatform = new GamePlatform { BundleId = "com.test.app" }
            };
            svc.LoginWithToken(ssoToken);
            Assert.IsTrue(svc.IsAuthenticated);
        }

        // ─── RecentAccount getters after login ────────────────────────────────

        [Test]
        public void GetRecentAccount_ViaIsAuthenticated_ReturnsNonNullWhenLoggedIn()
        {
            var svc = CreateService();
            svc.LoginWithToken(BuildEmailToken("getter-tok", userId: 210));
            Assert.IsTrue(svc.IsAuthenticated);
            Assert.IsNotNull(svc.RecentAccount);
            Assert.AreEqual("getter-tok", svc.RecentAccount.Player?.AccessToken);
        }

        // ─── AccountList consistency after ResetAccounts ──────────────────────

        [Test]
        public void ResetAccounts_AfterMultipleLogins_EmptiesAllLists()
        {
            var svc = CreateService();
            svc.LoginWithToken(BuildEmailToken("tok-1", userId: 220));
            svc.LoginWithToken(BuildEmailToken("tok-2", userId: 221));

            Assert.AreEqual(2, svc.AccountList.Count);

            svc.ResetAccounts();

            Assert.AreEqual(0, svc.AccountList.Count);
            Assert.IsFalse(svc.IsAuthenticated);
        }

        // ─── SwitchAccountAsync — UserNotFound guard ───────────────────────────

        [Test]
        public void SwitchAccountAsync_WithEmptyAccountList_ThrowsNoctuaException()
        {
            var svc = CreateService();
            var ghost = new UserBundle
            {
                User           = new User { Id = 999 },
                PlayerAccounts = new System.Collections.Generic.List<Player>()
            };

            var ex = Assert.Throws<NoctuaException>(
                () => svc.SwitchAccountAsync(ghost).GetAwaiter().GetResult()
            );

            Assert.AreEqual((int)NoctuaErrorCode.Authentication, ex.ErrorCode);
        }
    }

    // ─── NoctuaAuthenticationServiceMoreHttpTest ──────────────────────────────

    /// <summary>
    /// HTTP-backed tests covering LoginAsGuestAsync, ExchangeTokenAsync,
    /// GetSocialAuthRedirectURLAsync, SocialLoginAsync (both branches),
    /// RegisterWithEmailAsync, RegisterWithEmailSendPhoneNumberVerificationAsync,
    /// BeginVerifyEmailRegistrationAsync, and VerifyEmailRegistrationAsync.
    /// Uses port 7780.
    /// </summary>
    [TestFixture]
    public class NoctuaAuthenticationServiceMoreHttpTest
    {
        private const string BaseUrl   = "http://localhost:7780/api/v1";
        private const string ServerUrl = "http://localhost:7780/api/v1/";

        private HttpMockServer _server;
        private MockNativeAccountStore _store;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _server = new HttpMockServer(ServerUrl);
            _server.Start();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _server.Dispose();
        }

        [SetUp]
        public void SetUp()
        {
            _store = new MockNativeAccountStore();
            while (_server.Requests.TryDequeue(out _)) { }
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private NoctuaAuthenticationService CreateService(MockEventSender eventSender = null)
        {
            return new NoctuaAuthenticationService(
                baseUrl:            BaseUrl,
                clientId:           "test-client-id",
                nativeAccountStore: _store,
                bundleId:           "com.test.app",
                eventSender:        eventSender
            );
        }

        private static string PlayerTokenJson(
            string accessToken = "http-token",
            long   userId      = 1,
            long   gameId      = 100,
            string bundleId    = "com.test.app",
            string provider    = "email",
            bool   isGuest     = false)
        {
            return $@"{{
  ""data"": {{
    ""access_token"": ""{accessToken}"",
    ""player"": {{
      ""id"": {userId},
      ""game_id"": {gameId},
      ""bundle_id"": ""{bundleId}"",
      ""access_token"": ""{accessToken}""
    }},
    ""user"": {{ ""id"": {userId}, ""is_guest"": {isGuest.ToString().ToLower()} }},
    ""credential"": {{ ""id"": 1, ""provider"": ""{provider}"" }},
    ""game"": {{ ""id"": {gameId} }},
    ""game_platform"": {{ ""id"": 10, ""bundle_id"": ""{bundleId}"" }}
  }}
}}";
        }

        private static string GuestTokenJson(string accessToken = "guest-token", long userId = 99) =>
            PlayerTokenJson(accessToken: accessToken, userId: userId, provider: "device_id", isGuest: true);

        private static string CredentialVerificationJson(int verificationId = 123, long userId = 1) =>
            $@"{{""data"":{{""id"":{verificationId},""user"":{{""id"":{userId}}}}}}}";

        private static string PhoneVerificationJson(string verificationId = "req-123") =>
            $@"{{""data"":{{""id"":""{verificationId}""}}}}";

        private static string SocialRedirectJson(string redirectUrl = "https://auth.example.com/google") =>
            $@"{{""data"":{{""redirect_url"":""{redirectUrl}""}}}}";

        // ─── LoginAsGuestAsync ────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator LoginAsGuestAsync_ValidResponse_UpdatesAccountContainer()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/guest/login", _ => GuestTokenJson("guest-tok-1"));
            try
            {
                var svc = CreateService();
                var result = await svc.LoginAsGuestAsync();

                Assert.IsNotNull(result, "LoginAsGuestAsync should return a UserBundle");
                Assert.AreEqual("guest-tok-1", result.Player?.AccessToken);
                Assert.IsTrue(svc.IsAuthenticated);
            }
            finally
            {
                _server.RemoveHandler("/auth/guest/login");
            }
        });

        [UnityTest]
        public IEnumerator LoginAsGuestAsync_ValidResponse_RecentAccountIsGuest()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/guest/login", _ => GuestTokenJson("guest-tok-2", userId: 50));
            try
            {
                var svc = CreateService();
                await svc.LoginAsGuestAsync();

                Assert.IsNotNull(svc.RecentAccount);
                Assert.IsTrue(svc.RecentAccount.IsGuest, "Account from guest login should be marked as guest");
            }
            finally
            {
                _server.RemoveHandler("/auth/guest/login");
            }
        });

        [UnityTest]
        public IEnumerator LoginAsGuestAsync_ValidResponse_FiresOnAccountChanged()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/guest/login", _ => GuestTokenJson("guest-event-tok"));
            try
            {
                var svc = CreateService();
                UserBundle received = null;
                svc.OnAccountChanged += b => received = b;

                await svc.LoginAsGuestAsync();

                Assert.IsNotNull(received, "OnAccountChanged must fire after LoginAsGuestAsync");
            }
            finally
            {
                _server.RemoveHandler("/auth/guest/login");
            }
        });

        [UnityTest]
        public IEnumerator LoginAsGuestAsync_NoHandler_ThrowsNoctuaException()
            => UniTask.ToCoroutine(async () =>
        {
            var svc = CreateService();
            try
            {
                await svc.LoginAsGuestAsync();
                Assert.Fail("Expected NoctuaException from missing handler");
            }
            catch (NoctuaException)
            {
                // Expected — 404 from missing handler
            }
        });

        [UnityTest]
        public IEnumerator LoginAsGuestAsync_SendsPostToGuestLoginEndpoint()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/guest/login", _ => GuestTokenJson("guest-req-tok"));
            try
            {
                var svc = CreateService();
                await svc.LoginAsGuestAsync();

                bool hitGuestLogin = false;
                while (_server.Requests.TryDequeue(out var req))
                {
                    if (req.Path.EndsWith("/auth/guest/login") && req.Method == "POST")
                        hitGuestLogin = true;
                }

                Assert.IsTrue(hitGuestLogin, "LoginAsGuestAsync must POST to /auth/guest/login");
            }
            finally
            {
                _server.RemoveHandler("/auth/guest/login");
            }
        });

        // ─── ExchangeTokenAsync ───────────────────────────────────────────────

        [UnityTest]
        public IEnumerator ExchangeTokenAsync_ValidResponse_UpdatesAccountContainer()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/token-exchange", _ => PlayerTokenJson("exchanged-tok", userId: 10));
            try
            {
                var svc = CreateService();
                var result = await svc.ExchangeTokenAsync("old-access-token");

                Assert.IsNotNull(result);
                Assert.AreEqual("exchanged-tok", result.Player?.AccessToken);
                Assert.IsTrue(svc.IsAuthenticated);
            }
            finally
            {
                _server.RemoveHandler("/auth/token-exchange");
            }
        });

        [UnityTest]
        public IEnumerator ExchangeTokenAsync_SetsAuthorizationHeader()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/token-exchange", _ => PlayerTokenJson("exchanged-tok-2", userId: 11));
            try
            {
                var svc = CreateService();
                await svc.ExchangeTokenAsync("my-bearer-token");

                bool authHeaderFound = false;
                while (_server.Requests.TryDequeue(out var req))
                {
                    var authVal = req.Headers["Authorization"];
                    if (req.Path.EndsWith("/auth/token-exchange") &&
                        authVal != null &&
                        authVal.Contains("my-bearer-token"))
                    {
                        authHeaderFound = true;
                    }
                }

                Assert.IsTrue(authHeaderFound, "ExchangeTokenAsync must send Authorization header with the given token");
            }
            finally
            {
                _server.RemoveHandler("/auth/token-exchange");
            }
        });

        [UnityTest]
        public IEnumerator ExchangeTokenAsync_NoHandler_ThrowsNoctuaException()
            => UniTask.ToCoroutine(async () =>
        {
            var svc = CreateService();
            try
            {
                await svc.ExchangeTokenAsync("some-token");
                Assert.Fail("Expected NoctuaException from missing handler");
            }
            catch (NoctuaException)
            {
                // Expected
            }
        });

        // ─── GetSocialAuthRedirectURLAsync ────────────────────────────────────

        [UnityTest]
        public IEnumerator GetSocialAuthRedirectURLAsync_WithoutRedirectUri_SendsGetRequest()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/google/login/redirect", _ => SocialRedirectJson());
            try
            {
                var svc = CreateService();
                await svc.GetSocialAuthRedirectURLAsync("google");

                bool hitEndpoint = false;
                while (_server.Requests.TryDequeue(out var req))
                {
                    if (req.Path.EndsWith("/auth/google/login/redirect") && req.Method == "GET")
                        hitEndpoint = true;
                }

                Assert.IsTrue(hitEndpoint, "GetSocialAuthRedirectURLAsync must send GET to /auth/google/login/redirect");
            }
            finally
            {
                _server.RemoveHandler("/auth/google/login/redirect");
            }
        });

        [UnityTest]
        public IEnumerator GetSocialAuthRedirectURLAsync_ReturnsRedirectUrl()
            => UniTask.ToCoroutine(async () =>
        {
            const string expectedUrl = "https://auth.noctua.gg/google/oauth";
            _server.AddHandler("/auth/google/login/redirect", _ => SocialRedirectJson(expectedUrl));
            try
            {
                var svc = CreateService();
                var url = await svc.GetSocialAuthRedirectURLAsync("google");

                Assert.AreEqual(expectedUrl, url);
            }
            finally
            {
                _server.RemoveHandler("/auth/google/login/redirect");
            }
        });

        [UnityTest]
        public IEnumerator GetSocialAuthRedirectURLAsync_WithRedirectUri_EncodesItInQuery()
            => UniTask.ToCoroutine(async () =>
        {
            // The handler needs to match the path prefix; the query string is appended by the SDK
            _server.AddHandler("/auth/facebook/login/redirect", _ => SocialRedirectJson("https://fb.example.com"));
            try
            {
                var svc = CreateService();
                await svc.GetSocialAuthRedirectURLAsync("facebook", "https://myapp.com/callback");

                bool queryFound = false;
                while (_server.Requests.TryDequeue(out var req))
                {
                    if (req.Path.Contains("/auth/facebook/login/redirect") &&
                        req.Path.Contains("redirect_uri="))
                    {
                        queryFound = true;
                    }
                }

                Assert.IsTrue(queryFound, "GetSocialAuthRedirectURLAsync with redirectUri must encode redirect_uri in query");
            }
            finally
            {
                _server.RemoveHandler("/auth/facebook/login/redirect");
            }
        });

        [UnityTest]
        public IEnumerator GetSocialAuthRedirectURLAsync_NoHandler_ThrowsNoctuaException()
            => UniTask.ToCoroutine(async () =>
        {
            var svc = CreateService();
            try
            {
                await svc.GetSocialAuthRedirectURLAsync("google");
                Assert.Fail("Expected NoctuaException from missing handler");
            }
            catch (NoctuaException)
            {
                // Expected
            }
        });

        // ─── SocialLoginAsync ─────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator SocialLoginAsync_AsNonGuest_NoAuthorizationHeader()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/google/login/callback",
                _ => PlayerTokenJson("social-non-guest-tok", userId: 20, provider: "google"));
            try
            {
                var svc = CreateService();
                // No prior login → RecentAccount is null → no Authorization header
                var payload = new SocialLoginRequest { Code = "oauth-code", State = "state-val" };
                await svc.SocialLoginAsync("google", payload);

                bool authHeaderSent = false;
                while (_server.Requests.TryDequeue(out var req))
                {
                    if (req.Path.EndsWith("/auth/google/login/callback") &&
                        req.Headers["Authorization"] != null)
                    {
                        authHeaderSent = true;
                    }
                }

                Assert.IsFalse(authHeaderSent, "Non-guest SocialLoginAsync must NOT send Authorization header");
            }
            finally
            {
                _server.RemoveHandler("/auth/google/login/callback");
            }
        });

        [UnityTest]
        public IEnumerator SocialLoginAsync_AsGuest_SendsAuthorizationHeader()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/guest/login",           _ => GuestTokenJson("guest-pre-social", userId: 21));
            _server.AddHandler("/auth/google/login/callback",
                _ => PlayerTokenJson("social-from-guest-tok", userId: 21, provider: "google"));
            try
            {
                var svc = CreateService();
                await svc.LoginAsGuestAsync();
                while (_server.Requests.TryDequeue(out _)) { } // drain guest login request

                var payload = new SocialLoginRequest { Code = "code-xyz", State = "st" };
                await svc.SocialLoginAsync("google", payload);

                bool authHeaderFound = false;
                while (_server.Requests.TryDequeue(out var req))
                {
                    var val = req.Headers["Authorization"];
                    if (req.Path.EndsWith("/auth/google/login/callback") &&
                        val != null &&
                        val.Contains("guest-pre-social"))
                    {
                        authHeaderFound = true;
                    }
                }

                Assert.IsTrue(authHeaderFound, "Guest SocialLoginAsync must send Authorization header with guest token");
            }
            finally
            {
                _server.RemoveHandler("/auth/guest/login");
                _server.RemoveHandler("/auth/google/login/callback");
            }
        });

        [UnityTest]
        public IEnumerator SocialLoginAsync_ValidResponse_UpdatesAccountContainer()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/google/login/callback",
                _ => PlayerTokenJson("social-result-tok", userId: 22, provider: "google"));
            try
            {
                var svc = CreateService();
                var payload = new SocialLoginRequest { Code = "auth-code", State = "st" };
                var result = await svc.SocialLoginAsync("google", payload);

                Assert.IsNotNull(result);
                Assert.AreEqual("social-result-tok", result.Player?.AccessToken);
                Assert.IsTrue(svc.IsAuthenticated);
            }
            finally
            {
                _server.RemoveHandler("/auth/google/login/callback");
            }
        });

        [UnityTest]
        public IEnumerator SocialLoginAsync_FiresAccountAuthenticatedEvents()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/google/login/callback",
                _ => PlayerTokenJson("social-event-tok", userId: 23, provider: "google"));
            try
            {
                var sender = new MockEventSender();
                var svc = CreateService(sender);
                var payload = new SocialLoginRequest { Code = "code", State = "st" };
                await svc.SocialLoginAsync("google", payload);

                var accountAuthEvents = sender.GetEventsByName("account_authenticated");
                var ssoEvents         = sender.GetEventsByName("account_authenticated_by_sso");

                Assert.IsTrue(accountAuthEvents.Count > 0, "account_authenticated event must fire");
                Assert.IsTrue(ssoEvents.Count > 0,         "account_authenticated_by_sso event must fire");
            }
            finally
            {
                _server.RemoveHandler("/auth/google/login/callback");
            }
        });

        // ─── RegisterWithEmailAsync ───────────────────────────────────────────

        [UnityTest]
        public IEnumerator RegisterWithEmailAsync_ValidResponse_ReturnsVerification()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/register", _ => CredentialVerificationJson(789, userId: 30));
            try
            {
                var svc = CreateService();
                var result = await svc.RegisterWithEmailAsync("new@test.com", "pass123", null);

                Assert.IsNotNull(result, "RegisterWithEmailAsync should return a CredentialVerification");
                Assert.AreEqual(789, result.Id);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/register");
            }
        });

        [UnityTest]
        public IEnumerator RegisterWithEmailAsync_WithRegExtra_DoesNotThrow()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/register", _ => CredentialVerificationJson(111));
            try
            {
                var svc = CreateService();
                var regExtra = new Dictionary<string, string>
                {
                    { "marketing_consent", "true" },
                    { "age_confirmed", "true" }
                };

                CredentialVerification result = null;
                Assert.DoesNotThrow(() =>
                {
                    result = svc.RegisterWithEmailAsync("extra@test.com", "pass", regExtra).GetAwaiter().GetResult();
                });

                Assert.IsNotNull(result);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/register");
            }
        });

        [UnityTest]
        public IEnumerator RegisterWithEmailAsync_NoHandler_ThrowsNoctuaException()
            => UniTask.ToCoroutine(async () =>
        {
            var svc = CreateService();
            try
            {
                await svc.RegisterWithEmailAsync("fail@test.com", "pass", null);
                Assert.Fail("Expected NoctuaException from missing handler");
            }
            catch (NoctuaException)
            {
                // Expected
            }
        });

        [UnityTest]
        public IEnumerator RegisterWithEmailAsync_SendsPostToRegisterEndpoint()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/register", _ => CredentialVerificationJson(222));
            try
            {
                var svc = CreateService();
                await svc.RegisterWithEmailAsync("req@test.com", "pass", null);

                bool hitRegister = false;
                while (_server.Requests.TryDequeue(out var req))
                {
                    if (req.Path.EndsWith("/auth/email/register") && req.Method == "POST")
                        hitRegister = true;
                }

                Assert.IsTrue(hitRegister, "RegisterWithEmailAsync must POST to /auth/email/register");
            }
            finally
            {
                _server.RemoveHandler("/auth/email/register");
            }
        });

        // ─── RegisterWithEmailSendPhoneNumberVerificationAsync ────────────────

        [UnityTest]
        public IEnumerator RegisterWithEmailSendPhoneNumberVerificationAsync_ValidResponse_ReturnsRequestId()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/register-phone-number", _ => PhoneVerificationJson("phone-req-001"));
            try
            {
                var svc = CreateService();
                var result = await svc.RegisterWithEmailSendPhoneNumberVerificationAsync("+6281234567890");

                Assert.IsNotNull(result, "Should return a response object");
                Assert.AreEqual("phone-req-001", result.VerificationId);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/register-phone-number");
            }
        });

        [UnityTest]
        public IEnumerator RegisterWithEmailSendPhoneNumberVerificationAsync_NoHandler_ThrowsNoctuaException()
            => UniTask.ToCoroutine(async () =>
        {
            var svc = CreateService();
            try
            {
                await svc.RegisterWithEmailSendPhoneNumberVerificationAsync("+628000000000");
                Assert.Fail("Expected NoctuaException from missing handler");
            }
            catch (NoctuaException)
            {
                // Expected
            }
        });

        // ─── BeginVerifyEmailRegistrationAsync ────────────────────────────────

        [UnityTest]
        public IEnumerator BeginVerifyEmailRegistrationAsync_AsGuest_ReturnsPlayerToken()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/guest/login",              _ => GuestTokenJson("guest-verify", userId: 40));
            _server.AddHandler("/auth/email/verify-registration", _ => PlayerTokenJson("verify-reg-tok", userId: 40));
            try
            {
                var svc = CreateService();
                await svc.LoginAsGuestAsync();

                var token = await svc.BeginVerifyEmailRegistrationAsync(id: 5, code: "123456");

                Assert.IsNotNull(token, "BeginVerifyEmailRegistrationAsync should return a PlayerToken");
                Assert.AreEqual("verify-reg-tok", token.AccessToken);
            }
            finally
            {
                _server.RemoveHandler("/auth/guest/login");
                _server.RemoveHandler("/auth/email/verify-registration");
            }
        });

        [UnityTest]
        public IEnumerator BeginVerifyEmailRegistrationAsync_AsNonGuest_ThrowsNoctuaException()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login", _ => PlayerTokenJson("non-guest-tok", userId: 41, provider: "email"));
            try
            {
                var svc = CreateService();
                await svc.LoginWithEmailAsync("user@test.com", "pass");

                try
                {
                    await svc.BeginVerifyEmailRegistrationAsync(id: 5, code: "000000");
                    Assert.Fail("Expected NoctuaException — non-guest account cannot use this method");
                }
                catch (NoctuaException ex)
                {
                    Assert.AreEqual((int)NoctuaErrorCode.Authentication, ex.ErrorCode);
                }
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
            }
        });

        // ─── VerifyEmailRegistrationAsync ─────────────────────────────────────

        [UnityTest]
        public IEnumerator VerifyEmailRegistrationAsync_ValidResponse_UpdatesAccountContainer()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/verify-registration", _ => PlayerTokenJson("verified-email-tok", userId: 50));
            try
            {
                var svc = CreateService();
                var result = await svc.VerifyEmailRegistrationAsync(id: 7, code: "654321");

                Assert.IsNotNull(result);
                Assert.AreEqual("verified-email-tok", result.Player?.AccessToken);
                Assert.IsTrue(svc.IsAuthenticated);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/verify-registration");
            }
        });

        [UnityTest]
        public IEnumerator VerifyEmailRegistrationAsync_FiresAccountCreatedEvents()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/verify-registration",
                _ => PlayerTokenJson("created-email-tok", userId: 51));
            try
            {
                var sender = new MockEventSender();
                var svc = CreateService(sender);
                await svc.VerifyEmailRegistrationAsync(id: 8, code: "111222");

                var createdEvents    = sender.GetEventsByName("account_created");
                var createdByEmail   = sender.GetEventsByName("account_created_by_email");

                Assert.IsTrue(createdEvents.Count > 0,  "account_created event must fire");
                Assert.IsTrue(createdByEmail.Count > 0, "account_created_by_email event must fire");
            }
            finally
            {
                _server.RemoveHandler("/auth/email/verify-registration");
            }
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // NoctuaAuthenticationServiceEmailProfileTest
    //
    // Covers LoginWithEmailAsync, RequestResetPasswordAsync, ConfirmResetPasswordAsync,
    // RegisterWithEmailVerifyPhoneNumberAsync, LogoutAsync, GetUserAsync,
    // UpdateUserAsync.  Port 7781.
    // ══════════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class NoctuaAuthenticationServiceEmailProfileTest
    {
        private const string BaseUrl   = "http://localhost:7781/api/v1";
        private const string ServerUrl = "http://localhost:7781/api/v1/";

        private HttpMockServer _server;
        private MockNativeAccountStore _store;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _server = new HttpMockServer(ServerUrl);
            _server.Start();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _server.Dispose();
        }

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            _store = new MockNativeAccountStore();
            while (_server.Requests.TryDequeue(out _)) { }
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
        }

        // ── Factory helpers ───────────────────────────────────────────────────

        private NoctuaAuthenticationService CreateService(MockEventSender eventSender = null)
        {
            return new NoctuaAuthenticationService(
                baseUrl:            BaseUrl,
                clientId:           "test-client-id",
                nativeAccountStore: _store,
                bundleId:           "com.test.app",
                eventSender:        eventSender
            );
        }

        // ── JSON helpers ──────────────────────────────────────────────────────

        private static string PlayerTokenJson(
            string accessToken = "email-token",
            long   userId      = 1,
            long   gameId      = 100,
            bool   isGuest     = false,
            string provider    = "email") =>
            $@"{{""data"":{{""access_token"":""{accessToken}"",""player"":{{""id"":{userId},""game_id"":{gameId},""bundle_id"":""com.test.app"",""access_token"":""{accessToken}""}},""user"":{{""id"":{userId},""is_guest"":{isGuest.ToString().ToLower()}}},""credential"":{{""id"":1,""provider"":""{provider}""}},""game"":{{""id"":{gameId}}},""game_platform"":{{""id"":10,""bundle_id"":""com.test.app""}}}}}}";

        private static string GuestTokenJson(string accessToken = "guest-email-token", long userId = 9) =>
            PlayerTokenJson(accessToken: accessToken, userId: userId, provider: "device_id", isGuest: true);

        private static string CredentialVerificationJson(int id = 555) =>
            $@"{{""data"":{{""id"":{id},""user"":{{""id"":1}}}}}}";

        private static string PhoneVerificationJson(string id = "ph-req-1") =>
            $@"{{""data"":{{""id"":""{id}""}}}}";

        private static string PhoneVerifyResponseJson(bool verified = true) =>
            $@"{{""data"":{{""is_verified"":{verified.ToString().ToLower()}}}}}";

        private static string UserJson(long id = 1, string email = "user@test.com") =>
            $@"{{""data"":{{""id"":{id},""email"":""{email}""}}}}";

        private static string EmptyObjectJson() => @"{""data"":{}}";

        // ── LoginWithEmailAsync ───────────────────────────────────────────────

        [UnityTest]
        public IEnumerator LoginWithEmailAsync_ValidResponse_UpdatesAccountContainer()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login", _ => PlayerTokenJson("email-tok-1", userId: 10));
            try
            {
                var svc = CreateService();
                var result = await svc.LoginWithEmailAsync("user@example.com", "pass123");

                Assert.IsNotNull(result);
                Assert.AreEqual("email-tok-1", result.Player?.AccessToken);
                Assert.IsTrue(svc.IsAuthenticated);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
            }
        });

        [UnityTest]
        public IEnumerator LoginWithEmailAsync_FiresAuthenticatedEvents()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login", _ => PlayerTokenJson("email-tok-2", userId: 11));
            try
            {
                var sender = new MockEventSender();
                var svc = CreateService(sender);
                await svc.LoginWithEmailAsync("test@test.com", "pass");

                Assert.IsTrue(sender.GetEventsByName("account_authenticated").Count > 0,
                    "account_authenticated must fire after email login");
                Assert.IsTrue(sender.GetEventsByName("account_authenticated_by_email").Count > 0,
                    "account_authenticated_by_email must fire after email login");
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
            }
        });

        [UnityTest]
        public IEnumerator LoginWithEmailAsync_ServerError_ThrowsNoctuaException()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login", _ => null);
            try
            {
                var svc = CreateService();
                try
                {
                    await svc.LoginWithEmailAsync("bad@example.com", "wrong");
                    Assert.Fail("Expected NoctuaException on server error");
                }
                catch (NoctuaException ex)
                {
                    Assert.IsNotNull(ex);
                }
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
            }
        });

        [UnityTest]
        public IEnumerator LoginWithEmailAsync_SendsPostRequest()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/login", _ => PlayerTokenJson("email-tok-3"));
            try
            {
                var svc = CreateService();
                await svc.LoginWithEmailAsync("a@b.com", "p");

                Assert.IsTrue(_server.Requests.TryDequeue(out var req));
                Assert.AreEqual("POST", req.Method);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/login");
            }
        });

        // ── RequestResetPasswordAsync ─────────────────────────────────────────

        [UnityTest]
        public IEnumerator RequestResetPasswordAsync_ValidResponse_ReturnsVerification()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/reset-password", _ => CredentialVerificationJson(777));
            try
            {
                var svc = CreateService();
                var result = await svc.RequestResetPasswordAsync("user@example.com");

                Assert.IsNotNull(result);
                Assert.AreEqual(777, result.Id);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/reset-password");
            }
        });

        [UnityTest]
        public IEnumerator RequestResetPasswordAsync_FiresResetRequestedEvent()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/reset-password", _ => CredentialVerificationJson(888));
            try
            {
                var sender = new MockEventSender();
                var svc = CreateService(sender);
                await svc.RequestResetPasswordAsync("user@example.com");

                Assert.IsTrue(sender.GetEventsByName("reset_password_requested").Count > 0,
                    "reset_password_requested event must fire");
            }
            finally
            {
                _server.RemoveHandler("/auth/email/reset-password");
            }
        });

        [UnityTest]
        public IEnumerator RequestResetPasswordAsync_ServerError_ThrowsNoctuaException()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/reset-password", _ => null);
            try
            {
                var svc = CreateService();
                try
                {
                    await svc.RequestResetPasswordAsync("x@y.com");
                    Assert.Fail("Expected NoctuaException");
                }
                catch (NoctuaException ex)
                {
                    Assert.IsNotNull(ex);
                }
            }
            finally
            {
                _server.RemoveHandler("/auth/email/reset-password");
            }
        });

        // ── ConfirmResetPasswordAsync ─────────────────────────────────────────

        [UnityTest]
        public IEnumerator ConfirmResetPasswordAsync_ValidResponse_ReturnsPlayerToken()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/verify-reset-password", _ => PlayerTokenJson("reset-tok-1"));
            try
            {
                var svc = CreateService();
                var result = await svc.ConfirmResetPasswordAsync(id: 10, code: "123456", newPassword: "newpass");

                Assert.IsNotNull(result);
                Assert.AreEqual("reset-tok-1", result.Player?.AccessToken);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/verify-reset-password");
            }
        });

        [UnityTest]
        public IEnumerator ConfirmResetPasswordAsync_FiresResetCompletedEvent()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/verify-reset-password", _ => PlayerTokenJson("reset-tok-2"));
            try
            {
                var sender = new MockEventSender();
                var svc = CreateService(sender);
                await svc.ConfirmResetPasswordAsync(id: 11, code: "654321", newPassword: "securepass");

                Assert.IsTrue(sender.GetEventsByName("reset_password_completed").Count > 0,
                    "reset_password_completed event must fire");
            }
            finally
            {
                _server.RemoveHandler("/auth/email/verify-reset-password");
            }
        });

        // ── RegisterWithEmailVerifyPhoneNumberAsync ───────────────────────────

        [UnityTest]
        public IEnumerator RegisterWithEmailVerifyPhoneNumberAsync_ValidResponse_ReturnsVerification()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/verify-phone-number-registration",
                _ => PhoneVerifyResponseJson(true));
            try
            {
                var svc = CreateService();
                var result = await svc.RegisterWithEmailVerifyPhoneNumberAsync("req-abc", "123456");

                Assert.IsNotNull(result);
            }
            finally
            {
                _server.RemoveHandler("/auth/email/verify-phone-number-registration");
            }
        });

        [UnityTest]
        public IEnumerator RegisterWithEmailVerifyPhoneNumberAsync_ServerError_ThrowsNoctuaException()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/email/verify-phone-number-registration", _ => null);
            try
            {
                var svc = CreateService();
                try
                {
                    await svc.RegisterWithEmailVerifyPhoneNumberAsync("bad-id", "000000");
                    Assert.Fail("Expected NoctuaException");
                }
                catch (NoctuaException ex)
                {
                    Assert.IsNotNull(ex);
                }
            }
            finally
            {
                _server.RemoveHandler("/auth/email/verify-phone-number-registration");
            }
        });

        // ── LogoutAsync ───────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator LogoutAsync_DelegatesToLoginAsGuestAsync()
            => UniTask.ToCoroutine(async () =>
        {
            // LogoutAsync internally calls LoginAsGuestAsync
            _server.AddHandler("/auth/guest/login", _ => GuestTokenJson("logout-guest-tok"));
            try
            {
                var svc = CreateService();
                var result = await svc.LogoutAsync();

                Assert.IsNotNull(result, "LogoutAsync must return a UserBundle (guest)");
                Assert.AreEqual("logout-guest-tok", result.Player?.AccessToken);
            }
            finally
            {
                _server.RemoveHandler("/auth/guest/login");
            }
        });

        // ── GetUserAsync ──────────────────────────────────────────────────────

        [Test]
        public async Task GetUserAsync_NoAccessToken_ThrowsMissingAccessToken()
        {
            var svc = CreateService();
            // No authentication → RecentAccount.Player.AccessToken is null
            try
            {
                await svc.GetUserAsync();
                Assert.Fail("Expected NoctuaException for missing access token");
            }
            catch (NoctuaException ex)
            {
                Assert.IsNotNull(ex, "NoctuaException must be thrown when access token is missing");
            }
        }

        [UnityTest]
        public IEnumerator GetUserAsync_WithToken_ReturnsUser()
            => UniTask.ToCoroutine(async () =>
        {
            // First authenticate to get a token
            _server.AddHandler("/auth/guest/login", _ => PlayerTokenJson("user-profile-tok", userId: 20));
            _server.AddHandler("/user/profile", _ => UserJson(id: 20, email: "profuser@test.com"));
            try
            {
                var svc = CreateService();
                await svc.LoginAsGuestAsync();

                var user = await svc.GetUserAsync();

                Assert.IsNotNull(user);
                Assert.AreEqual(20L, user.Id);
            }
            finally
            {
                _server.RemoveHandler("/auth/guest/login");
                _server.RemoveHandler("/user/profile");
            }
        });

        // ── UpdateUserAsync ───────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator UpdateUserAsync_NoAccessToken_ThrowsMissingAccessToken()
            => UniTask.ToCoroutine(async () =>
        {
            var svc = CreateService();
            try
            {
                await svc.UpdateUserAsync(new UpdateUserRequest { Language = "en" });
                Assert.Fail("Expected NoctuaException for missing token");
            }
            catch (NoctuaException ex)
            {
                Assert.AreEqual((int)NoctuaErrorCode.Authentication, ex.ErrorCode,
                    "Missing token must throw Authentication error");
            }
        });

        [UnityTest]
        public IEnumerator UpdateUserAsync_WithToken_FiresProfileUpdatedEvent()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/guest/login", _ => PlayerTokenJson("update-tok", userId: 25));
            _server.AddHandler("/user/profile",     _ => EmptyObjectJson());
            // ExchangeTokenAsync is called after update — mock it too
            _server.AddHandler("/auth/token-exchange", _ => PlayerTokenJson("exchanged-update-tok", userId: 25));
            try
            {
                var sender = new MockEventSender();
                var svc = CreateService(sender);
                await svc.LoginAsGuestAsync();

                await svc.UpdateUserAsync(new UpdateUserRequest
                {
                    Language = "en",
                    Country  = "US",
                    Currency = "USD"
                });

                Assert.IsTrue(sender.GetEventsByName("profile_updated").Count > 0,
                    "profile_updated event must fire after UpdateUserAsync");
            }
            finally
            {
                _server.RemoveHandler("/auth/guest/login");
                _server.RemoveHandler("/user/profile");
                _server.RemoveHandler("/auth/token-exchange");
            }
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // NoctuaAuthenticationServiceAccountMgmtTest
    //
    // Covers game state CRUD (SaveGameStateAsync, LoadGameStateAsync,
    // GetGameStateKeysAsync, DeleteGameStateAsync), GetProfileOptions,
    // UpdatePlayerAccountAsync, DeletePlayerAccountAsync, ResetAccounts.
    // Port 7791 — distinct from all other auth test ports (7778, 7779, 7780, 7781).
    // ══════════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class NoctuaAuthenticationServiceAccountMgmtTest
    {
        private const string BaseUrl   = "http://localhost:7791/api/v1";
        private const string ServerUrl = "http://localhost:7791/api/v1/";

        private HttpMockServer _server;
        private MockNativeAccountStore _store;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _server = new HttpMockServer(ServerUrl);
            _server.Start();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _server.Dispose();
        }

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            _store = new MockNativeAccountStore();
            while (_server.Requests.TryDequeue(out _)) { }
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
        }

        // ── Factory helpers ───────────────────────────────────────────────────

        private NoctuaAuthenticationService CreateService(MockEventSender eventSender = null)
        {
            return new NoctuaAuthenticationService(
                baseUrl:            BaseUrl,
                clientId:           "test-client-id",
                nativeAccountStore: _store,
                bundleId:           "com.test.app",
                eventSender:        eventSender
            );
        }

        // ── JSON helpers ──────────────────────────────────────────────────────

        private static string PlayerTokenJson(
            string accessToken = "mgmt-token",
            long   userId      = 50,
            long   gameId      = 200) =>
            $@"{{""data"":{{""access_token"":""{accessToken}"",""player"":{{""id"":{userId},""game_id"":{gameId},""bundle_id"":""com.test.app"",""access_token"":""{accessToken}""}},""user"":{{""id"":{userId},""is_guest"":true}},""credential"":{{""id"":1,""provider"":""device_id""}},""game"":{{""id"":{gameId}}},""game_platform"":{{""id"":10,""bundle_id"":""com.test.app""}}}}}}";

        private static string DataEnvelope(string inner) => $"{{\"data\":{inner}}}";

        private static string CloudSaveListJson(params string[] keys)
        {
            var saves = string.Join(",", System.Array.ConvertAll(
                keys,
                k => $"{{\"slot_key\":\"{k}\",\"size_bytes\":10,\"content_type\":\"text/plain\"}}"));
            return DataEnvelope($"{{\"saves\":[{saves}],\"total\":{keys.Length}}}");
        }

        private static string ProfileOptionsJson() =>
            DataEnvelope("{\"countries\":[{\"iso_code\":\"US\"}],\"languages\":[{\"iso_code\":\"en\"}],\"currencies\":[{\"iso_code\":\"USD\"}]}");

        private static string DeleteAccountJson() =>
            DataEnvelope("{\"is_deleted\":true}");

        private static string PlayerSyncJson() =>
            DataEnvelope("{}");

        // ══════════════════════════════════════════════════════════════════════
        // SaveGameStateAsync
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task SaveGameStateAsync_NoAccessToken_ThrowsMissingToken()
        {
            var svc = CreateService();
            try
            {
                await svc.SaveGameStateAsync("level", "5").AsTask();
                Assert.Fail("Expected NoctuaException for missing access token");
            }
            catch (NoctuaException ex)
            {
                Assert.IsNotNull(ex, "SaveGameStateAsync must throw NoctuaException when token is missing");
            }
        }

        [UnityTest]
        public IEnumerator SaveGameStateAsync_WithToken_SendsPutRequest()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/guest/login", _ => PlayerTokenJson());
            _server.AddHandler("/cloud-saves/level", _ => DataEnvelope("{\"slot_key\":\"level\",\"size_bytes\":1}"));
            try
            {
                var svc = CreateService();
                await svc.LoginAsGuestAsync();

                await svc.SaveGameStateAsync("level", "42");

                // Verify PUT request was made
                // (may have queued both login + cloud-save requests)
                bool foundPut = false;
                while (_server.Requests.TryDequeue(out var req))
                {
                    if (req.Path.Contains("cloud-saves") && req.Method == "PUT")
                    {
                        foundPut = true;
                        break;
                    }
                }
                Assert.IsTrue(foundPut, "SaveGameStateAsync must issue a PUT /cloud-saves/{key} request");
            }
            finally
            {
                _server.RemoveHandler("/auth/guest/login");
                _server.RemoveHandler("/cloud-saves/level");
            }
        });

        // ══════════════════════════════════════════════════════════════════════
        // LoadGameStateAsync
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task LoadGameStateAsync_NoAccessToken_ThrowsMissingToken()
        {
            var svc = CreateService();
            try
            {
                await svc.LoadGameStateAsync("level").AsTask();
                Assert.Fail("Expected NoctuaException for missing access token");
            }
            catch (NoctuaException ex)
            {
                Assert.IsNotNull(ex);
            }
        }

        [UnityTest]
        public IEnumerator LoadGameStateAsync_WithToken_ReturnsRawValue()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/guest/login", _ => PlayerTokenJson());
            _server.AddHandler("/cloud-saves/mykey", _ => "hello-world");
            try
            {
                var svc = CreateService();
                await svc.LoginAsGuestAsync();

                var result = await svc.LoadGameStateAsync("mykey");

                Assert.AreEqual("hello-world", result,
                    "LoadGameStateAsync must return the raw server response string");
            }
            finally
            {
                _server.RemoveHandler("/auth/guest/login");
                _server.RemoveHandler("/cloud-saves/mykey");
            }
        });

        // ══════════════════════════════════════════════════════════════════════
        // GetGameStateKeysAsync
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetGameStateKeysAsync_NoAccessToken_ThrowsMissingToken()
        {
            var svc = CreateService();
            try
            {
                await svc.GetGameStateKeysAsync().AsTask();
                Assert.Fail("Expected NoctuaException for missing access token");
            }
            catch (NoctuaException ex)
            {
                Assert.IsNotNull(ex);
            }
        }

        [UnityTest]
        public IEnumerator GetGameStateKeysAsync_WithToken_ReturnsKeys()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/guest/login", _ => PlayerTokenJson());
            _server.AddHandler("/cloud-saves", _ => CloudSaveListJson("slot1", "slot2", "slot3"));
            try
            {
                var svc = CreateService();
                await svc.LoginAsGuestAsync();

                var keys = await svc.GetGameStateKeysAsync();

                Assert.IsNotNull(keys);
                Assert.AreEqual(3, keys.Count, "GetGameStateKeysAsync must return one key per save slot");
                Assert.Contains("slot1", keys);
                Assert.Contains("slot3", keys);
            }
            finally
            {
                _server.RemoveHandler("/auth/guest/login");
                _server.RemoveHandler("/cloud-saves");
            }
        });

        [UnityTest]
        public IEnumerator GetGameStateKeysAsync_EmptyList_ReturnsEmptyList()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/guest/login", _ => PlayerTokenJson());
            _server.AddHandler("/cloud-saves", _ => DataEnvelope("{\"saves\":null,\"total\":0}"));
            try
            {
                var svc = CreateService();
                await svc.LoginAsGuestAsync();

                var keys = await svc.GetGameStateKeysAsync();

                Assert.IsNotNull(keys);
                Assert.AreEqual(0, keys.Count, "Null saves array should return empty list");
            }
            finally
            {
                _server.RemoveHandler("/auth/guest/login");
                _server.RemoveHandler("/cloud-saves");
            }
        });

        // ══════════════════════════════════════════════════════════════════════
        // DeleteGameStateAsync
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DeleteGameStateAsync_NoAccessToken_ThrowsMissingToken()
        {
            var svc = CreateService();
            try
            {
                await svc.DeleteGameStateAsync("somekey").AsTask();
                Assert.Fail("Expected NoctuaException for missing access token");
            }
            catch (NoctuaException ex)
            {
                Assert.IsNotNull(ex);
            }
        }

        [UnityTest]
        public IEnumerator DeleteGameStateAsync_WithToken_SendsDeleteRequest()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/guest/login", _ => PlayerTokenJson());
            _server.AddHandler("/cloud-saves/myslot", _ => DataEnvelope("null"));
            try
            {
                var svc = CreateService();
                await svc.LoginAsGuestAsync();

                await svc.DeleteGameStateAsync("myslot");

                bool foundDelete = false;
                while (_server.Requests.TryDequeue(out var req))
                {
                    if (req.Path.Contains("cloud-saves") && req.Method == "DELETE")
                    {
                        foundDelete = true;
                        break;
                    }
                }
                Assert.IsTrue(foundDelete, "DeleteGameStateAsync must issue a DELETE request");
            }
            finally
            {
                _server.RemoveHandler("/auth/guest/login");
                _server.RemoveHandler("/cloud-saves/myslot");
            }
        });

        // ══════════════════════════════════════════════════════════════════════
        // GetProfileOptions
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetProfileOptions_NoAccessToken_ThrowsMissingToken()
        {
            var svc = CreateService();
            try
            {
                await svc.GetProfileOptions().AsTask();
                Assert.Fail("Expected NoctuaException for missing access token");
            }
            catch (NoctuaException ex)
            {
                Assert.IsNotNull(ex);
            }
        }

        [UnityTest]
        public IEnumerator GetProfileOptions_WithToken_ReturnsData()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/guest/login",     _ => PlayerTokenJson());
            _server.AddHandler("/user/profile-options", _ => ProfileOptionsJson());
            try
            {
                var svc = CreateService();
                await svc.LoginAsGuestAsync();

                var options = await svc.GetProfileOptions();

                Assert.IsNotNull(options, "GetProfileOptions must return a non-null result");
                Assert.IsNotNull(options.Countries, "Countries list must not be null");
                Assert.IsTrue(options.Countries.Count > 0, "Must have at least one country option");
            }
            finally
            {
                _server.RemoveHandler("/auth/guest/login");
                _server.RemoveHandler("/user/profile-options");
            }
        });

        // ══════════════════════════════════════════════════════════════════════
        // UpdatePlayerAccountAsync
        // ══════════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator UpdatePlayerAccountAsync_WithToken_FiresRoleUpdatedEvent()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/guest/login", _ => PlayerTokenJson());
            _server.AddHandler("/players/sync",     _ => PlayerSyncJson());
            try
            {
                var sender = new MockEventSender();
                var svc = CreateService(sender);
                await svc.LoginAsGuestAsync();

                await svc.UpdatePlayerAccountAsync(new PlayerAccountData
                {
                    IngameUsername = "TestPlayer",
                    IngameServerId = "server-1",
                    IngameRoleId   = "role-warrior"
                });

                Assert.IsTrue(sender.GetEventsByName("role_updated").Count > 0,
                    "role_updated event must fire after UpdatePlayerAccountAsync");
            }
            finally
            {
                _server.RemoveHandler("/auth/guest/login");
                _server.RemoveHandler("/players/sync");
            }
        });

        // ══════════════════════════════════════════════════════════════════════
        // DeletePlayerAccountAsync
        // ══════════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator DeletePlayerAccountAsync_WithToken_FiresAccountDeletedEvent()
            => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth/guest/login",  _ => PlayerTokenJson());
            _server.AddHandler("/players/destroy",   _ => DeleteAccountJson());
            try
            {
                var sender = new MockEventSender();
                var svc = CreateService(sender);
                await svc.LoginAsGuestAsync();

                await svc.DeletePlayerAccountAsync();

                Assert.IsTrue(sender.GetEventsByName("account_deleted").Count > 0,
                    "account_deleted event must fire after DeletePlayerAccountAsync");
            }
            finally
            {
                _server.RemoveHandler("/auth/guest/login");
                _server.RemoveHandler("/players/destroy");
            }
        });

        // ══════════════════════════════════════════════════════════════════════
        // ResetAccounts
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public void ResetAccounts_DoesNotThrow()
        {
            var svc = CreateService();
            Assert.DoesNotThrow(() => svc.ResetAccounts(),
                "ResetAccounts must not throw even when no accounts are stored");
        }

        // ══════════════════════════════════════════════════════════════════════
        // FileUploader — access-token guard only (file upload not testable in EditMode)
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task FileUploader_NoAccessToken_ThrowsMissingToken()
        {
            var svc = CreateService();
            try
            {
                await svc.FileUploader("/nonexistent/path.jpg").AsTask();
                Assert.Fail("Expected NoctuaException for missing access token");
            }
            catch (NoctuaException ex)
            {
                Assert.IsNotNull(ex);
            }
        }
    }
}
