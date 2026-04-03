using System.Net;
using System.Net.Http.Headers;
using CoClawBro.Auth;
using CoClawBro.Data;
using CoClawBro.Diagnostics;

namespace CoClawBro.Proxy;

/// <summary>
/// HttpClient wrapper for the GitHub Copilot API.
/// Adds required authentication headers and Copilot-specific identifiers.
/// Retries transient failures (429, 502, 503) with exponential backoff.
/// </summary>
public sealed class CopilotClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly ITokenProvider _tokenProvider;
    private readonly string _baseUrl;
    private const int MaxRetries = 2;

    public CopilotClient(ITokenProvider tokenProvider, string baseUrl = Constants.CopilotApi.BaseUrl)
    {
        _tokenProvider = tokenProvider;
        _baseUrl = baseUrl.TrimEnd('/');
        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    }

    public async Task<HttpResponseMessage> SendChatCompletionAsync(
        HttpContent content, bool streaming, CancellationToken ct = default)
    {
        // Buffer the content so we can replay on retry
        var body = await content.ReadAsStringAsync(ct);
        var mediaType = content.Headers.ContentType?.MediaType ?? "application/json";

        DebugLogger.LogRequest("POST", $"{_baseUrl}{Constants.CopilotApi.ChatCompletionsPath}",
            body.Length);

        return await SendWithRetryAsync(async token =>
        {
            var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}{Constants.CopilotApi.ChatCompletionsPath}");
            req.Content = new StringContent(body, System.Text.Encoding.UTF8, mediaType);
            AddCopilotHeaders(req, token);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var resp = await _http.SendAsync(req,
                streaming ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead,
                ct);
            sw.Stop();
            DebugLogger.LogResponse("COPILOT", (int)resp.StatusCode, sw.Elapsed);
            return resp;
        }, ct);
    }

    public async Task<HttpResponseMessage> GetModelsAsync(CancellationToken ct = default)
    {
        DebugLogger.LogRequest("GET", $"{_baseUrl}{Constants.CopilotApi.ModelsPath}");

        return await SendWithRetryAsync(async token =>
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}{Constants.CopilotApi.ModelsPath}");
            AddCopilotHeaders(req, token);
            var resp = await _http.SendAsync(req, ct);
            DebugLogger.LogResponse("MODELS", (int)resp.StatusCode);
            return resp;
        }, ct);
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<string, Task<HttpResponseMessage>> send, CancellationToken ct)
    {
        HttpResponseMessage? lastResponse = null;

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            var token = await _tokenProvider.GetTokenAsync(ct);
            var response = await send(token);

            if (!IsTransient(response.StatusCode) || attempt == MaxRetries)
                return response;

            lastResponse?.Dispose();
            lastResponse = response;

            // Exponential backoff: 1s, 2s
            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));

            // Respect Retry-After header if present
            if (response.Headers.RetryAfter?.Delta is { } retryDelta)
                delay = retryDelta;

            DebugLogger.LogRetry(attempt, MaxRetries, (int)response.StatusCode, delay);
            await Task.Delay(delay, ct);
        }

        return lastResponse!; // unreachable due to loop logic
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.TooManyRequests
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static void AddCopilotHeaders(HttpRequestMessage req, string token)
    {
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.TryAddWithoutValidation(Constants.CopilotApi.IntegrationIdHeader, Constants.CopilotApi.IntegrationIdValue);
        req.Headers.TryAddWithoutValidation(Constants.CopilotApi.EditorVersionHeader, Constants.CopilotApi.EditorVersionValue);
        req.Headers.TryAddWithoutValidation(Constants.CopilotApi.PluginVersionHeader, Constants.CopilotApi.PluginVersionValue);
        req.Headers.UserAgent.ParseAdd(Constants.CopilotApi.UserAgentValue);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public void Dispose() => _http.Dispose();
}
