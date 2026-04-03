using CoClawBro.Data;
using CoClawBro.Translation;

namespace CoClawBro.Tests.Translation;

public class RequestTranslatorTests
{
    [Test]
    public async Task SimpleText_TranslatesToOpenAiUserMessage()
    {
        var request = new AnthropicMessagesRequest(
            Model: "claude-sonnet-4",
            MaxTokens: 100,
            Messages: [new AnthropicMessage("user", [new TextBlock("Hello world")])]
        );

        var result = RequestTranslator.Translate(request);

        await Assert.That(result.Messages).Count().IsEqualTo(1);
        await Assert.That(result.Messages[0].Role).IsEqualTo("user");
        await Assert.That(result.Messages[0].Content).IsEqualTo("Hello world");
    }

    [Test]
    public async Task SystemPrompt_BecomesFirstSystemMessage()
    {
        var request = new AnthropicMessagesRequest(
            Model: "claude-sonnet-4",
            MaxTokens: 100,
            Messages: [new AnthropicMessage("user", [new TextBlock("Hi")])],
            System: "You are a helpful assistant"
        );

        var result = RequestTranslator.Translate(request);

        await Assert.That(result.Messages).Count().IsEqualTo(2);
        await Assert.That(result.Messages[0].Role).IsEqualTo("system");
        await Assert.That(result.Messages[0].Content).IsEqualTo("You are a helpful assistant");
        await Assert.That(result.Messages[1].Role).IsEqualTo("user");
    }

    [Test]
    public async Task MultiTurn_PreservesMessageOrdering()
    {
        var request = new AnthropicMessagesRequest(
            Model: "claude-sonnet-4",
            MaxTokens: 100,
            Messages:
            [
                new AnthropicMessage("user", [new TextBlock("Hello")]),
                new AnthropicMessage("assistant", [new TextBlock("Hi there")]),
                new AnthropicMessage("user", [new TextBlock("How are you?")])
            ]
        );

        var result = RequestTranslator.Translate(request);

        await Assert.That(result.Messages).Count().IsEqualTo(3);
        await Assert.That(result.Messages[0].Role).IsEqualTo("user");
        await Assert.That(result.Messages[0].Content).IsEqualTo("Hello");
        await Assert.That(result.Messages[1].Role).IsEqualTo("assistant");
        await Assert.That(result.Messages[1].Content).IsEqualTo("Hi there");
        await Assert.That(result.Messages[2].Role).IsEqualTo("user");
        await Assert.That(result.Messages[2].Content).IsEqualTo("How are you?");
    }

    [Test]
    public async Task ToolUseBlock_TranslatesToOpenAiToolCalls()
    {
        var input = new Dictionary<string, object?> { ["city"] = "NYC" };
        var request = new AnthropicMessagesRequest(
            Model: "claude-sonnet-4",
            MaxTokens: 100,
            Messages:
            [
                new AnthropicMessage("assistant",
                [
                    new ToolUseBlock("call_1", "get_weather", input)
                ])
            ]
        );

        var result = RequestTranslator.Translate(request);

        await Assert.That(result.Messages).Count().IsEqualTo(1);
        await Assert.That(result.Messages[0].Role).IsEqualTo("assistant");
        await Assert.That(result.Messages[0].ToolCalls).IsNotNull();
        await Assert.That(result.Messages[0].ToolCalls!).Count().IsEqualTo(1);
        await Assert.That(result.Messages[0].ToolCalls![0].Id).IsEqualTo("call_1");
        await Assert.That(result.Messages[0].ToolCalls![0].Type).IsEqualTo("function");
        await Assert.That(result.Messages[0].ToolCalls![0].Function.Name).IsEqualTo("get_weather");
    }

    [Test]
    public async Task ToolResultBlock_TranslatesToToolRoleMessage()
    {
        var request = new AnthropicMessagesRequest(
            Model: "claude-sonnet-4",
            MaxTokens: 100,
            Messages:
            [
                new AnthropicMessage("user",
                [
                    new ToolResultBlock("call_1", Content: "Sunny, 72°F")
                ])
            ]
        );

        var result = RequestTranslator.Translate(request);

        await Assert.That(result.Messages).Count().IsEqualTo(1);
        await Assert.That(result.Messages[0].Role).IsEqualTo("tool");
        await Assert.That(result.Messages[0].Content).IsEqualTo("Sunny, 72°F");
        await Assert.That(result.Messages[0].ToolCallId).IsEqualTo("call_1");
    }

    [Test]
    public async Task ModelOverride_IsApplied()
    {
        var request = new AnthropicMessagesRequest(
            Model: "claude-sonnet-4",
            MaxTokens: 100,
            Messages: [new AnthropicMessage("user", [new TextBlock("Hi")])]
        );

        var result = RequestTranslator.Translate(request, "custom-model-override");

        await Assert.That(result.Model).IsEqualTo("custom-model-override");
    }

    [Test]
    public async Task ToolDefinitions_AreTranslated()
    {
        var schema = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?> { ["q"] = new Dictionary<string, object?> { ["type"] = "string" } }
        };
        var request = new AnthropicMessagesRequest(
            Model: "claude-sonnet-4",
            MaxTokens: 100,
            Messages: [new AnthropicMessage("user", [new TextBlock("Search for cats")])],
            Tools:
            [
                new AnthropicTool("web_search", Description: "Search the web", InputSchema: schema)
            ]
        );

        var result = RequestTranslator.Translate(request);

        await Assert.That(result.Tools).IsNotNull();
        await Assert.That(result.Tools!).Count().IsEqualTo(1);
        await Assert.That(result.Tools![0].Type).IsEqualTo("function");
        await Assert.That(result.Tools![0].Function.Name).IsEqualTo("web_search");
        await Assert.That(result.Tools![0].Function.Description).IsEqualTo("Search the web");
        await Assert.That(result.Tools![0].Function.Parameters).IsNotNull();
    }

    [Test]
    public async Task EmptyUserMessage_ProducesEmptyContent()
    {
        var request = new AnthropicMessagesRequest(
            Model: "claude-sonnet-4",
            MaxTokens: 100,
            Messages: [new AnthropicMessage("user", [])]
        );

        var result = RequestTranslator.Translate(request);

        await Assert.That(result.Messages).Count().IsEqualTo(1);
        await Assert.That(result.Messages[0].Role).IsEqualTo("user");
        await Assert.That(result.Messages[0].Content).IsEqualTo("");
    }

    [Test]
    public async Task TemperatureMaxTokensStream_ArePreserved()
    {
        var request = new AnthropicMessagesRequest(
            Model: "claude-sonnet-4",
            MaxTokens: 500,
            Messages: [new AnthropicMessage("user", [new TextBlock("Hi")])],
            Stream: true,
            Temperature: 0.7
        );

        var result = RequestTranslator.Translate(request);

        await Assert.That(result.MaxTokens).IsEqualTo(500);
        await Assert.That(result.Stream).IsTrue();
        await Assert.That(result.Temperature).IsEqualTo(0.7);
    }
}
