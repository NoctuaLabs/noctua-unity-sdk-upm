using System;
using System.Collections.Generic;
using com.noctuagames.sdk.LiveOpsCampaign;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Tests.Runtime.Campaign
{
    [TestFixture]
    public class CampaignStorePricesTest
    {
        private static CampaignItem Item(Dictionary<string, string> data) =>
            new CampaignItem { Id = "pass", Data = data };

        private static CampaignStorePrices With(Dictionary<string, string> prices, List<int> calls = null) =>
            new CampaignStorePrices(() =>
            {
                calls?.Add(1);
                return UniTask.FromResult<IReadOnlyDictionary<string, string>>(prices);
            });

        private static bool Fetch(CampaignStorePrices prices) =>
            prices.EnsureFetchedAsync().GetAwaiter().GetResult();

        [Test]
        public void Apply_BeforeAnyFetch_ReturnsTheItemUnchanged()
        {
            var item = Item(new Dictionary<string, string> { { "store_price.gold", "$9.99" } });
            Assert.AreSame(item, With(new Dictionary<string, string>()).Apply(item));
        }

        [Test]
        public void Apply_OverridesKnownSkus_KeepsFallbackForUnknown_NeverMutates()
        {
            var item = Item(new Dictionary<string, string>
            {
                { "title", "Monthly Pass" },
                { "store_price.gold", "$9.99" },
                { "store_price.normal", "$4.99" },
            });
            var prices = With(new Dictionary<string, string> { { "gold", "Rp161.000" }, { "other", "Rp1" } });
            Assert.IsTrue(Fetch(prices));

            var applied = prices.Apply(item);

            Assert.AreEqual("Rp161.000", applied.Data["store_price.gold"]);
            Assert.AreEqual("$4.99", applied.Data["store_price.normal"]);
            Assert.AreEqual("Monthly Pass", applied.Data["title"]);
            Assert.IsFalse(applied.Data.ContainsKey("store_price.other"), "never adds keys the campaign doesn't ship");
            Assert.AreEqual("$9.99", item.Data["store_price.gold"], "the stored item is untouched");
        }

        [Test]
        public void SingleOfferPrice_FollowsItsProductId()
        {
            var item = Item(new Dictionary<string, string>
            {
                { "price", "$0.99" },
                { "product_id", "gold" },
                { "title", "Starter" },
            });
            Assert.IsTrue(CampaignStorePrices.Uses(item));

            var prices = With(new Dictionary<string, string> { { "gold", "Rp16.000" } });
            Fetch(prices);
            var applied = prices.Apply(item);

            Assert.AreEqual("Rp16.000", applied.Data["price"]);
            Assert.AreEqual("gold", applied.Data["product_id"]);
            Assert.AreEqual("$0.99", item.Data["price"], "the stored item is untouched");
        }

        [Test]
        public void PriceWithoutProductId_IsLeftAlone()
        {
            var item = Item(new Dictionary<string, string> { { "price", "$0.99" } });
            Assert.IsFalse(CampaignStorePrices.Uses(item));

            var prices = With(new Dictionary<string, string> { { "gold", "Rp16.000" } });
            Fetch(prices);
            Assert.AreSame(item, prices.Apply(item));
        }

        [Test]
        public void EmptyLocalPrice_KeepsTheFallback()
        {
            var item = Item(new Dictionary<string, string> { { "store_price.gold", "$9.99" } });
            var prices = With(new Dictionary<string, string> { { "gold", "" } });
            Fetch(prices);
            Assert.AreEqual("$9.99", prices.Apply(item).Data["store_price.gold"]);
        }

        [Test]
        public void FetchesOnce_ThenServesTheCache()
        {
            var calls = new List<int>();
            var prices = With(new Dictionary<string, string> { { "gold", "Rp1" } }, calls);

            Assert.IsTrue(Fetch(prices));
            Assert.IsFalse(Fetch(prices), "nothing new the second time");
            Assert.AreEqual(1, calls.Count);
            Assert.IsTrue(prices.HasPrices);
        }

        [Test]
        public void FailedFetch_IsRetriedNextTime()
        {
            var attempts = 0;
            var prices = new CampaignStorePrices(() =>
            {
                attempts++;
                if (attempts == 1) throw new Exception("Game ID not found. Please authenticate first");
                return UniTask.FromResult<IReadOnlyDictionary<string, string>>(
                    new Dictionary<string, string> { { "gold", "Rp1" } });
            });

            Assert.IsFalse(Fetch(prices));
            Assert.IsFalse(prices.HasPrices);
            Assert.IsTrue(Fetch(prices));
            Assert.AreEqual(2, attempts);
        }

        [Test]
        public void Clear_DropsPrices_AndDiscardsAFetchThatWasInFlight()
        {
            var gate = new UniTaskCompletionSource<IReadOnlyDictionary<string, string>>();
            var prices = new CampaignStorePrices(() => gate.Task);

            var pending = prices.EnsureFetchedAsync();
            prices.Clear(); // account switched while the old player's prices were loading
            gate.TrySetResult(new Dictionary<string, string> { { "gold", "Rp1" } });

            Assert.IsFalse(pending.GetAwaiter().GetResult());
            Assert.IsFalse(prices.HasPrices);
        }

        [Test]
        public void NoFetch_MeansFallbacksOnly()
        {
            var prices = new CampaignStorePrices(null);
            Assert.IsFalse(Fetch(prices));
            Assert.IsFalse(CampaignStorePrices.Uses(Item(new Dictionary<string, string> { { "title", "x" } })));
        }
    }
}
