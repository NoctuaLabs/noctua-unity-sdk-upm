using System;
using System.Collections.Generic;
using com.noctuagames.sdk.LiveOpsCampaign;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

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
        /// Claims one mission; its reward goes to the <c>MissionRewards</c> handler (and is
        /// returned). Popup Claim buttons do this for you.
        /// </summary>
        public async UniTask<MissionReward> ClaimMissionAsync(string campaignId, string missionKey) =>
            await (await ReadyModuleAsync()).ClaimMissionRewardAsync(campaignId, missionKey);

        /// <summary>
        /// Claims every completed mission of a campaign; the rewards go to the <c>MissionRewards</c>
        /// handler (and are returned). Popup Claim All buttons do this for you.
        /// </summary>
        public async UniTask<IReadOnlyList<MissionReward>> ClaimAllMissionsAsync(string campaignId) =>
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
            _ready.TrySetResult(module);
        }

        /// <summary>SDK-only: live ops could not be set up; waiting calls throw <paramref name="error"/>.</summary>
        public void Fail(Exception error)
        {
            if (_module == null) _ready.TrySetException(error);
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
