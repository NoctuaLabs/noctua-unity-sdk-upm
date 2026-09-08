using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// One node in a campaign's component tree. The renderer maps <see cref="Type"/>
    /// onto a UI Toolkit <c>VisualElement</c>; an unknown type is skipped (never throws).
    /// </summary>
    [Preserve]
    public class CampaignNode
    {
        public const string TypeContainer = "container";
        public const string TypeText = "text";
        public const string TypeImage = "image";

        /// <summary>Countdown end instant.</summary>
        public const string PropEndTimestamp = "end_timestamp";

        /// <summary>Pre-rename spelling of <see cref="PropEndTimestamp"/>.</summary>
        public const string PropEndTimestampLegacy = "end_ts";
        public const string TypeButton = "button";
        public const string TypeSpacer = "spacer";
        public const string TypeDivider = "divider";
        public const string TypeList = "list";
        public const string TypeCarousel = "carousel";
        public const string TypeProgressBar = "progressbar";
        public const string TypeCountdown = "countdown";

        /// <summary>Widget type — one of the <c>Type*</c> constants.</summary>
        [JsonProperty("type")]
        public string Type;

        /// <summary>
        /// Per-type scalar properties. Common keys:
        /// text/button: <c>text</c>, <c>loc_key</c>;
        /// image: <c>url</c>, <c>scale_mode</c>, <c>srcset</c>;
        /// countdown: <c>end_timestamp</c> (ISO-8601 or unix seconds; <c>end_ts</c> pre-rename), <c>prefix</c>, <c>suffix</c>,
        ///   <c>icon_url</c> (leading icon, supports tokens), <c>icon_size</c> (px, default 16),
        ///   <c>icon_gap</c> (px, default 6), <c>icon_position</c> (<c>leading</c> | <c>trailing</c>);
        /// progressbar: <c>value</c>, <c>min</c>, <c>max</c>;
        /// carousel: <c>autoplay</c>, <c>interval_ms</c>, <c>loop</c>.
        /// </summary>
        [JsonProperty("props")]
        public Dictionary<string, object> Props;

        /// <summary>Base style. Whitelisted USS subset; unknown props are ignored.</summary>
        [JsonProperty("style")]
        public CampaignStyleProps Style;

        /// <summary>
        /// Orientation overrides applied on top of <see cref="Style"/>.
        /// Keys: <c>"portrait"</c>, <c>"landscape"</c>.
        /// </summary>
        [JsonProperty("responsive")]
        public Dictionary<string, CampaignStyleProps> Responsive;

        /// <summary>Child nodes (for <c>container</c>, <c>list</c>, <c>carousel</c>).</summary>
        [JsonProperty("children")]
        public List<CampaignNode> Children;

        /// <summary>Optional action fired when the node is tapped (buttons, tappable images).</summary>
        [JsonProperty("action")]
        public CampaignAction Action;

        /// <summary>Reads a string prop, or <paramref name="fallback"/> when missing.</summary>
        public string PropString(string key, string fallback = null)
        {
            if (Props == null || !Props.TryGetValue(key, out var v) || v == null) return fallback;
            return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The countdown's end instant, read from <c>end_timestamp</c> and falling back
        /// to the pre-rename <c>end_ts</c>.
        ///
        /// Both are accepted because the two ends of this contract ship separately: a
        /// payload written by an admin that has been renamed can reach a client that
        /// has not, and vice versa. A countdown that stops resolving takes its whole
        /// campaign off the air, so the fallback stays until no live payload uses the
        /// old key.
        /// </summary>
        public string EndTimestamp()
        {
            var value = PropString(PropEndTimestamp);
            return string.IsNullOrWhiteSpace(value) ? PropString(PropEndTimestampLegacy) : value;
        }

        /// <summary>Reads a float prop, or <c>null</c> when missing/unparseable.</summary>
        public float? PropFloat(string key)
        {
            if (Props == null || !Props.TryGetValue(key, out var v) || v == null) return null;
            try { return Convert.ToSingle(v, System.Globalization.CultureInfo.InvariantCulture); }
            catch { return float.TryParse(Convert.ToString(v), out var f) ? f : (float?)null; }
        }

        /// <summary>Reads an int prop, or <c>null</c> when missing/unparseable.</summary>
        public int? PropInt(string key)
        {
            var f = PropFloat(key);
            return f.HasValue ? (int?)Mathf_RoundToInt(f.Value) : null;
        }

        /// <summary>
        /// Reads the <c>image</c> node's <c>srcset</c> as a list of <see cref="CampaignImageSrc"/>,
        /// sorted ascending by width, with empty-url / non-positive-width entries dropped.
        /// Returns an empty list when <c>srcset</c> is absent, not an array, or unparseable —
        /// callers then fall back to <c>props.url</c>.
        /// </summary>
        public List<CampaignImageSrc> PropSrcset()
        {
            if (Props == null || !Props.TryGetValue("srcset", out var raw) || raw == null)
                return new List<CampaignImageSrc>();

            try
            {
                // `raw` is a JArray when it came through Newtonsoft (runtime), or a plain
                // IEnumerable when built in code (tests) — JToken.FromObject handles a JToken
                // by returning it and serializes anything else.
                var token = raw as JToken ?? JToken.FromObject(raw);
                if (token.Type != JTokenType.Array) return new List<CampaignImageSrc>();

                var parsed = token.ToObject<List<CampaignImageSrc>>() ?? new List<CampaignImageSrc>();
                return parsed
                    .Where(e => e != null && !string.IsNullOrWhiteSpace(e.Url) && e.Width > 0)
                    .OrderBy(e => e.Width)
                    .ToList();
            }
            catch
            {
                return new List<CampaignImageSrc>();
            }
        }

        /// <summary>Reads a bool prop, or <paramref name="fallback"/> when missing/unparseable.</summary>
        public bool PropBool(string key, bool fallback = false)
        {
            if (Props == null || !Props.TryGetValue(key, out var v) || v == null) return fallback;
            if (v is bool b) return b;
            var s = Convert.ToString(v);
            if (bool.TryParse(s, out var parsed)) return parsed;
            return s == "1" || string.Equals(s, "yes", StringComparison.OrdinalIgnoreCase);
        }

        // Local rounding helper — avoids a UnityEngine dependency in this pure model type.
        private static int Mathf_RoundToInt(float f) => (int)Math.Round(f, MidpointRounding.AwayFromZero);
    }
}
