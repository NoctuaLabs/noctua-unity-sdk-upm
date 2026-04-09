using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace com.noctuagames.sdk.Tests.Auth
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
}
