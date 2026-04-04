using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using CoClawBro.Data;
using CoClawBro.Diagnostics;
using CoClawBro.Serialization;

namespace CoClawBro.Translation;

/// <summary>
/// Translates an OpenAI SSE stream into Anthropic SSE events.
/// Implements a state machine that tracks content blocks and tool call accumulation.
/// Pure async calculation — reads from a stream, yields events.
/// </summary>
public static class StreamTranslator
{
    /// <summary>
    /// Reads OpenAI SSE chunks from a stream and yields Anthropic SSE event strings.
    /// Each yielded string is a complete "event: ...\ndata: ...\n\n" block.
    /// </summary>
    public static async IAsyncEnumerable<(string Event, OpenAiUsage? Usage)> TranslateAsync(
        Stream openAiStream,
        string requestModel,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var state = new TranslationState(requestModel);
        var reader = new StreamReader(openAiStream, Encoding.UTF8);

        await foreach (var line in ReadLinesAsync(reader, ct))
        {
            if (!line.StartsWith("data: ", StringComparison.Ordinal))
                continue;

            var payload = line.AsSpan(6);

            if (payload is "[DONE]")
            {
                foreach (var ev in state.FinishStream())
                    yield return (ev, null);
                continue;
            }

            OpenAiStreamChunk? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize(payload.ToString(), AppJsonContext.App.OpenAiStreamChunk);
            }
            catch { continue; }

            if (chunk?.Choices is null || chunk.Choices.Count == 0)
                continue;

            var choice = chunk.Choices[0];
            var delta = choice.Delta;

            if (!state.MessageStarted)
                yield return (state.EmitMessageStart(chunk), null);

            if (delta?.Content is not null)
            {
                foreach (var ev in state.HandleTextDelta(delta.Content))
                    yield return (ev, null);
            }

            if (delta?.ToolCalls is not null)
            {
                foreach (var tc in delta.ToolCalls)
                {
                    foreach (var ev in state.HandleToolCallDelta(tc))
                        yield return (ev, null);
                }
            }

            if (choice.FinishReason is not null)
            {
                foreach (var ev in state.HandleFinish(choice.FinishReason, chunk.Usage))
                    yield return (ev, chunk.Usage);
            }
        }
    }

    private static async IAsyncEnumerable<string> ReadLinesAsync(
        StreamReader reader,
        [EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            if (line.Length > 0)
                yield return line;
        }
    }

    /// <summary>
    /// Tracks state across the translation of one SSE stream.
    /// </summary>
    private sealed class TranslationState
    {
        private readonly string _model;
        private static int _msgCounter;
        private readonly string _msgId;

        public bool MessageStarted { get; private set; }
        private int _blockIndex = -1;
        private bool _textBlockOpen;
        private readonly Dictionary<int, ToolCallAccumulator> _toolAccumulators = new();

        public TranslationState(string model)
        {
            _model = model;
            _msgId = $"msg_{Interlocked.Increment(ref _msgCounter):D6}";
        }

        public string EmitMessageStart(OpenAiStreamChunk chunk)
        {
            MessageStarted = true;
            var msg = new AnthropicMessagesResponse(
                Id: _msgId, Type: "message", Role: "assistant",
                Content: [], Model: _model,
                StopSequence: null,
                Usage: new AnthropicUsage(0, 0));
            var ev = new SseMessageStart("message_start", msg);
            return FormatEvent("message_start",
                JsonSerializer.Serialize(ev, AppJsonContext.App.SseMessageStart));
        }

        public IEnumerable<string> HandleTextDelta(string text)
        {
            if (!_textBlockOpen)
            {
                _blockIndex++;
                _textBlockOpen = true;
                var start = new SseContentBlockStart("content_block_start", _blockIndex,
                    new TextBlock(""));
                yield return FormatEvent("content_block_start",
                    JsonSerializer.Serialize(start, AppJsonContext.App.SseContentBlockStart));
            }

            var delta = new SseTextDelta("text_delta", text);
            var blockDelta = new SseContentBlockDelta("content_block_delta", _blockIndex, delta);
            yield return FormatEvent("content_block_delta",
                JsonSerializer.Serialize(blockDelta, AppJsonContext.App.SseContentBlockDelta));
        }

        public IEnumerable<string> HandleToolCallDelta(OpenAiStreamToolCall tc)
        {
            if (!_toolAccumulators.TryGetValue(tc.Index, out var acc))
            {
                acc = new ToolCallAccumulator();
                _toolAccumulators[tc.Index] = acc;
            }

            if (tc.Id is not null) acc.Id = tc.Id;
            if (tc.Function?.Name is not null) acc.Name = tc.Function.Name;
            if (tc.Function?.Arguments is not null) acc.Arguments.Append(tc.Function.Arguments);

            return []; // tool_use blocks are emitted on finish
        }

        public IEnumerable<string> HandleFinish(string finishReason, OpenAiUsage? usage)
        {
            // Close text block if open
            if (_textBlockOpen)
            {
                _textBlockOpen = false;
                yield return FormatEvent("content_block_stop",
                    JsonSerializer.Serialize(new SseContentBlockStop("content_block_stop", _blockIndex),
                        AppJsonContext.App.SseContentBlockStop));
            }

            // Emit accumulated tool_use blocks using the Anthropic streaming protocol:
            // 1) content_block_start with input: {} (empty)
            // 2) content_block_delta with input_json_delta containing the raw arguments
            // 3) content_block_stop
            foreach (var (_, acc) in _toolAccumulators.OrderBy(kv => kv.Key))
            {
                _blockIndex++;
                var rawArgs = acc.Arguments.ToString();
                var toolId = acc.Id ?? $"call_{_blockIndex}";
                var toolName = acc.Name ?? "unknown";

                DebugLogger.Log("TOOL-USE",
                    $"id={toolId} name={toolName} args={Truncate(rawArgs, 500)}");

                // Start with empty input (Anthropic protocol)
                var emptyToolBlock = new ToolUseBlock(toolId, toolName, new Dictionary<string, object?>());
                yield return FormatEvent("content_block_start",
                    JsonSerializer.Serialize(
                        new SseContentBlockStart("content_block_start", _blockIndex, emptyToolBlock),
                        AppJsonContext.App.SseContentBlockStart));

                // Stream the full arguments as an input_json_delta
                if (!string.IsNullOrEmpty(rawArgs))
                {
                    var inputDelta = new SseInputJsonDelta("input_json_delta", rawArgs);
                    var blockDelta = new SseContentBlockDelta("content_block_delta", _blockIndex, inputDelta);
                    yield return FormatEvent("content_block_delta",
                        JsonSerializer.Serialize(blockDelta, AppJsonContext.App.SseContentBlockDelta));
                }

                yield return FormatEvent("content_block_stop",
                    JsonSerializer.Serialize(new SseContentBlockStop("content_block_stop", _blockIndex),
                        AppJsonContext.App.SseContentBlockStop));
            }

            var anthropicUsage = usage is not null
                ? new AnthropicUsage(usage.PromptTokens, usage.CompletionTokens,
                    CacheCreationInputTokens: 0, CacheReadInputTokens: 0)
                : new AnthropicUsage(0, 0, CacheCreationInputTokens: 0, CacheReadInputTokens: 0);

            yield return FormatEvent("message_delta",
                JsonSerializer.Serialize(new SseMessageDelta("message_delta",
                    new SseMessageDeltaPayload(ResponseTranslator.MapStopReason(finishReason)),
                    anthropicUsage), AppJsonContext.App.SseMessageDelta));
        }

        public IEnumerable<string> FinishStream()
        {
            // Close text block if still open
            if (_textBlockOpen)
            {
                _textBlockOpen = false;
                yield return FormatEvent("content_block_stop",
                    JsonSerializer.Serialize(new SseContentBlockStop("content_block_stop", _blockIndex),
                        AppJsonContext.App.SseContentBlockStop));
            }

            yield return FormatEvent("message_stop",
                JsonSerializer.Serialize(new SseMessageStop("message_stop"),
                    AppJsonContext.App.SseMessageStop));
        }

        private static string FormatEvent(string eventType, string data) =>
            $"event: {eventType}\ndata: {data}\n\n";

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : string.Concat(s.AsSpan(0, max), "…");
    }

    private sealed class ToolCallAccumulator
    {
        public string? Id;
        public string? Name;
        public readonly StringBuilder Arguments = new();
    }
}
