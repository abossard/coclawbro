using System.Text.Json.Serialization;
using CoClawBro.Serialization;

namespace CoClawBro.Data;

// --- Content Blocks (Anthropic uses typed content blocks, not plain strings) ---

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextBlock), "text")]
[JsonDerivedType(typeof(ThinkingBlock), "thinking")]
[JsonDerivedType(typeof(ToolUseBlock), "tool_use")]
[JsonDerivedType(typeof(ToolResultBlock), "tool_result")]
public abstract record ContentBlock;

public sealed record TextBlock(
    [property: JsonPropertyName("text")] string Text
) : ContentBlock;

public sealed record ThinkingBlock(
    [property: JsonPropertyName("thinking")] string Thinking,
    [property: JsonPropertyName("signature")] string? Signature = null
) : ContentBlock;

public sealed record ToolUseBlock(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("input")] Dictionary<string, object?>? Input = null
) : ContentBlock;

public sealed record ToolResultBlock(
    [property: JsonPropertyName("tool_use_id")] string ToolUseId,
    [property: JsonPropertyName("content")] string? Content = null,
    [property: JsonPropertyName("is_error")] bool? IsError = null
) : ContentBlock;

// --- Messages ---

public sealed record AnthropicMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] List<ContentBlock> Content
);

// --- Thinking Config (in request body) ---

public sealed record AnthropicThinking(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("budget_tokens")] int? BudgetTokens = null
);

// --- Tool Definition ---

public sealed record AnthropicTool(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("input_schema")] Dictionary<string, object?>? InputSchema = null
);

// --- Request ---

public sealed record AnthropicMessagesRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("max_tokens")] int MaxTokens,
    [property: JsonPropertyName("messages")] List<AnthropicMessage> Messages,
    [property: JsonPropertyName("system")]
    [property: JsonConverter(typeof(SystemContentConverter))] string? System = null,
    [property: JsonPropertyName("stream")] bool Stream = false,
    [property: JsonPropertyName("thinking")] AnthropicThinking? Thinking = null,
    [property: JsonPropertyName("tools")] List<AnthropicTool>? Tools = null,
    [property: JsonPropertyName("temperature")] double? Temperature = null
);

// --- Usage ---

public sealed record AnthropicUsage(
    [property: JsonPropertyName("input_tokens")] int InputTokens,
    [property: JsonPropertyName("output_tokens")] int OutputTokens
);

// --- Response (non-streaming) ---

public sealed record AnthropicMessagesResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] List<ContentBlock> Content,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("stop_reason")] string? StopReason = null,
    [property: JsonPropertyName("usage")] AnthropicUsage? Usage = null
);

// --- Count Tokens ---

public sealed record AnthropicCountTokensRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] List<AnthropicMessage> Messages,
    [property: JsonPropertyName("system")]
    [property: JsonConverter(typeof(SystemContentConverter))] string? System = null
);

public sealed record AnthropicCountTokensResponse(
    [property: JsonPropertyName("input_tokens")] int InputTokens
);

// --- Error ---

public sealed record AnthropicError(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("message")] string Message
);

public sealed record AnthropicErrorResponse(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("error")] AnthropicError Error
);

// --- SSE Event Types (for streaming responses) ---

public sealed record AnthropicSseEvent(string EventType, string Data);

public sealed record SseMessageStart(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("message")] AnthropicMessagesResponse Message
);

public sealed record SseContentBlockStart(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("content_block")] ContentBlock ContentBlock
);

public sealed record SseTextDelta(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string Text
);

public sealed record SseThinkingDelta(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("thinking")] string Thinking
);

public sealed record SseContentBlockDelta(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("delta")] object Delta
);

public sealed record SseContentBlockStop(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("index")] int Index
);

public sealed record SseMessageDeltaPayload(
    [property: JsonPropertyName("stop_reason")] string? StopReason = null
);

public sealed record SseMessageDelta(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("delta")] SseMessageDeltaPayload Delta,
    [property: JsonPropertyName("usage")] AnthropicUsage? Usage = null
);

public sealed record SseMessageStop(
    [property: JsonPropertyName("type")] string Type
);
