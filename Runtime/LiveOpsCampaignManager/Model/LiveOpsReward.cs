using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>What earned a <see cref="LiveOpsReward"/>.</summary>
    public enum RewardSource
    {
        /// <summary>A daily mission the SDK claimed (Claim / Claim All).</summary>
        Mission,

        /// <summary>A milestone chest the SDK claimed once the player's points reached it.</summary>
        Milestone,

        /// <summary>A campaign Buy button whose bundle lists its items, paid and verified.</summary>
        Purchase,
    }

    /// <summary>One item of a reward: the game's item id and how many.</summary>
    [Preserve]
    public sealed class RewardItem
    {
        /// <summary>Most items one reward may list.</summary>
        public const int MaxItems = 20;

        /// <summary>Longest item id.</summary>
        public const int MaxIdLength = 64;

        /// <summary>Largest quantity of one item.</summary>
        public const long MaxQuantity = 1_000_000_000;

        private static readonly Regex IdPattern = new Regex(@"^[A-Za-z0-9_.:\-]+$");

        /// <summary>The game's item id, e.g. <c>gem</c>.</summary>
        [JsonProperty("id")] public string Id;

        /// <summary>How many, at least 1.</summary>
        [JsonProperty("qty")] public long Quantity;

        public override string ToString() => $"{Id} x{Quantity}";

        /// <summary>
        /// A reward's item list: a JSON array of <c>{"id", "qty"}</c> (a mission's or chest's
        /// rewards). Null when it is empty or any item is invalid — a reward is granted whole or not
        /// at all, never partly.
        /// </summary>
        public static List<RewardItem> ParseList(string json) => ParseItems(ParseJson(json) as JArray);

        /// <summary>
        /// The items a dynamic product bundle lists: its <c>items</c>, or for an array bundle every
        /// entry's <c>items</c> joined. Null when it lists none (the game delivers it by SKU) or any
        /// item is invalid.
        /// </summary>
        public static List<RewardItem> ParseBundle(JToken bundle)
        {
            var entries = bundle is JArray array ? array.ToList() : new List<JToken> { bundle };
            if (entries.Count == 0 || entries.All(entry => (entry as JObject)?["items"] == null)) return null;

            var raw = new JArray();
            foreach (var entry in entries)
            {
                if ((entry as JObject)?["items"] is not JArray items) return null;
                foreach (var item in items) raw.Add(item);
            }
            return ParseItems(raw);
        }

        /// <summary><see cref="ParseBundle(JToken)"/> for the bundle's JSON text.</summary>
        public static List<RewardItem> ParseBundle(string json) => ParseBundle(ParseJson(json));

        private static JToken ParseJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JToken.Parse(json); }
            catch (JsonException) { return null; }
        }

        private static List<RewardItem> ParseItems(JArray raw)
        {
            if (raw == null || raw.Count == 0 || raw.Count > MaxItems) return null;

            var result = new List<RewardItem>(raw.Count);
            foreach (var entry in raw)
            {
                var item = Parse(entry);
                if (item == null) return null;
                result.Add(item);
            }
            return result;
        }

        private static RewardItem Parse(JToken entry)
        {
            if (entry is not JObject obj) return null;

            var id = obj["id"]?.Type == JTokenType.String ? ((string)obj["id"]).Trim() : null;
            if (string.IsNullOrEmpty(id) || id.Length > MaxIdLength || !IdPattern.IsMatch(id)) return null;

            var qty = obj["qty"];
            if (qty == null || qty.Type != JTokenType.Integer) return null;
            var quantity = (long)qty;
            if (quantity < 1 || quantity > MaxQuantity) return null;

            return new RewardItem { Id = id, Quantity = quantity };
        }
    }

    /// <summary>
    /// A reward the SDK owes the game: handed to <c>CampaignHandlers.Rewards</c>, saved on the
    /// device until that handler finishes, and delivered again after the next init or login if it
    /// does not. <see cref="ClaimId"/> is unique per player, so the game grants each one once.
    /// </summary>
    [Preserve]
    public sealed class LiveOpsReward
    {
        /// <summary>
        /// Unique per player: the tracker key of a mission or chest (claimable once), or
        /// <c>order:&lt;order id&gt;</c> for a purchase. Ignore one you have already granted.
        /// </summary>
        [JsonProperty("claim_id")] public string ClaimId;

        /// <summary>What earned it.</summary>
        [JsonProperty("source"), JsonConverter(typeof(StringEnumConverter))] public RewardSource Source;

        /// <summary>The campaign it came from.</summary>
        [JsonProperty("campaign_id")] public string CampaignId;

        /// <summary>The mission key (<c>use_stm</c>), the chest's points (<c>25</c>), or the SKU bought.</summary>
        [JsonProperty("key")] public string Key;

        /// <summary>
        /// What to grant, as set in the admin. Empty when the admin set none (the mission or chest
        /// is still claimed).
        /// </summary>
        [JsonProperty("items")] public List<RewardItem> Items = new List<RewardItem>();

        /// <summary>When the SDK recorded it (UTC).</summary>
        [JsonProperty("claimed_at")] public DateTime ClaimedAtUtc;

        public override string ToString() =>
            $"{Source} {CampaignId}/{Key}: {(Items == null || Items.Count == 0 ? "no items" : string.Join(", ", Items))}";
    }
}
