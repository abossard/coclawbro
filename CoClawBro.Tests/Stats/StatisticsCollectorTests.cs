using CoClawBro.Data;
using CoClawBro.Stats;

namespace CoClawBro.Tests.Stats;

public class StatisticsCollectorTests
{
    private static RequestMetrics MakeMetrics(
        string requestModel = "claude-sonnet-4",
        string upstreamModel = "claude-sonnet-4",
        int inputTokens = 10,
        int outputTokens = 20,
        int httpStatus = 200,
        string? error = null,
        TimeSpan? latency = null,
        TimeSpan? ttft = null)
    {
        return new RequestMetrics(
            Timestamp: DateTimeOffset.UtcNow,
            RequestModel: requestModel,
            UpstreamModel: upstreamModel,
            InputTokens: inputTokens,
            OutputTokens: outputTokens,
            ThinkingBudget: null,
            Latency: latency ?? TimeSpan.FromMilliseconds(100),
            TimeToFirstToken: ttft ?? TimeSpan.Zero,
            HttpStatus: httpStatus,
            IsStreaming: false,
            Error: error
        );
    }

    [Test]
    public async Task EmptyStats_ReturnsZeroes()
    {
        var collector = new StatisticsCollector();
        var stats = collector.GetStats();

        await Assert.That(stats.TotalRequests).IsEqualTo(0);
        await Assert.That(stats.TotalInputTokens).IsEqualTo(0L);
        await Assert.That(stats.TotalOutputTokens).IsEqualTo(0L);
        await Assert.That(stats.ErrorCount).IsEqualTo(0);
        await Assert.That(stats.ActiveStreams).IsEqualTo(0);
        await Assert.That(stats.LastRequestTime).IsNull();
    }

    [Test]
    public async Task RecordAndGetStats_Aggregates()
    {
        var collector = new StatisticsCollector();
        collector.Record(MakeMetrics(inputTokens: 10, outputTokens: 20));
        collector.Record(MakeMetrics(inputTokens: 30, outputTokens: 40));

        var stats = collector.GetStats();

        await Assert.That(stats.TotalRequests).IsEqualTo(2);
        await Assert.That(stats.TotalInputTokens).IsEqualTo(40L);
        await Assert.That(stats.TotalOutputTokens).IsEqualTo(60L);
        await Assert.That(stats.LastRequestTime).IsNotNull();
    }

    [Test]
    public async Task GetRecentRequests_ReturnsReverseChronological()
    {
        var collector = new StatisticsCollector();
        collector.Record(MakeMetrics(requestModel: "model-a"));
        collector.Record(MakeMetrics(requestModel: "model-b"));
        collector.Record(MakeMetrics(requestModel: "model-c"));

        var recent = collector.GetRecentRequests(2);

        await Assert.That(recent).Count().IsEqualTo(2);
        await Assert.That(recent[0].RequestModel).IsEqualTo("model-c");
        await Assert.That(recent[1].RequestModel).IsEqualTo("model-b");
    }

    [Test]
    public async Task ActiveStreamCounting_IncrementsAndDecrements()
    {
        var collector = new StatisticsCollector();

        await Assert.That(collector.ActiveStreams).IsEqualTo(0);

        collector.IncrementActiveStreams();
        collector.IncrementActiveStreams();
        await Assert.That(collector.ActiveStreams).IsEqualTo(2);

        collector.DecrementActiveStreams();
        await Assert.That(collector.ActiveStreams).IsEqualTo(1);

        var stats = collector.GetStats();
        await Assert.That(stats.ActiveStreams).IsEqualTo(1);
    }

    [Test]
    public async Task ThreadSafety_ConcurrentRecords()
    {
        var collector = new StatisticsCollector();
        const int count = 500;

        Parallel.For(0, count, i =>
        {
            collector.Record(MakeMetrics(inputTokens: 1, outputTokens: 1));
        });

        var stats = collector.GetStats();

        await Assert.That(stats.TotalRequests).IsEqualTo(count);
        await Assert.That(stats.TotalInputTokens).IsEqualTo((long)count);
        await Assert.That(stats.TotalOutputTokens).IsEqualTo((long)count);
    }
}
