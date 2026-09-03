using System;
using System.Collections.Generic;
using com.noctuagames.sdk.Campaign;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tests.Runtime.Campaign
{
    /// <summary>
    /// A real UI Toolkit panel so <c>SendEvent</c> dispatches and <c>resolvedStyle</c> is
    /// computed. Cheap enough for EditMode; call <see cref="Dispose"/> in TearDown.
    /// </summary>
    public sealed class CampaignPanelFixture : IDisposable
    {
        private readonly GameObject _go;
        private readonly PanelSettings _panelSettings;

        public VisualElement Root { get; }

        public CampaignPanelFixture()
        {
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _go = new GameObject("CampaignPanelFixture");
            var doc = _go.AddComponent<UIDocument>();
            doc.panelSettings = _panelSettings;
            Root = doc.rootVisualElement;
        }

        public void Add(VisualElement ve) => Root.Add(ve);

        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(_go);
            UnityEngine.Object.DestroyImmediate(_panelSettings);
        }
    }

    /// <summary>In-memory <see cref="IPlayerPrefsStore"/> so the frequency gate is deterministic.</summary>
    public sealed class FakePrefsStore : IPlayerPrefsStore
    {
        private readonly Dictionary<string, string> _s = new Dictionary<string, string>();
        private readonly Dictionary<string, int> _i = new Dictionary<string, int>();

        public string GetString(string key, string fallback) => _s.TryGetValue(key, out var v) ? v : fallback;
        public int GetInt(string key, int fallback) => _i.TryGetValue(key, out var v) ? v : fallback;
        public void SetString(string key, string value) => _s[key] = value;
        public void SetInt(string key, int value) => _i[key] = value;
        public void Save() { }
    }

    /// <summary>Fixed environment for <see cref="CampaignManager"/> targeting tests.</summary>
    public sealed class FakeEnv : ICampaignEnvironment
    {
        public List<string> Tags = new List<string>();
        public string CountryCode = "ID";
        public string Version = "1.0.0";
        public DateTime Now = DateTime.UtcNow;

        public IReadOnlyList<string> PlayerTags() => Tags;
        public string Country() => CountryCode;
        public string AppVersion() => Version;
        public DateTime UtcNow() => Now;
    }

    /// <summary>Records image requests + pin/unpin calls; never calls back with a texture.</summary>
    public sealed class FakeImageSource : ICampaignImageSource
    {
        public readonly List<string> Requested = new List<string>();
        public readonly List<string> Pinned = new List<string>();
        public readonly List<string> Unpinned = new List<string>();

        public void GetImage(string url, Action<Texture2D> onLoaded)
        {
            Requested.Add(url);
            onLoaded?.Invoke(null);
        }

        public void Pin(IReadOnlyCollection<string> urls)
        {
            if (urls != null) Pinned.AddRange(urls);
        }

        public void Unpin(IReadOnlyCollection<string> urls)
        {
            if (urls != null) Unpinned.AddRange(urls);
        }
    }

    /// <summary>
    /// Records requested font Resources paths and calls back with <see cref="Next"/> (null by
    /// default — the renderer must handle "unknown/failed" gracefully).
    /// </summary>
    public sealed class FakeFontSource : ICampaignFontSource
    {
        public readonly List<string> Requested = new List<string>();
        public UnityEngine.TextCore.Text.FontAsset Next;

        public void GetFont(string resourcesPath, Action<UnityEngine.TextCore.Text.FontAsset> onLoaded)
        {
            Requested.Add(resourcesPath);
            onLoaded?.Invoke(Next);
        }
    }

    /// <summary>Captures every <see cref="ICampaignActions.Dispatch"/> call.</summary>
    public sealed class RecordingActions : ICampaignActions
    {
        public readonly List<(CampaignAction action, CampaignItem item)> Calls =
            new List<(CampaignAction, CampaignItem)>();

        public void Dispatch(CampaignAction action, CampaignItem campaign) => Calls.Add((action, campaign));
    }

    internal static class CampaignFactory
    {
        public static CampaignNode Node(string type, Dictionary<string, object> props = null,
            CampaignStyleProps style = null, CampaignAction action = null, params CampaignNode[] children)
            => new CampaignNode
            {
                Type = type,
                Props = props,
                Style = style,
                Action = action,
                Children = children != null && children.Length > 0 ? new List<CampaignNode>(children) : null,
            };

        public static CampaignItem Item(string id, string engagementType, CampaignNode view,
            Dictionary<string, string> data = null)
            => new CampaignItem { Id = id, EngagementType = engagementType, View = view, Data = data };
    }
}
