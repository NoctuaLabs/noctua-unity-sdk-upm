using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace com.noctuagames.sdk
{

    public class ProductDetails // Based on GoogleBilling ProductDetails struct
    {
    public string ProductId { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string Price { get; set; }
    public long PriceAmountMicros { get; set; }
    public string CurrencyCode { get; set; }
    public string ProductType { get; set; }
    public string SubscriptionPeriod { get; set; }
    public string FreeTrialPeriod { get; set; }
    public string IntroductoryPrice { get; set; }
    public long IntroductoryPriceAmountMicros { get; set; }
    public string IntroductoryPricePeriod { get; set; }
    public int IntroductoryPriceCycles { get; set; }

    public override string ToString()
    {
        return $"ProductId: {ProductId}\n" +
               $"Title: {Title}\n" +
               $"Description: {Description}\n" +
               $"Price: {Price}\n" +
               $"PriceAmountMicros: {PriceAmountMicros}\n" +
               $"CurrencyCode: {CurrencyCode}\n" +
               $"ProductType: {ProductType}\n" +
               $"SubscriptionPeriod: {SubscriptionPeriod}\n" +
               $"FreeTrialPeriod: {FreeTrialPeriod}\n" +
               $"IntroductoryPrice: {IntroductoryPrice}\n" +
               $"IntroductoryPriceAmountMicros: {IntroductoryPriceAmountMicros}\n" +
               $"IntroductoryPricePeriod: {IntroductoryPricePeriod}\n" +
               $"IntroductoryPriceCycles: {IntroductoryPriceCycles}";
        }
    }

    public class AdjustConfig
{
    [JsonProperty("appToken")]
    public string AppToken { get; set; }

    [JsonProperty("environment")]
    public string Environment { get; set; }

    [JsonProperty("eventMap")]
    public Dictionary<string, string> EventMap { get; set; }
}

public class NoctuaConfig
{
    [JsonProperty("trackerUrl")]
    public string TrackerUrl { get; set; }

    [JsonProperty("baseUrl")]
    public string BaseUrl { get; set; }
}

public class GlobalConfig
{
    [JsonProperty("clientId")]
    public string ClientId { get; set; }

    [JsonProperty("adjust")]
    public AdjustConfig Adjust { get; set; }

    [JsonProperty("noctua")]
    public NoctuaConfig Noctua { get; set; }
}

    public class Noctua
    {
        public static readonly NoctuaAuthService Auth;
        private static readonly INativePlugin Plugin = GetNativePlugin();
        private static readonly GoogleBilling GoogleBilling = new GoogleBilling();

        static Noctua()
        {
            GlobalConfig config = new GlobalConfig();
            Debug.Log("Loading streaming assets...");
            var configPath = Path.Combine(Application.streamingAssetsPath, "noctuagg.json");
            Debug.Log(configPath);
            var configLoadRequest = UnityWebRequest.Get(configPath);
            configLoadRequest.SendWebRequest();
            while (!configLoadRequest.isDone) {
                if (configLoadRequest.result == UnityWebRequest.Result.ProtocolError) {
                    Debug.Log("Loading streaming assets: configLoadRequest ProtocolError");
                    break;
                }
            }
            if (configLoadRequest.result == UnityWebRequest.Result.ProtocolError) {
                    Debug.Log("Loading streaming assets: configLoadRequest ProtocolError");
            } else {
                config = JsonConvert.DeserializeObject<GlobalConfig>(configLoadRequest.downloadHandler.text[1..]);
                Debug.Log(config.ClientId);
            }

            Auth = new NoctuaAuthService(
                new NoctuaAuthService.Config
                {
                    BaseUrl = config.Noctua.BaseUrl,
                    ClientId = config.ClientId
                }
            );

        }

        public static void Init()
        {
            Debug.Log("Noctua.Init()");
            Plugin?.Init();
            GoogleBilling?.Init();
        }

        public static void OnApplicationPause(bool pause)
        {
            Plugin?.OnApplicationPause(pause);
        }

        public static void TrackAdRevenue(
            string source,
            double revenue,
            string currency,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            Plugin?.TrackAdRevenue(source, revenue, currency, extraPayload);
        }

        public static void TrackPurchase(
            string orderId,
            double amount,
            string currency,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            Plugin?.TrackPurchase(orderId, amount, currency, extraPayload);
        }

        public static void TrackCustomEvent(
            string name,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            Plugin?.TrackCustomEvent(name, extraPayload);
        }

        public static void PurchaseItem(
            string productId
        )
        {
            Debug.Log("=========================Noctua.PurchaseItem");
            GoogleBilling?.PurchaseItem(productId);
        }

        public static void GetProductList()
        {
            Debug.Log("=========================Noctua.GetProductList");
            GoogleBilling?.GetProductList();
        }

        private static INativePlugin GetNativePlugin()
        {
#if UNITY_ANDROID
                Debug.Log("Plugin is NoctuaAndroidPlugin");
                return new AndroidPlugin();
#elif UNITY_IOS
                Debug.Log("Plugin is NoctuaIPhonePlugin");
                return new IosPlugin();
#else
            Debug.Log("Plugin is null");
            return null;
#endif
        }
    }
}