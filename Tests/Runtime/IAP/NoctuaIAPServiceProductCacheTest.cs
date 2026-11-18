using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using com.noctuagames.sdk;

namespace Tests.Runtime.IAP
{
    /// <summary>
    /// Product list caching in <see cref="NoctuaIAPService.GetProductListAsync"/>:
    /// cached per request params, cleared by <see cref="NoctuaIAPService.ClearProductListCache"/>
    /// (called on every SDK init), and falling back to the persisted copy when the
    /// remote fetch fails.
    /// </summary>
    [TestFixture]
    public class NoctuaIAPServiceProductCacheTest
    {
        private const string BaseUrl         = "http://localhost:7781/api/v1";
        private const string ServerUrl       = "http://localhost:7781/api/v1/";
        private const string ProductsPath    = "/products";
        private const string CachePrefsKey   = "NoctuaProductListCache";
        private const string AccessTokenKey  = "NoctuaAccessToken";

        // HttpRequest unwraps the API envelope: { "data": <payload> }.
        private const string ProductsJson =
            "{\"data\":[{\"id\":\"pack1\",\"description\":\"Pack 1\",\"game_id\":1,\"price\":0.99," +
            "\"currency\":\"USD\",\"display_price\":\"$0.99\",\"price_in_usd\":\"0.99\",\"platform\":\"playstore\"}]}";

        private HttpMockServer _server;
        private string _savedAccessToken;

        // ── Stubs ─────────────────────────────────────────────────────────────

        private class StubAuthProvider : IAuthProvider
        {
            public long? PlayerId => 42;

            public UserBundle RecentAccount => new()
            {
                Player = new Player { Id = 42, GameId = 1 }
            };

            public UniTask<UserBundle> AuthenticateAsync() => UniTask.FromResult(RecentAccount);
            public UniTask UpdatePlayerAccountAsync(PlayerAccountData data) => UniTask.CompletedTask;
        }

        private class StubAccountEvents : IAccountEvents
        {
            public event Action<UserBundle> OnAccountChanged { add { } remove { } }
            public event Action<Player> OnAccountDeleted { add { } remove { } }
        }

        private class StubLocale : ILocaleProvider
        {
            public string GetLanguage() => "en";
            public string GetCountry() => "US";
            public string GetCurrency() => "USD";
            public string GetTimezone() => "UTC";
            public string GetTranslation(LocaleTextKey textKey) => textKey.ToString();
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

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
            _savedAccessToken = PlayerPrefs.GetString(AccessTokenKey, null);
            PlayerPrefs.SetString(AccessTokenKey, "test-access-token");
            PlayerPrefs.DeleteKey(CachePrefsKey);
            PlayerPrefs.Save();

            _server.AddHandler(ProductsPath, _ => ProductsJson);
            while (_server.Requests.TryDequeue(out _)) { }
        }

        [TearDown]
        public void TearDown()
        {
            if (string.IsNullOrEmpty(_savedAccessToken))
                PlayerPrefs.DeleteKey(AccessTokenKey);
            else
                PlayerPrefs.SetString(AccessTokenKey, _savedAccessToken);

            PlayerPrefs.DeleteKey(CachePrefsKey);
            PlayerPrefs.Save();

            LogAssert.ignoreFailingMessages = false;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static NoctuaIAPService CreateService()
        {
            var service = new NoctuaIAPService(
                config: new NoctuaIAPService.Config { BaseUrl = BaseUrl, ClientId = "test-client-id" },
                accessTokenProvider: new AccessTokenProvider(new StubAccountEvents()),
                paymentUI:           null,
                nativePlugin:        null,
                authProvider:        new StubAuthProvider(),
                localeProvider:      new StubLocale()
            );
            service.Enable();

            return service;
        }

        private int ProductRequestCount =>
            _server.Requests.Count(r => r.Path.EndsWith(ProductsPath, StringComparison.Ordinal));

        private void SimulateServerDown()
        {
            // No handler → HttpMockServer answers 404 → HttpRequest throws.
            _server.RemoveHandler(ProductsPath);
            LogAssert.ignoreFailingMessages = true;
        }

        // ── Tests ─────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator GetProductListAsync_CalledTwice_FetchesRemoteOnce() =>
            UniTask.ToCoroutine(async () =>
            {
                var service = CreateService();

                var first  = await service.GetProductListAsync();
                var second = await service.GetProductListAsync();

                Assert.AreEqual(1, ProductRequestCount, "Second call must be served from cache");
                Assert.AreEqual("pack1", first[0].Id);
                Assert.AreEqual("pack1", second[0].Id);
            });

        [UnityTest]
        public IEnumerator GetProductListAsync_AfterClearProductListCache_FetchesRemoteAgain() =>
            UniTask.ToCoroutine(async () =>
            {
                var service = CreateService();

                await service.GetProductListAsync();
                service.ClearProductListCache(); // what SDK init does
                await service.GetProductListAsync();

                Assert.AreEqual(2, ProductRequestCount, "Call after clear must hit remote");
            });

        [UnityTest]
        public IEnumerator GetProductListAsync_ForceRefresh_AlwaysFetchesRemote() =>
            UniTask.ToCoroutine(async () =>
            {
                var service = CreateService();

                await service.GetProductListAsync();
                await service.GetProductListAsync(forceRefresh: true);

                Assert.AreEqual(2, ProductRequestCount, "forceRefresh must bypass the cache");
            });

        [UnityTest]
        public IEnumerator GetProductListAsync_DifferentCurrency_CachedSeparately() =>
            UniTask.ToCoroutine(async () =>
            {
                var service = CreateService();

                await service.GetProductListAsync(currency: "USD");
                await service.GetProductListAsync(currency: "IDR");
                await service.GetProductListAsync(currency: "USD");
                await service.GetProductListAsync(currency: "IDR");

                Assert.AreEqual(2, ProductRequestCount, "One remote fetch per currency");
            });

        [UnityTest]
        public IEnumerator GetProductListAsync_ConcurrentFirstCalls_FetchRemoteOnce() =>
            UniTask.ToCoroutine(async () =>
            {
                var service = CreateService();

                await UniTask.WhenAll(service.GetProductListAsync(), service.GetProductListAsync());

                Assert.AreEqual(1, ProductRequestCount, "Concurrent first calls must share one fetch");
            });

        [UnityTest]
        public IEnumerator GetProductListAsync_RemoteFailsAfterClear_ReturnsPersistedFallback() =>
            UniTask.ToCoroutine(async () =>
            {
                var service = CreateService();
                await service.GetProductListAsync();

                service.ClearProductListCache();
                SimulateServerDown();

                var fallback = await service.GetProductListAsync();

                Assert.AreEqual(1, fallback.Count);
                Assert.AreEqual("pack1", fallback[0].Id);
            });

        [UnityTest]
        public IEnumerator GetProductListAsync_PersistedCache_SurvivesNewServiceInstance() =>
            UniTask.ToCoroutine(async () =>
            {
                await CreateService().GetProductListAsync();

                SimulateServerDown();
                var fallback = await CreateService().GetProductListAsync();

                Assert.AreEqual("pack1", fallback[0].Id, "Cold start offline must use persisted list");
            });

        [UnityTest]
        public IEnumerator GetProductListAsync_RemoteFailsWithoutFallback_Throws() =>
            UniTask.ToCoroutine(async () =>
            {
                var service = CreateService();
                SimulateServerDown();

                Exception caught = null;
                try
                {
                    await service.GetProductListAsync();
                }
                catch (Exception e)
                {
                    caught = e;
                }

                Assert.IsNotNull(caught, "Must rethrow when there is nothing cached");
            });

        [UnityTest]
        public IEnumerator GetProductListAsync_MutatingReturnedList_DoesNotAffectCache() =>
            UniTask.ToCoroutine(async () =>
            {
                var service = CreateService();

                var first = await service.GetProductListAsync();
                first.Clear();
                var second = await service.GetProductListAsync();

                Assert.AreEqual(1, second.Count, "Callers must receive a copy, not the cached list");
            });

        [UnityTest]
        public IEnumerator GetProductListAsync_CorruptPersistedCache_RethrowsFetchError() =>
            UniTask.ToCoroutine(async () =>
            {
                PlayerPrefs.SetString(CachePrefsKey, "{not valid json");
                PlayerPrefs.Save();

                var service = CreateService();
                SimulateServerDown();

                Exception caught = null;
                try
                {
                    await service.GetProductListAsync();
                }
                catch (Exception e)
                {
                    caught = e;
                }

                Assert.IsInstanceOf<NoctuaException>(caught,
                    "Corrupt cache must be ignored; the original fetch error is rethrown");
            });

        [UnityTest]
        public IEnumerator GetProductListAsync_AfterCorruptCache_RemoteSuccessOverwritesIt() =>
            UniTask.ToCoroutine(async () =>
            {
                PlayerPrefs.SetString(CachePrefsKey, "{not valid json");
                PlayerPrefs.Save();

                var products = await CreateService().GetProductListAsync();

                Assert.AreEqual("pack1", products[0].Id);
                StringAssert.Contains("pack1", PlayerPrefs.GetString(CachePrefsKey));
            });

        // ── Items set in SKU Management, and filtering by product id ────────────

        // pack1 delivers items, pack2 has none, pack3 comes from a backend that sends null.
        private const string ProductsWithItemsJson =
            "{\"data\":[" +
            "{\"id\":\"pack1\",\"price\":0.99,\"currency\":\"USD\",\"items\":{\"coin\":100,\"skin\":1}}," +
            "{\"id\":\"pack2\",\"price\":1.99,\"currency\":\"USD\",\"items\":{}}," +
            "{\"id\":\"pack3\",\"price\":2.99,\"currency\":\"USD\",\"items\":null}]}";

        private NoctuaIAPService CreateServiceWithItems()
        {
            _server.RemoveHandler(ProductsPath);
            _server.AddHandler(ProductsPath, _ => ProductsWithItemsJson);
            return CreateService();
        }

        [UnityTest]
        public IEnumerator GetProductListAsync_ReadsItems_EmptyWhenNoneOrOlderBackend() =>
            UniTask.ToCoroutine(async () =>
            {
                var products = await CreateServiceWithItems().GetProductListAsync();

                CollectionAssert.AreEquivalent(new Dictionary<string, long> { { "coin", 100 }, { "skin", 1 } },
                    products.Single(p => p.Id == "pack1").Items);
                Assert.IsEmpty(products.Single(p => p.Id == "pack2").Items);
                Assert.IsNotNull(products.Single(p => p.Id == "pack3").Items, "null from the backend reads as no items");
                Assert.IsEmpty(products.Single(p => p.Id == "pack3").Items);
            });

        [UnityTest]
        public IEnumerator GetProductListAsync_ProductId_FiltersWithoutAnotherRequest() =>
            UniTask.ToCoroutine(async () =>
            {
                var service = CreateServiceWithItems();

                var all = await service.GetProductListAsync();
                var one = await service.GetProductListAsync(productId: "pack1");
                var noItems = await service.GetProductListAsync(productId: "pack2");
                var none = await service.GetProductListAsync(productId: "unknown");

                Assert.AreEqual(3, all.Count, "no filter: the whole list");
                Assert.AreEqual("pack1", one.Single().Id);
                Assert.AreEqual(100, one.Single().Items["coin"]);
                Assert.IsEmpty(noItems.Single().Items, "a product without items is still returned");
                Assert.IsEmpty(none, "an unknown product: empty list");
                Assert.AreEqual(1, ProductRequestCount, "filtering reuses the cached list");

                var again = await service.GetProductListAsync();
                Assert.AreEqual(3, again.Count, "a filtered call never narrows the cache");
            });

        [UnityTest]
        public IEnumerator GetProductListAsync_ProductId_FirstCallFetchesOnce() =>
            UniTask.ToCoroutine(async () =>
            {
                var service = CreateServiceWithItems();

                var results = await UniTask.WhenAll(
                    service.GetProductListAsync(productId: "pack1"),
                    service.GetProductListAsync(productId: "pack2"),
                    service.GetProductListAsync());

                Assert.AreEqual(1, ProductRequestCount, "concurrent filtered calls share one fetch");
                Assert.AreEqual("pack1", results.Item1.Single().Id);
                Assert.AreEqual("pack2", results.Item2.Single().Id);
                Assert.AreEqual(3, results.Item3.Count);
            });

        [UnityTest]
        public IEnumerator GetProductListAsync_Offline_KeepsItemsFromTheDeviceCopy() =>
            UniTask.ToCoroutine(async () =>
            {
                await CreateServiceWithItems().GetProductListAsync();   // persisted on the device

                SimulateServerDown();
                var offline = CreateService();                           // a new session, cache empty
                var product = (await offline.GetProductListAsync(productId: "pack1")).Single();

                Assert.AreEqual(1, product.Items["skin"]);
            });
    }
}
