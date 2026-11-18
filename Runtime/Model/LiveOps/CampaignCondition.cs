using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// A node's <c>visible_if</c>: the node renders only when every clause in
    /// <see cref="All"/> holds. Deliberately tiny — AND of comparisons, no nesting, no OR —
    /// enough to switch a mission button between Go / Claim / Claimed without turning the
    /// payload into a programming language. Evaluated by <see cref="CampaignConditions"/>.
    /// </summary>
    [Preserve]
    public class CampaignCondition
    {
        /// <summary>Clauses that must all be true. Null or empty means visible.</summary>
        [JsonProperty("all")]
        public List<CampaignConditionClause> All;
    }

    /// <summary>
    /// One comparison. <see cref="Left"/> and <see cref="Right"/> are token-resolved against the
    /// campaign's data (a missing token resolves to empty) before comparing.
    /// </summary>
    [Preserve]
    public class CampaignConditionClause
    {
        [JsonProperty("left")]
        public string Left;

        /// <summary><c>eq</c> / <c>ne</c> compare strings; <c>lt</c> / <c>lte</c> / <c>gt</c> / <c>gte</c> compare numbers.</summary>
        [JsonProperty("op")]
        public string Op;

        [JsonProperty("right")]
        public string Right;
    }
}
