using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace com.noctuagames.sdk.Campaign
{
    /// <summary>
    /// Resolves a campaign <c>style.fontFamily</c> key to a UI Toolkit <see cref="FontAsset"/>.
    /// The <see cref="CampaignConfig.Fonts"/> registry maps a server-chosen family name to a
    /// <c>Resources</c> path of a Font Asset the game ships in its build (Unity 6.3 has no
    /// runtime API to build a <see cref="FontAsset"/> from a downloaded TTF/OTF, so the font
    /// file must be bundled — only the <em>choice</em> of font per campaign is server-driven).
    /// An unknown key or a missing asset resolves to <c>null</c> and the text keeps the panel's
    /// default typeface — a custom font never blocks a campaign.
    /// </summary>
    public sealed class CampaignFontSource : ICampaignFontSource
    {
        private readonly Dictionary<string, string> _registry;
        private readonly Dictionary<string, FontAsset> _byFamily = new Dictionary<string, FontAsset>();
        private readonly HashSet<string> _misses = new HashSet<string>();
        private readonly ILogger _log;

        private const string LogTag = "[campaign_fonts]";

        public CampaignFontSource(IReadOnlyDictionary<string, string> registry, ILogger log = null)
        {
            _registry = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (registry != null)
            {
                foreach (var kv in registry)
                {
                    if (!string.IsNullOrWhiteSpace(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
                        _registry[kv.Key.Trim()] = kv.Value.Trim();
                }
            }
            _log = log ?? new NoctuaLogger(typeof(CampaignFontSource));
        }

        /// <inheritdoc />
        public void GetFont(string family, Action<FontAsset> onLoaded)
        {
            onLoaded?.Invoke(Resolve(family));
        }

        /// <summary>True when <paramref name="family"/> resolves to a bundled Font Asset.</summary>
        public bool IsCached(string family) => Resolve(family) != null;

        /// <summary>Warms the cache for every registered family. Cheap — a synchronous Resources load each.</summary>
        public void Preload(CampaignConfig config)
        {
            foreach (var family in _registry.Keys) Resolve(family);
        }

        private FontAsset Resolve(string family)
        {
            if (string.IsNullOrWhiteSpace(family)) return null;
            family = family.Trim();

            if (_byFamily.TryGetValue(family, out var cached)) return cached;
            if (_misses.Contains(family)) return null;

            if (!_registry.TryGetValue(family, out var path) || string.IsNullOrWhiteSpace(path))
            {
                _log.Warning($"{LogTag} unknown font family '{family}' — falling back to default");
                _misses.Add(family);
                return null;
            }

            FontAsset fa = null;
            try { fa = Resources.Load<FontAsset>(path); }
            catch (Exception e) { _log.Warning($"{LogTag} load error for '{family}' ({path}): {e.Message}"); }

            if (fa == null)
            {
                _log.Warning($"{LogTag} no Font Asset at Resources path '{path}' for family '{family}'");
                _misses.Add(family);
                return null;
            }

            _byFamily[family] = fa;
            return fa;
        }
    }
}
