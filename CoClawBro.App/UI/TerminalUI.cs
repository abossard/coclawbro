using CoClawBro.Auth;
using CoClawBro.Data;
using CoClawBro.Proxy;
using CoClawBro.Stats;
using CoClawBro.Thinking;
using Spectre.Console;
using System.Text.Json;

namespace CoClawBro.UI;

/// <summary>
/// Terminal UI using Spectre.Console. Runs on the main thread while the
/// HTTP server runs in the background. Handles keyboard input and display refresh.
/// </summary>
public sealed class TerminalUI
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan InputPollInterval = TimeSpan.FromMilliseconds(100);

    private readonly TokenManager _tokenManager;
    private readonly CopilotClient _copilot;
    private readonly ThinkingController _thinking;
    private readonly StatisticsCollector _stats;
    private readonly int _port;
    private readonly CancellationTokenSource _cts;
    private string _currentModel;
    private readonly string _authToken;

    public TerminalUI(TokenManager tokenManager, CopilotClient copilot, ThinkingController thinking,
        StatisticsCollector stats, int port, string currentModel, string authToken,
        CancellationTokenSource cts)
    {
        _tokenManager = tokenManager;
        _copilot = copilot;
        _thinking = thinking;
        _stats = stats;
        _port = port;
        _currentModel = currentModel;
        _authToken = authToken;
        _cts = cts;
    }

    public async Task RunAsync()
    {
        var nextRefreshAt = DateTimeOffset.MinValue;

        while (!_cts.IsCancellationRequested)
        {
            if (DateTimeOffset.UtcNow >= nextRefreshAt)
            {
                BeginInPlaceRedraw();
                RenderHeader();
                RenderStatus();
                nextRefreshAt = DateTimeOffset.UtcNow + RefreshInterval;
            }

            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);
                await HandleKey(key);
                nextRefreshAt = DateTimeOffset.MinValue;
            }

            try { await Task.Delay(InputPollInterval, _cts.Token); }
            catch (OperationCanceledException) { break; }
        }
    }

    private void RenderHeader()
    {
        AnsiConsole.Write(new FigletText("CoClawBro").Color(Color.Blue));
        AnsiConsole.MarkupLine("[dim]Claude Code ↔ GitHub Copilot Proxy[/]");
        AnsiConsole.WriteLine();
    }

    private void RenderStatus()
    {
        var authStatus = _tokenManager.IsAuthenticated
            ? $"[green]● Authenticated as @{_tokenManager.Username ?? "unknown"}[/]"
            : "[red]● Not authenticated[/]";

        var tokenExpiry = _tokenManager.CopilotTokenExpiresAt;
        var remaining = tokenExpiry - DateTimeOffset.UtcNow;
        var tokenStatus = remaining.TotalSeconds > 0
            ? $"[green]● Valid (expires in {remaining:mm\\:ss})[/]"
            : "[yellow]● Refreshing...[/]";

        AnsiConsole.MarkupLine($"  Proxy:    [green]● Running on http://localhost:{_port}[/]");
        AnsiConsole.MarkupLine($"  GitHub:   {authStatus}");
        AnsiConsole.MarkupLine($"  Token:    {tokenStatus}");
        AnsiConsole.MarkupLine($"  Model:    [cyan]{_currentModel}[/]");
        AnsiConsole.MarkupLine($"  Thinking: [cyan]{_thinking.GetStatusText()}[/]");
        AnsiConsole.WriteLine();

        // Stats table
        var stats = _stats.GetStats();
        var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
        table.AddColumn("Requests");
        table.AddColumn("Input Tokens");
        table.AddColumn("Output Tokens");
        table.AddColumn("Avg Latency");
        table.AddColumn("Streams");
        table.AddColumn("Errors");
        table.AddRow(
            stats.TotalRequests.ToString(),
            stats.TotalInputTokens.ToString("N0"),
            stats.TotalOutputTokens.ToString("N0"),
            stats.AverageLatency.TotalMilliseconds > 0
                ? $"{stats.AverageLatency.TotalMilliseconds:F0}ms" : "-",
            stats.ActiveStreams.ToString(),
            stats.ErrorCount > 0 ? $"[red]{stats.ErrorCount}[/]" : "0"
        );
        AnsiConsole.Write(table);

        // Recent requests
        var recent = _stats.GetRecentRequests(5);
        if (recent.Count > 0)
        {
            AnsiConsole.MarkupLine("[dim]Recent requests:[/]");
            foreach (var r in recent)
            {
                var statusColor = r.HttpStatus < 400 ? "green" : "red";
                AnsiConsole.MarkupLine(
                    $"  [{statusColor}]{r.HttpStatus}[/] {r.UpstreamModel} " +
                    $"{r.Latency.TotalMilliseconds:F0}ms " +
                    $"{(r.IsStreaming ? "stream" : "batch")} " +
                    $"[dim]{r.Timestamp:HH:mm:ss}[/]");
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim][[Q]]uit  [[R]]efresh Token  [[M]]odel  [[C]]onfigure CC  [[E]]nv Export[/]");
    }

    private async Task HandleKey(ConsoleKeyInfo key)
    {
        switch (char.ToLowerInvariant(key.KeyChar))
        {
            case 'q':
                AnsiConsole.MarkupLine("\n[yellow]Shutting down...[/]");
                _cts.Cancel();
                break;

            case 'r':
                AnsiConsole.MarkupLine("\n[yellow]Refreshing token...[/]");
                try
                {
                    await _tokenManager.ForceRefreshAsync();
                    AnsiConsole.MarkupLine("[green]Token refreshed![/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Refresh failed: {ex.Message}[/]");
                }
                await Task.Delay(1000);
                break;

            case 'm':
                await SelectModelAsync();
                break;

            case 'c':
                ConfigureClaudeCode();
                break;

            case 'e':
                PrintEnvExport();
                break;
        }
    }

    private async Task SelectModelAsync()
    {
        IReadOnlyList<ModelChoice> models;
        try
        {
            models = await FetchModelChoicesAsync();
        }
        catch (Exception ex)
        {
            BeginInPlaceRedraw();
            AnsiConsole.MarkupLine($"[yellow]Could not fetch live model list: {Markup.Escape(ex.Message)}[/]");
            models = GetFallbackModelChoices();
        }

        if (models.Count == 0)
            models = GetFallbackModelChoices();

        // Seed cursor on the previously chosen model
        var lastModel = ModelPreferences.LoadLastModel() ?? _currentModel;
        var filter    = string.Empty;
        var modelList = models.ToList();
        var seedIndex = modelList.FindIndex(
            m => string.Equals(m.Id, lastModel, StringComparison.OrdinalIgnoreCase));
        var cursor    = Math.Max(0, seedIndex);
        const int pageSize = 18;

        while (true)
        {
            var filtered = ApplyModelFilter(models, filter);
            if (cursor >= filtered.Count) cursor = Math.Max(0, filtered.Count - 1);

            // ---- render ----
            BeginInPlaceRedraw();
            AnsiConsole.MarkupLine("[bold]Select Copilot model[/]  [dim](↑↓ move · Enter select · Esc clear filter)[/]");
            if (filter.Length > 0)
                AnsiConsole.MarkupLine($"  Filter: [yellow]{Markup.Escape(filter)}[/]  [dim](Backspace to delete · Esc to clear)[/]");
            else
                AnsiConsole.MarkupLine("  [dim]Type to filter…[/]");
            AnsiConsole.WriteLine();

            if (filtered.Count == 0)
            {
                AnsiConsole.MarkupLine("  [grey]No models match.[/]");
            }
            else
            {
                // Scroll window
                var windowStart = Math.Max(0, cursor - pageSize / 2);
                var windowEnd   = Math.Min(filtered.Count, windowStart + pageSize);
                windowStart     = Math.Max(0, windowEnd - pageSize);

                for (var i = windowStart; i < windowEnd; i++)
                {
                    var m = filtered[i];
                    if (i == cursor)
                        AnsiConsole.MarkupLine($"  [bold green]▶ {Markup.Escape(m.Label)}[/]");
                    else
                        AnsiConsole.MarkupLine($"    [grey]{Markup.Escape(m.Label)}[/]");
                }

                if (filtered.Count > pageSize)
                    AnsiConsole.MarkupLine($"  [dim]…{filtered.Count} models total[/]");
            }

            // ---- input ----
            var key = Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    cursor = Math.Max(0, cursor - 1);
                    break;

                case ConsoleKey.DownArrow:
                    cursor = Math.Min(Math.Max(0, filtered.Count - 1), cursor + 1);
                    break;

                case ConsoleKey.Enter:
                    if (filtered.Count == 0) break;
                    var chosen = filtered[cursor];
                    ModelMapper.SetGlobalModel(chosen.Id);
                    _currentModel = chosen.Id;
                    ModelPreferences.SaveLastModel(chosen.Id);
                    BeginInPlaceRedraw();
                    AnsiConsole.MarkupLine($"[green]Model set to: {Markup.Escape(chosen.Id)}[/]");
                    Thread.Sleep(600);
                    return;

                case ConsoleKey.Escape:
                    if (filter.Length > 0)
                    {
                        filter = string.Empty;
                        cursor = 0;
                    }
                    break;

                case ConsoleKey.Backspace:
                    if (filter.Length > 0)
                    {
                        filter = filter[..^1];
                        cursor = 0;
                    }
                    break;

                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        filter += key.KeyChar;
                        cursor  = 0;
                    }
                    break;
            }
        }
    }

    private static IReadOnlyList<ModelChoice> ApplyModelFilter(IReadOnlyList<ModelChoice> models, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return models;

        return models
            .Where(m => m.Id.Contains(filter, StringComparison.OrdinalIgnoreCase)
                        || m.Label.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private async Task<IReadOnlyList<ModelChoice>> FetchModelChoicesAsync()
    {
        var response = await _copilot.GetModelsAsync(_cts.Token);
        var body = await response.Content.ReadAsStringAsync(_cts.Token);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Upstream returned {(int)response.StatusCode}");

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return [];

        var choices = new List<ModelChoice>();
        foreach (var item in data.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idProp) || idProp.ValueKind != JsonValueKind.String)
                continue;

            var id = idProp.GetString();
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var contextWindow = FindInt(item,
                "context_window",
                "context_window_tokens",
                "max_input_tokens",
                "input_token_limit",
                "max_context_tokens");

            var maxOutput = FindInt(item,
                "max_output_tokens",
                "output_token_limit",
                "completion_token_limit");

            choices.Add(new ModelChoice(id, BuildModelLabel(id, contextWindow, maxOutput), contextWindow));
        }

        return choices
            .GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderByDescending(c => c.ContextWindow ?? 0)
            .ThenBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int? FindInt(JsonElement element, params string[] keys)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        foreach (var prop in element.EnumerateObject())
        {
            if (keys.Any(k => string.Equals(k, prop.Name, StringComparison.OrdinalIgnoreCase))
                && prop.Value.ValueKind == JsonValueKind.Number
                && prop.Value.TryGetInt32(out var value))
                return value;
        }
        return null;
    }

    private static IReadOnlyList<ModelChoice> GetFallbackModelChoices() =>
    [
        new("claude-sonnet-4", "claude-sonnet-4", null),
        new("claude-opus-4",   "claude-opus-4",   null),
        new("gpt-4o",   "gpt-4o",   null),
        new("gpt-4.1",   "gpt-4.1",   null),
    ];

    private static string BuildModelLabel(string id, int? contextWindow, int? maxOutput)
    {
        if (contextWindow is null && maxOutput is null)
            return id;

        var parts = new List<string>();
        if (contextWindow is not null)
            parts.Add($"ctx {FormatTokenCount(contextWindow.Value)}");
        if (maxOutput is not null)
            parts.Add($"out {FormatTokenCount(maxOutput.Value)}");

        return $"{id} ({string.Join(", ", parts)})";
    }

    private static string FormatTokenCount(int value)
    {
        if (value >= 1_000_000)
            return $"{value / 1_000_000d:0.#}M";
        if (value >= 1_000)
            return $"{value / 1_000d:0.#}K";
        return value.ToString();
    }

    private sealed record ModelChoice(string Id, string Label, int? ContextWindow);

    private void SelectThinking()
    {
        BeginInPlaceRedraw();
        var options = new Dictionary<string, ThinkingConfig>
        {
            ["Off (strip all thinking)"] = ThinkingConfig.Off,
            ["Low (3,000 tokens)"] = ThinkingConfig.Low,
            ["Medium (10,000 tokens)"] = ThinkingConfig.Medium,
            ["High (32,000 tokens)"] = ThinkingConfig.High,
            ["Pass-through (log only)"] = ThinkingConfig.Default,
        };

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select [green]thinking mode[/]:")
                .AddChoices(options.Keys));

        _thinking.Config = options[selection];
        AnsiConsole.MarkupLine($"[green]Thinking mode set to: {selection}[/]");
        Thread.Sleep(800);
    }

    private void ConfigureClaudeCode()
    {
        BeginInPlaceRedraw();
        AnsiConsole.MarkupLine("[yellow]Configuring Claude Code settings.json...[/]");

        try
        {
            var result = CoClawBro.Config.ClaudeCodeConfigurator.ConfigureSettings(_port, _currentModel, _authToken);
            AnsiConsole.MarkupLine("[green]Configured Claude Code successfully.[/]");
            AnsiConsole.MarkupLine($"[dim]settings.json:[/] [cyan]{Markup.Escape(result.SettingsPath)}[/]");

            if (!string.IsNullOrWhiteSpace(result.BackupPath))
                AnsiConsole.MarkupLine($"[dim]backup created:[/] [cyan]{Markup.Escape(result.BackupPath!)}[/]");
            else
                AnsiConsole.MarkupLine("[dim]backup created:[/] [grey]none (new settings file)[/]");

            var envTable = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
            envTable.AddColumn("Key");
            envTable.AddColumn("Value");

            foreach (var kvp in result.AppliedEnvValues.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                var value = kvp.Key == "ANTHROPIC_AUTH_TOKEN" ? MaskSecret(kvp.Value) : kvp.Value;
                envTable.AddRow(Markup.Escape(kvp.Key), Markup.Escape(value));
            }

            AnsiConsole.MarkupLine("\n[dim]Applied settings under env:[/]");
            AnsiConsole.Write(envTable);
            AnsiConsole.MarkupLine("\n[green]Restart Claude Code to apply.[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed: {ex.Message}[/]");
        }

        AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
        Console.ReadKey(intercept: true);
    }

    private static string MaskSecret(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        if (value.Length <= 10)
            return new string('*', value.Length);

        return $"{value[..6]}...{value[^4..]}";
    }

    private void PrintEnvExport()
    {
        BeginInPlaceRedraw();
        var exports = CoClawBro.Config.ClaudeCodeConfigurator.GenerateEnvExport(_port, _currentModel, _authToken);
        AnsiConsole.MarkupLine("[yellow]Copy these into your shell:[/]\n");
        AnsiConsole.WriteLine(exports);
        AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
        Console.ReadKey(intercept: true);
    }

    private static void BeginInPlaceRedraw()
    {
        if (Console.IsOutputRedirected)
            return;

        var width = Math.Max(1, Console.WindowWidth);
        var height = Math.Max(1, Console.WindowHeight);
        var blankLine = new string(' ', Math.Max(1, width - 1));

        Console.SetCursorPosition(0, 0);
        for (var row = 0; row < height; row++)
        {
            Console.SetCursorPosition(0, row);
            Console.Write(blankLine);
        }
        Console.SetCursorPosition(0, 0);
    }
}
