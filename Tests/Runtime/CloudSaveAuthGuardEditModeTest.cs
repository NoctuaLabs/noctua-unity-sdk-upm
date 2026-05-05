using com.noctuagames.sdk;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime
{
    /// <summary>
    /// EditMode NUnit tests for the CloudSave authentication-guard paths in
    /// <see cref="NoctuaAuthenticationService"/>:
    ///   — <c>SaveGameStateAsync</c>, <c>LoadGameStateAsync</c>,
    ///     <c>GetGameStateKeysAsync</c>, <c>DeleteGameStateAsync</c>
    ///     all throw <see cref="NoctuaException"/> with
    ///     <see cref="NoctuaErrorCode.Authentication"/> before the first
    ///     <c>await</c> when no access token is present.
    ///   — <see cref="NoctuaAuthenticationService.IsAuthenticated"/> and
    ///     <see cref="NoctuaAuthenticationService.RecentAccount"/> on a freshly
    ///     constructed (unauthenticated) service.
    ///
    /// These tests mirror the 8 guard-path <c>[UnityTest]</c> methods in
    /// <c>CloudSaveTest.CloudSaveAuthGuardTests</c>, which use
    /// <c>[UnitySetUp]</c> / <c>yield return null</c> solely because they are
    /// PlayMode tests — not because they exercise any async logic.
    ///
    /// All guard branches throw <c>NoctuaException.MissingAccessToken</c>
    /// BEFORE the first HTTP <c>await</c>, so <c>.GetAwaiter().GetResult()</c>
    /// resolves synchronously without entering the UniTask scheduler.
    /// </summary>
    [TestFixture]
    public class CloudSaveAuthGuardEditModeTest
    {
        private NoctuaAuthenticationService _authService;

        [SetUp]
        public void SetUp()
        {
            // Clear stored accounts so the service starts unauthenticated.
            PlayerPrefs.DeleteKey("NoctuaAccountContainer");
            PlayerPrefs.DeleteKey("NoctuaAccountContainer.UseFallback");

            // Construct with a DefaultNativePlugin (EditMode-safe editor stub).
            // No call to Load() or LoginAsGuestAsync() → RecentAccount is null,
            // AccessToken is empty, IsAuthenticated is false.
            _authService = new NoctuaAuthenticationService(
                baseUrl: "https://sdk-test.noctuaprojects.com",
                clientId: "test-client-id",
                nativeAccountStore: new DefaultNativePlugin(),
                locale: null,
                bundleId: "com.test.cloudsave",
                eventSender: null
            );
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey("NoctuaAccountContainer");
            PlayerPrefs.DeleteKey("NoctuaAccountContainer.UseFallback");
        }

        // ─── IsAuthenticated / RecentAccount on fresh service ─────────────────

        [Test]
        public void IsAuthenticated_FreshService_ReturnsFalse()
        {
            // No accounts loaded → AccessToken is null → IsAuthenticated must be false.
            Assert.IsFalse(_authService.IsAuthenticated);
        }

        [Test]
        public void RecentAccount_FreshService_IsNull()
        {
            // AccountContainer.Load() was never called → RecentAccount is null.
            Assert.IsNull(_authService.RecentAccount);
        }

        // ─── Auth guard on CloudSave methods ──────────────────────────────────

        [Test]
        public void SaveGameStateAsync_NotAuthenticated_ThrowsNoctuaException()
        {
            // Guard fires before the first HTTP await → UniTask is faulted synchronously.
            var ex = Assert.Throws<NoctuaException>(() =>
                _authService.SaveGameStateAsync("slot-key", "value")
                    .GetAwaiter().GetResult());

            Assert.AreEqual((int)NoctuaErrorCode.Authentication, ex.ErrorCode,
                "SaveGameStateAsync must throw Authentication error when not authenticated");
        }

        [Test]
        public void LoadGameStateAsync_NotAuthenticated_ThrowsNoctuaException()
        {
            var ex = Assert.Throws<NoctuaException>(() =>
                _authService.LoadGameStateAsync("slot-key")
                    .GetAwaiter().GetResult());

            Assert.AreEqual((int)NoctuaErrorCode.Authentication, ex.ErrorCode,
                "LoadGameStateAsync must throw Authentication error when not authenticated");
        }

        [Test]
        public void GetGameStateKeysAsync_NotAuthenticated_ThrowsNoctuaException()
        {
            var ex = Assert.Throws<NoctuaException>(() =>
                _authService.GetGameStateKeysAsync()
                    .GetAwaiter().GetResult());

            Assert.AreEqual((int)NoctuaErrorCode.Authentication, ex.ErrorCode,
                "GetGameStateKeysAsync must throw Authentication error when not authenticated");
        }

        [Test]
        public void DeleteGameStateAsync_NotAuthenticated_ThrowsNoctuaException()
        {
            var ex = Assert.Throws<NoctuaException>(() =>
                _authService.DeleteGameStateAsync("slot-key")
                    .GetAwaiter().GetResult());

            Assert.AreEqual((int)NoctuaErrorCode.Authentication, ex.ErrorCode,
                "DeleteGameStateAsync must throw Authentication error when not authenticated");
        }

        // ─── Error message content ────────────────────────────────────────────

        [Test]
        public void SaveGameStateAsync_NotAuthenticated_MessageContainsAccessToken()
        {
            // MissingAccessToken message must reference "access token" (case-insensitive).
            NoctuaException caught = null;
            try
            {
                _authService.SaveGameStateAsync("k", "v").GetAwaiter().GetResult();
            }
            catch (NoctuaException ex)
            {
                caught = ex;
            }

            Assert.IsNotNull(caught);
            StringAssert.Contains("access token", caught.Message.ToLower(),
                "Exception message must mention 'access token'");
        }

        // ─── All four guards throw (stateless, idempotent) ────────────────────

        [Test]
        public void MultipleGuardCalls_AllThrowNoctuaException()
        {
            // All four save/load/keys/delete calls throw before the first HTTP await.
            // TryCall awaits a faulted UniTask → catches synchronously → completes synchronously.
            // The outer RunAll().GetAwaiter().GetResult() therefore runs to completion
            // without entering the UniTask scheduler.
            int throwCount = 0;

            async UniTask TryCall(System.Func<UniTask> call)
            {
                try { await call(); }
                catch (NoctuaException) { throwCount++; }
            }

            async UniTask RunAll()
            {
                await TryCall(() => _authService.SaveGameStateAsync("k", "v"));
                await TryCall(() => _authService.LoadGameStateAsync("k"));
                await TryCall(() => _authService.GetGameStateKeysAsync());
                await TryCall(() => _authService.DeleteGameStateAsync("k"));
            }

            RunAll().GetAwaiter().GetResult();

            Assert.AreEqual(4, throwCount,
                "All four CloudSave guard checks must throw NoctuaException when unauthenticated");
        }
    }
}
