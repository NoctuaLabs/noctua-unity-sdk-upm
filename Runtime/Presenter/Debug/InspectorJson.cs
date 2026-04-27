using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// JSON helper used by the native bridge trampolines to marshal the
    /// payload/extra params strings coming across the C ABI / JNI boundary
    /// into a <see cref="IReadOnlyDictionary{TKey,TValue}"/> the UI can
    /// iterate without string parsing.
    /// </summary>
    public static class InspectorJson
    {
        public static IReadOnlyDictionary<string, object> Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json) || json == "{}")
            {
                return new Dictionary<string, object>();
            }
            try
            {
                var jobj = JObject.Parse(json);
                var dict = new Dictionary<string, object>(jobj.Count);
                foreach (var p in jobj.Properties())
                {
                    dict[p.Name] = ConvertToken(p.Value);
                }
                return dict;
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }

        private static object ConvertToken(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Integer: return token.Value<long>();
                case JTokenType.Float:   return token.Value<double>();
                case JTokenType.Boolean: return token.Value<bool>();
                case JTokenType.String:  return token.Value<string>();
                case JTokenType.Null:    return null;
                case JTokenType.Object:
                {
                    var o = (JObject)token;
                    var inner = new Dictionary<string, object>(o.Count);
                    foreach (var p in o.Properties()) inner[p.Name] = ConvertToken(p.Value);
                    return inner;
                }
                case JTokenType.Array:
                {
                    var arr = (JArray)token;
                    var list = new List<object>(arr.Count);
                    foreach (var t in arr) list.Add(ConvertToken(t));
                    return list;
                }
                default: return token.ToString();
            }
        }
    }
}
