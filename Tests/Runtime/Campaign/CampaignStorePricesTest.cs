using System;
using System.Collections.Generic;
using com.noctuagames.sdk.LiveOpsCampaign;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime.Campaign
{
    /// <summary>
    /// Local store prices on <see cref="NoctuaLiveOpsCampaign"/>: the pure price swap
    /// (<see cref="NoctuaLiveOpsCampaign.ApplyStorePrices"/>) and the fetch / cache / retry rules.
    /// </summary>
    [TestFixture]
    public class CampaignStorePricesTest
    {
        private readonly List<NoctuaLiveOpsCampaign> _created = new List<NoctuaLiveOpsCampaign>();

        [TearDown]
        public void TearDown()
        {
            // Each facade parks an empty UI root in DontDestroyOnLoad.
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go.name == "NoctuaLiveOpsCampaignUI") UnityEngine.Object.DestroyImmediate(go);
            }
            _created.Clear();
        }

        private static CampaignItem Item(Dictionary<string, string> data) =>
            new CampaignItem { Id = "pass", Data = data };

        private NoctuaLiveOpsCampaign Campaign(Func<UniTask<IReadOnlyDictionary<string, string>>> fetch)
        {
            var campaign = new NoctuaLiveOpsCampaign(
                new CampaignConfig(), null, null, new MockEventSender(),
                () => Array.Empty<string>(), fetch);
            _created.Add(campaign);
            return campaign;
        }

        private NoctuaLiveOpsCampaign With(Dictionary<string, string> prices, List<int> calls = null) =>
            Campaign(() =>
            {
                calls?.Add(1);
                return UniTask.FromResult<IReadOnlyDictionary<string, string>>(prices);
            });

        private static bool Fetch(NoctuaLiveOpsCampaign campaign) =>
            campaign.PrefetchStorePricesAsync().GetAwaiter().GetResult();

        // ---- the pure swap ------------------------------------------------

        [Test]
        public void Apply_WithoutPrices_ReturnsTheItemUnchanged()
        {
            var item = Item(new Dictionary<string, string> { { "store_price.gold", "$9.99" } });
            Assert.AreSame(item, NoctuaLiveOpsCampaign.ApplyStorePrices(item, null));
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
            var applied = NoctuaLiveOpsCampaign.ApplyStorePrices(item,
                new Dictionary<string, string> { { "gold", "Rp161.000" }, { "other", "Rp1" } });

            Assert.AreEqual("Rp161.000", applied.Data["store_price.gold"]);
            Assert.AreEqual("$4.99", applied.Data["store_price.normal"]);
            Assert.AreEqual("Monthly Pass", applied.Data["title"]);
            Assert.IsFalse(applied.Data.ContainsKey("store_price.other"), "never adds keys the campaign doesn't ship");
            Assert.AreEqual("$9.99", item.Data["store_price.gold"], "the stored item is untouched");
        }

        [Test]
        public void EmptyLocalPrice_KeepsTheFallback()
        {
            var item = Item(new Dictionary<string, string> { { "store_price.gold", "$9.99" } });
            var applied = NoctuaLiveOpsCampaign.ApplyStorePrices(item, new Dictionary<string, string> { { "gold", "" } });
            Assert.AreEqual("$9.99", applied.Data["store_price.gold"]);
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
            Assert.IsTrue(NoctuaLiveOpsCampaign.UsesStorePrices(item));

            var applied = NoctuaLiveOpsCampaign.ApplyStorePrices(item,
                new Dictionary<string, string> { { "gold", "Rp16.000" } });

            Assert.AreEqual("Rp16.000", applied.Data["price"]);
            Assert.AreEqual("gold", applied.Data["product_id"]);
            Assert.AreEqual("$0.99", item.Data["price"], "the stored item is untouched");
        }

        [Test]
        public void PriceWithoutProductId_IsLeftAlone()
        {
            var item = Item(new Dictionary<string, string> { { "price", "$0.99" } });
            Assert.IsFalse(NoctuaLiveOpsCampaign.UsesStorePrices(item));
            Assert.AreSame(item, NoctuaLiveOpsCampaign.ApplyStorePrices(item,
                new Dictionary<string, string> { { "gold", "Rp16.000" } }));
        }

        [Test]
        public void Uses_OnlyWhenTheCampaignShipsAPrice()
        {
            Assert.IsFalse(NoctuaLiveOpsCampaign.UsesStorePrices(Item(new Dictionary<string, string> { { "title", "x" } })));
            Assert.IsTrue(NoctuaLiveOpsCampaign.UsesStorePrices(Item(new Dictionary<string, string> { { "store_price.a", "$1" } })));
        }

        // ---- fetch / cache / retry ---------------------------------------

        [Test]
        public void FetchesOnce_ThenServesTheCache()
        {
            var calls = new List<int>();
            var campaign = With(new Dictionary<string, string> { { "gold", "Rp1" } }, calls);

            Assert.IsTrue(Fetch(campaign));
            Assert.IsFalse(Fetch(campaign), "nothing new the second time");
            Assert.AreEqual(1, calls.Count);
            Assert.AreEqual("Rp1", campaign.StorePrices["gold"]);
        }

        [Test]
        public void FailedFetch_IsRetriedNextTime()
        {
            var attempts = 0;
            var campaign = Campaign(() =>
            {
                attempts++;
                if (attempts == 1) throw new Exception("Game ID not found. Please authenticate first");
                return UniTask.FromResult<IReadOnlyDictionary<string, string>>(
                    new Dictionary<string, string> { { "gold", "Rp1" } });
            });

            Assert.IsFalse(Fetch(campaign));
            Assert.IsNull(campaign.StorePrices);
            Assert.IsTrue(Fetch(campaign));
            Assert.AreEqual(2, attempts);
        }

        [Test]
        public void Clear_DropsPrices_AndDiscardsAFetchThatWasInFlight()
        {
            var gate = new UniTaskCompletionSource<IReadOnlyDictionary<string, string>>();
            var campaign = Campaign(() => gate.Task);

            var pending = campaign.PrefetchStorePricesAsync();
            campaign.ClearStorePrices(); // account switched while the old player's prices were loading
            gate.TrySetResult(new Dictionary<string, string> { { "gold", "Rp1" } });

            Assert.IsFalse(pending.GetAwaiter().GetResult());
            Assert.IsNull(campaign.StorePrices);
        }

        [Test]
        public void NoFetch_MeansFallbacksOnly()
        {
            Assert.IsFalse(Fetch(Campaign(null)));
        }
    }
}
