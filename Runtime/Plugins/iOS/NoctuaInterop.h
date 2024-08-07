extern "C" {

void noctuaInitialize(void);
void noctuaTrackAdRevenue(const char* source, double revenue, const char* currency, const char* extraPayloadJson);
void noctuaTrackPurchase(const char* orderId, double amount, const char* currency, const char* extraPayloadJson);
void noctuaTrackCustomEvent(const char* eventName, const char* payloadJson);

}
