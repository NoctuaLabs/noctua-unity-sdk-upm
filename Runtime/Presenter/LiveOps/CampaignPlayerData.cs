using System;
using System.Collections.Generic;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// Folds the values a game supplies at show time (mission progress, claimed flags…) into a
    /// campaign's <c>data</c>. A campaign declares the keys it expects in
    /// <see cref="CampaignItem.PlayerData"/>, with defaults; only those keys are accepted, so
    /// game values can never overwrite LiveOps copy. Pure: never mutates the stored item.
    /// </summary>
    public static class CampaignPlayerData
    {
        /// <summary>
        /// A render-ready copy of <paramref name="item"/> whose <c>Data</c> is layered
        /// <c>data</c> → <c>player_data</c> defaults → <paramref name="gameValues"/>. The copy's
        /// <c>PlayerData</c> is null: the declared keys now live in <c>Data</c>, and leaving the
        /// declaration would read as a collision when the copy is validated again.
        /// Undeclared game keys are dropped and reported through <paramref name="onUndeclared"/>.
        /// </summary>
        public static CampaignItem Merge(
            CampaignItem item,
            IReadOnlyDictionary<string, string> gameValues,
            Action<string> onUndeclared = null)
        {
            if (item == null) return null;

            var data = ValidationData(item);
            var declared = item.PlayerData;

            if (gameValues != null)
            {
                foreach (var pair in gameValues)
                {
                    if (declared == null || !declared.ContainsKey(pair.Key))
                    {
                        onUndeclared?.Invoke(pair.Key);
                        continue;
                    }
                    data[pair.Key] = pair.Value ?? string.Empty;
                }
            }

            var copy = item.ShallowCopy();
            copy.Data = data;
            copy.PlayerData = null;
            return copy;
        }

        /// <summary>
        /// <c>data</c> plus the <c>player_data</c> defaults, as a new dictionary — what a
        /// campaign resolves against before the game has supplied anything. On a key present in
        /// both, <c>data</c> wins (the validator reports that collision separately).
        /// </summary>
        public static Dictionary<string, string> ValidationData(CampaignItem item)
        {
            var data = item?.Data != null
                ? new Dictionary<string, string>(item.Data)
                : new Dictionary<string, string>();

            if (item?.PlayerData == null) return data;

            foreach (var pair in item.PlayerData)
            {
                if (!data.ContainsKey(pair.Key)) data[pair.Key] = pair.Value ?? string.Empty;
            }
            return data;
        }
    }
}
