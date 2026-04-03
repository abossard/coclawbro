using System.Text;
using CoClawBro.Translation;

namespace CoClawBro.Tests.Translation;

public class StreamTranslatorTests
{
    private static MemoryStream MakeSseStream(params string[] dataPayloads)
    {
        var sb = new StringBuilder();
        foreach (var payload in dataPayloads)
            sb.AppendLine($"data: {payload}");
        sb.AppendLine("data: [DONE]");
        sb.AppendLine();
        return new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    [Test]
    public async Task TextStream_ProducesCorrectEventSequence()
    {
        var chunk1 = """{"id":"c1","object":"chat.completion.chunk","created":1,"model":"gpt-4o","choices":[{"index":0,"delta":{"role":"assistant","content":"Hello"},"finish_reason":null}]}""";
        var chunk2 = """{"id":"c1","object":"chat.completion.chunk","created":1,"model":"gpt-4o","choices":[{"index":0,"delta":{"content":" world"},"finish_reason":null}]}""";
        var chunkFinish = """{"id":"c1","object":"chat.completion.chunk","created":1,"model":"gpt-4o","choices":[{"index":0,"delta":{},"finish_reason":"stop"}],"usage":{"prompt_tokens":5,"completion_tokens":2,"total_tokens":7}}""";

        using var stream = MakeSseStream(chunk1, chunk2, chunkFinish);
        var events = new List<string>();

        await foreach (var (ev, _) in StreamTranslator.TranslateAsync(stream, "claude-sonnet-4"))
            events.Add(ev);

        // Expected sequence: message_start, content_block_start, delta("Hello"), delta(" world"),
        // content_block_stop (from finish), message_delta, message_stop (from [DONE])
        await Assert.That(events.Count).IsGreaterThanOrEqualTo(6);
        await Assert.That(events[0]).Contains("message_start");
        await Assert.That(events[1]).Contains("content_block_start");
        await Assert.That(events[2]).Contains("text_delta");
        await Assert.That(events[3]).Contains("text_delta");
        await Assert.That(events[4]).Contains("content_block_stop");
        await Assert.That(events[5]).Contains("message_delta");
        await Assert.That(events[^1]).Contains("message_stop");
    }

    [Test]
    public async Task ToolCallAccumulation_ProducesToolUseBlocksOnFinish()
    {
        var chunk1 = """{"id":"c1","object":"chat.completion.chunk","created":1,"model":"gpt-4o","choices":[{"index":0,"delta":{"role":"assistant","tool_calls":[{"index":0,"id":"call_1","type":"function","function":{"name":"search","arguments":""}}]},"finish_reason":null}]}""";
        var chunk2 = """{"id":"c1","object":"chat.completion.chunk","created":1,"model":"gpt-4o","choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"function":{"arguments":"{\"q\":"}}]},"finish_reason":null}]}""";
        var chunk3 = """{"id":"c1","object":"chat.completion.chunk","created":1,"model":"gpt-4o","choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"function":{"arguments":"\"test\"}"}}]},"finish_reason":null}]}""";
        var chunkFinish = """{"id":"c1","object":"chat.completion.chunk","created":1,"model":"gpt-4o","choices":[{"index":0,"delta":{},"finish_reason":"tool_calls"}]}""";

        using var stream = MakeSseStream(chunk1, chunk2, chunk3, chunkFinish);
        var events = new List<string>();

        await foreach (var (ev, _) in StreamTranslator.TranslateAsync(stream, "claude-sonnet-4"))
            events.Add(ev);

        // Should contain content_block_start with tool_use and the accumulated tool call
        var hasToolUse = events.Any(e => e.Contains("tool_use"));
        await Assert.That(hasToolUse).IsTrue();

        var hasSearch = events.Any(e => e.Contains("search"));
        await Assert.That(hasSearch).IsTrue();
    }

    [Test]
    public async Task MalformedJson_IsSkippedGracefully()
    {
        var validChunk = """{"id":"c1","object":"chat.completion.chunk","created":1,"model":"gpt-4o","choices":[{"index":0,"delta":{"role":"assistant","content":"Hi"},"finish_reason":null}]}""";
        var malformed = """not valid json at all""";
        var chunkFinish = """{"id":"c1","object":"chat.completion.chunk","created":1,"model":"gpt-4o","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}""";

        using var stream = MakeSseStream(validChunk, malformed, chunkFinish);
        var events = new List<string>();

        await foreach (var (ev, _) in StreamTranslator.TranslateAsync(stream, "claude-sonnet-4"))
            events.Add(ev);

        // Should still produce valid events despite the malformed line
        await Assert.That(events.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(events[0]).Contains("message_start");
    }

    [Test]
    public async Task Done_TerminatesWithMessageStop()
    {
        var chunk = """{"id":"c1","object":"chat.completion.chunk","created":1,"model":"gpt-4o","choices":[{"index":0,"delta":{"role":"assistant","content":"Done"},"finish_reason":null}]}""";
        var chunkFinish = """{"id":"c1","object":"chat.completion.chunk","created":1,"model":"gpt-4o","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}""";

        using var stream = MakeSseStream(chunk, chunkFinish);
        var events = new List<string>();

        await foreach (var (ev, _) in StreamTranslator.TranslateAsync(stream, "claude-sonnet-4"))
            events.Add(ev);

        await Assert.That(events[^1]).Contains("message_stop");
    }
}
