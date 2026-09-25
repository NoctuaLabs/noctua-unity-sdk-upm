using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// The player's local price for each SKU, for the <c>{{store_price.&lt;sku&gt;}}</c> tokens a
    /// purchase popup ships. The campaign's <c>data</c> already carries a USD fallback under the
    /// same key, so a popup is never blocked on this: it shows the fallback until a fetch lands,
    /// then refreshes in place.
    ///
    /// The single-offer templates (Regular offer, Countdown sale) predate the token: they ship
    /// one <c>price</c> with its <c>product_id</c>, and <c>price</c> gets the same treatment.
    ///
    /// Prices come from an injected fetch (the composition root wires it to the SKU Management
    /// product list, <c>GET /products</c>, in the player's currency). A failed fetch — offline,
    /// not logged in yet, IAP disabled — is retried on the next popup rather than cached.
    /// </summary>
    public sealed class CampaignStorePrices
    {
        /// <summary>Token prefix; the rest of the key is the SKU (dots included).</summary>
        public const string KeyPrefix = "store_price.";

        /// <summary>The single-offer templates' price key, and the SKU key it belongs to.</summary>
        public const string PriceKey = "price";
        public const string ProductIdKey = "product_id";

        private readonly Func<UniTask<IReadOnlyDictionary<string, string>>> _fetch;
        private readonly ILogger _log;

        private IReadOnlyDictionary<string, string> _prices;
        private bool _fetching;
        private int _generation;

        /// <param name="fetch">SKU → display price. Null disables local prices (fallbacks only).</param>
        public CampaignStorePrices(Func<UniTask<IReadOnlyDictionary<string, string>>> fetch, ILogger log = null)
        {
            _fetch = fetch;
            _log = log ?? new NoctuaLogger(typeof(CampaignStorePrices));
        }

        /// <summary>True once a fetch has succeeded (and not been cleared since).</summary>
        public bool HasPrices => _prices != null;

        /// <summary>
        /// Forgets the cached prices — call when the account changes, since currency and the
        /// product list are per player.
        /// </summary>
        public void Clear()
        {
            _prices = null;
            _generation++;
        }

        /// <summary>
        /// A copy of <paramref name="item"/> whose <c>store_price.*</c> data keys carry the local
        /// price wherever one is known. Keys the campaign does not ship are never added, and
        /// SKUs with no local price keep their fallback. Pure: never mutates the stored item.
        /// </summary>
        public CampaignItem Apply(CampaignItem item)
        {
            if (item?.Data == null || _prices == null) return item;

            Dictionary<string, string> data = null;

            void Override(string key, string sku, string current)
            {
                if (string.IsNullOrEmpty(sku)) return;
                if (!_prices.TryGetValue(sku, out var price) || string.IsNullOrEmpty(price)) return;
                if (price == current) return;

                data ??= new Dictionary<string, string>(item.Data);
                data[key] = price;
            }

            foreach (var pair in item.Data)
            {
                if (pair.Key.StartsWith(KeyPrefix, StringComparison.Ordinal))
                {
                    Override(pair.Key, pair.Key.Substring(KeyPrefix.Length), pair.Value);
                }
            }
            if (item.Data.TryGetValue(PriceKey, out var singlePrice)
                && item.Data.TryGetValue(ProductIdKey, out var singleSku))
            {
                Override(PriceKey, singleSku, singlePrice);
            }

            if (data == null) return item;

            var copy = item.ShallowCopy();
            copy.Data = data;
            return copy;
        }

        /// <summary>True when <paramref name="item"/> shows at least one store price.</summary>
        public static bool Uses(CampaignItem item)
        {
            if (item?.Data == null) return false;
            if (item.Data.ContainsKey(PriceKey) && item.Data.TryGetValue(ProductIdKey, out var sku)
                && !string.IsNullOrEmpty(sku))
            {
                return true;
            }
            foreach (var key in item.Data.Keys)
            {
                if (key.StartsWith(KeyPrefix, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>
        /// Fetches prices if none are cached. Resolves true only when this call stored new
        /// prices, so the caller knows an open popup is worth refreshing. Concurrent calls
        /// share nothing: the second returns false straight away.
        /// </summary>
        public async UniTask<bool> EnsureFetchedAsync()
        {
            if (_fetch == null || _prices != null || _fetching) return false;

            _fetching = true;
            var generation = _generation;
            try
            {
                var prices = await _fetch();
                // An account switch mid-fetch makes this answer stale.
                if (prices == null || generation != _generation) return false;

                _prices = prices;
                return true;
            }
            catch (Exception e)
            {
                _log.Debug($"[campaign_prices] local prices unavailable, using fallbacks: {e.Message}");
                return false;
            }
            finally
            {
                _fetching = false;
            }
        }
    }
}
