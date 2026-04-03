using System.Collections.Concurrent;
using CoClawBro.Data;

namespace CoClawBro.Stats;

/// <summary>
/// Thread-safe statistics collection and aggregation.
/// Recording is an action (side-effect); aggregation is a calculation (pure).
/// </summary>
public sealed class StatisticsCollector
{
    private readonly ConcurrentQueue<RequestMetrics> _metrics = new();
    private int _activeStreams;
    private const int MaxEntries = 1000;

    public int ActiveStreams => _activeStreams;

    public void Record(RequestMetrics metrics)
    {
        _metrics.Enqueue(metrics);
        while (_metrics.Count > MaxEntries)
            _metrics.TryDequeue(out _);
    }

    public void IncrementActiveStreams() => Interlocked.Increment(ref _activeStreams);
    public void DecrementActiveStreams() => Interlocked.Decrement(ref _activeStreams);

    /// <summary>
    /// Pure calculation: aggregate all recorded metrics into summary stats.
    /// </summary>
    public ProxyStats GetStats()
    {
        var all = _metrics.ToArray();
        if (all.Length == 0)
            return new ProxyStats(0, _activeStreams, 0, 0, TimeSpan.Zero, TimeSpan.Zero,
                0, new(), new(), null);

        var byModel = all.GroupBy(m => m.UpstreamModel)
            .ToDictionary(g => g.Key, g => g.Count());
        var byStatus = all.GroupBy(m => m.HttpStatus)
            .ToDictionary(g => g.Key, g => g.Count());
        var errors = all.Count(m => m.Error is not null || m.HttpStatus >= 400);
        var avgLatency = TimeSpan.FromMilliseconds(all.Average(m => m.Latency.TotalMilliseconds));
        var streaming = all.Where(m => m.TimeToFirstToken > TimeSpan.Zero).ToArray();
        var avgTtft = streaming.Length > 0
            ? TimeSpan.FromMilliseconds(streaming.Average(m => m.TimeToFirstToken.TotalMilliseconds))
            : TimeSpan.Zero;

        return new ProxyStats(
            TotalRequests: all.Length,
            ActiveStreams: _activeStreams,
            TotalInputTokens: all.Sum(m => (long)m.InputTokens),
            TotalOutputTokens: all.Sum(m => (long)m.OutputTokens),
            AverageLatency: avgLatency,
            AverageTimeToFirstToken: avgTtft,
            ErrorCount: errors,
            RequestsByModel: byModel,
            RequestsByStatus: byStatus,
            LastRequestTime: all.Max(m => m.Timestamp)
        );
    }

    public IReadOnlyList<RequestMetrics> GetRecentRequests(int count = 10)
    {
        var all = _metrics.ToArray();
        return all.TakeLast(count).Reverse().ToArray();
    }
}
