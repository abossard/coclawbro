namespace CoClawBro.Data;

// --- Thinking Mode ---

public enum ThinkingMode
{
    PassThrough,  // Log what Claude Code requested, strip for Copilot
    ForceOff,     // Always strip thinking blocks
    Override      // Apply proxy-configured budget, then strip for Copilot
}

// --- Thinking Configuration ---

public sealed record ThinkingConfig(
    ThinkingMode Mode = ThinkingMode.PassThrough,
    int? BudgetTokensOverride = null
)
{
    public static readonly ThinkingConfig Off = new(ThinkingMode.ForceOff);
    public static readonly ThinkingConfig Low = new(ThinkingMode.Override, 3000);
    public static readonly ThinkingConfig Medium = new(ThinkingMode.Override, 10000);
    public static readonly ThinkingConfig High = new(ThinkingMode.Override, 32000);
    public static readonly ThinkingConfig Default = new(ThinkingMode.PassThrough);
}

// --- Proxy Configuration ---

public sealed record ProxyConfig(
    int Port = Constants.Defaults.Port,
    string CopilotApiEndpoint = Constants.CopilotApi.BaseUrl,
    string? ModelOverride = null,
    ThinkingConfig Thinking = default!
)
{
    public ProxyConfig() : this(Thinking: ThinkingConfig.Default) { }
}

// --- Model Mapping ---

public static class ModelMapper
{
    private static readonly Dictionary<string, string> DefaultMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [Constants.Models.Sonnet4Dated] = Constants.Models.Sonnet4,
        [Constants.Models.Sonnet4]      = Constants.Models.Sonnet4,
        [Constants.Models.Sonnet45]     = Constants.Models.Sonnet4,
        [Constants.Models.Opus4Dated]   = Constants.Models.Opus4,
        [Constants.Models.Opus4]        = Constants.Models.Opus4,
        [Constants.Models.Opus45]       = Constants.Models.Opus4,
        [Constants.Models.Opus46]       = Constants.Models.Opus4,
        [Constants.Models.Haiku45]      = Constants.Models.Gpt4o,
        [Constants.Models.Haiku35Dated] = Constants.Models.Gpt4o,
        [Constants.Models.Gpt4o]        = Constants.Models.Gpt4o,
        [Constants.Models.Gpt41]        = Constants.Models.Gpt41,
    };

    private static readonly Dictionary<string, string> UserOverrides = new(StringComparer.OrdinalIgnoreCase);

    public static string MapToCopilot(string anthropicModel, string? globalOverride = null)
    {
        if (globalOverride is not null)
            return globalOverride;

        if (UserOverrides.TryGetValue(anthropicModel, out var userMapped))
            return userMapped;

        if (DefaultMap.TryGetValue(anthropicModel, out var mapped))
            return mapped;

        return anthropicModel; // pass through unknown models
    }

    public static void SetOverride(string anthropicModel, string copilotModel) =>
        UserOverrides[anthropicModel] = copilotModel;

    public static void SetGlobalModel(string copilotModel) =>
        UserOverrides["*"] = copilotModel;

    public static string? GetGlobalModel() =>
        UserOverrides.TryGetValue("*", out var m) ? m : null;

    public static IReadOnlyDictionary<string, string> GetDefaultMap() => DefaultMap;
}
