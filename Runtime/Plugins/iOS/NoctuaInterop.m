@import NoctuaSDK;
#import <UIKit/UIKit.h>

// MARK: - Initialization

void noctuaInitialize(bool verifyPurchasesOnServer, bool useStoreKit1) {
    NSError *error = nil;
    [Noctua initNoctuaWithVerifyPurchasesOnServer:verifyPurchasesOnServer useStoreKit1:useStoreKit1 error:&error];
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

// MARK: - StoreKit / In-App Purchases (New API)

// Static state for StoreKit callback bridging
static BOOL _storeKitInitialized = NO;

// Pending callback pointers for purchase flow
typedef void (*CompletionDelegate)(bool success, const char* message);
static CompletionDelegate _pendingPurchaseCallback = NULL;

// Pending callback for get active currency
static CompletionDelegate _pendingActiveCurrencyCallback = NULL;

// Pending callback for get product purchased by id
typedef void (*ProductPurchasedCompletionDelegate)(bool success);
static ProductPurchasedCompletionDelegate _pendingProductPurchasedCallback = NULL;

// Pending callback for get receipt
typedef void (*ReceiptCompletionDelegate)(const char* message);
static ReceiptCompletionDelegate _pendingReceiptCallback = NULL;

// Pending callback for full purchase status detail
typedef void (*ProductPurchaseStatusDetailDelegate)(const char* statusJson);
static ProductPurchaseStatusDetailDelegate _pendingPurchaseStatusDetailCallback = NULL;

static void ensureStoreKitInitialized(void) {
    if (_storeKitInitialized) {
        return;
    }
    _storeKitInitialized = YES;

    [Noctua initializeStoreKitOnPurchaseCompleted:^(NoctuaPurchaseResult * _Nonnull result) {
        NSLog(@"StoreKit onPurchaseCompleted: success=%d, productId=%@", result.success, result.productId);
        NSLog(@"StoreKit onPurchaseCompleted detail: purchaseToken=%@, orderId=%@, errorCode=%ld, purchaseState=%ld, originalJson.length=%lu, message=%@",
            result.purchaseToken, result.orderId, (long)result.errorCode, (long)result.purchaseState,
            (unsigned long)result.originalJson.length, result.message);
        if (_pendingPurchaseCallback != NULL) {
            CompletionDelegate callback = _pendingPurchaseCallback;
            _pendingPurchaseCallback = NULL;

            if (result.success) {
                const char* receipt = [result.originalJson UTF8String];
                callback(true, receipt ? receipt : "");
            } else {
                NSString *msg = result.message ?: @"Purchase failed";
                callback(false, [msg UTF8String]);
            }
        }
    } onPurchaseUpdated:^(NoctuaPurchaseResult * _Nonnull result) {
        NSLog(@"StoreKit onPurchaseUpdated: success=%d, productId=%@", result.success, result.productId);
        NSLog(@"StoreKit onPurchaseUpdated detail: purchaseToken=%@, orderId=%@, errorCode=%ld, purchaseState=%ld, originalJson.length=%lu, message=%@",
            result.purchaseToken, result.orderId, (long)result.errorCode, (long)result.purchaseState,
            (unsigned long)result.originalJson.length, result.message);
        // If purchase callback is still pending (e.g., for pending state updates), handle it
        if (_pendingPurchaseCallback != NULL) {
            CompletionDelegate callback = _pendingPurchaseCallback;
            _pendingPurchaseCallback = NULL;

            if (result.success) {
                const char* receipt = [result.originalJson UTF8String];
                callback(true, receipt ? receipt : "");
            } else {
                NSString *msg = result.message ?: @"Purchase updated with failure";
                callback(false, [msg UTF8String]);
            }
        }
    } onProductDetailsLoaded:^(NSArray<NoctuaProductDetails *> * _Nonnull details) {
        NSLog(@"StoreKit onProductDetailsLoaded: count=%lu", (unsigned long)details.count);
        if (_pendingActiveCurrencyCallback != NULL) {
            CompletionDelegate callback = _pendingActiveCurrencyCallback;
            _pendingActiveCurrencyCallback = NULL;

            if (details.count > 0) {
                NoctuaProductDetails *first = details[0];
                const char* currency = [first.priceCurrencyCode UTF8String];
                callback(true, currency ? currency : "");
            } else {
                callback(false, "No product details found");
            }
        }
    } onQueryPurchasesCompleted:^(NSArray<NoctuaPurchaseResult *> * _Nonnull results) {
        NSLog(@"StoreKit onQueryPurchasesCompleted: count=%lu", (unsigned long)results.count);
    } onRestorePurchasesCompleted:^(NSArray<NoctuaPurchaseResult *> * _Nonnull results) {
        NSLog(@"StoreKit onRestorePurchasesCompleted: count=%lu", (unsigned long)results.count);
    } onProductPurchaseStatusResult:^(NoctuaProductPurchaseStatus * _Nonnull status) {
        NSLog(@"StoreKit onProductPurchaseStatusResult: productId=%@, isPurchased=%d", status.productId, status.isPurchased);
        NSLog(@"StoreKit onProductPurchaseStatusResult detail: purchaseToken=%@, orderId=%@, isAcknowledged=%d, isAutoRenewing=%d, purchaseTime=%lld, originalJson.length=%lu",
            status.purchaseToken, status.orderId, status.isAcknowledged, status.isAutoRenewing,
            status.purchaseTime, (unsigned long)status.originalJson.length);
        if (_pendingProductPurchasedCallback != NULL) {
            ProductPurchasedCompletionDelegate callback = _pendingProductPurchasedCallback;
            _pendingProductPurchasedCallback = NULL;
            callback(status.isPurchased);
        }
        if (_pendingReceiptCallback != NULL) {
            ReceiptCompletionDelegate callback = _pendingReceiptCallback;
            _pendingReceiptCallback = NULL;
            if (status.isPurchased) {
                const char* token = [status.purchaseToken UTF8String];
                callback(token ? token : "");
            } else {
                callback(NULL);
            }
        }
        if (_pendingPurchaseStatusDetailCallback != NULL) {
            ProductPurchaseStatusDetailDelegate callback = _pendingPurchaseStatusDetailCallback;
            _pendingPurchaseStatusDetailCallback = NULL;

            NSDictionary *statusDict = @{
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
            };

            NSError *error = nil;
            NSData *jsonData = [NSJSONSerialization dataWithJSONObject:statusDict options:0 error:&error];
            if (jsonData) {
                NSString *jsonString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
                callback([jsonString UTF8String]);
            } else {
                callback("{}");
            }
        }
    } onServerVerificationRequired:^(NoctuaPurchaseResult * _Nonnull result, enum ConsumableType consumableType) {
        NSLog(@"StoreKit onServerVerificationRequired: productId=%@, success=%d", result.productId, result.success);
        NSLog(@"StoreKit onServerVerificationRequired detail: purchaseToken=%@, orderId=%@, errorCode=%ld, purchaseState=%ld, originalJson.length=%lu, message=%@",
            result.purchaseToken, result.orderId, (long)result.errorCode, (long)result.purchaseState,
            (unsigned long)result.originalJson.length, result.message);
        // Forward to pending purchase callback so Unity can run its own VerifyOrderAsync
        // Pass purchaseToken + consumableType alongside the receipt so Unity can call
        // completePurchaseProcessing after server verification succeeds.
        if (_pendingPurchaseCallback != NULL) {
            CompletionDelegate callback = _pendingPurchaseCallback;
            _pendingPurchaseCallback = NULL;
            if (result.success) {
                NSDictionary *callbackData = @{
                    @"receipt": result.originalJson ?: @"",
                    @"purchaseToken": result.purchaseToken ?: @"",
                    @"consumableType": @(consumableType)
                };
                NSData *jsonData = [NSJSONSerialization dataWithJSONObject:callbackData options:0 error:nil];
                NSString *jsonString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
                const char* msg = [jsonString UTF8String];
                callback(true, msg ? msg : "");
            } else {
                NSString *msg = result.message ?: @"Server verification required";
                callback(false, [msg UTF8String]);
            }
        }
    } onStoreKitError:^(enum StoreKitErrorCode errorCode, NSString * _Nonnull message) {
        NSLog(@"StoreKit onStoreKitError: code=%ld, message=%@", (long)errorCode, message);
        // If a purchase was pending, fail it
        if (_pendingPurchaseCallback != NULL) {
            CompletionDelegate callback = _pendingPurchaseCallback;
            _pendingPurchaseCallback = NULL;
            const char* msg = [message UTF8String];
            callback(false, msg ? msg : "StoreKit error");
        }
        // If currency query was pending, fail it
        if (_pendingActiveCurrencyCallback != NULL) {
            CompletionDelegate callback = _pendingActiveCurrencyCallback;
            _pendingActiveCurrencyCallback = NULL;
            const char* msg = [message UTF8String];
            callback(false, msg ? msg : "StoreKit error");
        }
    }];
}

void noctuaPurchaseItem(const char* productId, CompletionDelegate callback) {
    NSLog(@"noctuaPurchaseItem called with productId: %s", productId);

    if (productId == NULL) {
        NSLog(@"Product ID is null");
        if (callback != NULL) {
            callback(false, "Product ID is null");
        }
        return;
    }

    NSString *productIdStr = [NSString stringWithUTF8String:productId];
    if (productIdStr.length == 0) {
        NSLog(@"Product ID is empty");
        if (callback != NULL) {
            callback(false, "Product ID is empty");
        }
        return;
    }

    ensureStoreKitInitialized();

    _pendingPurchaseCallback = callback;

    NSLog(@"Calling Noctua purchase via new StoreKit API");
    [Noctua purchaseWithProductId:productIdStr];
}

void noctuaGetActiveCurrency(const char* productId, CompletionDelegate callback) {
    NSLog(@"noctuaGetActiveCurrency called with productId: %s", productId);

    if (productId == NULL) {
        NSLog(@"Product ID is null");
        if (callback != NULL) {
            callback(false, "Product ID is null");
        }
        return;
    }

    NSString *productIdStr = [NSString stringWithUTF8String:productId];
    if (productIdStr.length == 0) {
        NSLog(@"Product ID is empty");
        if (callback != NULL) {
            callback(false, "Product ID is empty");
        }
        return;
    }

    ensureStoreKitInitialized();

    _pendingActiveCurrencyCallback = callback;

    [Noctua queryProductDetailsWithProductIds:@[productIdStr] productType:ProductTypeInapp];
}

void noctuaGetProductPurchasedById(const char* productId, ProductPurchasedCompletionDelegate callback) {
    NSLog(@"noctuaGetProductPurchasedById called with productId: %s", productId);

    if (productId == NULL) {
        NSLog(@"Product ID is null");
        if (callback != NULL) {
            callback(false);
        }
        return;
    }

    NSString *productIdStr = [NSString stringWithUTF8String:productId];
    if (productIdStr.length == 0) {
        NSLog(@"Product ID is empty");
        if (callback != NULL) {
            callback(false);
        }
        return;
    }

    ensureStoreKitInitialized();

    _pendingProductPurchasedCallback = callback;

    [Noctua getProductPurchaseStatusWithProductId:productIdStr];
}

void noctuaGetReceiptProductPurchasedStoreKit1(const char* productId, ReceiptCompletionDelegate callback) {
    NSLog(@"noctuaGetReceiptProductPurchasedStoreKit1 called with productId: %s", productId);

    if (productId == NULL) {
        NSLog(@"Product ID is null");
        if (callback != NULL) {
            callback(NULL);
        }
        return;
    }

    NSString *productIdStr = [NSString stringWithUTF8String:productId];
    if (productIdStr.length == 0) {
        NSLog(@"Product ID is empty");
        if (callback != NULL) {
            callback(NULL);
        }
        return;
    }

    ensureStoreKitInitialized();

    _pendingReceiptCallback = callback;

    [Noctua getProductPurchaseStatusWithProductId:productIdStr];
}

void noctuaGetProductPurchaseStatusDetail(const char* productId, ProductPurchaseStatusDetailDelegate callback) {
    NSLog(@"noctuaGetProductPurchaseStatusDetail called with productId: %s", productId);

    if (productId == NULL) {
        NSLog(@"Product ID is null");
        if (callback != NULL) {
            callback("{}");
        }
        return;
    }

    NSString *productIdStr = [NSString stringWithUTF8String:productId];
    if (productIdStr.length == 0) {
        NSLog(@"Product ID is empty");
        if (callback != NULL) {
            callback("{}");
        }
        return;
    }

    ensureStoreKitInitialized();

    _pendingPurchaseStatusDetailCallback = callback;

    [Noctua getProductPurchaseStatusWithProductId:productIdStr];
}

// MARK: - Additional StoreKit Functions

void noctuaRegisterProduct(const char* productId, int consumableType) {
    NSLog(@"noctuaRegisterProduct called with productId: %s, type: %d", productId, consumableType);
    if (productId == NULL) {
        NSLog(@"Product ID is null");
        return;
    }
    NSString *productIdStr = [NSString stringWithUTF8String:productId];

    ensureStoreKitInitialized();

    [Noctua registerProductWithProductId:productIdStr consumableType:(enum ConsumableType)consumableType];
}

typedef void (*BoolCallbackDelegate)(bool success);

void noctuaCompletePurchaseProcessing(const char* purchaseToken, int consumableType, bool verified, BoolCallbackDelegate callback) {
    NSLog(@"noctuaCompletePurchaseProcessing called with token: %s, type: %d, verified: %d", purchaseToken, consumableType, verified);
    if (purchaseToken == NULL) {
        NSLog(@"Purchase token is null");
        if (callback != NULL) {
            callback(false);
        }
        return;
    }
    NSString *tokenStr = [NSString stringWithUTF8String:purchaseToken];

    ensureStoreKitInitialized();

    [Noctua completePurchaseProcessingWithPurchaseToken:tokenStr consumableType:(enum ConsumableType)consumableType verified:verified callback:^(BOOL success) {
        if (callback != NULL) {
            callback(success);
        }
    }];
}

void noctuaRestorePurchases(void) {
    NSLog(@"noctuaRestorePurchases called");
    ensureStoreKitInitialized();
    [Noctua restorePurchases];
}

void noctuaDisposeStoreKit(void) {
    NSLog(@"noctuaDisposeStoreKit called");
    [Noctua disposeStoreKit];
    _storeKitInitialized = NO;
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
