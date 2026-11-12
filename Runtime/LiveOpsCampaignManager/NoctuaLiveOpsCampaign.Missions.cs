using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// Daily mission and milestone chest claims done by the SDK — a popup's <c>claim</c> /
    /// <c>claim_all</c> button, or the game calling <see cref="ClaimMissionRewardAsync"/>,
    /// <see cref="ClaimMilestoneRewardAsync"/> or <see cref="ClaimAllMissionsAsync"/>. Each claim
    /// is a tracker claim (once per player, on the server); its reward goes to the game's
    /// <c>Rewards</c> handler (see the Rewards partial).
    ///
    /// <para>Points: when the admin gives missions points (<c>m_&lt;key&gt;_points</c>), the
    /// player's points are the sum over claimed missions, worked out here from server progress, and
    /// a chest (<c>milestone_&lt;points&gt;_claimed</c>) becomes claimable once they reach it. A
    /// chest the SDK claims has its own tracker key, <c>&lt;scope&gt;.milestone_&lt;points&gt;</c>
    /// (max 1), registered by the DAG; it is set to 1 and claimed.</para>
    /// </summary>
    public sealed partial class NoctuaLiveOpsCampaign
    {
        /// <summary>
        /// <c>player_data</c> key a daily missions popup can declare: how many missions and chests
        /// are ready to claim. Show a Claim All button with <c>visible_if claimable_count gt 0</c>.
        /// </summary>
        public const string ClaimableCountKey = "claimable_count";

        /// <summary><c>player_data</c> key for the player's points; filled by the SDK when missions carry points.</summary>
        public const string PointsKey = "points";

        private static readonly Regex MissionPointsKey = new Regex(@"^m_(.+)_points$");
        private static readonly Regex MilestoneClaimedKey = new Regex(@"^milestone_(\d+)_claimed$");

        /// <summary>
        /// Claims daily mission <paramref name="missionKey"/> of campaign <paramref name="campaignId"/>
        /// and delivers its reward through the <c>Rewards</c> handler; also returns it. Throws like
        /// <see cref="ClaimProgressAsync"/> (e.g. already claimed, not completed, offline).
        /// </summary>
        public async UniTask<LiveOpsReward> ClaimMissionRewardAsync(string campaignId, string missionKey)
        {
            RequireMissionKey(missionKey);
            var item = RequireMissionCampaign(campaignId, missionKey);

            var trackerKey = TrackerKey(item, missionKey);
            await ClaimProgressAsync(trackerKey);

            var reward = MissionRewardOf(item, missionKey, trackerKey);
            RecordRewards(new[] { reward });
            return reward;
        }

        /// <summary>
        /// Claims the chest at <paramref name="points"/> of campaign <paramref name="campaignId"/>
        /// and delivers its reward through the <c>Rewards</c> handler; also returns it. Throws when
        /// the player's points are below it (<c>LiveOpsProgressNotCompleted</c>), when it was
        /// claimed already (<c>LiveOpsProgressAlreadyClaimed</c>), or like <see cref="ClaimProgressAsync"/>.
        /// </summary>
        public async UniTask<LiveOpsReward> ClaimMilestoneRewardAsync(string campaignId, int points)
        {
            var item = FindCampaign(campaignId)
                       ?? throw new NoctuaException(NoctuaErrorCode.Application, $"No live ops campaign '{campaignId}'");
            if (!DeclaredMilestones(item).Contains(points))
            {
                throw new NoctuaException(NoctuaErrorCode.Application, $"Campaign '{campaignId}' has no milestone at {points} points");
            }

            var reward = await ClaimMilestoneCoreAsync(item, points, await GetProgressAsync());
            RecordRewards(new[] { reward });
            return reward;
        }

        /// <summary>
        /// Claims every completed, unclaimed mission of daily missions campaign
        /// <paramref name="campaignId"/>, then every chest the new points reach, and delivers their
        /// rewards through the <c>Rewards</c> handler; also returns them. Anything claimed elsewhere
        /// is skipped. Stops when the tracker goes down (or rejects the login) and returns what was
        /// claimed before; throws that error only when nothing could be claimed.
        /// </summary>
        public async UniTask<IReadOnlyList<LiveOpsReward>> ClaimAllMissionsAsync(string campaignId)
        {
            var item = FindCampaign(campaignId)
                       ?? throw new NoctuaException(NoctuaErrorCode.Application, $"No live ops campaign '{campaignId}'");

            var claimed = new List<LiveOpsReward>();
            NoctuaException down = null;

            foreach (var mission in ClaimableMissions(item, await GetProgressAsync()))
            {
                var trackerKey = TrackerKey(item, mission);
                down = await TryClaimAsync(campaignId, mission, async () =>
                {
                    await ClaimProgressAsync(trackerKey);
                    claimed.Add(MissionRewardOf(item, mission, trackerKey));
                });
                if (down != null) break;
            }

            // Chests after missions: the missions just claimed may have reached them. Progress
            // holds those claims already.
            if (down == null)
            {
                foreach (var points in ClaimableMilestones(item, Progress))
                {
                    down = await TryClaimAsync(campaignId, $"milestone {points}", async () =>
                        claimed.Add(await ClaimMilestoneCoreAsync(item, points, Progress)));
                    if (down != null) break;
                }
            }

            if (claimed.Count == 0 && down != null) throw down;
            RecordRewards(claimed);
            return claimed;
        }

        /// <summary>
        /// The missions of <paramref name="item"/> that are completed and not claimed in
        /// <paramref name="progress"/>, in progress order.
        /// </summary>
        public static IReadOnlyList<string> ClaimableMissions(CampaignItem item, IEnumerable<LiveOpsProgress> progress)
        {
            if (item == null || progress == null) return Array.Empty<string>();

            return progress
                .Where(row => row != null && !string.IsNullOrEmpty(row.Key) && row.Completed && !row.Claimed)
                .Select(row => DeclaredMissionOf(item, row.Key))
                .Where(mission => !string.IsNullOrEmpty(mission))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// The chests of <paramref name="item"/> the SDK can claim now, lowest first: the campaign
        /// counts points, the chest has its tracker key, it is not claimed, and the points reach it.
        /// </summary>
        public static IReadOnlyList<int> ClaimableMilestones(CampaignItem item, IEnumerable<LiveOpsProgress> progress)
        {
            if (!CountsPoints(item) || progress == null) return Array.Empty<int>();

            var rows = progress.Where(row => !string.IsNullOrEmpty(row?.Key)).GroupBy(row => row.Key)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var points = PointsOf(item, rows.Values);

            return DeclaredMilestones(item)
                .Where(chest => points >= chest
                                && rows.TryGetValue(MilestoneTrackerKey(item, chest), out var row)
                                && !row.Claimed)
                .ToList();
        }

        /// <summary>
        /// The player's points in <paramref name="item"/>: the sum of <c>m_&lt;key&gt;_points</c>
        /// over its claimed missions. 0 for a campaign whose missions carry no points.
        /// </summary>
        public static long PointsOf(CampaignItem item, IEnumerable<LiveOpsProgress> progress)
        {
            if (item?.Data == null || progress == null) return 0;

            long total = 0;
            foreach (var mission in progress
                         .Where(row => row != null && row.Claimed && !string.IsNullOrEmpty(row.Key))
                         .Select(row => DeclaredMissionOf(item, row.Key))
                         .Where(mission => !string.IsNullOrEmpty(mission))
                         .Distinct(StringComparer.Ordinal))
            {
                if (item.Data.TryGetValue($"m_{mission}_points", out var raw) && long.TryParse(raw, out var points) && points > 0)
                {
                    total += points;
                }
            }
            return total;
        }

        /// <summary>True when the admin gave the campaign's missions points, so the SDK counts them.</summary>
        public static bool CountsPoints(CampaignItem item) =>
            item?.Data != null && item.Data.Keys.Any(key => MissionPointsKey.IsMatch(key));

        /// <summary>The chests the popup shows (<c>milestone_&lt;points&gt;_claimed</c>), lowest first.</summary>
        public static IReadOnlyList<int> DeclaredMilestones(CampaignItem item) =>
            item?.PlayerData == null
                ? Array.Empty<int>()
                : item.PlayerData.Keys
                    .Select(key => MilestoneClaimedKey.Match(key))
                    .Where(match => match.Success && int.TryParse(match.Groups[1].Value, out _))
                    .Select(match => int.Parse(match.Groups[1].Value))
                    .Distinct()
                    .OrderBy(points => points)
                    .ToList();

        /// <summary>The tracker key of the chest at <paramref name="points"/>: <c>&lt;scope&gt;.milestone_&lt;points&gt;</c>.</summary>
        public static string MilestoneTrackerKey(CampaignItem item, int points) => TrackerKey(item, "milestone_" + points);

        /// <summary>
        /// The <c>player_data</c> the SDK fills for points and chests: <c>points</c> (when missions
        /// carry points), <c>milestone_&lt;points&gt;_claimed</c> for every chest with a tracker key,
        /// and <c>claimable_count</c> covering missions and chests.
        /// </summary>
        public static IReadOnlyDictionary<string, string> ToMilestonePlayerData(
            CampaignItem item, IReadOnlyList<LiveOpsProgress> progress)
        {
            var data = new Dictionary<string, string>(StringComparer.Ordinal);
            if (item == null || progress == null) return data;

            if (CountsPoints(item)) data[PointsKey] = PointsOf(item, progress).ToString();
            foreach (var chest in DeclaredMilestones(item))
            {
                var row = progress.FirstOrDefault(r => r?.Key == MilestoneTrackerKey(item, chest));
                if (row != null) data[$"milestone_{chest}_claimed"] = row.Claimed ? "true" : "false";
            }
            data[ClaimableCountKey] = (ClaimableMissions(item, progress).Count + ClaimableMilestones(item, progress).Count).ToString();
            return data;
        }

        /// <summary>Sets the chest's key to 1 and claims it; the reward is returned, not recorded.</summary>
        private async UniTask<LiveOpsReward> ClaimMilestoneCoreAsync(CampaignItem item, int points, IReadOnlyList<LiveOpsProgress> progress)
        {
            if (!CountsPoints(item))
            {
                throw new NoctuaException(NoctuaErrorCode.Application, $"Campaign '{item.Id}' has no mission points, so the game claims its chests");
            }

            var key = MilestoneTrackerKey(item, points);
            var row = progress.FirstOrDefault(r => r?.Key == key)
                      ?? throw new NoctuaException(NoctuaErrorCode.Application, $"Chest '{key}' is not registered on the tracker yet");
            if (row.Claimed)
            {
                throw new NoctuaException(NoctuaErrorCode.LiveOpsProgressAlreadyClaimed, $"Chest '{key}' was claimed already");
            }
            if (PointsOf(item, progress) < points)
            {
                throw new NoctuaException(NoctuaErrorCode.LiveOpsProgressNotCompleted, $"Chest '{key}' needs {points} points");
            }

            if (!row.Completed) await SetProgressAsync(key, Math.Max(1, row.Max));
            await ClaimProgressAsync(key);

            return new LiveOpsReward
            {
                ClaimId = key,
                Source = RewardSource.Milestone,
                CampaignId = item.Id,
                Key = points.ToString(),
                Items = ItemsAt(item, $"milestone_{points}_rewards"),
                ClaimedAtUtc = _utcNow(),
            };
        }

        /// <summary>
        /// Runs one claim of Claim All. Returns the error that should stop the run (tracker down,
        /// login rejected); a claim already made elsewhere or refused is logged and skipped.
        /// </summary>
        private async UniTask<NoctuaException> TryClaimAsync(string campaignId, string what, Func<UniTask> claim)
        {
            try
            {
                await claim();
            }
            catch (NoctuaException e) when (
                e.ErrorCode == (int)NoctuaErrorCode.Networking || Classify(e) == ProgressFailure.Auth)
            {
                return e;
            }
            catch (NoctuaException e) when (IsClaimSkip(e))
            {
                _log.Debug($"{LogTag} claim all '{campaignId}': '{what}' skipped ({e.Message})");
            }
            catch (NoctuaException e)
            {
                _log.Warning($"{LogTag} claim all '{campaignId}': '{what}' refused: {e.Message}");
            }
            return null;
        }

        /// <summary>The reward of <paramref name="mission"/>: the items the admin set at <c>m_&lt;key&gt;_rewards</c>.</summary>
        private LiveOpsReward MissionRewardOf(CampaignItem item, string mission, string trackerKey) =>
            new LiveOpsReward
            {
                ClaimId = trackerKey,
                Source = RewardSource.Mission,
                CampaignId = item.Id,
                Key = mission,
                Items = ItemsAt(item, $"m_{mission}_rewards"),
                ClaimedAtUtc = _utcNow(),
            };

        private CampaignItem RequireMissionCampaign(string campaignId, string missionKey)
        {
            var item = FindCampaign(campaignId)
                       ?? throw new NoctuaException(NoctuaErrorCode.Application, $"No live ops campaign '{campaignId}'");
            if (!HasMission(item, missionKey))
            {
                throw new NoctuaException(NoctuaErrorCode.Application, $"Campaign '{campaignId}' has no mission '{missionKey}'");
            }
            return item;
        }

        private static bool IsClaimSkip(NoctuaException e) =>
            e.ErrorCode == (int)NoctuaErrorCode.LiveOpsProgressAlreadyClaimed
            || e.ErrorCode == (int)NoctuaErrorCode.LiveOpsProgressNotCompleted;

        /// <summary>
        /// A <c>claim_all</c> button: claims everything ready; the rewards go to the handler and the
        /// open popup refreshes from the new progress. When the tracker is down or the login is
        /// gone, the player gets the usual "missions unavailable" notice. Never throws.
        /// </summary>
        private async UniTask ClaimAllFromPopupAsync(CampaignItem item)
        {
            try
            {
                var claimed = await ClaimAllMissionsAsync(item?.Id);
                _log.Info($"{LogTag} claim all in '{item?.Id}': {(claimed.Count == 0 ? "nothing to claim" : string.Join("; ", claimed))}");
            }
            catch (Exception e)
            {
                _log.Warning($"{LogTag} claim all in '{item?.Id}' failed: {e.Message}");
                ShowMissionsUnavailableNotice();
            }
        }

        /// <summary>
        /// A mission's or chest's <c>claim</c> button. Already claimed (another device) or not ready
        /// just refreshes the popup. Never throws.
        /// </summary>
        private async UniTask ClaimFromPopupAsync(CampaignItem item, string mission, string milestone)
        {
            var what = string.IsNullOrEmpty(milestone) ? $"'{mission}'" : $"milestone {milestone}";
            try
            {
                var reward = string.IsNullOrEmpty(milestone)
                    ? await ClaimMissionRewardAsync(item?.Id, mission)
                    : int.TryParse(milestone, out var points)
                        ? await ClaimMilestoneRewardAsync(item?.Id, points)
                        : throw new NoctuaException(NoctuaErrorCode.Application, $"Milestone '{milestone}' is not a number");
                _log.Info($"{LogTag} claimed {reward}");
            }
            catch (NoctuaException e) when (IsClaimSkip(e))
            {
                _log.Debug($"{LogTag} claim {what} in '{item?.Id}' skipped ({e.Message})");
                RefreshProgressQuietlyAsync().Forget();
            }
            catch (Exception e)
            {
                _log.Warning($"{LogTag} claim {what} in '{item?.Id}' failed: {e.Message}");
                ShowMissionsUnavailableNotice();
            }
        }
    }
}
