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

        /// <summary>
        /// True when the campaign must not sell again (a <c>purchase_once</c> campaign already
        /// bought). A blocked tap starts no purchase, reports <see cref="CampaignActionDispatcher.StatusAlreadyPurchased"/>
        /// and calls <see cref="PurchaseBlocked"/>.
        /// </summary>
        public Func<CampaignItem, bool> IsPurchaseBlocked;

        /// <summary>Called when a purchase tap was blocked by <see cref="IsPurchaseBlocked"/>.</summary>
        public Action<CampaignItem> PurchaseBlocked;

        /// <summary>
        /// Called with the campaign id once a settled purchase is credited to that campaign's Buy
        /// tap (the same rule as <c>live_ops_campaign_purchase_result</c>).
        /// </summary>
        public Action<string, PurchaseOutcome> PurchaseCredited;

        /// <summary>
        /// Claims every completed mission of the campaign (a <c>claim_all</c> button). Awaited
        /// with the popup veiled; the popup stays open and refreshes from the new progress.
        /// </summary>
        public Func<CampaignItem, UniTask> ClaimAllAsync;
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

        /// <summary><c>status</c> on <see cref="PurchaseResultEvent"/> for a tap on a campaign the player already bought.</summary>
        public const string StatusAlreadyPurchased = "already_purchased";

        // Clock skew allowance: the tap and the purchase start are stamped by different classes.
        private const int AttributionSkewMs = 1000;

        private sealed class PendingPurchase
        {
            public string CampaignId;
            public string EngagementType;
            public string ButtonId;
            public DateTime TappedAtUtc;

            // When the game's awaitable purchase handler returned. Null while it runs, and on the
            // legacy fire-and-forget path, which has no end signal.
            public DateTime? HandlerEndedAtUtc;
        }

        /// <summary>
        /// Whether a purchase that started at <paramref name="startedAtUtc"/> is this tap's. With the
        /// awaitable handler: only one started while that handler ran, so a later purchase of the
        /// same SKU from the game's shop is never credited, however soon after. On the legacy event
        /// path, which gives no end signal: within <see cref="PurchaseAttributionWindowSeconds"/>.
        /// </summary>
        private static bool StartedInsideTap(PendingPurchase tap, DateTime startedAtUtc)
        {
            var sinceTap = startedAtUtc - tap.TappedAtUtc;
            if (sinceTap.TotalMilliseconds < -AttributionSkewMs) return false;

            // The handler's end and the purchase start are stamped by different classes: allow skew.
            if (tap.HandlerEndedAtUtc.HasValue)
            {
                return (startedAtUtc - tap.HandlerEndedAtUtc.Value).TotalMilliseconds <= AttributionSkewMs;
            }
            return sinceTap.TotalSeconds <= PurchaseAttributionWindowSeconds;
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
                    case CampaignActionType.ClaimAll:
                        HandleClaimAll(campaign);
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

        private bool _claimAllInFlight;

        private void HandleClaimAll(CampaignItem campaign)
        {
            if (_handlers.ClaimAllAsync == null)
            {
                _log.Warning($"{LogTag} no handler wired for 'claim_all'");
                return;
            }
            if (_claimAllInFlight)
            {
                _log.Debug($"{LogTag} claim all ignored — one is already running");
                return;
            }
            RunClaimAllAsync(campaign).Forget();
        }

        /// <summary>Runs the claim with the popup veiled, like a purchase; never throws.</summary>
        private async UniTaskVoid RunClaimAllAsync(CampaignItem campaign)
        {
            var busy = CurrentBusy;
            _claimAllInFlight = true;
            try
            {
                busy?.Invoke(true);
                await _handlers.ClaimAllAsync(campaign);
            }
            catch (Exception e)
            {
                _log.Error($"{LogTag} claim all failed for '{campaign?.Id}': {e.Message}");
            }
            finally
            {
                _claimAllInFlight = false;
                try { busy?.Invoke(false); }
                catch (Exception e) { _log.Warning($"{LogTag} busy callback threw: {e.Message}"); }
            }
        }

        /// <summary>
        /// The campaign whose Buy button is buying <paramref name="productId"/> right now, or null —
        /// the same rule that credits <see cref="PurchaseResultEvent"/> (<see cref="StartedInsideTap"/>).
        /// Lets an order carry its campaign without game code, and never tags an ordinary order.
        /// </summary>
        public string RecentPurchaseCampaign(string productId)
        {
            if (string.IsNullOrEmpty(productId) || !_pendingPurchases.TryGetValue(productId, out var tap)) return null;
            return StartedInsideTap(tap, _now()) ? tap.CampaignId : null;
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

            if (IsBlocked(campaign))
            {
                _log.Info($"{LogTag} campaign '{campaign?.Id}' was already bought — purchase of '{action.ProductId}' not started");
                _events?.Send(PurchaseResultEvent, BlockedPayload(action, campaign));
                try { _handlers.PurchaseBlocked?.Invoke(campaign); }
                catch (Exception e) { _log.Warning($"{LogTag} PurchaseBlocked threw: {e.Message}"); }
                return;
            }

            var tap = RememberPurchaseTap(action, campaign);

            if (_handlers.PurchaseAsync != null)
            {
                RunPurchaseAsync(action.ProductId, campaign, tap).Forget();
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
        private async UniTaskVoid RunPurchaseAsync(string productId, CampaignItem campaign, PendingPurchase tap)
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
                // Closes the tap: from now on a purchase of this SKU is the game's own, not the popup's.
                tap.HandlerEndedAtUtc = _now();
                _purchaseInFlight = false;
                try { busy?.Invoke(false); }
                catch (Exception e) { _log.Warning($"{LogTag} busy callback threw: {e.Message}"); }
            }
        }

        /// <summary>
        /// Reports a settled purchase. When it is for a SKU a campaign's Buy button asked for, and
        /// it started inside that tap (<see cref="StartedInsideTap"/>), emits
        /// <see cref="PurchaseResultEvent"/> once and credits the campaign. Anything else is ignored.
        /// Never throws.
        /// </summary>
        public void HandlePurchaseSettled(PurchaseOutcome outcome)
        {
            try
            {
                if (outcome == null || string.IsNullOrEmpty(outcome.ProductId)) return;
                if (!_pendingPurchases.TryGetValue(outcome.ProductId, out var tap)) return;

                // Started before the tap: an earlier purchase of this SKU finishing. Keep waiting.
                if ((outcome.StartedAtUtc - tap.TappedAtUtc).TotalMilliseconds < -AttributionSkewMs) return;

                _pendingPurchases.Remove(outcome.ProductId);

                // Started after the popup's purchase was over: the player bought it elsewhere.
                if (!StartedInsideTap(tap, outcome.StartedAtUtc)) return;

                _events?.Send(PurchaseResultEvent, PurchaseResultPayload(outcome, tap));

                try { _handlers.PurchaseCredited?.Invoke(tap.CampaignId, outcome); }
                catch (Exception e) { _log.Warning($"{LogTag} PurchaseCredited threw: {e.Message}"); }
            }
            catch (Exception e)
            {
                _log.Warning($"{LogTag} purchase result tracking failed: {e.Message}");
            }
        }

        private PendingPurchase RememberPurchaseTap(CampaignAction action, CampaignItem campaign)
        {
            var now = _now();
            PruneExpiredPurchaseTaps(now);
            var tap = new PendingPurchase
            {
                CampaignId = campaign?.Id ?? string.Empty,
                EngagementType = campaign?.EngagementType,
                ButtonId = action.Id,
                TappedAtUtc = now,
            };
            _pendingPurchases[action.ProductId] = tap;
            return tap;
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

        private bool IsBlocked(CampaignItem campaign)
        {
            try { return _handlers.IsPurchaseBlocked != null && _handlers.IsPurchaseBlocked(campaign); }
            catch (Exception e)
            {
                // Fail open: a broken check must never stop a sale.
                _log.Warning($"{LogTag} IsPurchaseBlocked threw: {e.Message}");
                return false;
            }
        }

        private static Dictionary<string, IConvertible> BlockedPayload(CampaignAction action, CampaignItem campaign)
        {
            var payload = new Dictionary<string, IConvertible>
            {
                { "campaign_id", campaign?.Id ?? string.Empty },
                { "product_id", action.ProductId },
                { "status", StatusAlreadyPurchased },
            };
            if (!string.IsNullOrEmpty(campaign?.EngagementType)) payload["engagement_type"] = campaign.EngagementType;
            if (!string.IsNullOrEmpty(action.Id)) payload["button_id"] = action.Id;
            return payload;
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
