using System.Text.Json;
using CoClawBro.Data;
using CoClawBro.Diagnostics;
using CoClawBro.Serialization;

namespace CoClawBro.Translation;

/// <summary>
/// Pure calculation: translates Anthropic Messages API requests to OpenAI Chat Completions format.
/// No I/O, no side effects — fully testable.
/// </summary>
public static class RequestTranslator
{
    public static OpenAiChatRequest Translate(AnthropicMessagesRequest request, string? modelOverride = null)
    {
        var messages = new List<OpenAiMessage>();

        // System prompt → system message (Anthropic uses top-level field, OpenAI uses role)
        if (request.System is not null)
            messages.Add(new OpenAiMessage(Role: "system", Content: request.System));

        foreach (var msg in request.Messages)
            messages.AddRange(TranslateMessage(msg));

        var tools = request.Tools?.Select(TranslateTool).ToList();

        // top_k has no OpenAI equivalent — log and drop
        if (request.TopK is not null)
            DebugLogger.Log("TRANSLATE", $"Dropping top_k={request.TopK} (no OpenAI equivalent)");

        return new OpenAiChatRequest(
            Model: modelOverride ?? ModelMapper.MapToCopilot(request.Model),
            Messages: messages,
            MaxTokens: request.MaxTokens,
            Stream: request.Stream,
            Temperature: request.Temperature,
            TopP: request.TopP,
            Stop: request.StopSequences,
            Tools: tools?.Count > 0 ? tools : null,
            ToolChoice: TranslateToolChoice(request.ToolChoice)
        );
    }

    private static IEnumerable<OpenAiMessage> TranslateMessage(AnthropicMessage msg)
    {
        // Separate tool results into individual messages (OpenAI uses role:"tool" per result)
        var toolResults = msg.Content.OfType<ToolResultBlock>().ToList();
        var toolUses = msg.Content.OfType<ToolUseBlock>().ToList();
        var textBlocks = msg.Content.OfType<TextBlock>().ToList();

        // Log skipped block types (thinking, images, etc.) for observability
        var skippedCount = msg.Content.Count - toolResults.Count - toolUses.Count - textBlocks.Count;
        if (skippedCount > 0)
        {
            var skippedTypes = msg.Content
                .Where(b => b is not TextBlock and not ToolUseBlock and not ToolResultBlock)
                .Select(b => b.GetType().Name)
                .Distinct();
            DebugLogger.Log("TRANSLATE", $"Skipping {skippedCount} block(s) in {msg.Role} message: {string.Join(", ", skippedTypes)}");
        }

        if (msg.Role == "assistant")
        {
            // Assistant message may contain text + tool_use blocks
            var content = FlattenTextBlocks(textBlocks);
            var toolCalls = toolUses.Select(TranslateToolUse).ToList();

            yield return new OpenAiMessage(
                Role: "assistant",
                Content: content?.Length > 0 ? content : null,
                ToolCalls: toolCalls.Count > 0 ? toolCalls : null
            );
        }
        else if (msg.Role == "user")
        {
            // User message: text blocks become content, tool_result blocks become separate messages
            if (textBlocks.Count > 0)
            {
                yield return new OpenAiMessage(
                    Role: "user",
                    Content: FlattenTextBlocks(textBlocks)
                );
            }

            foreach (var result in toolResults)
            {
                yield return new OpenAiMessage(
                    Role: "tool",
                    Content: result.Content ?? "",
                    ToolCallId: result.ToolUseId
                );
            }

            // If no text and no tool results, still emit a user message with empty content
            if (textBlocks.Count == 0 && toolResults.Count == 0)
            {
                yield return new OpenAiMessage(Role: "user", Content: "");
            }
        }
    }

    private static string? FlattenTextBlocks(List<TextBlock> blocks)
    {
        if (blocks.Count == 0) return null;
        if (blocks.Count == 1) return blocks[0].Text;
        return string.Join("", blocks.Select(b => b.Text));
    }

    private static OpenAiToolCall TranslateToolUse(ToolUseBlock toolUse)
    {
        var argsJson = toolUse.Input is not null
            ? JsonSerializer.Serialize(toolUse.Input, AppJsonContext.App.DictionaryStringObject)
            : "{}";

        return new OpenAiToolCall(
            Id: toolUse.Id,
            Type: "function",
            Function: new OpenAiFunction(
                Name: toolUse.Name,
                Arguments: argsJson
            )
        );
    }

    private static OpenAiToolDefinition TranslateTool(AnthropicTool tool)
    {
        return new OpenAiToolDefinition(
            Type: "function",
            Function: new OpenAiFunctionDefinition(
                Name: tool.Name,
                Description: tool.Description,
                Parameters: tool.InputSchema
            )
        );
    }

    /// <summary>
    /// Translates Anthropic tool_choice to OpenAI format.
    /// Anthropic: "auto" | {"type":"auto"} | {"type":"any"} | {"type":"tool","name":"X"}
    /// OpenAI:    "auto" | "required" | "none" | {"type":"function","function":{"name":"X"}}
    /// </summary>
    private static object? TranslateToolChoice(JsonElement? toolChoice)
    {
        if (toolChoice is null || toolChoice.Value.ValueKind == JsonValueKind.Undefined
                               || toolChoice.Value.ValueKind == JsonValueKind.Null)
            return null;

        var tc = toolChoice.Value;

        // String form: "auto", "any", "none"
        if (tc.ValueKind == JsonValueKind.String)
        {
            return tc.GetString() switch
            {
                "any" => "required",
                "none" => "none",
                _ => "auto"
            };
        }

        // Object form: {"type":"auto"}, {"type":"any"}, {"type":"tool","name":"X"}
        if (tc.ValueKind == JsonValueKind.Object && tc.TryGetProperty("type", out var typeProp))
        {
            var type = typeProp.GetString();
            if (type == "any")
                return "required";
            if (type == "none")
                return "none";
            if (type == "tool" && tc.TryGetProperty("name", out var nameProp))
            {
                return new Dictionary<string, object?>
                {
                    ["type"] = "function",
                    ["function"] = new Dictionary<string, object?> { ["name"] = nameProp.GetString() }
                };
            }
            return "auto";
        }

        return null;
    }
}
