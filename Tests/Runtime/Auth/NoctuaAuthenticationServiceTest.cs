using System;
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
    }
}
