using CoClawBro.Data;

namespace CoClawBro.Thinking;

/// <summary>
/// Intercepts and controls thinking parameters from Anthropic requests.
/// Applies proxy-level overrides before the request is translated and forwarded.
/// </summary>
public sealed class ThinkingController
{
    private ThinkingConfig _config = ThinkingConfig.Default;
    private AnthropicThinking? _lastSeen;
    private ThinkingMode _lastAppliedMode;

    public ThinkingConfig Config
    {
        get => _config;
        set => _config = value;
    }

    public AnthropicThinking? LastSeenThinking => _lastSeen;
    public ThinkingMode LastAppliedMode => _lastAppliedMode;

    /// <summary>
    /// Process a request: log the thinking params, apply override, return modified request.
    /// Since Copilot doesn't support thinking, the thinking field is always stripped
    /// from the outgoing request. But we log what was requested for the UI.
    /// </summary>
    public AnthropicMessagesRequest Process(AnthropicMessagesRequest request)
    {
        _lastSeen = request.Thinking;
        _lastAppliedMode = _config.Mode;

        // Thinking is always stripped for Copilot (it doesn't support it).
        // The mode controls what we log and whether we'd use it if the upstream supported it.
        return request with { Thinking = null };
    }

    /// <summary>
    /// Returns a human-readable description of current thinking state for the UI.
    /// </summary>
    public string GetStatusText()
    {
        var mode = _config.Mode switch
        {
            ThinkingMode.ForceOff => "Off",
            ThinkingMode.PassThrough => "Pass-through (stripped for Copilot)",
            ThinkingMode.Override => $"Override ({_config.BudgetTokensOverride} tokens)",
            _ => "Unknown"
        };

        var lastReq = _lastSeen is not null
            ? $" | Last requested: {_lastSeen.Type}, budget={_lastSeen.BudgetTokens}"
            : "";

        return $"{mode}{lastReq}";
    }
}
