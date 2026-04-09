# Noctua SDK for Unity

**Version:** 0.91.0 | **Unity:** 2021.3+ | **Platforms:** Android, iOS

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
    "com.noctuagames.sdk": "https://github.com/noctuagames/noctua-sdk-unity-upm.git#0.91.0"
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
