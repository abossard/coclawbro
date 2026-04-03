using CoClawBro.Data;
using CoClawBro.Translation;

namespace CoClawBro.Tests.Translation;

public class ResponseTranslatorTests
{
    [Test]
    public async Task TextResponse_TranslatesToTextBlock()
    {
        var response = new OpenAiChatResponse(
            Id: "chatcmpl-1",
            Choices:
            [
                new OpenAiChoice(0,
                    Message: new OpenAiMessage("assistant", Content: "Hello world"),
                    FinishReason: "stop")
            ]
        );

        var result = ResponseTranslator.Translate(response, "claude-sonnet-4");

        await Assert.That(result.Content).Count().IsEqualTo(1);
        var textBlock = result.Content[0] as TextBlock;
        await Assert.That(textBlock).IsNotNull();
        await Assert.That(textBlock!.Text).IsEqualTo("Hello world");
        await Assert.That(result.Role).IsEqualTo("assistant");
        await Assert.That(result.Type).IsEqualTo("message");
    }

    [Test]
    public async Task ToolCalls_TranslateToToolUseBlocks()
    {
        var response = new OpenAiChatResponse(
            Id: "chatcmpl-2",
            Choices:
            [
                new OpenAiChoice(0,
                    Message: new OpenAiMessage("assistant",
                        ToolCalls:
                        [
                            new OpenAiToolCall("call_abc", "function",
                                new OpenAiFunction("search", "{\"q\":\"cats\"}"))
                        ]),
                    FinishReason: "tool_calls")
            ]
        );

        var result = ResponseTranslator.Translate(response, "claude-sonnet-4");

        var toolBlock = result.Content.OfType<ToolUseBlock>().FirstOrDefault();
        await Assert.That(toolBlock).IsNotNull();
        await Assert.That(toolBlock!.Id).IsEqualTo("call_abc");
        await Assert.That(toolBlock.Name).IsEqualTo("search");
        await Assert.That(toolBlock.Input).IsNotNull();
    }

    [Test]
    public async Task EmptyResponse_ProducesEmptyTextBlock()
    {
        var response = new OpenAiChatResponse(Id: "chatcmpl-3");

        var result = ResponseTranslator.Translate(response, "claude-sonnet-4");

        await Assert.That(result.Content).Count().IsEqualTo(1);
        var textBlock = result.Content[0] as TextBlock;
        await Assert.That(textBlock).IsNotNull();
        await Assert.That(textBlock!.Text).IsEqualTo("");
    }

    [Test]
    [Arguments("stop", "end_turn")]
    [Arguments("length", "max_tokens")]
    [Arguments("tool_calls", "tool_use")]
    [Arguments(null, "end_turn")]
    public async Task MapStopReason_MapsCorrectly(string? openAiReason, string expectedAnthropicReason)
    {
        var result = ResponseTranslator.MapStopReason(openAiReason);

        await Assert.That(result).IsEqualTo(expectedAnthropicReason);
    }

    [Test]
    public async Task MapUsage_TranslatesTokenCounts()
    {
        var openAiUsage = new OpenAiUsage(PromptTokens: 10, CompletionTokens: 25, TotalTokens: 35);

        var result = ResponseTranslator.MapUsage(openAiUsage);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.InputTokens).IsEqualTo(10);
        await Assert.That(result.OutputTokens).IsEqualTo(25);
    }

    [Test]
    public async Task MapUsage_NullReturnsNull()
    {
        var result = ResponseTranslator.MapUsage(null);

        await Assert.That(result).IsNull();
    }

    [Test]
    [Arguments(400, "invalid_request_error")]
    [Arguments(401, "authentication_error")]
    [Arguments(429, "rate_limit_error")]
    [Arguments(500, "api_error")]
    public async Task TranslateError_MapsStatusCodes(int statusCode, string expectedErrorType)
    {
        var result = ResponseTranslator.TranslateError(statusCode, "test message");

        await Assert.That(result.Type).IsEqualTo("error");
        await Assert.That(result.Error.Type).IsEqualTo(expectedErrorType);
        await Assert.That(result.Error.Message).IsEqualTo("test message");
    }

    [Test]
    public async Task Translate_PreservesRequestModel()
    {
        var response = new OpenAiChatResponse(
            Id: "chatcmpl-4",
            Model: "gpt-4o",
            Choices:
            [
                new OpenAiChoice(0,
                    Message: new OpenAiMessage("assistant", Content: "Hi"),
                    FinishReason: "stop")
            ]
        );

        var result = ResponseTranslator.Translate(response, "claude-sonnet-4");

        await Assert.That(result.Model).IsEqualTo("claude-sonnet-4");
    }
}
