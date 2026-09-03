using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>Ambient facts campaign eligibility depends on. Kept off the <c>Noctua</c> static class.</summary>
    public interface ICampaignEnvironment
    {
        /// <summary>Player tags (from <c>player_remote_configs.tags</c>). Never null.</summary>
        IReadOnlyList<string> PlayerTags();

        /// <summary>ISO country code, or empty/unknown.</summary>
        string Country();

        /// <summary>Dotted-numeric app version (e.g. <c>Application.version</c>).</summary>
        string AppVersion();

        /// <summary>Current UTC time.</summary>
        DateTime UtcNow();
    }

    /// <summary>One line in the Inspector's Campaigns tab: was this campaign shown, and if not, why.</summary>
    public readonly struct CampaignResolution
    {
        public readonly string Id;
        public readonly string EngagementType;
        public readonly bool Eligible;
        public readonly string Reason;

        public CampaignResolution(string id, string engagementType, bool eligible, string reason)
        {
            Id = id;
            EngagementType = engagementType;
            Eligible = eligible;
            Reason = reason;
        }
    }

    /// <summary>
    /// Resolves which campaigns are eligible: schema-version gate → validation → schedule
    /// window → targeting → frequency, then orders by priority. An optional engagement-type
    /// filter narrows the set. Each campaign is evaluated in a try/catch so one bad entry
    /// never blocks the rest. Holds no UI.
    /// </summary>
    public sealed class CampaignManager
    {
        private readonly ICampaignEnvironment _env;
        private readonly CampaignFrequencyGate _frequency;
        private readonly Func<bool> _isOffline;
        private readonly Func<CampaignItem, bool> _assetsReady;
        private readonly ILogger _log;
        private readonly List<CampaignResolution> _lastResolutions = new List<CampaignResolution>();

        private const string LogTag = "[campaign_manager]";

        public CampaignManager(
            CampaignConfig config,
            ICampaignEnvironment env,
            CampaignFrequencyGate frequency,
            ILogger log = null,
            Func<bool> isOffline = null,
            Func<CampaignItem, bool> assetsReady = null)
        {
            Config = config ?? new CampaignConfig();
            _env = env;
            _frequency = frequency ?? new CampaignFrequencyGate();
            _isOffline = isOffline ?? (() => false);
            _assetsReady = assetsReady ?? (_ => true);
            _log = log ?? new NoctuaLogger(typeof(CampaignManager));
        }

        /// <summary>The merged campaign payload backing this manager.</summary>
        public CampaignConfig Config { get; }

        /// <summary>Report from the most recent <see cref="GetActiveCampaigns"/> call (for the Inspector).</summary>
        public IReadOnlyList<CampaignResolution> LastResolutions => _lastResolutions;

        /// <summary>
        /// Eligible campaigns, highest <c>priority</c> first. When
        /// <paramref name="engagementType"/> is non-null, only campaigns of that type are
        /// returned. Also refreshes <see cref="LastResolutions"/>.
        /// </summary>
        public List<CampaignItem> GetActiveCampaigns(string engagementType = null)
        {
            _lastResolutions.Clear();
            var eligible = new List<CampaignItem>();

            if (Config?.Campaigns == null) return eligible;

            foreach (var item in Config.Campaigns)
            {
                if (item == null) continue;

                try
                {
                    var reason = Evaluate(item, engagementType);
                    var ok = reason == null;
                    _lastResolutions.Add(new CampaignResolution(item.Id, item.EngagementType, ok, ok ? "eligible" : reason));
                    if (ok)
                    {
                        eligible.Add(item);
                    }
                    else if (reason != null && reason.StartsWith("invalid:", StringComparison.Ordinal))
                    {
                        // A structurally broken campaign is a content bug — surface it loudly,
                        // unlike the routine targeting/schedule/frequency misses.
                        _log.Warning($"{LogTag} campaign '{item.Id}' skipped — {reason}");
                    }
                }
                catch (Exception e)
                {
                    _log.Error($"{LogTag} campaign '{item.Id}' threw during evaluation: {e.Message}");
                    _lastResolutions.Add(new CampaignResolution(item.Id, item.EngagementType, false, "evaluation error: " + e.Message));
                }
            }

            eligible.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            return eligible;
        }

        /// <summary>
        /// Top eligible campaign (optionally of <paramref name="engagementType"/>), or the one
        /// matching <paramref name="id"/>.
        /// </summary>
        public CampaignItem GetTopCampaign(string engagementType = null, string id = null)
        {
            var list = GetActiveCampaigns(engagementType);
            if (list.Count == 0) return null;
            if (string.IsNullOrEmpty(id)) return list[0];
            return list.FirstOrDefault(c => c.Id == id);
        }

        /// <summary>Marks <paramref name="item"/> as shown so frequency caps advance.</summary>
        public void MarkShown(CampaignItem item) => _frequency.RecordShow(item);

        // ---- eligibility rules (null == eligible) ------------------------

        private string Evaluate(CampaignItem item, string engagementType)
        {
            if (string.IsNullOrEmpty(item.Id)) return "missing id";

            var required = item.EffectiveSchemaVersion(Config.SchemaVersion);
            if (required > CampaignRenderer.SupportedSchemaVersion)
                return $"schema v{required} > supported v{CampaignRenderer.SupportedSchemaVersion}";

            if (!string.IsNullOrEmpty(engagementType)
                && !string.Equals(item.EngagementType, engagementType, StringComparison.OrdinalIgnoreCase))
                return "engagement type mismatch";

            if (item.View == null)
                return "no view tree";

            // Structural pre-flight: a campaign missing a required node/action value (or an
            // unresolved {{token}} in one) is cancelled outright, not rendered half-blank.
            if (!CampaignValidator.TryValidate(item, out var invalidReason))
                return "invalid: " + invalidReason;

            var scheduleReason = CheckSchedule(item.Schedule);
            if (scheduleReason != null) return scheduleReason;

            var targetingReason = CheckTargeting(item.Targeting);
            if (targetingReason != null) return targetingReason;

            // Offline: only show a campaign whose creatives are all cached (or it has none) —
            // otherwise it would render blank image boxes.
            if (_isOffline() && !_assetsReady(item))
                return "assets not cached (offline)";

            if (!_frequency.CanShow(item))
                return "frequency cap";

            return null;
        }

        private string CheckSchedule(CampaignSchedule schedule)
        {
            if (schedule == null) return null;
            var now = _env?.UtcNow() ?? DateTime.UtcNow;

            if (TryParseInstant(schedule.Start, out var start) && now < start) return "before schedule start";
            if (TryParseInstant(schedule.End, out var end) && now >= end) return "after schedule end";
            return null;
        }

        private string CheckTargeting(CampaignTargeting t)
        {
            if (t == null || _env == null) return null;

            if (t.Tags != null && t.Tags.Count > 0)
            {
                var playerTags = _env.PlayerTags() ?? Array.Empty<string>();
                if (!t.Tags.Any(tag => playerTags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
                    return "tag mismatch";
            }

            if (t.Countries != null && t.Countries.Count > 0)
            {
                var country = _env.Country() ?? string.Empty;
                if (!t.Countries.Any(c => string.Equals(c, country, StringComparison.OrdinalIgnoreCase)))
                    return "country mismatch";
            }

            var appVersion = _env.AppVersion() ?? string.Empty;
            if (!string.IsNullOrEmpty(t.MinAppVersion) && CompareVersions(appVersion, t.MinAppVersion) < 0)
                return "app version below min";
            if (!string.IsNullOrEmpty(t.MaxAppVersion) && CompareVersions(appVersion, t.MaxAppVersion) > 0)
                return "app version above max";

            return null;
        }

        private static bool TryParseInstant(string raw, out DateTime utc)
        {
            utc = default;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            return DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc);
        }

        /// <summary>Compares dotted-numeric versions. Non-numeric segments compare as 0.</summary>
        public static int CompareVersions(string a, string b)
        {
            var pa = (a ?? string.Empty).Split('.');
            var pb = (b ?? string.Empty).Split('.');
            var len = Math.Max(pa.Length, pb.Length);
            for (var i = 0; i < len; i++)
            {
                var na = i < pa.Length && int.TryParse(pa[i], out var x) ? x : 0;
                var nb = i < pb.Length && int.TryParse(pb[i], out var y) ? y : 0;
                if (na != nb) return na < nb ? -1 : 1;
            }
            return 0;
        }
    }
}
