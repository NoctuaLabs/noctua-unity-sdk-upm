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
| `Noctua.LiveOpsCampaign` | Live ops — admin-authored popups (offers, events, daily missions), mission progress, one-time offers ([guide](#live-ops-campaigns--missions)) |

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
| AppLovin MAX SDK | 8.6.1 | Wraps MAX SDK 13.6.1 (pinned to avoid GMA conflict with AdMob AppLovin adapter) |
| AdMob / GMA SDK | 11.0.0 | GMA iOS ~> 13.0.0, Android 25.0.0 |
| AppLovin → Google Android | 25010000.0.0 | Routes AdMob demand (Android) |
| AppLovin → Google iOS | 13020000.0.0 | Routes AdMob demand (iOS) |
| AppLovin → Ad Manager Android | 25010000.0.0 | Routes Ad Manager demand (Android) |
| AppLovin → Ad Manager iOS | 13020000.0.0 | Routes Ad Manager demand (iOS) |

**Why no conflict:** `com.google.ads.mobile` 11.0.0 pins GMA iOS `~> 13.0.0` (allows any 13.x). AppLovin's Google adapter requires GMA iOS 13.2.0 — fully satisfied. On Android, GMA 25.1.0 is resolved via Gradle from the 25.x-compatible adapter, backward-compatible with AdMob SDK's declared 25.0.0 baseline.

### CocoaPods Conflict Fixer (iOS only)

**Noctua > iOS > Fix CocoaPods Conflicts** — patches `GoogleMobileAdsDependencies.xml` and removes the legacy `~/.cocoapods/repos/cocoapods` repo that causes version-conflict errors. Menu items are greyed out when build target is not iOS. Auto-detects 7+ cross-catalog conflicts (AppLovin, BidMachine, Vungle, Mintegral, UnityAds, Fyber, Verve).

> **Maio is mutually exclusive.** Install Maio from **either** AppLovin MAX **or** AdMob — never both. `com.applovin.mediation.adapters.maio.ios 2.1.6.0` pins `MaioSDK-v2 = 2.1.6`; `com.google.ads.mobile.mediation.maio 3.1.6` pins `MaioSDK-v2 = 2.2.1`. The two adapters cannot coexist at any version. Fix CocoaPods Conflicts reports `⚠ MUTUALLY EXCLUSIVE — remove one` and intentionally skips auto-patch — uninstall one in Integration Manager. AppLovin MAX is the primary mediator in the Recommended Setup, so installing Maio only via AppLovin MAX is the safest choice (it continues to serve Maio demand without the AdMob adapter). See [docs.noctua.gg troubleshooting guide](https://docs.noctua.gg/troubleshoot/cocoapods-maio-gma-13-conflict) for details.

### iOS Dependency Manager: CocoaPods or Swift Package Manager

**Noctua > iOS > Dependency Manager** switches how the native Noctua iOS SDK is integrated:

- **CocoaPods** (default) — EDM4U installs the `NoctuaSDK/*` pods.
- **Swift Package Manager** — links [`noctua-native-sdk-ios`](https://github.com/NoctuaLabs/noctua-native-sdk-ios) into the Xcode project. Firebase, Adjust, Facebook and NoctuaInternalSDK come from SPM; ad SDKs and adapters stay on CocoaPods.

The choice is saved in `ProjectSettings/NoctuaIosDependencySettings.json` — commit it so CI builds the same way. The `NOCTUA_IOS_DEPENDENCY_MANAGER` env var (`cocoapods` | `spm`) overrides it. Use a clean (Replace) build folder when switching. In SPM mode the build fails if `Podfile.lock` still installs a pod the package already provides (Firebase, Adjust, Facebook, GoogleUtilities, …), since linking both duplicates symbols.

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

## Live Ops

Everything live ops is under one entry, `Noctua.LiveOps`, one property per feature. It is never null and works before `Noctua.InitAsync`.

| Feature | Entry | Status |
|---|---|---|
| Campaign — popups, offers, daily missions | `Noctua.LiveOps.Campaign` | Available |
| Cross Promotion | — | Coming soon |

### Campaign

Popups, offers and daily missions are authored in **noctua-admin → Live Ops** and delivered by `Noctua.InitAsync`. The SDK shows them, runs their buttons, claims missions and chests, keeps progress on the server, and hands the game every reward it owes. The game only supplies what the SDK cannot know: where a deeplink goes and how to add items to its inventory.

#### 1. Setup — one call, before init

```csharp
using com.noctuagames.sdk;
using com.noctuagames.sdk.LiveOpsCampaign;
using Cysharp.Threading.Tasks;

Noctua.LiveOps.Campaign.Configure(new CampaignHandlers
{
    // A popup button's deeplink, e.g. "shop/gems" or "event/summer".
    Deeplink = route => GameRouter.Open(route),

    // No Purchase handler needed: a popup's Buy is bought by the SDK, with the role and server
    // from your last Noctua.Auth.UpdatePlayerAccountAsync (call it when the player picks a role).

    // Claimed missions and chests. Grant and save the items, then return.
    Rewards = async rewards =>
    {
        foreach (var r in rewards)                       // r.Source: Mission / Milestone
        {
            if (Inventory.HasGranted(r.ClaimId)) continue;
            foreach (var item in r.Items) Inventory.Add(item.Id, item.Quantity);
            Inventory.MarkGranted(r.ClaimId);
        }
        await Inventory.SaveAsync();
    },
});

await Noctua.InitAsync();
```

Every field is optional, and calling `Configure` again replaces only the fields you pass. Keep the handlers alive for the whole session (static or a `DontDestroyOnLoad` object).

Before init, async calls wait until live ops is ready, `ShowPopup` shows once it is, and reads (`GetActiveCampaigns`, `Progress`) are empty.

#### 2. Showing popups

Popups marked **auto show** appear by themselves after init. To show one yourself:

```csharp
Noctua.LiveOps.Campaign.ShowPopup("summer_sale");      // by campaign id
Noctua.LiveOps.Campaign.ShowPopup();                   // the highest-priority eligible one
var offers = Noctua.LiveOps.Campaign.GetActiveCampaigns(CampaignItem.EngagementPurchase);
```

A campaign that is not eligible is skipped, never thrown. The reason is in the sandbox Inspector's Campaigns tab, e.g. `schema v3 > supported`, `sdk version below min`, `country mismatch`. Events: `OnCampaignShown`, `OnCampaignClicked`, `OnCampaignDismissed`, `OnProgressChanged`, `OnCampaignUnavailable`.

#### 3. Offers and purchases

| Admin setting | Game code |
|---|---|
| Buy button with a SKU | None: the SDK buys it (role and server from `Noctua.Auth.UpdatePlayerAccountAsync`) and the game delivers it in `OnPurchaseDone`. Bought non-consumables and subscriptions are hidden or shown as "Purchased" automatically. |
| **One purchase per player** | None. After a completed purchase from the popup the SDK records it on the server; the campaign is not shown again and its Buy buttons read "Purchased", on every device. |
| **Different SKU on iOS** (the product has a different id per store) | None. The admin sends both ids (`{"sku_android":"gem_500","sku_ios":"com.game.gem500"}`) and the SDK buys the one for the store it runs on. A button with no SKU for this store is hidden. |
| One-time consumable offer | None: turn on **One purchase per player**. The store only remembers non-consumables and subscriptions. If the game also sells the offer outside the popup, call `Noctua.LiveOps.Campaign.Module.RecordCampaignPurchaseAsync(campaignId)` after that sale. |

**Every purchase is delivered from `Noctua.IAP.OnPurchaseDone`, with the items set on its SKU.** In SKU Management each SKU can list items as key → quantity (`coin = 100`, `skin = 1`; whole numbers). They come with the product list, so read them for the purchased SKU — the list is cached, so this makes no request when your shop already loaded it, and offline it uses the copy on the device:

```csharp
Noctua.IAP.OnPurchaseDone += async order =>
{
    Product product = null;
    try { product = (await Noctua.IAP.GetProductListAsync(productId: order.ProductId)).FirstOrDefault(); }
    catch (Exception) { /* not logged in, or offline with nothing cached */ }

    if (product == null) { KeepForRetry(order); return; }  // items unknown: never guess
    if (product.Items.Count > 0)
    {
        foreach (var item in product.Items) Grant(item.Key, item.Value);  // deliver one by one
        return;
    }
    GrantBySku(order.ProductId);                           // no items: your own SKU mapping
};
```

`Items` is never null. `productId` only narrows the result on the device; without it you get the whole list as before. Ignore an order id you already granted. A purchase limit counts its SKU, so the admin refuses two limited cards on one SKU.

Every order started from a popup carries `campaign_id` in its `extra`; your own `PurchaseRequest.Extra` keys always win.

#### 4. Daily missions

The admin defines the missions (key, target, reward id and quantity, art); the game reports progress and grants rewards. A player must be logged in (guest is fine).

```csharp
// Report the player's absolute value for a mission key (the key from the admin, e.g. "use_stm").
await Noctua.LiveOps.Campaign.SetMissionProgressAsync("use_stm", totalStaminaUsed);
```

- **Progress** is stored on the server, survives reinstalls, and only ever goes up. While offline it is saved on the device and sent later.
- **Claim** and **Claim All** buttons (a mission or chest with no claim deeplink in the admin) are handled by the SDK: it claims on the server, once per player, and calls `Rewards` with a `LiveOpsReward` per claim — `Source`, `Key`, the admin's reward `Items` and `ClaimId`.
- **Milestone chests:** give missions **points** in the admin. The SDK adds up the points of claimed missions (it fills `{{points}}` for the popup), and a chest with no claim deeplink becomes claimable once the points reach it; Claim All claims the missions first, then the chests they unlock, in one tap.
- To claim from your own UI: `ClaimMissionAsync(campaignId, missionKey)`, `ClaimMilestoneAsync(campaignId, points)` or `ClaimAllMissionsAsync(campaignId)` — rewards still arrive through `Rewards`.

#### 5. Rewards, from every source

- **One handler** — `Rewards` — for missions and chests (purchases go to `OnPurchaseDone`, above). Each reward has `Items` (`Id`, `Quantity`), a `Source`, the `CampaignId` and a `ClaimId`.
- **Never lost.** A reward is saved on the device, for the player who earned it, before the handler runs, and counts as delivered once the handler finishes without throwing. If the app dies first or the handler throws, it is delivered again after the next init or login.
- **Exactly once on your side.** Redelivery means the handler can see a reward twice (granted, then the app died before it returned): ignore a `ClaimId` you already granted. Granting on your own server? Send `ClaimId` as the idempotency key.
- **Items are granted whole.** An item list with any invalid item (bad id, quantity not a whole number ≥ 1) is not delivered at all; the admin validates the same rules.
- **Limit:** the client delivers the items, as with any client-granted reward. A game whose inventory lives on its server should treat the items as a request; verifying them server-to-server needs Noctua backend support (the order already carries `campaign_id` for it).
- Read progress any time with `GetProgressAsync()` / `Progress`; listen with `OnProgressChanged`.

#### 6. Audience: SDK version

An audience can require a Noctua SDK version range (admin → Audience → SDK version). There is nothing to code: popups check the running SDK's version on the device, and push notifications are filtered on the server. Campaigns using it — and one-time offers and built-in Claim buttons — need this SDK version or newer; older SDKs skip them.

#### Upgrading: renamed and removed API

`Noctua.IAA.X()`, `Noctua.App.X()` and the other `Noctua.*` calls are unchanged. Only code that names these types or members needs editing:

| Before | Now |
|---|---|
| `MediationManager` | `NoctuaIAAService` |
| `NoctuaAppManager` | `NoctuaApp` |
| `ExperimentManager` | `NoctuaExperiment` |
| `AppOpenAdManager`, `Noctua.IAA.AppOpenManager` | `AppOpenAds`, `Noctua.IAA.AppOpen` |
| `AdRevenueTrackingManager` | `AdRevenueReporter` |
| `AdFrequencyManager` | `AdFrequencyCap` |
| `CpmFloorManager` | `CpmFloor` |
| `UserSegmentManager` | `UserSegments` |
| `AdExperimentManager` | `AdExperimentAssignments` |
| `HybridAdOrchestrator` | `HybridAdRouting` |
| `AdmobManager` / `AppLovinManager` | `AdmobNetwork` / `AppLovinNetwork` |
| `AdmobAdPreloadManager` (`.Instance`) | `AdmobAdPreloader` (created by the SDK) |
| `AdWatchMilestoneTracker.Default` | removed: the SDK owns the instance |
| `Noctua.LiveOpsCampaign` | `Noctua.LiveOps.Campaign` |
| `RegisterDeeplinkHandler(...)` | `Configure(new CampaignHandlers { Deeplink = ... })` |
| `CampaignHandlers.Purchase`, `OnCampaignPurchaseRequested` | removed: the SDK buys a popup's SKU itself |
| `CampaignHandlers.ProductResolver`, `GetProductBundleForSku`, `tier` / array / `sku` bundles | removed: a per-store Buy button names its SKUs as `{"sku_android", "sku_ios"}` and needs no game code |

#### Checklist

- `Noctua.LiveOps.Campaign.Configure(...)` before `Noctua.InitAsync()`.
- The player is logged in before any missions or one-time-offer feature (guest is fine).
- Purchases go through `Noctua.IAP.PurchaseItemAsync` — that is how the SDK sees a popup purchase complete.
- Grant live ops rewards only in `Rewards`, finish it only once they are saved, and skip a `ClaimId` already granted.
- Something not showing? Check the Inspector's Campaigns tab for the reason.

## Dependencies

- **UniTask** (`Cysharp.Threading.Tasks`) — async/await for Unity
- **Newtonsoft.Json** — JSON serialization
- **Google External Dependency Manager** — native SDK management
- **URP 14+** — Universal Render Pipeline (optional)

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for the full release history.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for the release and publish process.
