using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// Per-campaign override for the popup's built-in close button. Omitted → the SDK draws
    /// its default "✕" chip at the top-right. The presenter applies this each show and resets
    /// to the USS default between campaigns.
    /// </summary>
    [Preserve]
    public class CampaignCloseButton
    {
        /// <summary>Hide the SDK close button entirely — the creative draws its own close in <c>view</c> (an <c>image</c> node with <c>action.type = "dismiss"</c>).</summary>
        [JsonProperty("hidden")]
        public bool Hidden;

        /// <summary>
        /// <c>Resources</c>-independent image URL. When set, the close button renders this image
        /// with no chip background, border or glyph. Supports <c>{{tokens}}</c>. Single asset,
        /// no <c>srcset</c> — keep it ≤ 512&nbsp;px.
        /// </summary>
        [JsonProperty("image_url")]
        public string ImageUrl;

        /// <summary>Width &amp; height in reference px. Null → the USS default (36).</summary>
        [JsonProperty("size")]
        public int? Size;

        /// <summary>Distance from the card's top and right edge in reference px. Null → the USS default (14).</summary>
        [JsonProperty("inset")]
        public int? Inset;
    }
}
