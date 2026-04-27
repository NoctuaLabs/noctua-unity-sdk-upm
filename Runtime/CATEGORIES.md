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
| Model | `Model/App/AppUpdateInfo.cs`, `Model/App/FirebaseConfig.cs`, `Model/App/GameServiceModels.cs` |

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
