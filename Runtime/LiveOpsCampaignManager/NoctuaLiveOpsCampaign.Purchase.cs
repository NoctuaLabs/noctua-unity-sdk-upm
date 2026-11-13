using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    ///
    /// <para><b>Dynamic products</b> (<see cref="CampaignAction.ProductKey"/>): the admin ships a
    /// JSON bundle under a <c>data</c> key. The bundle names its SKU (<c>sku_android</c> /
    /// <c>sku_ios</c> / <c>sku</c>), or the game's resolver (<see cref="SetProductResolver"/>) maps
    /// it to one. The order carries the campaign and bundle automatically (<see cref="CampaignOrderExtra"/>).</para>
    /// </summary>
    public sealed partial class NoctuaLiveOpsCampaign
    {
        /// <summary>Suffix of a <c>purchase_once</c> campaign's tracker key.</summary>
        public const string PurchaseKeySuffix = ".purchase";

        /// <summary><see cref="PurchaseExtra"/> keys.</summary>
        public const string ExtraCampaignId = "campaign_id";
        public const string ExtraBundleKey = "campaign_bundle_key";
        public const string ExtraBundle = "campaign_bundle";

        /// <summary>Largest bundle (UTF-8 bytes) a <c>product_key</c> may point at.</summary>
        public const int MaxBundleBytes = 4096;

        /// <summary>How old loaded progress may be before a <c>purchase_once</c> campaign re-checks it.</summary>
        public static readonly TimeSpan PurchaseStateMaxAge = TimeSpan.FromMinutes(5);

        private const string AlreadyPurchasedFallback = "You already bought this offer.";
        private const string BundleKeySeparator = "\n";

        /// <summary>Fires with a campaign id once its one-time purchase is recorded on the tracker.</summary>
        public event Action<string> OnCampaignPurchaseRecorded;

        private Func<CampaignItem, string, JToken, string> _productResolver;
        // "<campaign id>\n<product key>" → parsed bundle; null = missing or invalid (logged once).
        private readonly Dictionary<string, JToken> _bundles = new(StringComparer.Ordinal);
        // "<campaign id>\n<sku>" → the product key the resolver mapped to that SKU.
        private readonly Dictionary<string, string> _bundleKeyBySku = new(StringComparer.Ordinal);
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
            _manager.Config?.Campaigns?.FirstOrDefault(c => c != null && c.PurchaseOnce && PurchaseKey(c) == purchaseKey)?.Id;

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
            var popup = _host?.PopupIfCreated;
            if (_shownItem == null || _shownItem.Id != campaignId || popup == null || !popup.IsShowing) return;

            popup.Refresh(RenderItem(_shownItem, _shownPlayerData), _manager.Config.SchemaVersion);
        }

        private CampaignItem FindCampaign(string campaignId) =>
            string.IsNullOrEmpty(campaignId)
                ? null
                : _manager.Config?.Campaigns?.FirstOrDefault(c => c != null && c.Id == campaignId);

        // ── Dynamic products ──────────────────────────────────────────────

        /// <summary>
        /// Registers how the game turns a dynamic product into a SKU: called with the campaign, the
        /// action's <c>product_key</c> and that key's bundle (a JSON object or array, a copy the
        /// resolver may keep). Return the SKU to sell, or null when the game has nothing for it —
        /// the button is then hidden. Optional: without a resolver the SKU comes from the bundle
        /// itself (<see cref="DefaultBundleSku"/>). Pass <c>null</c> to go back to that.
        /// </summary>
        public void SetProductResolver(Func<CampaignItem, string, JToken, string> resolver)
        {
            _productResolver = resolver;
            _bundleKeyBySku.Clear();
            _hiddenWarned.Clear();
        }

        /// <summary>
        /// The bundle behind <paramref name="productKey"/> in <paramref name="item"/>'s data, or
        /// null when it is missing, not JSON, not an object or array of objects, or over
        /// <see cref="MaxBundleBytes"/>. Returns a copy.
        /// </summary>
        public JToken GetProductBundle(CampaignItem item, string productKey)
        {
            if (item?.Data == null || string.IsNullOrWhiteSpace(productKey)) return null;

            productKey = productKey.Trim();
            var cacheKey = item.Id + BundleKeySeparator + productKey;
            if (!_bundles.TryGetValue(cacheKey, out var bundle))
            {
                bundle = item.Data.TryGetValue(productKey, out var raw) ? ParseBundle(item.Id, productKey, raw) : null;
                _bundles[cacheKey] = bundle;
            }
            return bundle?.DeepClone();
        }

        /// <summary>
        /// The bundle the resolver turned into <paramref name="sku"/> for <paramref name="item"/> —
        /// for a purchase handler that needs the contents. Null for a fixed SKU.
        /// </summary>
        public JToken GetProductBundleForSku(CampaignItem item, string sku)
        {
            if (item == null || string.IsNullOrEmpty(sku)) return null;
            return _bundleKeyBySku.TryGetValue(item.Id + BundleKeySeparator + sku, out var key)
                ? GetProductBundle(item, key)
                : null;
        }

        /// <summary>
        /// The order <c>extra</c> for a purchase from <paramref name="item"/>: the game's own
        /// <paramref name="extra"/> plus <c>campaign_id</c>, and for a dynamic product
        /// <c>campaign_bundle_key</c> and <c>campaign_bundle</c> (compact JSON). A new dictionary;
        /// set it on <c>PurchaseRequest.Extra</c>. The bundle is the one the resolver turned into
        /// <paramref name="sku"/>; see the overload taking the button's product key.
        /// </summary>
        public Dictionary<string, string> PurchaseExtra(
            CampaignItem item, string sku, IReadOnlyDictionary<string, string> extra = null) =>
            PurchaseExtra(item, sku, productKey: null, extra);

        /// <summary>
        /// <see cref="PurchaseExtra(CampaignItem, string, IReadOnlyDictionary{string, string})"/> for
        /// the Buy button whose bundle is <paramref name="productKey"/> — right even when two buttons
        /// sell the same SKU with different items. When that bundle lists items and the game has a
        /// <c>Rewards</c> handler, it also sets <c>noctua_delivery = rewards</c>: the SDK delivers
        /// the items, and the game skips the order (<see cref="IsDeliveredByRewards"/>).
        /// </summary>
        public Dictionary<string, string> PurchaseExtra(
            CampaignItem item, string sku, string productKey, IReadOnlyDictionary<string, string> extra)
        {
            var result = extra == null
                ? new Dictionary<string, string>()
                : extra.ToDictionary(pair => pair.Key, pair => pair.Value);
            if (item == null) return result;

            result[ExtraCampaignId] = item.Id ?? string.Empty;

            var key = !string.IsNullOrWhiteSpace(productKey)
                ? productKey.Trim()
                : !string.IsNullOrEmpty(sku) && _bundleKeyBySku.TryGetValue(item.Id + BundleKeySeparator + sku, out var mapped)
                    ? mapped
                    : null;
            if (key == null) return result;

            result[ExtraBundleKey] = key;
            var bundle = GetProductBundle(item, key);
            if (bundle == null) return result;

            result[ExtraBundle] = bundle.ToString(Formatting.None);
            if (_rewards != null && HasDeliverableItems(bundle)) result[ExtraDelivery] = DeliveryRewards;
            return result;
        }

        /// <summary>
        /// The order <c>extra</c> for a purchase of <paramref name="sku"/> started from a campaign's
        /// Buy button (<see cref="PurchaseExtra(CampaignItem, string, string, IReadOnlyDictionary{string, string})"/>
        /// with the tapped button's bundle), or null for any other purchase. The composition root
        /// hands it to <c>Noctua.IAP</c>, which adds it under the game's own keys — no game code needed.
        /// </summary>
        public IReadOnlyDictionary<string, string> CampaignOrderExtra(string sku)
        {
            try
            {
                var item = FindCampaign(_dispatcher.RecentPurchaseCampaign(sku));
                return item == null ? null : PurchaseExtra(item, sku, _dispatcher.RecentPurchaseProductKey(sku), null);
            }
            catch (Exception e)
            {
                _log.Warning($"{LogTag} campaign order extra for '{sku}' failed: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// A copy of <paramref name="item"/> whose dynamic Buy buttons carry the resolved SKU, and
        /// whose unresolvable ones are hidden. The item itself when it has none; never mutates it.
        /// </summary>
        public CampaignItem WithDynamicProducts(CampaignItem item)
        {
            if (item?.View == null) return item;

            var view = ResolveDynamicNode(item.View, item, depth: 0);
            if (ReferenceEquals(view, item.View)) return item;

            var copy = item.ShallowCopy();
            copy.View = view;
            return copy;
        }

        private CampaignNode ResolveDynamicNode(CampaignNode node, CampaignItem item, int depth)
        {
            if (node == null || depth >= MaxSkuSearchDepth) return node;

            List<CampaignNode> children = null;
            if (node.Children != null)
            {
                for (var i = 0; i < node.Children.Count; i++)
                {
                    var child = ResolveDynamicNode(node.Children[i], item, depth + 1);
                    if (ReferenceEquals(child, node.Children[i])) continue;

                    children ??= new List<CampaignNode>(node.Children);
                    children[i] = child;
                }
            }

            var dynamic = IsDynamicPurchase(node.Action);
            if (!dynamic && children == null) return node;

            var copy = node.ShallowCopy();
            if (children != null) copy.Children = children;
            if (!dynamic) return copy;

            var sku = ResolveDynamicSku(item, node.Action.ProductKey.Trim());
            if (string.IsNullOrEmpty(sku))
            {
                copy.Action = null;
                copy.VisibleIf = Hidden();
                return copy;
            }

            copy.Action = new CampaignAction
            {
                TypeRaw = node.Action.TypeRaw,
                Deeplink = node.Action.Deeplink,
                ProductId = sku,
                ProductKey = node.Action.ProductKey,
                KeepOpen = node.Action.KeepOpen,
                Id = node.Action.Id,
            };
            return copy;
        }

        private static bool IsDynamicPurchase(CampaignAction action) =>
            action != null
            && action.Type == CampaignActionType.Purchase
            && string.IsNullOrWhiteSpace(action.ProductId)
            && !string.IsNullOrWhiteSpace(action.ProductKey);

        private string ResolveDynamicSku(CampaignItem item, string productKey)
        {
            var bundle = GetProductBundle(item, productKey);
            if (bundle == null) return null;

            var resolver = _productResolver;
            string sku;
            try { sku = (resolver != null ? resolver(item, productKey, bundle) : DefaultBundleSku(bundle))?.Trim(); }
            catch (Exception e)
            {
                _log.Warning($"{LogTag} product resolver threw for '{item.Id}'/'{productKey}': {e.Message}");
                return null;
            }

            if (string.IsNullOrEmpty(sku))
            {
                WarnHiddenOnce(item.Id, productKey, resolver != null
                    ? "the game's product resolver returned no SKU"
                    : $"its bundle has no {PlatformSkuKey} or {GenericSkuKey}, and no product resolver is set (SetProductResolver)");
                return null;
            }
            _bundleKeyBySku[item.Id + BundleKeySeparator + sku] = productKey;
            return sku;
        }

        /// <summary>Bundle key naming the SKU on this store; read when the game sets no resolver.</summary>
#if UNITY_IOS
        public const string PlatformSkuKey = "sku_ios";
#else
        public const string PlatformSkuKey = "sku_android";
#endif

        /// <summary>Bundle key naming the SKU on every store; the fallback after <see cref="PlatformSkuKey"/>.</summary>
        public const string GenericSkuKey = "sku";

        /// <summary>
        /// The SKU a bundle names for this store without game code: <see cref="PlatformSkuKey"/>,
        /// else <see cref="GenericSkuKey"/>, read from the object (or an array's first entry).
        /// Null when it names none.
        /// </summary>
        public static string DefaultBundleSku(JToken bundle)
        {
            var entry = bundle is JArray array ? array.FirstOrDefault() : bundle;
            if (entry is not JObject obj) return null;

            foreach (var key in new[] { PlatformSkuKey, GenericSkuKey })
            {
                if (obj.TryGetValue(key, out var value) && value.Type == JTokenType.String)
                {
                    var sku = value.Value<string>()?.Trim();
                    if (!string.IsNullOrEmpty(sku)) return sku;
                }
            }
            return null;
        }

        // Campaign + key pairs already warned about, so a re-render does not repeat the log.
        private readonly HashSet<string> _hiddenWarned = new(StringComparer.Ordinal);

        private void WarnHiddenOnce(string campaignId, string productKey, string reason)
        {
            if (!_hiddenWarned.Add(campaignId + BundleKeySeparator + productKey)) return;
            _log.Warning($"{LogTag} dynamic product '{productKey}' in campaign '{campaignId}' is hidden: {reason}.");
        }

        private JToken ParseBundle(string campaignId, string productKey, string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            if (Encoding.UTF8.GetByteCount(raw) > MaxBundleBytes)
            {
                _log.Warning($"{LogTag} bundle '{productKey}' in '{campaignId}' is over {MaxBundleBytes} bytes — button hidden");
                return null;
            }

            try
            {
                var token = JToken.Parse(raw);
                var valid = token is JObject
                            || (token is JArray array && array.Count > 0 && array.All(entry => entry is JObject));
                if (valid) return token;

                _log.Warning($"{LogTag} bundle '{productKey}' in '{campaignId}' is not an object or array of objects — button hidden");
                return null;
            }
            catch (JsonException e)
            {
                _log.Warning($"{LogTag} bundle '{productKey}' in '{campaignId}' is not valid JSON — button hidden: {e.Message}");
                return null;
            }
        }

        /// <summary>A condition that is never true: the node is not rendered.</summary>
        private static CampaignCondition Hidden() => new()
        {
            All = new List<CampaignConditionClause> { new() { Left = "0", Op = "eq", Right = "1" } },
        };

        /// <summary>The SKUs <paramref name="item"/> sells once dynamic products are resolved.</summary>
        private IReadOnlyList<string> SkusOf(CampaignItem item) => PurchaseSkus(WithDynamicProducts(item));
    }
}
