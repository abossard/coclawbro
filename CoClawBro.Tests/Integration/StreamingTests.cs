using CoClawBro.Tests.Fixtures;
using System.Text;

namespace CoClawBro.Tests.Integration;

public class StreamingTests
{
    [Test]
    public async Task PostMessages_Streaming_ReturnsEventStream()
    {
        await using var fixture = new ProxyTestFixture();
        await fixture.StartAsync();

        fixture.StubStreamingResponse(
            """{"id":"chatcmpl-1","choices":[{"index":0,"delta":{"role":"assistant","content":"Hi"},"finish_reason":null}]}""",
            """{"id":"chatcmpl-1","choices":[{"index":0,"delta":{"content":" there"},"finish_reason":null}]}""",
            """{"id":"chatcmpl-1","choices":[{"index":0,"delta":{},"finish_reason":"stop"}],"usage":{"prompt_tokens":10,"completion_tokens":5,"total_tokens":15}}"""
        );

        var request = new StringContent(
            """{"model":"claude-sonnet-4","max_tokens":1024,"stream":true,"messages":[{"role":"user","content":[{"type":"text","text":"Hello"}]}]}""",
            Encoding.UTF8, "application/json");

        var response = await fixture.Client.PostAsync("/v1/messages", request);

        await Assert.That(response.Content.Headers.ContentType!.MediaType).IsEqualTo("text/event-stream");
    }

    [Test]
    public async Task PostMessages_Streaming_EmitsCorrectEventSequence()
    {
        await using var fixture = new ProxyTestFixture();
        await fixture.StartAsync();

        fixture.StubStreamingResponse(
            """{"id":"chatcmpl-1","choices":[{"index":0,"delta":{"role":"assistant","content":"Hello"},"finish_reason":null}]}""",
            """{"id":"chatcmpl-1","choices":[{"index":0,"delta":{},"finish_reason":"stop"}],"usage":{"prompt_tokens":8,"completion_tokens":3,"total_tokens":11}}"""
        );

        var request = new StringContent(
            """{"model":"claude-sonnet-4","max_tokens":1024,"stream":true,"messages":[{"role":"user","content":[{"type":"text","text":"Hi"}]}]}""",
            Encoding.UTF8, "application/json");

        var response = await fixture.Client.PostAsync("/v1/messages", request);
        var body = await response.Content.ReadAsStringAsync();

        // Verify the SSE event types appear in order
        await Assert.That(body).Contains("event: message_start");
        await Assert.That(body).Contains("event: content_block_start");
        await Assert.That(body).Contains("event: content_block_delta");
        await Assert.That(body).Contains("event: content_block_stop");
        await Assert.That(body).Contains("event: message_delta");
        await Assert.That(body).Contains("event: message_stop");

        // Verify order: message_start comes before content_block_start
        var msgStartIdx = body.IndexOf("event: message_start");
        var blockStartIdx = body.IndexOf("event: content_block_start");
        var blockStopIdx = body.IndexOf("event: content_block_stop");
        var msgDeltaIdx = body.IndexOf("event: message_delta");
        var msgStopIdx = body.IndexOf("event: message_stop");

        await Assert.That(msgStartIdx).IsLessThan(blockStartIdx);
        await Assert.That(blockStartIdx).IsLessThan(blockStopIdx);
        await Assert.That(blockStopIdx).IsLessThan(msgDeltaIdx);
        await Assert.That(msgDeltaIdx).IsLessThan(msgStopIdx);
    }
}
