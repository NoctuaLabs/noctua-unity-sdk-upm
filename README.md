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
      "clientId": "<NOCTUA_CLIENT_ID>",
      "adjust": {
        "appToken": "<ADJUST_APP_TOKEN>",
        "environment": "sandbox",
        "disableCustomEvent": false,
        "eventMap": {
          "Purchase": "<ADJUST_PURCHASE_EVENT_TOKEN>",
          "TestSendEvent": "<ADJUST_CUSTOM_EVENT_TOKEN>"
        }
      },
      "facebook": {
        "appId": "<FB_APP_ID>",
        "clientToken": "<FB_CLIENT_TOKEN",
        "displayName": "<FB_DISPLAY_NAME>",
        "enableDebug": true,
        "disableCustomEvent": false,
        "eventMap": {
          "AdRevenue": "ad_revenue",
          "Purchase": "purchase",
          "TestSendEvent": "test_send_event"
        }
      },
      "firebase": {
        "disableCustomEvent": false,
        "eventMap": {
          "AdRevenue": "ad_revenue",
          "Purchase": "purchase",
          "TestSendEvent": "test_send_event"
        }
      },
      "noctua": {
        "disableCustomEvent": false,
        "disableTracker": true,
        "trackerUrl": "<NOCTUA_TRACKER_URL>",
        "baseUrl": "<NOCTUA_BASE_URL>",
        "paymentBaseUrl": "<NOCTUA_PAYMENT_BASE_URL>",
        "announcementBaseUrl": "<NOCTUA_ANNOUNCEMENT_BASE_URL>",
        "rewardBaseUrl": "<NOCTUA_REWARD_BASE_URL>",
        "customerServiceBaseUrl": "<NOCTUA_CUSTOMER_SERVICE_BASE_URL>"
      }
    }
    ```

    a. Replace the placeholders with the actual values.

    b. The "trackerUrl" field is optional and should be used only if you have a custom Noctua tracker URL.

    c. The "Purchase" event is a special event that is used to track purchases. 

    d. The "TestSendEvent" event is a custom event that you can use to track any custom event.

    e. The "disableCustomEvent" field is optional and should be used only if you want to disable custom events.

    f. The "disableTracker" field is optional and should be used only if you want to disable the Noctua tracker completely.

    g. Third party services (e.g., Adjust, Facebook, Firebase) are optional. You can remove their configurations completely if you don't use them.

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