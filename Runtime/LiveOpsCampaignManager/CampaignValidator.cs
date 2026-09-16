using System;
using System.Collections.Generic;
using System.Globalization;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// Structural pre-flight for a <see cref="CampaignItem"/>: does every node and action have
    /// the values it needs to render something meaningful? Unlike the renderer (best-effort —
    /// a missing prop renders an empty box), a failure here cancels the whole campaign: the
    /// caller shows nothing and logs the returned reason. Pure — no Unity types.
    /// </summary>
    public static class CampaignValidator
    {
        // Mirror the renderer's guards so a hostile tree can't stack-overflow the walk.
        private const int MaxDepth = 40;
        private const int MaxNodes = 600;

        /// <summary>
        /// Returns <c>true</c> when <paramref name="item"/> has everything it needs to render.
        /// On the first problem returns <c>false</c> and sets <paramref name="error"/> to a
        /// short human-readable reason (e.g. <c>"image node: missing or unresolved 'url'"</c>).
        /// </summary>
        public static bool TryValidate(CampaignItem item, out string error)
        {
            error = null;

            if (item == null) { error = "campaign is null"; return false; }
            if (string.IsNullOrEmpty(item.Id)) { error = "missing id"; return false; }

            if (!string.Equals(item.EngagementType, CampaignItem.EngagementPurchase, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(item.EngagementType, CampaignItem.EngagementEvent, StringComparison.OrdinalIgnoreCase))
            {
                error = $"engagement_type must be '{CampaignItem.EngagementPurchase}' or " +
                        $"'{CampaignItem.EngagementEvent}' (got '{item.EngagementType}')";
                return false;
            }

            if (item.View == null) { error = "no view tree"; return false; }

            if (item.PlayerData != null && item.Data != null)
            {
                foreach (var key in item.PlayerData.Keys)
                {
                    if (!item.Data.ContainsKey(key)) continue;
                    error = $"player_data key '{key}' is also a data key";
                    return false;
                }
            }

            // Resolve against data + player_data defaults, so a declared runtime token is not
            // "unresolved" before the game has supplied it.
            var data = CampaignPlayerData.ValidationData(item);
            var nodeBudget = MaxNodes;
            return ValidateNode(item.View, item, data, 0, ref nodeBudget, out error);
        }

        private static bool ValidateNode(CampaignNode node, CampaignItem item, IReadOnlyDictionary<string, string> data,
            int depth, ref int nodeBudget, out string error)
        {
            error = null;
            if (node == null) return true;

            if (!CampaignConditions.TryValidate(node.VisibleIf, out error)) return false;
            // A hidden subtree never renders, so its requirements do not apply.
            if (!CampaignConditions.Evaluate(node.VisibleIf, data)) return true;

            // Over-limit subtrees are truncated by the renderer the same way — not a failure.
            if (depth >= MaxDepth || nodeBudget <= 0) return true;
            nodeBudget--;

            var type = (node.Type ?? string.Empty).Trim().ToLowerInvariant();
            switch (type)
            {
                case CampaignNode.TypeText:
                case CampaignNode.TypeButton:
                {
                    var label = ResolveLabel(node, data, out var labelMissing);
                    if (string.IsNullOrWhiteSpace(label) || labelMissing)
                    {
                        error = $"{type} node: empty or unresolved label";
                        return false;
                    }
                    break;
                }

                case CampaignNode.TypeImage:
                {
                    var url = ResolveRequired(node.PropString("url"), data, out var urlMissing);
                    if (string.IsNullOrWhiteSpace(url) || urlMissing)
                    {
                        error = "image node: missing or unresolved 'url'";
                        return false;
                    }
                    break;
                }

                case CampaignNode.TypeCountdown:
                {
                    var endTs = ResolveRequired(node.EndTimestamp(), data, out var endMissing);
                    if (string.IsNullOrWhiteSpace(endTs) || endMissing || !CanParseTimestamp(endTs))
                    {
                        error = "countdown node: missing or unparseable 'end_timestamp'";
                        return false;
                    }
                    break;
                }

                case CampaignNode.TypeCarousel:
                {
                    if (node.Children == null || node.Children.Count == 0)
                    {
                        error = "carousel node: needs at least one child";
                        return false;
                    }
                    break;
                }
            }

            if (!ValidateAction(node.Action, data, out error)) return false;

            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    if (!ValidateNode(child, item, data, depth + 1, ref nodeBudget, out error)) return false;
                }
            }

            return true;
        }

        private static bool ValidateAction(CampaignAction a, IReadOnlyDictionary<string, string> data, out string error)
        {
            error = null;
            if (a == null || a.Type == CampaignActionType.None) return true;

            switch (a.Type)
            {
                case CampaignActionType.Purchase:
                {
                    var pid = ResolveRequired(a.ProductId, data, out var pidMissing);
                    if (string.IsNullOrWhiteSpace(pid) || pidMissing)
                    {
                        error = "purchase action: missing 'product_id'";
                        return false;
                    }
                    break;
                }

                case CampaignActionType.Deeplink:
                {
                    var route = ResolveRequired(a.Deeplink, data, out var routeMissing);
                    if (string.IsNullOrWhiteSpace(route) || routeMissing)
                    {
                        error = "deeplink action: missing 'deeplink'";
                        return false;
                    }
                    break;
                }
            }

            return true;
        }

        // ---- helpers -------------------------------------------------------

        private static string ResolveLabel(CampaignNode node, IReadOnlyDictionary<string, string> data, out bool tokenMissing)
        {
            var locKey = node.PropString("loc_key");
            if (!string.IsNullOrEmpty(locKey) && data != null && data.TryGetValue(locKey, out var loc))
            {
                return ResolveRequired(loc, data, out tokenMissing);
            }
            return ResolveRequired(node.PropString("text", string.Empty), data, out tokenMissing);
        }

        /// <summary>
        /// Resolves <c>{{token}}</c>s against <c>item.Data</c> and flags
        /// <paramref name="tokenMissing"/> when a referenced key is absent — an unresolved
        /// token in a required field is treated as a missing value.
        /// </summary>
        private static string ResolveRequired(string raw, IReadOnlyDictionary<string, string> data, out bool tokenMissing)
        {
            var missing = false;
            var result = CampaignTokens.Resolve(raw, data, _ => missing = true);
            tokenMissing = missing;
            return result;
        }

        private static bool CanParseTimestamp(string raw)
        {
            if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) return true;
            return DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out _);
        }
    }
}
