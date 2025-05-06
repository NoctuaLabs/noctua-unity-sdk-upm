# Noctua SDK for Unity Package

This package provides a set of tools to help you develop Noctua games using Unity.

## Getting Started

1. Create a new Unity project or open an existing one.
2. This package also requires Google's External Dependency Manager for Unity. Add following lines to your project's `Packages/manifest.json` file:

    ```json
    {
      "dependencies": {
        // other dependencies ...
        "com.google.external-dependency-manager": "https://github.com/google-unity/external-dependency-manager.git#1.2.181",
        "com.noctuagames.sdk": "https://github.com/NoctuaLabs/noctua-unity-sdk-upm.git#0.8.0",
        // other dependencies ...
      }
    }
    ```

3. Platform Settings for External Dependency Manager

    * **Android**
        1. Enable gradle custom templates in Project Settings > Player > (Android Logo) > Publishing Settings

            * Check Custom Main Gradle Template
            * Check Custom Gradle Properties Template
            * Check Custom Gradle Settings Template

        2. Go to Assets > External Dependency Manager > Android Resolver, and click Resolve

4. Create a config file called "noctuagg.json" in the "Assets/StreamingAssets" folder. The file should contain the following content:

    ```json
    {
  "productCode": "noctuaggdev", // Deprecated
  "clientId": "1-e724f5a9e6f1", // Derived from backend
  
  // To track changes / version  
  "meta": {
    "name": "Sortify - 20250320",
    "version": 4
  },
   
  // White labeling and co publisher
  // for region-based behaviours related, please see noctua.region
  "copublisher": {
    "companyName": "",
    "companyWebsiteUrl": "",
    "companyTermUrl": "",
    "companyPrivacyUrl": ""
  },
  
  // Noctua SDK Section
  "noctua": {
    "region": "vn", // Some region has verify specific behaviours/flow
    "sentryDsnUrl": "https://", // Sentry DSN
    
    // Client side feature flags that will not be overriden by server
    "iaaEnabled": true, // In-app advertising
    "iapDisabled": true, // Some games may use their own IAP implementation and we need to disable our IAP internal callback
    "sandboxEnabled": true, // If true, the SDK request will be point out to "https://sandbox-sdk-api-v2.noctuaprojects.com/api/v1"
    "offlineFirstEnabled": true, // To enable offline first behaviours
    "welcomeToastDisabled": false, // Usually casual games that need offline first feature wants to disable welcome toast// Remote feature flag:
    
    // Generic feature flags that could be overriden by server, the actual remote configs
    // It's the feature reponsibility to parse it properly.
    "remoteFeatureFlags": { // If defined here, then it will be default value of the remote config
      "ssoDisabled": true, // To disable all SSO's entirely, 
      "vnLegalPurposeEnabled": true,
      "vnLegalPurposeFullKycEnabled": true,
      "vnLegalPurposePhoneNumberVerificationEnabled": true,
      "key": false
    }
  },
  
  // Third party trackers section
  "adjust": { // Only Adjust that will have event map
    "android": {
      "appToken": "kg7l0jhuem80",
      "eventMap": {
        "purchase": "qye2vk",
        "login": "xoizir"
      }
    },
    "ios": {
      "appToken": "kg7l0jhuem80",
      "eventMap": {
        "purchase": "qye2vk",
        "login": "xoizir"
      }
    }
  },
  "facebook": { // Required for Facebook analytics and SSO
    "android": {
      "appId": "807936677546640",
      "clientToken": "5eb9cb06333460b31c60e484676110a0",
      "displayName": "SDK Test Noctua",
      "enableDebug": true
    },
    "ios": {
      "appId": "807936677546640",
      "clientToken": "5eb9cb06333460b31c60e484676110a0",
      "displayName": "SDK Test Noctua",
      "enableDebug": true
    }
  },
  "firebase": {
    "android": {
      "customEventDisabled": true // Custom event for specific thirdparty tracker could be disabled
    },
    "ios": {
      "customEventDisabled": false
    }
  }
}
    ```

5. If you use Firebase, put the "google-services.json" and "GoogleService-Info.plist" files in the "Assets/StreamingAssets" folder.

6. Add some C# scripts to use the SDK features.
    
    ```csharp
    public class NoctuaEventsHandlers : MonoBehaviour
    {
        public Button btnTrackAdRevenue;
        public Button btnTrackPurchase;
        public Button btnTrackCustomEvent;
    
        private async void Awake()
        {
             await Noctua.InitAsync();
        }
    
        private void Start()
        {
            btnTrackAdRevenue.onClick.AddListener(
                () =>
                {
                    Noctua.TrackAdRevenue(
                        "source",
                        1.3,
                        "USD",
                        new()  { { "k1", 23 }, { "k2", false }, { "k3", DayOfWeek.Monday } }
                    );
                }
            );
            
            btnTrackPurchase.onClick.AddListener(
                () =>
                {
                    Noctua.TrackPurchase(
                        Guid.NewGuid().ToString(),
                        1.5,
                        "USD",
                        new()  { { "k1", 42 }, { "k2", "k2string" }, { "k3", DateTime.UtcNow } }
                    );
                }
            );
            
            btnTrackCustomEvent.onClick.AddListener(
                () =>
                {
                    Noctua.TrackCustomEvent(
                        "TestSendEvent",
                        new()  { { "k1", 19 }, { "k2", "k2string" }, { "k3", DateTime.UtcNow } }
                    );
                }
            );
        }
    
        private void OnApplicationPause(bool pause)
        {
            Noctua.OnApplicationPause(pause);
        }
        
        private void OnDestroy()
        {
            btnTrackAdRevenue.onClick.RemoveAllListeners();
            btnTrackPurchase.onClick.RemoveAllListeners();
            btnTrackCustomEvent.onClick.RemoveAllListeners();
        }
    }
    ```

7. Attach the buttons in your scene to the script's fields.

## Sample Project

The Noctua Unity SDK package is embedded within the sample project. The sample project demonstrates how to use the SDK features.
Those features are:

1. Event tracking (Ad Revenue, Purchase, Custom Event)

    Event tracking modules are used to track events in the game. The events are represented in these methods:

    * `Noctua.Event.TrackAdRevenue(string source, double revenue, string currency, Dictionary<string, object> parameters)`
    * `Noctua.Event.TrackPurchase(string orderId, double amount, string currency, Dictionary<string, object> parameters)`
    * `Noctua.Event.TrackCustomEvent(string eventName, Dictionary<string, object> parameters)`

    The parameters are optional and can be used to send additional data with the event.

2. Authentication

    Authentication module is used to authenticate the player in the game. The authentication is represented in these methods:

    * `Noctua.Auth.AuthenticateAsync()`

        This method will automatically authenticate the player using the saved credentials.
        If the player is not authenticated, the method will login the player as a guest.     
 
    * `Noctua.Auth.LoginAsGuest()`
     
        This method will login the player as a guest.
   
    * `Noctua.Auth.SwitchAccountAsync()`

         This method will show UI for the player to switch the account.   

    * `Noctua.Auth.ShowUserCenter()`
 
         This method will show UI for the player to manage the account, such as editing user profile or connecting to social accounts. 

    The authentication module supports the following authentication methods:

    * Noctua Account (Email and Password)
    * Google
    * Facebook

3. In-app purchase

    In-app purchase module is used to handle in-app purchases in the game. The in-app purchase is represented in these methods:
    
    * `Noctua.IAP.GetProductListAsync()`
    * `Noctua.IAP.PurchaseItemAsync(PurchaseRequest purchaseRequest))`

    Noctua supports the following payment methods:

    * Google Play Billing (Android)
    * Apple StoreKit (iOS)
    * Noctua Wallet (All Platforms)

4. Noctua Platform

    Noctua platform module is used to interact with the Noctua platform. The platform module is represented in these methods:

    * `Noctua.Platform.Content.ShowAnnouncement()`
    * `Noctua.Platform.Content.ShowReward()`
    * `Noctua.Platform.Content.ShowCustomerService()`
    * `Noctua.Platform.Locale.GetLanguage()`
    * `Noctua.Platform.Locale.GetCountry()`
    * `Noctua.Platform.Locale.GetCurrency()`

    Platform contents are used to show announcements, rewards, and customer service web pages in the game using the in-app browser.