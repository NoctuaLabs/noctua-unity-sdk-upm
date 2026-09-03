using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace com.noctuagames.sdk.Campaign
{
    /// <summary>
    /// Resolves a <c>Resources</c> path to a UI Toolkit <see cref="FontAsset"/>. The path comes
    /// from a campaign's own font registry (<see cref="CampaignItem.Fonts"/>) or straight from
    /// <c>style.fontFamily</c> when it is written as a path. The path may point at either a TextCore
    /// <see cref="FontAsset"/> the game bundles, or the raw <c>.ttf</c>/<c>.otf</c> (imported as
    /// <see cref="Font"/>) — in the latter case a dynamic <see cref="FontAsset"/> is built at
    /// runtime via <see cref="FontAsset.CreateFontAsset(Font)"/>. A <c>TMP_FontAsset</c> at the
    /// path does not count: it is a separate type and will not load as a <see cref="FontAsset"/>,
    /// so point the registry at that font's source <c>.ttf</c> instead. Only the <em>choice</em>
    /// of font per campaign is server-driven; the file itself must ship in the build. A missing
    /// file resolves to <c>null</c> and the text keeps the panel's default typeface — a custom
    /// font never blocks a campaign. Results are cached by path.
    /// </summary>
    public sealed class CampaignFontSource : ICampaignFontSource
    {
        private readonly Dictionary<string, FontAsset> _byPath = new Dictionary<string, FontAsset>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _missPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly ILogger _log;

        private const string LogTag = "[campaign_fonts]";

        public CampaignFontSource(ILogger log = null)
        {
            _log = log ?? new NoctuaLogger(typeof(CampaignFontSource));
        }

        /// <inheritdoc />
        public void GetFont(string resourcesPath, Action<FontAsset> onLoaded)
        {
            onLoaded?.Invoke(Resolve(resourcesPath));
        }

        /// <summary>True when <paramref name="resourcesPath"/> resolves to a bundled Font Asset.</summary>
        public bool IsCached(string resourcesPath) => Resolve(resourcesPath) != null;

        /// <summary>
        /// Warms the cache for every font path referenced by the payload — each campaign's own
        /// <see cref="CampaignItem.Fonts"/> registry. Cheap: a synchronous Resources load each.
        /// </summary>
        public void Preload(CampaignConfig config)
        {
            if (config?.Campaigns == null) return;

            foreach (var item in config.Campaigns)
            {
                if (item?.Fonts == null) continue;
                foreach (var path in item.Fonts.Values) Resolve(path);
            }
        }

        private FontAsset Resolve(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            path = path.Trim();

            if (_byPath.TryGetValue(path, out var cached)) return cached;
            if (_missPaths.Contains(path)) return null;

            FontAsset fa = null;
            try { fa = Resources.Load<FontAsset>(path); }
            catch (Exception e) { _log.Warning($"{LogTag} load error for '{path}': {e.Message}"); }

            // Fallback: the game may only bundle the raw .ttf/.otf (imported as UnityEngine.Font),
            // or a TMP_FontAsset — a different type — sits at that path. Build a dynamic UI Toolkit
            // FontAsset from the bundled Font at runtime.
            if (fa == null)
            {
                try
                {
                    var font = Resources.Load<Font>(path);
                    if (font != null)
                    {
                        fa = FontAsset.CreateFontAsset(font);
                        if (fa != null)
                        {
                            fa.name = font.name + " (campaign)";
                            _log.Info($"{LogTag} built dynamic FontAsset from Font '{path}'");
                        }
                    }
                }
                catch (Exception e) { _log.Warning($"{LogTag} dynamic build failed for '{path}': {e.Message}"); }
            }

            if (fa == null)
            {
                _log.Warning($"{LogTag} no font at Resources path '{path}' — point the registry at a " +
                             "TextCore FontAsset or the source .ttf/.otf (not a TMP_FontAsset)");
                _missPaths.Add(path);
                return null;
            }

            _byPath[path] = fa;
            return fa;
        }
    }
}
