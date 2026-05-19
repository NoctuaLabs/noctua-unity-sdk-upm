# Noctua SDK — Unity UPM Package

SDK package at `Packages/com.noctuagames.sdk/`. All runtime code under `Runtime/`, Editor tooling under `Editor/`, tests under `Tests/`.

- **Version:** 0.117.0
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
├── CATEGORIES.md      Cross-layer index — single source of truth for "where does feature X live"
├── Core/              Shared interfaces (ILocaleProvider, IConnectivityProvider, Logging/)
├── Model/             Pure data classes, organised by category (Auth/, IAP/, Event/, Platform/, App/, Common/)
├── Infrastructure/
│   ├── Event/         EventSender (split into partials)
│   ├── Network/       Http, HttpExchange, InternetChecker
│   ├── Debug/         HttpInspectorHooks, HttpInspectorLog
│   ├── Storage/       AccessTokenProvider
│   └── Common/        Utility, MobileDateTimePicker
├── Presenter/         Business logic services, organised by category
│   ├── Auth/          NoctuaAuthenticationService (split into partials), SocialAuthenticationService, AccountContainer
│   ├── IAP/           NoctuaIAPService (split into partials), InternalPurchaseItem
│   ├── IAA/           MediationManager (split into partials), AppOpenAdManager, AdRevenueTrackingManager, AdFrequencyManager, HybridAdOrchestrator, CpmFloorManager, UserSegmentManager
│   ├── Event/         NoctuaEventService, ExperimentManager
│   ├── Session/       SessionTracker, NativeSessionTracker, SessionTrackerBehaviour, NativeSessionTrackerBehaviour, NativeCrashForwarder
│   ├── App/           NoctuaGameService
│   ├── Debug/         TrackerDebugMonitor, TrackerEmission, TrackerObserverRegistry
│   └── Interfaces/    IEventSender, IIAPService, IAuthProvider, IPaymentUI, IAdRevenueTracker, etc. — kept flat
├── Platform/          Native bridges: Android/ (JNI), iOS/ (P/Invoke), Editor/ (stub)
├── UI/
│   ├── Controllers/   UIElements presenters by category (Auth/, IAP/, IAA/, Platform/, Common/)
│   └── UIFactory, UIUtility, BasePresenter, Spinner, ColorModule, ScreenRotationMonitor
├── View/              Composition root + thin facades, organised by category
│   ├── Auth/          NoctuaAuthentication
│   ├── IAP/           PaymentUIAdapter
│   ├── Platform/      NoctuaPlatform, NoctuaLocale, NoctuaConnectivityProvider, NoctuaWebContent
│   ├── App/           NoctuaAppManager, Noctua.Firebase.cs, Noctua.PlayerPrefs.cs
│   ├── Common/        LazyAuthProvider
│   └── Noctua.cs, Noctua.Initialization.cs (composition root at View/ root)
├── AdsManager/        Ad network adapters: Admob/, AppLovin/, AdPlaceholder/
├── Inspector/         Debug overlay: NoctuaInspectorController (split into partials), InspectorExporter, FirebaseProjectLookup
└── Plugins/           Third-party native/managed plugins (UniWebView, etc.)
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

**Audit before each release:**
```sh
grep -rn "internal class\|internal static class" Runtime/
```
Any matches are violations — change to `public`.

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
| `Runtime/View/Auth/NoctuaAuthentication.cs` | View | Auth facade |
| `Runtime/View/Platform/NoctuaLocale.cs` | View | `ILocaleProvider` impl |
| `Runtime/View/Platform/NoctuaConnectivityProvider.cs` | View | `IConnectivityProvider` adapter |
| `Runtime/View/IAP/PaymentUIAdapter.cs` | View | Adapts `UIFactory` → `IPaymentUI` |
| `Runtime/View/Common/LazyAuthProvider.cs` | View | Deferred `IAuthProvider` for circular init |
| `Runtime/Presenter/IAP/NoctuaIAPService.cs` | Presenter | IAP: purchases, products, payment types |
| `Runtime/Presenter/Auth/NoctuaAuthenticationService.cs` | Presenter | Auth: social login, account mgmt |
| `Runtime/Presenter/IAA/MediationManager.cs` | Presenter | Ad mediation orchestration |
| `Runtime/Presenter/Session/SessionTracker.cs` | Presenter | Session lifecycle, heartbeat, engagement |
| `Runtime/Presenter/Session/SessionTrackerBehaviour.cs` | Presenter | MonoBehaviour bridge → SessionTracker |
| `Runtime/Presenter/Session/NativeSessionTracker.cs` | Presenter | Native OS engagement tracking |
| `Runtime/Presenter/Session/NativeSessionTrackerBehaviour.cs` | Presenter | MonoBehaviour bridge → NativeSessionTracker |
| `Runtime/Presenter/Event/ExperimentManager.cs` | Presenter | A/B testing, session tags |
| `Runtime/Presenter/Event/NoctuaEventService.cs` | Presenter | High-level event tracking API |
| `Runtime/Infrastructure/Event/EventSender.cs` | Infra | Event storage, batching, HTTP flush |
| `Runtime/Infrastructure/Network/Http.cs` | Infra | `HttpRequest` with JSON serialization |
| `Runtime/Infrastructure/Network/InternetChecker.cs` | Infra | Connectivity ping check |
| `Runtime/Infrastructure/Common/Utility.cs` | Infra | Validation, parsing, retry helpers |
| `Runtime/Infrastructure/Common/MobileDateTimePicker.cs` | Infra | Date picker bridge (static delegate) |
| `Runtime/Platform/INativePlugin.cs` | Platform | All native sub-interfaces (incl. `INativeLogStream`, `INativeDeviceMetrics`) |
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

Three parallel engagement signals across two tracker classes:

| Event | Driver class | Trigger |
|-------|-------------|---------|
| `noctua_user_engagement` | `SessionTracker.cs` | Unity Stopwatch: start, heartbeat (60s), pause, end |
| `native_user_engagement` | `NativeSessionTracker.cs` | OS callbacks: iOS `UIApplication`, Android `Activity` |
| `noctua_user_engagement_per_session` | `NativeSessionTracker.cs` | Session timeout resume / graceful quit |

`engagement_time_msec` is **incremental** (since last send), not cumulative. `noctua_user_engagement` always fires **before** its paired session event (`session_heartbeat`, `session_pause`, `session_end`).

## Session Events

Events in the `sessionEvents` HashSet in `EventSender.cs` receive the `tag` property from `ExperimentManager.GetSessionTag()`. See the HashSet definition in `Runtime/Infrastructure/Event/EventSender.cs` for the authoritative list — do not maintain a duplicate here. Current members include: `session_start`, `session_pause`, `session_continue`, `session_heartbeat`, `session_end`, `noctua_user_engagement`, `noctua_user_engagement_per_session`, `native_user_engagement`.

## Testing

Tests in `Tests/Runtime/`. All test classes must be `public`. Use `MockEventSender` for unit tests — no HTTP needed.

**`MockEventSender`** is defined at the top of `Tests/Runtime/SessionTrackerEngagementTest.cs` (not a separate file). Import namespace `Tests.Runtime` to use it in new test files.

Key test files:

| File | What it covers |
|------|---------------|
| `SessionTrackerEngagementTest.cs` | `noctua_user_engagement` lifecycle |
| `SessionTrackerBehaviourTest.cs` | `SessionTrackerBehaviour` MonoBehaviour bridge |
| `NativeSessionTrackerCoverageTest.cs` | `NativeSessionTracker` + `NativeSessionTrackerBehaviour` |
| `EventSenderCoverageTest.cs` | `EventSender` — offline, queuing, flush, HTTP paths |
| `IAA/MediationManagerCoverageTest.cs` | `MediationManager` — init, show, frequency, CPM |
| `UITests.cs` + `UITests_Extended.cs` | `UIUtility`, `DropdownNoctua`, `InputFieldNoctua`, `Spinner` |
| `EventTest.cs` | Integration tests — session events pipeline |
| `IAA/IAAConfigTest.cs` | IAA config parsing and validation |

**Coverage target:** 80%+ per class.

### Test Patterns

**Private lifecycle methods (MonoBehaviour):** Unity does not fire lifecycle hooks in EditMode. Invoke via reflection:
```csharp
typeof(MyBehaviour).GetMethod("OnDestroy", BindingFlags.NonPublic | BindingFlags.Instance)
    .Invoke(behaviour, null);
```

**Time-dependent state:** Inject private fields via reflection instead of `Thread.Sleep`:
```csharp
typeof(NativeSessionTracker)
    .GetField("_nextSessionTimeout", BindingFlags.NonPublic | BindingFlags.Instance)
    .SetValue(tracker, DateTime.UtcNow.AddMilliseconds(-1));
```

**`[Test]` vs `[UnityTest]`:** Use `[Test]` for pure logic (no Unity frame tick needed). Use `[UnityTest]` + `UniTask.ToCoroutine()` only when a coroutine yield or PlayerLoop tick is required.

**Null events:** Public event-handler methods that do not dereference their event parameter (e.g. `OnFocusChange(FocusInEvent evt)`) may be invoked with `null` in tests:
```csharp
_sut.OnFocusChange((FocusInEvent)null);
```

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
  **/Editor/Build/**
  **/Editor/Menu/**
  ```
  These folders are structurally untestable in EditMode (P/Invoke / JNI bridges, runtime UI presenters, debug overlays, vendored third-party plugins, Unity Editor build post-processors and menu windows) and would otherwise inflate the denominator.

  **`Editor/Build/**` and `Editor/Menu/**`** exclude `BuildPostProcessor`, `CocoaPodsConflictFixer`, `BuildPreprocessor`, `NoctuaIntegrationManagerWindow`, `EmbedFrameworksDeduper`, `NoctuaAdapterStabilizer`, and similar Editor-only utilities. These classes require a full Unity Editor build context (not available in EditMode tests) and are validated manually during builds. Note: `Runtime/Platform/Editor/DefaultNativePlugin.cs` is **not** excluded (it has `Editor` in its path but sits under `Runtime/`, not the package-root `Editor/` directory).

  **Note:** `AdsManager/AppLovin/**` and `AdsManager/Admob/**` are intentionally **not** excluded — they have tests in `Tests/Runtime/IAA/AppLovinManagerTests.cs` and `AdmobManagerTests.cs`. These tests are wrapped in `#if UNITY_APPLOVIN` / `#if UNITY_ADMOB` and only compile when the respective UPM packages are installed. SDK-calling paths (`Initialize`, `Load*`, `Show*`, ad callbacks) cannot be exercised in EditMode without a real device, so partial coverage of these files is expected.

After changing either, re-run the EditMode test suite and click **Generate from Last** in the Code Coverage window to refresh `CodeCoverage/Report/index.html`.

## Unit Testing Rules

### 1. Isolation and Determinism

Every test must be fully self-contained: no shared mutable state, no real network calls, no real clock. Reset all state in `[SetUp]` / `[TearDown]`. A test that passes sometimes and fails other times is broken — fix it immediately.

Inject all external dependencies. Never let production singletons (`PlayerPrefs`, `Noctua.*` statics, `DateTime.UtcNow`) leak into tests. Substitute controllable fakes:

| Production dependency | Test substitute |
|---|---|
| HTTP | `HttpMockServer` (in-process) |
| Event pipeline | `MockEventSender` (captures sent events) |
| Platform bridge | `MockNativeLifecycle` / fake `INativePlugin` |
| Time | Inject `DateTime` via field reflection or a `Func<DateTime>` config param |

### 2. Mock/Stub Discipline

- Mock at **process boundaries only** — HTTP, platform bridges, file system, time, randomness.
- Do **not** mock pure domain logic or value objects — test them directly.
- Prefer **fakes** over mocks for complex collaborators (e.g. an in-memory event sink is more stable than a mock with many `Verify` calls).
- If a test has more `mock.Verify` calls than real-output assertions, it is testing wiring, not behaviour — redesign it.

### 3. Coverage Targets and Exclusions

**Target: 80% line/branch coverage** on testable production code.

Exclude from metrics (via Code Coverage path filters):
- `Runtime/Platform/iOS/**` and `Runtime/Platform/Android/**` — P/Invoke / JNI bridges; validated on-device
- `Runtime/UI/Controllers/**` — runtime UIElements presenters requiring a live game window
- `Runtime/Inspector/**` — debug overlay; sandbox-gated
- `Runtime/Plugins/**` — vendored third-party code

`#if UNITY_IOS` / `#if UNITY_ANDROID` blocks that cannot compile in EditMode are **structurally unreachable** — document them with a comment rather than trying to force coverage.

### 4. Naming Convention

Use `MethodName_StateUnderTest_ExpectedBehavior`:

```csharp
OnNativeResume_WhenFirstResume_EmitsNativeUserEngagement
FlushAsync_WhenIsQuitting_ExitsEarly
GetProductList_WhenOffline_ReturnsEmptyList
```

Never use `Test1`, `HappyPath`, or vague verb names. The test name is executable documentation.

### 5. Test Organisation

- One test class per production class, mirroring the source layout under `Tests/Runtime/`.
- Test doubles (fakes, stubs, mock servers) live in `Tests/Runtime/` alongside the tests, never inside the production assembly.
- Keep test files under 400 lines; extract `TestHelpers` / builder utilities when setup code grows beyond ~30 lines.
- `MockEventSender` is defined in `SessionTrackerEngagementTest.cs` — import `using Tests.Runtime;` to reuse it.

### 6. EditMode vs PlayMode

- **Default to EditMode** for all pure logic — parsing, serialization, state machines, event routing, HTTP response mapping, MonoBehaviour lifecycle via reflection.
- Use **PlayMode** only when a coroutine scheduler tick or real `Start`/`Update` cycle is unavoidable. PlayMode tests require a domain reload and run significantly slower.
- For MonoBehaviour lifecycle (`OnDestroy`, `OnApplicationPause`, `Start`), prefer EditMode + reflection over spinning up a full PlayMode scene:
  ```csharp
  typeof(MyBehaviour).GetMethod("OnDestroy", BindingFlags.NonPublic | BindingFlags.Instance)
      .Invoke(behaviour, null);
  ```

### 7. Async / UniTask Patterns

- Return `async Task` (not `async UniTask`) from NUnit test methods — Unity Test Framework resolves `Task` natively in EditMode.
- Wrap UniTask-returning production methods with `.AsTask()` at the test boundary when needed.
- **Never `await` coroutines inside EditMode tests** — the coroutine scheduler does not tick; the test will hang. Use `UniTask.ToCoroutine()` only in PlayMode `[UnityTest]` methods.
- Add `[Timeout(5000)]` on all async tests to catch hangs instead of blocking CI indefinitely.

### 8. What to Avoid

| Anti-pattern | Why / what to do instead |
|---|---|
| Calling private methods via reflection for logic | Extract into a testable internal class; only use reflection for unavoidable lifecycle hooks |
| Asserting on internal field values | Assert observable outputs (events sent, return values, thrown exceptions) |
| Sharing static state between tests | Reset via `[TearDown]` or redesign with injected state |
| Testing that a mock was called exactly N times | Assert the real output the mock enables, not the call count |
| `Thread.Sleep` in tests | Inject time or fast-forward state via reflection (e.g. set `_nextSessionTimeout` to past) |

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
