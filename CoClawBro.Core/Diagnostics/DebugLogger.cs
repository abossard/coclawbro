namespace CoClawBro.Diagnostics;

/// <summary>
/// Global debug logger that writes timestamped entries to ~/.coclawbro/debug.log.
/// Toggle at runtime with the [D] key or start with --debug.
/// All methods are thread-safe and no-op when disabled.
/// </summary>
public static class DebugLogger
{
    private static readonly object Lock = new();
    private static StreamWriter? _writer;

    public static bool IsEnabled { get; private set; }
    public static bool Headless { get; set; }

    public static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".coclawbro", "debug.log");

    public static void Enable()
    {
        lock (Lock)
        {
            if (IsEnabled) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                _writer = new StreamWriter(LogPath, append: true) { AutoFlush = true };
                IsEnabled = true;
                Log("SYSTEM", $"Debug logging enabled — writing to {LogPath}");
            }
            catch
            {
                // If we can't open the log file, don't crash
            }
        }
    }

    public static void Disable()
    {
        lock (Lock)
        {
            if (!IsEnabled) return;
            Log("SYSTEM", "Debug logging disabled");
            _writer?.Dispose();
            _writer = null;
            IsEnabled = false;
        }
    }

    public static void Toggle()
    {
        if (IsEnabled) Disable();
        else Enable();
    }

    public static void Log(string category, string message)
    {
        if (!IsEnabled) return;
        lock (Lock)
        {
            _writer?.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] [{category,-10}] {message}");
        }
    }

    public static void LogRequest(string method, string url, int? bodyLength = null, string? bodySnippet = null)
    {
        if (!IsEnabled) return;
        Log("HTTP-OUT", $"{method} {url} (body={bodyLength ?? 0} bytes)");
        if (bodySnippet is not null)
            Log("HTTP-OUT", $"  ┗ {Truncate(bodySnippet, 500)}");
    }

    public static void LogResponse(string label, int statusCode, TimeSpan? latency = null, int? bodyLength = null)
    {
        if (!IsEnabled) return;
        var lat = latency.HasValue ? $" {latency.Value.TotalMilliseconds:F0}ms" : "";
        var len = bodyLength.HasValue ? $" body={bodyLength.Value}" : "";
        Log("HTTP-IN", $"{label} → {statusCode}{lat}{len}");
    }

    public static void LogProxy(string direction, string requestModel, string upstreamModel,
        bool streaming, int? thinkingBudget)
    {
        if (!IsEnabled) return;
        var stream = streaming ? "stream" : "batch";
        var think = thinkingBudget.HasValue ? $" thinking={thinkingBudget}" : "";
        Log("PROXY", $"{direction} model={requestModel}→{upstreamModel} {stream}{think}");
    }

    public static void LogStream(string eventType, int blockIndex = -1, string? detail = null)
    {
        if (!IsEnabled) return;
        var idx = blockIndex >= 0 ? $" block={blockIndex}" : "";
        var det = detail is not null ? $" {detail}" : "";
        Log("STREAM", $"{eventType}{idx}{det}");
    }

    public static void LogAuth(string step, string? detail = null)
    {
        if (!IsEnabled) return;
        var det = detail is not null ? $": {detail}" : "";
        Log("AUTH", $"{step}{det}");
    }

    public static void LogRetry(int attempt, int maxRetries, int statusCode, TimeSpan delay)
    {
        if (!IsEnabled) return;
        Log("RETRY", $"Attempt {attempt + 1}/{maxRetries + 1} after {statusCode}, backoff {delay.TotalSeconds:F1}s");
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : string.Concat(s.AsSpan(0, max), $"…[+{s.Length - max}]");
}
