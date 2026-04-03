using System.Text.Json;
using CoClawBro.Data;
using CoClawBro.Serialization;

namespace CoClawBro.UI;

/// <summary>
/// Persists user model preference to disk so the last selection is
/// remembered across restarts. Stored as JSON; invalid or missing files
/// are silently ignored and treated as no preference.
/// </summary>
public static class ModelPreferences
{
    private static readonly string PrefsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Constants.GitHub.TokenDir);   // reuse ~/.coclawbro/

    private static readonly string LastModelPath = Path.Combine(PrefsDir, Constants.Prefs.LastModelFile);

    public static void SaveLastModel(string modelId)
    {
        try
        {
            Directory.CreateDirectory(PrefsDir);
            var json = JsonSerializer.Serialize(new LastModelPrefs(modelId.Trim()), AppJsonContext.App.LastModelPrefs);
            File.WriteAllText(LastModelPath, json);
        }
        catch { /* non-critical */ }
    }

    public static string? LoadLastModel()
    {
        try
        {
            if (!File.Exists(LastModelPath)) return null;
            var json = File.ReadAllText(LastModelPath);
            var prefs = JsonSerializer.Deserialize(json, AppJsonContext.App.LastModelPrefs);
            var model = prefs?.Model?.Trim();
            return string.IsNullOrEmpty(model) ? null : model;
        }
        catch { return null; }   // invalid / corrupt JSON — ignore
    }
}
