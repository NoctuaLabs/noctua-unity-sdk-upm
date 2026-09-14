using System;
using System.Collections.Generic;
using com.noctuagames.sdk.Events;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk.LiveOpsCampaign
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

        /// <summary>
        /// Starts a purchase for the given product id, in the campaign's context.
        /// Fire-and-forget: the dispatcher gets no completion signal, so it cannot show a
        /// busy state. Used only when <see cref="PurchaseAsync"/> is null.
        /// </summary>
        public Action<string, CampaignItem> Purchase;

        /// <summary>
        /// Opt-in awaitable purchase hook. When set it takes precedence over
        /// <see cref="Purchase"/> — invoking both would start two purchases — and gives the
        /// dispatcher the completion signal it needs to bracket the call with a busy state.
        /// </summary>
        public Func<string, CampaignItem, UniTask> PurchaseAsync;
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

        /// <summary>
        /// How long a <c>purchase</c> tap suppresses further ones when only the legacy
        /// fire-and-forget <see cref="CampaignActionHandlers.Purchase"/> hook is wired. With no
        /// completion signal a real in-flight flag could never clear, so the legacy path gets a
        /// time window instead — enough to eat a double-tap, short enough not to strand the player.
        /// </summary>
        public const int SyncPurchaseDebounceMs = 1000;

        private readonly CampaignActionHandlers _handlers;
        private readonly IEventSender _events;
        private readonly Action<CampaignItem, CampaignAction> _onDispatched;
        private readonly ILogger _log;
        private readonly Func<DateTime> _now;

        private bool _purchaseInFlight;
        private DateTime _lastSyncPurchaseAt = DateTime.MinValue;

        private const string LogTag = "[campaign_action]";

        /// <summary>
        /// Dismiss hook for the currently-shown presenter. Set on <c>Show</c>, cleared on
        /// <c>Close</c>. A <see cref="CampaignActionType.Dismiss"/> action invokes this.
        /// </summary>
        public Action CurrentDismiss { get; set; }

        /// <summary>
        /// Busy hook for the currently-shown presenter, set on <c>Show</c> and cleared on
        /// <c>Close</c> exactly like <see cref="CurrentDismiss"/>. Called with <c>true</c> when an
        /// awaitable purchase starts and <c>false</c> when it settles, so the popup can veil
        /// itself. A bool callback keeps this class free of any UI reference.
        /// </summary>
        public Action<bool> CurrentBusy { get; set; }

        public CampaignActionDispatcher(
            CampaignActionHandlers handlers,
            IEventSender events,
            Action<CampaignItem, CampaignAction> onDispatched = null,
            ILogger log = null,
            Func<DateTime> now = null)
        {
            _handlers = handlers ?? new CampaignActionHandlers();
            _events = events;
            _onDispatched = onDispatched;
            _log = log ?? new NoctuaLogger(typeof(CampaignActionDispatcher));
            _now = now ?? (() => DateTime.UtcNow);
        }

        /// <inheritdoc />
        public void Dispatch(CampaignAction action, CampaignItem campaign)
        {
            if (action == null) return;

            try
            {
                // Ahead of EmitClick on purpose: a suppressed double-tap must not inflate
                // live_ops_campaign_click or re-fire OnCampaignClicked.
                if (action.Type == CampaignActionType.Purchase && IsPurchaseSuppressed())
                {
                    _log.Debug($"{LogTag} purchase tap ignored — one is already in flight");
                    return;
                }

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
                        HandlePurchase(action, campaign);
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

        /// <summary>
        /// True when this purchase tap should be dropped. The awaitable path knows exactly when
        /// one is running; the legacy path can only fall back to a time window.
        /// </summary>
        private bool IsPurchaseSuppressed()
        {
            if (_handlers.PurchaseAsync != null) return _purchaseInFlight;

            return (_now() - _lastSyncPurchaseAt).TotalMilliseconds < SyncPurchaseDebounceMs;
        }

        private void HandlePurchase(CampaignAction action, CampaignItem campaign)
        {
            if (string.IsNullOrEmpty(action.ProductId))
            {
                _log.Warning($"{LogTag} purchase action missing product_id in campaign '{campaign?.Id}'");
                return;
            }

            if (_handlers.PurchaseAsync != null)
            {
                RunPurchaseAsync(action.ProductId, campaign).Forget();
                return;
            }

            if (_handlers.Purchase == null)
            {
                _log.Warning($"{LogTag} no handler wired for 'purchase'");
                return;
            }

            _lastSyncPurchaseAt = _now();
            _handlers.Purchase(action.ProductId, campaign);
        }

        /// <summary>
        /// Runs the awaitable purchase hook, holding the busy state for its whole duration.
        /// Deliberately does not touch <see cref="CurrentDismiss"/> — the popup stays open so a
        /// failed or cancelled purchase returns the player to the offer.
        /// </summary>
        private async UniTaskVoid RunPurchaseAsync(string productId, CampaignItem campaign)
        {
            // Captured up front: this continuation can outlive the popup, and a later ShowPopup
            // may have repointed CurrentBusy by the time the finally runs — clearing the busy
            // state on a different popup than the one we set it on.
            var busy = CurrentBusy;

            _purchaseInFlight = true;

            try
            {
                busy?.Invoke(true);
                await _handlers.PurchaseAsync(productId, campaign);
            }
            catch (Exception e)
            {
                // The game's handler owns its own error UI; we only make sure the veil lifts.
                _log.Error($"{LogTag} purchase handler failed for '{productId}': {e.Message}");
            }
            finally
            {
                _purchaseInFlight = false;
                try { busy?.Invoke(false); }
                catch (Exception e) { _log.Warning($"{LogTag} busy callback threw: {e.Message}"); }
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
