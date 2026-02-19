package com.noctuagames.sdk;

import com.noctuagames.sdk.models.NoctuaBillingConfig;

public class NoctuaBillingConfigHelper {
    public static NoctuaBillingConfig create(
        boolean enablePendingPurchases,
        boolean enableAutoServiceReconnection,
        boolean verifyPurchasesOnServer
    ) {
        return new NoctuaBillingConfig(
            enablePendingPurchases,
            enableAutoServiceReconnection,
            verifyPurchasesOnServer
        );
    }
}
