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

        /// <summary>Default playback slot — the full-screen cross-promo placeholder.</summary>
        public const string FullScreenSlot = "fullscreen";

        /// <summary>Playback slot for the banner cross-promo surface — has its own <see cref="VideoPlayer"/>
        /// and <see cref="RenderTexture"/> so the banner and the full-screen placeholder can each play a
        /// video at the same time without one blanking the other.</summary>
        public const string BannerSlot = "banner";

        /// <summary>
        /// One independent playback slot: its own <see cref="VideoPlayer"/> + <see cref="RenderTexture"/>
        /// + <see cref="AudioSource"/>, its own in-flight load coroutine, and its own monotonic attempt
        /// id so a superseded prepare (self-heal, re-show) is ignored per surface.
        /// </summary>
        private sealed class PlaybackSlot
        {
            public VideoPlayer Player;
            public RenderTexture RenderTexture;
            public AudioSource Audio;
            public int Attempt;
            public Coroutine ActiveCoroutine;
        }

        // Playback slots keyed by slot id (FullScreenSlot / BannerSlot), created on first use.
        private readonly Dictionary<string, PlaybackSlot> _slots = new();

        private PlaybackSlot GetSlot(string id)
        {
            if (!_slots.TryGetValue(id, out var slot))
            {
                slot = new PlaybackSlot();
                _slots[id] = slot;
            }
            return slot;
        }

        // In-memory cache of decoded cross-promo images, keyed by source URL (shared — images are
        // immutable and safe to hand to any slot).
        private readonly Dictionary<string, Texture2D> _imageCache = new();

        // URLs with a download currently in flight — guards against concurrent/duplicate caching.
        private readonly HashSet<string> _caching = new();

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
        /// <param name="slotId">
        /// Playback slot — <see cref="FullScreenSlot"/> (default) or <see cref="BannerSlot"/>. Each slot
        /// owns an independent <see cref="VideoPlayer"/>/<see cref="RenderTexture"/> and in-flight load,
        /// so the banner and the full-screen placeholder never clobber each other.
        /// </param>
        public void GetAdAsset(string assetUrl, Action<CrossPromoAsset> callback, string slotId = FullScreenSlot)
        {
            if (string.IsNullOrEmpty(assetUrl))
            {
                _log.Warning($"{LogTag} GetAdAsset called with empty URL.");
                callback?.Invoke(null);
                return;
            }

            var slot = GetSlot(slotId);
            if (slot.ActiveCoroutine != null)
            {
                StopCoroutine(slot.ActiveCoroutine);
                slot.ActiveCoroutine = null;
            }

            string cachedFile = CacheFilePath(assetUrl);

            if (IsVideoUrl(assetUrl))
            {
                if (File.Exists(cachedFile))
                {
                    // Cached → play from local file (fast, offline-safe), self-healing if it's corrupt.
                    _log.Debug($"{LogTag} get_asset - video cache HIT (disk), playing local file [{slotId}]: {assetUrl}");
                    slot.ActiveCoroutine = StartCoroutine(PlayCachedVideo(assetUrl, cachedFile, callback, slot));
                }
                else
                {
                    // Not cached → decide by size (mediation only fetches small creatives on demand).
                    _log.Debug($"{LogTag} get_asset - video cache MISS [{slotId}]: {assetUrl}");
                    slot.ActiveCoroutine = StartCoroutine(LoadVideoOnMiss(assetUrl, callback, slot));
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

            slot.ActiveCoroutine = File.Exists(cachedFile)
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
        private IEnumerator LoadVideoOnMiss(string url, Action<CrossPromoAsset> callback, PlaybackSlot slot)
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
                LoadVideoAsset(url, callback, slot);
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
        private void LoadVideoAsset(string url, Action<CrossPromoAsset> callback, PlaybackSlot slot)
        {
            EnsureVideoPlayer(slot);

            // Each call supersedes the previous FOR THIS SLOT: a stale attempt's events are ignored.
            // Makes the slot's single VideoPlayer safe across re-attempts (e.g. self-heal → stream).
            int attempt = ++slot.Attempt;
            var player = slot.Player;

            player.Stop();
            player.source = VideoSource.Url;
            player.url = url;
            player.renderMode = VideoRenderMode.RenderTexture;

            _log.Debug($"{LogTag} prepare_video - preparing video (attempt {attempt}): {url}");

            void OnPrepared(VideoPlayer vp)
            {
                vp.prepareCompleted -= OnPrepared;
                vp.errorReceived -= OnError;
                if (attempt != slot.Attempt) return; // superseded — ignore

                ReleaseRenderTexture(slot);
                int width = Mathf.Max(1, (int)vp.width);
                int height = Mathf.Max(1, (int)vp.height);
                slot.RenderTexture = new RenderTexture(width, height, 0);
                vp.targetTexture = slot.RenderTexture;

                _log.Debug($"{LogTag} prepare_video - video prepared ({width}x{height}): {url}");

                callback?.Invoke(new CrossPromoAsset
                {
                    IsVideo = true,
                    Video = slot.RenderTexture,
                    Player = vp
                });
            }

            void OnError(VideoPlayer vp, string message)
            {
                vp.prepareCompleted -= OnPrepared;
                vp.errorReceived -= OnError;
                if (attempt != slot.Attempt) return; // superseded — ignore
                _log.Warning($"{LogTag} prepare_video - failed to prepare video from {url}: {message}");
                callback?.Invoke(null);
            }

            player.prepareCompleted += OnPrepared;
            player.errorReceived += OnError;
            player.Prepare();
        }

        /// <summary>
        /// Plays a cached video from local disk, self-healing if the cached file is corrupt: if it
        /// fails or does not prepare within <see cref="CachedVideoPrepareTimeoutSec"/>, the bad file is
        /// deleted and the original URL is streamed (and re-cached) instead.
        /// </summary>
        private IEnumerator PlayCachedVideo(string url, string file, Action<CrossPromoAsset> callback, PlaybackSlot slot)
        {
            bool finished = false;
            CrossPromoAsset result = null;

            LoadVideoAsset("file://" + file, asset => { finished = true; result = asset; }, slot);

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
            LoadVideoAsset(url, callback, slot); // new attempt id supersedes the stale cached attempt
            StartCoroutine(EnsureCached(url));
        }

        /// <summary>
        /// Lazily creates the slot's own <see cref="VideoPlayer"/> + <see cref="AudioSource"/> on this
        /// GameObject (multiple VideoPlayer components on one GameObject is supported).
        /// </summary>
        private void EnsureVideoPlayer(PlaybackSlot slot)
        {
            if (slot.Player != null) return;

            slot.Player = gameObject.AddComponent<VideoPlayer>();
            slot.Player.playOnAwake = false;
            slot.Player.isLooping = false;
            slot.Player.audioOutputMode = VideoAudioOutputMode.AudioSource;

            slot.Audio = gameObject.AddComponent<AudioSource>();
            slot.Audio.playOnAwake = false;
            slot.Player.SetTargetAudioSource(0, slot.Audio);
        }

        private void ReleaseRenderTexture(PlaybackSlot slot)
        {
            if (slot.RenderTexture == null) return;

            if (slot.Player != null && slot.Player.targetTexture == slot.RenderTexture)
            {
                slot.Player.targetTexture = null;
            }

            slot.RenderTexture.Release();
            Destroy(slot.RenderTexture);
            slot.RenderTexture = null;
        }

        /// <summary>
        /// Stops video playback for a slot and releases its render texture. Call when that placeholder
        /// surface closes. Defaults to the full-screen slot.
        /// </summary>
        /// <param name="slotId"><see cref="FullScreenSlot"/> (default) or <see cref="BannerSlot"/>.</param>
        public void StopVideo(string slotId = FullScreenSlot)
        {
            if (!_slots.TryGetValue(slotId, out var slot)) return;

            // Invalidate any in-flight prepare for this slot so a late callback after close is ignored.
            slot.Attempt++;

            if (slot.Player != null)
            {
                slot.Player.Stop();
            }
            ReleaseRenderTexture(slot);
        }

        private void OnDestroy()
        {
            foreach (var slot in _slots.Values) ReleaseRenderTexture(slot);
        }
    }
}
