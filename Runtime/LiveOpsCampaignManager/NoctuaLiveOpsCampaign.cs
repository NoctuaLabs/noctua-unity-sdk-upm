using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using com.noctuagames.sdk.Events;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// Public entry point for the server-driven campaign feature, exposed as
    /// <c>Noctua.LiveOpsCampaign</c> (the way <c>MediationManager</c> is <c>Noctua.IAA</c>).
    /// Owns the manager, renderer, dispatcher and UI host for the module.
    /// </summary>
    public sealed partial class NoctuaLiveOpsCampaign
    {
        private const string ImpressionEvent = "live_ops_campaign_impression";
        private const string DismissEvent = "live_ops_campaign_dismiss";

        private readonly ILogger _log = new NoctuaLogger(typeof(NoctuaLiveOpsCampaign));

        private const string LogTag = "[campaign]";
        private readonly CampaignManager _manager;
        private readonly CampaignUIHost _host;
        private readonly CampaignActionDispatcher _dispatcher;
        private readonly CampaignActionHandlers _handlers;
        private readonly IEventSender _events;
        // Local store prices (SKU → display price) for {{store_price.<sku>}} and the single-offer
        // templates' price. Null until a fetch succeeds; a failed fetch is retried on the next popup.
        private readonly Func<UniTask<IReadOnlyDictionary<string, string>>> _fetchStorePrices;
        private IReadOnlyDictionary<string, string> _storePrices;
        private bool _fetchingStorePrices;
        private int _storePricesGeneration;
        // "Does the player own this SKU?" (the store check by default). Null = ownership never
        // hides a campaign or flips a card. Answers are cached per SKU; a SKU missing from the
        // cache is unknown, and unknown never hides anything.
        private Func<string, UniTask<bool>> _isProductOwned;
        private readonly Dictionary<string, bool> _ownership = new Dictionary<string, bool>();
        private int _ownershipGeneration;

        /// <summary>Token prefix for a local price; the rest of the key is the SKU (dots included).</summary>
        public const string StorePricePrefix = "store_price.";

        /// <summary>
        /// Token prefix for ownership: <c>owned.&lt;sku&gt;</c> is <c>"1"</c> when the player owns the
        /// SKU and <c>"0"</c> when not. Ship it in <c>data</c> with a <c>"0"</c> default.
        /// </summary>
        public const string OwnedPrefix = "owned.";

        /// <summary>How long one ownership check may take before the SKU is treated as unknown.</summary>
        public const int OwnershipTimeoutMs = 1500;

        /// <summary>The single-offer templates' price key, and the SKU key it belongs to.</summary>
        public const string PriceKey = "price";
        public const string ProductIdKey = "product_id";

        private Action<string> _deeplinkHandler;
        private bool _autoShown;

        // The stored campaign currently on screen and the game values it was shown with, so
        // UpdatePopupData can re-merge against the original rather than a merged copy.
        private CampaignItem _shownItem;
        private IReadOnlyDictionary<string, string> _shownPlayerData;

        /// <summary>Fires with a campaign id when its popup is shown.</summary>
        public event Action<string> OnCampaignShown;

        /// <summary>Fires with a campaign id when any of its actions is tapped.</summary>
        public event Action<string> OnCampaignClicked;

        /// <summary>Fires with a campaign id when its popup is dismissed.</summary>
        public event Action<string> OnCampaignDismissed;

        /// <summary>
        /// Fires when a <c>purchase</c> action runs — the game completes it with its own
        /// role/server/price context via <c>Noctua.IAP</c>.
        /// </summary>
        public event Action<string, CampaignItem> OnCampaignPurchaseRequested;

        public NoctuaLiveOpsCampaign(
            CampaignConfig merged,
            PanelSettings panelSettings,
            NoctuaLocale locale,
            IEventSender events,
            Func<IReadOnlyList<string>> playerTags,
            Func<UniTask<IReadOnlyDictionary<string, string>>> fetchStorePrices = null,
            ProgressTrackerOptions progress = null,
            Func<string, UniTask<bool>> isProductOwned = null)
        {
            _events = events;
            _fetchStorePrices = fetchStorePrices;
            _isProductOwned = isProductOwned;
            InitProgress(progress, locale);

            var env = new DefaultCampaignEnvironment(playerTags, locale);
            var assets = new CampaignAssetSource(isOffline: Noctua.IsOfflineMode);
            var fonts = new CampaignFontSource();

            _manager = new CampaignManager(
                merged, env, new CampaignFrequencyGate(),
                isOffline: Noctua.IsOfflineMode,
                assetsReady: assets.AreAllImagesCached,
                isOwned: IsFullyOwned);

            try { assets.Preload(merged); }
            catch (Exception e) { _log.Warning("campaign asset preload failed: " + e.Message); }

            try { fonts.Preload(merged); }
            catch (Exception e) { _log.Warning("campaign font preload failed: " + e.Message); }

            // Both hooks are always non-null here, so the dispatcher's own
            // "no handler wired" warning can never fire for them -- it checks the
            // hook, and the hook exists. What can be missing is the game's
            // subscriber behind it, and that failed silently: a popup shown by
            // auto_show, with nothing subscribed, swallowed every tap with no
            // log anywhere. So the emptiness is reported here, where it is
            // actually visible.
            _handlers = new CampaignActionHandlers
            {
                Deeplink = route =>
                {
                    if (_deeplinkHandler == null)
                    {
                        _log.Warning($"{LogTag} a campaign's deeplink action fired, but no handler " +
                                     "is registered -- call RegisterDeeplinkHandler after init.");
                        return;
                    }
                    _deeplinkHandler.Invoke(route);
                },
                Purchase = (pid, item) =>
                {
                    if (OnCampaignPurchaseRequested == null)
                    {
                        _log.Warning($"{LogTag} campaign '{item?.Id}' asked to purchase '{pid}', but " +
                                     "nothing is subscribed to OnCampaignPurchaseRequested -- the tap " +
                                     "was dropped. Subscribe after init; auto-shown popups need it too.");
                        return;
                    }
                    OnCampaignPurchaseRequested.Invoke(pid, item);
                },
            };

            _dispatcher = new CampaignActionDispatcher(
                _handlers,
                events,
                onDispatched: (item, _) => { if (item != null) SafeInvoke(OnCampaignClicked, item.Id); });

            var renderer = new CampaignRenderer(_dispatcher, assets, fonts);
            _host = new CampaignUIHost(panelSettings, locale, renderer, assets);
        }

        /// <summary>The resolver behind this facade — read by the sandbox Inspector.</summary>
        public CampaignManager Manager => _manager;

        /// <summary>The id of the campaign on screen, or null.</summary>
        public string ShownCampaignId => _shownItem?.Id;

        /// <summary>The per-player values the popup on screen was last rendered with, or null.</summary>
        public IReadOnlyDictionary<string, string> ShownPlayerData => _shownItem == null ? null : _shownPlayerData;

        /// <summary>Registers the game's deeplink router for <c>deeplink</c> actions.</summary>
        public void RegisterDeeplinkHandler(Action<string> handler) => _deeplinkHandler = handler;

        /// <summary>
        /// Registers an awaitable purchase handler — the recommended way to handle
        /// <c>purchase</c> actions. While it runs the popup veils itself with a spinner and
        /// further purchase taps are ignored; the popup stays open, so a failed or cancelled
        /// purchase returns the player to the offer. Pass <c>null</c> to clear.
        ///
        /// When set, <see cref="OnCampaignPurchaseRequested"/> is NOT raised for purchase
        /// actions — raising both would start two purchases. The event remains for games that
        /// have not migrated, but it gives the SDK no completion signal, so it gets a short
        /// double-tap debounce instead of a real busy state.
        /// </summary>
        public void SetPurchaseHandler(Func<string, CampaignItem, UniTask> handler)
        {
            // Once the purchase settles, ask again whether the SKU is owned so the open popup
            // flips its card in place. After the game's handler, so a game-supplied ownership
            // record has already been updated.
            _handlers.PurchaseAsync = handler == null
                ? (Func<string, CampaignItem, UniTask>)null
                : async (pid, item) =>
                {
                    try { await handler(pid, item); }
                    finally { RefreshOwnershipAsync(pid).Forget(); }
                };
        }

        /// <summary>
        /// Eligible campaigns, highest priority first. Pass
        /// <see cref="CampaignItem.EngagementPurchase"/> / <see cref="CampaignItem.EngagementEvent"/>
        /// to filter, or nothing for all.
        /// </summary>
        public IReadOnlyList<CampaignItem> GetActiveCampaigns(string engagementType = null) =>
            _manager.GetActiveCampaigns(engagementType);

        /// <summary>
        /// Shows a campaign in the popup — the one with <paramref name="id"/>, or the
        /// highest-priority eligible campaign of any engagement type when <paramref name="id"/>
        /// is null. No-op when nothing is eligible.
        /// </summary>
        public void ShowPopup(string id = null) => ShowPopup(id, null);

        /// <summary>
        /// Shows a campaign with per-player values — e.g. mission progress — for the keys the
        /// campaign declares in <c>player_data</c>. Keys the campaign does not declare are
        /// ignored (and logged); declared keys left out render their defaults.
        /// </summary>
        public void ShowPopup(string id, IReadOnlyDictionary<string, string> playerData)
        {
            try
            {
                var stored = _manager.GetTopCampaign(id: id);
                if (stored == null)
                {
                    _log.Debug($"ShowPopup: no eligible campaign (id='{id}')");
                    return;
                }

                // An offer whose SKUs were never checked: ask the store first, so a bought
                // offer is not shown and an owned card never flashes its Buy button.
                if (NeedsOwnershipCheck(stored))
                {
                    ShowAfterOwnershipCheckAsync(stored, playerData).Forget();
                    return;
                }

                ShowStored(stored, playerData);
            }
            catch (Exception e)
            {
                _log.Error("ShowPopup failed: " + e.Message);
            }
        }

        private void ShowStored(CampaignItem stored, IReadOnlyDictionary<string, string> playerData)
        {
            // Daily missions with the tracker configured: shown only with real progress.
            if (GatesOnProgress(stored))
            {
                ShowWithProgressAsync(stored, playerData, auto: false).Forget();
                return;
            }

            ShowResolved(stored, playerData);
        }

        private async UniTaskVoid ShowAfterOwnershipCheckAsync(CampaignItem stored, IReadOnlyDictionary<string, string> playerData)
        {
            try
            {
                if (await IsOwnedAfterCheckAsync(stored)) return;
                ShowStored(stored, playerData);
            }
            catch (Exception e)
            {
                _log.Error("ShowPopup failed: " + e.Message);
            }
        }

        /// <summary>
        /// Shows a campaign whose per-player values are settled. <paramref name="gameData"/> is the
        /// game's own part of them, kept so later progress refreshes stay under it.
        /// </summary>
        private void ShowResolved(
            CampaignItem stored,
            IReadOnlyDictionary<string, string> playerData,
            bool progressBacked = false,
            IReadOnlyDictionary<string, string> gameData = null)
        {
            try
            {
                var item = RenderItem(stored, playerData);

                var popup = _host.Popup;
                popup.SetCallbacks(
                    onShown: shown =>
                    {
                        _manager.MarkShown(shown);
                        _events?.Send(ImpressionEvent, IdPayload(shown.Id));
                        SafeInvoke(OnCampaignShown, shown.Id);
                    },
                    onClosed: () =>
                    {
                        if (ReferenceEquals(_shownItem, stored))
                        {
                            _shownItem = null;
                            _shownPlayerData = null;
                            _shownProgressBacked = false;
                            _shownGameData = null;
                        }
                        if (_dispatcher.CurrentDismiss == (Action)popup.Close) _dispatcher.CurrentDismiss = null;
                        if (_dispatcher.CurrentBusy == (Action<bool>)popup.SetBusy) _dispatcher.CurrentBusy = null;
                        _events?.Send(DismissEvent, IdPayload(item.Id));
                        SafeInvoke(OnCampaignDismissed, item.Id);
                    },
                    onFailed: () => _log.Warning($"ShowPopup: campaign '{item.Id}' failed to render"));

                _dispatcher.CurrentDismiss = popup.Close;
                _dispatcher.CurrentBusy = popup.SetBusy;
                _shownItem = stored;
                _shownPlayerData = playerData;
                _shownProgressBacked = progressBacked;
                _shownGameData = gameData == null
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string>(gameData);
                popup.Show(item, _manager.Config.SchemaVersion);

                // Shown with the USD fallback; swap in the player's local prices once known.
                RefreshStorePricesAsync(stored).Forget();
            }
            catch (Exception e)
            {
                _log.Error("ShowPopup failed: " + e.Message);
            }
        }

        /// <summary>
        /// The player's local price per SKU, from the SKU Management product list. Null until
        /// fetched — popups show the admin's USD fallback until then.
        /// </summary>
        public IReadOnlyDictionary<string, string> StorePrices => _storePrices;

        /// <summary>
        /// Forgets the cached local prices — the composition root calls this when the account
        /// changes, since the currency and product list are per player.
        /// </summary>
        public void ClearStorePrices()
        {
            _storePrices = null;
            _storePricesGeneration++;
        }

        /// <summary>
        /// Loads local prices if none are cached. Popups do this on their own; call it ahead of
        /// time to have prices ready before the first popup. Resolves true only when this call
        /// stored new prices. A failure (offline, not logged in, IAP off) is not cached.
        /// </summary>
        public async UniTask<bool> PrefetchStorePricesAsync()
        {
            if (_fetchStorePrices == null || _storePrices != null || _fetchingStorePrices) return false;

            _fetchingStorePrices = true;
            var generation = _storePricesGeneration;
            try
            {
                var prices = await _fetchStorePrices();
                // An account switch mid-fetch makes this answer stale.
                if (prices == null || generation != _storePricesGeneration) return false;

                _storePrices = prices;
                return true;
            }
            catch (Exception e)
            {
                _log.Debug($"{LogTag} local prices unavailable, using fallbacks: {e.Message}");
                return false;
            }
            finally
            {
                _fetchingStorePrices = false;
            }
        }

        /// <summary>True when <paramref name="item"/> shows at least one store price.</summary>
        public static bool UsesStorePrices(CampaignItem item)
        {
            if (item?.Data == null) return false;
            if (item.Data.ContainsKey(PriceKey) && item.Data.TryGetValue(ProductIdKey, out var sku)
                && !string.IsNullOrEmpty(sku))
            {
                return true;
            }
            foreach (var key in item.Data.Keys)
            {
                if (key.StartsWith(StorePricePrefix, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>
        /// A copy of <paramref name="item"/> whose price keys carry the local price wherever
        /// <paramref name="prices"/> has one. Keys the campaign does not ship are never added,
        /// and SKUs with no local price keep their fallback. Pure: never mutates the stored item.
        /// </summary>
        public static CampaignItem ApplyStorePrices(CampaignItem item, IReadOnlyDictionary<string, string> prices)
        {
            if (item?.Data == null || prices == null) return item;

            Dictionary<string, string> data = null;

            void Override(string key, string sku, string current)
            {
                if (string.IsNullOrEmpty(sku)) return;
                if (!prices.TryGetValue(sku, out var price) || string.IsNullOrEmpty(price)) return;
                if (price == current) return;

                data ??= new Dictionary<string, string>(item.Data);
                data[key] = price;
            }

            foreach (var pair in item.Data)
            {
                if (pair.Key.StartsWith(StorePricePrefix, StringComparison.Ordinal))
                {
                    Override(pair.Key, pair.Key.Substring(StorePricePrefix.Length), pair.Value);
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

        private async UniTaskVoid RefreshStorePricesAsync(CampaignItem stored)
        {
            try
            {
                if (!UsesStorePrices(stored)) return;
                if (!await PrefetchStorePricesAsync()) return;

                var popup = _host.PopupIfCreated;
                if (!ReferenceEquals(_shownItem, stored) || popup == null || !popup.IsShowing) return;

                popup.Refresh(RenderItem(stored, _shownPlayerData), _manager.Config.SchemaVersion);
            }
            catch (Exception e)
            {
                _log.Warning($"{LogTag} local price refresh failed: {e.Message}");
            }
        }

        /// <summary>The stored campaign with the game's values and the known local prices folded in.</summary>
        private CampaignItem RenderItem(CampaignItem stored, IReadOnlyDictionary<string, string> playerData) =>
            ApplyOwnership(ApplyStorePrices(MergePlayerData(stored, playerData), _storePrices), _ownership);

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
            _ownership.Clear();
            _ownershipGeneration++;
        }

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
        /// of it. Unknown SKUs count as not owned, so a failed check never hides an offer.
        /// </summary>
        public bool IsFullyOwned(CampaignItem item) => IsFullyOwned(item, _ownership);

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

        private bool NeedsOwnershipCheck(CampaignItem item)
        {
            if (_isProductOwned == null) return false;
            foreach (var sku in PurchaseSkus(item))
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
            foreach (var sku in PurchaseSkus(item))
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
            return learned;
        }

        /// <summary>
        /// Asks again whether <paramref name="productId"/> is owned — after a purchase — and
        /// re-renders the open popup if it sells that SKU, so its card flips in place.
        /// </summary>
        public async UniTask RefreshOwnershipAsync(string productId)
        {
            try
            {
                var provider = _isProductOwned;
                if (provider == null || string.IsNullOrEmpty(productId)) return;

                _ownership.Remove(productId);
                var generation = _ownershipGeneration;
                var owned = await CheckOwnedAsync(provider, productId);
                if (owned == null || generation != _ownershipGeneration) return;
                _ownership[productId] = owned.Value;

                var popup = _host.PopupIfCreated;
                if (_shownItem == null || popup == null || !popup.IsShowing) return;
                if (!PurchaseSkus(_shownItem).Contains(productId)) return;

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
            await PrefetchOwnershipAsync(item);
            if (!IsFullyOwned(item)) return false;

            _log.Info($"{LogTag} campaign '{item.Id}' not shown — the player already owns everything it sells");
            return true;
        }

        /// <summary>One provider call: its answer, or null when it threw or took too long.</summary>
        private async UniTask<bool?> CheckOwnedAsync(Func<string, UniTask<bool>> provider, string sku)
        {
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
        /// Refreshes the open popup with new per-player values — call it after the game handles
        /// a <c>keep_open</c> deeplink such as a claim, so the card flips to its new state in
        /// place. Keys left out keep the value the popup was last shown or updated with. No-op
        /// when nothing is showing.
        /// </summary>
        public void UpdatePopupData(IReadOnlyDictionary<string, string> playerData)
        {
            try
            {
                var popup = _host.PopupIfCreated;
                if (_shownItem == null || popup == null || !popup.IsShowing)
                {
                    _log.Debug("UpdatePopupData: no campaign popup is showing");
                    return;
                }

                var combined = new Dictionary<string, string>();
                if (_shownPlayerData != null) foreach (var pair in _shownPlayerData) combined[pair.Key] = pair.Value;
                if (playerData != null) foreach (var pair in playerData) combined[pair.Key] = pair.Value;
                // The game's values keep winning over later progress refreshes.
                if (_shownProgressBacked && playerData != null)
                {
                    foreach (var pair in playerData) _shownGameData[pair.Key] = pair.Value;
                }

                if (!popup.Refresh(RenderItem(_shownItem, combined), _manager.Config.SchemaVersion))
                {
                    _log.Warning($"UpdatePopupData: campaign '{_shownItem.Id}' could not re-render; kept current content");
                    return;
                }
                _shownPlayerData = combined;
            }
            catch (Exception e)
            {
                _log.Error("UpdatePopupData failed: " + e.Message);
            }
        }

        private CampaignItem MergePlayerData(CampaignItem stored, IReadOnlyDictionary<string, string> playerData) =>
            CampaignPlayerData.Merge(stored, playerData, key =>
                _log.Warning($"{LogTag} campaign '{stored.Id}' does not declare player_data key '{key}' — ignored"));

        /// <summary>
        /// Closes the campaign popup if one is showing — e.g. from inside a deeplink handler
        /// after navigating the player away. No-op when nothing is showing. Fires
        /// <c>live_ops_campaign_dismiss</c> and <see cref="OnCampaignDismissed"/> like the close button.
        /// </summary>
        public void ClosePopup()
        {
            try { _host.PopupIfCreated?.Close(); }
            catch (Exception e) { _log.Error("ClosePopup failed: " + e.Message); }
        }

        /// <summary>
        /// Composition-root hook: shows the first eligible <c>auto_show</c> campaign once,
        /// right after init. Safe to call more than once — only the first has effect.
        /// </summary>
        public void RunAutoShow() => RunAutoShowAsync().Forget();

        /// <summary>
        /// <see cref="RunAutoShow"/>, awaitable: true when a popup was shown. A daily missions
        /// campaign with no progress to show (tracker down, no saved copy) is skipped for the next
        /// eligible one; when it was skipped only because nobody is logged in yet and nothing else
        /// showed, it is tried again after the first login.
        /// </summary>
        public UniTask<bool> RunAutoShowAsync()
        {
            if (_autoShown) return UniTask.FromResult(false);
            _autoShown = true;
            return AutoShowCoreAsync();
        }

        private async UniTask<bool> AutoShowCoreAsync()
        {
            try
            {
                var waitingForPlayer = false;
                foreach (var item in _manager.GetActiveCampaigns())
                {
                    if (!item.AutoShow) continue;
                    // A bought offer gives way to the next auto_show campaign.
                    if (await IsOwnedAfterCheckAsync(item)) continue;

                    if (!GatesOnProgress(item))
                    {
                        ShowResolved(item, null);
                        return true;
                    }

                    if (TryProgressCaller() == null)
                    {
                        waitingForPlayer = true;
                        continue;
                    }

                    if (await ShowWithProgressAsync(item, null, auto: true)) return true;
                }

                _autoShowNeedsPlayer = waitingForPlayer;
                return false;
            }
            catch (Exception e)
            {
                _log.Warning("RunAutoShow failed: " + e.Message);
                return false;
            }
        }

        private static Dictionary<string, IConvertible> IdPayload(string id) =>
            new Dictionary<string, IConvertible> { { "campaign_id", id ?? string.Empty } };

        private void SafeInvoke(Action<string> evt, string arg)
        {
            try { evt?.Invoke(arg); }
            catch (Exception e) { _log.Warning("campaign event handler threw: " + e.Message); }
        }
    }
}
