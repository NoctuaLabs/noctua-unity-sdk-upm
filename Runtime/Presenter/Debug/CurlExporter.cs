using System.Text;

namespace com.noctuagames.sdk.Inspector
{
    /// <summary>
    /// Builds a <c>curl</c> command that reproduces a captured HTTP exchange.
    /// Intended for QA bug reports — tapping "Copy as cURL" on an expanded
    /// HTTP row places the command on the clipboard.
    ///
    /// Redacted headers (already masked by <see cref="HttpRequest"/>) stay
    /// masked here — we don't have the real token at this point anyway.
    /// </summary>
    public static class CurlExporter
    {
        public static string ToCurl(HttpExchange ex)
        {
            if (ex == null) return "";
            var sb = new StringBuilder();
            sb.Append("curl -X ").Append(ex.Method ?? "GET").Append(' ');
            sb.Append(Quote(ex.Url));

            if (ex.RequestHeaders != null)
            {
                foreach (var kv in ex.RequestHeaders)
                {
                    sb.Append(" \\\n  -H ").Append(Quote($"{kv.Key}: {kv.Value}"));
                }
            }

            if (!string.IsNullOrEmpty(ex.RequestBody))
            {
                sb.Append(" \\\n  --data-raw ").Append(Quote(ex.RequestBody));
            }

            return sb.ToString();
        }

        private static string Quote(string s)
        {
            if (s == null) return "''";
            // Use single-quotes (shell-safe for everything except single-quote itself).
            return "'" + s.Replace("'", "'\\''") + "'";
        }
    }
}
