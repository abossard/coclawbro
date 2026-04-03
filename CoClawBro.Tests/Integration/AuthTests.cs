using CoClawBro.Tests.Fixtures;
using System.Text;

namespace CoClawBro.Tests.Integration;

public class AuthTests
{
    [Test]
    public async Task PostMessages_NoAuthHeader_StillSucceeds()
    {
        await using var fixture = new ProxyTestFixture();
        await fixture.StartAsync();

        fixture.StubBatchResponse("""{"id":"chatcmpl-1","choices":[{"index":0,"message":{"role":"assistant","content":"OK"},"finish_reason":"stop"}],"model":"gpt-4o","usage":{"prompt_tokens":5,"completion_tokens":2,"total_tokens":7}}""");

        // Create a separate client without auth header
        using var noAuthClient = new HttpClient { BaseAddress = new Uri(fixture.ProxyUrl) };

        var request = new StringContent(
            """{"model":"claude-sonnet-4","max_tokens":1024,"messages":[{"role":"user","content":[{"type":"text","text":"Hi"}]}]}""",
            Encoding.UTF8, "application/json");

        var response = await noAuthClient.PostAsync("/v1/messages", request);
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That((int)response.StatusCode).IsEqualTo(200);
        await Assert.That(body).Contains("\"type\":\"message\"");
    }

    [Test]
    public async Task HealthEndpoint_Returns200WithStatusOk()
    {
        await using var fixture = new ProxyTestFixture();
        await fixture.StartAsync();

        var response = await fixture.Client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That((int)response.StatusCode).IsEqualTo(200);
        await Assert.That(body).Contains("\"status\":\"ok\"");
    }
}
