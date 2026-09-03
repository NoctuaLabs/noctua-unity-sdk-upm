using System;
using System.Collections.Generic;
using System.Text;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// <c>{{token}}</c> substitution against a campaign's <c>data</c> bag. Dumb by design:
    /// plain key lookup, no expressions, no conditionals. A missing key resolves to empty
    /// and is reported through the optional <paramref name="onMissing"/> callback.
    /// </summary>
    public static class CampaignTokens
    {
        public static string Resolve(string input, IReadOnlyDictionary<string, string> data, Action<string> onMissing = null)
        {
            if (string.IsNullOrEmpty(input) || input.IndexOf("{{", StringComparison.Ordinal) < 0) return input;

            var sb = new StringBuilder(input.Length);
            var i = 0;
            while (i < input.Length)
            {
                var open = input.IndexOf("{{", i, StringComparison.Ordinal);
                if (open < 0) { sb.Append(input, i, input.Length - i); break; }

                sb.Append(input, i, open - i);
                var close = input.IndexOf("}}", open + 2, StringComparison.Ordinal);
                if (close < 0) { sb.Append(input, open, input.Length - open); break; }

                var key = input.Substring(open + 2, close - open - 2).Trim();
                if (data != null && data.TryGetValue(key, out var val))
                {
                    sb.Append(val);
                }
                else
                {
                    onMissing?.Invoke(key);
                }
                i = close + 2;
            }
            return sb.ToString();
        }
    }
}
