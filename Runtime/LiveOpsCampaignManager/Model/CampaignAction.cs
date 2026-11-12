using System;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// The closed set of things a campaign button/tap can do. Anything the client
    /// doesn't recognise resolves to <see cref="None"/> — never arbitrary code.
    /// Scoped to the purchase + event use cases: buy a product, route into the game,
    /// close the popup, or claim every finished mission.
    /// </summary>
    public enum CampaignActionType
    {
        None,
        Deeplink,
        Purchase,
        Dismiss,

        /// <summary>
        /// Daily missions: claim every completed, unclaimed mission of the campaign in one tap.
        /// The SDK does the claims and hands the rewards to the game's <c>Rewards</c> handler; the popup stays open.
        /// </summary>
        ClaimAll,

        /// <summary>
        /// Daily missions: claim the mission named by <see cref="CampaignAction.Mission"/>, or the
        /// chest at <see cref="CampaignAction.Milestone"/> points. The SDK does the claim and hands
        /// the reward to the game's <c>Rewards</c> handler; the popup stays open.
        /// </summary>
        Claim,
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
        /// Dynamic product, instead of <see cref="ProductId"/>: a key into the campaign's <c>data</c>
        /// whose value is a JSON bundle (an object, or an array of objects). The game's product
        /// resolver (<c>SetProductResolver</c>) turns the bundle into the SKU to buy; with no
        /// resolver, or no SKU for it, the button is hidden.
        /// </summary>
        [JsonProperty("product_key")]
        public string ProductKey;

        /// <summary>Mission key for <see cref="CampaignActionType.Claim"/>, e.g. <c>use_stm</c>.</summary>
        [JsonProperty("mission")]
        public string Mission;

        /// <summary>Chest points for <see cref="CampaignActionType.Claim"/>, e.g. <c>25</c>; instead of <see cref="Mission"/>.</summary>
        [JsonProperty("milestone")]
        public string Milestone;

        /// <summary>
        /// Deeplink only: when true the popup stays open after the route is handed to the game,
        /// so the game can claim a reward and refresh the popup with
        /// <c>UpdatePopupData</c>. Default false — a deeplink navigates away and closes it.
        /// </summary>
        [JsonProperty("keep_open")]
        public bool KeepOpen;

        /// <summary>
        /// Optional stable id the admin assigns to a button (e.g. <c>offer_1_btn_0</c>). Reported
        /// on <c>live_ops_campaign_click</c> as <c>button_id</c> so analytics can tell buttons
        /// apart; has no effect on what the action does.
        /// </summary>
        [JsonProperty("id")]
        public string Id;

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
