@import NoctuaSDK;
#import <UIKit/UIKit.h>

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

void noctuaTrackCustomEventWithRevenue(const char* eventName, double revenue, const char* currency, const char* payloadJson) {
    NSLog(@"eventName: %s, revenue: %f, currency: %s, payload: %s", eventName, revenue, currency, payloadJson);

    NSString *eventNameStr = [NSString stringWithUTF8String:eventName];
    NSString *currencyStr = [NSString stringWithUTF8String:currency];
    NSString *payloadStr = [NSString stringWithUTF8String:payloadJson];
    NSData *data = [payloadStr dataUsingEncoding:NSUTF8StringEncoding];
    NSDictionary *payload = [NSJSONSerialization JSONObjectWithData:data options:0 error:nil];
    [Noctua trackCustomEventWithRevenue:eventNameStr revenue:revenue currency:currencyStr payload:payload];
}

typedef void (*CompletionDelegate)(bool success, const char* message);

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
    
    NSLog(@"Calling Noctua purchaseItem");
    [Noctua purchaseItem:productIdStr completion:^(BOOL success, NSString * _Nonnull message) {
        NSLog(@"Noctua purchase completion called. Success: %d, Message: %@", success, message);
        if (callback != NULL) {
            const char* cMessage = [message UTF8String];
            callback(success, cMessage);
        }
    }];
    /* Do nothing for now 
    */
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

    [Noctua getActiveCurrency:productIdStr completion:^(BOOL success, NSString * _Nonnull message) {
        NSLog(@"Noctua getActiveCurrency completion called. Success: %d, Message: %@", success, message);
        if (callback != NULL) {
            const char* cMessage = [message UTF8String];
            callback(success, cMessage);
        }
    }];
}

typedef void (*ProductPurchasedCompletionDelegate)(bool success);
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

    [Noctua getProductPurchasedByIdWithId:productIdStr completion:^(BOOL hasPurchased) {
        NSLog(@"Noctua getProductPurchasedById completion called. HasPurchased: %d", hasPurchased);
        if (callback != NULL) {
            callback(hasPurchased);
        }
    } completionHandler:^ {
        NSLog(@"Noctua get product purchased by id successfully!");
    }];
}

typedef void (*ReceiptCompletionDelegate)(const char* message);

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

    [Noctua getReceiptProductPurchasedStoreKit1WithId:productIdStr completion:^(NSString * _Nonnull receipt) {
        NSLog(@"Noctua getReceiptProductPurchasedStoreKit1 completion called. Receipt: %@", receipt);
        if (callback != NULL) {
            const char* cReceipt = [receipt UTF8String];
            callback(cReceipt);
        }
    }];
}

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

