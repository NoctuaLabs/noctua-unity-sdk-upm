using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// Default <see cref="ICampaignEnvironment"/>: player tags via an injected getter (the
    /// composition root points it at <c>_config.Noctua.PlayerRemoteConfigs.Tags</c>),
    /// country from the locale provider, version from <c>Application.version</c>.
    /// </summary>
    public sealed class DefaultCampaignEnvironment : ICampaignEnvironment
    {
        private static readonly string[] Empty = Array.Empty<string>();

        private readonly Func<IReadOnlyList<string>> _playerTags;
        private readonly ILocaleProvider _locale;

        public DefaultCampaignEnvironment(Func<IReadOnlyList<string>> playerTags, ILocaleProvider locale)
        {
            _playerTags = playerTags;
            _locale = locale;
        }

        public IReadOnlyList<string> PlayerTags()
        {
            try { return _playerTags?.Invoke() ?? Empty; }
            catch { return Empty; }
        }

        public string Country()
        {
            try { return _locale?.GetCountry() ?? string.Empty; }
            catch { return string.Empty; }
        }

        public string AppVersion() => Application.version ?? string.Empty;

        public DateTime UtcNow() => DateTime.UtcNow;
    }
}
