using System.Text.Json;
using System.Text.Json.Serialization;

namespace CoClawBro.Serialization;

/// <summary>
/// Handles the Anthropic <c>system</c> field which the API allows in two forms:
/// <list type="bullet">
///   <item>Plain string: <c>"You are helpful"</c></item>
///   <item>Array of content blocks: <c>[{"type":"text","text":"You are helpful"}]</c></item>
/// </list>
/// Both are normalised to a plain string on read. Write always emits a string.
/// </summary>
public sealed class SystemContentConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;

            case JsonTokenType.String:
                return reader.GetString();

            case JsonTokenType.StartArray:
                return ReadBlockArray(ref reader);

            default:
                reader.Skip();
                return null;
        }
    }

    private static string? ReadBlockArray(ref Utf8JsonReader reader)
    {
        var parts = new List<string>();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                continue;
            }

            string? blockType = null;
            string? blockText = null;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                    continue;

                var name = reader.GetString();
                if (!reader.Read()) break;

                if (name == "type")
                    blockType = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                else if (name == "text")
                    blockText = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                else
                    reader.Skip();
            }

            if (blockType == "text" && blockText is not null)
                parts.Add(blockText);
        }

        return parts.Count > 0 ? string.Join("\n\n", parts) : null;
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value);
    }
}
