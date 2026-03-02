using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Converts a JSON object/value to its string representation, or passes through if already a string.
    /// Used for fields where the native SDK may return a nested JSON object instead of an escaped string.
    /// </summary>
    [Preserve]
    public class RawJsonStringConverter : JsonConverter<string>
    {
        public override string ReadJson(JsonReader reader, Type objectType, string existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
                return (string)reader.Value;
            if (reader.TokenType == JsonToken.Null)
                return null;
            var token = JToken.Load(reader);
            return token.ToString(Formatting.None);
        }

        public override void WriteJson(JsonWriter writer, string value, JsonSerializer serializer)
        {
            writer.WriteValue(value);
        }
    }
}
