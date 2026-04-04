using System.Text.Json;
using CoClawBro.Data;
using CoClawBro.Serialization;

namespace CoClawBro.Translation;

/// <summary>
/// Pure calculation: translates OpenAI Chat Completions responses to Anthropic Messages API format.
/// No I/O, no side effects — fully testable.
/// </summary>
public static class ResponseTranslator
{
    private static int _messageCounter;

    public static AnthropicMessagesResponse Translate(OpenAiChatResponse response, string requestModel)
    {
        var choice = response.Choices?.FirstOrDefault();
        var content = new List<ContentBlock>();

        if (choice?.Message is not null)
        {
            // Text content
            if (choice.Message.Content is not null)
                content.Add(new TextBlock(choice.Message.Content));

            // Tool calls → tool_use blocks
            if (choice.Message.ToolCalls is not null)
            {
                foreach (var tc in choice.Message.ToolCalls)
                    content.Add(TranslateToolCall(tc));
            }
        }

        if (content.Count == 0)
            content.Add(new TextBlock(""));

        return new AnthropicMessagesResponse(
            Id: $"msg_{Interlocked.Increment(ref _messageCounter):D6}",
            Type: "message",
            Role: "assistant",
            Content: content,
            Model: requestModel,
            StopReason: MapStopReason(choice?.FinishReason),
            StopSequence: null,
            Usage: MapUsage(response.Usage)
        );
    }

    public static AnthropicErrorResponse TranslateError(int statusCode, string? message = null)
    {
        var (type, errorType) = statusCode switch
        {
            400 => ("error", "invalid_request_error"),
            401 => ("error", "authentication_error"),
            403 => ("error", "permission_error"),
            404 => ("error", "not_found_error"),
            429 => ("error", "rate_limit_error"),
            >= 500 => ("error", "api_error"),
            _ => ("error", "api_error")
        };

        return new AnthropicErrorResponse(
            Type: type,
            Error: new AnthropicError(
                Type: errorType,
                Message: message ?? $"Upstream error (HTTP {statusCode})"
            )
        );
    }

    private static ToolUseBlock TranslateToolCall(OpenAiToolCall tc)
    {
        Dictionary<string, object?>? input = null;
        try
        {
            if (!string.IsNullOrEmpty(tc.Function.Arguments))
                input = JsonSerializer.Deserialize(tc.Function.Arguments, AppJsonContext.App.DictionaryStringObject);
        }
        catch
        {
            input = new() { ["_raw"] = tc.Function.Arguments };
        }

        return new ToolUseBlock(
            Id: tc.Id,
            Name: tc.Function.Name,
            Input: input
        );
    }

    public static string MapStopReason(string? finishReason) => finishReason switch
    {
        "stop" => "end_turn",
        "length" => "max_tokens",
        "tool_calls" => "tool_use",
        "content_filter" => "end_turn",
        null => "end_turn",
        _ => "end_turn"
    };

    public static AnthropicUsage? MapUsage(OpenAiUsage? usage)
    {
        if (usage is null) return null;
        return new AnthropicUsage(
            InputTokens: usage.PromptTokens,
            OutputTokens: usage.CompletionTokens,
            CacheCreationInputTokens: 0,
            CacheReadInputTokens: 0
        );
    }
}
