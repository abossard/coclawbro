using System.Text.Json;
using System.Text.Json.Serialization;
using CoClawBro.Data;

namespace CoClawBro.Serialization;

/// <summary>
/// Handles the Anthropic <c>content</c> field on messages, which the API allows in two forms:
/// <list type="bullet">
///   <item>Plain string: <c>"Hello"</c></item>
///   <item>Array of content blocks: <c>[{"type":"text","text":"Hello"}]</c></item>
/// </list>
/// A plain string is normalised to <c>[TextBlock("Hello")]</c> on read.
/// Unknown or unrecognised content block types are silently skipped.
/// Write always emits an array of content blocks.
/// </summary>
public sealed class MessageContentConverter : JsonConverter<List<ContentBlock>>
{
    public override List<ContentBlock>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var text = reader.GetString() ?? "";
            return [new TextBlock(text)];
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return [];
        }

        var blocks = new List<ContentBlock>();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                continue;
            }

            // Parse each element as a raw JsonElement to inspect the "type" field
            // before dispatching to the correct concrete type. This avoids the
            // polymorphic deserializer which throws on missing/unknown type discriminators.
            var element = JsonElement.ParseValue(ref reader);
            var block = DeserializeBlock(element, options);
            if (block is not null)
                blocks.Add(block);
        }

        return blocks;
    }

    private static ContentBlock? DeserializeBlock(JsonElement element, JsonSerializerOptions options)
    {
        if (!element.TryGetProperty("type", out var typeProp))
            return null;

        var type = typeProp.GetString();
        var raw = element.GetRawText();

        try
        {
            return type switch
            {
                "text" => JsonSerializer.Deserialize(raw, AppJsonContext.App.TextBlock),
                "thinking" => JsonSerializer.Deserialize(raw, AppJsonContext.App.ThinkingBlock),
                "redacted_thinking" => JsonSerializer.Deserialize(raw, AppJsonContext.App.RedactedThinkingBlock),
                "tool_use" => JsonSerializer.Deserialize(raw, AppJsonContext.App.ToolUseBlock),
                "tool_result" => JsonSerializer.Deserialize(raw, AppJsonContext.App.ToolResultBlock),
                "image" => JsonSerializer.Deserialize(raw, AppJsonContext.App.ImageBlock),
                _ => null // Unknown block type — skip gracefully
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, List<ContentBlock> value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }
}
