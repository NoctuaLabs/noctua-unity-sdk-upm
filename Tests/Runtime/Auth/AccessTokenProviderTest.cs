using System;
using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime.Auth
{
    public class AccessTokenProviderTest
    {
        private class FakeAccountEvents : IAccountEvents
        {
            public event Action<UserBundle> OnAccountChanged;
            public event Action<Player> OnAccountDeleted;

            public void RaiseChanged(UserBundle bundle) => OnAccountChanged?.Invoke(bundle);
            public void RaiseDeleted(Player player) => OnAccountDeleted?.Invoke(player);
        }

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey("NoctuaAccessToken");
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey("NoctuaAccessToken");
            PlayerPrefs.Save();
        }

        [Test]
        public void IsAuthenticated_InitiallyFalse_WhenNoTokenStored()
        {
            var events = new FakeAccountEvents();
            var provider = new AccessTokenProvider(events);
            Assert.IsFalse(provider.IsAuthenticated);
        }

        [Test]
        public void AccessToken_ThrowsNoctuaException_WhenNotAuthenticated()
        {
            var events = new FakeAccountEvents();
            var provider = new AccessTokenProvider(events);

            var ex = Assert.Throws<NoctuaException>(() => { var _ = provider.AccessToken; });
            Assert.AreEqual((int)NoctuaErrorCode.Authentication, ex.ErrorCode);
        }

        [Test]
        public void AccessToken_ReadsFromPlayerPrefs_WhenMemoryEmpty()
        {
            PlayerPrefs.SetString("NoctuaAccessToken", "prefs-token");
            PlayerPrefs.Save();

            var events = new FakeAccountEvents();
            var provider = new AccessTokenProvider(events);

            Assert.AreEqual("prefs-token", provider.AccessToken);
        }

        [Test]
        public void OnAccountChanged_UpdatesInMemoryToken()
        {
            var events = new FakeAccountEvents();
            var provider = new AccessTokenProvider(events);

            events.RaiseChanged(new UserBundle { Player = new Player { AccessToken = "new-token" } });

            Assert.IsTrue(provider.IsAuthenticated);
            Assert.AreEqual("new-token", provider.AccessToken);
        }

        [Test]
        public void OnAccountChanged_WithNullBundle_ClearsToken()
        {
            var events = new FakeAccountEvents();
            var provider = new AccessTokenProvider(events);

            events.RaiseChanged(new UserBundle { Player = new Player { AccessToken = "tok" } });
            Assert.IsTrue(provider.IsAuthenticated);

            events.RaiseChanged(null);
            Assert.IsFalse(provider.IsAuthenticated);
        }

        [Test]
        public void OnAccountChanged_WithNullPlayer_ClearsToken()
        {
            var events = new FakeAccountEvents();
            var provider = new AccessTokenProvider(events);

            events.RaiseChanged(new UserBundle { Player = new Player { AccessToken = "tok" } });
            events.RaiseChanged(new UserBundle { Player = null });

            Assert.IsFalse(provider.IsAuthenticated);
        }

        [Test]
        public void OnAccountDeleted_ClearsToken()
        {
            var events = new FakeAccountEvents();
            var provider = new AccessTokenProvider(events);

            events.RaiseChanged(new UserBundle { Player = new Player { AccessToken = "tok" } });
            Assert.IsTrue(provider.IsAuthenticated);

            events.RaiseDeleted(new Player { Id = 1 });
            Assert.IsFalse(provider.IsAuthenticated);
        }

        [Test]
        public void InMemoryToken_PreferredOverPlayerPrefs()
        {
            PlayerPrefs.SetString("NoctuaAccessToken", "prefs-token");
            PlayerPrefs.Save();

            var events = new FakeAccountEvents();
            var provider = new AccessTokenProvider(events);

            events.RaiseChanged(new UserBundle { Player = new Player { AccessToken = "memory-token" } });

            Assert.AreEqual("memory-token", provider.AccessToken);
        }
    }
}
