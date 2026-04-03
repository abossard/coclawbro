using CoClawBro.Data;

namespace CoClawBro.Tests.Data;

public class ModelMapperTests
{
    [Test]
    [Arguments("claude-sonnet-4", "claude-sonnet-4")]
    [Arguments("claude-sonnet-4-20250514", "claude-sonnet-4")]
    [Arguments("claude-haiku-4.5", "gpt-4o")]
    [Arguments("claude-opus-4", "claude-opus-4")]
    [Arguments("gpt-4o", "gpt-4o")]
    public async Task DefaultMappings_MapCorrectly(string input, string expected)
    {
        var result = ModelMapper.MapToCopilot(input);

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task UnknownModel_PassesThrough()
    {
        var result = ModelMapper.MapToCopilot("some-unknown-model-xyz");

        await Assert.That(result).IsEqualTo("some-unknown-model-xyz");
    }

    [Test]
    public async Task GlobalOverrideParameter_TakesPrecedence()
    {
        var result = ModelMapper.MapToCopilot("claude-sonnet-4", globalOverride: "my-custom-model");

        await Assert.That(result).IsEqualTo("my-custom-model");
    }
}
