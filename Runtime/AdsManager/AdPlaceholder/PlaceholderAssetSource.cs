using System;
using System.Collections;
using System.Collections.Generic;
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

        private readonly Dictionary<AdPlaceholderType, List<string>> resourcePools = new();
        private static readonly System.Random random = new();

        private void Awake()
        {
            LoadAllResources();
        }

        /// <summary>
        /// Loads all textures from Resources/NoctuaAdPlaceholder/{folder} into memory.
        /// </summary>
        private void LoadAllResources()
        {
            foreach (AdPlaceholderType type in Enum.GetValues(typeof(AdPlaceholderType)))
            {
                string folderName = GetFolderName(type);
                var textures = Resources.LoadAll<Texture2D>($"NoctuaAdPlaceholder/{folderName}");
                var paths = new List<string>();

                foreach (var tex in textures)
                {
                    string path = $"NoctuaAdPlaceholder/{folderName}/{tex.name}";
                    paths.Add(path);
                }

                resourcePools[type] = paths;

                Debug.Log($"[AdPlaceholder] Loaded {paths.Count} local assets for {type} in folder '{folderName}'");
            }
        }

        /// <summary>
        /// Public method to retrieve an ad placeholder asset by type.
        /// </summary>
        public void GetAdAssetResource(AdPlaceholderType adType, Action<Texture2D> callback)
        {
            StartCoroutine(LoadAdAsset(adType, callback));
        }

        /// <summary>
        /// Coroutine to load either a local or remote asset.
        /// </summary>
        private IEnumerator LoadAdAsset(AdPlaceholderType type, Action<Texture2D> callback)
        {
            if (resourcePools.TryGetValue(type, out var paths) && paths.Count > 0)
            {
                string randomPath = paths[random.Next(paths.Count)];
                Texture2D texture = Resources.Load<Texture2D>(randomPath);

                if (texture != null)
                {
                    callback?.Invoke(texture);
                    yield break;
                }

                Debug.LogWarning($"[AdPlaceholder] Local texture failed to load from {randomPath}, trying remote.");
            }
            else
            {
                Debug.LogWarning($"[AdPlaceholder] No local resources found for {type}, trying remote.");
            }

            // Remote fallback
            string remoteUrl = GetRemoteUrl(type);
            using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(remoteUrl))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    Texture2D remoteTexture = DownloadHandlerTexture.GetContent(www);
                    callback?.Invoke(remoteTexture);
                }
                else
                {
                    Debug.LogError($"[AdPlaceholder] Failed to load remote asset: {www.error}");
                    callback?.Invoke(null);
                }
            }
        }

        /// <summary>
        /// Converts AdPlaceholderType to folder name.
        /// </summary>
        private string GetFolderName(AdPlaceholderType type)
        {
            return type switch
            {
                AdPlaceholderType.Interstitial => "interstitial",
                AdPlaceholderType.Rewarded => "rewarded",
                AdPlaceholderType.RewardedInterstitial => "rewardedinterstitial",
                AdPlaceholderType.Banner => "banner",
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }

        /// <summary>
        /// Hardcoded remote URLs as fallback. You can replace these with actual CDN links.
        /// </summary>
        private string GetRemoteUrl(AdPlaceholderType type)
        {
            return type switch
            {
                AdPlaceholderType.Interstitial => "https://example.com/ads/InterstitialFallback.png",
                AdPlaceholderType.Rewarded => "https://example.com/ads/RewardedFallback.png",
                AdPlaceholderType.RewardedInterstitial => "https://example.com/ads/RewardedInterstitialFallback.png",
                AdPlaceholderType.Banner => "https://example.com/ads/BannerFallback.png",
                _ => null
            };
        }
    }
}
