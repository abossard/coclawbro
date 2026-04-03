using CoClawBro.Auth;
using CoClawBro.Data;
using CoClawBro.Proxy;
using CoClawBro.Stats;
using CoClawBro.Thinking;
using CoClawBro.UI;
using Spectre.Console;

// --- Configuration ---
var port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : 5050;
var authToken = $"coclawbro-{Guid.NewGuid():N}"[..24];
var defaultModel = CoClawBro.UI.ModelPreferences.LoadLastModel() ?? "claude-sonnet-4";

// --- Build core modules ---
var tokenManager = new TokenManager();
var thinking = new ThinkingController();
var stats = new StatisticsCollector();
var cts = new CancellationTokenSource();

// Handle Ctrl+C
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// Capture SIGTERM for clean container shutdown
AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();

// --- Authenticate ---
AnsiConsole.Write(new FigletText("CoClawBro").Color(Color.Blue));
AnsiConsole.MarkupLine("[dim]Claude Code ↔ GitHub Copilot Proxy[/]\n");

try
{
    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync("Authenticating with GitHub...", async ctx =>
        {
            await tokenManager.InitializeAsync(cts.Token);
        });
}
catch (OperationCanceledException)
{
    AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
    return 0;
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine($"[red]Authentication failed: {ex.Message}[/]");
    return 1;
}

// --- Build HTTP server ---
var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseUrls($"http://localhost:{port}");
builder.Logging.ClearProviders(); // quiet — we have our own UI

var app = builder.Build();
var copilotClient = new CopilotClient(tokenManager);
var handler = new ProxyHandler(copilotClient, thinking, stats);
handler.MapEndpoints(app);

// --- Auth middleware: validate proxy token ---
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/health"))
    {
        await next();
        return;
    }

    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
    if (authHeader is null || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        ctx.Response.StatusCode = 401;
        await ctx.Response.WriteAsync("{\"error\":\"Missing Authorization header\"}");
        return;
    }

    await next();
});

// --- Start server in background ---
var serverTask = app.RunAsync(cts.Token);

// --- Run terminal UI on main thread ---
var ui = new TerminalUI(tokenManager, copilotClient, thinking, stats, port, defaultModel, authToken, cts);

try
{
    await ui.RunAsync();
}
catch (OperationCanceledException) { }

// --- Graceful shutdown ---
AnsiConsole.MarkupLine("\n[yellow]Shutting down...[/]");
cts.Cancel();

using var shutdownCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
try { await serverTask.WaitAsync(shutdownCts.Token); }
catch (OperationCanceledException) { }
catch { }

copilotClient.Dispose();
tokenManager.Dispose();

AnsiConsole.MarkupLine("[green]CoClawBro stopped.[/]");
return 0;
