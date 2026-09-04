using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// Per-campaign override for the popup's close button. Omitted → the SDK draws its default
    /// "✕" chip at the top-right. Every aspect is overridable: hide it, skin it with an image,
    /// resize it, and place it anywhere on the card (a corner + inset, explicit edges, or a
    /// translate for centring). The presenter applies this each show and resets to the USS
    /// default between campaigns.
    /// </summary>
    [Preserve]
    public class CampaignCloseButton
    {
        /// <summary>Hide the SDK button entirely — the creative draws its own close in <c>view</c> (an <c>image</c> node with <c>action.type = "dismiss"</c>).</summary>
        [JsonProperty("hidden")]
        public bool Hidden;

        /// <summary>
        /// Image URL. When set, the button renders this image with no chip background, border or
        /// glyph. Supports <c>{{tokens}}</c>. Single asset, no <c>srcset</c> — keep it ≤ 512&nbsp;px.
        /// </summary>
        [JsonProperty("image_url")]
        public string ImageUrl;

        /// <summary>Width &amp; height in reference px. Null → the USS default (36). Overridden by <see cref="Width"/> / <see cref="Height"/>.</summary>
        [JsonProperty("size")]
        public int? Size;

        /// <summary>Explicit width — length string (<c>"40"</c>, <c>"40px"</c>, <c>"10%"</c>). Wins over <see cref="Size"/>.</summary>
        [JsonProperty("width")]
        public string Width;

        /// <summary>Explicit height — length string. Wins over <see cref="Size"/>.</summary>
        [JsonProperty("height")]
        public string Height;

        /// <summary>
        /// Corner the button is pinned to: <c>top-right</c> (default) / <c>top-left</c> /
        /// <c>bottom-right</c> / <c>bottom-left</c>. Ignored when any explicit edge
        /// (<see cref="Top"/> / <see cref="Right"/> / <see cref="Bottom"/> / <see cref="Left"/>) is set.
        /// </summary>
        [JsonProperty("anchor")]
        public string Anchor;

        /// <summary>Distance from the anchored corner's two edges, reference px. Null → the USS default (14).</summary>
        [JsonProperty("inset")]
        public int? Inset;

        /// <summary>Explicit distance from the card's top edge — length string. Overrides <see cref="Anchor"/> / <see cref="Inset"/> for this edge.</summary>
        [JsonProperty("top")]
        public string Top;

        /// <summary>Explicit distance from the card's right edge — length string.</summary>
        [JsonProperty("right")]
        public string Right;

        /// <summary>Explicit distance from the card's bottom edge — length string.</summary>
        [JsonProperty("bottom")]
        public string Bottom;

        /// <summary>Explicit distance from the card's left edge — length string.</summary>
        [JsonProperty("left")]
        public string Left;

        /// <summary>
        /// Post-layout offset, <c>"x y"</c> length pair (e.g. <c>"-50% -50%"</c> with
        /// <c>left: "50%"</c> / <c>top: "50%"</c> to centre the button on the card).
        /// </summary>
        [JsonProperty("translate")]
        public string Translate;
    }
}
