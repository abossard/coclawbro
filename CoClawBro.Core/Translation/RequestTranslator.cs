using System.Text.Json;
using CoClawBro.Data;
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

        return new OpenAiChatRequest(
            Model: modelOverride ?? ModelMapper.MapToCopilot(request.Model),
            Messages: messages,
            MaxTokens: request.MaxTokens,
            Stream: request.Stream,
            Temperature: request.Temperature,
            Tools: tools?.Count > 0 ? tools : null
        );
    }

    private static IEnumerable<OpenAiMessage> TranslateMessage(AnthropicMessage msg)
    {
        // Separate tool results into individual messages (OpenAI uses role:"tool" per result)
        var toolResults = msg.Content.OfType<ToolResultBlock>().ToList();
        var toolUses = msg.Content.OfType<ToolUseBlock>().ToList();
        var textBlocks = msg.Content.OfType<TextBlock>().ToList();

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
}
