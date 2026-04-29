# Noctua SDK — Unity UPM Package

SDK package at `Packages/com.noctuagames.sdk/`. All runtime code under `Runtime/`, Editor tooling under `Editor/`, tests under `Tests/`.

- **Version:** 0.113.0
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
| `Runtime/View/Noctua.cs` | View | Public API entry point |
| `Runtime/View/Noctua.Initialization.cs` | View | Composition root — wires all services |
| `Runtime/View/NoctuaAuthentication.cs` | View | Auth facade |
| `Runtime/View/NoctuaLocale.cs` | View | `ILocaleProvider` impl |
| `Runtime/View/NoctuaConnectivityProvider.cs` | View | `IConnectivityProvider` adapter |
| `Runtime/View/PaymentUIAdapter.cs` | View | Adapts `UIFactory` → `IPaymentUI` |
| `Runtime/View/LazyAuthProvider.cs` | View | Deferred `IAuthProvider` for circular init |
| `Runtime/Presenter/NoctuaIAPService.cs` | Presenter | IAP: purchases, products, payment types |
| `Runtime/Presenter/NoctuaAuthenticationService.cs` | Presenter | Auth: social login, account mgmt |
| `Runtime/Presenter/SessionTracker.cs` | Presenter | Session lifecycle, heartbeat, engagement |
| `Runtime/Presenter/ExperimentManager.cs` | Presenter | A/B testing, session tags |
| `Runtime/Presenter/NoctuaEventService.cs` | Presenter | High-level event tracking API |
| `Runtime/Infrastructure/EventSender.cs` | Infra | Event storage, batching, HTTP flush |
| `Runtime/Infrastructure/Network/Http.cs` | Infra | `HttpRequest` with JSON serialization |
| `Runtime/Infrastructure/Network/InternetChecker.cs` | Infra | Connectivity ping check |
| `Runtime/Infrastructure/Utility.cs` | Infra | Validation, parsing, retry helpers |
| `Runtime/Infrastructure/MobileDateTimePicker.cs` | Infra | Date picker bridge (static delegate) |
| `Runtime/Platform/INativePlugin.cs` | Platform | All native sub-interfaces (incl. `INativeLogStream`, `INativeDeviceMetrics` for Inspector Logs/Memory tabs) |
| `Runtime/Platform/iOS/IosPlugin.cs` | Platform | iOS P/Invoke declarations |
| `Runtime/Platform/Android/AndroidPlugin.cs` | Platform | Android JNI bridge |
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
| `INativePlugin` | `Platform/` | Aggregate of all native sub-interfaces |

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
| HTTP | `HttpInspectorLog` ring buffer (100 cap) | `Runtime/Infrastructure/Debug/HttpInspectorLog.cs` |
| Trackers | `TrackerDebugMonitor` ring buffer (200 cap) | `Runtime/Presenter/Debug/TrackerDebugMonitor.cs` |
| Logs | Unity `Application.logMessageReceivedThreaded` + native bus | `Runtime/Infrastructure/Debug/LogInspectorLedger.cs` (5,000 cap), `UnityLogStream.cs` |
| Perf | `PerformanceMonitor` MonoBehaviour, per-frame | `Runtime/Presenter/Debug/PerformanceMonitor.cs` |
| Memory | `MemoryMonitor` MonoBehaviour, 1 Hz, + native bridge | `Runtime/Presenter/Debug/MemoryMonitor.cs` |

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

## Git Workflow

Branch naming: `feat/name`, `fix/name`, `chore/name`  
Commit style: conventional commits (`feat:`, `fix:`, `chore:`, `test:`, `docs:`)

To commit changes in this submodule:
```sh
cd Packages/com.noctuagames.sdk
git checkout -b fix/branch-name
git add <files> && git commit -m "fix: ..."
git push -u origin fix/branch-name
# Create MR via GitLab link in push output
```

## Release Checklist

1. Bump version in `package.json` and `Runtime/AssemblyInfo.cs`
2. Update `CHANGELOG.md` with new version section
3. Update `README.md` version badge and install snippet
4. If native SDK updated: bump version in `Editor/NativePluginDependencies.xml`
5. Commit: `git commit -m "chore: release vX.Y.Z"`
6. Tag: `git tag -a vX.Y.Z -m "Release vX.Y.Z"`
7. Push: `git push origin main --follow-tags`
