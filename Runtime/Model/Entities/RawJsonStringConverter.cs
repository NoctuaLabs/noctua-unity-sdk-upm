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
        /// <summary>
        /// Reads a JSON token and returns it as a string, handling both string tokens and nested JSON objects/arrays.
        /// </summary>
        public override string ReadJson(JsonReader reader, Type objectType, string existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
                return (string)reader.Value;
            if (reader.TokenType == JsonToken.Null)
                return null;
            var token = JToken.Load(reader);
            return token.ToString(Formatting.None);
        }

        /// <summary>
        /// Writes the string value directly to the JSON output.
        /// </summary>
        public override void WriteJson(JsonWriter writer, string value, JsonSerializer serializer)
        {
            writer.WriteValue(value);
        }
    }
}
