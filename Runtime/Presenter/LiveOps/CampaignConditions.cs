using System;
using System.Collections.Generic;
using System.Globalization;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// Evaluates and validates <see cref="CampaignCondition"/> (<c>visible_if</c>). Pure — no
    /// Unity types — so the validator and the renderer share one definition of "visible".
    /// </summary>
    public static class CampaignConditions
    {
        public const string OpEq = "eq";
        public const string OpNe = "ne";
        public const string OpLt = "lt";
        public const string OpLte = "lte";
        public const string OpGt = "gt";
        public const string OpGte = "gte";

        private static readonly HashSet<string> KnownOps = new HashSet<string>
        {
            OpEq, OpNe, OpLt, OpLte, OpGt, OpGte,
        };

        /// <summary>
        /// True when every clause holds against <paramref name="data"/>. A null or empty
        /// condition is visible. Never throws: an unknown operator evaluates to false (the
        /// validator rejects it before a campaign ever renders).
        /// </summary>
        public static bool Evaluate(CampaignCondition condition, IReadOnlyDictionary<string, string> data)
        {
            if (condition?.All == null) return true;

            foreach (var clause in condition.All)
            {
                if (clause == null || !EvaluateClause(clause, data)) return false;
            }
            return true;
        }

        /// <summary>Structural check: every clause present and using a known operator.</summary>
        public static bool TryValidate(CampaignCondition condition, out string error)
        {
            error = null;
            if (condition?.All == null) return true;

            for (var i = 0; i < condition.All.Count; i++)
            {
                var clause = condition.All[i];
                if (clause == null)
                {
                    error = $"visible_if clause {i} is empty";
                    return false;
                }
                if (!KnownOps.Contains(Normalize(clause.Op)))
                {
                    error = $"visible_if clause {i}: unknown op '{clause.Op}'";
                    return false;
                }
            }
            return true;
        }

        private static bool EvaluateClause(CampaignConditionClause clause, IReadOnlyDictionary<string, string> data)
        {
            var left = (CampaignTokens.Resolve(clause.Left, data) ?? string.Empty).Trim();
            var right = (CampaignTokens.Resolve(clause.Right, data) ?? string.Empty).Trim();

            switch (Normalize(clause.Op))
            {
                case OpEq: return string.Equals(left, right, StringComparison.Ordinal);
                case OpNe: return !string.Equals(left, right, StringComparison.Ordinal);
                case OpLt: return CompareNumbers(left, right, c => c < 0);
                case OpLte: return CompareNumbers(left, right, c => c <= 0);
                case OpGt: return CompareNumbers(left, right, c => c > 0);
                case OpGte: return CompareNumbers(left, right, c => c >= 0);
                default: return false;
            }
        }

        /// <summary>A numeric comparison where either side is not a number is false, not an error.</summary>
        private static bool CompareNumbers(string left, string right, Func<int, bool> test)
        {
            if (!double.TryParse(left, NumberStyles.Float, CultureInfo.InvariantCulture, out var l)) return false;
            if (!double.TryParse(right, NumberStyles.Float, CultureInfo.InvariantCulture, out var r)) return false;
            return test(l.CompareTo(r));
        }

        private static string Normalize(string op) => (op ?? string.Empty).Trim().ToLowerInvariant();
    }
}
