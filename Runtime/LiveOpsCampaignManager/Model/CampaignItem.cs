using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// A single campaign: what to show, where, when, to whom, and how often.
    /// </summary>
    [Preserve]
    public class CampaignItem
    {
        /// <summary>Engagement type for a purchase / offer promotion.</summary>
        public const string EngagementPurchase = "purchase";

        /// <summary>Engagement type for an event / news announcement.</summary>
        public const string EngagementEvent = "event";

        /// <summary>Stable unique id. Used for merge, frequency capping, and targeting by id.</summary>
        [JsonProperty("id")]
        public string Id;

        /// <summary>
        /// Widget-schema version this campaign's <see cref="View"/> tree requires.
        /// 0 or missing is treated as 1. A value above
        /// <c>CampaignRenderer.SupportedSchemaVersion</c> makes the whole campaign skip.
        /// </summary>
        [JsonProperty("schema_version")]
        public int SchemaVersion;

        /// <summary>
        /// Campaign classification: <see cref="EngagementPurchase"/> or <see cref="EngagementEvent"/>.
        /// Both show through the same popup surface; this drives reporting and lets a caller
        /// query one kind via <c>GetActiveCampaigns(engagementType)</c>.
        /// </summary>
        [JsonProperty("engagement_type")]
        public string EngagementType;

        /// <summary>Higher shows first when several campaigns are eligible at once.</summary>
        [JsonProperty("priority")]
        public int Priority;

        /// <summary>Optional targeting filters. Null means "everyone".</summary>
        [JsonProperty("targeting")]
        public CampaignTargeting Targeting;

        /// <summary>Optional per-player frequency caps. Null means "no cap".</summary>
        [JsonProperty("frequency")]
        public CampaignFrequency Frequency;

        /// <summary>Optional active window. Null means "always".</summary>
        [JsonProperty("schedule")]
        public CampaignSchedule Schedule;

        /// <summary>The component tree to render.</summary>
        [JsonProperty("view")]
        public CampaignNode View;

        /// <summary>Values substituted into <c>{{token}}</c> placeholders in the view tree.</summary>
        [JsonProperty("data")]
        public Dictionary<string, string> Data;

        /// <summary>
        /// Keys the game supplies when it shows the popup (<c>ShowPopup(id, playerData)</c>),
        /// each with a default — per-player values such as mission progress that cannot ship in
        /// the shared <see cref="Data"/>. Their <c>{{tokens}}</c> resolve like any other; a key
        /// the game leaves out renders its default. Must not repeat a <see cref="Data"/> key.
        /// </summary>
        [JsonProperty("player_data")]
        public Dictionary<string, string> PlayerData;

        /// <summary>
        /// Optional per-campaign custom-font registry: alias → <c>Resources</c> path of a
        /// Font Asset (or source <c>.ttf</c>/<c>.otf</c>) the game ships in its build. A node's
        /// <c>style.font_path</c> is looked up here; when it isn't a key (or there is no
        /// registry) the value is used as the <c>Resources</c> path directly. An unknown key or
        /// a missing asset falls back to the default typeface.
        /// </summary>
        [JsonProperty("fonts")]
        public Dictionary<string, string> Fonts;

        /// <summary>
        /// When true, the SDK shows this campaign once automatically right after init
        /// (subject to targeting/frequency).
        /// </summary>
        [JsonProperty("auto_show")]
        public bool AutoShow;

        /// <summary>
        /// Render edge-to-edge with no card chrome (padding / rounded corners / max-size).
        /// Use for full-screen splash promos.
        /// </summary>
        [JsonProperty("fullscreen")]
        public bool Fullscreen;

        /// <summary>
        /// Optional override for the popup's built-in close button (hide it, or skin it with a
        /// custom image + size + inset). Null → the SDK's default "✕" chip.
        /// </summary>
        [JsonProperty("close_button")]
        public CampaignCloseButton CloseButton;

        /// <summary>
        /// Optional hex colour (e.g. <c>#3B82F6</c>) for the SDK card's frame border. Only
        /// applies with card chrome on (<c>"borderless": false</c>, not fullscreen). An
        /// unparseable value is ignored — the card keeps its default border.
        /// </summary>
        [JsonProperty("frame_color")]
        public string FrameColor;

        /// <summary>
        /// Optional hex colour for the dim behind the popup, e.g. <c>#000000A8</c>; the alpha
        /// channel is the opacity. Null or unparseable keeps the SDK default dim.
        /// </summary>
        [JsonProperty("backdrop_color")]
        public string BackdropColor;

        /// <summary>
        /// Raw JSON binding for <see cref="Borderless"/>. Null when the campaign omits
        /// <c>"borderless"</c> — read <see cref="Borderless"/> for the effective value.
        /// </summary>
        [JsonProperty("borderless")]
        public bool? BorderlessRaw;

        /// <summary>
        /// Drop the card chrome (dark background, padding, rounded corners) but keep the popup
        /// centered and content-sized — the view tree draws its own frame. Use when the
        /// creative is a self-contained rounded card image.
        /// <para>
        /// Defaults to <c>true</c> when <c>"borderless"</c> is absent from the payload: most
        /// campaign creatives are self-framed images. Set <c>"borderless": false</c> to opt
        /// back into the SDK card chrome.
        /// </para>
        /// </summary>
        [JsonIgnore]
        public bool Borderless
        {
            get => BorderlessRaw ?? true;
            set => BorderlessRaw = value;
        }

        /// <summary>A member-wise copy sharing nested objects — for render-time variants such as
        /// <see cref="CampaignPlayerData.Merge"/>, which replace a field rather than edit it.</summary>
        public CampaignItem ShallowCopy() => (CampaignItem)MemberwiseClone();

        /// <summary>Effective widget-schema version (see <see cref="SchemaVersion"/>).</summary>
        public int EffectiveSchemaVersion(int configDefault) =>
            SchemaVersion > 0 ? SchemaVersion : (configDefault > 0 ? configDefault : 1);
    }

    /// <summary>Optional ISO-8601 active window for a campaign.</summary>
    [Preserve]
    public class CampaignSchedule
    {
        /// <summary>ISO-8601 start instant, inclusive. Null/empty means "no lower bound".</summary>
        [JsonProperty("start")]
        public string Start;

        /// <summary>ISO-8601 end instant, exclusive. Null/empty means "no upper bound".</summary>
        [JsonProperty("end")]
        public string End;
    }
}
