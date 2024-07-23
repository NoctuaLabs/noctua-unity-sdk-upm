# Noctua SDK for Unity Package

This package provides a set of tools to help you develop Noctua games using Unity.

## Installation

This package requires Google's External Dependency Manager for Unity. 
You can install it and this package by adding following lines to your project's `Packages/manifest.json` file:

```json
{
  "dependencies": {
    "com.google.external-dependency-manager": "https://github.com/google-unity/external-dependency-manager.git#1.2.181",
	"com.noctuagames.sdk": "https://github.com/NoctuaLabs/noctua-unity-upm.git#0.1.0",
    "...": "..."
  }
}
```

## Getting Started

1. Create a new Unity project or open an existing one.
2. Add the package to your project as described in the Installation section.
3. Create a config file called "noctuagg.json" in the "Asserts/StreamingAssets" folder. The file should contain the following content:

    ```json
    {
      "productCode": "<NOCTUA_PRODUCT_CODE>",
      "adjust": {
        "appToken": "<ADJUST_APP_TOKEN>",
        "environment": "sandbox",
        "eventMap": {
          "Purchase": "<ADJUST_PURCHASE_EVENT_TOKEN>",
          "TestSendEvent": "<ADJUST_CUSTOM_EVENT_TOKEN>"
        }
      },
      "noctua": {
        "trackerUrl": ""
      }
    }
    ```

    a. Replace the placeholders with the actual values. 
    b. The "trackerUrl" field is optional and should be used only if you have a custom Noctua tracker URL.
    c. The "Purchase" event is a special event that is used to track purchases. 
    d. The "TestSendEvent" event is a custom event that you can use to track any custom event.

4. Add some C# scripts to use the SDK features.
    
    ```csharp
    public class NoctuaEventsHandlers : MonoBehaviour
    {
        public Button btnTrackAdRevenue;
        public Button btnTrackPurchase;
        public Button btnTrackCustomEvent;
    
        private void Awake()
        {
            Noctua.Init();
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

5. Attach the buttons in your scene to the script's fields.