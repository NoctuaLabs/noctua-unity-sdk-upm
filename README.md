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

Popups, offers and daily missions are authored in **noctua-admin → Live Ops** and delivered by `Noctua.InitAsync`. The SDK shows them, runs their buttons, claims missions and keeps their progress on the server. The game only supplies what the SDK cannot know: where a deeplink goes, how to buy a product, and how to grant a reward.

#### 1. Setup — one call, before init

```csharp
using com.noctuagames.sdk;
using com.noctuagames.sdk.LiveOpsCampaign;
using Cysharp.Threading.Tasks;

Noctua.LiveOps.Campaign.Configure(new CampaignHandlers
{
    // A popup button's deeplink, e.g. "shop/gems" or "event/summer".
    Deeplink = route => GameRouter.Open(route),

    // A popup's Buy button. The campaign is added to the order for you.
    Purchase = async (productId, campaign) =>
        await Noctua.IAP.PurchaseItemAsync(new PurchaseRequest
        {
            ProductId = productId, Price = price, Currency = currency,
            RoleId = roleId, ServerId = serverId, IngameItemId = itemId,
        }),

    // Missions the SDK claimed (Claim / Claim All buttons): grant their rewards.
    MissionRewards = async rewards =>
    {
        foreach (var r in rewards) await Inventory.AddAsync(r.RewardId, int.Parse(r.Quantity), r.ClaimId);
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
| Buy button with a SKU | Only the `Purchase` handler above. Bought non-consumables and subscriptions are hidden or shown as "Purchased" automatically. |
| **One purchase per player** | None. After a completed purchase from the popup the SDK records it on the server; the campaign is not shown again and its Buy buttons read "Purchased", on every device. |
| **Dynamic product** (a JSON bundle instead of a SKU) | None when the bundle names its SKU: `{"sku_android":"gem_500","sku_ios":"com.game.gem500"}`, or just `{"sku":"gem_500"}`. To map anything else (e.g. a price `tier`) set `ProductResolver`. |
| Consumable offer with a limit | Optional: `OwnershipProvider = sku => myStore.OwnsAsync(sku)` — the store only knows non-consumables. |

```csharp
// Only for bundles that do not name a SKU. Returning null hides that button.
ProductResolver = (campaign, productKey, bundle) =>
    myCatalogue.SkuForTier((string)bundle["tier"]) ?? NoctuaLiveOpsCampaign.DefaultBundleSku(bundle),
```

Inside the `Purchase` handler, `Noctua.LiveOps.Campaign.GetProductBundleForSku(campaign, productId)` returns the bundle a dynamic button was resolved from. Every order started from a popup carries `campaign_id` (and `campaign_bundle_key` / `campaign_bundle`) in its `extra`; your own `PurchaseRequest.Extra` keys always win.

#### 4. Daily missions

The admin defines the missions (key, target, reward id and quantity, art); the game reports progress and grants rewards. A player must be logged in (guest is fine).

```csharp
// Report the player's absolute value for a mission key (the key from the admin, e.g. "use_stm").
await Noctua.LiveOps.Campaign.SetMissionProgressAsync("use_stm", totalStaminaUsed);
```

- **Progress** is stored on the server, survives reinstalls, and only ever goes up. While offline it is saved on the device and sent later.
- **Claim** and **Claim All** buttons are handled by the SDK: it claims on the server (once per player) and calls `MissionRewards` with a `MissionReward` per mission — `MissionKey`, the admin's `RewardId` and `Quantity`, and `ClaimId`.
- **Rewards are never lost.** A reward counts as delivered once `MissionRewards` finishes without throwing. If the app dies first or the handler throws, the SDK delivers it again after the next init or login. Granting on your server? Ignore a `ClaimId` you have already granted.
- To claim from your own UI: `Noctua.LiveOps.Campaign.ClaimMissionAsync(campaignId, missionKey)` or `ClaimAllMissionsAsync(campaignId)` — rewards still arrive through `MissionRewards`.
- Read progress any time with `GetProgressAsync()` / `Progress`; listen with `OnProgressChanged`.

#### 5. Audience: SDK version

An audience can require a Noctua SDK version range (admin → Audience → SDK version). There is nothing to code: popups check the running SDK's version on the device, and push notifications are filtered on the server. Campaigns using it — and one-time offers, dynamic products and built-in Claim buttons — need this SDK version or newer; older SDKs skip them.

#### Upgrading from `Noctua.LiveOpsCampaign`

`Noctua.LiveOpsCampaign` (null until init) still works but is obsolete. Move to `Noctua.LiveOps.Campaign`: replace `RegisterDeeplinkHandler`, `SetPurchaseHandler`, `OnCampaignPurchaseRequested`, `SetProductResolver` and `SetOwnershipProvider` with one `Configure(...)`. Claim deeplinks you route yourself (`ClaimMissionAsync` on the old object, grant when it returns) keep working; switching the admin's Claim buttons to the built-in claim moves granting into `MissionRewards`.

#### Checklist

- `Noctua.LiveOps.Campaign.Configure(...)` before `Noctua.InitAsync()`.
- The player is logged in before any missions or one-time-offer feature (guest is fine).
- Purchases go through `Noctua.IAP.PurchaseItemAsync` — that is how the SDK sees a popup purchase complete.
- Grant mission rewards only in `MissionRewards`, and finish it only once they are granted.
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
