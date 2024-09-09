#import <NoctuaSDK/NoctuaSDK-Swift.h>

void noctuaInitialize(void) {
    NSError *error = nil;
    [Noctua initNoctuaAndReturnError:&error];
    if (error) {
        NSLog(@"Error initializing Noctua: %@", error);
    }
}

void noctuaTrackAdRevenue(const char* source, double revenue, const char* currency, const char* extraPayloadJson) {
    NSLog(@"source: %s, revenue: %f, currency: %s, extraPayload: %s", source, revenue, currency, extraPayloadJson);

    NSString *sourceStr = [NSString stringWithUTF8String:source];
    NSString *currencyStr = [NSString stringWithUTF8String:currency];
    NSString *extraPayloadStr = [NSString stringWithUTF8String:extraPayloadJson];
    NSData *data = [extraPayloadStr dataUsingEncoding:NSUTF8StringEncoding];
    NSDictionary *extraPayload = [NSJSONSerialization JSONObjectWithData:data options:0 error:nil];
    [Noctua trackAdRevenueWithSource:sourceStr revenue:revenue currency:currencyStr extraPayload:extraPayload];
    // log params

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
