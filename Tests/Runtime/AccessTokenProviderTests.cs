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
    }
}
