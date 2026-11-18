# Live ops campaign: payload and public API

The contract between noctua-admin (authoring), the noctua-data DAG (rendering), labs-sdk-service
(serving) and this SDK (showing). The JSON fixtures in `Tests/Runtime/Campaign/Fixtures/` and
noctua-admin's `golden.json` are the executable form of this document: change the spec first, then
the fixtures, then the code.

## 1. Rules for the payload

1. **One shape per field.** A field is always the same JSON type. No "object or array", no free
   keys the reader has to guess at.
2. **Unknown fields are ignored**, so a newer admin can add fields without breaking an older SDK.
3. **A campaign the SDK cannot fully read is skipped whole** (`schema_version` above what the SDK
   supports, or a node that fails validation). It is never shown partly.

## 2. Campaign

| Field | Type | Meaning |
|---|---|---|
| `id` | string | Unique campaign id. |
| `schema_version` | int | Contract version the campaign was built for. |
| `engagement_type` | string | `purchase` or `event`. |
| `priority` | int | Higher shows first. |
| `targeting` | object | `tags`, `countries`, `min/max_app_version`, `min/max_sdk_version`. |
| `frequency` | object | `max_per_day`, `cooldown_hours`, `once_ever`. |
| `schedule` | object | `start`, `end` (UTC). |
| `view` | node | The popup tree (section 3). |
| `data` | object of string → string | Values for `{{token}}`s in the view. |
| `player_data` | object of string → string | Defaults for per-player tokens. |
| `purchase_once` | bool | The campaign sells once per player. |
| `auto_show` | bool | Shown at game start. |

## 3. Node and action

A node is `{ type, props, style, responsive, children, action, visible_if }`.

An **action** is `{ type, id, ... }`:

| `type` | Extra fields | What the SDK does |
|---|---|---|
| `dismiss` | none | Closes the popup. |
| `deeplink` | `deeplink` (string route) | Calls the game's `Deeplink` handler. |
| `purchase` | `product_id` (string SKU) | Buys the SKU itself (section 4). |
| `claim` | `mission` or `milestone` | Claims on the server, then calls `Rewards`. |
| `claim_all` | none | Claims every claimable mission and chest, then calls `Rewards`. |

## 4. Purchase

A purchase action names exactly one SKU in `product_id`. The same SKU id is bought on Google Play
and the App Store, so ops create the product with one id on both stores. There is no per-store
mapping and no bundle: `product_key` is not part of the contract, and a purchase action without
`product_id` fails validation, so its campaign is skipped.

The SDK starts the purchase itself: price and currency from the product list, role and server from
the last `Noctua.Auth.UpdatePlayerAccountAsync`. The order's `extra` carries `campaign_id`, which the
backend stores as `orders.live_ops_campaign_id`. Nothing else about the campaign is sent.

**What the player gets is not in the campaign.** Items belong to the SKU (`products.items`, an
object of item key → whole-number quantity, e.g. `{"coin": 100}`), set in SKU Management and sent
with the product list as `Product.Items`.

## 5. Public API (game side)

A normal campaign needs no game code: popups show, Buy purchases, and the order arrives in
`Noctua.IAP.OnPurchaseDone` like any other purchase.

```csharp
// Delivery, for every purchase (popup or shop).
Noctua.IAP.OnPurchaseDone += async order =>
{
    var product = (await Noctua.IAP.GetProductListAsync(productId: order.ProductId)).FirstOrDefault();
    if (product == null) return;                       // unknown right now: retry later
    if (product.Items.Count > 0) foreach (var item in product.Items) Grant(item.Key, item.Value);
    else GrantBySku(order.ProductId);
};

// Role and server for SDK-started purchases.
await Noctua.Auth.UpdatePlayerAccountAsync(new PlayerAccountData { IngameServerId = "S1", IngameRoleId = "R1" });
```

Optional, through `Noctua.LiveOps.Campaign`:

| Member | Use |
|---|---|
| `Configure(new CampaignHandlers { Deeplink, OwnershipProvider, Rewards })` | Route deeplinks; report ownership of one-time consumables; grant mission and chest rewards. |
| `ShowPopup(id)`, `UpdatePopupData(data)`, `ClosePopup()`, `GetActiveCampaigns(type)` | Show campaigns from your own UI. |
| `SetMissionProgressAsync`, `ClaimMissionAsync`, `ClaimMilestoneAsync`, `ClaimAllMissionsAsync`, `GetProgressAsync` | Daily missions. |
| `OnCampaignShown`, `OnCampaignClicked`, `OnCampaignDismissed`, `OnProgressChanged`, `OnCampaignUnavailable` | Events. |
