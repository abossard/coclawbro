using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using CoClawBro.Data;
using CoClawBro.Serialization;
using CoClawBro.Stats;
using CoClawBro.Thinking;
using CoClawBro.Translation;

namespace CoClawBro.Proxy;

/// <summary>
/// ASP.NET Core endpoint handlers for the proxy.
/// These are thin "action" wrappers that orchestrate calculations and I/O.
/// </summary>
public sealed class ProxyHandler
{
    private readonly CopilotClient _copilot;
    private readonly ThinkingController _thinking;
    private readonly StatisticsCollector _stats;

    public ProxyHandler(CopilotClient copilot, ThinkingController thinking, StatisticsCollector stats)
    {
        _copilot = copilot;
        _thinking = thinking;
        _stats = stats;
    }

    public void MapEndpoints(WebApplication app)
    {
        app.MapPost(Constants.Endpoints.MessagesV1, HandleMessages);
        app.MapPost(Constants.Endpoints.CountTokensV1, HandleCountTokens);
        app.MapGet(Constants.Endpoints.Models, HandleModels);
        app.MapGet(Constants.Endpoints.ModelsV1, HandleModels);
        // Health check
        app.MapGet(Constants.Endpoints.Health, () => Results.Ok(new { status = "ok" }));
    }

    private async Task HandleMessages(HttpContext ctx)
    {
        var sw = Stopwatch.StartNew();
        var ttftSw = new Stopwatch();
        string requestModel = "unknown";
        string upstreamModel = "unknown";
        int inputTokens = 0, outputTokens = 0;
        int? thinkingBudget = null;
        bool isStreaming = false;

        try
        {
            // Parse Anthropic request
            var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize(body, AppJsonContext.App.AnthropicMessagesRequest);
            if (request is null)
            {
                ctx.Response.StatusCode = 400;
                await WriteAnthropicError(ctx, 400, "Invalid request body");
                return;
            }

            requestModel = request.Model;
            isStreaming = request.Stream;
            thinkingBudget = request.Thinking?.BudgetTokens;

            // Apply thinking overrides
            request = _thinking.Process(request);

            // Translate Anthropic → OpenAI
            var globalModelOverride = ModelMapper.GetGlobalModel();
            var openAiRequest = RequestTranslator.Translate(request, globalModelOverride);
            upstreamModel = openAiRequest.Model;

            // Serialize and forward
            var json = JsonSerializer.Serialize(openAiRequest, AppJsonContext.App.OpenAiChatRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _copilot.SendChatCompletionAsync(content, isStreaming, ctx.RequestAborted);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ctx.RequestAborted);
                ctx.Response.StatusCode = (int)response.StatusCode;
                await WriteAnthropicError(ctx, (int)response.StatusCode, errorBody);
                RecordMetrics(sw, ttftSw, requestModel, upstreamModel, inputTokens, outputTokens,
                    thinkingBudget, (int)response.StatusCode, isStreaming, errorBody);
                return;
            }

            if (isStreaming)
            {
                (inputTokens, outputTokens) = await HandleStreamingResponse(ctx, response, requestModel, sw, ttftSw);
            }
            else
            {
                (inputTokens, outputTokens) = await HandleBatchResponse(ctx, response, requestModel);
            }

            // Extract token counts from response headers if available
            RecordMetrics(sw, ttftSw, requestModel, upstreamModel, inputTokens, outputTokens,
                thinkingBudget, 200, isStreaming, null);
        }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected — don't try to write a response
        }
        catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
        {
            var status = (int)ex.StatusCode.Value;
            if (!ctx.Response.HasStarted)
            {
                ctx.Response.StatusCode = status;
                await WriteAnthropicError(ctx, status, $"Upstream error: {ex.Message}");
            }
            RecordMetrics(sw, ttftSw, requestModel, upstreamModel, inputTokens, outputTokens,
                thinkingBudget, status, isStreaming, ex.Message);
        }
        catch (Exception ex)
        {
            if (!ctx.Response.HasStarted)
            {
                ctx.Response.StatusCode = 500;
                await WriteAnthropicError(ctx, 500, ex.Message);
            }
            RecordMetrics(sw, ttftSw, requestModel, upstreamModel, inputTokens, outputTokens,
                thinkingBudget, 500, isStreaming, ex.Message);
        }
    }

    private async Task<(int InputTokens, int OutputTokens)> HandleBatchResponse(
        HttpContext ctx, HttpResponseMessage response, string requestModel)
    {
        var responseBody = await response.Content.ReadAsStringAsync(ctx.RequestAborted);
        var openAiResponse = JsonSerializer.Deserialize(responseBody, AppJsonContext.App.OpenAiChatResponse);

        if (openAiResponse is null)
        {
            ctx.Response.StatusCode = 502;
            await WriteAnthropicError(ctx, 502, "Invalid upstream response");
            return (0, 0);
        }

        var anthropicResponse = ResponseTranslator.Translate(openAiResponse, requestModel);
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(
            JsonSerializer.Serialize(anthropicResponse, AppJsonContext.App.AnthropicMessagesResponse),
            ctx.RequestAborted);

        var inputTokens  = openAiResponse.Usage?.PromptTokens     ?? 0;
        var outputTokens = openAiResponse.Usage?.CompletionTokens ?? 0;
        return (inputTokens, outputTokens);
    }

    private async Task<(int InputTokens, int OutputTokens)> HandleStreamingResponse(
        HttpContext ctx, HttpResponseMessage response, string requestModel,
        Stopwatch sw, Stopwatch ttftSw)
    {
        _stats.IncrementActiveStreams();
        int inputTokens = 0, outputTokens = 0;
        try
        {
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers["Cache-Control"] = "no-cache";
            ctx.Response.Headers["Connection"] = "keep-alive";

            var stream = await response.Content.ReadAsStreamAsync(ctx.RequestAborted);
            ttftSw.Start();
            var first = true;

            await foreach (var (sseEvent, usage) in StreamTranslator.TranslateAsync(stream, requestModel, ctx.RequestAborted))
            {
                if (first)
                {
                    ttftSw.Stop();
                    first = false;
                }

                if (usage is not null)
                {
                    inputTokens  = usage.PromptTokens;
                    outputTokens = usage.CompletionTokens;
                }

                await ctx.Response.WriteAsync(sseEvent, ctx.RequestAborted);
                await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
            }
        }
        finally
        {
            _stats.DecrementActiveStreams();
        }
        return (inputTokens, outputTokens);
    }

    private async Task HandleCountTokens(HttpContext ctx)
    {
        try
        {
            var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
            // Simple heuristic: ~4 chars per token
            var estimatedTokens = Math.Max(1, body.Length / 4);
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(
                JsonSerializer.Serialize(new AnthropicCountTokensResponse(estimatedTokens),
                    AppJsonContext.App.AnthropicCountTokensResponse),
                ctx.RequestAborted);
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await WriteAnthropicError(ctx, 500, ex.Message);
        }
    }

    private async Task HandleModels(HttpContext ctx)
    {
        try
        {
            var response = await _copilot.GetModelsAsync(ctx.RequestAborted);
            var body = await response.Content.ReadAsStringAsync(ctx.RequestAborted);
            ctx.Response.ContentType = "application/json";
            ctx.Response.StatusCode = (int)response.StatusCode;
            await ctx.Response.WriteAsync(body, ctx.RequestAborted);
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await WriteAnthropicError(ctx, 500, ex.Message);
        }
    }

    private static async Task WriteAnthropicError(HttpContext ctx, int statusCode, string message)
    {
        ctx.Response.ContentType = "application/json";
        var error = ResponseTranslator.TranslateError(statusCode, message);
        await ctx.Response.WriteAsync(
            JsonSerializer.Serialize(error, AppJsonContext.App.AnthropicErrorResponse));
    }

    private void RecordMetrics(Stopwatch sw, Stopwatch ttftSw,
        string requestModel, string upstreamModel,
        int inputTokens, int outputTokens, int? thinkingBudget,
        int httpStatus, bool isStreaming, string? error)
    {
        sw.Stop();
        _stats.Record(new RequestMetrics(
            Timestamp: DateTimeOffset.UtcNow,
            RequestModel: requestModel,
            UpstreamModel: upstreamModel,
            InputTokens: inputTokens,
            OutputTokens: outputTokens,
            ThinkingBudget: thinkingBudget,
            Latency: sw.Elapsed,
            TimeToFirstToken: ttftSw.Elapsed,
            HttpStatus: httpStatus,
            IsStreaming: isStreaming,
            Error: error));
    }
}
