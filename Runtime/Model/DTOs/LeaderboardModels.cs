using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    [Preserve]
    public class UpdateLeaderboardScoreRequest
    {
        [JsonProperty("score")]
        public int Score;
    }

    [Preserve]
    public class LeaderboardRanking
    {
        [JsonProperty("rank")]
        public int Rank;

        [JsonProperty("player_id")]
        public long PlayerId;

        [JsonProperty("user_id")]
        public long UserId;

        [JsonProperty("score")]
        public int Score;

        [JsonProperty("nickname")]
        public string Nickname;

        [JsonProperty("country")]
        public string Country;

        [JsonProperty("metadata")]
        public Dictionary<string, object> Metadata;

        [JsonProperty("updated_at")]
        public string UpdatedAt;
    }

    [Preserve]
    public class LeaderboardCurrentUser
    {
        [JsonProperty("rank")]
        public int Rank;

        [JsonProperty("score")]
        public int Score;

        [JsonProperty("updated_at")]
        public string UpdatedAt;
    }

    [Preserve]
    public class LeaderboardResponse
    {
        [JsonProperty("slug")]
        public string Slug;

        [JsonProperty("name")]
        public string Name;

        [JsonProperty("period_key")]
        public string PeriodKey;

        [JsonProperty("total_entries")]
        public int TotalEntries;

        [JsonProperty("current_user")]
        public LeaderboardCurrentUser CurrentUser;

        [JsonProperty("rankings")]
        public List<LeaderboardRanking> Rankings;
    }
}
