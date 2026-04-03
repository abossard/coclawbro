using WireMock.Server;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using CoClawBro.Auth;
using CoClawBro.Proxy;
using CoClawBro.Stats;
using CoClawBro.Thinking;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;

namespace CoClawBro.Tests.Fixtures;

public sealed class ProxyTestFixture : IAsyncDisposable
{
    public WireMockServer WireMock { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public string ProxyUrl { get; private set; } = null!;
    private WebApplication? _app;

    public async Task StartAsync()
    {
        WireMock = WireMockServer.Start();

        var proxyPort = GetFreePort();
        ProxyUrl = $"http://localhost:{proxyPort}";

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls(ProxyUrl);
        builder.Logging.ClearProviders();
        _app = builder.Build();

        var tokenProvider = new FakeTokenProvider();
        var copilotClient = new CopilotClient(tokenProvider, WireMock.Url!);
        var thinking = new ThinkingController();
        var stats = new StatisticsCollector();
        var handler = new ProxyHandler(copilotClient, thinking, stats);
        handler.MapEndpoints(_app);

        await _app.StartAsync();

        Client = new HttpClient { BaseAddress = new Uri(ProxyUrl) };
        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-token");
    }

    public void StubBatchResponse(string responseBody, int statusCode = 200)
    {
        WireMock.Given(Request.Create().WithPath("/chat/completions").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBody(responseBody));
    }

    public void StubStreamingResponse(params string[] sseLines)
    {
        var body = string.Join("\n", sseLines.Select(l => $"data: {l}")) + "\ndata: [DONE]\n\n";
        WireMock.Given(Request.Create().WithPath("/chat/completions").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/event-stream")
                .WithBody(body));
    }

    public void StubModelsResponse(string responseBody)
    {
        WireMock.Given(Request.Create().WithPath("/models").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(responseBody));
    }

    public void StubError(int statusCode, string body = "{\"error\":\"test\"}")
    {
        WireMock.Given(Request.Create().WithPath("/chat/completions").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithBody(body));
    }

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        if (_app is not null) await _app.DisposeAsync();
        WireMock?.Stop();
        WireMock?.Dispose();
    }
}
