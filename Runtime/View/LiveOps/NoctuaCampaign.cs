using System;
using System.Collections.Generic;
using com.noctuagames.sdk.LiveOpsCampaign;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// <c>Noctua.LiveOps.Campaign</c>: popups, offers and daily missions authored in noctua-admin.
    /// Never null and safe to use before <c>Noctua.InitAsync</c>: handlers and
    /// event subscriptions are kept, async calls wait until live ops is ready, and
    /// <see cref="ShowPopup(string)"/> waits too. Reads return empty until then.
    /// </summary>
    public sealed class NoctuaCampaign
    {
        private readonly ILogger _log = new NoctuaLogger(typeof(NoctuaCampaign));
        private readonly UniTaskCompletionSource<NoctuaLiveOpsCampaign> _ready = new();
        private NoctuaLiveOpsCampaign _module;
        private CampaignHandlers _handlers;

        /// <summary>Fires with a campaign id when its popup is shown.</summary>
        public event Action<string> OnCampaignShown;

        /// <summary>Fires with a campaign id when a button in its popup is tapped.</summary>
        public event Action<string> OnCampaignClicked;

        /// <summary>Fires with a campaign id when its popup is closed.</summary>
        public event Action<string> OnCampaignDismissed;

        /// <summary>Fires with the player's mission progress whenever it changes.</summary>
        public event Action<IReadOnlyList<LiveOpsProgress>> OnProgressChanged;

        /// <summary>
        /// Fires with a campaign id when its missions cannot be shown right now (tracker down or
        /// nobody logged in); the SDK has already shown the player a notice.
        /// </summary>
        public event Action<string> OnCampaignUnavailable;

        /// <summary>True once <c>Noctua.InitAsync</c> has set live ops up.</summary>
        public bool IsReady => _module != null;

        /// <summary>The campaign module behind this facade, for diagnostics (the Inspector); null until ready.</summary>
        public NoctuaLiveOpsCampaign Module => _module;

        // ---- setup ---------------------------------------------------------------

        /// <summary>
        /// The game's handlers — deeplinks, purchases, product resolver, ownership, mission
        /// rewards. Call it before <c>Noctua.InitAsync</c> so an auto-shown popup already has them;
        /// calling it again replaces only the fields you pass.
        /// </summary>
        public void Configure(CampaignHandlers handlers)
        {
            _handlers = CampaignHandlers.Merge(_handlers, handlers);
            _module?.Configure(handlers);
        }

        /// <summary>Completes once live ops is ready; throws when it could not be set up.</summary>
        public UniTask WhenReadyAsync() => _ready.Task;

        // ---- popups --------------------------------------------------------------

        /// <summary>Shows campaign <paramref name="id"/>, or the highest-priority eligible one when null.</summary>
        public void ShowPopup(string id = null) => ShowPopup(id, null);

        /// <summary>Shows campaign <paramref name="id"/> with per-player values for its <c>player_data</c>.</summary>
        public void ShowPopup(string id, IReadOnlyDictionary<string, string> playerData)
        {
            if (_module != null)
            {
                _module.ShowPopup(id, playerData);
                return;
            }
            ShowWhenReadyAsync(id, playerData).Forget();
        }

        /// <summary>Re-renders the open popup with new per-player values. Nothing is open before init.</summary>
        public void UpdatePopupData(IReadOnlyDictionary<string, string> playerData) => _module?.UpdatePopupData(playerData);

        /// <summary>Closes the open popup, if any.</summary>
        public void ClosePopup() => _module?.ClosePopup();

        /// <summary>
        /// Eligible campaigns, highest priority first; pass <see cref="CampaignItem.EngagementPurchase"/>
        /// or <see cref="CampaignItem.EngagementEvent"/> to filter. Empty before init.
        /// </summary>
        public IReadOnlyList<CampaignItem> GetActiveCampaigns(string engagementType = null) =>
            _module?.GetActiveCampaigns(engagementType) ?? Array.Empty<CampaignItem>();

        // ---- missions ------------------------------------------------------------

        /// <summary>
        /// Reports the player's absolute value for mission <paramref name="missionKey"/> (the key
        /// from the admin). Saved on the device and sent later when offline. Needs a logged-in player.
        /// </summary>
        public async UniTask<IReadOnlyList<LiveOpsProgress>> SetMissionProgressAsync(string missionKey, long value) =>
            await (await ReadyModuleAsync()).SetMissionProgressAsync(missionKey, value);

        /// <summary>
        /// Claims one mission; its reward goes to the <c>Rewards</c> handler (and is returned).
        /// Popup Claim buttons do this for you.
        /// </summary>
        public async UniTask<LiveOpsReward> ClaimMissionAsync(string campaignId, string missionKey) =>
            await (await ReadyModuleAsync()).ClaimMissionRewardAsync(campaignId, missionKey);

        /// <summary>
        /// Claims the milestone chest at <paramref name="points"/> once the player's points reach it;
        /// its reward goes to the <c>Rewards</c> handler (and is returned). Popup chest buttons do
        /// this for you.
        /// </summary>
        public async UniTask<LiveOpsReward> ClaimMilestoneAsync(string campaignId, int points) =>
            await (await ReadyModuleAsync()).ClaimMilestoneRewardAsync(campaignId, points);

        /// <summary>
        /// Claims every completed mission of a campaign, then every chest the points reach; the
        /// rewards go to the <c>Rewards</c> handler (and are returned). Popup Claim All buttons do
        /// this for you.
        /// </summary>
        public async UniTask<IReadOnlyList<LiveOpsReward>> ClaimAllMissionsAsync(string campaignId) =>
            await (await ReadyModuleAsync()).ClaimAllMissionsAsync(campaignId);

        /// <summary>The player's mission progress from the server (or the saved copy when offline).</summary>
        public async UniTask<IReadOnlyList<LiveOpsProgress>> GetProgressAsync() =>
            await (await ReadyModuleAsync()).GetProgressAsync();

        /// <summary>The last known mission progress, without a network call. Empty before init.</summary>
        public IReadOnlyList<LiveOpsProgress> Progress => _module?.Progress ?? Array.Empty<LiveOpsProgress>();

        // ---- purchases -----------------------------------------------------------

        /// <summary>Whether the player already bought one-purchase-per-player campaign <paramref name="campaignId"/>.</summary>
        public async UniTask<bool> IsCampaignPurchasedAsync(string campaignId) =>
            await (await ReadyModuleAsync()).IsCampaignPurchasedAsync(campaignId);

        /// <summary>The dynamic-product bundle a Buy button resolved to <paramref name="sku"/>, or null.</summary>
        public JToken GetProductBundleForSku(CampaignItem campaign, string sku) => _module?.GetProductBundleForSku(campaign, sku);

        /// <summary>
        /// True when the SDK delivers this order's items through the <c>Rewards</c> handler — a
        /// campaign bundle that lists valid items, bought while the game had a <c>Rewards</c>
        /// handler. The SDK keeps such orders from <c>Noctua.IAP.OnPurchaseDone</c> itself; this is
        /// for code that reads orders elsewhere (e.g. a purchase history screen).
        /// </summary>
        public bool IsDeliveredByRewards(OrderRequest order) => NoctuaLiveOpsCampaign.PurchaseRewardOf(order) != null;

        /// <summary>
        /// SDK-only: a verified purchase (<c>Noctua.IAP.OnOrderVerified</c>). An order the SDK
        /// delivers becomes a <c>Purchase</c> reward for <paramref name="buyerId"/>, and an order from
        /// a <c>purchase_once</c> campaign marks that campaign bought. Before live ops is ready a
        /// campaign's order waits on the device and is recorded on <see cref="Attach"/>.
        /// </summary>
        public void OnPurchaseDone(OrderRequest order, long? buyerId)
        {
            if (order?.Extra == null || !order.Extra.ContainsKey(NoctuaLiveOpsCampaign.ExtraCampaignId)) return;

            if (_module != null)
            {
                Record(_module, order, buyerId);
                return;
            }
            SaveToInbox(order, buyerId);
        }

        private static void Record(NoctuaLiveOpsCampaign module, OrderRequest order, long? buyerId)
        {
            module.RecordPurchaseReward(order, buyerId);
            module.RecordOrderPurchaseAsync(order, buyerId).Forget();
        }

        // ---- composition root ----------------------------------------------------

        /// <summary>SDK-only: called by <c>Noctua.InitAsync</c> when the campaign module is built.</summary>
        public void Attach(NoctuaLiveOpsCampaign module)
        {
            if (module == null || module == _module) return;

            if (_module != null)
            {
                _module.OnCampaignShown -= RaiseShown;
                _module.OnCampaignClicked -= RaiseClicked;
                _module.OnCampaignDismissed -= RaiseDismissed;
                _module.OnProgressChanged -= RaiseProgress;
                _module.OnCampaignUnavailable -= RaiseUnavailable;
            }

            _module = module;
            module.OnCampaignShown += RaiseShown;
            module.OnCampaignClicked += RaiseClicked;
            module.OnCampaignDismissed += RaiseDismissed;
            module.OnProgressChanged += RaiseProgress;
            module.OnCampaignUnavailable += RaiseUnavailable;
            if (_handlers != null) module.Configure(_handlers);
            DrainInbox(module);
            _ready.TrySetResult(module);
        }

        /// <summary>SDK-only: live ops could not be set up; waiting calls throw <paramref name="error"/>.</summary>
        public void Fail(Exception error)
        {
            if (_module == null) _ready.TrySetException(error);
        }

        // ---- purchases verified before live ops was ready ---------------------------

        private const string InboxPrefsKey = "Noctua.LiveOps.Campaign.PurchaseInbox";

        [Preserve]
        private sealed class InboxEntry
        {
            [JsonProperty("order_id")] public int OrderId;
            [JsonProperty("product_id")] public string ProductId;
            [JsonProperty("extra")] public Dictionary<string, string> Extra;
            [JsonProperty("buyer_id")] public long? BuyerId;
            [JsonProperty("price")] public decimal Price;
            [JsonProperty("currency")] public string Currency;
        }

        private void SaveToInbox(OrderRequest order, long? buyerId)
        {
            var inbox = ReadInbox();
            if (inbox.Exists(entry => entry.OrderId == order.Id)) return;

            inbox.Add(new InboxEntry
            {
                OrderId = order.Id, ProductId = order.ProductId, Extra = order.Extra, BuyerId = buyerId,
                Price = order.Price, Currency = order.Currency,
            });
            WriteInbox(inbox);
            _log.Info($"[LiveOps.Campaign] order {order.Id} verified before live ops was ready; its reward is kept until then");
        }

        private void DrainInbox(NoctuaLiveOpsCampaign module)
        {
            var inbox = ReadInbox();
            if (inbox.Count == 0) return;

            foreach (var entry in inbox)
            {
                Record(
                    module,
                    new OrderRequest
                    {
                        Id = entry.OrderId, ProductId = entry.ProductId, Extra = entry.Extra,
                        Price = entry.Price, Currency = entry.Currency,
                    },
                    entry.BuyerId);
            }
            WriteInbox(new List<InboxEntry>());
        }

        private List<InboxEntry> ReadInbox()
        {
            try
            {
                var json = UnityEngine.PlayerPrefs.GetString(InboxPrefsKey, null);
                return string.IsNullOrEmpty(json)
                    ? new List<InboxEntry>()
                    : JsonConvert.DeserializeObject<List<InboxEntry>>(json) ?? new List<InboxEntry>();
            }
            catch (Exception e)
            {
                _log.Warning($"[LiveOps.Campaign] purchase inbox unreadable, ignoring it: {e.Message}");
                return new List<InboxEntry>();
            }
        }

        private static void WriteInbox(List<InboxEntry> inbox)
        {
            if (inbox.Count == 0) UnityEngine.PlayerPrefs.DeleteKey(InboxPrefsKey);
            else UnityEngine.PlayerPrefs.SetString(InboxPrefsKey, JsonConvert.SerializeObject(inbox));
            UnityEngine.PlayerPrefs.Save();
        }

        private async UniTask<NoctuaLiveOpsCampaign> ReadyModuleAsync() => _module ?? await _ready.Task;

        private async UniTaskVoid ShowWhenReadyAsync(string id, IReadOnlyDictionary<string, string> playerData)
        {
            try { (await _ready.Task).ShowPopup(id, playerData); }
            catch (Exception e) { _log.Warning($"[LiveOps.Campaign] popup '{id}' not shown, live ops unavailable: {e.Message}"); }
        }

        private void RaiseShown(string id) => Raise(OnCampaignShown, id);
        private void RaiseClicked(string id) => Raise(OnCampaignClicked, id);
        private void RaiseDismissed(string id) => Raise(OnCampaignDismissed, id);
        private void RaiseUnavailable(string id) => Raise(OnCampaignUnavailable, id);
        private void RaiseProgress(IReadOnlyList<LiveOpsProgress> progress) => Raise(OnProgressChanged, progress);

        private void Raise<T>(Action<T> handler, T value)
        {
            try { handler?.Invoke(value); }
            catch (Exception e) { _log.Warning($"[LiveOps.Campaign] event handler threw: {e.Message}"); }
        }
    }
}
