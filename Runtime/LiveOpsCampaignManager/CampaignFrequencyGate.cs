using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// Per-device display-frequency enforcement for campaigns, persisted in
    /// <c>PlayerPrefs</c> under <c>Noctua.LiveOpsCampaign.&lt;id&gt;.*</c>. Independent of the
    /// IAA <c>AdFrequencyManager</c>; the persistence idiom mirrors <c>NoctuaWebContent</c>.
    /// The clock is injectable so it can be unit-tested without real time.
    /// </summary>
    public sealed class CampaignFrequencyGate
    {
        private const string KeyPrefix = "Noctua.LiveOpsCampaign.";
        private readonly Func<DateTime> _utcNow;
        private readonly IPlayerPrefsStore _prefs;

        public CampaignFrequencyGate(Func<DateTime> utcNow = null, IPlayerPrefsStore prefs = null)
        {
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _prefs = prefs ?? new UnityPlayerPrefsStore();
        }

        /// <summary>True when <paramref name="item"/> is allowed to show right now.</summary>
        public bool CanShow(CampaignItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.Id)) return false;
            var f = item.Frequency;
            if (f == null) return true;

            var shows = ReadShows(item.Id);

            if (f.OnceEver && shows.Count > 0) return false;

            if (f.CooldownHours > 0 && shows.Count > 0)
            {
                var last = shows[shows.Count - 1];
                if ((_utcNow() - last).TotalHours < f.CooldownHours) return false;
            }

            if (f.MaxPerDay > 0)
            {
                var since = _utcNow().AddDays(-1);
                var inWindow = 0;
                foreach (var s in shows) if (s >= since) inWindow++;
                if (inWindow >= f.MaxPerDay) return false;
            }

            return true;
        }

        /// <summary>Records a show of <paramref name="item"/> at "now" and prunes old entries.</summary>
        public void RecordShow(CampaignItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.Id)) return;

            var shows = ReadShows(item.Id);
            shows.Add(_utcNow());

            // Keep only what any rule could still care about: 24h window + cooldown span,
            // with a hard cap so the string can't grow without bound.
            var cutoffHours = Math.Max(24, item.Frequency?.CooldownHours ?? 0);
            var cutoff = _utcNow().AddHours(-cutoffHours);
            var kept = new List<long>();
            foreach (var s in shows)
            {
                if (s >= cutoff) kept.Add(ToUnix(s));
            }
            if (kept.Count > 64) kept.RemoveRange(0, kept.Count - 64);

            _prefs.SetString(KeyPrefix + item.Id + ".Shows", string.Join(",", kept));
            if (item.Frequency?.OnceEver == true)
            {
                _prefs.SetInt(KeyPrefix + item.Id + ".Ever", 1);
            }
            _prefs.Save();
        }

        private List<DateTime> ReadShows(string id)
        {
            var result = new List<DateTime>();

            if (_prefs.GetInt(KeyPrefix + id + ".Ever", 0) == 1)
            {
                // Marker that a once-ever campaign has fired even if the CSV was pruned.
                result.Add(DateTime.MinValue.ToUniversalTime());
            }

            var csv = _prefs.GetString(KeyPrefix + id + ".Shows", string.Empty);
            if (!string.IsNullOrEmpty(csv))
            {
                foreach (var part in csv.Split(','))
                {
                    if (long.TryParse(part, out var unix)) result.Add(FromUnix(unix));
                }
            }

            result.Sort();
            return result;
        }

        private static long ToUnix(DateTime dt) => new DateTimeOffset(dt.ToUniversalTime()).ToUnixTimeSeconds();
        private static DateTime FromUnix(long unix) => DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
    }

    /// <summary>Tiny persistence seam so the gate is unit-testable without Unity.</summary>
    public interface IPlayerPrefsStore
    {
        string GetString(string key, string fallback);
        int GetInt(string key, int fallback);
        void SetString(string key, string value);
        void SetInt(string key, int value);
        void Save();
    }

    /// <summary>Default <see cref="IPlayerPrefsStore"/> backed by <c>UnityEngine.PlayerPrefs</c>.</summary>
    public sealed class UnityPlayerPrefsStore : IPlayerPrefsStore
    {
        public string GetString(string key, string fallback) => PlayerPrefs.GetString(key, fallback);
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public void Save() => PlayerPrefs.Save();
    }
}
