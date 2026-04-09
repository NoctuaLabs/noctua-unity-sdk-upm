using NUnit.Framework;

namespace com.noctuagames.sdk.Tests
{
    /// <summary>
    /// Tests for <see cref="InternetChecker"/>.
    ///
    /// <see cref="InternetChecker.CheckInternetConnectionAsync"/> makes a real HTTP GET to
    /// <c>https://sdk-api-v2.noctuaprojects.com/api/v1/games/ping</c> and requires the Unity
    /// main thread (it calls <c>await UniTask.SwitchToMainThread()</c> internally).  All tests
    /// that exercise the live network call are therefore marked [Ignore] and belong to the
    /// integration test suite.
    ///
    /// What <em>can</em> be tested here is the static class shape and the fact that
    /// <see cref="InternetChecker"/> is a public, accessible type with the expected API surface.
    /// </summary>
    [TestFixture]
    public class InternetCheckerTest
    {
        // ─── API surface (compile-time assertions) ─────────────────────────────

        [Test]
        public void InternetChecker_IsPublicStaticClass()
        {
            var type = typeof(InternetChecker);

            Assert.IsTrue(type.IsClass,    "InternetChecker must be a class");
            Assert.IsTrue(type.IsAbstract && type.IsSealed,
                "InternetChecker must be a static class (abstract + sealed in IL)");
        }

        [Test]
        public void CheckInternetConnectionAsync_MethodExists()
        {
            // Verify the method is accessible and has the expected signature
            var method = typeof(InternetChecker).GetMethod(
                "CheckInternetConnectionAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
            );

            Assert.IsNotNull(method, "CheckInternetConnectionAsync must be a public static method");
        }

        // ─── Live-network tests (Ignore) ───────────────────────────────────────

        [Test]
        [Ignore("Requires live network: HTTP GET to https://sdk-api-v2.noctuaprojects.com/api/v1/games/ping. " +
                "Also requires Unity main thread (UnityWebRequest). Run in device integration suite.")]
        public void CheckInternetConnectionAsync_OnlineDevice_InvokesCallbackWithTrue() { }

        [Test]
        [Ignore("Requires offline device or unreachable server to test the false path. " +
                "Run in device integration suite with airplane mode enabled.")]
        public void CheckInternetConnectionAsync_OfflineDevice_InvokesCallbackWithFalse() { }

        [Test]
        [Ignore("Requires Unity play mode (Application.isPlaying must be true). " +
                "Run as a UnityTest in Play Mode test suite.")]
        public void CheckInternetConnectionAsync_DuringAppQuit_SkipsCheck() { }
    }
}
