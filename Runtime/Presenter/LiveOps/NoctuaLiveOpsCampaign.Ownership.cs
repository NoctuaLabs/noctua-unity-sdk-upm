using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using com.noctuagames.sdk.Events;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>Ownership: which purchase SKUs the player already owns, hiding fully owned offers and showing owned Buy buttons as Purchased.</summary>
    public sealed partial class NoctuaLiveOpsCampaign
    {
        // ── Ownership ─────────────────────────────────────────────────────

        /// <summary>
        /// Replaces the "does the player own this SKU?" check — by default the store's
        /// (<c>Noctua.IAP.GetPurchaseStatusAsync</c>), which only knows non-consumables and
        /// subscriptions. Supply the game's own record for one-time consumable offers. Pass
        /// <c>null</c> to stop ownership from hiding campaigns. Clears cached answers.
        /// </summary>
        public void SetOwnershipProvider(Func<string, UniTask<bool>> isOwned)
        {
            _isProductOwned = isOwned;
            // The game's own record may count a consumable as owned (a one-time offer), so the
            // product type no longer stands in for its answer.
            _ownershipIsStoreCheck = false;
            ClearOwnership();
        }

        /// <summary>Known ownership per SKU (unknown SKUs are absent) — read by the Inspector.</summary>
        public IReadOnlyDictionary<string, bool> Ownership => _ownership;

        /// <summary>
        /// Forgets every ownership answer — the composition root calls this when the account
        /// changes, since what is owned is per player.
        /// </summary>
        public void ClearOwnership()
        {
            _log.Debug($"{LogTag} ownership cleared (was: {DescribeOwnership()})");
            _ownership.Clear();
            _ownershipGeneration++;

            // A popup can already be open — auto_show runs before the first login finishes, and
            // that login lands here. Without asking again its owned cards fall back to "Buy" at
            // the next re-render (a purchase, a price update), though nothing changed in the store.
            if (_shownItem != null) RecheckShownOwnershipAsync().Forget();
        }

        /// <summary>
        /// Re-asks ownership for the campaign on screen after the cache was cleared, then
        /// re-renders it so its cards reflect the current player and provider.
        /// </summary>
        private async UniTaskVoid RecheckShownOwnershipAsync()
        {
            try
            {
                var shown = _shownItem;
                if (shown == null || !await PrefetchOwnershipAsync(shown)) return;

                // Closed, or replaced by another campaign, while the store answered.
                if (!ReferenceEquals(_shownItem, shown)) return;
                var popup = _host.PopupIfCreated;
                if (popup == null || !popup.IsShowing) return;

                popup.Refresh(RenderItem(shown, _shownPlayerData), _manager.Config.SchemaVersion);
            }
            catch (Exception e)
            {
                _log.Warning($"{LogTag} ownership re-check after clear failed: {e.Message}");
            }
        }

        private string DescribeOwnership() =>
            _ownership.Count == 0 ? "none" : string.Join(", ", _ownership.Select(p => p.Key + "=" + p.Value));

        /// <summary>
        /// The SKUs <paramref name="item"/>'s Buy buttons purchase, in tree order, without
        /// duplicates; <c>{{token}}</c> product ids are resolved against its <c>data</c>.
        /// </summary>
        public static IReadOnlyList<string> PurchaseSkus(CampaignItem item)
        {
            var skus = new List<string>();
            if (item?.View != null) CollectPurchaseSkus(item.View, item.Data, skus, depth: 0);
            return skus;
        }

        private const int MaxSkuSearchDepth = 40;

        private static void CollectPurchaseSkus(
            CampaignNode node, IReadOnlyDictionary<string, string> data, List<string> skus, int depth)
        {
            if (node == null || depth >= MaxSkuSearchDepth) return;

            if (node.Action != null && node.Action.Type == CampaignActionType.Purchase)
            {
                var sku = CampaignTokens.Resolve(node.Action.ProductId, data)?.Trim();
                if (!string.IsNullOrEmpty(sku) && !skus.Contains(sku)) skus.Add(sku);
            }

            if (node.Children == null) return;
            foreach (var child in node.Children) CollectPurchaseSkus(child, data, skus, depth + 1);
        }

        /// <summary>
        /// True when <paramref name="item"/> sells something and the player is known to own all
        /// of it — or it is a <c>purchase_once</c> campaign already bought. Unknown SKUs count as
        /// not owned, so a failed check never hides an offer.
        /// </summary>
        public bool IsFullyOwned(CampaignItem item) =>
            IsCampaignPurchased(item) || IsFullyOwned(WithDynamicProducts(item), _ownership);

        public static bool IsFullyOwned(CampaignItem item, IReadOnlyDictionary<string, bool> ownership)
        {
            if (ownership == null || ownership.Count == 0) return false;

            var skus = PurchaseSkus(item);
            if (skus.Count == 0) return false;
            foreach (var sku in skus)
            {
                if (!ownership.TryGetValue(sku, out var owned) || !owned) return false;
            }
            return true;
        }

        /// <summary>
        /// A copy of <paramref name="item"/> whose <c>owned.&lt;sku&gt;</c> keys read <c>"1"</c> or
        /// <c>"0"</c> wherever ownership is known. Like <see cref="ApplyStorePrices"/>: keys the
        /// campaign does not ship are never added, unknown SKUs keep the shipped default, and the
        /// stored item is never mutated.
        /// </summary>
        public static CampaignItem ApplyOwnership(CampaignItem item, IReadOnlyDictionary<string, bool> ownership)
        {
            if (item?.Data == null || ownership == null || ownership.Count == 0) return item;

            Dictionary<string, string> data = null;
            foreach (var pair in item.Data)
            {
                if (!pair.Key.StartsWith(OwnedPrefix, StringComparison.Ordinal)) continue;
                if (!ownership.TryGetValue(pair.Key.Substring(OwnedPrefix.Length), out var owned)) continue;

                var value = owned ? "1" : "0";
                if (value == pair.Value) continue;

                data ??= new Dictionary<string, string>(item.Data);
                data[pair.Key] = value;
            }

            if (data == null) return item;

            var copy = item.ShallowCopy();
            copy.Data = data;
            return copy;
        }

        /// <summary>
        /// A copy of <paramref name="item"/> in which every Buy button for an owned SKU is shown as
        /// purchased: no action, the label from <c>data.purchased_label</c> (else
        /// <paramref name="defaultLabel"/>) and a grey background. A SKU the campaign styles itself
        /// through an <c>owned.&lt;sku&gt;</c> key is left alone. Only the nodes on the way to a
        /// changed button are copied; the stored item is never mutated.
        /// </summary>
        public static CampaignItem MarkOwnedButtons(
            CampaignItem item, IReadOnlyDictionary<string, bool> ownership, string defaultLabel)
        {
            if (item?.View == null || ownership == null || ownership.Count == 0) return item;

            var label = item.Data != null && item.Data.TryGetValue(PurchasedLabelKey, out var custom)
                && !string.IsNullOrWhiteSpace(custom)
                    ? custom
                    : (string.IsNullOrWhiteSpace(defaultLabel) ? DefaultPurchasedLabel : defaultLabel);

            var view = MarkOwnedNode(item.View, item.Data, ownership, label, depth: 0);
            if (ReferenceEquals(view, item.View)) return item;

            var copy = item.ShallowCopy();
            copy.View = view;
            return copy;
        }

        private static CampaignNode MarkOwnedNode(
            CampaignNode node, IReadOnlyDictionary<string, string> data,
            IReadOnlyDictionary<string, bool> ownership, string label, int depth)
        {
            if (node == null || depth >= MaxSkuSearchDepth) return node;

            List<CampaignNode> children = null;
            if (node.Children != null)
            {
                for (var i = 0; i < node.Children.Count; i++)
                {
                    var child = MarkOwnedNode(node.Children[i], data, ownership, label, depth + 1);
                    if (ReferenceEquals(child, node.Children[i])) continue;

                    children ??= new List<CampaignNode>(node.Children);
                    children[i] = child;
                }
            }

            var owned = IsOwnedPurchase(node.Action, data, ownership);
            if (!owned && children == null) return node;

            var copy = node.ShallowCopy();
            if (children != null) copy.Children = children;
            if (owned) ShowAsPurchased(copy, label);
            return copy;
        }

        /// <summary>A purchase action for a SKU known to be owned, that the campaign does not style itself.</summary>
        private static bool IsOwnedPurchase(
            CampaignAction action, IReadOnlyDictionary<string, string> data, IReadOnlyDictionary<string, bool> ownership)
        {
            if (action == null || action.Type != CampaignActionType.Purchase) return false;

            var sku = CampaignTokens.Resolve(action.ProductId, data)?.Trim();
            if (string.IsNullOrEmpty(sku)) return false;
            if (data != null && data.ContainsKey(OwnedPrefix + sku)) return false;
            return ownership.TryGetValue(sku, out var owned) && owned;
        }

        /// <summary>Turns a node copy into the purchased state: untappable, and for a button relabelled and greyed.</summary>
        private static void ShowAsPurchased(CampaignNode copy, string label)
        {
            copy.Action = null;
            if (!string.Equals(copy.Type, CampaignNode.TypeButton, StringComparison.OrdinalIgnoreCase)) return;

            var props = copy.Props == null
                ? new Dictionary<string, object>()
                : new Dictionary<string, object>(copy.Props);
            props.Remove("loc_key");
            props["text"] = label;
            copy.Props = props;

            var style = copy.Style?.ShallowCopy() ?? new CampaignStyleProps();
            style.BackgroundColor = PurchasedButtonColor;
            copy.Style = style;

            // A breakpoint that sets its own background would otherwise bring the Buy colour back.
            if (copy.Responsive == null) return;
            var responsive = new Dictionary<string, CampaignStyleProps>();
            foreach (var pair in copy.Responsive)
            {
                var variant = pair.Value;
                if (variant?.BackgroundColor != null)
                {
                    variant = variant.ShallowCopy();
                    variant.BackgroundColor = PurchasedButtonColor;
                }
                responsive[pair.Key] = variant;
            }
            copy.Responsive = responsive;
        }

        /// <summary>The player-language label for an owned product's button.</summary>
        private string PurchasedLabel()
        {
            var text = _locale?.GetTranslation(LocaleTextKey.LiveOpsCampaignPurchased);
            return string.IsNullOrEmpty(text) || text == nameof(LocaleTextKey.LiveOpsCampaignPurchased)
                ? DefaultPurchasedLabel
                : text;
        }

        private bool NeedsOwnershipCheck(CampaignItem item)
        {
            if (NeedsPurchaseStateCheck(item)) return true;
            if (_isProductOwned == null) return false;
            foreach (var sku in SkusOf(item))
            {
                if (!_ownership.ContainsKey(sku)) return true;
            }
            return false;
        }

        /// <summary>
        /// Checks every SKU of <paramref name="item"/> whose ownership is not known yet, in
        /// parallel. Resolves true when it learned something new. Failures and timeouts stay
        /// unknown and are asked again next time.
        /// </summary>
        public async UniTask<bool> PrefetchOwnershipAsync(CampaignItem item)
        {
            var provider = _isProductOwned;
            if (provider == null) return false;

            var pending = new List<string>();
            foreach (var sku in SkusOf(item))
            {
                if (!_ownership.ContainsKey(sku)) pending.Add(sku);
            }
            if (pending.Count == 0) return false;

            var generation = _ownershipGeneration;
            var answers = await UniTask.WhenAll(pending.Select(sku => CheckOwnedAsync(provider, sku)));
            // An account switch mid-check makes these answers someone else's.
            if (generation != _ownershipGeneration) return false;

            var learned = false;
            for (var i = 0; i < pending.Count; i++)
            {
                if (answers[i] == null) continue;
                _ownership[pending[i]] = answers[i].Value;
                learned = true;
            }
            _log.Debug($"{LogTag} ownership after prefetch for '{item?.Id}': {DescribeOwnership()}");
            return learned;
        }

        /// <summary>
        /// The waits before asking the store again when it says a just-bought SKU is owned: a
        /// consumable drops out once the store finishes it, a non-consumable stays owned.
        /// </summary>
        public static readonly TimeSpan[] OwnedRecheckDelays =
        {
            TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8),
        };

        /// <summary>
        /// Asks again whether <paramref name="productId"/> is owned — after a purchase — and
        /// re-renders the open popup if it sells that SKU, so its card flips in place.
        /// <paramref name="knownOwned"/> is what a verified order's product type says (false for
        /// a consumable, true for a non-consumable or subscription); with the store check it is
        /// taken as the answer, since the store lags behind it. Null falls back to the product
        /// list's type, else asks the provider.
        /// </summary>
        public async UniTask RefreshOwnershipAsync(string productId, bool? knownOwned = null)
        {
            try
            {
                var provider = _isProductOwned;
                if (provider == null || string.IsNullOrEmpty(productId)) return;

                _ownership.Remove(productId);
                var generation = _ownershipGeneration;

                // The order's type is the freshest; the Buy flow's refresh runs without the order.
                var nonConsumable = _ownershipIsStoreCheck
                    ? knownOwned ?? await IsNonConsumableAsync(productId)
                    : null;
                var typeKnown = nonConsumable.HasValue;

                // A consumable is never owned in the store, and a verified non-consumable order is;
                // a non-consumable without an order this time (cancelled, failed) asks the store.
                var owned = typeKnown && (nonConsumable == false || knownOwned == true)
                    ? nonConsumable
                    : await CheckOwnedAsync(provider, productId);

                // Without a known type: the store finishes a consumable after verification
                // (Google Play consumes it on the server, StoreKit finishes it after delivery), so
                // right after a purchase it can still list it. Believe "owned" only once it holds;
                // until then the SKU stays unknown, which shows Buy.
                foreach (var wait in OwnedRecheckDelays)
                {
                    if (typeKnown || owned != true || generation != _ownershipGeneration) break;
                    await _retryDelay(wait);
                    if (generation != _ownershipGeneration) return;
                    owned = await CheckOwnedAsync(provider, productId);
                }
                if (owned == null || generation != _ownershipGeneration) return;
                _ownership[productId] = owned.Value;
                _log.Debug($"{LogTag} ownership after refresh of '{productId}': {DescribeOwnership()}");

                var popup = _host.PopupIfCreated;
                if (_shownItem == null || popup == null || !popup.IsShowing) return;
                if (!SkusOf(_shownItem).Contains(productId)) return;

                popup.Refresh(RenderItem(_shownItem, _shownPlayerData), _manager.Config.SchemaVersion);
            }
            catch (Exception e)
            {
                _log.Warning($"{LogTag} ownership refresh for '{productId}' failed: {e.Message}");
            }
        }

        /// <summary>Checks ownership first; true (and logged) when the player already owns all of it.</summary>
        private async UniTask<bool> IsOwnedAfterCheckAsync(CampaignItem item)
        {
            await EnsurePurchaseStateAsync(item);
            await PrefetchOwnershipAsync(item);
            if (!IsFullyOwned(item)) return false;

            _log.Info($"{LogTag} campaign '{item.Id}' not shown — the player already owns everything it sells");
            return true;
        }

        /// <summary>
        /// One provider call: its answer, or null when it threw or took too long. With the store
        /// check, a SKU the product list calls consumable is not owned without asking the store.
        /// </summary>
        private async UniTask<bool?> CheckOwnedAsync(Func<string, UniTask<bool>> provider, string sku)
        {
            if (_ownershipIsStoreCheck && await IsNonConsumableAsync(sku) == false) return false;

            try
            {
                var check = provider(sku);
                if (check.Status.IsCompleted()) return await check;

                var (timedOut, owned) = await check.TimeoutWithoutException(
                    TimeSpan.FromMilliseconds(OwnershipTimeoutMs), DelayType.Realtime);
                if (!timedOut) return owned;

                _log.Debug($"{LogTag} ownership check for '{sku}' timed out — treated as unknown");
                return null;
            }
            catch (Exception e)
            {
                _log.Debug($"{LogTag} ownership check for '{sku}' failed — treated as unknown: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// The product list's type for <paramref name="sku"/>: true for a non-consumable or
        /// subscription, false for a consumable, null when unknown, unavailable or too slow.
        /// </summary>
        private async UniTask<bool?> IsNonConsumableAsync(string sku)
        {
            if (_isNonConsumable == null) return null;
            try
            {
                var lookup = _isNonConsumable(sku);
                if (lookup.Status.IsCompleted()) return await lookup;

                var (timedOut, nonConsumable) = await lookup.TimeoutWithoutException(
                    TimeSpan.FromMilliseconds(OwnershipTimeoutMs), DelayType.Realtime);
                return timedOut ? null : nonConsumable;
            }
            catch (Exception e)
            {
                _log.Debug($"{LogTag} product type for '{sku}' unavailable — treated as unknown: {e.Message}");
                return null;
            }
        }
    }
}
