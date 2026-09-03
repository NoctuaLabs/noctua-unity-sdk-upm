using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// One entry in an <c>image</c> node's <c>props.srcset</c> — a candidate asset plus the
    /// intrinsic pixel width it was authored at. The renderer picks the smallest entry whose
    /// <see cref="Width"/> is at least the element's resolved box width in physical pixels,
    /// falling back to the largest entry, and to <c>props.url</c> when the set is empty.
    /// Width descriptors (not <c>1x</c>/<c>2x</c>) so the pick matches the actual layout box
    /// rather than an assumed DPI bucket.
    /// </summary>
    [Preserve]
    public class CampaignImageSrc
    {
        /// <summary>Asset URL for this tier.</summary>
        [JsonProperty("url")]
        public string Url;

        /// <summary>Intrinsic pixel width the asset was exported at. Entries with <c>w &lt;= 0</c> are dropped.</summary>
        [JsonProperty("w")]
        public int Width;
    }
}
