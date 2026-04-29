extern "C" {

void noctuaInitialize(bool verifyPurchasesOnServer, bool useStoreKit1);
void noctuaTrackAdRevenue(const char* source, double revenue, const char* currency, const char* extraPayloadJson);
void noctuaTrackPurchase(const char* orderId, double amount, const char* currency, const char* extraPayloadJson);
void noctuaTrackCustomEvent(const char* eventName, const char* payloadJson);
void noctuaTrackCustomEventWithRevenue(const char* eventName, double revenue, const char* currency, const char* payloadJson);
void noctuaOnOnline();
void noctuaOnOffline();
void noctuaSaveEvents(const char* eventsJson);
void noctuaDeleteEvents();

typedef void (*PurchaseCompletionDelegate)(bool success, const char* message);
void noctuaPurchaseItem(const char* productId, PurchaseCompletionDelegate callback);
typedef void (*ActiveCurrencyCompletionDelegate)(bool success, const char* currency);
void noctuaGetActiveCurrency(const char* productId, ActiveCurrencyCompletionDelegate callback);
typedef void (*ProductPurchasedCompletionDelegate)(bool hasPurchased);
void noctuaGetProductPurchasedById(const char* productId, ProductPurchasedCompletionDelegate callback);
typedef void (*ReceiptCompletionDelegate)(const char* receipt);
void noctuaGetReceiptProductPurchasedStoreKit1(const char* productId, ReceiptCompletionDelegate callback);
typedef void (*ProductPurchaseStatusDetailDelegate)(const char* statusJson);
void noctuaGetProductPurchaseStatusDetail(const char* productId, ProductPurchaseStatusDetailDelegate callback);
typedef void (*GetFirebaseIDCallbackDelegate)(const char* firebaseId);
void noctuaGetFirebaseInstallationID(GetFirebaseIDCallbackDelegate callback);
typedef void (*GetFirebaseSessionIDCallbackDelegate)(const char* sessionId);
void noctuaGetFirebaseAnalyticsSessionID(GetFirebaseSessionIDCallbackDelegate callback);
typedef void (*GetFirebaseMessagingTokenCallbackDelegate)(const char* token);
void noctuaGetFirebaseMessagingToken(GetFirebaseMessagingTokenCallbackDelegate callback);

// Push-notification callback registration. Unity passes function pointers; the
// CustomAppController calls them when the matching ObjC delegate methods fire.
// Payloads arrive as UTF-8 JSON strings (NSDictionary userInfo serialised by
// NSJSONSerialization).
typedef void (*NoctuaPushStringCallback)(const char* json);
void noctuaSetRemoteNotificationCallback(NoctuaPushStringCallback callback);
void noctuaSetNotificationTappedCallback(NoctuaPushStringCallback callback);
void noctuaSetFcmTokenRefreshCallback(NoctuaPushStringCallback callback);
typedef void (*GetFirebaseRemoteConfigStringCallbackDelegate)(const char* configString);
void noctuaGetFirebaseRemoteConfigString(const char* key, GetFirebaseRemoteConfigStringCallbackDelegate callback);
typedef void (*GetFirebaseRemoteConfigBooleanCallbackDelegate)(const bool configBool);
void noctuaGetFirebaseRemoteConfigBoolean(const char* key, GetFirebaseRemoteConfigBooleanCallbackDelegate callback);
typedef void (*GetFirebaseRemoteConfigDoubleCallbackDelegate)(const double configDouble);
void noctuaGetFirebaseRemoteConfigDouble(const char* key, GetFirebaseRemoteConfigDoubleCallbackDelegate callback);
typedef void (*GetFirebaseRemoteConfigLongCallbackDelegate)(long long configLong);
void noctuaGetFirebaseRemoteConfigLong(const char* key, GetFirebaseRemoteConfigLongCallbackDelegate callback);
typedef void (*AdjustAttributionCallbackDelegate)(const char* jsonString);
void noctuaGetAdjustAttribution(AdjustAttributionCallbackDelegate callback);
typedef void (*GetEventsCallbackDelegate)(const char* eventsJson);
void noctuaGetEvents(GetEventsCallbackDelegate callback);

// Account Management
typedef void (*StringDelegate)(const char* result);
void noctuaPutAccount(int64_t gameId, int64_t playerId, const char* rawData);
void noctuaGetAllAccounts(StringDelegate callback);
void noctuaGetSingleAccount(int64_t gameId, int64_t playerId, StringDelegate callback);
void noctuaDeleteAccount(int64_t gameId, int64_t playerId);

// Additional StoreKit Functions
void noctuaRegisterProduct(const char* productId, int consumableType);
typedef void (*BoolCallbackDelegate)(bool success);
void noctuaCompletePurchaseProcessing(const char* purchaseToken, int consumableType, bool verified, BoolCallbackDelegate callback);
void noctuaRestorePurchases(void);
void noctuaDisposeStoreKit(void);
bool noctuaIsStoreKitReady(void);

// Per-Row Event Storage (Unlimited)
void noctuaInsertEvent(const char* eventJson);
typedef void (*GetEventsBatchCallbackDelegate)(const char* eventsJson);
void noctuaGetEventsBatch(int limit, int offset, GetEventsBatchCallbackDelegate callback);
typedef void (*DeleteEventsByIdsCallbackDelegate)(int deletedCount);
void noctuaDeleteEventsByIds(const char* idsJson, DeleteEventsByIdsCallbackDelegate callback);
typedef void (*GetEventCountCallbackDelegate)(int count);
void noctuaGetEventCount(GetEventCountCallbackDelegate callback);

// In-App Review
void noctuaRequestInAppReview(void);

// Native Lifecycle Callback
typedef void (*NativeLifecycleCallbackDelegate)(const char* lifecycleEvent);
void noctuaRegisterLifecycleCallback(NativeLifecycleCallbackDelegate callback);

// ===================================================================
// Inspector — sandbox-only debugging surface. All exports below are
// no-ops in production builds (the native bus self-gates on
// `sandboxEnabled` from noctuagg.json) and the Unity SDK only invokes
// them when `Noctua.IsSandbox()` returns true.
// ===================================================================

// Tracker emission bus (Inspector "Trackers" tab) — already shipped.
typedef void (*NoctuaTrackerEmissionCallback)(const char* provider,
                                              const char* eventName,
                                              const char* payloadJson,
                                              const char* extraParamsJson,
                                              int phase);
void noctuaSetTrackerEmissionCallback(NoctuaTrackerEmissionCallback callback);
void noctuaInspectorSetEnabled(int enabled);

// Verbose log stream (Inspector "Logs" tab). `level` follows logcat
// priority numbering (Verbose=2..Error=6); `timestampMillisUtc` is the
// log entry timestamp. Pass NULL callback to unregister.
typedef void (*NoctuaLogStreamCallback)(int level,
                                        const char* source,
                                        const char* tag,
                                        const char* message,
                                        long long timestampMillisUtc);
void noctuaSetLogStreamCallback(NoctuaLogStreamCallback callback);
void noctuaSetLogStreamEnabled(int enabled);

// Device metrics snapshot (Inspector "Memory" tab). Returns 0 on
// success; out-pointers are populated atomically. Sentinel -1 in
// numeric fields means "platform does not expose this metric".
//   thermal: -1 unknown, 0 nominal, 1 fair, 2 serious, 3 critical
int noctuaSnapshotDeviceMetrics(long long* outPhysFootprint,
                                long long* outAvailable,
                                long long* outSystemTotal,
                                int* outLowMemory,
                                int* outThermal);

}
