@import NoctuaSDK;
#import <UIKit/UIKit.h>

// MARK: - Initialization

void noctuaInitialize(bool verifyPurchasesOnServer, bool useStoreKit1, bool sandboxEnabled) {
    NSError *error = nil;
    // Explicit override (plain BOOL) — wins over noctuagg.json.
    [Noctua initNoctuaWithVerifyPurchasesOnServer:verifyPurchasesOnServer useStoreKit1:useStoreKit1 sandboxEnabled:sandboxEnabled error:&error];
    if (error) {
        NSLog(@"Error initializing Noctua: %@", error);
    }
}

// MARK: - Tracking

void noctuaTrackAdRevenue(const char* source, double revenue, const char* currency, const char* extraPayloadJson) {
    NSLog(@"source: %s, revenue: %f, currency: %s, extraPayload: %s", source, revenue, currency, extraPayloadJson);

    NSString *sourceStr = [NSString stringWithUTF8String:source];
    NSString *currencyStr = [NSString stringWithUTF8String:currency];
    NSString *extraPayloadStr = [NSString stringWithUTF8String:extraPayloadJson];
    NSData *data = [extraPayloadStr dataUsingEncoding:NSUTF8StringEncoding];
    NSDictionary *extraPayload = [NSJSONSerialization JSONObjectWithData:data options:0 error:nil];
    [Noctua trackAdRevenueWithSource:sourceStr revenue:revenue currency:currencyStr extraPayload:extraPayload];
}

void noctuaTrackPurchase(const char* orderId, double amount, const char* currency, const char* extraPayloadJson) {
    NSLog(@"orderId: %s, amount: %f, currency: %s, extraPayload: %s", orderId, amount, currency, extraPayloadJson);

    NSString *orderIdStr = [NSString stringWithUTF8String:orderId];
    NSString *currencyStr = [NSString stringWithUTF8String:currency];
    NSString *extraPayloadStr = [NSString stringWithUTF8String:extraPayloadJson];
    NSData *data = [extraPayloadStr dataUsingEncoding:NSUTF8StringEncoding];
    NSDictionary *extraPayload = [NSJSONSerialization JSONObjectWithData:data options:0 error:nil];
    [Noctua trackPurchaseWithOrderId:orderIdStr amount:amount currency:currencyStr extraPayload:extraPayload];
}

void noctuaTrackCustomEvent(const char* eventName, const char* payloadJson) {
    NSLog(@"eventName: %s, payload: %s", eventName, payloadJson);

    NSString *eventNameStr = [NSString stringWithUTF8String:eventName];
    NSString *payloadStr = [NSString stringWithUTF8String:payloadJson];
    NSData *data = [payloadStr dataUsingEncoding:NSUTF8StringEncoding];
    NSDictionary *payload = [NSJSONSerialization JSONObjectWithData:data options:0 error:nil];
    [Noctua trackCustomEvent:eventNameStr payload:payload];
}

void noctuaTrackCustomEventWithRevenue(const char* eventName, double revenue, const char* currency, const char* payloadJson) {
    NSLog(@"eventName: %s, revenue: %f, currency: %s, payload: %s", eventName, revenue, currency, payloadJson);

    NSString *eventNameStr = [NSString stringWithUTF8String:eventName];
    NSString *currencyStr = [NSString stringWithUTF8String:currency];
    NSString *payloadStr = [NSString stringWithUTF8String:payloadJson];
    NSData *data = [payloadStr dataUsingEncoding:NSUTF8StringEncoding];
    NSDictionary *payload = [NSJSONSerialization JSONObjectWithData:data options:0 error:nil];
    [Noctua trackCustomEventWithRevenue:eventNameStr revenue:revenue currency:currencyStr payload:payload];
}

// MARK: - Locale

// Returns the device's local IANA timezone identifier (e.g. "Asia/Jakarta").
// Unlike Android, iOS/Mono's TimeZoneInfo.Local.Id resolves reliably most of
// the time, but real-device testing has shown it can still fall back to a
// non-IANA "Local" placeholder — so we resolve it natively via Foundation
// here, the same way we work around the equivalent Android/IL2CPP gap.
// Caller (NoctuaLocale) caches the result, so this is safe to allocate on
// every call — it's expected to run at most once per app session.
const char* noctuaGetTimezone(void) {
    NSString *timezoneName = [[NSTimeZone localTimeZone] name];

    if (timezoneName == nil) {
        return NULL;
    }

    return strdup([timezoneName UTF8String]);
}

// MARK: - StoreKit / In-App Purchases

// This bridge keeps NO per-request state. Every StoreKit event is forwarded — with the product id
// it belongs to — to a single C# callback (IosPlugin -> StoreKitRequestRouter), which matches events
// to the calls waiting for them.
//
// The previous design kept one global callback slot per operation and completed whatever happened
// to be waiting when any event arrived. Under fast or overlapping calls that dropped callbacks
// (hanging the caller), answered a status check with another product's status, failed an in-flight
// purchase on an unrelated StoreKit error, and let a replayed old transaction complete a brand-new
// purchase while the new transaction was silently left unfinished.
//
// Threading: Unity may call these functions from any thread. Every StoreKit call is hopped onto
// the main queue (C strings are copied first; they are only valid for the duration of the call),
// and the native SDK delivers its events on main, so events reach C# in native order.

typedef NS_ENUM(int, NoctuaStoreKitEventKind) {
    NoctuaStoreKitEventPurchaseCompleted = 1,
    NoctuaStoreKitEventPurchaseUpdated = 2,
    NoctuaStoreKitEventServerVerificationRequired = 3,
    NoctuaStoreKitEventPurchaseStatus = 4,
    NoctuaStoreKitEventProductDetails = 5,
    NoctuaStoreKitEventError = 6,
};

typedef void (*StoreKitEventDelegate)(int kind, const char* json);
typedef void (*CompletePurchaseProcessingDelegate)(int requestId, bool success);

static StoreKitEventDelegate _storeKitEventCallback = NULL;
static BOOL _storeKitInitialized = NO;

static NSObject *storeKitBridgeLock(void) {
    static NSObject *lock;
    static dispatch_once_t once;
    dispatch_once(&once, ^{
        lock = [NSObject new];
    });
    return lock;
}

static void runStoreKitOnMain(dispatch_block_t block) {
    if ([NSThread isMainThread]) {
        block();
    } else {
        dispatch_async(dispatch_get_main_queue(), block);
    }
}

static NSString *copyCString(const char* value) {
    return value == NULL ? nil : [NSString stringWithUTF8String:value];
}

static void emitStoreKitEvent(NoctuaStoreKitEventKind kind, id payload) {
    StoreKitEventDelegate callback;
    @synchronized (storeKitBridgeLock()) {
        callback = _storeKitEventCallback;
    }
    if (callback == NULL) {
        NSLog(@"[NoctuaStoreKit] Dropping event %d: no event callback registered", kind);
        return;
    }

    NSError *error = nil;
    NSData *data = [NSJSONSerialization dataWithJSONObject:payload options:0 error:&error];
    if (data == nil) {
        NSLog(@"[NoctuaStoreKit] Failed to serialize event %d: %@", kind, error);
        return;
    }

    NSString *json = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
    callback(kind, json.UTF8String);
}

static NSDictionary *transactionPayload(NoctuaPurchaseResult *result, int consumableType) {
    return @{
        @"ProductId": result.productId ?: @"",
        @"Success": @(result.success),
        @"ErrorCode": @(result.errorCode),
        @"PurchaseState": @(result.purchaseState),
        @"PurchaseToken": result.purchaseToken ?: @"",
        @"PurchaseTimeMs": @(result.purchaseTime),
        @"Receipt": result.originalJson ?: @"",
        @"ConsumableType": @(consumableType),
        @"Message": result.message ?: @""
    };
}

static void logTransaction(NSString *event, NoctuaPurchaseResult *result) {
    NSLog(@"[NoctuaStoreKit] %@: productId=%@, success=%d, errorCode=%ld, purchaseState=%ld, token=%@, message=%@",
        event, result.productId, result.success, (long)result.errorCode, (long)result.purchaseState,
        result.purchaseToken, result.message);
}

static NSString *const StoreKitUnavailableMessage = @"StoreKit is not available (IAP disabled in config, or the native SDK is not initialized)";

// Must be called on the main queue. Returns whether StoreKit is available. Only a successful
// initialization is remembered, so a call made before the native SDK is ready retries later
// instead of leaving StoreKit permanently uninitialized.
static BOOL ensureStoreKitInitialized(void) {
    @synchronized (storeKitBridgeLock()) {
        if (_storeKitInitialized) {
            return YES;
        }
    }

    [Noctua initializeStoreKitOnPurchaseCompleted:^(NoctuaPurchaseResult * _Nonnull result) {
        logTransaction(@"onPurchaseCompleted", result);
        emitStoreKitEvent(NoctuaStoreKitEventPurchaseCompleted, transactionPayload(result, 0));
    } onPurchaseUpdated:^(NoctuaPurchaseResult * _Nonnull result) {
        logTransaction(@"onPurchaseUpdated", result);
        emitStoreKitEvent(NoctuaStoreKitEventPurchaseUpdated, transactionPayload(result, 0));
    } onProductDetailsLoaded:^(NSArray<NoctuaProductDetails *> * _Nonnull details) {
        NSLog(@"[NoctuaStoreKit] onProductDetailsLoaded: count=%lu", (unsigned long)details.count);
        NSMutableArray *products = [NSMutableArray arrayWithCapacity:details.count];
        for (NoctuaProductDetails *detail in details) {
            [products addObject:@{
                @"ProductId": detail.productId ?: @"",
                @"Currency": detail.priceCurrencyCode ?: @""
            }];
        }
        emitStoreKitEvent(NoctuaStoreKitEventProductDetails, products);
    } onQueryPurchasesCompleted:^(NSArray<NoctuaPurchaseResult *> * _Nonnull results) {
        NSLog(@"[NoctuaStoreKit] onQueryPurchasesCompleted: count=%lu", (unsigned long)results.count);
    } onRestorePurchasesCompleted:^(NSArray<NoctuaPurchaseResult *> * _Nonnull results) {
        NSLog(@"[NoctuaStoreKit] onRestorePurchasesCompleted: count=%lu", (unsigned long)results.count);
    } onProductPurchaseStatusResult:^(NoctuaProductPurchaseStatus * _Nonnull status) {
        NSLog(@"[NoctuaStoreKit] onProductPurchaseStatusResult: productId=%@, isPurchased=%d, token=%@",
            status.productId, status.isPurchased, status.purchaseToken);
        emitStoreKitEvent(NoctuaStoreKitEventPurchaseStatus, @{
            @"ProductId": status.productId ?: @"",
            @"IsPurchased": @(status.isPurchased),
            @"IsAcknowledged": @(status.isAcknowledged),
            @"IsAutoRenewing": @(status.isAutoRenewing),
            @"PurchaseState": @(status.purchaseState),
            @"PurchaseToken": status.purchaseToken ?: @"",
            @"PurchaseTime": @(status.purchaseTime),
            @"ExpiryTime": @(status.expiryTime),
            @"OrderId": status.orderId ?: @"",
            @"OriginalJson": status.originalJson ?: @"",
            @"TransactionJson": status.transactionJson ?: @""
        });
    } onServerVerificationRequired:^(NoctuaPurchaseResult * _Nonnull result, enum ConsumableType consumableType) {
        logTransaction(@"onServerVerificationRequired", result);
        emitStoreKitEvent(NoctuaStoreKitEventServerVerificationRequired, transactionPayload(result, (int)consumableType));
    } onStoreKitError:^(enum StoreKitErrorCode errorCode, NSString * _Nonnull message) {
        NSLog(@"[NoctuaStoreKit] onStoreKitError: code=%ld, message=%@", (long)errorCode, message);
        emitStoreKitEvent(NoctuaStoreKitEventError, @{
            @"Code": @(errorCode),
            @"Message": message ?: @""
        });
    }];

    BOOL ready = [Noctua isStoreKitReady];
    @synchronized (storeKitBridgeLock()) {
        _storeKitInitialized = ready;
    }
    if (!ready) {
        NSLog(@"[NoctuaStoreKit] %@", StoreKitUnavailableMessage);
    }
    return ready;
}

void noctuaSetStoreKitEventCallback(StoreKitEventDelegate callback) {
    @synchronized (storeKitBridgeLock()) {
        _storeKitEventCallback = callback;
    }
}

void noctuaPurchaseItem(const char* productId) {
    NSString *productIdStr = copyCString(productId);
    if (productIdStr.length == 0) {
        NSLog(@"[NoctuaStoreKit] noctuaPurchaseItem: product ID is empty");
        return;
    }

    runStoreKitOnMain(^{
        if (!ensureStoreKitInitialized()) {
            // Answer the purchase instead of letting it wait for an event that will never come.
            emitStoreKitEvent(NoctuaStoreKitEventPurchaseCompleted, @{
                @"ProductId": productIdStr,
                @"Success": @NO,
                @"ErrorCode": @(StoreKitErrorCodeStoreKitUnavailable),
                @"PurchaseState": @(PurchaseStateUnspecified),
                @"Message": StoreKitUnavailableMessage
            });
            return;
        }
        NSLog(@"[NoctuaStoreKit] purchase: %@", productIdStr);
        [Noctua purchaseWithProductId:productIdStr];
    });
}

void noctuaQueryPurchaseStatus(const char* productId) {
    NSString *productIdStr = copyCString(productId);
    if (productIdStr.length == 0) {
        NSLog(@"[NoctuaStoreKit] noctuaQueryPurchaseStatus: product ID is empty");
        return;
    }

    runStoreKitOnMain(^{
        if (!ensureStoreKitInitialized()) {
            emitStoreKitEvent(NoctuaStoreKitEventPurchaseStatus, @{
                @"ProductId": productIdStr,
                @"IsPurchased": @NO
            });
            return;
        }
        [Noctua getProductPurchaseStatusWithProductId:productIdStr];
    });
}

void noctuaQueryActiveCurrency(const char* productId) {
    NSString *productIdStr = copyCString(productId);
    if (productIdStr.length == 0) {
        NSLog(@"[NoctuaStoreKit] noctuaQueryActiveCurrency: product ID is empty");
        return;
    }

    runStoreKitOnMain(^{
        if (!ensureStoreKitInitialized()) {
            emitStoreKitEvent(NoctuaStoreKitEventError, @{
                @"Code": @(StoreKitErrorCodeStoreKitUnavailable),
                @"Message": [NSString stringWithFormat:@"Failed to query product details: %@", StoreKitUnavailableMessage]
            });
            return;
        }
        [Noctua queryProductDetailsWithProductIds:@[productIdStr] productType:ProductTypeInapp];
    });
}

void noctuaRegisterProduct(const char* productId, int consumableType) {
    NSString *productIdStr = copyCString(productId);
    if (productIdStr.length == 0) {
        NSLog(@"[NoctuaStoreKit] noctuaRegisterProduct: product ID is empty");
        return;
    }

    runStoreKitOnMain(^{
        if (!ensureStoreKitInitialized()) {
            return;
        }
        [Noctua registerProductWithProductId:productIdStr consumableType:(enum ConsumableType)consumableType];
    });
}

void noctuaCompletePurchaseProcessing(const char* purchaseToken, int consumableType, bool verified, int requestId, CompletePurchaseProcessingDelegate callback) {
    NSString *tokenStr = copyCString(purchaseToken);
    if (tokenStr.length == 0) {
        NSLog(@"[NoctuaStoreKit] noctuaCompletePurchaseProcessing: purchase token is empty");
        if (callback != NULL) {
            callback(requestId, false);
        }
        return;
    }

    runStoreKitOnMain(^{
        if (!ensureStoreKitInitialized()) {
            if (callback != NULL) {
                callback(requestId, false);
            }
            return;
        }
        [Noctua completePurchaseProcessingWithPurchaseToken:tokenStr consumableType:(enum ConsumableType)consumableType verified:verified callback:^(BOOL success) {
            if (callback != NULL) {
                callback(requestId, success);
            }
        }];
    });
}

void noctuaRestorePurchases(void) {
    runStoreKitOnMain(^{
        if (!ensureStoreKitInitialized()) {
            return;
        }
        [Noctua restorePurchases];
    });
}

void noctuaDisposeStoreKit(void) {
    runStoreKitOnMain(^{
        [Noctua disposeStoreKit];
        @synchronized (storeKitBridgeLock()) {
            _storeKitInitialized = NO;
        }
    });
}

bool noctuaIsStoreKitReady(void) {
    return [Noctua isStoreKitReady];
}

// MARK: - Accounts

void noctuaPutAccount(int64_t gameId, int64_t playerId, const char* rawData) {
    NSString *rawDataStr = [NSString stringWithUTF8String:rawData];
    [Noctua putAccountWithGameId:gameId playerId:playerId rawData:rawDataStr];
}

typedef void (*StringDelegate)(const char* result);

void noctuaGetAllAccounts(StringDelegate callback) {
    NSArray *accounts = [Noctua getAllAccounts];
    NSError *error;
    NSData *jsonData = [NSJSONSerialization dataWithJSONObject:accounts options:0 error:&error];
    if (!jsonData) {
        NSLog(@"Error serializing accounts to JSON: %@", error);
        callback(NULL);
        return;
    }
    NSString *jsonString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
    callback([jsonString UTF8String]);
}

void noctuaGetSingleAccount(int64_t gameId, int64_t playerId, StringDelegate callback) {
    NSDictionary *account = [Noctua getSingleAccountWithGameId:gameId playerId:playerId];

    if (!account) {
        callback(NULL);
        return;
    }

    NSError *error;
    NSData *jsonData = [NSJSONSerialization dataWithJSONObject:account options:0 error:&error];
    if (!jsonData) {
        NSLog(@"Error serializing account to JSON: %@", error);
        callback(NULL);
        return;
    }
    NSString *jsonString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
    callback([jsonString UTF8String]);
}

void noctuaDeleteAccount(int64_t gameId, int64_t playerId) {
    [Noctua deleteAccountWithGameId:gameId playerId:playerId];
}

// MARK: - Session & Lifecycle

void noctuaOnOnline() {
    [Noctua onOnline];
}

void noctuaOnOffline() {
    [Noctua onOffline];
}

typedef void (*GetFirebaseIDCallbackDelegate)(const char* firebaseId);
void noctuaGetFirebaseInstallationID(GetFirebaseIDCallbackDelegate callback) {
    [Noctua getFirebaseInstallationIDWithCompletion:^(NSString * _Nonnull fid) {
        if (callback != NULL && fid != nil) {
            callback([fid UTF8String]);
        }
    }];
}

typedef void (*GetFirebaseSessionIDCallbackDelegate)(const char* sessionId);
void noctuaGetFirebaseAnalyticsSessionID(GetFirebaseSessionIDCallbackDelegate callback) {
    [Noctua getFirebaseSessionIDWithCompletion:^(NSString * _Nonnull sessionId) {
        if (callback != NULL && sessionId != nil) {
            callback([sessionId UTF8String]);
        }
    }];
}

// Returns the current Firebase Cloud Messaging (FCM) registration token. Empty string when
// the APNs ↔ FCM handshake has not completed yet (call again after the
// messaging:didReceiveRegistrationToken: delegate fires — see CustomAppController.mm).
typedef void (*GetFirebaseMessagingTokenCallbackDelegate)(const char* token);
void noctuaGetFirebaseMessagingToken(GetFirebaseMessagingTokenCallbackDelegate callback) {
    if (callback == NULL) return;

    Class firMessagingCls = NSClassFromString(@"FIRMessaging");
    if (firMessagingCls == nil) {
        NSLog(@"[Noctua] FIRMessaging class not found — Firebase Messaging framework not linked");
        callback("");
        return;
    }

    id messagingInstance = [firMessagingCls performSelector:@selector(messaging)];
    if (messagingInstance == nil) {
        callback("");
        return;
    }

    // Dispatch [FIRMessaging tokenWithCompletion:^(NSString *token, NSError *error)] via runtime
    // so this translation unit does not require a hard link against FirebaseMessaging.
    SEL tokenSel = NSSelectorFromString(@"tokenWithCompletion:");
    if (![messagingInstance respondsToSelector:tokenSel]) {
        callback("");
        return;
    }

    void (^completion)(NSString *, NSError *) = ^(NSString *token, NSError *error) {
        if (error != nil) {
            NSLog(@"[Noctua] FCM token fetch failed: %@", error);
            callback("");
            return;
        }
        callback(token != nil ? [token UTF8String] : "");
    };

    NSMethodSignature *sig = [messagingInstance methodSignatureForSelector:tokenSel];
    NSInvocation *inv = [NSInvocation invocationWithMethodSignature:sig];
    [inv setTarget:messagingInstance];
    [inv setSelector:tokenSel];
    [inv setArgument:&completion atIndex:2];
    [inv invoke];
}

typedef void (*GetFirebaseRemoteConfigStringCallbackDelegate)(const char* configString);
void noctuaGetFirebaseRemoteConfigString(const char* key, GetFirebaseRemoteConfigStringCallbackDelegate callback) {

    if (callback == NULL) {
        return;
    }

    NSString* nsKey = [NSString stringWithUTF8String:key];
    NSString* result = [Noctua getFirebaseRemoteConfigStringWithKey:nsKey];

    if (result != nil) {
        callback(result.UTF8String);
    } else {
        callback("");
    }
}

typedef void (*GetFirebaseRemoteConfigBooleanCallbackDelegate)(const bool configBool);
void noctuaGetFirebaseRemoteConfigBoolean(const char* key, GetFirebaseRemoteConfigBooleanCallbackDelegate callback) {

    if (callback == NULL) {
        return;
    }

    NSString* nsKey = [NSString stringWithUTF8String:key];
    BOOL result = [Noctua getFirebaseRemoteConfigBooleanWithKey:nsKey];

    // Convert Objective-C BOOL → C bool
    bool cppBool = (result == YES);
    callback(cppBool);
}

typedef void (*GetFirebaseRemoteConfigDoubleCallbackDelegate)(const double configDouble);
void noctuaGetFirebaseRemoteConfigDouble(const char* key, GetFirebaseRemoteConfigDoubleCallbackDelegate callback) {

    if (callback == NULL) {
        return;
    }

    NSString* nsKey = [NSString stringWithUTF8String:key];
    double result = [Noctua getFirebaseRemoteConfigDoubleWithKey:nsKey];
    callback(result);
}

typedef void (*GetFirebaseRemoteConfigLongCallbackDelegate)(long long configLong);
void noctuaGetFirebaseRemoteConfigLong(const char* key, GetFirebaseRemoteConfigLongCallbackDelegate callback) {

    if (callback == NULL) {
        return;
    }

    NSString* nsKey = [NSString stringWithUTF8String:key];
    long long result = [Noctua getFirebaseRemoteConfigLongWithKey:nsKey];
    callback(result);
}

typedef void (*FcmBoolCallbackDelegate)(bool success);
void noctuaSubscribeToFcmTopic(const char* topic, FcmBoolCallbackDelegate callback) {
    if (topic == NULL) {
        if (callback != NULL) callback(false);
        return;
    }

    NSString* nsTopic = [NSString stringWithUTF8String:topic];
    [Noctua subscribeToFcmTopic:nsTopic completion:^(BOOL success) {
        if (callback != NULL) {
            callback(success == YES);
        }
    }];
}

void noctuaUnsubscribeFromFcmTopic(const char* topic, FcmBoolCallbackDelegate callback) {
    if (topic == NULL) {
        if (callback != NULL) callback(false);
        return;
    }

    NSString* nsTopic = [NSString stringWithUTF8String:topic];
    [Noctua unsubscribeFromFcmTopic:nsTopic completion:^(BOOL success) {
        if (callback != NULL) {
            callback(success == YES);
        }
    }];
}

typedef void (*GetFcmTokenCallbackDelegate)(const char* token);
void noctuaGetFcmToken(GetFcmTokenCallbackDelegate callback) {
    [Noctua getFcmTokenWithCompletion:^(NSString * _Nonnull token) {
        if (callback != NULL) {
            callback(token != nil ? [token UTF8String] : "");
        }
    }];
}

void noctuaDeleteFcmToken(FcmBoolCallbackDelegate callback) {
    [Noctua deleteFcmTokenWithCompletion:^(BOOL success) {
        if (callback != NULL) {
            callback(success == YES);
        }
    }];
}

typedef void (*AdjustAttributionCallbackDelegate)(const char* jsonString);
void noctuaGetAdjustAttribution(AdjustAttributionCallbackDelegate callback) {
    if (callback == NULL) {
        return;
    }

    [Noctua getAdjustCurrentAttributionWithCompletion:^(NSDictionary<NSString *, id> * _Nonnull attribution) {

        if (attribution == nil || attribution.count == 0) {
            callback("{}");
            return;
        }

        NSError *error = nil;
        NSData *jsonData = [NSJSONSerialization dataWithJSONObject:attribution options:0 error:&error];

        if (error || jsonData == nil) {
            callback("{}");
            return;
        }

        NSString *jsonString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
        callback([jsonString UTF8String]);
    }];
}

// MARK: - Adjust Device Info

typedef void (*AdjustDeviceInfoCallbackDelegate)(const char* value);

void noctuaGetAdjustAdid(AdjustDeviceInfoCallbackDelegate callback) {
    if (callback == NULL) return;
    [Noctua getAdjustAdidWithCompletion:^(NSString * _Nullable adid) {
        callback(adid ? [adid UTF8String] : "");
    }];
}

void noctuaGetAdjustIdfa(AdjustDeviceInfoCallbackDelegate callback) {
    if (callback == NULL) return;
    [Noctua getAdjustIdfaWithCompletion:^(NSString * _Nullable idfa) {
        callback(idfa ? [idfa UTF8String] : "");
    }];
}

void noctuaGetAdjustIdfv(AdjustDeviceInfoCallbackDelegate callback) {
    if (callback == NULL) return;
    [Noctua getAdjustIdfvWithCompletion:^(NSString * _Nullable idfv) {
        callback(idfv ? [idfv UTF8String] : "");
    }];
}

void noctuaGetAdjustSdkVersion(AdjustDeviceInfoCallbackDelegate callback) {
    if (callback == NULL) return;
    [Noctua getAdjustSdkVersionWithCompletion:^(NSString * _Nullable version) {
        callback(version ? [version UTF8String] : "");
    }];
}

// MARK: - Legacy Blob Event Storage

typedef void (*GetEventsCallbackDelegate)(const char* eventsJson);
void noctuaGetEvents(GetEventsCallbackDelegate callback) {
    [Noctua getEventsOnResult:^(NSArray<NSString *> * _Nonnull events)
    {
        NSError *error;
        NSData *jsonData = [NSJSONSerialization dataWithJSONObject:events options:0 error:&error];
        if (!jsonData) {
            NSLog(@"Error serializing events to JSON: %@", error);
            callback(NULL);
            return;
        }
        NSString *jsonString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
        callback([jsonString UTF8String]);
    }];
}

void noctuaSaveEvents(const char* eventsJson) {
    NSString *eventsJsonStr = [NSString stringWithUTF8String:eventsJson];
    [Noctua saveEventsWithJsonString:eventsJsonStr];
}

void noctuaDeleteEvents() {
    [Noctua deleteEvents];
}

// MARK: - Per-Row Event Storage (Unlimited)

void noctuaInsertEvent(const char* eventJson) {
    if (eventJson == NULL) {
        NSLog(@"noctuaInsertEvent: eventJson is null");
        return;
    }
    NSString *eventJsonStr = [NSString stringWithUTF8String:eventJson];
    [Noctua insertEventWithEventJson:eventJsonStr];
}

typedef void (*GetEventsBatchCallbackDelegate)(const char* eventsJson);
void noctuaGetEventsBatch(int limit, int offset, GetEventsBatchCallbackDelegate callback) {
    if (callback == NULL) {
        return;
    }
    [Noctua getEventsBatchWithLimit:limit offset:offset onResult:^(NSString * _Nonnull json) {
        const char* cJson = [json UTF8String];
        callback(cJson ? cJson : "[]");
    }];
}

typedef void (*DeleteEventsByIdsCallbackDelegate)(int deletedCount);
void noctuaDeleteEventsByIds(const char* idsJson, DeleteEventsByIdsCallbackDelegate callback) {
    if (idsJson == NULL) {
        NSLog(@"noctuaDeleteEventsByIds: idsJson is null");
        if (callback != NULL) {
            callback(0);
        }
        return;
    }
    NSString *idsJsonStr = [NSString stringWithUTF8String:idsJson];
    [Noctua deleteEventsByIdsWithIdsJson:idsJsonStr onResult:^(int32_t count) {
        if (callback != NULL) {
            callback((int)count);
        }
    }];
}

typedef void (*GetEventCountCallbackDelegate)(int count);
void noctuaGetEventCount(GetEventCountCallbackDelegate callback) {
    if (callback == NULL) {
        return;
    }
    [Noctua getEventCountOnResult:^(int32_t count) {
        callback((int)count);
    }];
}

void noctuaRequestInAppReview(void) {
    [Noctua requestInAppReview];
}

// MARK: - Native Lifecycle Callback

typedef void (*NativeLifecycleCallbackDelegate)(const char* lifecycleEvent);
static NativeLifecycleCallbackDelegate _nativeLifecycleCallback = NULL;

void noctuaRegisterLifecycleCallback(NativeLifecycleCallbackDelegate callback) {
    _nativeLifecycleCallback = callback;
    if (callback != NULL) {
        [Noctua registerLifecycleCallbackWithCallback:^(NSString * _Nonnull event) {
            if (_nativeLifecycleCallback != NULL) {
                _nativeLifecycleCallback([event UTF8String]);
            }
        }];
    } else {
        [Noctua registerLifecycleCallbackWithCallback:nil];
    }
}
