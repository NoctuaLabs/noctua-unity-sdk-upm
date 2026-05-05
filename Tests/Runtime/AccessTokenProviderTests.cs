using System;
using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime
{
    public class AccessTokenProviderTests
    {
        private const string PrefsKey = "NoctuaAccessToken";

        private class StubAccountEvents : IAccountEvents
        {
            public event Action<UserBundle> OnAccountChanged;
            public event Action<Player> OnAccountDeleted;

            public void RaiseChanged(UserBundle u) => OnAccountChanged?.Invoke(u);
            public void RaiseDeleted(Player p) => OnAccountDeleted?.Invoke(p);
        }

        [SetUp]
        public void ClearPrefs()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
        }

        [TearDown]
        public void ResetPrefs()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
        }

        [Test]
        public void IsAuthenticated_FalseWhenNoToken()
        {
            var stub = new StubAccountEvents();
            var provider = new AccessTokenProvider(stub);
            Assert.IsFalse(provider.IsAuthenticated);
        }

        [Test]
        public void AccessToken_ThrowsWhenUnauthenticated()
        {
            var stub = new StubAccountEvents();
            var provider = new AccessTokenProvider(stub);

            var ex = Assert.Throws<NoctuaException>(() => { var _ = provider.AccessToken; });
            Assert.AreEqual((int)NoctuaErrorCode.Authentication, ex.ErrorCode);
        }

        [Test]
        public void AccessToken_FallsBackToPlayerPrefs()
        {
            PlayerPrefs.SetString(PrefsKey, "prefs-token");
            try
            {
                var stub = new StubAccountEvents();
                var provider = new AccessTokenProvider(stub);

                Assert.AreEqual("prefs-token", provider.AccessToken);
                Assert.IsTrue(provider.IsAuthenticated);
            }
            finally
            {
                PlayerPrefs.DeleteKey(PrefsKey);
            }
        }

        [Test]
        public void OnAccountChanged_UpdatesToken()
        {
            var stub = new StubAccountEvents();
            var provider = new AccessTokenProvider(stub);

            var bundle = new UserBundle { Player = new Player { AccessToken = "fresh" } };
            stub.RaiseChanged(bundle);

            Assert.IsTrue(provider.IsAuthenticated);
            Assert.AreEqual("fresh", provider.AccessToken);
        }

        [Test]
        public void OnAccountChanged_NullBundle_ClearsToken()
        {
            var stub = new StubAccountEvents();
            var provider = new AccessTokenProvider(stub);

            stub.RaiseChanged(new UserBundle { Player = new Player { AccessToken = "x" } });
            Assert.IsTrue(provider.IsAuthenticated);

            stub.RaiseChanged(null);
            Assert.IsFalse(provider.IsAuthenticated);
        }

        [Test]
        public void OnAccountDeleted_ClearsToken()
        {
            var stub = new StubAccountEvents();
            var provider = new AccessTokenProvider(stub);

            stub.RaiseChanged(new UserBundle { Player = new Player { AccessToken = "x" } });
            Assert.IsTrue(provider.IsAuthenticated);

            stub.RaiseDeleted(new Player());
            Assert.IsFalse(provider.IsAuthenticated);
        }

        [Test]
        public void IAccessTokenProvider_InterfaceImplemented()
        {
            var stub = new StubAccountEvents();
            IAccessTokenProvider provider = new AccessTokenProvider(stub);
            Assert.IsNotNull(provider);
            Assert.IsFalse(provider.IsAuthenticated);
        }

        // ─── Additional tests ─────────────────────────────────────────────────

        [Test]
        public void OnAccountChanged_OverwritesPreviousToken()
        {
            var stub     = new StubAccountEvents();
            var provider = new AccessTokenProvider(stub);

            stub.RaiseChanged(new UserBundle { Player = new Player { AccessToken = "first" } });
            stub.RaiseChanged(new UserBundle { Player = new Player { AccessToken = "second" } });

            Assert.IsTrue(provider.IsAuthenticated);
            Assert.AreEqual("second", provider.AccessToken,
                "Second OnAccountChanged must overwrite the first token");
        }

        [Test]
        public void OnAccountChanged_NullPlayer_DoesNotThrow_ClearsToken()
        {
            var stub     = new StubAccountEvents();
            var provider = new AccessTokenProvider(stub);

            // First establish a valid token
            stub.RaiseChanged(new UserBundle { Player = new Player { AccessToken = "valid" } });
            Assert.IsTrue(provider.IsAuthenticated);

            // Now send a bundle whose Player is null — must not throw and must clear the token
            Assert.DoesNotThrow(() => stub.RaiseChanged(new UserBundle { Player = null }),
                "OnAccountChanged with null Player must not throw");
            Assert.IsFalse(provider.IsAuthenticated,
                "IsAuthenticated must be false after receiving a bundle with null Player");
        }

        [Test]
        public void OnAccountChanged_EmptyStringToken_IsNotAuthenticated()
        {
            var stub     = new StubAccountEvents();
            var provider = new AccessTokenProvider(stub);

            stub.RaiseChanged(new UserBundle { Player = new Player { AccessToken = "" } });

            Assert.IsFalse(provider.IsAuthenticated,
                "Empty-string AccessToken must not count as authenticated");
        }

        [Test]
        public void IsAuthenticated_FalseBeforeGetAccessToken()
        {
            var stub     = new StubAccountEvents();
            var provider = new AccessTokenProvider(stub);

            // Set a token then delete it via OnAccountDeleted
            stub.RaiseChanged(new UserBundle { Player = new Player { AccessToken = "tok" } });
            stub.RaiseDeleted(new Player());

            Assert.IsFalse(provider.IsAuthenticated,
                "IsAuthenticated must be false after OnAccountDeleted");
        }

        [Test]
        public void OnAccountDeleted_CalledTwice_DoesNotThrow()
        {
            var stub     = new StubAccountEvents();
            var provider = new AccessTokenProvider(stub);

            stub.RaiseChanged(new UserBundle { Player = new Player { AccessToken = "tok" } });

            Assert.DoesNotThrow(() =>
            {
                stub.RaiseDeleted(new Player());
                stub.RaiseDeleted(new Player());
            }, "Calling OnAccountDeleted twice must not throw");

            Assert.IsFalse(provider.IsAuthenticated);
        }

        [Test]
        public void OnAccountChanged_AfterDeleted_ReturnsNewToken()
        {
            var stub     = new StubAccountEvents();
            var provider = new AccessTokenProvider(stub);

            stub.RaiseChanged(new UserBundle { Player = new Player { AccessToken = "old" } });
            stub.RaiseDeleted(new Player());
            Assert.IsFalse(provider.IsAuthenticated);

            stub.RaiseChanged(new UserBundle { Player = new Player { AccessToken = "new-token" } });

            Assert.IsTrue(provider.IsAuthenticated,
                "IsAuthenticated must be true after a new OnAccountChanged following deletion");
            Assert.AreEqual("new-token", provider.AccessToken,
                "AccessToken must return the new token set after deletion");
        }
    }
}
