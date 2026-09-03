using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using com.noctuagames.sdk.Events;

namespace com.noctuagames.sdk.Campaign
{
    /// <summary>
    /// Public entry point for the server-driven campaign feature, exposed as
    /// <c>Noctua.Campaign</c> (the way <c>MediationManager</c> is <c>Noctua.IAA</c>).
    /// Owns the manager, renderer, dispatcher and UI host for the module.
    /// </summary>
    public sealed class NoctuaCampaign
    {
        private const string ImpressionEvent = "live_ops_campaign_impression";
        private const string DismissEvent = "live_ops_campaign_dismiss";

        private readonly ILogger _log = new NoctuaLogger(typeof(NoctuaCampaign));
        private readonly CampaignManager _manager;
        private readonly CampaignUIHost _host;
        private readonly CampaignActionDispatcher _dispatcher;
        private readonly IEventSender _events;

        private Action<string> _deeplinkHandler;
        private bool _autoShown;

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

        public NoctuaCampaign(
            CampaignConfig merged,
            PanelSettings panelSettings,
            NoctuaLocale locale,
            IEventSender events,
            Func<IReadOnlyList<string>> playerTags)
        {
            _events = events;

            var env = new DefaultCampaignEnvironment(playerTags, locale);
            var assets = new CampaignAssetSource(isOffline: Noctua.IsOfflineMode);
            var fonts = new CampaignFontSource();

            _manager = new CampaignManager(
                merged, env, new CampaignFrequencyGate(),
                isOffline: Noctua.IsOfflineMode,
                assetsReady: assets.AreAllImagesCached);

            try { assets.Preload(merged); }
            catch (Exception e) { _log.Warning("campaign asset preload failed: " + e.Message); }

            try { fonts.Preload(merged); }
            catch (Exception e) { _log.Warning("campaign font preload failed: " + e.Message); }

            var handlers = new CampaignActionHandlers
            {
                Deeplink = route => _deeplinkHandler?.Invoke(route),
                Purchase = (pid, item) => OnCampaignPurchaseRequested?.Invoke(pid, item),
            };

            _dispatcher = new CampaignActionDispatcher(
                handlers,
                events,
                onDispatched: (item, _) => { if (item != null) SafeInvoke(OnCampaignClicked, item.Id); });

            var renderer = new CampaignRenderer(_dispatcher, assets, fonts);
            _host = new CampaignUIHost(panelSettings, locale, renderer);
        }

        /// <summary>The resolver behind this facade — read by the sandbox Inspector.</summary>
        public CampaignManager Manager => _manager;

        /// <summary>Registers the game's deeplink router for <c>deeplink</c> actions.</summary>
        public void RegisterDeeplinkHandler(Action<string> handler) => _deeplinkHandler = handler;

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
        public void ShowPopup(string id = null)
        {
            try
            {
                var item = _manager.GetTopCampaign(id: id);
                if (item == null)
                {
                    _log.Debug($"ShowPopup: no eligible campaign (id='{id}')");
                    return;
                }

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
                        if (_dispatcher.CurrentDismiss == (Action)popup.Close) _dispatcher.CurrentDismiss = null;
                        _events?.Send(DismissEvent, IdPayload(item.Id));
                        SafeInvoke(OnCampaignDismissed, item.Id);
                    },
                    onFailed: () => _log.Warning($"ShowPopup: campaign '{item.Id}' failed to render"));

                _dispatcher.CurrentDismiss = popup.Close;
                popup.Show(item, _manager.Config.SchemaVersion);
            }
            catch (Exception e)
            {
                _log.Error("ShowPopup failed: " + e.Message);
            }
        }

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
        public void RunAutoShow()
        {
            if (_autoShown) return;
            _autoShown = true;

            try
            {
                foreach (var item in _manager.GetActiveCampaigns())
                {
                    if (!item.AutoShow) continue;
                    ShowPopup(item.Id);
                    return;
                }
            }
            catch (Exception e)
            {
                _log.Warning("RunAutoShow failed: " + e.Message);
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
