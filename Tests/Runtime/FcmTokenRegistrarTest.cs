using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace com.noctuagames.sdk.Tests
{
    /// <summary>
    /// Unit tests for <see cref="FcmTokenRegistrar"/> — the cache backing the X-FCM-TOKEN header.
    /// The header injection itself lives in the internal <c>HttpRequest</c> and is verified on
    /// device via the Inspector HTTP tab.
    /// </summary>
    public class FcmTokenRegistrarTest
    {
        private static FcmTokenRegistrar Registrar(Func<string> next) =>
            new(() => UniTask.FromResult(next()), isSandbox: false);

        [Test]
        public void Current_IsEmpty_BeforeAnyTokenArrives()
        {
            Assert.AreEqual(string.Empty, Registrar(() => "").Current);
        }

        [Test]
        public void Accept_StoresToken()
        {
            var registrar = Registrar(() => "");

            registrar.Accept("token-a");

            Assert.AreEqual("token-a", registrar.Current);
        }

        [Test]
        public void Accept_EmptyOrNull_DoesNotClearAGoodToken()
        {
            // "Not available yet" must never be mistaken for "revoked" — otherwise a transient
            // empty read would silently stop the header being sent for the rest of the session.
            var registrar = Registrar(() => "");
            registrar.Accept("token-a");

            registrar.Accept("");
            registrar.Accept(null);

            Assert.AreEqual("token-a", registrar.Current);
        }

        [Test]
        public void Accept_OverwritesOnRotation()
        {
            var registrar = Registrar(() => "");
            registrar.Accept("token-a");

            registrar.Accept("token-b");

            Assert.AreEqual("token-b", registrar.Current);
        }

        [Test]
        public async Task RefreshAsync_PicksUpTokenOnceItBecomesAvailable()
        {
            // Models iOS: no token until the user grants notification permission.
            var calls = 0;
            var registrar = new FcmTokenRegistrar(
                () => UniTask.FromResult(++calls < 3 ? "" : "token-late"),
                isSandbox: false
            );

            await registrar.RefreshAsync();
            Assert.AreEqual(string.Empty, registrar.Current, "no token yet on first fetch");

            await registrar.RefreshAsync();
            Assert.AreEqual(string.Empty, registrar.Current, "still no token on second fetch");

            await registrar.RefreshAsync();
            Assert.AreEqual("token-late", registrar.Current, "token adopted once it arrives");
        }

        [Test]
        public async Task RefreshAsync_SwallowsFetchFailure_AndKeepsExistingToken()
        {
            var registrar = new FcmTokenRegistrar(
                () => throw new InvalidOperationException("native plugin exploded"),
                isSandbox: false
            );
            registrar.Accept("token-a");

            await registrar.RefreshAsync();

            Assert.AreEqual("token-a", registrar.Current);
        }

        [Test]
        public async Task OnApplicationResume_RefetchesToken()
        {
            // This is the whole Android rotation strategy — there is no onNewToken bridge there.
            var registrar = Registrar(() => "token-after-resume");

            registrar.OnApplicationResume();
            await UniTask.Yield();

            Assert.AreEqual("token-after-resume", registrar.Current);
        }
    }
}
