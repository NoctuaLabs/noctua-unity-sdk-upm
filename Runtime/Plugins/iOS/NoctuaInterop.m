#import <NoctuaSDK/NoctuaSDK.h>
#import <NoctuaSDK/NoctuaSDK-Swift.h>

void noctuaInitialize(void) {
    [Noctua initialize];
}

void noctuaTrackAdRevenue(const char* source, double revenue, const char* currency, const char* extraPayloadJson) {
    NSString *sourceStr = [NSString stringWithUTF8String:source];
    NSString *currencyStr = [NSString stringWithUTF8String:currency];
    NSString *extraPayloadStr = [NSString stringWithUTF8String:extraPayloadJson];
    NSData *data = [extraPayloadStr dataUsingEncoding:NSUTF8StringEncoding];
    NSDictionary *extraPayload = [NSJSONSerialization JSONObjectWithData:data options:0 error:nil];
    [Noctua trackAdRevenueWithSource:sourceStr revenue:revenue currency:currencyStr extraPayload:extraPayload];
}

void noctuaTrackPurchase(const char* orderId, double amount, const char* currency, const char* extraPayloadJson) {
    NSString *orderIdStr = [NSString stringWithUTF8String:orderId];
    NSString *currencyStr = [NSString stringWithUTF8String:currency];
    NSString *extraPayloadStr = [NSString stringWithUTF8String:extraPayloadJson];
    NSData *data = [extraPayloadStr dataUsingEncoding:NSUTF8StringEncoding];
    NSDictionary *extraPayload = [NSJSONSerialization JSONObjectWithData:data options:0 error:nil];
    [Noctua trackPurchaseWithOrderId:orderIdStr amount:amount currency:currencyStr extraPayload:extraPayload];
}

void noctuaTrackCustomEvent(const char* eventName, const char* payloadJson) {
    NSString *eventNameStr = [NSString stringWithUTF8String:eventName];
    NSString *payloadStr = [NSString stringWithUTF8String:payloadJson];
    NSData *data = [payloadStr dataUsingEncoding:NSUTF8StringEncoding];
    NSDictionary *payload = [NSJSONSerialization JSONObjectWithData:data options:0 error:nil];
    [Noctua trackCustomEvent:eventNameStr payload:payload];
}
