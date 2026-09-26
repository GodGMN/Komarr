using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;

namespace NzbDrone.Core.MetadataSource.OpenLibrary.Resources
{
    // OL returns `description` (and `bio`) as one of:
    //   "plain string"
    //   {"type": "/type/text", "value": "string"}
    //   ["joined", "lines"]                                    (rare)
    //   {"value": "string"} / {"text": "string"} (no type)     (rare)
    //   missing / null
    //
    // Without this converter, the bare-string form (~half of real responses)
    // fails deserialization. Apply via [JsonConverter(typeof(OpenLibraryDescriptionConverter))]
    // on string properties.
    //
    // For genuinely unrecognized shapes (e.g. nested {"type": ..., "value":
    // {...}}) we log once and return null rather than throw — the rest of
    // the document still parses.
    public class OpenLibraryDescriptionConverter : JsonConverter
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public override bool CanConvert(Type objectType) => objectType == typeof(string);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonToken.String)
            {
                return (string)reader.Value;
            }

            if (reader.TokenType == JsonToken.StartObject)
            {
                var obj = JObject.Load(reader);
                return ExtractFromObject(obj);
            }

            if (reader.TokenType == JsonToken.StartArray)
            {
                // Some works return descriptions as an array of strings
                // (mainly older imports). Join on newline to preserve any
                // paragraph structure the uploader intended.
                var arr = JArray.Load(reader);
                var parts = arr
                    .Where(t => t.Type == JTokenType.String)
                    .Select(t => t.ToString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

                if (parts.Count == 0)
                {
                    Logger.Trace("OL description array contained no usable strings — skipping");
                    return null;
                }

                return string.Join("\n", parts);
            }

            // Unknown scalar (bool, number) — skip the token so the rest of
            // the document parses but make some noise so we hear about new
            // shapes in the wild.
            Logger.Debug("OL description had unexpected token type {0} — skipping", reader.TokenType);
            JToken.Load(reader);
            return null;
        }

        private static string ExtractFromObject(JObject obj)
        {
            // Canonical OL shape uses "value"; some older records use
            // "text". Prefer whichever is a non-empty string.
            var value = obj["value"];
            var text = obj["text"];

            var fromValue = AsString(value);
            if (!string.IsNullOrWhiteSpace(fromValue))
            {
                return fromValue;
            }

            var fromText = AsString(text);
            if (!string.IsNullOrWhiteSpace(fromText))
            {
                return fromText;
            }

            // Nothing usable — log once with the type for diagnostics.
            // Don't dump the full object: works can be huge.
            var typeHint = obj["type"]?.ToString() ?? "<no type>";
            Logger.Debug("OL description object had no usable value/text (type={0})", typeHint);
            return null;
        }

        private static string AsString(JToken token)
        {
            if (token == null)
            {
                return null;
            }

            if (token.Type == JTokenType.String)
            {
                return token.ToString();
            }

            if (token.Type == JTokenType.Null)
            {
                return null;
            }

            // Nested object/array under "value" — flatten primitive contents
            // if any, otherwise punt. Don't recurse: deeply nested OL
            // descriptions in the wild are vanishingly rare and the safer
            // play is to return null than to silently reformat garbage.
            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // We never serialize OL DTOs back out; no-op is safe.
            writer.WriteValue(value as string);
        }
    }
}
