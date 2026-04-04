namespace CoClawBro.Diagnostics;

/// <summary>
/// Writes timestamped, plain-text log lines to stdout for headless/non-TTY mode.
/// Thread-safe. Only active when <see cref="IsEnabled"/> is true (set in headless mode).
/// </summary>
public static class ConsoleLogger
{
    private static readonly object Lock = new();

    public static bool IsEnabled { get; set; }

    public static void Log(string category, string message)
    {
        if (!IsEnabled) return;
        lock (Lock)
        {
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] [{category,-10}] {message}");
        }
    }

    public static void LogRequest(string requestModel, string upstreamModel,
        bool streaming, int httpStatus, TimeSpan latency,
        int inputTokens, int outputTokens, string? error)
    {
        if (!IsEnabled) return;

        var mode = streaming ? "stream" : "batch";
        var status = httpStatus < 400 ? $"{httpStatus} OK" : $"{httpStatus} ERR";
        var tokens = $"in={inputTokens} out={outputTokens}";
        var errSuffix = error is not null ? $" error=\"{Truncate(error, 120)}\"" : "";

        Log("REQUEST", $"{requestModel}→{upstreamModel} {mode} {status} {latency.TotalMilliseconds:F0}ms {tokens}{errSuffix}");
    }

    public static void LogAuth(string step, string? detail = null)
    {
        if (!IsEnabled) return;
        var det = detail is not null ? $": {detail}" : "";
        Log("AUTH", $"{step}{det}");
    }

    public static void LogStats(int totalRequests, int activeStreams, long inputTokens,
        long outputTokens, int errors, TimeSpan avgLatency)
    {
        if (!IsEnabled) return;
        Log("STATS",
            $"requests={totalRequests} streams={activeStreams} " +
            $"tokens_in={inputTokens:N0} tokens_out={outputTokens:N0} " +
            $"errors={errors} avg_latency={avgLatency.TotalMilliseconds:F0}ms");
    }

    public static void LogInfo(string message) => Log("INFO", message);
    public static void LogError(string message) => Log("ERROR", message);

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : string.Concat(s.AsSpan(0, max), "…");
}
