# Noctua SDK — Unity UPM Package

SDK package at `Packages/com.noctuagames.sdk/`. All runtime code under `Runtime/`, Editor tooling under `Editor/`, tests under `Tests/`.

- **Version:** 0.126.0
- **Repo:** `gitlab.com/evosverse/noctua/noctua-sdk-unity-upm`
- **Namespace:** `com.noctuagames.sdk`

## Layered Architecture

Dependencies flow **downward only**:

```
View  →  Presenter  →  Infrastructure  →  Core / Model
  ↓         ↓               ↓
Platform   UI          (no upward deps)
```

```
Runtime/
├── Core/           Shared interfaces (ILocaleProvider, IConnectivityProvider)
├── Model/          DTOs/ and Entities/ — pure data, no logic
├── Infrastructure/ EventSender, Http, InternetChecker, Storage, Utility
├── Presenter/      NoctuaIAPService, NoctuaAuthenticationService, SessionTracker
│   └── Interfaces/ IEventSender, IIAPService, IAuthProvider, IPaymentUI, etc.
├── Platform/       Native bridges: Android/ (JNI), iOS/ (P/Invoke)
├── UI/             UIElements controllers, UIUtility
├── View/           Composition root — Noctua.cs, Noctua.Initialization.cs, facades
├── AdsManager/     MediationManager, AppLovin, AdMob, AdPlaceholder
└── Plugins/        Third-party plugins (UniWebView, etc.)
```

| Rule |
|------|
| Infrastructure must NOT depend on Presenter, UI, View, or `Noctua` static class |
| Presenter must NOT depend on UI, View, or `Noctua` static class |
| UI must NOT depend on View or `Noctua` static class |
| Core/Model — no dependencies on other SDK layers |
| View (composition root) — may depend on all layers |

## Class Visibility Rule

**All SDK classes must be `public`, never `internal`.** Tests (`com.noctuagames.sdk.Tests`) and game developers need access. `internal` causes CS0122 errors. Do NOT use `InternalsVisibleTo` — just make the class `public`.

## Dependency Injection Patterns

| Pattern | Example | When |
|---------|---------|------|
| Constructor injection | `NoctuaIAPService(config, accessTokenProvider, ...)` | Primary — all services |
| Config object injection | `EventSenderConfig.IsOfflineModeFunc` | Adding deps to existing classes |
| Static delegate injection | `MobileDateTimePicker.SetShowDatePickerAction(action)` | Static classes or many call sites |
| Static provider injection | `HttpRequest.SetLocaleProvider(locale)` | Cross-cutting concerns |
| Setter injection | `MediationManager.SetAdRevenueTracker(tracker)` | Circular init deps |
| Interface adapter | `NoctuaConnectivityProvider : IConnectivityProvider` | Wrapping `Noctua.*` for injection |

**Lazy<T> re-entry:** `Noctua()` constructor runs inside `Lazy<Noctua>`. Never call `Noctua.Instance.Value` during construction — causes `ValueFactory re-entry`. All deps use delegates/interfaces/config fields, never `Noctua.*` static calls.

## Key Files

| File | Layer | Purpose |
|------|-------|---------|
| `Runtime/View/Noctua.cs` | View | Public API entry point — static facade + lifecycle |
| `Runtime/View/Noctua.Initialization.cs` | View | Composition root — wires all services |
| `Runtime/View/App/Noctua.Adjust.cs` | View | Adjust attribution & device info API (ADID, IDFA, IDFV, Google/Amazon Ad ID, SDK version) |
| `Runtime/View/App/Noctua.Firebase.cs` | View | Firebase Remote Config, FCM token, push notification events |
| `Runtime/View/App/Noctua.PlayerPrefs.cs` | View | PlayerPrefs backup/restore utilities |
| `Runtime/View/App/NoctuaAppManager.cs` | View | In-app review & flexible app updates |
| `Runtime/View/Auth/NoctuaAuthentication.cs` | View | Auth facade |
| `Runtime/View/Platform/NoctuaLocale.cs` | View | `ILocaleProvider` impl |
| `Runtime/View/Platform/NoctuaConnectivityProvider.cs` | View | `IConnectivityProvider` adapter |
| `Runtime/View/Common/LazyAuthProvider.cs` | View | Deferred `IAuthProvider` for circular init |
| `Runtime/View/Common/NoctuaDeviceMetricsAdapter.cs` | View | Wraps `INativePlugin` → `IDeviceMetricsProvider` |
| `Runtime/Presenter/IAP/NoctuaIAPService.cs` | Presenter | IAP: purchases, products, payment types |
| `Runtime/Presenter/Auth/NoctuaAuthenticationService.cs` | Presenter | Auth: social login, account mgmt |
| `Runtime/Presenter/Session/SessionTracker.cs` | Presenter | Session lifecycle, heartbeat, engagement |
| `Runtime/Presenter/Event/ExperimentManager.cs` | Presenter | A/B testing, session tags |
| `Runtime/Presenter/Event/NoctuaEventService.cs` | Presenter | High-level event tracking API |
| `Runtime/Infrastructure/Event/EventSender.cs` | Infra | Event storage, batching, HTTP flush |
| `Runtime/Infrastructure/Network/Http.cs` | Infra | `HttpRequest` with JSON serialization |
| `Runtime/Infrastructure/Common/Utility.cs` | Infra | Validation, parsing, retry helpers |
| `Runtime/Infrastructure/Common/MobileDateTimePicker.cs` | Infra | Date picker bridge (static delegate) |
| `Runtime/Platform/INativePlugin.cs` | Platform | Aggregate of all native sub-interfaces |
| `Runtime/Platform/iOS/IosPlugin.cs` | Platform | iOS P/Invoke declarations + trampolines |
| `Runtime/Platform/Android/AndroidPlugin.cs` | Platform | Android JNI bridge via `AndroidJavaClass` |
| `Runtime/Plugins/iOS/NoctuaInterop.h` | Platform | C function declarations for iOS native bridge |
| `Runtime/Plugins/iOS/NoctuaInterop.m` | Platform | C function implementations → `Noctua.swift` |
| `Editor/Menu/NoctuaSDKMenu.cs` | Editor | Integration Manager window |
| `Editor/Build/CocoaPodsConflictFixer.cs` | Editor | CocoaPods conflict auto-fixer |
| `Editor/Build/BuildPostProcessor.cs` | Editor | iOS/Android post-build processing |

## Key Interfaces

| Interface | Location | Purpose |
|-----------|----------|---------|
| `IEventSender` | `Presenter/Interfaces/` | Event tracking abstraction |
| `IIAPService` | `Presenter/Interfaces/` | IAP operations |
| `IAuthenticationService` | `Presenter/Interfaces/` | Token retrieval (narrow) |
| `IAuthProvider` | `Presenter/Interfaces/` | Decouples IAP from `Noctua.Auth` |
| `IPaymentUI` | `Presenter/Interfaces/` | Payment dialog |
| `IAdRevenueTracker` | `Presenter/Interfaces/` | Ad revenue tracking |
| `ILocaleProvider` | `Core/` | Language, country, currency, translations |
| `IConnectivityProvider` | `Core/` | Offline check, init status |
| `INativePlugin` | `Platform/` | Aggregate of all native sub-interfaces (see below) |

### `INativePlugin` sub-interfaces (`Runtime/Platform/INativePlugin.cs`)

| Sub-interface | Key methods | Notes |
|---|---|---|
| `INativeLifecycle` | `Init`, `OnApplicationPause`, `DisposeStoreKit`, `IsStoreKitReady` | App lifecycle |
| `INativeTracker` | `TrackAdRevenue`, `TrackPurchase`, `TrackCustomEvent`, `OnOnline`, `OnOffline` | Analytics |
| `INativeIAP` | `QueryProductDetails`, `Purchase`, `QueryPurchases`, `RestorePurchases`, etc. | In-app purchases |
| `INativeAccountStore` | `PutAccount`, `GetAllAccounts`, `GetSingleAccount`, `DeleteAccount` | Account persistence |
| `INativeFirebase` | `GetFirebaseInstallationID`, `GetFirebaseAnalyticsSessionID`, `GetAdjustAttribution`, `GetAdjust*` | Firebase + Adjust |
| `INativeEventStorage` | `SaveEvents`, `GetEvents`, `DeleteEvents`, `InsertEvent`, `GetEventsBatch`, etc. | Event persistence |
| `INativeDatePicker` | `ShowDatePicker`, `CloseDatePicker` | Native date picker |
| `INativeAppManagement` | `RequestInAppReview`, `CheckForUpdate`, `StartImmediateUpdate`, `StartFlexibleUpdate` | App updates |
| `INativeLogStream` | `SetLogStreamEnabled`, `RegisterNativeLogCallback` | Inspector Logs tab |
| `INativeDeviceMetrics` | `SnapshotDeviceMetrics` | Inspector Memory tab |
| `INativeBuildInfo` | `GetNativeSdkVersion`, `GetFirebaseProjectId`, etc. | Inspector Build tab |
| `INativeMaintenance` | `ClearNativeHttpCache` | Cache management |

## Logging Convention

**NEVER use `Debug.Log`/`Debug.LogWarning`/`Debug.LogError` in SDK runtime code.** Use `NoctuaLogger`:

```csharp
// Instance — non-static methods
private readonly ILogger _log = new NoctuaLogger(typeof(MyClass));
_log.Debug("msg"); _log.Info("msg"); _log.Warning("msg"); _log.Error("msg");

// Static — for MonoPInvokeCallbacks and static methods
private static readonly ILogger _sLog = new NoctuaLogger(typeof(MyClass));
```

In `Noctua.cs` static methods: `var log = Instance.Value._log;` — NOT `_log` directly.

**Exception:** `BuildPostProcessor.cs` uses `Debug.Log` via `Log()`/`LogError()`/`LogWarning()` wrappers — acceptable for Editor-only code.

## Native Plugin Bridges

**iOS chain:** `IosPlugin.cs` → P/Invoke `[DllImport("__Internal")]` → `NoctuaInterop.m` → `Noctua.swift`

**Android chain:** `AndroidPlugin.cs` → `AndroidJavaClass`/`AndroidJavaObject` JNI → `Noctua.kt`

**Static callback pitfall (iOS):** `IosPlugin.cs` uses single static callback fields. Concurrent async calls overwrite the pending callback — only last one completes. Use caching instead of per-call async fetching (see `EventSender.cs`).

### Adding a new native method

Follow this checklist (use `GetAdjustAttribution` as reference):

1. **`NoctuaInterop.h`** — declare C function + typedef delegate
2. **`NoctuaInterop.m`** — implement C function calling `[Noctua methodWithCompletion:]`
3. **`INativePlugin.cs`** — add method signature to the correct sub-interface
4. **`IosPlugin.cs`** — add `[DllImport]`, static field, delegate type, `[MonoPInvokeCallback]` trampoline, interface impl
5. **`AndroidPlugin.cs`** — add JNI impl via `noctua.Call("methodName", new AndroidCallback<T>(callback))`; return empty/default on unsupported platform
6. **`Noctua.Adjust.cs`** (or appropriate partial) — add public `async Task<T>` wrapping `TaskCompletionSource<T>`

## Adjust Device Info (`Runtime/View/App/Noctua.Adjust.cs`)

All Adjust public API lives here. `GetAdjustAttribution` was moved here from `Noctua.Firebase.cs`.

| Method | Return | iOS | Android |
|---|---|---|---|
| `GetAdjustAttributionAsync()` | `Task<NoctuaAdjustAttribution>` | ✅ | ✅ |
| `GetAdjustAdidAsync()` | `Task<string>` | ✅ | ✅ |
| `GetAdjustIdfaAsync()` | `Task<string>` | ✅ | ❌ `""` |
| `GetAdjustIdfvAsync()` | `Task<string>` | ✅ | ❌ `""` |
| `GetAdjustGoogleAdIdAsync()` | `Task<string>` | ❌ `""` | ✅ |
| `GetAdjustAmazonAdIdAsync()` | `Task<string>` | ❌ `""` | ✅ |
| `GetAdjustSdkVersionAsync()` | `Task<string>` | ✅ | ✅ |

All methods use `NativeStringCallAsync()` helper (private, in `Noctua.Adjust.cs`) — avoids repeating `TaskCompletionSource` boilerplate. Platform-only methods return `string.Empty` silently on the unsupported platform.

## Editor Tooling

### Noctua Integration Manager (`Editor/Menu/NoctuaSDKMenu.cs`)

Open via **Noctua > Noctua Integration Manager**. Manages ad SDK UPM packages directly via `Packages/manifest.json`.

**Sections:**
- **Recommended Setup** — pre-validated 6-package AppLovin MAX + AdMob combination; one-click install; covers both Android and iOS without version conflicts
- **IAA Providers** — AppLovin MAX SDK + AdMob SDK
- **AppLovin MAX — Ad Network Adapters** — 22 adapters from `unity.packages.applovin.com`
- **AdMob — Mediation Adapters** — 17 adapters from `package.openupm.com`

**Version encoding for AppLovin UPM adapters:** `25010000.0.0` = adapter `25.1.0.0` (wraps GMA Android 25.1.0). First numeric segment encodes underlying SDK version.

**Auto-resolve:** All install/update/remove actions call `Client.Resolve()` inside `WriteManifest()` — UPM resolves immediately, no manual editor refresh needed.

**Adding a new adapter to the catalog:**
1. Add entry to `maxAdapterPackages` (Android + iOS pkg/ver tuple) or `admobAdapterPackages`
2. Version must be verified stable against current MAX SDK (8.6.1) or AdMob SDK (11.0.0)
3. For AppLovin adapters: versions come from `unity.packages.applovin.com/-/all`
4. For AdMob adapters: versions come from `package.openupm.com`

**Conflict-free compatibility (Recommended Setup):**
- `com.google.ads.mobile` 11.0.0 → GMA iOS `~> 13.0.0` (allows any 13.x)
- AppLovin Google adapter 13020000.0.0 → requires GMA iOS 13.2.0 → satisfied by `~> 13.0.0`
- GMA Android: AdMob 11.0.0 declares 25.0.0; Gradle resolves to 25.1.0 (adapter target) — safe, same major series

### CocoaPods Conflict Fixer (`Editor/Build/CocoaPodsConflictFixer.cs`)

Menu: **Noctua > iOS > Fix CocoaPods Conflicts** (greyed out unless build target is iOS).

- Auto-warns at Editor startup when iOS build target is active and conflict detected
- Patches `GoogleMobileAdsDependencies.xml` in `Library/PackageCache` to align constraint with installed adapter version
- Removes duplicate `~/.cocoapods/repos/cocoapods` repo that causes pod install failures
- Dynamic detection: reads adapter versions from `Library/PackageCache` — no hardcoded versions
- Detects **7+ cross-catalog conflicts** (AppLovin, BidMachine, Vungle, Mintegral, UnityAds, Fyber, Verve) via `IsConflicting()` pessimistic-constraint comparison
- **Warning:** `Library/PackageCache` patches are ephemeral — cleared on package cache refresh. Root fix: upgrade `com.google.ads.mobile` to 11.0.0+ (use Integration Manager).

**`IsConflicting()` logic:** Checks CocoaPods pessimistic constraint `~> X.Y.Z` against required version. Three-component constraint pins minor+patch; two-component (`~> X.Y`) allows all `X.*`.

### Mutually Exclusive Adapter Pairs

Some adapter pairs cannot coexist because they pin the **same native pod** to **different exact versions** — no XML patch can reconcile this, only uninstalling one adapter resolves it.

| Adapter A | Adapter B | Shared pod | A pins | B pins |
|-----------|-----------|------------|--------|--------|
| `com.applovin.mediation.adapters.maio.ios 2.1.6.0` | `com.google.ads.mobile.mediation.maio 3.1.6` | `MaioSDK-v2` | `= 2.1.6` | `= 2.2.1` |

`CocoaPodsConflictFixer` treats these specially:
- Startup warning fires when both are detected in `Library/PackageCache`
- `Fix CocoaPods Conflicts` reports `⚠ MUTUALLY EXCLUSIVE — remove one` and **intentionally skips auto-patch**
- Recommendation: keep Maio installed only via AppLovin MAX (primary mediator) — it continues to serve Maio demand without the AdMob adapter

Additionally, `com.google.ads.mobile.mediation.maio 3.0.1` wraps `GoogleMobileAdsMediationMaio 2.1.6.1` which pins GMA `~> 12.0` — incompatible with AppLovin GAM adapter `13.2.0.0` that pins GMA `= 13.2.0`. Bumping to 3.1.6 resolves the GMA version conflict but triggers the `MaioSDK-v2` mutual exclusion above.

## Noctua Inspector — sandbox-only debug overlay

Auto-spawned in-game overlay for development / QA. **Single guardrail:** the
existing `_config.Noctua.IsSandbox` flag (`sandboxEnabled: true` in
`noctuagg.json`). Production builds spawn no `GameObject`, allocate no
buffers, register no native callbacks; all `Noctua.*` accessors return null.

Never add a separate `NOCTUA_INSPECTOR` define or `DEVELOPMENT_BUILD`
check — pile new debug surfaces onto this same gate.

| Tab | Source | Backing class |
|---|---|---|
| Timeline | combined HTTP + Trackers | `NoctuaInspectorController.cs` |
| HTTP | `HttpInspectorLog` ring buffer | `Runtime/Infrastructure/Debug/HttpInspectorLog.cs` |
| Trackers | `TrackerDebugMonitor` ring buffer | `Runtime/Presenter/Debug/TrackerDebugMonitor.cs` |
| Logs | Unity `Application.logMessageReceivedThreaded` + native bus | `Runtime/Infrastructure/Debug/LogInspectorLedger.cs`, `UnityLogStream.cs` |
| Perf | `PerformanceMonitor` MonoBehaviour, per-frame | `Runtime/Presenter/Debug/PerformanceMonitor.cs` |
| Memory | `MemoryMonitor` MonoBehaviour, 1 Hz, + native bridge | `Runtime/Presenter/Debug/MemoryMonitor.cs` |

**Ring-buffer capacities are RAM-tiered** by `InspectorBufferLimits.ForCurrentDevice()`
(`Runtime/Infrastructure/Debug/InspectorBufferLimits.cs`), chosen at init from
`SystemInfo.systemMemorySize` and passed to the three buffers' constructors. Tiers
(logs / trackers / http): `<3GB` → 5000/200/100 (historical defaults, floor — never regresses),
`≥3GB` → 10000/500/200, `≥4GB` → 20000/1000/300, `≥6GB` → 40000/1500/500. The chosen limits are
logged at init: `Inspector buffer limits (RAM …MB): logs=…, trackers=…, http=…`. All three buffers
drop the oldest entry on overflow.

**Static accessors on `Noctua` (View facade):** `HttpLog`, `DebugMonitor`,
`LogLedger`, `PerfMonitor`, `MemMonitor`, `Inspector`. Each is null-safe
when sandbox is off. UI rendering is split across partial classes
(`NoctuaInspectorController.{Logs,Performance,Memory}.cs`) so adding a
new tab means: extend `Tab` enum + new partial file with `RenderXxx`.

**Native bridge contracts** (Platform layer):
- `INativeLogStream` — `SetLogStreamEnabled(bool)` + `RegisterNativeLogCallback(Action<int level, string source, string tag, string message, long tsMs>)`. iOS: `noctuaSetLogStreamCallback` / `noctuaSetLogStreamEnabled` C exports. Android: `NoctuaInspector.setLogStreamCallback` + `setLogStreamEnabled` (Kotlin). Native bus self-gates on its own `isLogStreamEnabled()` flag — toggling from the Logs tab is the only way it flips on.
- `INativeDeviceMetrics` — `SnapshotDeviceMetrics()` returns `DeviceMetricsSnapshot` (phys footprint, available, system total, low-mem, thermal). iOS: `noctuaSnapshotDeviceMetrics` (5 out-pointers). Android: `NoctuaInspector.snapshotDeviceMetricsTuple` returns shared `long[5]` (avoids GC churn at 1 Hz).

**Adapter lives in View layer** — `NoctuaDeviceMetricsAdapter`
(`Runtime/View/Common/`) wraps `INativePlugin` to satisfy
`IDeviceMetricsProvider` (Presenter); keeps `MemoryMonitor` free of any
Platform-layer reference.

## Public API — `Noctua` static class

### Static properties

| Member | Type | Description |
|--------|------|-------------|
| `Noctua.Event` | `NoctuaEventService` | Analytics (TrackCustomEvent, SetSessionTag, GetSessionTag) |
| `Noctua.Auth` | `NoctuaAuthentication` | Authentication (AuthenticateAsync, LoginAsGuest, etc.) |
| `Noctua.IAP` | `NoctuaIAPService` | In-app purchases |
| `Noctua.Platform` | `NoctuaPlatform` | Platform features (Content, Locale) |
| `Noctua.IAA` | `MediationManager` | Ad mediation |
| `Noctua.App` | `NoctuaAppManager` | In-app review / in-app updates |
| `Noctua.Config` | `GlobalConfig` | Loaded `noctuagg.json` config |
| `Noctua.HttpLog` | `HttpInspectorLog` | HTTP ring buffer (sandbox only) |
| `Noctua.DebugMonitor` | `TrackerDebugMonitor` | Tracker ring buffer (sandbox only) |
| `Noctua.LogLedger` | `LogInspectorLedger` | Log ring buffer (sandbox only) |
| `Noctua.PerfMonitor` | `PerformanceMonitor` | FPS monitor (sandbox only) |
| `Noctua.MemMonitor` | `MemoryMonitor` | Memory monitor (sandbox only) |
| `Noctua.Inspector` | `NoctuaInspectorController` | Inspector overlay handle (sandbox only) |

### Static methods (`Noctua.cs`)

| Method | Description |
|--------|-------------|
| `InitAsync()` | SDK entry point |
| `IsInitialized()` | Whether SDK has completed init |
| `IsOfflineMode()` | Whether SDK is in offline mode |
| `IsOfflineFirst()` | Whether SDK was configured offline-first |
| `IsSandbox()` | Whether `sandboxEnabled: true` in config |
| `IsOfflineAsync()` | Checks connectivity and updates offline mode |
| `BuildSanity()` | Returns `BuildSanityInfo` (sandbox only) |
| `ShowInspector()` / `HideInspector()` / `ToggleInspector()` | Inspector overlay control |
| `OnOnline()` / `OnOffline()` | Notify native plugin of connectivity change |
| `AdjustOfflineModeDisabled()` | Checks remote feature flag |
| `GetPseudoUserId()` | Deterministic pseudo user ID |
| `SetGeneralExperiment(key, value)` | Set A/B experiment key/value |
| `GetGeneralExperiment(key)` | Get experiment value by key |
| `SetExperiment(name)` | Set active experiment identifier |
| `GetActiveExperiment()` | Get currently active experiment |
| `ShowDatePicker(year, month, day, id)` | Show native date picker |
| `CloseDatePicker()` | Close native date picker |
| `SaveEvents(jsonString)` | Save events to native storage |
| `GetEventsAsync()` | Get saved events from native storage |
| `DeleteEvents()` | Delete saved events from native storage |
| `InsertEvent(eventJson)` | Insert single event into per-row storage |
| `GetEventsBatchAsync(limit, offset)` | Batch read from per-row storage |
| `DeleteEventsByIdsAsync(ids)` | Delete events by ID from per-row storage |
| `GetEventCountAsync()` | Total stored event count |
| `OnInitSuccess` | `Action?` callback after successful init |

### Adjust methods (`Noctua.Adjust.cs`)

| Method | Return | Platform |
|--------|--------|----------|
| `GetAdjustAttributionAsync()` | `Task<NoctuaAdjustAttribution>` | Both |
| `GetAdjustAdidAsync()` | `Task<string>` | Both |
| `GetAdjustIdfaAsync()` | `Task<string>` | iOS only |
| `GetAdjustIdfvAsync()` | `Task<string>` | iOS only |
| `GetAdjustGoogleAdIdAsync()` | `Task<string>` | Android only |
| `GetAdjustAmazonAdIdAsync()` | `Task<string>` | Android only |
| `GetAdjustSdkVersionAsync()` | `Task<string>` | Both |

### Firebase methods (`Noctua.Firebase.cs`)

| Method | Return |
|--------|--------|
| `GetFirebaseInstallationID()` | `Task<string>` |
| `GetFirebaseAnalyticsSessionID()` | `Task<string>` |
| `GetFirebaseMessagingToken()` | `Task<string>` |
| `GetFirebaseRemoteConfigString(key)` | `Task<string>` |
| `GetFirebaseRemoteConfigBoolean(key)` | `Task<bool>` |
| `GetFirebaseRemoteConfigDouble(key)` | `Task<double>` |
| `GetFirebaseRemoteConfigLong(key)` | `Task<long>` |
| `OnRemoteNotificationReceived` | `event Action<NoctuaNotificationPayload>` |
| `OnNotificationTapped` | `event Action<NoctuaNotificationPayload>` |
| `OnFirebaseMessagingTokenRefresh` | `event Action<string>` |

### PlayerPrefs utilities (`Noctua.PlayerPrefs.cs`)

| Method | Description |
|--------|-------------|
| `BackupPlayerPrefs()` | Returns all PlayerPrefs as `KeyValuePair<string, string>[]` |
| `RestorePlayerPrefs(keyValues)` | Restores PlayerPrefs from backup array |
| `GetPlayerPrefsKeys()` | Returns all current PlayerPrefs keys |

## Engagement Tracking Architecture

Three parallel engagement signals in `SessionTracker.cs`:

| Event | Driver | Trigger |
|-------|--------|---------|
| `noctua_user_engagement` | Unity `Stopwatch` | Start, heartbeat (60s), pause, end |
| `native_user_engagement` | OS callbacks | iOS `UIApplication`, Android `Activity` |
| `noctua_user_engagement_per_session` | Cumulative | Session timeout resume / graceful quit |

`engagement_time_msec` is **incremental** (since last send), not cumulative. `noctua_user_engagement` always fires **before** its paired session event (`session_heartbeat`, `session_pause`, `session_end`).

## Session Events

`EventSender.cs` includes these in the `sessionEvents` HashSet (receive `tag` from `ExperimentManager`): `session_start`, `session_pause`, `session_continue`, `session_heartbeat`, `session_end`, `noctua_user_engagement`, `noctua_user_engagement_per_session`, `native_user_engagement`.

## Testing

Tests in `Tests/Runtime/`. All test classes must be `public`. Use `MockEventSender` for unit tests — no HTTP needed.

Key test files:
- `SessionTrackerEngagementTest.cs` — 7 tests for engagement tracking
- `EventTest.cs` — integration tests; filters `noctua_user_engagement` to verify session events unchanged
- `IAAConfigTest.cs` — IAA unit tests (80%+ coverage)

**Coverage target:** 80%+ per class.

### Code Coverage settings (committed)

The Unity Code Coverage window settings are persisted in `ProjectSettings/Packages/com.unity.testtools.codecoverage/Settings.json` and **committed to the consumer repo** (not the SDK submodule). Current configuration:

- **Included Assemblies:** `com.noctuagames.sdk` only — drops third-party (`NativeGallery.*`, `StompyRobot.*`) and the test/editor assemblies (`com.noctuagames.sdk.Tests`, `com.noctuagames.sdk.Editor`) from the report.
- **Excluded Paths** (set via Window → Analysis → Code Coverage → Path Filters → Excluded Paths, then re-save the json):
  ```
  **/Runtime/Plugins/**
  **/Runtime/Platform/iOS/**
  **/Runtime/Platform/Android/**
  **/Runtime/UI/Controllers/**
  **/Runtime/Inspector/**
  ```
  These folders are structurally untestable in EditMode (P/Invoke / JNI bridges, runtime UI presenters, debug overlays, vendored third-party plugins) and would otherwise inflate the denominator.

  **Note:** `AdsManager/AppLovin/**` and `AdsManager/Admob/**` are intentionally **not** excluded — they have tests in `Tests/Runtime/IAA/AppLovinManagerTests.cs` and `AdmobManagerTests.cs`. These tests are wrapped in `#if UNITY_APPLOVIN` / `#if UNITY_ADMOB` and only compile when the respective UPM packages are installed. SDK-calling paths (`Initialize`, `Load*`, `Show*`, ad callbacks) cannot be exercised in EditMode without a real device, so partial coverage of these files is expected.

After changing either, re-run the EditMode test suite and click **Generate from Last** in the Code Coverage window to refresh `CodeCoverage/Report/index.html`.

## Git Workflow

Branch naming: `feat/name`, `fix/name`, `improve/name`, `chore/name`

To commit changes in this submodule:
```sh
cd Packages/com.noctuagames.sdk
git checkout -b fix/branch-name
git add <files> && git commit -m "fix: ..."
git push -u origin fix/branch-name
# Create MR via GitLab link in push output
```

### Commit Types & Changelog Sections (git-cliff)

We follow [**Conventional Commits 1.0.0**](https://www.conventionalcommits.org/en/v1.0.0/) and
[**Semantic Versioning 2.0.0**](https://semver.org/). Versioning + changelog are automated by
**git-cliff** (see `cliff.toml`). The commit **type** controls both the **semver bump** and the
**changelog section**, so choose it deliberately — `fix:` is reserved for runtime-impacting bugs,
not every change.

**Authoritative rules (from the spec):**
- `feat:` → **MINOR**, `fix:` → **PATCH**. These are the only two types the Conventional Commits
  spec defines normatively.
- A commit is **MAJOR** when it is breaking — indicated by a `!` after the type/scope
  (`feat!:`, `refactor(iap)!:`) **or** a `BREAKING CHANGE:` footer in the body. Per the spec,
  *"Commits with `BREAKING CHANGE`, regardless of type, … translate to MAJOR releases."*
- `build / chore / ci / docs / style / refactor / perf / test` come from the
  [Angular convention](https://github.com/angular/angular/blob/main/CONTRIBUTING.md#commit) — the
  spec says these *"have no implicit effect in Semantic Versioning"*, so **we** assign their bump
  behaviour below via `cliff.toml`.
- `improve:` and `correct:` are **repo-custom** types (adopted from the native SDK) for non-bug,
  patch-level changes — they are **not** standard Conventional Commits types.

| Type | Semver bump | Changelog section | Use when |
|---|---|---|---|
| `feat:` | **MINOR** | 🚀 Features | New public API / capability |
| `fix:` | **PATCH** | 🐛 Bug Fixes | A defect that affected runtime behaviour — something was broken and now works |
| `improve:` *(custom)* | **PATCH** | ✨ Improvements | Non-bug enhancement: UX tweak, better logging/observability, cleaner flow |
| `correct:` *(custom)* | **PATCH** | ✨ Improvements | Correction that isn't a bug: wrong config value, **misleading/renamed field**, bad default |
| `perf:` | **PATCH** | ✨ Improvements | Performance optimisation |
| `refactor:` | **PATCH** | ✨ Improvements | Code restructure, no behaviour change |
| `chore:` | **PATCH** | ⚙️ Miscellaneous | Dependency bumps, build tooling (user-visible, so it bumps) |
| `feat!:` / any `!` / `BREAKING CHANGE:` | **MAJOR** | (its type's section) | Removes/changes existing public API or event/data contract in a breaking way |
| `docs:` | none | *(hidden)* | Docs and comments — internal only |
| `test:` | none | *(hidden)* | Adding or fixing tests — internal only |
| `ci:` | none | *(hidden)* | CI pipeline changes only |
| `style:` | none | *(hidden)* | Formatting / whitespace only |
| `build:` | none | *(hidden)* | Build-system changes only |

> **Rule:** anything visible to SDK consumers bumps a version. Anything purely internal (docs,
> tests, CI) does not. Internal scopes (`fix(ci)`, `feat(ci)`, `fix(build)`, `fix(test)`,
> `fix(deps)`, `feat(cd)`) are hidden from the changelog and do not bump.

#### Pick the right type — common mistakes

git-cliff groups **only by the type token**; it never judges whether a change is "really" a bug.
A wrong type permanently mislabels the changelog and can mis-bump the version. Frequent traps:

| Change | ❌ Wrong | ✅ Right | Why |
|---|---|---|---|
| Rename an event/payload field (`impression_id` → `sdk_impression_id`) | `fix:` | `correct:` — or `feat!:` / `BREAKING CHANGE:` if dashboards/marts query the old name | A rename isn't a runtime defect; if downstream data consumers depend on the field name it's a **breaking** contract change |
| Add more logging / diagnostics | `fix:` | `improve:` | Nothing was broken — it's an observability enhancement |
| Tidy code, no behaviour change | `fix:` | `refactor:` | Restructure, not a defect |
| Adjust a wrong default / config value | `fix:` | `correct:` | A correction, not a runtime bug (unless it actually caused a crash/wrong behaviour at runtime) |
| Bump a dependency | `fix:`/`feat:` | `chore:` | Tooling/deps |
| New SDK method that removes an old one | `feat:` | `feat!:` (+ `BREAKING CHANGE:` footer) | Removing public API breaks integrations → MAJOR |

Rule of thumb: **use `fix:` only when something was broken at runtime and now works.** Everything
else that's a patch-level change is `improve:` / `correct:` / `perf:` / `refactor:`.

**Squash + correct typing:** land each logical change as one squashed commit with the correct type.
Preview the changelog locally before relying on CI:
```sh
git-cliff --bump            # full changelog incl. the bumped (unreleased) section
git-cliff --unreleased      # just the pending section for the next release
```
> ⚠️ Do **not** trust `git-cliff --bumped-version` for the version number on this repo: release
> tags are created by the `publish` job on a divergent (test-stripped) commit, so they are not
> ancestors of `main` and git-cliff bumps from a stale base. The CI `bump-version-for-release` job
> instead anchors the base to `package.json` and derives only the bump *level* from the commits
> (see `.gitlab-ci.yml`). The proper long-term fix is to tag the `main` lineage (as the native SDK
> does), after which `--bumped-version` becomes reliable.

### Squashing Commits

**Squash before pushing when commits are noisy or don't tell a coherent story.** `git-cliff`
reads every commit merged to `main` to build the changelog and decide the version bump — WIP /
fixup / "oops" commits pollute both the changelog and the semver level.

#### When to squash

| Situation | Action |
|---|---|
| WIP / checkpoint commits (`wip: halfway`, `tmp: debug log`) | Always squash |
| Multiple fixups to the same change (`fix typo`, `fix build`, `oops`, `address review`) | Squash into the parent |
| One feature spread across many tiny commits with no individual value | Squash into one `feat:` |
| Each commit is a meaningful, self-contained unit (separate feature / fix / chore) | Keep as-is |

> **Rule of thumb: one logical change = one commit on `main`.** If a branch has 8 commits that
> all implement the same feature, squash to 1–2 before merging. If they are genuinely independent
> (separate fixes/features), keep them separate so each gets its own changelog line.

#### How to squash

`git rebase -i` is the usual tool but is **interactive** (opens an editor) — not available in
non-interactive/agent shells. Prefer the non-interactive forms:

```sh
# Squash the entire branch into one commit (most common for a single-feature branch):
git reset --soft main
git commit -m "feat: <one-line summary of the whole change>"

# Squash the last N commits into one:
git reset --soft HEAD~N
git commit -m "improve: <summary>"

# Fold a just-made fixup into the previous commit (keeps its message):
git commit --amend --no-edit
```

After squashing an already-pushed branch you must force-push (feature branches only, never a
shared/protected branch):

```sh
git push --force-with-lease
```

#### Examples

**Before (noisy — squash):**
```
wip: start login milestones
fix build error
oops forgot .meta
add test
fix test
```
**After (clean — one meaningful commit):**
```
feat: add login retention milestones (login_on_d0..d30)
```

**Before (keep — each is independent):**
```
feat: add taichi inspector filter
fix: NPE in AdWatchMilestoneTracker on null adType
chore: bump native SDK deps
```

> **Mixed-concern files:** when one file legitimately spans concerns (e.g. `Noctua.Initialization.cs`
> wires several features), hunk-level splitting needs interactive staging which isn't available
> here — group the file under its **dominant** type rather than forcing artificial splits.

## Release Checklist

Releases are automated by the GitLab CI `bump-version-for-release` job (manual trigger on `main`),
which runs git-cliff to compute the next version and regenerate `CHANGELOG.md`, then bumps
`package.json` + `Runtime/AssemblyInfo.cs` and tags. You normally only need to:

1. Ensure your commits use the correct conventional-commit types (table above).
2. Merge to `main`.
3. Trigger the manual `bump-version-for-release` job in GitLab CI.
4. If native SDK updated: bump version in `Editor/NativePluginDependencies.xml` beforehand.

Do **not** hand-edit `CHANGELOG.md` or the version fields — git-cliff/CI owns them.
