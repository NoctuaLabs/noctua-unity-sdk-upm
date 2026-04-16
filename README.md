# Noctua SDK for Unity

**Version:** 0.101.0 | **Unity:** 2021.3+ | **Platforms:** Android, iOS

For full integration details and usage guides, visit the official documentation: https://docs.noctua.gg

## Overview

The Noctua SDK provides a unified set of services for mobile game development:

| Module | Description |
|--------|-------------|
| `Noctua.Auth` | Authentication — guest, social login, account switching |
| `Noctua.IAP` | In-app purchases — products, purchase flow, pending deliverables, redeems |
| `Noctua.Event` | Analytics — custom events, session tracking, engagement time |
| `Noctua.IAA` | Ad mediation — banner, interstitial, rewarded, app open ads ([docs](docs/IAA.md) · [advanced](docs/IAA-Advanced.md)) |
| `Noctua.Platform` | Platform features — announcements, customer service, rewards, social media |
| `Noctua.Platform.Locale` | Locale — language, country, currency |

## Installation

Add to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.noctuagames.sdk": "https://github.com/noctuagames/noctua-sdk-unity-upm.git#0.101.0"
  }
}
```

## Quick Start

```csharp
using com.noctuagames.sdk;
using Cysharp.Threading.Tasks;

// Initialize
await Noctua.InitAsync();

// Authenticate
var account = await Noctua.Auth.AuthenticateAsync();

// Track event
Noctua.Event.TrackCustomEvent("level_complete", new Dictionary<string, object> {
    { "level", 5 }
});

// Purchase
var result = await Noctua.IAP.PurchaseItemAsync(productId);
```

## Ad Network Setup (Integration Manager)

Open **Noctua > Noctua Integration Manager** in the Unity menu bar.

### Recommended Setup

One-click install of a pre-validated combination that runs AppLovin MAX and AdMob demand on both Android and iOS without version conflicts:

| Package | Version | Notes |
|---------|---------|-------|
| AppLovin MAX SDK | 8.6.2 | Wraps MAX SDK 13.6.2 |
| AdMob / GMA SDK | 11.0.0 | GMA iOS ~> 13.0.0, Android 25.0.0 |
| AppLovin → Google Android | 25010000.0.0 | Routes AdMob demand (Android) |
| AppLovin → Google iOS | 13020000.0.0 | Routes AdMob demand (iOS) |
| AppLovin → Ad Manager Android | 25010000.0.0 | Routes Ad Manager demand (Android) |
| AppLovin → Ad Manager iOS | 13020000.0.0 | Routes Ad Manager demand (iOS) |

**Why no conflict:** `com.google.ads.mobile` 11.0.0 pins GMA iOS `~> 13.0.0` (allows any 13.x). AppLovin's Google adapter requires GMA iOS 13.2.0 — fully satisfied. On Android, GMA 25.1.0 is resolved via Gradle from the 25.x-compatible adapter, backward-compatible with AdMob SDK's declared 25.0.0 baseline.

### CocoaPods Conflict Fixer (iOS only)

**Noctua > iOS > Fix CocoaPods Conflicts** — patches `GoogleMobileAdsDependencies.xml` and removes the legacy `~/.cocoapods/repos/cocoapods` repo that causes version-conflict errors. Menu items are greyed out when build target is not iOS. Auto-detects 7+ cross-catalog conflicts (AppLovin, BidMachine, Vungle, Mintegral, UnityAds, Fyber, Verve).

> **Maio is mutually exclusive.** Install Maio from **either** AppLovin MAX **or** AdMob — never both. `com.applovin.mediation.adapters.maio.ios 2.1.6.0` pins `MaioSDK-v2 = 2.1.6`; `com.google.ads.mobile.mediation.maio 3.1.6` pins `MaioSDK-v2 = 2.2.1`. The two adapters cannot coexist at any version. Fix CocoaPods Conflicts reports `⚠ MUTUALLY EXCLUSIVE — remove one` and intentionally skips auto-patch — uninstall one in Integration Manager. AppLovin MAX is the primary mediator in the Recommended Setup, so installing Maio only via AppLovin MAX is the safest choice (it continues to serve Maio demand without the AdMob adapter). See [docs.noctua.gg troubleshooting guide](https://docs.noctua.gg/troubleshoot/cocoapods-maio-gma-13-conflict) for details.

### Ad Network Adapters

- **AppLovin MAX — Ad Network Adapters**: 22 adapters from `unity.packages.applovin.com`
- **AdMob — Mediation Adapters**: 17 adapters from `package.openupm.com`

Version column is color-coded: green = at recommended, amber = update available. Click **→ Stable** to update to the verified version. Install/Update/Remove triggers `Client.Resolve()` automatically — no manual editor refresh needed.

## Session & Engagement Tracking

The SDK tracks two parallel engagement signals:

### `noctua_user_engagement` (Unity-driven)
Fired at every lifecycle transition using a `System.Diagnostics.Stopwatch`:

| lifecycle | Trigger |
|-----------|---------|
| `start` | App opens / `InitAsync()` |
| `foreground` | App resumes from background (every 60s heartbeat) |
| `pause` | App enters background |
| `end` | Session ends / app quits |

```json
{
  "event_name": "noctua_user_engagement",
  "engagement_time_msec": 58649,
  "lifecycle": "pause"
}
```

### `native_user_engagement` (OS-driven, real device only)
Parallel tracker driven by native platform callbacks (iOS `UIApplication`, Android `Activity`). Fires immediately at OS level — ~5ms ahead of Unity's `OnApplicationPause`. Used for cross-validation of engagement accuracy. Server-side filtered (cross-validation only).

### `noctua_user_engagement_per_session`
Cumulative foreground time for the entire session. Fires on session timeout resume or graceful app quit.

### `feature_engagement` (feature-level engagement)
Tracks time spent on individual game features. Call `Noctua.Event.TrackCustomEvent("feature_engagement_start", ...)` and `feature_engagement_end` to bracket feature interactions; the SDK computes `time_msec` and attaches a `visit_id` for grouping related start/end pairs.

### Session Timeout
Default: **15 minutes** (`sessionTimeoutMs: 900000` in `NoctuaConfig`). On timeout, the old session is closed without `session_end` (force-kill limitation) and a new session begins.

## Dependencies

- **UniTask** (`Cysharp.Threading.Tasks`) — async/await for Unity
- **Newtonsoft.Json** — JSON serialization
- **Google External Dependency Manager** — native SDK management
- **URP 14+** — Universal Render Pipeline (optional)

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for the full release history.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for the release and publish process.
