using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// Claim All for daily missions: one tap claims every mission of a campaign that is completed
    /// and not claimed yet. Each mission is still its own tracker claim, so each reward is granted
    /// at most once even if Claim All is retried after a partial failure.
    /// </summary>
    public sealed partial class NoctuaLiveOpsCampaign
    {
        /// <summary>
        /// <c>player_data</c> key a daily missions popup can declare: how many of its missions are
        /// completed and not claimed. Show a Claim All button with <c>visible_if claimable_count gt 0</c>.
        /// </summary>
        public const string ClaimableCountKey = "claimable_count";

        /// <summary>
        /// Fires with the campaign id and the mission keys <see cref="ClaimAllCompletedAsync"/> just
        /// claimed — grant those rewards.
        /// </summary>
        public event Action<string, IReadOnlyList<string>> OnMissionsClaimed;

        /// <summary>
        /// Claims every completed, unclaimed mission of daily missions campaign
        /// <paramref name="campaignId"/> and returns the mission keys claimed now — grant exactly
        /// those rewards. Missions already claimed elsewhere are skipped. Stops when the tracker
        /// goes down (or rejects the login) and returns what was claimed before; throws that error
        /// only when nothing could be claimed.
        /// </summary>
        public async UniTask<IReadOnlyList<string>> ClaimAllCompletedAsync(string campaignId)
        {
            var item = FindCampaign(campaignId)
                       ?? throw new NoctuaException(NoctuaErrorCode.Application, $"No live ops campaign '{campaignId}'");

            var progress = await GetProgressAsync();
            var claimed = new List<string>();
            NoctuaException down = null;

            foreach (var mission in ClaimableMissions(item, progress))
            {
                try
                {
                    await ClaimProgressAsync(TrackerKey(item, mission));
                    claimed.Add(mission);
                }
                catch (NoctuaException e) when (
                    e.ErrorCode == (int)NoctuaErrorCode.Networking || Classify(e) == ProgressFailure.Auth)
                {
                    down = e;
                    break;
                }
                catch (NoctuaException e) when (
                    e.ErrorCode == (int)NoctuaErrorCode.LiveOpsProgressAlreadyClaimed
                    || e.ErrorCode == (int)NoctuaErrorCode.LiveOpsProgressNotCompleted)
                {
                    _log.Debug($"{LogTag} claim all '{campaignId}': '{mission}' skipped ({e.Message})");
                }
                catch (NoctuaException e)
                {
                    _log.Warning($"{LogTag} claim all '{campaignId}': '{mission}' refused: {e.Message}");
                }
            }

            if (claimed.Count == 0 && down != null) throw down;
            if (claimed.Count > 0) NotifyMissionsClaimed(campaignId, claimed);
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
        /// A <c>claim_all</c> button: claims everything finished and lets
        /// <see cref="OnMissionsClaimed"/> tell the game what to grant. The open popup refreshes
        /// itself from the new progress. When the tracker is down or the login is gone, the
        /// player gets the usual "missions unavailable" notice. Never throws.
        /// </summary>
        private async UniTask ClaimAllFromPopupAsync(CampaignItem item)
        {
            try
            {
                var claimed = await ClaimAllCompletedAsync(item?.Id);
                _log.Info($"{LogTag} claim all in '{item?.Id}': {(claimed.Count == 0 ? "nothing to claim" : string.Join(", ", claimed))}");
            }
            catch (Exception e)
            {
                _log.Warning($"{LogTag} claim all in '{item?.Id}' failed: {e.Message}");
                ShowMissionsUnavailableNotice();
            }
        }

        private void NotifyMissionsClaimed(string campaignId, IReadOnlyList<string> missions)
        {
            try { OnMissionsClaimed?.Invoke(campaignId, missions); }
            catch (Exception e) { _log.Warning($"{LogTag} OnMissionsClaimed handler threw: {e.Message}"); }
        }
    }
}
