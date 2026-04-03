using System.Text.Json.Serialization;

namespace CoClawBro.Data;

// --- OpenAI Chat Completions API Data Types ---

// --- Messages ---

public sealed record OpenAiMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string? Content = null,
    [property: JsonPropertyName("tool_calls")] List<OpenAiToolCall>? ToolCalls = null,
    [property: JsonPropertyName("tool_call_id")] string? ToolCallId = null,
    [property: JsonPropertyName("name")] string? Name = null
);

// --- Tool Calls ---

public sealed record OpenAiToolCall(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("function")] OpenAiFunction Function
);

public sealed record OpenAiFunction(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("arguments")] string Arguments
);

// --- Tool Definitions ---

public sealed record OpenAiToolDefinition(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("function")] OpenAiFunctionDefinition Function
);

public sealed record OpenAiFunctionDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("parameters")] Dictionary<string, object?>? Parameters = null
);

// --- Request ---

public sealed record OpenAiChatRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] List<OpenAiMessage> Messages,
    [property: JsonPropertyName("max_tokens")] int? MaxTokens = null,
    [property: JsonPropertyName("stream")] bool Stream = false,
    [property: JsonPropertyName("temperature")] double? Temperature = null,
    [property: JsonPropertyName("tools")] List<OpenAiToolDefinition>? Tools = null
);

// --- Usage ---

public sealed record OpenAiUsage(
    [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
    [property: JsonPropertyName("completion_tokens")] int CompletionTokens,
    [property: JsonPropertyName("total_tokens")] int TotalTokens
);

// --- Response (non-streaming) ---

public sealed record OpenAiChatResponse(
    [property: JsonPropertyName("id")] string? Id = null,
    [property: JsonPropertyName("object")] string? Object = null,
    [property: JsonPropertyName("created")] long? Created = null,
    [property: JsonPropertyName("model")] string? Model = null,
    [property: JsonPropertyName("choices")] List<OpenAiChoice>? Choices = null,
    [property: JsonPropertyName("usage")] OpenAiUsage? Usage = null
);

public sealed record OpenAiChoice(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("message")] OpenAiMessage? Message = null,
    [property: JsonPropertyName("finish_reason")] string? FinishReason = null
);

// --- Streaming Chunk ---

public sealed record OpenAiStreamChunk(
    [property: JsonPropertyName("id")] string? Id = null,
    [property: JsonPropertyName("object")] string? Object = null,
    [property: JsonPropertyName("created")] long? Created = null,
    [property: JsonPropertyName("model")] string? Model = null,
    [property: JsonPropertyName("choices")] List<OpenAiStreamChoice>? Choices = null,
    [property: JsonPropertyName("usage")] OpenAiUsage? Usage = null
);

public sealed record OpenAiStreamChoice(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("delta")] OpenAiStreamDelta? Delta = null,
    [property: JsonPropertyName("finish_reason")] string? FinishReason = null
);

public sealed record OpenAiStreamDelta(
    [property: JsonPropertyName("role")] string? Role = null,
    [property: JsonPropertyName("content")] string? Content = null,
    [property: JsonPropertyName("tool_calls")] List<OpenAiStreamToolCall>? ToolCalls = null
);

public sealed record OpenAiStreamToolCall(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("id")] string? Id = null,
    [property: JsonPropertyName("type")] string? Type = null,
    [property: JsonPropertyName("function")] OpenAiStreamFunction? Function = null
);

public sealed record OpenAiStreamFunction(
    [property: JsonPropertyName("name")] string? Name = null,
    [property: JsonPropertyName("arguments")] string? Arguments = null
);

// --- Models endpoint ---

public sealed record OpenAiModelsResponse(
    [property: JsonPropertyName("data")] List<OpenAiModelInfo>? Data = null
);

public sealed record OpenAiModelInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("object")] string? Object = null,
    [property: JsonPropertyName("created")] long? Created = null,
    [property: JsonPropertyName("owned_by")] string? OwnedBy = null
);

// --- Error ---

public sealed record OpenAiErrorResponse(
    [property: JsonPropertyName("error")] OpenAiErrorDetail? Error = null
);

public sealed record OpenAiErrorDetail(
    [property: JsonPropertyName("message")] string? Message = null,
    [property: JsonPropertyName("type")] string? Type = null,
    [property: JsonPropertyName("code")] string? Code = null
);
