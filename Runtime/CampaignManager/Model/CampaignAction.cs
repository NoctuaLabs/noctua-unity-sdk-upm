using System;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk.Campaign
{
    /// <summary>
    /// The closed set of things a campaign button/tap can do. Anything the client
    /// doesn't recognise resolves to <see cref="None"/> — never arbitrary code.
    /// Scoped to the purchase + event use cases: buy a product, route into the game,
    /// or close the popup.
    /// </summary>
    public enum CampaignActionType
    {
        None,
        Deeplink,
        Purchase,
        Dismiss,
    }

    /// <summary>
    /// Action payload attached to a tappable node. Only the fields relevant to
    /// <see cref="Type"/> are read.
    /// </summary>
    [Preserve]
    public class CampaignAction
    {
        /// <summary>Raw action type string from JSON (e.g. <c>"deeplink"</c>).</summary>
        [JsonProperty("type")]
        public string TypeRaw;

        /// <summary>Route string handed to the game's registered deeplink handler.</summary>
        [JsonProperty("deeplink")]
        public string Deeplink;

        /// <summary>Product id for <see cref="CampaignActionType.Purchase"/>.</summary>
        [JsonProperty("product_id")]
        public string ProductId;

        /// <summary>
        /// Resolved action type. Matches <see cref="TypeRaw"/> case- and separator-insensitively
        /// (<c>"deeplink"</c> / <c>"deep-link"</c> / <c>"DeepLink"</c> all map to
        /// <see cref="CampaignActionType.Deeplink"/>); anything unrecognised → <see cref="CampaignActionType.None"/>.
        /// </summary>
        [JsonIgnore]
        public CampaignActionType Type
        {
            get
            {
                if (string.IsNullOrEmpty(TypeRaw)) return CampaignActionType.None;

                var norm = TypeRaw.Replace("_", string.Empty).Replace("-", string.Empty);

                foreach (CampaignActionType value in Enum.GetValues(typeof(CampaignActionType)))
                {
                    if (string.Equals(value.ToString(), norm, StringComparison.OrdinalIgnoreCase))
                    {
                        return value;
                    }
                }

                return CampaignActionType.None;
            }
        }
    }
}
