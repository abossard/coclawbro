using CoClawBro.Data;
using CoClawBro.Thinking;

namespace CoClawBro.Tests.Thinking;

public class ThinkingControllerTests
{
    [Test]
    public async Task PassThrough_StripsThinkingFromOutput()
    {
        var controller = new ThinkingController();
        // Default mode is PassThrough
        var request = new AnthropicMessagesRequest(
            Model: "claude-sonnet-4",
            MaxTokens: 100,
            Messages: [new AnthropicMessage("user", [new TextBlock("Hello")])],
            Thinking: new AnthropicThinking("enabled", BudgetTokens: 5000)
        );

        var result = controller.Process(request);

        await Assert.That(result.Thinking).IsNull();
    }

    [Test]
    public async Task ForceOff_StripsThinkingFromOutput()
    {
        var controller = new ThinkingController { Config = ThinkingConfig.Off };
        var request = new AnthropicMessagesRequest(
            Model: "claude-sonnet-4",
            MaxTokens: 100,
            Messages: [new AnthropicMessage("user", [new TextBlock("Hello")])],
            Thinking: new AnthropicThinking("enabled", BudgetTokens: 10000)
        );

        var result = controller.Process(request);

        await Assert.That(result.Thinking).IsNull();
        await Assert.That(controller.LastAppliedMode).IsEqualTo(ThinkingMode.ForceOff);
    }

    [Test]
    public async Task LastSeenThinking_IsCaptured()
    {
        var controller = new ThinkingController();
        var thinking = new AnthropicThinking("enabled", BudgetTokens: 8000);
        var request = new AnthropicMessagesRequest(
            Model: "claude-sonnet-4",
            MaxTokens: 100,
            Messages: [new AnthropicMessage("user", [new TextBlock("Hi")])],
            Thinking: thinking
        );

        controller.Process(request);

        await Assert.That(controller.LastSeenThinking).IsNotNull();
        await Assert.That(controller.LastSeenThinking!.Type).IsEqualTo("enabled");
        await Assert.That(controller.LastSeenThinking.BudgetTokens).IsEqualTo(8000);
    }

    [Test]
    public async Task GetStatusText_ReflectsCurrentMode()
    {
        var controller = new ThinkingController { Config = ThinkingConfig.Off };
        var statusOff = controller.GetStatusText();
        await Assert.That(statusOff).Contains("Off");

        controller.Config = ThinkingConfig.Default;
        var statusPassThrough = controller.GetStatusText();
        await Assert.That(statusPassThrough).Contains("Pass-through");

        controller.Config = ThinkingConfig.Medium;
        var statusOverride = controller.GetStatusText();
        await Assert.That(statusOverride).Contains("Override");
        await Assert.That(statusOverride).Contains("10000");
    }

    [Test]
    public async Task Process_ReturnsNewRequest_OriginalUnchanged()
    {
        var controller = new ThinkingController();
        var thinking = new AnthropicThinking("enabled", BudgetTokens: 5000);
        var original = new AnthropicMessagesRequest(
            Model: "claude-sonnet-4",
            MaxTokens: 100,
            Messages: [new AnthropicMessage("user", [new TextBlock("Hi")])],
            Thinking: thinking
        );

        var processed = controller.Process(original);

        // Original should still have thinking set
        await Assert.That(original.Thinking).IsNotNull();
        await Assert.That(original.Thinking!.BudgetTokens).IsEqualTo(5000);

        // Processed should have thinking stripped
        await Assert.That(processed.Thinking).IsNull();

        // Other fields preserved
        await Assert.That(processed.Model).IsEqualTo("claude-sonnet-4");
        await Assert.That(processed.MaxTokens).IsEqualTo(100);
    }
}
