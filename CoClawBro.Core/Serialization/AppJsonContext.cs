using System.Text.Json;
using System.Text.Json.Serialization;
using CoClawBro.Data;

namespace CoClawBro.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false
)]
// Anthropic types
[JsonSerializable(typeof(AnthropicMessagesRequest))]
[JsonSerializable(typeof(AnthropicMessagesResponse))]
[JsonSerializable(typeof(AnthropicCountTokensRequest))]
[JsonSerializable(typeof(AnthropicCountTokensResponse))]
[JsonSerializable(typeof(AnthropicErrorResponse))]
[JsonSerializable(typeof(AnthropicError))]
[JsonSerializable(typeof(AnthropicMessage))]
[JsonSerializable(typeof(AnthropicThinking))]
[JsonSerializable(typeof(AnthropicTool))]
[JsonSerializable(typeof(AnthropicUsage))]
[JsonSerializable(typeof(ContentBlock))]
[JsonSerializable(typeof(TextBlock))]
[JsonSerializable(typeof(ThinkingBlock))]
[JsonSerializable(typeof(ToolUseBlock))]
[JsonSerializable(typeof(ToolResultBlock))]
[JsonSerializable(typeof(List<ContentBlock>))]
// SSE event types
[JsonSerializable(typeof(SseMessageStart))]
[JsonSerializable(typeof(SseContentBlockStart))]
[JsonSerializable(typeof(SseContentBlockDelta))]
[JsonSerializable(typeof(SseContentBlockStop))]
[JsonSerializable(typeof(SseMessageDelta))]
[JsonSerializable(typeof(SseMessageDeltaPayload))]
[JsonSerializable(typeof(SseMessageStop))]
[JsonSerializable(typeof(SseTextDelta))]
[JsonSerializable(typeof(SseThinkingDelta))]
// OpenAI types
[JsonSerializable(typeof(OpenAiChatRequest))]
[JsonSerializable(typeof(OpenAiChatResponse))]
[JsonSerializable(typeof(OpenAiMessage))]
[JsonSerializable(typeof(OpenAiChoice))]
[JsonSerializable(typeof(OpenAiToolCall))]
[JsonSerializable(typeof(OpenAiFunction))]
[JsonSerializable(typeof(OpenAiToolDefinition))]
[JsonSerializable(typeof(OpenAiFunctionDefinition))]
[JsonSerializable(typeof(OpenAiUsage))]
[JsonSerializable(typeof(OpenAiStreamChunk))]
[JsonSerializable(typeof(OpenAiStreamChoice))]
[JsonSerializable(typeof(OpenAiStreamDelta))]
[JsonSerializable(typeof(OpenAiStreamToolCall))]
[JsonSerializable(typeof(OpenAiStreamFunction))]
[JsonSerializable(typeof(OpenAiModelsResponse))]
[JsonSerializable(typeof(OpenAiModelInfo))]
[JsonSerializable(typeof(OpenAiErrorResponse))]
[JsonSerializable(typeof(OpenAiErrorDetail))]
// GitHub types
[JsonSerializable(typeof(DeviceCodeResponse))]
[JsonSerializable(typeof(OAuthTokenResponse))]
[JsonSerializable(typeof(CopilotTokenResponse))]
[JsonSerializable(typeof(PersistedToken))]
[JsonSerializable(typeof(LastModelPrefs))]
// Generic types needed for serialization
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(List<OpenAiToolCall>))]
[JsonSerializable(typeof(List<OpenAiStreamToolCall>))]
[JsonSerializable(typeof(List<OpenAiMessage>))]
public partial class AppJsonContext : JsonSerializerContext
{
    private static AppJsonContext? _default;

    public static AppJsonContext App => _default ??= new AppJsonContext(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    });
}
