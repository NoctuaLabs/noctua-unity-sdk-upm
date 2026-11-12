# SDK Categories — Cross-Layer Index

This is the single source of truth for "where does feature X live" across the layered SDK. The folder layout (Core / Model / Infrastructure / Presenter / Platform / UI / View) groups by **layer**; this index groups by **SDK category** (Auth, IAP, IAA, Event, Session, Platform, App, Debug & Testing).

All paths are relative to `Packages/com.noctuagames.sdk/Runtime/`.

---

## Auth — `Noctua.Auth`

User identity, login flows, account management, social authentication.

| Layer | Files |
|---|---|
| View | `View/Auth/NoctuaAuthentication.cs` |
| Presenter | `Presenter/Auth/NoctuaAuthenticationService.cs`, `Presenter/Auth/SocialAuthenticationService.cs`, `Presenter/Auth/AccountContainer.cs` |
| Presenter / Interfaces | `Presenter/Interfaces/IAuthenticationService.cs`, `Presenter/Interfaces/IAuthProvider.cs`, `Presenter/Interfaces/IAccountEvents.cs` |
| Model | `Model/Auth/AuthEntities.cs`, `Model/Auth/NativeAccount.cs`, `Model/Auth/FacebookConfig.cs` |
| UI / Controllers | `UI/Controllers/Auth/*.cs` (19 dialog presenters: AuthUIController, EmailLogin/Register/Reset/Verification, AccountSelection, BindConflict, UserCenter, Welcome, Switch/Logout/Delete confirmations, etc.) |
| View / Common (cross-cutting bridge) | `View/Common/LazyAuthProvider.cs` |
| Platform | _(none — auth uses Http only)_ |

---

## IAP — `Noctua.IAP`

In-app purchases: products, payments, subscriptions, redemption, pending deliverables.

| Layer | Files |
|---|---|
| View | `View/IAP/PaymentUIAdapter.cs` |
| Presenter | `Presenter/IAP/NoctuaIAPService.cs` (large — split into partials per concern), `Presenter/IAP/InternalPurchaseItem.cs` |
| Presenter / Interfaces | `Presenter/Interfaces/IIAPService.cs`, `Presenter/Interfaces/IPaymentUI.cs` |
| Model | `Model/IAP/IAPModels.cs`, `Model/IAP/PurchaseItem.cs`, `Model/IAP/NoctuaProductType.cs`, `Model/IAP/NoctuaConsumableType.cs`, `Model/IAP/ProductPurchaseStatus.cs` |
| UI / Controllers | `UI/Controllers/IAP/EditorPaymentSheetPresenter.cs`, `CustomPaymentCompleteDialogPresenter.cs`, `FailedPaymentDialogPresenter.cs`, `PendingPurchasesDialogPresenter.cs`, `PurchaseHistoryDialogPresenter.cs` |
| Platform | `Platform/Android/GoogleBilling.cs` (Google Play Billing JNI bridge) |

---

## IAA — `Noctua.IAA` (Ads)

Ad mediation, network adapters, revenue tracking, ad frequency, user segmentation for ads.

| Layer | Files |
|---|---|
| Presenter | `Presenter/IAA/MediationManager.cs` (large — split into partials), `Presenter/IAA/AppOpenAdManager.cs`, `Presenter/IAA/AdRevenueTrackingManager.cs`, `Presenter/IAA/AdFrequencyManager.cs`, `Presenter/IAA/AdExperimentManager.cs`, `Presenter/IAA/AdNetworkPerformanceTracker.cs`, `Presenter/IAA/CpmFloorManager.cs`, `Presenter/IAA/HybridAdOrchestrator.cs`, `Presenter/IAA/UserSegmentManager.cs` |
| Presenter / Interfaces | `Presenter/Interfaces/IAdRevenueTracker.cs`, `Presenter/Interfaces/IAdPlaceholderUI.cs` |
| AdsManager (network adapters — pre-existing category-coherent folder) | `AdsManager/AdConstants.cs`, `AdsManager/AdTestUnitIds.cs`, `AdsManager/AdWatchMilestoneTracker.cs`, `AdsManager/IAAEventNames.cs`, `AdsManager/IAdNetwork.cs`, `AdsManager/Admob/*.cs`, `AdsManager/AppLovin/*.cs`, `AdsManager/AdPlaceholder/PlaceholderAssetSource.cs` |
| UI / Controllers | `UI/Controllers/IAA/NoctuaAdPlaceholder.cs` |

---

## Event — `Noctua.Event`

Analytics events, batching, persistence, network flush, A/B test session tags.

| Layer | Files |
|---|---|
| Presenter | `Presenter/Event/NoctuaEventService.cs`, `Presenter/Event/ExperimentManager.cs` |
| Presenter / Interfaces | `Presenter/Interfaces/IEventSender.cs` |
| Infrastructure | `Infrastructure/Event/EventSender.cs` (large — split into partials) |
| Model | `Model/Event/NativeEvent.cs`, `Model/Event/AdjustConfig.cs`, `Model/Event/NoctuaAdjustAttribution.cs` |

---

## Session

Session lifecycle, foreground engagement tracking, heartbeat, crash forwarding, observer registry.

| Layer | Files |
|---|---|
| Presenter | `Presenter/Session/SessionTracker.cs`, `Presenter/Session/SessionTrackerBehaviour.cs`, `Presenter/Session/NativeSessionTracker.cs`, `Presenter/Session/NativeSessionTrackerBehaviour.cs`, `Presenter/Session/NativeCrashForwarder.cs` |
| Platform | `Platform/Android/AndroidNativeCrashReporter.cs`, `Platform/iOS/IosCrashReporter.cs` |

---

## Platform — `Noctua.Platform`

Locale, connectivity, content (announcements, customer service, social media, rewards), native plugin bridge.

| Layer | Files |
|---|---|
| View | `View/Platform/NoctuaPlatform.cs`, `View/Platform/NoctuaLocale.cs`, `View/Platform/NoctuaConnectivityProvider.cs`, `View/Platform/NoctuaWebContent.cs` |
| Core | `Core/IConnectivityProvider.cs`, `Core/ILocaleProvider.cs` |
| Model | `Model/Platform/CountryData.cs` |
| Platform | `Platform/INativePlugin.cs`, `Platform/Android/AndroidPlugin.cs`, `Platform/iOS/IosPlugin.cs`, `Platform/Editor/DefaultNativePlugin.cs` |
| UI / Controllers | `UI/Controllers/Platform/WebContentPresenter.cs` |

---

## App Management

App lifecycle, init, remote configs, Firebase, PlayerPrefs, push, deep links, in-app review, app updates, web content for the app shell.

| Layer | Files |
|---|---|
| View | `View/App/NoctuaAppManager.cs`, `View/App/Noctua.Firebase.cs`, `View/App/Noctua.PlayerPrefs.cs` |
| Presenter | `Presenter/App/NoctuaGameService.cs` (init game / geo-IP / remote configs) |
| Model | `Model/App/AppUpdateInfo.cs`, `Model/App/FirebaseConfig.cs`, `Model/App/GameServiceModels.cs`, `Model/App/NoctuaNotificationPayload.cs` |

---

## LiveOps Campaign — `Noctua.LiveOps.Campaign`

Server-driven engagement popups (purchase / event-deeplink CTAs): config merge, eligibility (schedule, targeting, frequency, offline asset cache), rendering, action dispatch. Self-contained module under `LiveOpsCampaignManager/`.

| Layer | Files |
|---|---|
| Game facade | `Noctua.LiveOps` (static class nested in `View/Noctua.cs`, one property per live ops feature) + `View/LiveOps/NoctuaCampaign.cs` (`Noctua.LiveOps.Campaign`: never null, usable before init — `Configure`, popups, missions, events; `Attach`/`Fail` from `InitCampaigns`; `Module` for diagnostics). The old `Noctua.LiveOpsCampaign` is `[Obsolete]`. |
| Module | `LiveOpsCampaignManager/NoctuaLiveOpsCampaign.cs` (events, `RegisterDeeplinkHandler`, `SetPurchaseHandler`, `ShowPopup(id, playerData)`, `UpdatePopupData`, local store prices for `{{store_price.<sku>}}`, purchase state: `SetOwnershipProvider`, fully-owned offers hidden, owned Buy buttons shown as Purchased (`data.purchased_label`, `LocaleTextKey.LiveOpsCampaignPurchased`), `{{owned.<sku>}}` for custom designs) |
| Logic | `CampaignManager.cs`, `CampaignValidator.cs`, `CampaignConditions.cs` (`visible_if`), `CampaignPlayerData.cs` (game-supplied `player_data`), `CampaignFrequencyGate.cs`, `CampaignActionDispatcher.cs`, `CampaignTokens.cs`, `CampaignAssetSource.cs`, `CampaignFontSource.cs` |
| Render | `CampaignRenderer.cs`, `CampaignRenderer.Widgets.cs`, `CampaignStyleMapper.cs`, `CampaignRuntimeController.cs`, `CampaignUIHost.cs` |
| UI | `UI/CampaignPopupPresenter.cs`, `UI/CampaignPopupFit.cs`, `UI/CampaignFrameStyle.cs`, `UI/CampaignBackdropStyle.cs`, `UI/Resources/CampaignPopup.uxml`, `UI/Resources/Campaign.uss` |
| Model | `Model/CampaignConfig.cs`, `Model/CampaignItem.cs`, `Model/CampaignNode.cs`, `Model/CampaignAction.cs`, `Model/CampaignCondition.cs`, `Model/CampaignTargeting.cs`, `Model/CampaignCloseButton.cs`, `Model/CampaignStyleProps.cs`, `Model/CampaignImageSrc.cs` |
| Wiring | `View/Noctua.Initialization.cs` → `InitCampaigns` |
| Tests | `Tests/Runtime/Campaign/` |

### LiveOps Progress — on `Noctua.LiveOps.Campaign`

Client for the live-ops-progress-tracker service (per-player mission progress: read, set, claim), folded into the campaign facade. Needs a logged-in player. Talks to `https://liveops-progress-tracker.noctuaprojects.com/api/v1` (`NoctuaConfig.DefaultProgressTrackerBaseUrl`, built in — not in `noctuagg.json`), so the facade is alive after init even for games with no campaigns.

When the tracker is down nothing blocks the game: loads fall back to the player's saved copy (≤ 24 h, `IsProgressStale`), updates wait on disk and are retried with backoff (`LiveOpsProgress.Pending`; safe because the tracker only raises values), claims are online only and never queued. Daily missions keep progress per campaign: tracker key `<progress_scope>.<mission key>` (`TrackerKey`; `progress_scope` is the admin campaign cuid, the DAG registers the keys) — the game calls `SetMissionProgressAsync(mission, value)` (every campaign with that mission) and `ClaimMissionAsync(campaignId, mission)`. A daily missions popup (declares `m_<id>_progress`) is never shown with made-up zeros: with no copy an auto-show is skipped and a player-opened one raises `OnCampaignUnavailable` + a translated notice.

| Layer | Files |
|---|---|
| Facade | `LiveOpsCampaignManager/NoctuaLiveOpsCampaign.Progress.cs` (partial: `GetProgressAsync`, `SetProgressAsync`, `ClaimProgressAsync`, `SetMissionProgressAsync`, `ClaimMissionAsync`, `TrackerKey`, `FlushPendingProgressAsync`, `Progress`, `IsProgressStale`, `HasPendingProgress`, `OnProgressChanged`, `OnCampaignUnavailable`, `ToDailyMissionsPlayerData`; saved copy + pending queue in PlayerPrefs `Noctua.LiveOpsCampaign.Progress.<playerId>`) |
| Popup gating | `LiveOpsCampaignManager/NoctuaLiveOpsCampaign.cs` (`ShowPopup`, `RunAutoShowAsync`, `ShownPlayerData`) |
| Facade | `LiveOpsCampaignManager/NoctuaLiveOpsCampaign.Setup.cs` (partial: `CampaignHandlers` + `Configure` — the game's deeplink / purchase / product resolver / ownership / mission rewards handlers in one call; applied by `Noctua.LiveOps.Campaign`) |
| Facade | `LiveOpsCampaignManager/NoctuaLiveOpsCampaign.Missions.cs` (partial: SDK claims — `ClaimMissionRewardAsync`, `ClaimMilestoneRewardAsync`, `ClaimAllMissionsAsync` (missions, then the chests they unlock), `ClaimableMissions` / `ClaimableMilestones`, points per mission (`PointsOf`, `m_<key>_points`), chest tracker keys `<scope>.milestone_<points>`, `ToMilestonePlayerData` (`points`, `milestone_<p>_claimed`, `claimable_count`), the built-in `claim` (mission or milestone) / `claim_all` actions) |
| Facade | `LiveOpsCampaignManager/NoctuaLiveOpsCampaign.Rewards.cs` (partial: the shared `Rewards` ledger — `LiveOpsReward` / `RewardItem` (`Model/LiveOpsReward.cs`), saved per player with a claim-id history and redelivered until the handler finishes (`DeliverRewardsAsync`); campaign bundle purchases — `IsDeliveredByRewards` marker `noctua_delivery`, `RecordPurchaseReward` (also for another account's purchase); the facade keeps orders verified before init in a device inbox) |
| Facade | `LiveOpsCampaignManager/NoctuaLiveOpsCampaign.Purchase.cs` (partial: one purchase per campaign — `purchase_once`, tracker key `<id>.purchase`, `IsCampaignPurchased(Async)`, `RecordCampaignPurchaseAsync`, `OnCampaignPurchaseRecorded`, waiting claims in the progress save; dynamic products — `product_key` bundles, `SetProductResolver`, `GetProductBundle(ForSku)`, `WithDynamicProducts`, `PurchaseExtra` for the order's `extra`) |
| Model | `LiveOpsCampaignManager/Model/LiveOpsProgress.cs`; error codes `NoctuaErrorCode.LiveOpsProgress*` (2300–2402); notice key `LocaleTextKey.LiveOpsMissionsUnavailable` |
| HTTP | `Infrastructure/Network/Http.cs` → `WithErrorEnvelope()` (keeps codes on 409 / 410 / 422), `WithTimeout()` (5 s for progress) |
| Wiring | `View/Noctua.Initialization.cs` → `InitCampaigns` (`OnAccountChangedAsync`); `View/Noctua.cs` → `OnOnline` / `IsOfflineAsync` (`OnConnectivityRestoredAsync`) |
| Tests | `Tests/Runtime/Campaign/LiveOpsProgressTest.cs`, `LiveOpsProgressOfflineTest.cs`, `LiveOpsProgressPopupTest.cs`, `LiveOpsProgressMissionTest.cs` (`LiveOpsProgressTestKit.cs`) |

---

## Debug & Testing

Inspector tooling, tracker debug monitor, HTTP inspection hooks, JSON/cURL exporters.

| Layer | Files |
|---|---|
| Inspector (top-level — pre-existing category-coherent folder) | `Inspector/NoctuaInspectorController.cs` (large — split into partials), `Inspector/InspectorExporter.cs`, `Inspector/InspectorTrigger.cs`, `Inspector/CurlExporter.cs`, `Inspector/FirebaseProjectLookup.cs` |
| Presenter | `Presenter/Debug/TrackerDebugMonitor.cs`, `Presenter/Debug/TrackerEmission.cs`, `Presenter/Debug/TrackerObserverRegistry.cs`, `Presenter/Debug/InspectorJson.cs` |
| Presenter / Interfaces | `Presenter/Interfaces/IHttpObserver.cs`, `Presenter/Interfaces/ITrackerObserver.cs` |
| Infrastructure | `Infrastructure/Debug/HttpInspectorHooks.cs`, `Infrastructure/Debug/HttpInspectorLog.cs` |
| Tests (separate asmdef) | `Tests/Runtime/*.cs` |

---

## Common / Shared (cross-cutting, no single category)

| Layer | Files |
|---|---|
| Composition root | `View/Noctua.cs`, `View/Noctua.Initialization.cs`, `AssemblyInfo.cs` |
| Core | `Core/Logging/Log.cs` |
| Model | `Model/Common/NoctuaConfig.cs`, `Model/Common/GlobalConfig.cs`, `Model/Common/CoPublisherConfig.cs`, `Model/Common/NoctuaException.cs`, `Model/Common/RawJsonStringConverter.cs` |
| Infrastructure | `Infrastructure/Common/Utility.cs`, `Infrastructure/Common/MobileDateTimePicker.cs`, `Infrastructure/Network/Http.cs`, `Infrastructure/Network/HttpExchange.cs`, `Infrastructure/Network/InternetChecker.cs`, `Infrastructure/Storage/AccessTokenProvider.cs` |
| UI scaffolding (cross-category) | `UI/UIFactory.cs`, `UI/UIUtility.cs`, `UI/BasePresenter.cs`, `UI/Spinner.cs`, `UI/ColorModule.cs`, `UI/ScreenRotationMonitor.cs`, `UI/Controllers/Common/*.cs` (LoadingProgress, RetryDialog, GeneralNotification) |

---

## Maintenance

When adding a file:

1. Place it under the correct **layer** folder per the layered architecture rules in `CLAUDE.md`.
2. Place it inside the matching **category** subfolder for that layer.
3. **Update this file** — add the new file to the relevant category section.
4. If the new file doesn't fit any existing category, prefer adding to `Common/` over creating a one-file new category.

When the index drifts from reality, run the audit grep from `CLAUDE.md` (every `.cs` file under `Runtime/` excluding `Plugins/` should appear here at least once).
