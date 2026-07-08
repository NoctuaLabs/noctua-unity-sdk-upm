# Changelog

All notable changes to this project will be documented in this file.

## [0.130.0] - 2026-07-08

### ⚙️ Miscellaneous

- Bump native SDKs to android-sdk-v0.33.0 and ios-sdk-v0.38.0

### ✨ Improvements

- *(iaa)* Extract revenue tracking into AppLovinRevenueHelper and AdmobRevenueHelper
- *(iaa)* Replace ad format string literals with AdFormatKey constants in revenue helpers
- Greppable [tag] logging and first_purchase diagnostics
- *(inspector)* Eliminate per-frame cost of debug PerformanceMonitor
- *(inspector)* Shrink overlay via higher referenceDpi
- *(inspector)* Harden Taichi filter and render tick against exceptions
- *(inspector)* Make Taichi filter key a constant and case-insensitive
- Introduce INativeAdjust interface for ADID; decouple from INativeFirebase in EventSender
- Name adjust-adid fetched flag as a predicate
- Check pending purchases in HandleUnpairedPurchase redeem guard
- Defensive exception guards across AndroidPlugin JNI surface
- Add debug/warning logs on previously silent guard paths
- Add auth-flow diagnostic logs (token source, account save attempts)
- *(iaa)* Add detectable [mediation] entry logs to every MediationManager method
- *(iaa)* Downgrade non-ad-show [mediation] entry logs to Debug
- Track IAP taichi revenue in product local currency
- Lower Adjust attribution log from Info to Debug
- Bulk multi-select adapter install in Integration Manager
- *(iap)* Value taichi IAP revenue in USD with currency_to_usd_rate, local_price_in_usd, and type
- *(iap)* Accept currency_to_usd_rate and local_price_in_usd via PurchaseRequest
- *(iap)* Add detailed [taichi] logging for IAP revenue tracking
- *(iap)* Make taichi IAP logs Debug-level and drop type field

### 🐛 Bug Fixes

- Co-locate ad revenue tracking with impression emit + add impression↔revenue linkage + defensive logging
- Rename impression_id → sdk_impression_id and revenue_id → sdk_revenue_id
- *(iaa)* Remove value and currency fields from AdImpression event payload
- *(iaa)* Use 'app-open' (hyphenated) in revenue helper ad format label
- Add missing .meta files for Admob/AppLovin RevenueHelper
- *(inspector)* Render search fields and perf HUD text in themeless panel
- *(iap)* Correct ItemAlreadyOwned message to say the item is already owned (en/id/vi)
- Add missing Adjust stub methods to DefaultNativePlugin (CS0535)
- Move all Adjust methods from INativeFirebase to INativeAdjust — resolve CS0121 ambiguity
- Adjust_adid uses IDFA on iOS and Google Ad ID on Android in EventSender
- Do not block SDK init when IAA enabled but local config is null
- Add missing Noctua.Adjust.cs.meta to restore Adjust API compilation
- IOS bridge — per-operation purchase callbacks and GetAccounts null guard
- Serialize concurrent IAP flows and surface fire-and-forget failures
- Auth null guards, offline logout fallback, and token/listener hygiene
- Event pipeline thread safety, adjust_adid pre-persist loss, and session-end guard
- IAA frequency-cap persistence, revenue precision, and callback hygiene
- Platform/network hardening — 408 retry, JNI leaks, native-call timeouts
- Editor event store clearing and exact-boundary Taichi threshold comparison
- Pin just-saved account as RecentAccount instead of timestamp ordering
- TrackAdRevenue crash from unattached threads and null payload entries
- Marshal Android Track* JNI to main thread and fix KotlinUnit init race
- Guard NoctuaLogger ctor against IL2CPP null stack frames

### 🚀 Features

- Add Taichi IAP revenue tracker with Firebase Remote Config
- Add watch_ads_1x / watch_ads_15x / watch_ads_20x milestone thresholds
- Login retention milestones, taichi inspector filter/search, RAM-tiered buffers
- Add Adjust device info getters to Unity SDK
- Add adjust_adid to every event payload via cached single-flight fetch
- Add on/off toggle for Performance Monitor in Inspector
- Runtime sandbox override (remote-driven, noctuagg.json as source of truth)
- Add X-SANDBOX-ENABLED request header and dedicated sandbox-changed dialog
- Add Inspector debug-action registry + Inject tab
- *(iaa)* Add CDN cross-promotion ad placeholder gated by remote config
- *(iaa)* Track cross_ad_impression house-ad event on cross-promotion render
- *(iaa)* Add ShowCrossPromotion direct cross-promotion API
- *(iaa)* Add effortless ShowCrossPromotion(adType) via Firebase Remote Config

## [0.122.0] - 2026-05-18

### ⚙️ Miscellaneous

- Bump native SDK deps — Android 0.32.2, iOS 0.37.0

## [0.121.0] - 2026-05-12

### 🐛 Bug Fixes

- Skip CocoaPods-managed frameworks in EmbedDynamicPodsFrameworks

## [0.120.0] - 2026-05-12

### 🐛 Bug Fixes

- Correct fileRef Pods/ check in RemovePodsEmbedConflicts
- Remove unverified InMobiSDK from framework conflict checks

## [0.119.0] - 2026-05-12

### 🐛 Bug Fixes

- Remove AppLovin xcframework embed that conflicts with CocoaPods script

## [0.118.0] - 2026-05-12

### 🐛 Bug Fixes

- Match nested-brace PBXBuildFile entries in EmbedFrameworksDeduper

## [0.117.0] - 2026-05-08

### ⚙️ Miscellaneous

- Track Unity .meta files for new test scripts
- Track .meta for RetryAsyncTaskEditModeTest
- Add missing .meta for MediationCallbackHandlerRaceTest

### ✨ Improvements

- Rename IsRefundEligibleAsync, fix callback guard flags, add ReportError API, remove Sentry

### 🐛 Bug Fixes

- *(tests)* Resolve CS0118 — alias IAA model class to avoid namespace collision
- *(tests)* Correct 6 failing test assertions
- *(tests)* Remove LogAssert reference from AppOpenAdManagerTest (missing using)
- *(tests)* Correct 5 compile errors in Auth, Event, and MediationManager tests
- *(tests)* Correct SessionTracker constructor call in EventTest
- Resolve CS0118 IAA namespace/type ambiguity in GameServiceModelsTest
- Rename IAA alias to IAAModel in GameServiceModelsTest to avoid CS0118
- Update MiscModelTest for SDK enum rename and GeoIPData namespace
- Gate _log.Error/_log.Exception on ForwardToEvents dedup result
- Three test failures — null Info message, Unknown phone codes, IAP error code type
- Five test failures across model, config, HTTP, and floor logic
- Add missing using com.noctuagames.sdk in all namespace-migrated test files
- Add missing using com.noctuagames.sdk to ExperimentManagerTest
- Correct API usage in BugReportExporterTest and AdFrequencyManagerTest
- Replace ignoreFailingMessages with LogAssert.Expect in NotEnabledTest
- Resolve test failures and LogAssert noise in IAP/Event/Utility tests
- Replace LogAssert.Expect with scoped ignoreFailingMessages for Serilog-routed error logs
- Eliminate session recovery double-count and session inflation
- Allow 0ms engagement events for pause and end lifecycles
- Crash-persist events before Firebase fetch; persistent pseudo_user_id
- Add missing UnityEngine using to SessionTrackerEngagementTest
- Add missing System.Linq using to AdRevenueTrackingManagerTest
- Add missing com.noctuagames.sdk.Events using to AdRevenueAndSessionThreadingTest
- Resolve 5 test failures in threading and session tests
- Correct device_os assertion in BackgroundAndMainThreadTest C1
- Correct event names and context assumptions in 5 test cases
- Replace fragile raw-pattern match with proper deserialization in C1 test
- Close NoctuaEventServiceTest class brace prematurely causing CS0116
- Replace ConcurrentBag.Contains with Any() to avoid MemoryExtensions overload
- Catch AggregateException in P2 — Task.Wait throws when tasks are faulted
- Use IsNullOrEmpty in SetCurrentFeature null test — ExperimentManager returns null not empty
- Address code review issues in MediationCallbackHandlerRaceTest
- Rename ClientErrorSource.Sdk → NoctuaSdk and fix AdRevenueTrackingManager null-tracker warning
- Broaden DetermineSource to match 'octua' substring for obfuscated Noctua frames

### 🚀 Features

- Add thread-safety + survivability tests for EventSender and NoctuaEventService
- Track ad_rewarded_complete event when user earns a rewarded ad reward
- Add ad_rewarded_complete event with main-thread safety and unit tests

## [0.116.0] - 2026-05-04

### ✨ Improvements

- *(iaa)* Rename applied_iaa_config "source" field to "config_origin"

### 🐛 Bug Fixes

- *(iaa)* Marshal AppLovin and AdMob revenue handlers to main thread
- *(iaa)* Make MediationManager.CreateNetworks config-driven, not define-driven
- *(iaa)* Plug edge cases in CreateNetworks network selection

### 🚀 Features

- *(iaa)* Visible per-network init logs and SDK availability check
- *(iaa)* Emit applied_iaa_config event on every CreateNetworks success
- *(iaa)* Tag applied_iaa_config with source (local vs remote_override)

## [0.115.0] - 2026-04-30

### 🐛 Bug Fixes

- *(iaa)* AdMob banner shows at top instead of caller's requested position
- *(inspector)* Mobile-device compatibility pass
- *(inspector)* Logs rows tertumpuk + Editor preview too small
- *(inspector)* Logs row tertumpuk — switch to two-line layout
- *(inspector)* Mobile-readable fonts + paddings across all tabs
- *(inspector)* Rows tertumpuk in Build/Memory/Perf tabs
- *(inspector)* Noctuagg.json overlap when expanded
- *(inspector)* Drop GoogleServices file + Firebase project ID rows
- *(inspector)* Tab labels wrapped per-character + panel transparency
- *(inspector)* Drop checksum row + label-above-value rows for long values

### 🚀 Features

- *(inspector)* Expandable Logs rows with tap-to-expand UX
- *(inspector)* Header + tabs scroll with content (sticky bottom only)
- *(inspector)* Show full noctuagg.json on Build tab

## [0.114.0] - 2026-04-29

### 🐛 Bug Fixes

- Align purchase_canceled spelling + add 5 orphan .cs.meta files

## [0.113.0] - 2026-04-29

### ✨ Improvements

- *(iap)* Rename IsRefundedAsync to IsRefundEligibleAsync

### 🐛 Bug Fixes

- *(inspector)* Disambiguate CompressionLevel in BugReportExporter

### 🚀 Features

- *(iap)* Add Noctua.IAP.IsRefundedAsync for non-consumable refund detection
- *(inspector)* Add Logs / Performance / Memory tabs
- *(inspector)* Polish pass on Logs / Performance / Memory tabs
- *(inspector)* Build sanity panel (Phase 2.1)
- *(inspector)* One-tap bug report export (Phase 2.2)
- *(inspector)* Event replay via 'Re-fire' button (Phase 2.3)
- *(inspector)* Network condition simulator (Phase 2.5)
- *(inspector)* Experiment / feature flag overrides on Build tab (Phase 2.6)
- *(inspector)* Adjust event-map panel (Phase 2.9)

## [0.112.0] - 2026-04-28

### revert

- *(iaa)* Restore exponential backoff for ad load retries

### 🐛 Bug Fixes

- *(iaa)* Guard primary ad unit setup against missing/unknown ad unit IDs
- *(iaa)* Use linear backoff (10s * attempt) for ad load retries
- *(iaa)* Apply CPM hard floor to interstitial/rewarded show + fallback

## [0.111.0] - 2026-04-28

### 🐛 Bug Fixes

- Compile MediationManager when only UNITY_APPLOVIN is defined; enable ObjC exceptions for NoctuaCrashReporter

## [0.110.0] - 2026-04-27

### ✨ Improvements

- Organize Runtime/ by SDK category within each layer

### 🐛 Bug Fixes

- *(iap)* Observe fire-and-forget VerifyOrderImplAsync tasks
- *(ads)* Marshal orchestrator events to main thread
- Address MR#569 review (herpiko1)
- Detect LocalNotificationAppController sibling via mm scan
- *(integration-manager)* Pin default AppLovin MAX SDK to 8.6.1
- *(iaa)* Init secondary network only after primary ready

### 🚀 Features

- Sentry-style client_error tracker (managed + native)

## [0.109.0] - 2026-04-22

### 🐛 Bug Fixes

- *(adapters)* Restore safe default pairings for ByteDance iOS + Maio

## [0.108.0] - 2026-04-22

### 🐛 Bug Fixes

- *(adapters)* Pin ByteDance iOS to 709010100, Maio to 3.0.1

## [0.107.0] - 2026-04-22

### 🐛 Bug Fixes

- *(ios)* Harden CustomAppController APNs + FIRMessaging wiring
- *(push)* Buffer cold-start payloads until Unity registers the handler
- 4 integration gaps surfaced by post-release audit

### 🚀 Features

- *(ios)* Auto-bridge CustomAppController to LocalNotificationAppController + sibling-conflict detector
- *(firebase)* Expose Noctua.GetFirebaseMessagingToken() public API
- *(firebase)* Auto-log FCM token on sandbox builds after init
- *(push)* Expose remote-notification + tap + FCM token-refresh callbacks
- *(tracking)* Add stage_session_id + stage_time_msec to game_stage_failed
- Noctua Inspector (Beta) + SDK stabilizers

## [0.106.0] - 2026-04-17

### 🐛 Bug Fixes

- *(ios)* Skip dynamic XCFramework embed when Podfile uses dynamic linkage
- *(ios)* Auto-cleanup stale Pods xcframework embeds on SDK upgrade
- *(iaa)* Populate placement / value_usd / engagement_time correctly for AdMob ad_impression
- *(iaa)* Propagate placement across all ad-load events and all ad formats + AppLovin banner ad_shown
- *(iaa)* Block app-open pop immediately after rewarded/interstitial close
- *(iaa)* Decouple banner display/close from shared fullscreen event channel
- *(iaa)* Emit canonical ad_impression/shown/clicked/show_failed on AdMob preload path
- *(iaa)* Close remaining preload/legacy parity gaps (ad_loaded + RecordWatch)

### 🚀 Features

- *(analytics)* Route ad watch milestones through TrackCustomEvent

## [0.105.0] - 2026-04-16

### 🚀 Features

- *(iap)* Auto-emit first_purchase event to native trackers

## [0.104.0] - 2026-04-16

### 🐛 Bug Fixes

- *(iaa)* No-op IAdNetwork default methods + add cross-network HideBannerAd()

## [0.103.0] - 2026-04-16

### 🐛 Bug Fixes

- Resolve Maio vs AppLovin GAM GMA 13.x pod conflict
- Eliminate null-tracker revenue loss in AdRevenueTrackingManager

## [0.102.0] - 2026-04-15

### 🐛 Bug Fixes

- Route AdMob revenue to correct Taichi Process method per format

### 🚀 Features

- Add progress logs to Taichi ProcessAllFormatsThresholds
- Add progress logs for Taichi Steps 3-6

## [0.101.0] - 2026-04-15

### 🚀 Features

- Add canonical IAA event tracking + ad-watch milestones

## [0.100.0] - 2026-04-14

### 🐛 Bug Fixes

- Dispatch AdMob preloaded-ad callbacks to Unity main thread
- Skip secondary readiness check when ad_format_overrides pins the format
- Guard AdMob preload setup behind #if !UNITY_EDITOR
- Post banner AdMob revenue callback to main thread + add GetNetworkForFormat debug logs
- Marshal AdMob MobileAds.Initialize completion callback to Unity main thread

## [0.99.0] - 2026-04-14

### 🐛 Bug Fixes

- Field-by-field merge for FrequencyCaps and CooldownSeconds in IAA.MergeWith
- Secondary network fallback and Editor legacy path for banner/interstitial/rewarded

## [0.98.0] - 2026-04-13

### ⚙️ Miscellaneous

- Add ConfigLoadTest.cs.meta

### 🐛 Bug Fixes

- Correct AdjustConfig field reference in ConfigLoadTest (Android not AppToken)

### 🚀 Features

- CPM floor bidding, A/B experiment segmentation, and IAA diagnostics

## [0.97.0] - 2026-04-11

### 🐛 Bug Fixes

- Scripting define symbols not added when installing/updating ad SDKs

## [0.96.0] - 2026-04-11

### 🐛 Bug Fixes

- Remove Phase 1 immediate persist, fix is_sandbox null, fix ByteDance iOS version

## [0.95.0] - 2026-04-10

### ⚙️ Miscellaneous

- Add missing CLAUDE.md.meta to silence Unity immutable-folder warning

## [0.94.0] - 2026-04-10

### 🐛 Bug Fixes

- Detect and auto-fix 6 more cross-catalog iOS CocoaPods version conflicts

## [0.93.0] - 2026-04-10

### 🐛 Bug Fixes

- Downgrade Meta/Facebook Android MAX adapter to 6.20.0 to avoid Gradle 8 build failure
- Clean up Recommended Setup section UI
- Repaint window immediately after Install/Update/Remove click
- Compare MAX adapter versions per-platform, not cross-platform
- Set ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES=YES in iOS post-build
- Conditionally embed Swift runtime only when Swift adapter installed
- Remove redundant Swift dummy file injection — EDM4U already provides Dummy.swift
- Patch Podfile post_install to set BUILD_LIBRARY_FOR_DISTRIBUTION=NO for AppMetricaLibraryAdapter
- Auto-embed dynamic xcframeworks in Unity-iPhone via Podfile post_install hook
- Replace Podfile post_install with PBXProject embed for dynamic xcframeworks
- Move BUILD_LIBRARY_FOR_DISTRIBUTION fix from Podfile to PBXProject on Pods.xcodeproj
- Detect and auto-fix BidMachine cross-catalog iOS version conflict

### 🚀 Features

- Add CocoaPods conflict fixer + ad network adapter installer
- Auto-resolve UPM packages on Install/Update/Remove
- Show separate Android and iOS version columns in MAX adapter table
- Auto-fix Android duplicate deps and iOS CocoaPods conflicts via Noctua menu

## [0.92.0] - 2026-04-09

### ⚙️ Miscellaneous

- Remove TestMode from IAA config model and MediationManager
- Remove deprecated app_open_cooldown_seconds field

### 🐛 Bug Fixes

- Guard AdMob Preload API IsAdAvailable calls against JNI NPE and remove duplicate CreateNetworks call
- Defer secondary ad loading until secondary SDK has finished initialization
- Close missing #endif for UNITY_ADMOB rewarded block and move placement methods out of UNITY_APPLOVIN guard
- Revert rewarded_interstitial to legacy Load() path — Preload API not supported by AdMob SDK
- Add missing using com.noctuagames.sdk.Admob for RewardedInterstitialAdmob
- Scope TestMode AdMob test IDs to AdMob network only
- Copy extraPayload before mutating in TrackAdCustomEventRewardedInterstitial
- Switch to Unity main thread before invoking init callbacks in AdmobManager
- Dispatch MobileAds.Preload() on Android UI thread via Activity.runOnUiThread()
- Catch MobileAds.Preload NPE from GMS Dynamite module failure
- Reset _isFullscreenAdShowing on OnAdFailedDisplayed
- IsAppOpenAdReady includes cooldown check + OnAdNotAvailable for app_open
- Configure App Open secondary when secondary inits before primary
- Add missing TrackAdRevenue to MockAdRevenueTracker
- Resolve IAA namespace/type collision in IAAConfigTest using type alias

### 🚀 Features

- Migrate all AdMob ad loading to Preload API exclusively
- Per-format ad network routing, placement support, and centralised constants
- Selective remote config merge + OnAdNotAvailable callback

## [0.91.0] - 2026-04-07

### 🐛 Bug Fixes

- Add element-wise batch parse recovery for Android and iOS
- Log error and return instead of throwing on null/empty event name

### 🚀 Features

- Add stage_time_msec to game_stage_complete event
- Add feature_engagement event with time tracking and visit ID

## [0.90.0] - 2026-04-07

### 🐛 Bug Fixes

- Make NativeSessionTracker members public to resolve CS1061 build errors

## [0.89.0] - 2026-04-07

### 🐛 Bug Fixes

- Make NativeSessionTracker public to resolve CS0122 build error

## [0.88.0] - 2026-04-06

### revert

- Remove Obsolete from ExperimentManager session_id methods

### ⚙️ Miscellaneous

- Bump native SDKs and fix main thread marshalling
- Add missing .meta files for AppUpdateInfo and NoctuaAppManager
- Bump native SDK versions, add typedef fix, and add missing .meta files

### 🐛 Bug Fixes

- Remove GetPseudoUserId() leaked from unrelated branch
- Crash-recovery for orphaned sessions + immediate persist for session_start
- Register native lifecycle callback immediately to avoid missing first onResume
- Fire synthetic resume on NativeSessionTracker when app already in foreground
- Register native lifecycle callback after Init() to pass ensureInit() guard
- Correct session_id on recovery events and dispose tracker on graceful quit
- Capture caller session_id before reserved-key strip in EventSender.Send()
- Add timeout and single-flight lock to Firebase ID fetch in EventSender
- Replace ImmutableDictionary with Dictionary in NoctuaLocale to resolve CS0122
- Remove empty SSV options builder from RewardedAdmob

### 🚀 Features

- Add in-app review and in-app updates bridge
- Add native lifecycle callback bridge for engagement tracking
- Add PseudoUserId property to IEventSender interface
- Add pseudo_user_id to all events and route session_id through SetProperties
- Add lifecycle param to engagement events, per-session engagement, and session_id routing via SetProperties
- Expose GetPseudoUserId() public API, deprecate ExperimentManager session_id methods

## [0.87.0] - 2026-03-17

### 🐛 Bug Fixes

- Remove unused SSV options from rewarded interstitial ad

## [0.86.0] - 2026-03-17

### 🚀 Features

- Add Taichi tROAS ad impression threshold events

## [0.85.0] - 2026-03-12

### ⚙️ Miscellaneous

- Add Unity meta files for editor payment sheet assets
- Bump noctua-android-sdk to 0.27.1

### 🐛 Bug Fixes

- Filter out editor payment type from product list API request
- Use correct Kotlin JVM getter names for is-prefixed booleans

### 🚀 Features

- Add IAA improvements for AppLovin and AdMob
- Add Editor mock IAP payment sheet for dev testing
- Send payment type 'direct' to server for editor mock orders
- Skip create order for editor mock, show UI directly
- Add landscape support for editor payment sheet UI
- Add ad_user_id to ad revenue events and set AppLovin user ID

## [0.84.0] - 2026-03-10

### 🚀 Features

- Fallback to game_level when level is empty in game_stage_start

## [0.83.0] - 2026-03-10

### 🚀 Features

- Add ad_format to AppLovin ad revenue event

## [0.82.0] - 2026-03-10

### 🚀 Features

- Enrich all events with current_stage_level and current_stage_mode

## [0.81.0] - 2026-03-09

### 🚀 Features

- Auto-save game_stage_complete level and use for IAP CurrentStageLevel

## [0.80.0] - 2026-03-09

### 🐛 Bug Fixes

- Wire CompletePurchaseProcessing to finish SK1 transactions after server verification
- Fix CI publish pipeline — variable name, manual trigger, gitlab tag push, draft release

### 🚀 Features

- Add noctua_user_engagement event for Firebase-like engagement time tracking

## [0.78.0] - 2026-03-04

### misc

- Set minimum IOS version to 15 for ICM.
- Update native ios sdk version to 0.24.0 and 0.24.1 for icm in ci variable
- Update native android sdk to 0.20.0
- Update noctua native sdk ios to 0.24.0

### ⚙️ Miscellaneous

- Add debug log for active product id before currency lookup
- Bump native SDK versions (Android 0.27.0, iOS 0.30.0)
- Bump native iOS SDK version to 0.30.0
- Add Unity meta files for new directories and files
- Remove empty directories and stale meta files
- Add .meta files for Phase 4 new files
- Add missing .meta files, remove flaky FlushOnResume test
- Remove accidentally committed test artifacts

### ✨ Improvements

- IAAPreprocessor to BuildPreprocessor
- Remove unnecessary code
- Reorganize SDK into MVP + Platform Bridge architecture (Phase 1)
- Extract INativeFirebase, INativeEventStorage, INativeLifecycle from INativePlugin
- Narrow EventSenderConfig to INativeEventStorage and NoctuaIAPService to INativeIAP
- Extract IEventSender interface from EventSender
- Update services and UI to accept IEventSender instead of concrete EventSender
- Extract IPaymentUI interface, decouple UI creation from NoctuaIAPService
- Introduce IAuthProvider to decouple NoctuaIAPService from static Noctua.Auth
- Define IIAPService and IAuthenticationService presenter interfaces
- Move ad integrations from Platform/ to AdsManager/
- Fix AccessTokenProvider layer violation with IAccountEvents
- Decouple MediationManager from UIFactory and static Noctua.Event
- Extract nested PurchaseItem to InternalPurchaseItem, expand IIAPService
- Decouple Http.cs from static Noctua.Platform.Locale
- Decouple NoctuaIAPService from static Noctua.Platform.Locale
- Extract UI utility methods from Infrastructure/Utility.cs to UI/UIUtility.cs
- Add GetTranslation to ILocaleProvider, remove static locale calls
- Inject IConnectivityProvider into NoctuaIAPService
- Decouple MobileDateTimePicker from Noctua static facade
- Decouple EventSender from Noctua static facade
- Move EventSender from Network/ to Infrastructure/ root

### 🐛 Bug Fixes

- Adjust redeems API endpoint.
- Call OnPurchaseDone in one place to avoid double delivery.
- Fetch the playerID from recent account if not exist.
- Fetch the playerID from recent account if not exist.
- Persist access token in PlayerPrefs to avoid users becoming unauthenticated
- Remove unused ~> matcher in sed command for updating pod versions
- Text field email address in login dialog is stuck on the whole screens
- Applovin on user earned reward callback is not triggered
- Reduce size image ad placeholder
- Align deprecated AppLovin banner methods with AdViewPosition replacement
- Add empty dictionary to prevent null object
- Fix failing unit tests for per-row event storage migration
- Cache Firebase IDs to prevent iOS event loss and guard against quit-time crashes
- Adjust attribution is not track properly
- Move OnInitSuccess callback after sdk_init_complete for offline support
- Remove verbose debug log from GetEventCount
- Resolve push notification capability crash by providing entitlements path
- Update google-services to 4.3.15 and crashlytics to 3.0.6
- Comprehensive AdsManager bug fixes and improvements
- Resolve native plugin init race condition before GetActiveCurrencyAsync
- Replace Debug.Log with _log/_sLog in NoctuaIAPService and IosPlugin
- Add Flush() to IEventSender interface, include missing meta files
- Revert NoctuaIAPService native plugin type to INativePlugin
- Replace Debug.LogWarning with NoctuaLogger in infrastructure layer
- Make InternalPurchaseItem public to match IIAPService accessibility
- Replace Debug.Log with NoctuaLogger in remaining runtime code

### 🚀 Features

- Prevent game id null while offline
- Prevent reserved event keys from being overridden in internal tracker
- Add session id for all events internal tracker
- Handle purchase data from Redeem.
- Treat unpaired order as redeem and verify it like usual order.
- Add store pricing to onPurchaseDone callback parameters
- Pass the store pricing amount and currency to OnPurchaseDone.
- Implement bridge against firebase remote config methods.
- Add Objective-C → C# bridging function remote config
- Implement direct redeem as backup.
- Provide zero amount in purchase tracking for redeem.
- Automate the embedding process for AdjustSignature XCFramework
- Optimize iOS build process to add Adjust attribution endpoint and SKAdNetwork items
- Add offline_mode param into internal tracker
- Add meta file automation sh
- Add new remote config flag sendEventsOnFlushEnabled to prevent crash while flush event
- Add restore purchased products
- Add track adjust attribution with internal tracker
- Add get adjust attribution - ios
- Get adjust attribution ios
- Add unit test infrastructure with Makefile and code coverage
- Increase unit test coverage from 3.7% to 5.9%
- Expose ad unit id
- Add ClaimRedeemAsync API and unit tests
- Migrate Google Play Billing and iOS StoreKit to delegate to noctua-sdk-native
- Add debug log for event enqueue in EventSender
- Implement Cloud Saves.
- Track first_open custom event on initial app launch
- Add GetProductPurchaseStatusDetailAsync with full status model and expiry time
- Add current stage level to IAP purchase request
- Add logging for Adjust attribution details

## [0.56.0] - 2025-11-03

### misc

- Update readme

### ✨ Improvements

- Restrict IAP Init() and enable visibility to internal for SDK-only access
- Restrict authentication enable() visibility to internal for SDK-only access

### 🚀 Features

- Prevent requests to server while offline

## [0.55.0] - 2025-10-28

### 🚀 Features

- Update noctua sdk native version android to 0.15.0, ios to 0.22.0
- Add tag data to internal tracker for track TSPU
- Enhance retry mechanism for SDK initialization and authentication while offline

## [0.54.0] - 2025-10-24

### misc

- Bump version applovin sdk to 8.5.0
- Remove ui toolkit class ad-placeholder-banner

### 🐛 Bug Fixes

- Disabled internal tracker for debugger

### 🚀 Features

- Enhance initialization internal IAA
- Remove doubled internal tracker on admob
- Enhance applovin manager and prevent object null references
- Enhance logger ad placeholder asset source
- Increase SDK init retry delay to 10 seconds
- Add internal event tracker sdk init complete

## [0.53.0] - 2025-10-21

### misc

- Bump version to v0.53.0.

### 🐛 Bug Fixes

- Catch exceptions within event's geoIP.

### 🚀 Features

- Add xml documentation noctua public class
- Add xml documentation noctua authentication class
- Add xml documentation noctua iap class
- Add xml documentation noctua platform class
- Add xml documentation noctua event class
- Allow game to retrieve purchase orderID via event and exception.

## [0.52.0] - 2025-10-15

### 🚀 Features

- Enhance handler OnPurchaseDone by QueryPurchaseAsync
- Enhance init while offline

## [0.51.0] - 2025-10-06

### ✨ Improvements

- Load country in tracker.
- Determine country from geoIP first before call cloudflare API.

### 🚀 Features

- Add exception guards for Firebase ID native calls
- Prevent NullReferenceException when EventSystem is missing in scene

## [0.50.0] - 2025-09-18

### 🚀 Features

- Enhance default country code

## [0.49.0] - 2025-09-18

### 🚀 Features

- Update sdk native wrapper android version 0.13.0 to 0.14.0, ios version 0.17.0 to 0.18.0
- Add get firebase analytics session id and installation id
- Add firebase session id and installation id on internal tracker
- Update noctua native wrapper iOS 0.18.0 to 0.19.0
- Add get firebase installation id and session id
- Enhance additional internal event tracker on init and authentication sdk
- Add experiment manager

## [0.48.0] - 2025-09-04

### 🚀 Features

- Enhance payment status handling

## [0.47.0] - 2025-08-28

### 🚀 Features

- Update native wrapper sdk ios and android
- Add check product if purchased ios
- Completion delegate

## [0.46.4] - 2025-08-14

### 🐛 Bug Fixes

- Update native wrapper sdk

## [0.46.3] - 2025-08-13

### 🐛 Bug Fixes

- Update edm with new version
- Change built in event firebase ad revenue into ad impression

## [0.46.2] - 2025-08-01

### 🐛 Bug Fixes

- Add custom event with revenue

## [0.46.1] - 2025-08-01

### 🐛 Bug Fixes

- Rollback spm to cocoapods

## [0.46.0] - 2025-07-31

### 🚀 Features

- Add track event custom with revenue
- Add check google product purchased

## [0.45.0] - 2025-07-22

### ✨ Improvements

- Change GetStoreName to GetPlatformType

### 🐛 Bug Fixes

- Update asset ad placeholder and make it random show ad placeholder per ad type
- Add condition if iaa enabled equal false dont add the symbol

### 🚀 Features

- Add internal event tracker game_platform_type on init sdk

## [0.44.1] - 2025-07-14

### 🐛 Bug Fixes

- Move Xcode.Extensions import inside UNITY_IOS preprocessor block

## [0.44.0] - 2025-07-14

### ✨ Improvements

- Change to use Swift Package Manager and remove Cocoapods

### 🐛 Bug Fixes

- Update aps-environment to production
- Add more null check for lean noctuagg.json config.
- Add more null check.

### 🚀 Features

- Improve VN legal purpose, do not allow user to exit KYC.
- Add init callback

## [0.43.1] - 2025-06-26

### 🐛 Bug Fixes

- Remove space line

## [0.43.0] - 2025-06-26

### 🐛 Bug Fixes

- Issues collection was modified; enumeration operation may not execute on event sender
- Improve init iaa
- Version package not defined, noctua menu
- Install and remove iaa sdk not working properly

### 🚀 Features

- Noctua menu for unity editor

## [0.42.0] - 2025-06-16

### 🐛 Bug Fixes

- Built in iaa event tracker not track properly admob
- Vn full kyc when not enabled
- Email register vn translation wording
- Vn full kyc vn flow
- Translation error not found on email register vn dialog
- Validation form not working properly in email register vn dialog

### 🚀 Features

- Built in event tracker iaa admob
- Built in event tracker iaa applovin max

## [0.41.0] - 2025-06-05

### ⚙️ Miscellaneous

- Remove Script folder as we already have separated repository for config generation.

### ✨ Improvements

- AdFormat class into AdFormatNoctua to avoid confused class from Admob preloading configuration
- Change ad placeholder use uitoolkit
- Remove vn flow from email register dialog
- Adjust and add translation for email registration flow
- Translation for date of issues

### 🐛 Bug Fixes

- Adjust new config for sanbdox.
- Improve loop internet checker
- Enhance AuthenticateAsync to return empty data
- Remove double check offline status
- Improve init IAA SDK and analytics SDK
- Change variable config to _config
- Improve track ad custom event admob
- Improve IAA event queue
- PreloadManager class error not found when admob sdk is not installed
- Loop for override the client RemoteFeatureFlags if any is not working properly
- Adjust close ad placeholder for applovin max and add show ad placeholder for rewarded interstitial
- Class not found because admob not installed
- Adjust event queue name.
- Safely handle missing or null RemoteFeatureFlags for VN legal purpose
- Init not completed when remote config data is null
- Ui closed earlier when email verification got error
- Ui issues email register vn dialog

### 🚀 Features

- Implement remote feature flags. Refactor the configuration convention.
- Implement multilevel remote feature flags for VN (phone number verification).
- Update native SDK that bring new configuration convention.
- Improve offline first behaviour. Disable IAP on Unity SDK level if asked by config.
- Add built in login event tracker
- Add loop check internet connection
- Add tracker for internal IAA admob
- Thread-safe event queue for Admob events to ensure they are processed on the main thread
- Add event ads tracker applovin
- Add disbaled adjust offline mode by remote config
- Add admob ad preload manager
- Add preloading admob for ad format interstitial and rewarded
- Add noctua ad placeholder
- Add admob checker when using openupm-cli
- Add Ad Placeholder for Banner and Rewarded Ad
- Admob check init adapter
- Implement VN OEG phone number verification.
- Email register and phone verification for VN

## [0.40.1] - 2025-05-27

### 🐛 Bug Fixes

- Error 'EventSender.Flush()' returns voiid, a return keyword must not be followed by an object expression

## [0.40.0] - 2025-05-27

### misc

- Improve log when the event queue is full.

### 🐛 Bug Fixes

- Header value contains invalid characters
- Object reference not set to an instance of an object when show start game error dialog
- Adjust event queue name.
- Change cycle delay to 5 seconds

### 🚀 Features

- Add limit to event queue length.

## [0.39.7] - 2025-05-05

### 🐛 Bug Fixes

- Decrease event sender interval from 5 minutes to 1 minute.

## [0.39.6] - 2025-05-02

### 🐛 Bug Fixes

- Swap incorrect offline mode mapping.

## [0.39.5] - 2025-04-30

### ⚙️ Miscellaneous

- Update ios native sdk to 0.13.5 (support for disabling IAP).
- Update noctua sdk ios to 0.14.0

### 🐛 Bug Fixes

- Handle internet checker crashes when app is quitting
- Handle event flush crashes when app is quitting

## [0.39.4] - 2025-04-29

### 🐛 Bug Fixes

- Skip event flush on IOS as it could cause unexpected crash while sending the event.

## [0.39.3] - 2025-04-28

### 🐛 Bug Fixes

- Add checker when the game is quitting or not playing to avoid crashes
- Handle exeption when check internet connection
- Send offline event only on non-offline event.

## [0.39.2] - 2025-04-23

### 🐛 Bug Fixes

- Handle all status error code above 408 (including 522) properly.

## [0.39.1] - 2025-04-21

### 🐛 Bug Fixes

- Sdk version not found
- Do not track purchase_verify_order_failed if verify triggered by retry, worker, or loop.
- Move some log.Info to log.Debug.

## [0.39.0] - 2025-04-21

### 🐛 Bug Fixes

- Keep init nativePlugin outside the IAA case.
- Add more debug log around nativePlugin initialization.
- Hook up internet check on event sender loop. Increase granularity and decrease batch size.
- Add note to catch 500 error to suppress init error dialog in onfline-first mode.
- Simplify offline checker, fix misleading function name.
- Send offline event only if it is previously online.
- Handle 500 error in offline-first.
- Catch IsOfflineAsync result with ContinueWith.

### 🚀 Features

- Add bridging against native onOnline and onOffline methods.

## [0.38.0] - 2025-04-16

### 🐛 Bug Fixes

- Rename IAAResponse to IAA
- Remove unecessary code
- Remove whitespace
- Function not found when IAA SDK not installed
- Update comment for IAA init flow
- Update comment for IAA flow
- Refactor log error offline mode as info
- Missing responseInfo class admob
- Change convert value micros
- Preprocessor ios symbols
- Ad revenue tracker crashes
- Ios init ianalytics flow code is not working
- Change condition when iaaEnabled is false
- Keep native plugin init one time
- Improve EventSender.
- Backup internal tracker events to PlayerPrefs. Remove RetryAsync since there is already main loop that handle retries. Reenqueue if failed.
- Try to parse to IConvertible first before parse to object to reduce computation cost.
- Guard flush with try catch inside the async await.
- Remove merge conflict marker.

### 🚀 Features

- Add reference for AppLovin MAX SDK
- Add IAA Preprocessor for check mediation SDK is exists and generate the symbols #if UNITY_ADMOB and #if UNITY_APPLOVIN
- Add MediationManager for unified mediation SDK handling
- Add Admob Manager to handling function from ad format functions
- Add AppLovin Manager to handling function from ad format functions
- Add IAdNetwork interface for mediation abstraction
- Add InterstitialAdmob class for managing AdMob interstitial ads
- Add BannerAdmob class for managing AdMob Banner ads
- Add RewardedAdmob class for managing AdMob Rewarded ads
- Add InterstitialAppLovin class for managing AppLovin interstitial ads
- Add RewardedAppLovin class for managing AppLovin Rewarded ads
- Make MediationManager a public class for external access
- Make set ad unit id used function by self
- Add banner applovin
- Add isIAAEnabled to handle init analytics SDK after init IAA SDK success
- Add data class to handle mediation response
- Add event handler admob
- Add event handler for applovin
- Add mediation debugger, creative debugger and enhance some functions
- Rewarded interstitial admob
- Add ad revenue tracker admob and applovin
- Add ad revenue internal tracker
- Add internal tracker
- Add NoctuaEvents to registered player prefs keys.

## [0.37.3] - 2025-04-09

### 🐛 Bug Fixes

- Print serialized-parsed unpaired orders instead of the JSON one.

## [0.37.2] - 2025-04-09

### 🐛 Bug Fixes

- Check null after JsonConvert.Deserialize.

## [0.37.1] - 2025-04-08

### 🐛 Bug Fixes

- Add more observability on the log around unpaired order creation.
- Add more observability on the log around unpaired order creation.

## [0.37.0] - 2025-03-24

### 🚀 Features

- Add ShowSocialMedia() feature.

## [0.36.1] - 2025-03-24

### 🐛 Bug Fixes

- Limit the generated unused port range from 61000 to 61010 so we can register these determined range to Google SSO setup.

## [0.36.0] - 2025-03-11

### 🐛 Bug Fixes

- Error message not expected

### 🚀 Features

- Add OnPurchaseDone event to let the game know if a purchase is completed.

## [0.35.1] - 2025-02-25

### 🐛 Bug Fixes

- Corrected a typo on VerifyOrderTrigger.payment_flow.

## [0.35.0] - 2025-02-24

### refacor

- Add comment for closeDatepicker android

### ✨ Improvements

- Improve code close datepicker and open datepicker
- Clean IAP Service code for handle offline mode
- Clean web content code for handle offline mode

### 🐛 Bug Fixes

- Save button not disabled on starting edit profile
- Update user with the latest data.
- Increase choose account heigh
- Remove welcome notification border
- Eye naming
- Increase choose account heigh
- Datepicker not closed when back into user center in ios
- Number keyboard on verification input field
- Remove noctua text logo when potrait
- Resize icon account delete and change height dialog - account deletion confirmation dialog
- Improve dropdown field code
- Player label not in center position
- Color delete button
- Add localization on error message
- Strange grey selection in account selection
- Eye in show password design is reverse
- Remove error message when resend verification code on verification code window
- Add more padding left for player label - bind conflict dialog
- Keyboard blocking text input - edit profile
- Update credential to last used
- Missing name label - user center
- Remove Apple SSO if not on iOS
- Remove purchase timeout. Store pending purchase early. Add more valuable logs.
- Add payment update listener and pair it with unpaired order IDs. Adjust network related error message for start game error dialog.
- Add NoctuaUnpairedOrders to SDK's player prefs keys.
- Allow payment override from backend only if it's started as primary payment.
- Adding new item to pending purchase is now having old receipt data preserved.
- Revert bug fix that cause payment loop. Add notes instead.
- Fix missing user properties in event tracking.
- Conflict
- Conflict noctua file
- Conflict translation
- Update url test ping
- Add }
- Logic code check internet connection
- Rename isAuthSDK to welcomeToastDisabled
- Handle Online then Offline to show retry dialog
- Handle retry platform when Online then Offline to show retry dialog

### 🚀 Features

- Logout confirm dialog
- Logout left icon
- Localization for logout confirmation dialog
- Add Apple SSO
- Add text setting asset and font Noto Sans Thai
- Add text setting for config default font to panel setting
- Implement QueryPurchases to handle pending purchase. Split log to 800-char chunks.
- Pair unpaired purchase to unpaired order for both QueryPurchasesAsync and OnPurchaseUpdate. Add purchase history list.
- Get store/platform name
- Add request header X-PLATFORM into build config
- Add request header X-PLATFORM, X-OS-AGENT, X-OS for all http request
- Add request params exhange token, login as guest
- Add is auth with sdk checker to hide welcome notification
- Add default variable _offlineMode
- Add information about who trigger the verify order API call.

## [0.34.1] - 2025-01-24

### 🐛 Bug Fixes

- Revert hardcoded deviceId.

## [0.34.0] - 2025-01-24

### 🐛 Bug Fixes

- Reinit if the Playstore billing get disconnected at purchase.
- Add translation for IAPNotReady error.

### 🚀 Features

- Hide native payment button for direct APK distribution.

## [0.33.1] - 2025-01-14

### 🐛 Bug Fixes

- Increase timeout. Show error message toast for Platform Content.

## [0.33.0] - 2025-01-14

### refacor

- Add comment for closeDatepicker android

### ✨ Improvements

- Improve code close datepicker and open datepicker

### 🐛 Bug Fixes

- Show error if no token when getting web content details
- Save button not disabled on starting edit profile
- Update user with the latest data.
- Show mobile input on user center edit profile nicknameTF
- Increase choose account heigh
- Remove welcome notification border
- Eye naming
- Show error if no token when getting web content details
- Increase choose account heigh
- Remove welcome notification border
- Conflict
- Datepicker not closed when back into user center in ios
- Number keyboard on verification input field
- Remove noctua text logo when potrait
- Resize icon account delete and change height dialog - account deletion confirmation dialog
- Improve dropdown field code
- Player label not in center position
- Color delete button
- Add localization on error message
- Strange grey selection in account selection
- Eye in show password design is reverse
- Remove error message when resend verification code on verification code window
- Add more padding left for player label - bind conflict dialog
- Keyboard blocking text input - edit profile
- Update credential to last used
- Work around for fullscreen web content on iOS not aligned
- Restore readonly property of _credentials.

### 🚀 Features

- Logout confirm dialog
- Logout left icon
- Localization for logout confirmation dialog
- Add Apple SSO
- Implement remote config to enable or disable SSO.
- Implement remote config for user center SSO linking.
- Add HTTP timeout at 10 seconds. Add X-DEVICE-ID in the header.
- Add APIs to help gamedev maintains SDK playerPrefs.

## [0.32.0] - 2025-01-09

### 🐛 Bug Fixes

- Button reset password override error label
- Adjust style dialog footer
- Fix missing profile picture in edit mode.
- Revert deleted line.
- Show mobile input on user center edit profile nicknameTF
- Non guest email linking not working
- Fix minor styling issues.
- Birthdate value not set - edit profile
- Adjust clean code

### 🚀 Features

- Allow user to pay with native payment in custom payment complete dialog.

## [0.31.0] - 2025-01-08

### 🐛 Bug Fixes

- Update code email register dialog
- Update code email reset password dialog
- Update code email confirm reset password dialog
- Update code email confirm reset password dialog
- Code format
- Force scaling at startup.
- Back to user center after email linking
- Change user nickname to user id in bind conflict
- Use exit button with new style
- Use AuthService instead of removed Model proxy method
- Register and login back to login options
- Prevent copy player when scrolling
- Dropdown error label - edit profile
- Filter pending purchases by player id
- Logo profile teralu keatas saat guest diarahkan ke prompt buat switch account
- In edit profile, the loading screen is not the standard UI theme
- Failed to switch accounts if user logged in to other games
- Resolved an issue where the date picker could appear multiple times in the iOS registration flow VN.
- Resolved an issue where the date picker could appear multiple times in the iOS edit profile.
- Instead cancel, create new player if user doesn't want to bind guest
- Webview scale should be floating point
- Reversed match panel settings parameter
- Button not consistent in switch account confirm dialog
- Button not consistent in account deletion confirm dialog
- Change position confirm button - switch account confirm dialog
- Button position not consistent in bind confirmation dialog
- Button position not consistent in bind conflict dialog
- Translate pending purchase dialog to id and vi
- Add PlayerID as parameter for retry pending purchase item. Use JWT parser as backup.
- Add Player ID as VerifyOrderImplAsync param.
- Remove GetPlayerIdFromJwt.
- Bring back disappearing verification code
- Style pending purchase to match UI design
- Close button position
- Change margin close button failed payment dialog
- Add retry to event sender
- Usercenter design not matches

### 🚀 Features

- Allow payment type override from backend.

## [0.30.1] - 2024-12-27

### 🐛 Bug Fixes

- Check for existing instance before call Firebase::configure.

## [0.30.0] - 2024-12-27

### ⚙️ Miscellaneous

- No verbose log for HTTP requests with passwords

### 🐛 Bug Fixes

- Auto scaling problem
- Delete old code
- Wrong URL detection cause SSO to fail
- Webview scale broken due to scaling change
- Translation bind or add other account
- Change button style
- Scale should adjust with auto rotate
- Check for existing instance before call Firebase::configure.

### 🚀 Features

- Add pending purhase menu translation vn

## [0.29.0] - 2024-12-22

### 🐛 Bug Fixes

- Update native sdk for ios

### 🚀 Features

- Add custom app controller
- Automation add capability push notification

## [0.28.0] - 2024-12-20

### 🐛 Bug Fixes

- Add null check for account container in email login widget.
- Change picture button is gone after success update image
- Change picture button not center
- Spinner edit profile
- Update design exit pada account selection dialog
- Remove player username from user display name prioritization list.
- Date picker ios
- Improve UX on Vietnam registration.
- Button not active when change date picker and remove not necessary code
- Close the entire user center after profile is successfully updated.
- Remove duplicate lines.
- Reload the entire user center presenter to avoid unexpected bug.

### 🚀 Features

- Add copublisher logo on register widget. Improve register user experience.
- Pop up success message linked account
- Localization wording account linked

## [0.27.1] - 2024-12-20

### 🐛 Bug Fixes

- Use scrollable page instead of pagination for pending purchase widget.
- Different color panel pop up in bind confirmation dialog and connect conflict dialog
- More option menu still showing after closing and reopen the user center panel
- Design dropdown panel
- Save button turn blue on disable
- Login and register screen looped or stacked
- Use currentActivity instead of applicationContext to ask notification permission
- Check null on EditProfile saveButton enable and apply translations to dropdown fields

## [0.27.0] - 2024-12-19

### ⚙️ Miscellaneous

- Add SDK version to header.

### 🐛 Bug Fixes

- Back button is hard to click
- Add retry on failed init due to connection error
- Make sure pending purchases get removed from persistent storage after it get verified.
- Close the loading spinner widget if there is exception in User Center initialization.
- Add order ID validation for VerifyOrderImplAsync().
- Typo
- Add ID label in user ID
- Cutted input field
- Add ID label in user ID
- Cutted input field
- Account selection dialog close button move to right
- User center image resolution
- Align back button with tittle header
- Update reset password wording
- Show error message if payment failed
- Revamp pending purchases widget and add more functionalities (CS, Retry, Copy) and payment details.
- Update error message for Pending Purchase retry attempt.
- Find more website can clickable
- Remove blue login button if the state is still in link account process
- Edit profile ui
- User center isssues
- Apply localization immediately on language change
- Account selection dialog close button move to right
- User center image resolution
- Align back button with tittle header
- Update reset password wording
- Translate text field title except in user update dropdown field
- New scrollbar design
- Change wording connect account when guest user
- Tnc text size and position
- Text to center of button and bottom
- Copy icon change to copy button
- Revamp ui edit profile
- Append reason for customer service URL. Add loading spinner for PlatformContent API.
- Profile edit profile image streched
- Add start game error dialog if sdk init failed
- Error dialogue to overflow
- Add null check on register by email verification.
- Add null check on register by email verification.
- Error dialogue to overflow
- Overflow label text in the new design
- Nickname not update realtime after success edit
- Nickname field empty then save, change picture button is gone
- Use relative path instead of absolute path
- Adjust margin bottom save and remove not necessary code
- Localization text and key
- Typo key
- Update android native sdk to 0.9.0

### 🚀 Features

- Add Accept-Language in HTTP header to help error message translation in backend side.
- Show switch account menu in user center for guest account.
- Add copy localization languages
- Localization for text input validation message - edit profile

## [0.26.0] - 2024-12-16

### ⚙️ Miscellaneous

- Downgrade locale log to Debug.

### 🐛 Bug Fixes

- Add sandbox flag to events
- Add 18 years min age for VN
- Edit Keyboard type to match the value
- Logo switch account yg gepeng
- ConnectConflictDialog Cancel button color to blue
- Region vn not translated
- Datepicker open twice, hard to click, disabled focusable - edit profile
- Improve custom payment cancelation logic.
- Put back user pref for language determination.
- Update Pending Purchases widget title according to the total of the purchases.
- Reset password doesn't automatically login
- Rename SDK first_open to sdk_first_open to differentiate with custom tracker event.

### 🚀 Features

- Registration wizard for Vietnam region.
- Add locale information in HTTP request header.
- Add translation for Retry and Custom Payment Complete dialog.
- Prepare retry pending purchases container.
- Add Pending Purchase widget for both guest and authenticated user.

## [0.25.2] - 2024-12-12

### 🐛 Bug Fixes

- Adjust retry pending purchase mechanism to make it more persistent for upcoming failed verification.

## [0.25.1] - 2024-12-12

### 🐛 Bug Fixes

- Always unset the visibility of current dialog before calling ShowCustomerService().

## [0.25.0] - 2024-12-12

### 🐛 Bug Fixes

- Tidy up some UIs.

### 🚀 Features

- Enable secondary payment after Noctuastore payment get canceled.

## [0.24.0] - 2024-12-12

### ⚙️ Miscellaneous

- Print all custom tracker event parameters for easier debugging.

### 🐛 Bug Fixes

- Landscape user account design
- Remove payment options
- Add spinner after click continue button in email login
- Add spinner after click continue button in email login
- Update spinner grafic
- Failed init should disable auth completely
- Email confirm reset password panel not move to top when virtual keyboard showing
- Add noctua games to manifest at build times
- Adjust retry dialog ui into center
- Adjust retry mechanism code
- Fix some logs in retry dialog presenter
- Remove log when show retry dialog
- More robust implementation to add keychain sharing
- Filter payment type by runtime platform. Open payment URL with native browser.
- Remove currency from edit profile to prevent user playing around with currency to get cheaper goods.

### 🚀 Features

- Default avatar
- Add action help button
- Add geo metadata in tracker event's extra payload.
- Retry dialog ui
- Retry mechanism for create order and verify order
- Show error notification - purchase
- Get noctua gold
- Add Noctua Payment implementation using native browser with improved retry pending purchase.

## [0.23.0] - 2024-12-10

### ⚙️ Miscellaneous

- Add LICENSE

### 🐛 Bug Fixes

- Bring extra params for create order.
- Ui edit profile strached
- Icon more option streched - user center
- Change help button to position end - user center
- Change exit button position to flex end - edit profile
- Throws exception on Google Billing error
- Remove superfluous exception message
- Remove permission conflict

### 🚀 Features

- Improve currency accuracy by using country to currency map.

## [0.22.1] - 2024-12-06

### 🐛 Bug Fixes

- Match OrderStatus enum to backen types

## [0.22.0] - 2024-12-05

### 🐛 Bug Fixes

- Login options dialog ui issues
- Text button login with email
- Set default/fallback currency to USD.
- Naming convention
- Show error when playstore payment failed
- Addtional params for product purchase

### 🚀 Features

- Show/Hide password in email login pop up
- Add extra params in purchase request
- Add display_price in product class
- Add extra param in UpdatePlayerAccountAsync - purchase

## [0.21.0] - 2024-12-05

### ✨ Improvements

- Sso logo stretched code

### 🐛 Bug Fixes

- Set platform distribution by platform OS instead of payment type.
- Sso logo stretched
- Update IOS upstream library that fix Adjust event map null check.
- Remove justify in root
- Add translation to Bind Confirmation and Connect Conflict Dialog
- Wording id 'continue with another account'

### 🚀 Features

- Copy user data to clipboard when selected account held down for 3 seconds
- Bind confirmation and connect conflict dialogs
- Show/Hide password in email login pop up

## [0.20.0] - 2024-12-03

### ⚙️ Miscellaneous

- Add logging to aid debugging

### ✨ Improvements

- Do not write if url sentry is empty
- Change _log to Debug.Log
- Change noctua logger init position

### 🐛 Bug Fixes

- Change from Noctua.Log.Debug to _log.debug
- Delete log http
- Set result for google billing product details and makes CreateOrder works again
- Forgotten temporary undef

### 🚀 Features

- Add sentry dll files
- Add configuration sentry
- Add Dsn sentry url to config
- Log json body http
- Update sdk native version

## [0.19.9] - 2024-11-29

### 🐛 Bug Fixes

- Linker error for removed noctuaCloseKeyboardiOS

## [0.19.8] - 2024-11-28

### 🐛 Bug Fixes

- Update Android SDK to remove QUERY_ALL_PACKAGES permission

## [0.19.7] - 2024-11-28

### ⚙️ Miscellaneous

- Initiate locale once, then inject it anywhere we need.

### 🐛 Bug Fixes

- Determine language by this priority; user preference, region, system language.
- Update user prefs payment type with string instead of numeric enum.
- Use Active label instead of Recent for current active/recent account.
- Update wording for continue with another account button.
- Update the user language preference immediately after successfully update to backend.
- Remove duplicate HTTP log.
- Keyboard not closed after entering input

## [0.19.6] - 2024-11-27

### 🐛 Bug Fixes

- Keyboard show up at startup
- Blur if visible false

## [0.19.5] - 2024-11-27

### 🐛 Bug Fixes

- Simplify enum conversion

## [0.19.4] - 2024-11-26

### ✨ Improvements

- Change throw exeption to log warning

### 🐛 Bug Fixes

- Use fallback if native account store is unavailable.

## [0.19.3] - 2024-11-25

### 🐛 Bug Fixes

- Handle webview url empty
- Expose iOS logs to Files app

## [0.19.2] - 2024-11-22

### 🐛 Bug Fixes

- Session not initialized in iOS
- Purchase error blocked by loading box and platform param should be included in  get product list
- Notif box text should not overflow
- Redirect some Debug.Log to files and logcat/os_log
- Remove unnecessary namespace from Editor assembly
- UI event handling breaks when changing scenes
- Clear login form after success
- Clear form register after success

## [0.19.1] - 2024-11-20

### 🐛 Bug Fixes

- Welcome toast doesn't show up at first call of AuthenticateAsync

## [0.19.0] - 2024-11-20

### ✨ Improvements

- Method authenticateAsync
- Rename GeneralConfirmDialog to ConfirmationDialog
- Used color for hyperlink - translation for user banned
- Rename with spesific name banned confirmation dialog
- Rename method name to ShowBannedConfirmationDialog
- Changed to async and return UniTask

### 🐛 Bug Fixes

- Update translation vn
- Function authenticateAsync
- Rename key localization contact support
- Make throw exeption after user clicked button or hyperlink
- Retry saving account if failed

### 🚀 Features

- Add translation for user banned info
- Add exception error code for user banned
- General confirm dialog for user banned
- Add public method general confirm dialog
- Add handle error user banned - login with email

## [0.18.2] - 2024-11-19

### 🐛 Bug Fixes

- Overlay UI should block whole screen
- Account item should have no hover and connected account item should have no action indicator
- Align SDK version text in UserCenter

## [0.18.1] - 2024-11-18

### 🐛 Bug Fixes

- Ios log using os_log

## [0.18.0] - 2024-11-13

### 🚀 Features

- New spinner and logger to file

## [0.17.0] - 2024-11-13

### 🐛 Bug Fixes

- Virtual keyboard not hidden in iOS
- Validate call function close keyboard only ios
- Update noctua android sdk native to 0.6.0
- Change to follow BE payment type

### 🚀 Features

- Makes accounts available across games in iOS
- Add bridging function close keyboard ios
- Firebase crashlytics

## [0.16.0] - 2024-11-07

### 🐛 Bug Fixes

- Update iOS SDK to lower Facebook SDK version that doesn't have Swift float linker error
- Support ban user by exchange token for current game
- Track USD as revenue while still keeping  original currency

### 🚀 Features

- Dynamic custom event suffix for Android and iOS
- Add sdk version to account selection and user center

## [0.15.1] - 2024-11-01

### 🐛 Bug Fixes

- Tracker can be used without calling init
- Account bound should be fired on registering email with guest account
- Add semicolon to CI
- Purchase completed also send to native tracker
- Use credential provider before deleted when sending event
- Still used noctua logo
- Ui loading progress
- Make default show exit button for behaviour vn
- Remove params isShowBackButton
- Remove SSO connect UI - user center
- Hide noctua logo welcome notification

## [0.15.0] - 2024-10-29

### 🐛 Bug Fixes

- Text not translated - user center
- Text not translated - email register
- Object reference not set when open user center the first time (not logged yet)
- Translation loading
- Add retry to Google Billing init
- Use WebUtility instead of HttpUtility to be compatible with .NET Framework API Level

### 🚀 Features

- Add events to IAP and fix retry pending purchases
- Add platform content events
- Translation vn
- Add translation vn language
- Add translation for select gender and country
- Add session tracking

## [0.14.0] - 2024-10-25

### 🐛 Bug Fixes

- Adjsut name some widget to support localization - email login
- Update the localization text sources
- Enhance code localization data
- Update text localization
- Rename label name text
- Optimaze config localization code
- Enhance code to support localization - user center
- Translation en type widget
- Translation id type widget
- Translate label inside the button as container
- Change debug log to noctua debug log
- Add support for Unity versions with older Gradle 6
- Optional copublisher config should be ignored instead of exception
- Add elvis operators to potentially null configs

### 🚀 Features

- Authentication builtin tracker
- Add events  to Auth UIs, add double events to some auth process
- Add localization EN json file
- Add configuration for localization
- Add localization
- Add indonesia localization file

## [0.13.0] - 2024-10-22

### ✨ Improvements

- Open date picker - user center

### 🐛 Bug Fixes

- Shim for android sdk with content provider
- Guest can't bind account
- Change flag by company name
- Update library date picker android native
- Conflict rebase
- Update bridging file date picker native ios
- Adjust code conflict
- Update player avatar
- Change req_extra to dictionary
- Add idPicker params - showDatePicker
- Adjust code to filter non guest account - account selection
- Enhance flag checking more robust
- Make token optional for fetching platform content.

### 🚀 Features

- Add cross-game account storage
- Add countries data and phone code
- Add registration extra params for behaviour vn
- Form field for behaviour whitelabel vn
- Picker id param
- Add form register for behaviour whitelabel vn
- Configuration behaviour whitelabel vn
- Remove close/back button in login with email for behaviour whitelabel vn
- Don't show notif user guest if behavior whitelabel vn is true
- Disable SSO for Behaviour whitelabel vn
- Show direct login with email when player continue with other account
- Add reusable ContainsFlag Checker
- Add event tracker generator for multiple platforms and multiple thirdparty trackers.

## [0.12.0] - 2024-10-16

### ✨ Improvements

- Name logo with text - user center
- Method get co publisher logo

### 🐛 Bug Fixes

- Tnc and privacy can clickable
- Don't destroy panelSettings when switching scene

### 🚀 Features

- Add oeg logo
- Whitelabel - user center
- Add whitelabel - login options
- Add whitelabel - account selection
- Add reusbale get co publisher logo
- Add configuration for whitelabel

## [0.11.0] - 2024-10-14

### 🐛 Bug Fixes

- Add more failsafe around SDK init.
- Style button save - edit profile
- Disable button when text input is empty
- Disable button when text input is empty
- Disable button when text input is empty
- Disable button when text input is empty
- Prevent registering twice
- Optimaze validate textfield code
- Calculating webview frame

### 🚀 Features

- Add copy user id to clipboard
- Reusable validate textfield

## [0.10.0] - 2024-10-11

### 🐛 Bug Fixes

- Color not change message notification
- Back to user center after update profile success
- Spinner ui not correct
- Spinner ui, profile url null will not loaded, detect value changes to enable button save
- Dont destroy UI with new scene

### 🚀 Features

- Add method detect multiple values changes

## [0.9.1] - 2024-10-10

### ⚙️ Miscellaneous

- Split long log

## [0.9.0] - 2024-10-10

### ✨ Improvements

- Remove utility method remove white space
- Use method directly to remove white space

### 🐛 Bug Fixes

- Include token only if it's guest
- Error label not hide when on loading spinner
- Email not valid when have white space
- Email not valid when have white space
- Hide welcome notificaiton when success reset password
- Change wording Continue to Reset Password and Login
- Rollback after reset password and then login
- Hide show login with email after reset password success
- Bug ui edit profile and adjust save change profile
- Ordering ui message notification and loading progress
- Update VerifyOrderResponse struct to match with BE.
- Error label not showing when email verif code
- Apply scaling consistently between editor and real device

### 🚀 Features

- Add method remove white space

## [0.8.0] - 2024-10-07

### ✨ Improvements

- Moves Event methods under NoctuaEventService class
- Makes UI creation more reusable via UIFactory
- Take out social authentication flow from AuthenticationModel
- Use enum status and move tcs out of retry
- General notificaiton can be reusable
- Hide loading progress for temporary
- General notification message and loading progress

### 🐛 Bug Fixes

- More options menu shows on guest account opening user center
- Clear email reset password confirmation on start and makes error text not floating
- Rebase and resolve conflict
- Nickname input field and profile image and button change profile
- Change method post to get profile options
- Use tokens only if needed
- Bug ui edit profile when directly close
- Margin dropdown and border color
- Retry pending purchases with backoff
- Update native sdk to delete manifest entry that removes gms.permission.AD_ID
- Add validation dropdown and add default value for payment type
- Error label not hide
- Date picker default value
- Edit profile not working
- Remove log
- Set enabled payment types priority higher than user preferences.
- Update to ios-sdk-v0.2.0 to include facebook sdk as static framework
- Makes webview can be wrapped with UI and don't show again toggle
- Verif order not working
- Verif order not processed
- Get receipt data from response payment url noctua wallet

### 🚀 Features

- Add 3rd party NativeGallery
- Add edit profile service
- Add file uploader services
- Add get profile options service
- Date picker and refactor code
- Image picker
- Add payment by noctua website
- Spinner, error label and styling dropdown
- Add NoctuaWebContent
- Add payment type in user profile
- Noctua logo with text footer in edit profile left side
- Add date picker
- Add loading progress when iap
- Add parse query string

## [0.7.0] - 2024-09-18

### ✨ Improvements

- Indicator style code to uss code
- Remove comparation state

### 🐛 Bug Fixes

- Ios bridging init
- Truncate long PlayerName
- Update iOS SDK version to fix JSON serialization crash
- Show error on failed social link
- Change auto properties to public fields to avoid code stripping
- Remove get set to preserve deeply.
- Make Firebase tracker works from Unity
- Configure firebase Android from Unity
- Facebook player avatar
- Guest binding offer
- Guest connect button
- Remove GoogleService-Info.plist from project if Firebase disabled

### 🚀 Features

- Implement account deletion with confirmation dialog.
- Implement purchaseItem bridging against native SDK.
- Icon two hand carousel
- Code to show object in Noctua.uss
- Uss code configuration for carousel
- Add uxml carousel in user center
- Add carousel logic in user center presenter
- Wire up GetActiveCurrency for Android. Use UniTask for PurchaseItemAsync.
- Apply facebook config to android project

## [0.6.0] - 2024-09-09

### ✨ Improvements

- Makes config load more robust

### 🐛 Bug Fixes

- Add optional redirect_uri on desktop
- Add uniwebview android AAR and moves UniWebView inside Plugins folder
- Add facebook login support
- Handle error on social login failed
- Should throw error response from BE

### 🚀 Features

- Add UniWebView
- Warning icon (error notification icon)
- Error notification ui
- Add public method show general notification error
- Add method show notification error user center
- Add method show notification error login options

## [0.5.2] - 2024-09-07

### 🐛 Bug Fixes

- Font and layout doesn't render correctly

## [0.5.1] - 2024-09-06

### 🐛 Bug Fixes

- Unwanted selected background on listview
- Margin top dialog-title
- Check for BOM characters before skipping

## [0.5.0] - 2024-09-04

### 🐛 Bug Fixes

- Use link endpoints instead of login
- Remove warning and fix MoreOptionsMenu styles
- Make icons on MoreOptionsMenu smaller
- IOS and Android runtime error

### 🚀 Features

- Click outside to close MoreOptionsMenu

## [0.4.0] - 2024-09-02

### wip

- User center

### ✨ Improvements

- Move UI actions to NoctuaBehavior
- Conform more closely to MVP pattern
- Delete unused bind dialog

### 🐛 Bug Fixes

- Change name function
- Split account list into game users and noctua users
- Fix dummy var initiation
- Reset password endpoint and request
- VerifyCode is only for registration
- Check size before slicing
- Styling, navigation, memory leak
- Rename ShowUserCenterUI() to UserCenter()
- User center get data from /api/v1/user/profile

### 🚀 Features

- Login dialog ui
- Register dialog ui
- Login dialog style
- Email verification code ui
- Login options dialog
- Add player avatar
- Add user center
- Edit profile ui
- Add cs file edit profile
- Skeleton for register and reset-password flow
- Login options dialog
- Enable social login
- Implement UpdatePlayerAccountAsync
- OnAccountChanged and OnAccountDeleted
- Social login user center
- Change user center layout based on screen orientation

## [0.3.0] - 2024-08-15

### 🚀 Features

- Guest login integration
- Add welcome notification

## [0.2.0] - 2024-08-08

### 🚀 Features

- Integrate ios plugin

## [0.1.2] - 2024-07-31

### 🐛 Bug Fixes

- Change AndroidJNIHelper.Box for 2021.3 compatibility

## [0.1.1] - 2024-07-25

### ⚙️ Miscellaneous

- Add CI for release
- *(ci)* Fix invalid yaml
- *(ci)* Fix invalid yaml again
- *(ci)* Fix curl
- *(ci)* Fix skipped bump-version
- *(ci)* Generate release notes for github

### 🐛 Bug Fixes

- .gitlab-ci.yml rules

## [0.1.0] - 2024-07-24

### 🚀 Features

- Basic event trackers wrapping Noctua Android SDK


