using CoClawBro.Tests.Fixtures;
using System.Text;

namespace CoClawBro.Tests.Integration;

public class BatchTests
{
    [Test]
    public async Task PostMessages_BatchRequest_ReturnsAnthropicResponse()
    {
        await using var fixture = new ProxyTestFixture();
        await fixture.StartAsync();

        fixture.StubBatchResponse("""{"id":"chatcmpl-1","choices":[{"index":0,"message":{"role":"assistant","content":"Hello!"},"finish_reason":"stop"}],"model":"gpt-4o","usage":{"prompt_tokens":10,"completion_tokens":5,"total_tokens":15}}""");

        var request = new StringContent(
            """{"model":"claude-sonnet-4-20250514","max_tokens":1024,"messages":[{"role":"user","content":[{"type":"text","text":"Hi"}]}]}""",
            Encoding.UTF8, "application/json");

        var response = await fixture.Client.PostAsync("/v1/messages", request);
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That((int)response.StatusCode).IsEqualTo(200);
        await Assert.That(body).Contains("\"type\":\"message\"");
        await Assert.That(body).Contains("\"role\":\"assistant\"");
        await Assert.That(body).Contains("Hello!");
    }

    [Test]
    public async Task PostMessages_BatchRequest_MapsModel()
    {
        await using var fixture = new ProxyTestFixture();
        await fixture.StartAsync();

        fixture.StubBatchResponse("""{"id":"chatcmpl-2","choices":[{"index":0,"message":{"role":"assistant","content":"Response"},"finish_reason":"stop"}],"model":"gpt-4o","usage":{"prompt_tokens":5,"completion_tokens":3,"total_tokens":8}}""");

        var request = new StringContent(
            """{"model":"claude-sonnet-4-20250514","max_tokens":512,"messages":[{"role":"user","content":[{"type":"text","text":"Test"}]}]}""",
            Encoding.UTF8, "application/json");

        var response = await fixture.Client.PostAsync("/v1/messages", request);
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That((int)response.StatusCode).IsEqualTo(200);
        // The proxy should map the model; response should contain a model field
        await Assert.That(body).Contains("\"model\":");
    }

    [Test]
    public async Task PostMessages_BatchRequest_IncludesUsageStats()
    {
        await using var fixture = new ProxyTestFixture();
        await fixture.StartAsync();

        fixture.StubBatchResponse("""{"id":"chatcmpl-3","choices":[{"index":0,"message":{"role":"assistant","content":"Stats"},"finish_reason":"stop"}],"model":"gpt-4o","usage":{"prompt_tokens":12,"completion_tokens":7,"total_tokens":19}}""");

        var request = new StringContent(
            """{"model":"claude-sonnet-4","max_tokens":256,"messages":[{"role":"user","content":[{"type":"text","text":"Hello"}]}]}""",
            Encoding.UTF8, "application/json");

        var response = await fixture.Client.PostAsync("/v1/messages", request);
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That((int)response.StatusCode).IsEqualTo(200);
        await Assert.That(body).Contains("\"usage\":");
        await Assert.That(body).Contains("\"input_tokens\":");
        await Assert.That(body).Contains("\"output_tokens\":");
    }
}
