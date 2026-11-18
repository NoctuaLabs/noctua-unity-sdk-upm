using System.Collections.Generic;
using System.Text;

namespace com.noctuagames.sdk.Inspector
{
    /// <summary>
    /// Serialises the current Inspector capture (HTTP exchanges +
    /// tracker emissions) to a single JSON blob for sharing in bug
    /// reports. No timestamps are reformatted — keep raw UTC so
    /// engineers can diff across devices.
    /// </summary>
    public static class InspectorExporter
    {
        public static string ToJson(
            IReadOnlyList<HttpExchange> httpExchanges,
            IReadOnlyList<TrackerEmission> emissions)
        {
            var sb = new StringBuilder(4096);
            sb.Append("{\"schema\":1,\"http\":[");
            if (httpExchanges != null)
            {
                bool first = true;
                foreach (var ex in httpExchanges)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteHttp(sb, ex);
                }
            }
            sb.Append("],\"trackers\":[");
            if (emissions != null)
            {
                bool first = true;
                foreach (var em in emissions)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteTracker(sb, em);
                }
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static void WriteHttp(StringBuilder sb, HttpExchange ex)
        {
            sb.Append('{');
            Kv(sb, "id", ex.Id.ToString()); sb.Append(',');
            Kv(sb, "method", ex.Method);    sb.Append(',');
            Kv(sb, "url", ex.Url);          sb.Append(',');
            sb.Append("\"status\":").Append(ex.Status).Append(',');
            sb.Append("\"elapsedMs\":").Append(ex.ElapsedMs).Append(',');
            Kv(sb, "state", ex.State.ToString()); sb.Append(',');
            Kv(sb, "startUtc", ex.StartUtc.ToString("O")); sb.Append(',');
            WriteDict(sb, "reqHeaders", ex.RequestHeaders); sb.Append(',');
            WriteDict(sb, "resHeaders", ex.ResponseHeaders); sb.Append(',');
            Kv(sb, "reqBody", ex.RequestBody); sb.Append(',');
            Kv(sb, "resBody", ex.ResponseBody);
            if (!string.IsNullOrEmpty(ex.Error))
            {
                sb.Append(','); Kv(sb, "error", ex.Error);
            }
            sb.Append('}');
        }

        private static void WriteTracker(StringBuilder sb, TrackerEmission em)
        {
            sb.Append('{');
            Kv(sb, "id", em.Id.ToString());              sb.Append(',');
            Kv(sb, "provider", em.Provider);             sb.Append(',');
            Kv(sb, "eventName", em.EventName);           sb.Append(',');
            Kv(sb, "phase", em.Phase.ToString());        sb.Append(',');
            Kv(sb, "createdUtc", em.CreatedUtc.ToString("O"));
            if (em.History != null && em.History.Count > 0)
            {
                sb.Append(",\"history\":[");
                for (int i = 0; i < em.History.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append('{');
                    Kv(sb, "phase", em.History[i].Phase.ToString()); sb.Append(',');
                    Kv(sb, "atUtc", em.History[i].AtUtc.ToString("O"));
                    sb.Append('}');
                }
                sb.Append(']');
            }
            if (!string.IsNullOrEmpty(em.Error))
            {
                sb.Append(','); Kv(sb, "error", em.Error);
            }
            sb.Append('}');
        }

        private static void WriteDict(StringBuilder sb, string key, IReadOnlyDictionary<string, string> d)
        {
            sb.Append('"').Append(key).Append("\":{");
            if (d != null)
            {
                bool first = true;
                foreach (var kv in d)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    Kv(sb, kv.Key, kv.Value);
                }
            }
            sb.Append('}');
        }

        private static void Kv(StringBuilder sb, string key, string value)
        {
            sb.Append('"').Append(Escape(key)).Append("\":");
            if (value == null) sb.Append("null");
            else sb.Append('"').Append(Escape(value)).Append('"');
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            var sb = new StringBuilder(s.Length + 8);
            foreach (var ch in s)
            {
                switch (ch)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"':  sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': sb.Append("\\r");  break;
                    case '\t': sb.Append("\\t");  break;
                    case '\b': sb.Append("\\b");  break;
                    case '\f': sb.Append("\\f");  break;
                    default:
                        if (ch < 0x20) sb.Append("\\u").Append(((int)ch).ToString("x4"));
                        else sb.Append(ch);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
