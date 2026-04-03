using CoClawBro.Tests.Fixtures;

namespace CoClawBro.Tests.Integration;

public class ModelsTests
{
    [Test]
    public async Task GetModels_ReturnsPassthroughResponse()
    {
        await using var fixture = new ProxyTestFixture();
        await fixture.StartAsync();

        fixture.StubModelsResponse("""{"data":[{"id":"gpt-4o","object":"model"},{"id":"gpt-4.1","object":"model"}]}""");

        var response = await fixture.Client.GetAsync("/v1/models");
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That((int)response.StatusCode).IsEqualTo(200);
        await Assert.That(body).Contains("gpt-4o");
        await Assert.That(body).Contains("gpt-4.1");
    }
}
