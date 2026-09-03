using System;
using System.Collections.Generic;
using com.noctuagames.sdk.Events;

namespace com.noctuagames.sdk.Campaign
{
    /// <summary>
    /// Game/SDK-side hooks the dispatcher calls. The facade wires each to a concrete
    /// service (the game's deeplink handler, <c>Noctua.IAP</c> via
    /// <c>OnCampaignPurchaseRequested</c>); the dispatcher itself stays free of those
    /// references. Any hook may be <c>null</c> — a missing hook logs and no-ops.
    /// </summary>
    public sealed class CampaignActionHandlers
    {
        /// <summary>Hands a route string to the game's registered deeplink handler.</summary>
        public Action<string> Deeplink;

        /// <summary>Starts a purchase for the given product id, in the campaign's context.</summary>
        public Action<string, CampaignItem> Purchase;
    }

    /// <summary>
    /// Executes the closed <see cref="CampaignActionType"/> set (deeplink / purchase /
    /// dismiss). Every dispatch also emits <c>live_ops_campaign_click</c>. Never throws — a bad or
    /// unknown action is logged and dropped.
    /// </summary>
    public sealed class CampaignActionDispatcher : ICampaignActions
    {
        /// <summary>Analytics event fired on every dispatched action.</summary>
        public const string ClickEvent = "live_ops_campaign_click";

        private readonly CampaignActionHandlers _handlers;
        private readonly IEventSender _events;
        private readonly Action<CampaignItem, CampaignAction> _onDispatched;
        private readonly ILogger _log;

        private const string LogTag = "[campaign_action]";

        /// <summary>
        /// Dismiss hook for the currently-shown presenter. Set on <c>Show</c>, cleared on
        /// <c>Close</c>. A <see cref="CampaignActionType.Dismiss"/> action invokes this.
        /// </summary>
        public Action CurrentDismiss { get; set; }

        public CampaignActionDispatcher(
            CampaignActionHandlers handlers,
            IEventSender events,
            Action<CampaignItem, CampaignAction> onDispatched = null,
            ILogger log = null)
        {
            _handlers = handlers ?? new CampaignActionHandlers();
            _events = events;
            _onDispatched = onDispatched;
            _log = log ?? new NoctuaLogger(typeof(CampaignActionDispatcher));
        }

        /// <inheritdoc />
        public void Dispatch(CampaignAction action, CampaignItem campaign)
        {
            if (action == null) return;

            try
            {
                EmitClick(action, campaign);
                try { _onDispatched?.Invoke(campaign, action); }
                catch (Exception cb) { _log.Warning($"{LogTag} onDispatched threw: {cb.Message}"); }

                switch (action.Type)
                {
                    case CampaignActionType.Deeplink:
                        Invoke(_handlers.Deeplink, action.Deeplink, "deeplink");
                        // A deeplink navigates the player away — close the popup behind them.
                        CurrentDismiss?.Invoke();
                        break;
                    case CampaignActionType.Purchase:
                        if (_handlers.Purchase == null) _log.Warning($"{LogTag} no handler wired for 'purchase'");
                        else if (string.IsNullOrEmpty(action.ProductId)) _log.Warning($"{LogTag} purchase action missing product_id in campaign '{campaign?.Id}'");
                        else _handlers.Purchase(action.ProductId, campaign);
                        break;
                    case CampaignActionType.Dismiss:
                        CurrentDismiss?.Invoke();
                        break;
                    case CampaignActionType.None:
                    default:
                        _log.Warning($"{LogTag} unknown/none action '{action.TypeRaw}' — ignored");
                        break;
                }
            }
            catch (Exception e)
            {
                _log.Error($"{LogTag} dispatch failed for '{action.TypeRaw}': {e.Message}");
            }
        }

        private void Invoke(Action<string> handler, string arg, string label)
        {
            if (handler == null)
            {
                _log.Warning($"{LogTag} no handler wired for '{label}'");
                return;
            }
            handler(arg);
        }

        private void EmitClick(CampaignAction action, CampaignItem campaign)
        {
            _events?.Send(ClickEvent, new Dictionary<string, IConvertible>
            {
                { "campaign_id", campaign?.Id ?? string.Empty },
                { "action_type", action.Type.ToString() },
            });
        }
    }
}
