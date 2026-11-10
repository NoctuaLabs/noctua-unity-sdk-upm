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

        /// <summary><c>reason</c> on <c>live_ops_campaign_dismiss</c>: the shell's ✕ close button.</summary>
        public const string DismissReasonCloseButton = "close_button";

        /// <summary><c>reason</c> on <c>live_ops_campaign_dismiss</c>: a <c>dismiss</c> CTA was tapped.</summary>
        public const string DismissReasonCtaDismiss = "cta_dismiss";

        /// <summary><c>reason</c> on <c>live_ops_campaign_dismiss</c>: a <c>deeplink</c> CTA navigated away.</summary>
        public const string DismissReasonCtaDeeplink = "cta_deeplink";

        /// <summary><c>reason</c> on <c>live_ops_campaign_dismiss</c>: the game called <c>ClosePopup()</c>.</summary>
        public const string DismissReasonProgrammatic = "programmatic";

        /// <summary>
        /// Analytics event fired when a purchase started from a campaign's Buy button settles —
        /// links the campaign to the purchase's status and order id.
        /// </summary>
        public const string PurchaseResultEvent = "live_ops_campaign_purchase_result";

        /// <summary>
        /// A purchase is credited to a campaign tap only if <c>PurchaseItemAsync</c> was called
        /// within this many seconds after the tap — room for the game's own confirm dialog, but a
        /// later purchase of the same SKU from the game's shop is not credited to the campaign.
        /// </summary>
        public const int PurchaseAttributionWindowSeconds = 120;

        // Clock skew allowance: the tap and the purchase start are stamped by different classes.
        private const int AttributionSkewMs = 1000;

        private sealed class PendingPurchase
        {
            public string CampaignId;
            public string EngagementType;
            public string ButtonId;
            public DateTime TappedAtUtc;
        }

        // Latest campaign tap per SKU still waiting for its purchase to settle.
        private readonly Dictionary<string, PendingPurchase> _pendingPurchases =
            new Dictionary<string, PendingPurchase>(StringComparer.Ordinal);

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

        /// <summary>
        /// Why this dispatcher is closing the popup — non-null only while its own
        /// <see cref="CurrentDismiss"/> call runs. The popup's close callback fires inside that
        /// call, so the facade reads it there to tag <c>live_ops_campaign_dismiss</c>; any other
        /// close sees null.
        /// </summary>
        public string ActiveDismissReason { get; private set; }

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
                        // The reason also covers the handler: games are told to call ClosePopup()
                        // from inside it, and that close still came from this CTA tap.
                        WithDismissReason(DismissReasonCtaDeeplink, () =>
                        {
                            Invoke(_handlers.Deeplink, action.Deeplink, "deeplink");
                            // A deeplink navigates the player away — close the popup behind them,
                            // unless it is an in-place action such as a claim.
                            if (!action.KeepOpen) CurrentDismiss?.Invoke();
                        });
                        break;
                    case CampaignActionType.Purchase:
                        HandlePurchase(action, campaign);
                        break;
                    case CampaignActionType.Dismiss:
                        WithDismissReason(DismissReasonCtaDismiss, () => CurrentDismiss?.Invoke());
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

            RememberPurchaseTap(action, campaign);

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

        /// <summary>
        /// Reports a settled purchase. When it is for a SKU a campaign's Buy button asked for, and
        /// the purchase started within <see cref="PurchaseAttributionWindowSeconds"/> of that tap,
        /// emits <see cref="PurchaseResultEvent"/> once. Anything else is ignored. Never throws.
        /// </summary>
        public void HandlePurchaseSettled(PurchaseOutcome outcome)
        {
            try
            {
                if (outcome == null || string.IsNullOrEmpty(outcome.ProductId)) return;
                if (!_pendingPurchases.TryGetValue(outcome.ProductId, out var tap)) return;

                var sinceTapMs = (outcome.StartedAtUtc - tap.TappedAtUtc).TotalMilliseconds;

                // Started before the tap: an earlier purchase of this SKU finishing. Keep waiting.
                if (sinceTapMs < -AttributionSkewMs) return;

                _pendingPurchases.Remove(outcome.ProductId);

                // Started long after the tap: the player bought it somewhere else later.
                if (sinceTapMs > PurchaseAttributionWindowSeconds * 1000.0) return;

                _events?.Send(PurchaseResultEvent, PurchaseResultPayload(outcome, tap));
            }
            catch (Exception e)
            {
                _log.Warning($"{LogTag} purchase result tracking failed: {e.Message}");
            }
        }

        private void RememberPurchaseTap(CampaignAction action, CampaignItem campaign)
        {
            var now = _now();
            PruneExpiredPurchaseTaps(now);
            _pendingPurchases[action.ProductId] = new PendingPurchase
            {
                CampaignId = campaign?.Id ?? string.Empty,
                EngagementType = campaign?.EngagementType,
                ButtonId = action.Id,
                TappedAtUtc = now,
            };
        }

        // Bounds the map: a tap whose purchase never started (custom store, game ignored it) expires.
        private void PruneExpiredPurchaseTaps(DateTime now)
        {
            if (_pendingPurchases.Count == 0) return;

            var expired = new List<string>();
            foreach (var pair in _pendingPurchases)
            {
                if ((now - pair.Value.TappedAtUtc).TotalSeconds > PurchaseAttributionWindowSeconds) expired.Add(pair.Key);
            }
            foreach (var key in expired) _pendingPurchases.Remove(key);
        }

        private static Dictionary<string, IConvertible> PurchaseResultPayload(PurchaseOutcome outcome, PendingPurchase tap)
        {
            var payload = new Dictionary<string, IConvertible>
            {
                { "campaign_id", tap.CampaignId },
                { "product_id", outcome.ProductId },
                { "status", outcome.Status },
            };

            if (!string.IsNullOrEmpty(tap.EngagementType)) payload["engagement_type"] = tap.EngagementType;
            if (!string.IsNullOrEmpty(tap.ButtonId)) payload["button_id"] = tap.ButtonId;
            if (outcome.OrderId > 0) payload["order_id"] = outcome.OrderId;
            if (outcome.ErrorCode != 0) payload["error_code"] = outcome.ErrorCode;

            return payload;
        }

        /// <summary>
        /// Runs <paramref name="body"/> with <paramref name="reason"/> visible through
        /// <see cref="ActiveDismissReason"/>, and only for that long, so a stale reason can never
        /// leak onto a later close-button or programmatic close.
        /// </summary>
        private void WithDismissReason(string reason, Action body)
        {
            ActiveDismissReason = reason;
            try { body(); }
            finally { ActiveDismissReason = null; }
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
            var payload = new Dictionary<string, IConvertible>
            {
                { "campaign_id", campaign?.Id ?? string.Empty },
                { "action_type", action.Type.ToString() },
            };

            // Lets reporting split clicks by purchase offers vs event announcements without a join.
            if (!string.IsNullOrEmpty(campaign?.EngagementType))
            {
                payload["engagement_type"] = campaign.EngagementType;
            }

            // The route tells reporting which card in a multi-card popup was tapped.
            if (action.Type == CampaignActionType.Deeplink && !string.IsNullOrEmpty(action.Deeplink))
            {
                payload["deeplink"] = action.Deeplink;
            }

            // Which SKU was tapped — a multi-offer popup buys a different one per card.
            if (action.Type == CampaignActionType.Purchase && !string.IsNullOrEmpty(action.ProductId))
            {
                payload["product_id"] = action.ProductId;
            }

            // The admin-assigned button id (e.g. offer_1_btn_0), stable across edits to the copy.
            if (!string.IsNullOrEmpty(action.Id))
            {
                payload["button_id"] = action.Id;
            }

            _events?.Send(ClickEvent, payload);
        }
    }
}
