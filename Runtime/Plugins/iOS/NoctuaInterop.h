extern "C" {

void noctuaInitialize(void);
void noctuaTrackAdRevenue(const char* source, double revenue, const char* currency, const char* extraPayloadJson);
void noctuaTrackPurchase(const char* orderId, double amount, const char* currency, const char* extraPayloadJson);
void noctuaTrackCustomEvent(const char* eventName, const char* payloadJson);

/*
// Define the callback function type
typedef void (*PurchaseCompletion)(bool success, const char* message);

// Declare the noctuaPurchaseItem function
void noctuaPurchaseItem(const char* productId, PurchaseCompletion callback);
*/

typedef void (*PurchaseCompletionDelegate)(bool success, const char* message);

void noctuaPurchaseItem(const char* productId, PurchaseCompletionDelegate callback);

}
