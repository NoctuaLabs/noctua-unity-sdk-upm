using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// Campaign's own remote-image pipeline — in-memory + on-disk cache, images only in v1
    /// (no video / RenderTexture). Independent of the IAA <c>PlaceholderAssetSource</c>.
    /// Plain injectable class: uses UniTask for the web request, so it needs no
    /// <c>MonoBehaviour</c> host. A node whose image can't load renders empty (no fallback).
    /// </summary>
    public sealed class CampaignAssetSource : ICampaignImageSource
    {
        private static string CacheRoot => Path.Combine(Application.persistentDataPath, "noctua_campaign");
        private const long MaxCacheBytes = 32L * 1024 * 1024;

        /// <summary>
        /// Cap on decoded textures kept in RAM. Texture2D is an unmanaged Unity object, so the
        /// dictionary alone would leak GPU memory across a long session — evict oldest on overflow.
        /// Sized well above what a popup shows at once; an evicted image re-loads from the
        /// on-disk cache on next show.
        /// </summary>
        private const int MaxMemoryTextures = 48;

        private readonly Dictionary<string, Texture2D> _memory = new Dictionary<string, Texture2D>();
        private readonly List<string> _memoryOrder = new List<string>();
        private readonly HashSet<string> _inFlight = new HashSet<string>();
        private readonly Dictionary<string, int> _pins = new Dictionary<string, int>();
        private readonly Dictionary<string, List<Action<Texture2D>>> _waiters =
            new Dictionary<string, List<Action<Texture2D>>>();
        private readonly ILogger _log;
        private readonly Func<bool> _isOffline;

        private const string LogTag = "[campaign_assets]";

        public CampaignAssetSource(ILogger log = null, Func<bool> isOffline = null)
        {
            _log = log ?? new NoctuaLogger(typeof(CampaignAssetSource));
            _isOffline = isOffline ?? (() => false);
        }

        /// <inheritdoc />
        public void GetImage(string url, Action<Texture2D> onLoaded)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                onLoaded?.Invoke(null);
                return;
            }

            if (_memory.TryGetValue(url, out var cached) && cached != null)
            {
                _memoryOrder.Remove(url); // freshen — least-recently-used is evicted first
                _memoryOrder.Add(url);
                onLoaded?.Invoke(cached);
                return;
            }

            if (onLoaded != null)
            {
                if (!_waiters.TryGetValue(url, out var list))
                {
                    list = new List<Action<Texture2D>>();
                    _waiters[url] = list;
                }
                list.Add(onLoaded);
            }

            if (_inFlight.Contains(url)) return;
            _inFlight.Add(url);
            LoadAsync(url).Forget();
        }

        /// <inheritdoc />
        public void Pin(IReadOnlyCollection<string> urls)
        {
            if (urls == null) return;
            foreach (var u in urls)
            {
                if (string.IsNullOrEmpty(u)) continue;
                _pins[u] = (_pins.TryGetValue(u, out var n) ? n : 0) + 1;
            }
        }

        /// <inheritdoc />
        public void Unpin(IReadOnlyCollection<string> urls)
        {
            if (urls == null) return;
            foreach (var u in urls)
            {
                if (string.IsNullOrEmpty(u) || !_pins.TryGetValue(u, out var n)) continue;
                if (n <= 1) _pins.Remove(u);
                else _pins[u] = n - 1;
            }
        }

        /// <summary>True when <paramref name="url"/> is already in RAM or on disk (no fetch needed).</summary>
        public bool IsCached(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (_memory.TryGetValue(url, out var t) && t != null) return true;
            try { return File.Exists(CacheFilePath(url)); }
            catch { return false; }
        }

        /// <summary>True when every <c>image</c> node URL in <paramref name="item"/>'s view is cached (or it has none).</summary>
        public bool AreAllImagesCached(CampaignItem item)
        {
            if (item?.View == null) return true;

            var allCached = true;
            CollectImageUrls(item.View, item.Data, url =>
            {
                if (!IsCached(url)) allCached = false;
            });
            return allCached;
        }

        /// <summary>Warms the cache for every <c>image</c> node URL in <paramref name="config"/>.</summary>
        public void Preload(CampaignConfig config)
        {
            if (config?.Campaigns == null) return;

            foreach (var item in config.Campaigns)
            {
                if (item?.View == null) continue;
                CollectImageUrls(item.View, item.Data, url => GetImage(url, null));
            }
        }

        private static void CollectImageUrls(CampaignNode node, IReadOnlyDictionary<string, string> data, Action<string> sink)
        {
            if (node == null) return;

            if (string.Equals(node.Type, CampaignNode.TypeImage, StringComparison.OrdinalIgnoreCase))
            {
                var url = CampaignTokens.Resolve(node.PropString("url"), data);
                if (!string.IsNullOrWhiteSpace(url)) sink(url);
            }

            if (node.Children == null) return;
            foreach (var child in node.Children) CollectImageUrls(child, data, sink);
        }

        private async UniTaskVoid LoadAsync(string url)
        {
            Texture2D result = null;
            try
            {
                var file = CacheFilePath(url);

                if (File.Exists(file))
                {
                    var bytes = File.ReadAllBytes(file);
                    var tex = new Texture2D(2, 2);
                    if (tex.LoadImage(bytes)) result = tex;
                }

                if (result == null && _isOffline())
                {
                    // Offline: don't sit on a ~30s connection timeout per image. The node renders
                    // empty; a later show after reconnect re-fetches (URL is neither cached nor in-flight).
                    _log.Debug($"{LogTag} offline — skipping network fetch for {url}");
                }
                else if (result == null)
                {
                    using var req = UnityWebRequestTexture.GetTexture(url);
                    await req.SendWebRequest().ToUniTask();

                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        result = DownloadHandlerTexture.GetContent(req);
                        TryWriteCache(file, req.downloadHandler.data);
                    }
                    else
                    {
                        _log.Warning($"{LogTag} fetch failed for {url}: {req.error}");
                    }
                }
            }
            catch (Exception e)
            {
                _log.Warning($"{LogTag} load error for {url}: {e.Message}");
            }
            finally
            {
                if (result != null) Remember(url, result);
                _inFlight.Remove(url);
                Flush(url, result);
            }
        }

        private void Remember(string url, Texture2D tex)
        {
            if (_memory.ContainsKey(url))
            {
                _memory[url] = tex;
                _memoryOrder.Remove(url);
                _memoryOrder.Add(url);
                return;
            }

            _memory[url] = tex;
            _memoryOrder.Add(url);

            while (_memoryOrder.Count > MaxMemoryTextures)
            {
                // Evict the least-recently-used URL that isn't pinned by an open surface.
                var idx = _memoryOrder.FindIndex(u => !_pins.ContainsKey(u));
                if (idx < 0) break; // everything cached is currently on screen — leave it

                var oldest = _memoryOrder[idx];
                _memoryOrder.RemoveAt(idx);
                if (_memory.TryGetValue(oldest, out var old))
                {
                    _memory.Remove(oldest);
                    if (old != null) UnityEngine.Object.Destroy(old);
                }
            }
        }

        private void Flush(string url, Texture2D tex)
        {
            if (!_waiters.TryGetValue(url, out var list)) return;
            _waiters.Remove(url);
            foreach (var cb in list)
            {
                try { cb(tex); } catch (Exception e) { _log.Warning($"{LogTag} callback threw: {e.Message}"); }
            }
        }

        private void TryWriteCache(string file, byte[] data)
        {
            if (data == null || data.Length == 0) return;
            try
            {
                Directory.CreateDirectory(CacheRoot);
                File.WriteAllBytes(file, data);
                PruneCache();
            }
            catch (Exception e)
            {
                _log.Warning($"{LogTag} cache write failed: {e.Message}");
            }
        }

        private static void PruneCache()
        {
            if (!Directory.Exists(CacheRoot)) return;

            var files = new List<FileInfo>();
            long total = 0;
            foreach (var path in Directory.GetFiles(CacheRoot))
            {
                var fi = new FileInfo(path);
                files.Add(fi);
                total += fi.Length;
            }
            if (total <= MaxCacheBytes) return;

            files.Sort((a, b) => a.LastAccessTimeUtc.CompareTo(b.LastAccessTimeUtc));
            foreach (var fi in files)
            {
                if (total <= MaxCacheBytes) break;
                try { total -= fi.Length; fi.Delete(); } catch { /* ignore */ }
            }
        }

        private static string CacheFilePath(string url)
        {
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(url));
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));

            var ext = Path.GetExtension(new Uri(url, UriKind.RelativeOrAbsolute).IsAbsoluteUri
                ? new Uri(url).AbsolutePath
                : url);
            if (string.IsNullOrEmpty(ext) || ext.Length > 5) ext = ".img";

            return Path.Combine(CacheRoot, sb + ext);
        }
    }
}
