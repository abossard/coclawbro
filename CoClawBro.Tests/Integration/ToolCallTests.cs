using CoClawBro.Tests.Fixtures;
using System.Text;

namespace CoClawBro.Tests.Integration;

public class ToolCallTests
{
    [Test]
    public async Task PostMessages_ToolCallResponse_ReturnsToolUseBlock()
    {
        await using var fixture = new ProxyTestFixture();
        await fixture.StartAsync();

        fixture.StubBatchResponse("""{"id":"chatcmpl-1","choices":[{"index":0,"message":{"role":"assistant","content":null,"tool_calls":[{"id":"call_1","type":"function","function":{"name":"get_weather","arguments":"{\"location\":\"NYC\"}"}}]},"finish_reason":"tool_calls"}],"model":"gpt-4o","usage":{"prompt_tokens":10,"completion_tokens":20,"total_tokens":30}}""");

        var request = new StringContent(
            """{"model":"claude-sonnet-4","max_tokens":1024,"messages":[{"role":"user","content":[{"type":"text","text":"What is the weather in NYC?"}]}],"tools":[{"name":"get_weather","description":"Get weather","input_schema":{"type":"object","properties":{"location":{"type":"string"}}}}]}""",
            Encoding.UTF8, "application/json");

        var response = await fixture.Client.PostAsync("/v1/messages", request);
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That((int)response.StatusCode).IsEqualTo(200);
        await Assert.That(body).Contains("\"type\":\"tool_use\"");
        await Assert.That(body).Contains("\"name\":\"get_weather\"");
        await Assert.That(body).Contains("call_1");
        await Assert.That(body).Contains("\"stop_reason\":\"tool_use\"");
    }
}
