using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk.Campaign
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
        /// text/button: <c>text</c>, <c>locKey</c>;
        /// image: <c>url</c>, <c>scaleMode</c>;
        /// countdown: <c>end_ts</c> (ISO-8601 or unix seconds), <c>prefix</c>, <c>suffix</c>;
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
