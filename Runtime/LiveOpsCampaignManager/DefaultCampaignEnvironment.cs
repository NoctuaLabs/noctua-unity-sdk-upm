using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// Default <see cref="ICampaignEnvironment"/>: player tags via an injected getter (the
    /// composition root points it at <c>_config.Noctua.PlayerRemoteConfigs.Tags</c>),
    /// country from the locale provider, app version from <c>Application.version</c>, SDK
    /// version from this assembly.
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

        /// <summary>This SDK's version, from its assembly (kept in step with package.json by CI).</summary>
        public string SdkVersion() => CurrentSdkVersion;

        /// <summary>The running Noctua SDK version, e.g. <c>0.138.1</c>.</summary>
        public static readonly string CurrentSdkVersion = ReadSdkVersion();

        private static string ReadSdkVersion()
        {
            try
            {
                var version = typeof(DefaultCampaignEnvironment).Assembly.GetName().Version;
                return version == null ? string.Empty : $"{version.Major}.{version.Minor}.{version.Build}";
            }
            catch
            {
                return string.Empty;
            }
        }

        public DateTime UtcNow() => DateTime.UtcNow;
    }
}
