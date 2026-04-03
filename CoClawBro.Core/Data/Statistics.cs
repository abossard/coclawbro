using System.Collections.Concurrent;

namespace CoClawBro.Data;

// --- Per-Request Metrics ---

public sealed record RequestMetrics(
    DateTimeOffset Timestamp,
    string RequestModel,
    string UpstreamModel,
    int InputTokens,
    int OutputTokens,
    int? ThinkingBudget,
    TimeSpan Latency,
    TimeSpan TimeToFirstToken,
    int HttpStatus,
    bool IsStreaming,
    string? Error = null
);

// --- Aggregated Statistics ---

public sealed record ProxyStats(
    int TotalRequests,
    int ActiveStreams,
    long TotalInputTokens,
    long TotalOutputTokens,
    TimeSpan AverageLatency,
    TimeSpan AverageTimeToFirstToken,
    int ErrorCount,
    Dictionary<string, int> RequestsByModel,
    Dictionary<int, int> RequestsByStatus,
    DateTimeOffset? LastRequestTime
);
