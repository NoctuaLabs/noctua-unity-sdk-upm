using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Video;

namespace com.noctuagames.sdk.AdPlaceholder
{
    /// <summary>
    /// Result of a cross-promotion asset load. Exactly one of <see cref="Image"/> or
    /// <see cref="Video"/> is populated depending on <see cref="IsVideo"/>. A null result
    /// means the load failed and the caller should close the placeholder (no static fallback).
    /// </summary>
    public class CrossPromoAsset
    {
        /// <summary>True when the loaded asset is a video; false when it is an image.</summary>
        public bool IsVideo;

        /// <summary>The loaded image texture (null for videos).</summary>
        public Texture2D Image;

        /// <summary>The render texture the video draws into (null for images). Not yet playing — the caller calls <see cref="Player"/>.Play().</summary>
        public RenderTexture Video;

        /// <summary>The prepared video player (null for images). The caller owns playback (Play/Stop).</summary>
        public VideoPlayer Player;
    }

    /// <summary>
    /// Defines the types of ad placeholders that can be displayed when real ads are unavailable.
    /// </summary>
    public enum AdPlaceholderType
    {
        /// <summary>Full-screen interstitial ad placeholder.</summary>
        Interstitial,

        /// <summary>Rewarded video ad placeholder.</summary>
        Rewarded,

        /// <summary>Rewarded interstitial ad placeholder.</summary>
        RewardedInterstitial,

        /// <summary>Banner ad placeholder.</summary>
        Banner
    }

    /// <summary>
    /// Singleton MonoBehaviour that loads and caches cross-promotion placeholder assets (image or
    /// video) from a CDN, with an on-disk + in-memory cache and preload support. There is no bundled
    /// static-image fallback — when no cross-promotion asset is available, no placeholder is shown.
    /// </summary>
    public class PlaceholderAssetSource : MonoBehaviour
    {
        private static PlaceholderAssetSource _instance;
        private readonly NoctuaLogger _log = new(typeof(PlaceholderAssetSource));

        /// <summary>Structured log tag for all cross-promotion / placeholder asset logs.</summary>
        private const string LogTag = "[cross_promo]";

        /// <summary>
        /// Gets the singleton instance, creating a persistent GameObject if one does not already exist.
        /// </summary>
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

        private Coroutine _activeCrossPromoCoroutine;
        private VideoPlayer _videoPlayer;
        private RenderTexture _videoRenderTexture;

        // In-memory cache of decoded cross-promo images, keyed by source URL.
        private readonly Dictionary<string, Texture2D> _imageCache = new();

        // URLs with a download currently in flight — guards against concurrent/duplicate caching.
        private readonly HashSet<string> _caching = new();

        // Monotonic id so a superseded video-prepare attempt (e.g. after self-heal) is ignored.
        private int _videoAttempt;

        /// <summary>Max seconds to wait for a cached video to prepare before treating it as corrupt.</summary>
        private const float CachedVideoPrepareTimeoutSec = 6f;

        /// <summary>Root folder for the on-disk cross-promotion asset cache.</summary>
        private static string CacheRoot => Path.Combine(Application.persistentDataPath, "noctua_crosspromo");

        /// <summary>
        /// Preloads cross-promotion assets for all configured formats into the disk (and, for images,
        /// in-memory) cache so a later <see cref="GetAdAsset"/> can render instantly — mirroring the
        /// load-then-show pattern of mediation ads. Safe to call repeatedly; already-cached assets are skipped.
        /// </summary>
        public void Preload(CrossPromotionConfig config)
        {
            _log.Debug($"{LogTag} preload - warm cross-promotion asset cache");

            if (config == null)
            {
                _log.Debug($"{LogTag} preload - no cross-promotion config, nothing to cache");
                return;
            }

            int count = 0;
            var keepFiles = new HashSet<string>();
            foreach (var entry in new[] { config.Interstitial, config.Rewarded, config.RewardedInterstitial, config.Banner })
            {
                var url = entry?.AssetUrl;
                if (string.IsNullOrEmpty(url)) continue;
                count++;
                keepFiles.Add(Path.GetFileName(CacheFilePath(url))); // keep current asset even if not precaching now

                // Videos can be large; only precache them on an unmetered (Wi-Fi/ethernet) connection so
                // we never burn cellular data in the background (mirrors mediation SDKs' Wi-Fi precache).
                // Images are tiny — precache on any connection.
                if (IsVideoUrl(url) && !IsUnmetered())
                {
                    _log.Debug($"{LogTag} preload - deferring video precache until on Wi-Fi: {url}");
                    continue;
                }

                StartCoroutine(EnsureCached(url));
            }

            // Keep only the currently-configured assets on disk. When the remote config changes a URL,
            // the new asset downloads (above) and the now-stale one is removed here — so we cache once
            // and only re-fetch when the remote data differs from what's cached.
            PruneCache(keepFiles);

            _log.Debug($"{LogTag} preload - queued {count} cross-promotion asset(s) for caching");
        }

        /// <summary>
        /// Deletes cached files that are not in <paramref name="keepFiles"/> (current config assets).
        /// In-flight temp downloads (.tmp) are left untouched.
        /// </summary>
        private void PruneCache(HashSet<string> keepFiles)
        {
            try
            {
                if (!Directory.Exists(CacheRoot)) return;

                foreach (var path in Directory.GetFiles(CacheRoot))
                {
                    var name = Path.GetFileName(path);
                    if (name.EndsWith(".tmp")) continue;          // a download in progress
                    if (keepFiles.Contains(name)) continue;       // still the current asset

                    try
                    {
                        File.Delete(path);
                        _log.Debug($"{LogTag} prune - removed stale cached asset: {name}");
                    }
                    catch (Exception e)
                    {
                        _log.Warning($"{LogTag} prune - failed to remove {name}: {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                _log.Warning($"{LogTag} prune - failed to enumerate cache: {e.Message}");
            }
        }

        /// <summary>
        /// Loads a cross-promotion asset (image or video) and invokes the callback with the result, or
        /// null if loading fails. Resolution order: in-memory cache → on-disk cache → network (which is
        /// also written to the cache for next time). The asset type is detected by the URL's file extension.
        /// </summary>
        /// <param name="assetUrl">The CDN URL of the asset to load.</param>
        /// <param name="callback">Callback invoked with the loaded asset, or null on failure.</param>
        public void GetAdAsset(string assetUrl, Action<CrossPromoAsset> callback)
        {
            if (string.IsNullOrEmpty(assetUrl))
            {
                _log.Warning($"{LogTag} GetAdAsset called with empty URL.");
                callback?.Invoke(null);
                return;
            }

            if (_activeCrossPromoCoroutine != null)
            {
                StopCoroutine(_activeCrossPromoCoroutine);
                _activeCrossPromoCoroutine = null;
            }

            string cachedFile = CacheFilePath(assetUrl);

            if (IsVideoUrl(assetUrl))
            {
                if (File.Exists(cachedFile))
                {
                    // Cached → play from local file (fast, offline-safe), self-healing if it's corrupt.
                    _log.Debug($"{LogTag} get_asset - video cache HIT (disk), playing local file: {assetUrl}");
                    _activeCrossPromoCoroutine = StartCoroutine(PlayCachedVideo(assetUrl, cachedFile, callback));
                }
                else
                {
                    // Not cached → decide by size (mediation only fetches small creatives on demand).
                    _log.Debug($"{LogTag} get_asset - video cache MISS: {assetUrl}");
                    _activeCrossPromoCoroutine = StartCoroutine(LoadVideoOnMiss(assetUrl, callback));
                }
                return;
            }

            // Image: in-memory cache first.
            if (_imageCache.TryGetValue(assetUrl, out var cachedTex) && cachedTex != null)
            {
                _log.Debug($"{LogTag} get_asset - image cache HIT (memory): {assetUrl}");
                callback?.Invoke(new CrossPromoAsset { IsVideo = false, Image = cachedTex });
                return;
            }

            if (File.Exists(cachedFile))
                _log.Debug($"{LogTag} get_asset - image cache HIT (disk): {assetUrl}");
            else
                _log.Debug($"{LogTag} get_asset - image cache MISS, downloading: {assetUrl}");

            _activeCrossPromoCoroutine = File.Exists(cachedFile)
                ? StartCoroutine(LoadImageFromDisk(assetUrl, cachedFile, callback))
                : StartCoroutine(LoadImageFromNetwork(assetUrl, callback));
        }

        /// <summary>
        /// Returns true when the asset for <paramref name="url"/> can be shown from a local cache with
        /// no network fetch — an image already in the in-memory cache, or any asset (image or video)
        /// already on disk. Used by readiness checks so a cross-promotion is only reported "ready"
        /// when its creative is actually cached, not merely configured (a configured-but-uncached
        /// asset would flash a blank placeholder while it downloads, or fail outright when offline).
        /// </summary>
        public bool IsCached(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;

            // Image: served instantly from the in-memory cache.
            if (!IsVideoUrl(url) && _imageCache.TryGetValue(url, out var tex) && tex != null)
                return true;

            // Image or video: a file already on disk plays without a network fetch.
            return File.Exists(CacheFilePath(url));
        }

        /// <summary>
        /// Max video size we'll fetch on demand at show time (8 MB — generous vs. AppLovin's ~2-4 MB
        /// creative guidance). Anything larger must be precached on Wi-Fi beforehand; if it isn't ready
        /// it reports not-available, exactly like a mediation video that wasn't preloaded.
        /// </summary>
        private const long VideoStreamSizeLimitBytes = 8L * 1024 * 1024;

        /// <summary>True on an unmetered connection (Wi-Fi / ethernet) — safe for large background downloads.</summary>
        private static bool IsUnmetered() =>
            Application.internetReachability == NetworkReachability.ReachableViaLocalAreaNetwork;

        /// <summary>
        /// Handles an uncached video at show time. Mediation only fetches small creatives on demand, so
        /// we HEAD the URL: if it's within <see cref="VideoStreamSizeLimitBytes"/> we stream it (and cache
        /// for next time); if it's larger or its size is unknown we DON'T stream — we report not-ready
        /// (callback null) and precache it in the background (Wi-Fi only) so a later attempt is instant.
        /// </summary>
        private IEnumerator LoadVideoOnMiss(string url, Action<CrossPromoAsset> callback)
        {
            long size = -1;
            using (UnityWebRequest head = UnityWebRequest.Head(url))
            {
                yield return head.SendWebRequest();
                if (head.result == UnityWebRequest.Result.Success)
                    long.TryParse(head.GetResponseHeader("Content-Length"), out size);
            }

            if (size > 0 && size <= VideoStreamSizeLimitBytes)
            {
                _log.Debug($"{LogTag} get_asset - video {size / 1024}KB within stream limit, streaming + caching: {url}");
                LoadVideoAsset(url, callback);
                StartCoroutine(EnsureCached(url));
            }
            else
            {
                _log.Warning($"{LogTag} get_asset - video too large/unknown to stream on demand (size={size} bytes); " +
                             $"reporting not-ready and precaching in background: {url}");
                callback?.Invoke(null); // -> UI reports failed -> MediationManager fires OnAdNotAvailable
                if (IsUnmetered()) StartCoroutine(EnsureCached(url));
            }
        }

        /// <summary>
        /// Returns true when the URL points to a video by file extension (query string ignored).
        /// </summary>
        private static bool IsVideoUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;

            string path = url.Split('?', '#')[0].ToLowerInvariant();
            return path.EndsWith(".mp4") || path.EndsWith(".webm") || path.EndsWith(".ogv")
                || path.EndsWith(".mov") || path.EndsWith(".m4v");
        }

        /// <summary>Stable on-disk cache path for a URL (MD5 of the URL + original extension).</summary>
        private static string CacheFilePath(string url)
        {
            string noQuery = url.Split('?', '#')[0];
            int dot = noQuery.LastIndexOf('.');
            string ext = dot >= 0 ? noQuery.Substring(dot) : string.Empty;

            using var md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(url));
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));

            return Path.Combine(CacheRoot, sb.ToString() + ext);
        }

        /// <summary>
        /// Downloads the asset to the on-disk cache if not already present. For images, also warms the
        /// in-memory cache so the next show is instant.
        /// </summary>
        private IEnumerator EnsureCached(string url)
        {
            string file = CacheFilePath(url);

            if (File.Exists(file))
            {
                if (!IsVideoUrl(url) && !_imageCache.ContainsKey(url))
                    yield return LoadImageFromDisk(url, file, null);
                yield break;
            }

            // Dedupe concurrent/duplicate downloads of the same URL (e.g. the same asset used for
            // multiple formats, or Preload running for both local + remote config). Without this,
            // multiple writers race on the same file and produce a corrupted asset.
            if (_caching.Contains(url))
            {
                _log.Debug($"{LogTag} ensure_cached - already downloading, skipping duplicate: {url}");
                yield break;
            }
            _caching.Add(url);

            try { Directory.CreateDirectory(CacheRoot); }
            catch (Exception e)
            {
                _log.Warning($"{LogTag} ensure_cached - cannot create cache dir: {e.Message}");
                _caching.Remove(url);
                yield break;
            }

            // Unique temp file per download so two attempts can never write the same path.
            string temp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            bool downloaded = false;

            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                www.downloadHandler = new DownloadHandlerFile(temp) { removeFileOnAbort = true };
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    downloaded = true;
                }
                else
                {
                    _log.Warning($"{LogTag} ensure_cached - failed to download {url}: {www.error}");
                    TryDelete(temp);
                }
            }

            if (downloaded)
            {
                try
                {
                    if (File.Exists(file)) File.Delete(file);
                    File.Move(temp, file);
                    _log.Debug($"{LogTag} ensure_cached - cached asset: {url}");
                }
                catch (Exception e)
                {
                    _log.Warning($"{LogTag} ensure_cached - failed to finalize cache for {url}: {e.Message}");
                    TryDelete(temp);
                    downloaded = false;
                }
            }

            _caching.Remove(url);

            if (downloaded && !IsVideoUrl(url) && !_imageCache.ContainsKey(url))
                yield return LoadImageFromDisk(url, file, null);
        }

        /// <summary>Loads an image from the local cache file into a texture (and the in-memory cache).</summary>
        private IEnumerator LoadImageFromDisk(string url, string file, Action<CrossPromoAsset> callback)
        {
            using (UnityWebRequest www = UnityWebRequestTexture.GetTexture("file://" + file))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    Texture2D texture = DownloadHandlerTexture.GetContent(www);
                    _imageCache[url] = texture;
                    callback?.Invoke(new CrossPromoAsset { IsVideo = false, Image = texture });
                }
                else
                {
                    _log.Warning($"{LogTag} failed to load cached image {file}: {www.error}");
                    callback?.Invoke(null);
                }
            }
        }

        /// <summary>
        /// Downloads a remote image, returns it, and persists it to the cache (memory + disk).
        /// </summary>
        private IEnumerator LoadImageFromNetwork(string url, Action<CrossPromoAsset> callback)
        {
            using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    Texture2D texture = DownloadHandlerTexture.GetContent(www);
                    _imageCache[url] = texture;
                    TryWriteCache(url, www.downloadHandler.data);
                    callback?.Invoke(new CrossPromoAsset { IsVideo = false, Image = texture });
                }
                else
                {
                    _log.Warning($"{LogTag} Failed to load cross-promo image from {url}: {www.error}");
                    callback?.Invoke(null);
                }
            }
        }

        private void TryWriteCache(string url, byte[] data)
        {
            if (data == null || data.Length == 0) return;
            try
            {
                Directory.CreateDirectory(CacheRoot);
                File.WriteAllBytes(CacheFilePath(url), data);
            }
            catch (Exception e)
            {
                _log.Warning($"{LogTag} cache write failed for {url}: {e.Message}");
            }
        }

        private void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* best-effort cleanup */ }
        }

        /// <summary>
        /// Prepares a streaming video from the URL and returns a render texture + player. The caller
        /// owns playback. Invokes the callback with null if preparation fails.
        /// </summary>
        private void LoadVideoAsset(string url, Action<CrossPromoAsset> callback)
        {
            EnsureVideoPlayer();

            // Each call supersedes the previous: a stale attempt's events are ignored. This makes the
            // shared single VideoPlayer safe across re-attempts (e.g. self-heal falling back to stream).
            int attempt = ++_videoAttempt;

            _videoPlayer.Stop();
            _videoPlayer.source = VideoSource.Url;
            _videoPlayer.url = url;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;

            _log.Debug($"{LogTag} prepare_video - preparing video (attempt {attempt}): {url}");

            void OnPrepared(VideoPlayer vp)
            {
                vp.prepareCompleted -= OnPrepared;
                vp.errorReceived -= OnError;
                if (attempt != _videoAttempt) return; // superseded — ignore

                ReleaseRenderTexture();
                int width = Mathf.Max(1, (int)vp.width);
                int height = Mathf.Max(1, (int)vp.height);
                _videoRenderTexture = new RenderTexture(width, height, 0);
                vp.targetTexture = _videoRenderTexture;

                _log.Debug($"{LogTag} prepare_video - video prepared ({width}x{height}): {url}");

                callback?.Invoke(new CrossPromoAsset
                {
                    IsVideo = true,
                    Video = _videoRenderTexture,
                    Player = vp
                });
            }

            void OnError(VideoPlayer vp, string message)
            {
                vp.prepareCompleted -= OnPrepared;
                vp.errorReceived -= OnError;
                if (attempt != _videoAttempt) return; // superseded — ignore
                _log.Warning($"{LogTag} prepare_video - failed to prepare video from {url}: {message}");
                callback?.Invoke(null);
            }

            _videoPlayer.prepareCompleted += OnPrepared;
            _videoPlayer.errorReceived += OnError;
            _videoPlayer.Prepare();
        }

        /// <summary>
        /// Plays a cached video from local disk, self-healing if the cached file is corrupt: if it
        /// fails or does not prepare within <see cref="CachedVideoPrepareTimeoutSec"/>, the bad file is
        /// deleted and the original URL is streamed (and re-cached) instead.
        /// </summary>
        private IEnumerator PlayCachedVideo(string url, string file, Action<CrossPromoAsset> callback)
        {
            bool finished = false;
            CrossPromoAsset result = null;

            LoadVideoAsset("file://" + file, asset => { finished = true; result = asset; });

            float elapsed = 0f;
            while (!finished && elapsed < CachedVideoPrepareTimeoutSec)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (finished && result != null)
            {
                callback?.Invoke(result);
                yield break;
            }

            // Corrupt or hung cached file — invalidate and stream the original (which re-caches).
            _log.Warning($"{LogTag} play_cached_video - cached video unplayable (timeout/error), invalidating + streaming: {url}");
            TryDelete(file);
            LoadVideoAsset(url, callback); // new attempt id supersedes the stale cached attempt
            StartCoroutine(EnsureCached(url));
        }

        /// <summary>
        /// Lazily creates the shared <see cref="VideoPlayer"/> + <see cref="AudioSource"/> on this GameObject.
        /// </summary>
        private void EnsureVideoPlayer()
        {
            if (_videoPlayer != null) return;

            _videoPlayer = gameObject.AddComponent<VideoPlayer>();
            _videoPlayer.playOnAwake = false;
            _videoPlayer.isLooping = false;
            _videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;

            var audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            _videoPlayer.SetTargetAudioSource(0, audioSource);
        }

        private void ReleaseRenderTexture()
        {
            if (_videoRenderTexture == null) return;

            if (_videoPlayer != null && _videoPlayer.targetTexture == _videoRenderTexture)
            {
                _videoPlayer.targetTexture = null;
            }

            _videoRenderTexture.Release();
            Destroy(_videoRenderTexture);
            _videoRenderTexture = null;
        }

        /// <summary>
        /// Stops video playback and releases the render texture. Call when the placeholder closes.
        /// </summary>
        public void StopVideo()
        {
            // Invalidate any in-flight prepare so a late callback after close is ignored.
            _videoAttempt++;

            if (_videoPlayer != null)
            {
                _videoPlayer.Stop();
            }
            ReleaseRenderTexture();
        }

        private void OnDestroy()
        {
            ReleaseRenderTexture();
        }
    }
}
