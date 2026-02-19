extern "C" {

void noctuaInitialize(void);
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
typedef void (*GetFirebaseIDCallbackDelegate)(const char* firebaseId);
void noctuaGetFirebaseInstallationID(GetFirebaseIDCallbackDelegate callback);
typedef void (*GetFirebaseSessionIDCallbackDelegate)(const char* sessionId);
void noctuaGetFirebaseAnalyticsSessionID(GetFirebaseSessionIDCallbackDelegate callback);
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

}
