using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace com.noctuagames.sdk.AdPlaceholder
{
    public enum AdPlaceholderType
    {
        Interstitial,
        Rewarded,
        RewardedInterstitial,
        Banner
    }

    public class PlaceholderAssetSource : MonoBehaviour
    {
        private static PlaceholderAssetSource _instance;
        public static PlaceholderAssetSource Instance
        {
            get
            {
                if (_instance == null)
                {
                    var obj = new GameObject("PlaceholderAssetSource");
                    _instance = obj.AddComponent<PlaceholderAssetSource>();
                    DontDestroyOnLoad(obj);
                }
                return _instance;
            }
        }

        public void GetAdAssetResource(AdPlaceholderType adType, Action<Texture2D> callback)
        {
            StartCoroutine(LoadAdAsset(adType, callback));
        }

        private IEnumerator LoadAdAsset(AdPlaceholderType type, Action<Texture2D> callback)
        {
            string resourcePath = GetResourcePath(type);
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);

            if (texture != null)
            {
                callback?.Invoke(texture);
                yield break;
            }

            Debug.LogWarning($"Local resource not found at Resources/{resourcePath}. Falling back to remote.");

            // Load from remote if local not found
            string remoteUrl = GetRemoteUrl(type);
            using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(remoteUrl))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    texture = DownloadHandlerTexture.GetContent(www);
                }
                else
                {
                    Debug.LogError($"Failed to load remote asset: {www.error}");
                }
            }

            callback?.Invoke(texture);
        }

        private string GetResourcePath(AdPlaceholderType type)
        {
            return type switch
            {
                AdPlaceholderType.Interstitial => "NoctuaAdPlaceholder/InterstitialAd",
                AdPlaceholderType.Rewarded => "NoctuaAdPlaceholder/RewardedAd",
                AdPlaceholderType.RewardedInterstitial => "NoctuaAdPlaceholder/RewardedInterstitialAd",
                AdPlaceholderType.Banner => "NoctuaAdPlaceholder/BannerAd",
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }

        private string GetRemoteUrl(AdPlaceholderType type)
        {
            return type switch
            {
                AdPlaceholderType.Interstitial => "https://example.com/ads/InterstitialAd.png",
                AdPlaceholderType.Rewarded => "https://example.com/ads/RewardedAd.png",
                AdPlaceholderType.RewardedInterstitial => "https://example.com/ads/RewardedInterstitialAd.png",
                AdPlaceholderType.Banner => "https://example.com/ads/BannerAd.png",
                _ => null
            };
        }
    }
}
