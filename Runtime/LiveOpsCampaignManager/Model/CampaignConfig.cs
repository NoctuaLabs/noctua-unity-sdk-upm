using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// Top-level server-driven campaign payload. Delivered either in <c>noctuagg.json</c>
    /// (<c>live_ops_engagement_campaigns</c> section) for local/offline development or in the
    /// <c>/games/init</c> response under <c>remote_configs.live_ops_engagement_campaigns</c>.
    /// </summary>
    [Preserve]
    public class CampaignConfig
    {
        /// <summary>
        /// Envelope format version. The per-campaign gate uses
        /// <see cref="CampaignItem.SchemaVersion"/> (falling back to this when a
        /// campaign omits it). Default 1.
        /// </summary>
        [JsonProperty("schema_version")]
        public int SchemaVersion = 1;

        /// <summary>All campaigns in this payload, across every engagement type.</summary>
        [JsonProperty("campaigns")]
        public List<CampaignItem> Campaigns;

        /// <summary>
        /// Returns a new config where <paramref name="remote"/> takes precedence:
        /// campaigns are unioned by <see cref="CampaignItem.Id"/> with the remote entry
        /// winning on a collision, and the higher <see cref="SchemaVersion"/> wins. A
        /// <c>null</c> remote returns this config unchanged. Copies the null-coalesce idiom of
        /// <c>IAA.MergeWith</c> — not its code.
        /// </summary>
        public CampaignConfig MergeWith(CampaignConfig remote)
        {
            if (remote == null) return this;

            var byId = new Dictionary<string, CampaignItem>();

            if (Campaigns != null)
            {
                foreach (var c in Campaigns)
                {
                    if (c != null && !string.IsNullOrEmpty(c.Id)) byId[c.Id] = c;
                }
            }

            if (remote.Campaigns != null)
            {
                foreach (var c in remote.Campaigns)
                {
                    if (c != null && !string.IsNullOrEmpty(c.Id)) byId[c.Id] = c;
                }
            }

            return new CampaignConfig
            {
                SchemaVersion = remote.SchemaVersion > SchemaVersion ? remote.SchemaVersion : SchemaVersion,
                Campaigns = byId.Values.ToList(),
            };
        }
    }
}
