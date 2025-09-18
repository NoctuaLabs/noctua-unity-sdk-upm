extern "C" {

void noctuaInitialize(void);
void noctuaTrackAdRevenue(const char* source, double revenue, const char* currency, const char* extraPayloadJson);
void noctuaTrackPurchase(const char* orderId, double amount, const char* currency, const char* extraPayloadJson);
void noctuaTrackCustomEvent(const char* eventName, const char* payloadJson);
void noctuaTrackCustomEventWithRevenue(const char* eventName, double revenue, const char* currency, const char* payloadJson);
void noctuaOnOnline();
void noctuaOnOffline();

typedef void (*PurchaseCompletionDelegate)(bool success, const char* message);
void noctuaPurchaseItem(const char* productId, PurchaseCompletionDelegate callback);
typedef void (*ProductPurchasedCompletionDelegate)(bool hasPurchased);
void noctuaGetProductPurchasedById(const char* productId, ProductPurchasedCompletionDelegate callback);
typedef void (*ReceiptCompletionDelegate)(const char* receipt);
void noctuaGetReceiptProductPurchasedStoreKit1(const char* productId, ReceiptCompletionDelegate callback);
typedef void (*GetFirebaseIDCallbackDelegate)(const char* firebaseId);
void noctuaGetFirebaseInstallationID(GetFirebaseIDCallbackDelegate callback)
typedef void (*GetFirebaseSessionIDCallbackDelegate)(const char* sessionId);
void noctuaGetFirebaseAnalyticsSessionID(GetFirebaseSessionIDCallbackDelegate callback);

}
