using System.Text.Json;
using System.Text.Json.Nodes;
using CoClawBro.Data;

namespace CoClawBro.Config;

/// <summary>
/// Configures Claude Code's settings.json with the required environment variables
/// for connecting to the proxy. Also generates shell export commands.
/// </summary>
public static class ClaudeCodeConfigurator
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Constants.ClaudeCode.SettingsDir);
    private static readonly string SettingsPath = Path.Combine(SettingsDir, Constants.ClaudeCode.SettingsFile);

    /// <summary>
    /// Write or merge required env vars into ~/.claude/settings.json.
    /// Creates backup before modification.
    /// </summary>
    public static ConfigureSettingsResult ConfigureSettings(int proxyPort, string model, string authToken)
    {
        Directory.CreateDirectory(SettingsDir);
        var envValues = BuildEnvValues(proxyPort, model, authToken);
        string? backupPath = null;

        JsonObject? root = null;
        if (File.Exists(SettingsPath))
        {
            // Backup
            backupPath = SettingsPath + $".bak.{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
            File.Copy(SettingsPath, backupPath, overwrite: true);

            try
            {
                var existing = File.ReadAllText(SettingsPath);
                root = JsonNode.Parse(existing)?.AsObject();
            }
            catch { /* start fresh if corrupted */ }
        }

        root ??= new JsonObject();

        var env = root["env"]?.AsObject() ?? new JsonObject();
        foreach (var kvp in envValues)
            env[kvp.Key] = kvp.Value;

        root["env"] = env;

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(SettingsPath, root.ToJsonString(options));

        return new ConfigureSettingsResult(SettingsPath, backupPath, envValues);
    }

    /// <summary>
    /// Generate shell export commands for manual environment setup.
    /// </summary>
    public static string GenerateEnvExport(int proxyPort, string model, string authToken)
    {
        var envValues = BuildEnvValues(proxyPort, model, authToken);
        return $"""
            export ANTHROPIC_BASE_URL="{envValues[Constants.ClaudeCode.EnvBaseUrl]}"
            export ANTHROPIC_AUTH_TOKEN="{envValues[Constants.ClaudeCode.EnvAuthToken]}"
            export ANTHROPIC_MODEL="{envValues[Constants.ClaudeCode.EnvModel]}"
            export ANTHROPIC_DEFAULT_SONNET_MODEL="{envValues[Constants.ClaudeCode.EnvDefaultSonnet]}"
            export ANTHROPIC_DEFAULT_OPUS_MODEL="{envValues[Constants.ClaudeCode.EnvDefaultOpus]}"
            export ANTHROPIC_DEFAULT_HAIKU_MODEL="{envValues[Constants.ClaudeCode.EnvDefaultHaiku]}"
            export CLAUDE_CODE_DISABLE_EXPERIMENTAL_BETAS="{envValues[Constants.ClaudeCode.EnvDisableBetas]}"
            export DISABLE_PROMPT_CACHING="{envValues[Constants.ClaudeCode.EnvDisablePromptCache]}"
            """;
    }

    private static Dictionary<string, string> BuildEnvValues(int proxyPort, string model, string authToken) =>
        new(StringComparer.Ordinal)
        {
            [Constants.ClaudeCode.EnvBaseUrl]            = $"http://localhost:{proxyPort}",
            [Constants.ClaudeCode.EnvAuthToken]          = authToken,
            [Constants.ClaudeCode.EnvModel]              = model,
            [Constants.ClaudeCode.EnvDefaultSonnet]      = Constants.Models.Sonnet4,
            [Constants.ClaudeCode.EnvDefaultOpus]        = Constants.Models.Opus4,
            [Constants.ClaudeCode.EnvDefaultHaiku]       = Constants.Models.Gpt4o,
            [Constants.ClaudeCode.EnvDisableBetas]       = "1",
            [Constants.ClaudeCode.EnvDisablePromptCache] = "1",
        };

    public static bool SettingsExist() => File.Exists(SettingsPath);
}

public sealed record ConfigureSettingsResult(
    string SettingsPath,
    string? BackupPath,
    IReadOnlyDictionary<string, string> AppliedEnvValues
);
