namespace CoClawBro.Translation;

/// <summary>
/// Pure calculation: maps Anthropic model names to Copilot model IDs and vice versa.
/// Delegates to Data.ModelMapper for the actual mapping logic.
/// </summary>
public static class ModelMapping
{
    public static string ToCopilot(string anthropicModel, string? globalOverride = null) =>
        Data.ModelMapper.MapToCopilot(anthropicModel, globalOverride);

    public static string FromCopilot(string copilotModel) => copilotModel;
}
