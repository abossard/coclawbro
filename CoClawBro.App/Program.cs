using System.Reflection;
using CoClawBro.Auth;
using CoClawBro.Data;
using CoClawBro.Diagnostics;
using CoClawBro.Proxy;
using CoClawBro.Stats;
using CoClawBro.Thinking;
using CoClawBro.UI;
using Spectre.Console;

// --- Handle --version ---
if (args.Length > 0 && args[0] == "--version")
{
    var version = Assembly.GetEntryAssembly()
        ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? "0.0.0";
    Console.WriteLine($"coclawbro {version}");
    return 0;
}

// --- Parse CLI flags ---
var argList = args.ToList();
var headless = argList.Remove("--headless") || Console.IsInputRedirected;
var debug = argList.Remove("--debug");

// Remaining positional args (port)
var positionalArgs = argList.Where(a => !a.StartsWith('-')).ToArray();

// --- Configuration ---
var port = positionalArgs.Length > 0 && int.TryParse(positionalArgs[0], out var p) ? p : 5050;
var authToken = $"coclawbro-{Guid.NewGuid():N}"[..24];
var defaultModel = CoClawBro.UI.ModelPreferences.LoadLastModel() ?? "claude-sonnet-4";

// --- Set up debug/headless modes ---
DebugLogger.Headless = headless;
if (debug)
    DebugLogger.Enable();

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
if (headless)
{
    Console.WriteLine("CoClawBro — Claude Code ↔ GitHub Copilot Proxy");
    Console.WriteLine();
}
else
{
    AnsiConsole.Write(new FigletText("CoClawBro").Color(Color.Blue));
    AnsiConsole.MarkupLine("[dim]Claude Code ↔ GitHub Copilot Proxy[/]\n");
}

try
{
    if (headless)
    {
        Console.WriteLine("Authenticating with GitHub...");
        await tokenManager.InitializeAsync(cts.Token);
    }
    else
    {
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Authenticating with GitHub...", async ctx =>
            {
                await tokenManager.InitializeAsync(cts.Token);
            });
    }
}
catch (OperationCanceledException)
{
    if (headless)
        Console.WriteLine("Cancelled.");
    else
        AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
    return 0;
}
catch (Exception ex)
{
    if (headless)
        Console.Error.WriteLine($"Authentication failed: {ex.Message}");
    else
        AnsiConsole.MarkupLine($"[red]Authentication failed: {ex.Message}[/]");
    return 1;
}

// --- Build HTTP server ---
var builder = WebApplication.CreateSlimBuilder(positionalArgs);
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

DebugLogger.Log("SYSTEM", $"Server started on http://localhost:{port}");
DebugLogger.Log("SYSTEM", $"Mode: {(headless ? "headless" : "interactive")}, Debug: {DebugLogger.IsEnabled}");

if (headless)
{
    // --- Headless mode: print connection info and wait ---
    Console.WriteLine($"Proxy running on http://localhost:{port}");
    Console.WriteLine($"Authenticated as @{tokenManager.Username ?? "unknown"}");
    Console.WriteLine($"Model: {defaultModel}");
    if (DebugLogger.IsEnabled)
        Console.WriteLine($"Debug log: {DebugLogger.LogPath}");
    Console.WriteLine();
    Console.WriteLine("Press Ctrl+C to stop.");

    try { await Task.Delay(Timeout.Infinite, cts.Token); }
    catch (OperationCanceledException) { }
}
else
{
    // --- Run terminal UI on main thread ---
    var ui = new TerminalUI(tokenManager, copilotClient, thinking, stats, port, defaultModel, authToken, cts);

    try
    {
        await ui.RunAsync();
    }
    catch (OperationCanceledException) { }
}

// --- Graceful shutdown ---
if (headless)
    Console.WriteLine("Shutting down...");
else
    AnsiConsole.MarkupLine("\n[yellow]Shutting down...[/]");

cts.Cancel();

using var shutdownCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
try { await serverTask.WaitAsync(shutdownCts.Token); }
catch (OperationCanceledException) { }
catch { }

copilotClient.Dispose();
tokenManager.Dispose();

if (headless)
    Console.WriteLine("CoClawBro stopped.");
else
    AnsiConsole.MarkupLine("[green]CoClawBro stopped.[/]");

return 0;
