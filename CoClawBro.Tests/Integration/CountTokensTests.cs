using CoClawBro.Tests.Fixtures;
using System.Text;

namespace CoClawBro.Tests.Integration;

public class CountTokensTests
{
    [Test]
    public async Task CountTokens_ReturnsInputTokens()
    {
        await using var fixture = new ProxyTestFixture();
        await fixture.StartAsync();

        var request = new StringContent(
            """{"model":"claude-sonnet-4","messages":[{"role":"user","content":[{"type":"text","text":"Hello world, this is a test message for token counting."}]}]}""",
            Encoding.UTF8, "application/json");

        var response = await fixture.Client.PostAsync("/v1/messages/count_tokens", request);
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That((int)response.StatusCode).IsEqualTo(200);
        await Assert.That(body).Contains("\"input_tokens\":");
        // Body is ~140 chars, heuristic is length/4, so should be > 0
        await Assert.That(body).DoesNotContain("\"input_tokens\":0");
    }
}
