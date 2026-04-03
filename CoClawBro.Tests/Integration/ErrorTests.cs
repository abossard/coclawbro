using CoClawBro.Tests.Fixtures;
using System.Text;

namespace CoClawBro.Tests.Integration;

public class ErrorTests
{
    [Test]
    public async Task PostMessages_Upstream429_ReturnsRateLimitError()
    {
        await using var fixture = new ProxyTestFixture();
        await fixture.StartAsync();

        fixture.StubError(429, """{"error":{"message":"rate limited"}}""");

        var request = new StringContent(
            """{"model":"claude-sonnet-4","max_tokens":1024,"messages":[{"role":"user","content":[{"type":"text","text":"Hi"}]}]}""",
            Encoding.UTF8, "application/json");

        var response = await fixture.Client.PostAsync("/v1/messages", request);
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That((int)response.StatusCode).IsEqualTo(429);
        await Assert.That(body).Contains("\"type\":\"error\"");
        await Assert.That(body).Contains("rate_limit_error");
    }

    [Test]
    public async Task PostMessages_Upstream500_ReturnsApiError()
    {
        await using var fixture = new ProxyTestFixture();
        await fixture.StartAsync();

        fixture.StubError(500, """{"error":{"message":"internal error"}}""");

        var request = new StringContent(
            """{"model":"claude-sonnet-4","max_tokens":1024,"messages":[{"role":"user","content":[{"type":"text","text":"Hi"}]}]}""",
            Encoding.UTF8, "application/json");

        var response = await fixture.Client.PostAsync("/v1/messages", request);
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That((int)response.StatusCode).IsEqualTo(500);
        await Assert.That(body).Contains("\"type\":\"error\"");
        await Assert.That(body).Contains("api_error");
    }

    [Test]
    public async Task PostMessages_MalformedBody_Returns400()
    {
        await using var fixture = new ProxyTestFixture();
        await fixture.StartAsync();

        var request = new StringContent(
            "this is not json",
            Encoding.UTF8, "application/json");

        var response = await fixture.Client.PostAsync("/v1/messages", request);
        var body = await response.Content.ReadAsStringAsync();

        // Malformed JSON triggers a server-side exception → 500 with error envelope
        await Assert.That((int)response.StatusCode).IsEqualTo(500);
        await Assert.That(body).Contains("\"type\":\"error\"");
    }
}
