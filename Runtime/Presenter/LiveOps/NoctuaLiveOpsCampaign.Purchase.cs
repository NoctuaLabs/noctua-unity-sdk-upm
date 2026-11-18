using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// Campaign purchases beyond "buy this SKU":
    ///
    /// <para><b>One purchase per campaign</b> (<see cref="CampaignItem.PurchaseOnce"/>): after a
    /// completed purchase from the campaign, the SDK claims the tracker key
    /// <c>&lt;campaign id&gt;.purchase</c>. The tracker allows a claim only once, so the record is
    /// server-side and survives reinstalls. A bought campaign is no longer shown, and its Buy
    /// buttons show as purchased. While the tracker is down the purchase waits on disk and still
    /// counts as bought on this device.</para>
    /// </summary>
    public sealed partial class NoctuaLiveOpsCampaign
    {
        /// <summary>Suffix of a <c>purchase_once</c> campaign's tracker key.</summary>
        public const string PurchaseKeySuffix = ".purchase";

        /// <summary>Order <c>extra</c> key naming the campaign a purchase came from.</summary>
        public const string ExtraCampaignId = "campaign_id";

        /// <summary>How old loaded progress may be before a <c>purchase_once</c> campaign re-checks it.</summary>
        public static readonly TimeSpan PurchaseStateMaxAge = TimeSpan.FromMinutes(5);

        private const string AlreadyPurchasedFallback = "You already bought this offer.";

        /// <summary>Fires with a campaign id once its one-time purchase is recorded on the tracker.</summary>
        public event Action<string> OnCampaignPurchaseRecorded;

        // Purchase keys being claimed right now, so a flush never claims one twice at once.
        private readonly HashSet<string> _claimingPurchaseKeys = new(StringComparer.Ordinal);
        // Last purchase-state load attempt, so a tracker that is down is not waited on for every popup.
        private DateTime? _purchaseStateAttemptUtc;

        /// <summary>After a failed purchase-state load, how long popups use what is known instead of asking again.</summary>
        public static readonly TimeSpan PurchaseStateRetryBackoff = TimeSpan.FromMinutes(1);

        // ── One purchase per campaign ─────────────────────────────────────

        /// <summary>
        /// The tracker key recording that <paramref name="item"/> was bought:
        /// <c>&lt;progress_scope&gt;.purchase</c>. Remote campaigns carry the campaign id as
        /// <see cref="CampaignItem.ProgressScope"/> — their <c>id</c> is name-based, and the key
        /// is registered under the campaign id. Without a scope (bundled campaigns) it is
        /// <c>&lt;id&gt;.purchase</c>.
        /// </summary>
        public static string PurchaseKey(CampaignItem item) =>
            (string.IsNullOrEmpty(item?.ProgressScope) ? item?.Id ?? string.Empty : item.ProgressScope)
            + PurchaseKeySuffix;

        /// <summary>
        /// True when <paramref name="item"/> is a <c>purchase_once</c> campaign the logged-in player
        /// already bought — claimed on the tracker, or bought while it was down. Uses the loaded
        /// progress; see <see cref="IsCampaignPurchasedAsync"/> to load it first.
        /// </summary>
        public bool IsCampaignPurchased(CampaignItem item)
        {
            if (item == null || !item.PurchaseOnce || !IsProgressConfigured) return false;
            EnsureProgressPlayer();

            var key = PurchaseKey(item);
            if (_pendingPurchaseClaims.Contains(key)) return true;
            return _serverProgress.TryGetValue(key, out var row) && row.Claimed;
        }

        /// <summary><see cref="IsCampaignPurchased"/>, after loading progress when it is not fresh.</summary>
        public async UniTask<bool> IsCampaignPurchasedAsync(string campaignId)
        {
            var item = FindCampaign(campaignId);
            if (item == null || !item.PurchaseOnce) return false;

            await EnsurePurchaseStateAsync(item);
            return IsCampaignPurchased(item);
        }

        /// <summary>
        /// Records that the player bought <c>purchase_once</c> campaign <paramref name="campaignId"/>.
        /// The SDK does this itself when a purchase started from the campaign's Buy button
        /// completes; call it when the game sells the offer some other way. The campaign counts as
        /// bought at once; when the tracker is down the claim is retried later. Returns false when
        /// the campaign is not <c>purchase_once</c> or nobody is logged in. Never throws.
        /// </summary>
        public async UniTask<bool> RecordCampaignPurchaseAsync(string campaignId, long orderId = 0)
        {
            try
            {
                var item = FindCampaign(campaignId);
                if (item == null || !item.PurchaseOnce) return false;
                if (TryProgressCaller() == null)
                {
                    _log.Warning($"{LogTag} purchase of '{campaignId}' not recorded: nobody is logged in");
                    return false;
                }

                EnsureProgressPlayer();
                var key = PurchaseKey(item);
                _pendingPurchaseClaims.Add(key);
                SaveProgress();
                RefreshShownCampaign(item.Id);

                await ClaimPurchaseKeyAsync(key, orderId);
                return true;
            }
            catch (Exception e)
            {
                _log.Warning($"{LogTag} recording purchase of '{campaignId}' failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sets the purchase key to 1 and claims it. True once the tracker holds the claim (also
        /// when it already did). A tracker that is down or a logged-out player keeps the purchase
        /// waiting; a refused key (not set up for the game) is dropped and logged.
        /// </summary>
        private async UniTask<bool> ClaimPurchaseKeyAsync(string key, long orderId)
        {
            if (!_claimingPurchaseKeys.Add(key)) return false;

            try
            {
                await SetProgressAsync(key, 1);
                await ClaimProgressAsync(key);
                SettlePurchaseClaim(key);
                SafeInvoke(OnCampaignPurchaseRecorded, CampaignIdOf(key));
                return true;
            }
            catch (NoctuaException e) when (e.ErrorCode == (int)NoctuaErrorCode.LiveOpsProgressAlreadyClaimed)
            {
                _log.Warning($"{LogTag} '{key}' was already claimed — a second purchase of a purchase_once campaign" +
                             (orderId > 0 ? $" (order {orderId})" : string.Empty));
                SettlePurchaseClaim(key);
                SafeInvoke(OnCampaignPurchaseRecorded, CampaignIdOf(key));
                return true;
            }
            catch (Exception e) when (Classify(e) != ProgressFailure.Refused)
            {
                _log.Debug($"{LogTag} purchase claim '{key}' waits for the tracker: {e.Message}");
                ScheduleProgressRetry();
                return false;
            }
            catch (Exception e)
            {
                _log.Error($"{LogTag} purchase claim '{key}' refused by the tracker, so the campaign can be " +
                           $"bought again — is the key set up for this game? {e.Message}");
                SettlePurchaseClaim(key);
                RefreshShownCampaign(CampaignIdOf(key));
                return false;
            }
            finally
            {
                _claimingPurchaseKeys.Remove(key);
            }
        }

        /// <summary>The id of the campaign whose purchase key is <paramref name="purchaseKey"/>, or null.</summary>
        private string CampaignIdOf(string purchaseKey) =>
            _resolver.Config?.Campaigns?.FirstOrDefault(c => c != null && c.PurchaseOnce && PurchaseKey(c) == purchaseKey)?.Id;

        private void SettlePurchaseClaim(string key)
        {
            if (!_pendingPurchaseClaims.Remove(key)) return;
            SaveProgress();
        }

        /// <summary>Claims purchases bought while the tracker was down; stops at the first that still waits.</summary>
        private async UniTask RetryPurchaseClaimsAsync()
        {
            foreach (var key in _pendingPurchaseClaims.ToList())
            {
                // Being claimed by another call right now: that call settles it.
                if (_claimingPurchaseKeys.Contains(key)) continue;
                if (!await ClaimPurchaseKeyAsync(key, 0) && _pendingPurchaseClaims.Contains(key)) break;
            }
        }

        /// <summary>Loads progress for a <c>purchase_once</c> campaign when it is missing or old. Never throws.</summary>
        private async UniTask EnsurePurchaseStateAsync(CampaignItem item)
        {
            if (!NeedsPurchaseStateCheck(item)) return;

            _purchaseStateAttemptUtc = _utcNow();
            try { await GetProgressAsync(); }
            catch (Exception e) { _log.Debug($"{LogTag} purchase state for '{item.Id}' unavailable: {e.Message}"); }
        }

        private bool NeedsPurchaseStateCheck(CampaignItem item)
        {
            if (item == null || !item.PurchaseOnce || TryProgressCaller() == null) return false;
            EnsureProgressPlayer();
            var now = _utcNow();
            if (_purchaseStateAttemptUtc.HasValue && now - _purchaseStateAttemptUtc.Value < PurchaseStateRetryBackoff) return false;
            return !_progressFetchedAtUtc.HasValue || now - _progressFetchedAtUtc.Value > PurchaseStateMaxAge;
        }

        /// <summary>Ownership to render <paramref name="item"/> with: every SKU owned once the campaign was bought.</summary>
        private IReadOnlyDictionary<string, bool> OwnershipFor(CampaignItem item)
        {
            if (!IsCampaignPurchased(item)) return _ownership;

            var owned = new Dictionary<string, bool>(_ownership, StringComparer.Ordinal);
            foreach (var sku in PurchaseSkus(item)) owned[sku] = true;
            return owned;
        }

        /// <summary>
        /// Records a <c>purchase_once</c> campaign as bought from its verified order, which names the
        /// campaign in <c>extra</c> (<see cref="CampaignOrderExtra"/>). Runs for every verified order,
        /// so one that came back pending and was verified later (the pending-purchase retry, maybe on
        /// a later launch) still locks its campaign. Only for <paramref name="buyerId"/> when it is
        /// the player logged in now (unknown counts as them). True once recorded. Never throws.
        /// </summary>
        public async UniTask<bool> RecordOrderPurchaseAsync(OrderRequest order, long? buyerId)
        {
            try
            {
                if (order?.Extra == null || !order.Extra.TryGetValue(ExtraCampaignId, out var campaignId)) return false;

                var item = FindCampaign(campaignId);
                if (item == null || !item.PurchaseOnce || IsCampaignPurchased(item)) return false;

                EnsureProgressPlayer();
                if (buyerId is > 0 && buyerId != _progressPlayer)
                {
                    _log.Warning($"{LogTag} order {order.Id} of '{campaignId}' was paid by player {buyerId}, " +
                                 $"not the one logged in now; the campaign is not recorded as bought for them");
                    return false;
                }

                return await RecordCampaignPurchaseAsync(campaignId, order.Id);
            }
            catch (Exception e)
            {
                _log.Warning($"{LogTag} recording the campaign of order {order?.Id} failed: {e.Message}");
                return false;
            }
        }

        private void OnPurchaseBlocked(CampaignItem item)
        {
            var message = _locale?.GetTranslation(LocaleTextKey.LiveOpsCampaignAlreadyPurchased);
            if (string.IsNullOrEmpty(message) || message == nameof(LocaleTextKey.LiveOpsCampaignAlreadyPurchased))
            {
                message = AlreadyPurchasedFallback;
            }

            try { _showNotice?.Invoke(message); }
            catch (Exception e) { _log.Warning($"{LogTag} notice failed: {e.Message}"); }

            RefreshShownCampaign(item?.Id);
        }

        /// <summary>Re-renders the open popup when it shows <paramref name="campaignId"/>.</summary>
        private void RefreshShownCampaign(string campaignId)
        {
            var popup = _ui;
            if (_shownItem == null || _shownItem.Id != campaignId || popup == null || !popup.IsShowing) return;

            popup.Refresh(RenderItem(_shownItem, _shownPlayerData), _resolver.Config.SchemaVersion);
        }

        private CampaignItem FindCampaign(string campaignId) =>
            string.IsNullOrEmpty(campaignId)
                ? null
                : _resolver.Config?.Campaigns?.FirstOrDefault(c => c != null && c.Id == campaignId);

        // ── Per-store SKUs ────────────────────────────────────────────────

        /// <summary>The key of a <c>product_key</c> entry that names the SKU on this store.</summary>
#if UNITY_IOS
        public const string PlatformSkuKey = "sku_ios";
#else
        public const string PlatformSkuKey = "sku_android";
#endif

        /// <summary>
        /// The SKU a <c>product_key</c> button sells on this store. <c>data[productKey]</c> is the
        /// JSON object <c>{"sku_android": "...", "sku_ios": "..."}</c>; null when it is missing,
        /// not that object, or names no SKU for this store.
        /// </summary>
        public static string StoreSku(CampaignItem item, string productKey)
        {
            if (item?.Data == null || string.IsNullOrWhiteSpace(productKey)) return null;
            if (!item.Data.TryGetValue(productKey.Trim(), out var raw) || string.IsNullOrWhiteSpace(raw)) return null;

            try
            {
                var value = (JToken.Parse(raw) as JObject)?[PlatformSkuKey];
                var sku = value?.Type == JTokenType.String ? value.Value<string>()?.Trim() : null;
                return string.IsNullOrEmpty(sku) ? null : sku;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// A copy of <paramref name="item"/> whose <c>product_key</c> Buy buttons carry this store's
        /// SKU as their product id, and whose unreadable ones are hidden. The item itself when it
        /// has none; never mutates it.
        /// </summary>
        public CampaignItem WithStoreSkus(CampaignItem item)
        {
            if (item?.View == null) return item;

            var view = ResolveStoreSkus(item.View, item, depth: 0);
            if (ReferenceEquals(view, item.View)) return item;

            var copy = item.ShallowCopy();
            copy.View = view;
            return copy;
        }

        private CampaignNode ResolveStoreSkus(CampaignNode node, CampaignItem item, int depth)
        {
            if (node == null || depth >= MaxSkuSearchDepth) return node;

            List<CampaignNode> children = null;
            if (node.Children != null)
            {
                for (var i = 0; i < node.Children.Count; i++)
                {
                    var child = ResolveStoreSkus(node.Children[i], item, depth + 1);
                    if (ReferenceEquals(child, node.Children[i])) continue;

                    children ??= new List<CampaignNode>(node.Children);
                    children[i] = child;
                }
            }

            var action = node.Action;
            var perStore = action != null
                           && action.Type == CampaignActionType.Purchase
                           && string.IsNullOrWhiteSpace(action.ProductId)
                           && !string.IsNullOrWhiteSpace(action.ProductKey);
            if (!perStore && children == null) return node;

            var copy = node.ShallowCopy();
            if (children != null) copy.Children = children;
            if (!perStore) return copy;

            var sku = StoreSku(item, action.ProductKey);
            if (sku == null)
            {
                // Logged once per campaign and key, so a re-render does not repeat it.
                if (_hiddenStoreSkus.Add(item.Id + "\n" + action.ProductKey))
                {
                    _log.Warning($"{LogTag} Buy button '{action.ProductKey}' in campaign '{item.Id}' is hidden: " +
                                 $"its data entry has no {PlatformSkuKey}.");
                }
                copy.Action = null;
                copy.VisibleIf = new CampaignCondition
                {
                    // Never true: the node is not rendered.
                    All = new List<CampaignConditionClause> { new() { Left = "0", Op = "eq", Right = "1" } },
                };
                return copy;
            }

            copy.Action = new CampaignAction
            {
                TypeRaw = action.TypeRaw,
                Deeplink = action.Deeplink,
                ProductId = sku,
                ProductKey = action.ProductKey,
                KeepOpen = action.KeepOpen,
                Id = action.Id,
            };
            return copy;
        }

        private readonly HashSet<string> _hiddenStoreSkus = new(StringComparer.Ordinal);

        /// <summary>The SKUs <paramref name="item"/> sells on this store.</summary>
        private IReadOnlyList<string> SkusOf(CampaignItem item) => PurchaseSkus(WithStoreSkus(item));

        // ── Order attribution ─────────────────────────────────────────────

        /// <summary>
        /// The order <c>extra</c> for a purchase of <paramref name="sku"/> started from a campaign's
        /// Buy button: its <c>campaign_id</c>. Null for any other purchase. The composition root
        /// hands it to <c>Noctua.IAP</c>, which adds it under the game's own keys.
        /// </summary>
        public IReadOnlyDictionary<string, string> CampaignOrderExtra(string sku)
        {
            try
            {
                var item = FindCampaign(_dispatcher.RecentPurchaseCampaign(sku));
                return item == null ? null : new Dictionary<string, string> { { ExtraCampaignId, item.Id ?? string.Empty } };
            }
            catch (Exception e)
            {
                _log.Warning($"{LogTag} campaign order extra for '{sku}' failed: {e.Message}");
                return null;
            }
        }
    }
}
