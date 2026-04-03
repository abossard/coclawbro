using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using CoClawBro.Data;
using CoClawBro.Diagnostics;
using CoClawBro.Serialization;
using Spectre.Console;

namespace CoClawBro.Auth;

/// <summary>
/// Deep module: manages the entire GitHub Copilot authentication lifecycle.
/// Simple interface: GetTokenAsync() returns a valid Copilot bearer token.
/// Hides: device flow, OAuth polling, token exchange, proactive refresh, disk persistence.
/// </summary>
public sealed class TokenManager : ITokenProvider, IDisposable
{
    private static readonly string TokenDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Constants.GitHub.TokenDir);
    private static readonly string TokenPath = Path.Combine(TokenDir, Constants.GitHub.TokenFile);

    private readonly HttpClient _http;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private string? _oauthToken;
    private string? _copilotToken;
    private DateTimeOffset _copilotTokenExpiresAt;
    private string? _username;

    public string? Username => _username;
    public DateTimeOffset CopilotTokenExpiresAt => _copilotTokenExpiresAt;
    public bool IsAuthenticated => _copilotToken is not null;

    public TokenManager(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
    }

    /// <summary>
    /// Returns a valid Copilot bearer token. Handles all auth complexity internally.
    /// </summary>
    public async ValueTask<string> GetTokenAsync(CancellationToken ct = default)
    {
        if (_copilotToken is not null && DateTimeOffset.UtcNow < _copilotTokenExpiresAt.AddMinutes(-1))
            return _copilotToken;

        await _refreshLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (_copilotToken is not null && DateTimeOffset.UtcNow < _copilotTokenExpiresAt.AddMinutes(-1))
                return _copilotToken;

            await RefreshCopilotTokenAsync(ct);
            return _copilotToken!;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Initialize: try stored token, fall back to device flow.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        DebugLogger.LogAuth("Initialize", "starting");

        // Try loading persisted OAuth token
        if (TryLoadPersistedToken(out var persisted))
        {
            DebugLogger.LogAuth("Initialize", "found persisted token");
            _oauthToken = persisted!.AccessToken;
            try
            {
                await ExchangeForCopilotTokenAsync(ct);
                await FetchUsernameAsync(ct);
                DebugLogger.LogAuth("Initialize", $"authenticated as @{_username}");
                return;
            }
            catch (Exception ex)
            {
                DebugLogger.LogAuth("Initialize", $"persisted token failed: {ex.Message}");
                if (DebugLogger.Headless)
                    Console.Error.WriteLine("Stored token expired, re-authenticating...");
                else
                    AnsiConsole.MarkupLine("[yellow]Stored token expired, re-authenticating...[/]");
                DeletePersistedToken();
                _oauthToken = null;
            }
        }

        await RunDeviceFlowAsync(ct);
        await ExchangeForCopilotTokenAsync(ct);
        await FetchUsernameAsync(ct);
        PersistToken();
        DebugLogger.LogAuth("Initialize", $"completed, authenticated as @{_username}");
    }

    /// <summary>
    /// Force a token refresh (e.g., from UI [R]efresh command).
    /// </summary>
    public async Task ForceRefreshAsync(CancellationToken ct = default)
    {
        await _refreshLock.WaitAsync(ct);
        try
        {
            await RefreshCopilotTokenAsync(ct);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    // --- Device Flow ---

    private async Task RunDeviceFlowAsync(CancellationToken ct)
    {
        var deviceReq = new HttpRequestMessage(HttpMethod.Post, Constants.GitHub.DeviceCodeUrl);
        deviceReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        deviceReq.Headers.UserAgent.ParseAdd(Constants.CopilotApi.UserAgentValue);
        deviceReq.Content = new StringContent(
            $"{{\"client_id\":\"{Constants.GitHub.ClientId}\",\"scope\":\"{Constants.GitHub.Scope}\"}}",
            Encoding.UTF8, "application/json");

        var resp = await _http.SendAsync(deviceReq, ct);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync(ct);
        var deviceCode = JsonSerializer.Deserialize(body, AppJsonContext.App.DeviceCodeResponse)
            ?? throw new InvalidOperationException("Failed to parse device code response");

        if (deviceCode.DeviceCode is null || deviceCode.UserCode is null)
            throw new InvalidOperationException($"Device code error: {deviceCode.Error}");

        DebugLogger.LogAuth("DeviceFlow", $"user_code={deviceCode.UserCode} uri={deviceCode.VerificationUri}");

        // Display auth prompt — headless uses plain text
        if (DebugLogger.Headless)
        {
            Console.WriteLine();
            Console.WriteLine("=== GitHub Authentication Required ===");
            Console.WriteLine($"  Visit: {deviceCode.VerificationUri}");
            Console.WriteLine($"  Enter code: {deviceCode.UserCode}");
        }
        else
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[bold blue]GitHub Authentication Required[/]").RuleStyle("blue"));
            AnsiConsole.MarkupLine($"  Visit: [bold link]{deviceCode.VerificationUri}[/]");
            AnsiConsole.MarkupLine($"  Enter code: [bold yellow]{deviceCode.UserCode}[/]");
        }

        if (TryCopyToClipboard(deviceCode.UserCode, out var clipboardMessage))
        {
            if (DebugLogger.Headless)
                Console.WriteLine($"  ✓ {clipboardMessage}");
            else
                AnsiConsole.MarkupLine($"  [green]✓ {clipboardMessage}[/]");
        }
        else
        {
            if (DebugLogger.Headless)
                Console.WriteLine($"  {clipboardMessage}");
            else
                AnsiConsole.MarkupLine($"  [yellow]{clipboardMessage}[/]");
        }

        if (!DebugLogger.Headless)
        {
            AnsiConsole.Write(new Rule().RuleStyle("blue"));
            AnsiConsole.WriteLine();
        }
        else
        {
            Console.WriteLine("=======================================");
            Console.WriteLine();
        }

        // Poll for token
        var interval = deviceCode.Interval;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(deviceCode.ExpiresIn);

        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(interval), ct);

            var tokenReq = new HttpRequestMessage(HttpMethod.Post, Constants.GitHub.AccessTokenUrl);
            tokenReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            tokenReq.Content = new StringContent(
                $"{{\"client_id\":\"{Constants.GitHub.ClientId}\",\"device_code\":\"{deviceCode.DeviceCode}\",\"grant_type\":\"{Constants.GitHub.DeviceGrantType}\"}}",
                Encoding.UTF8, "application/json");

            var tokenResp = await _http.SendAsync(tokenReq, ct);
            var tokenBody = await tokenResp.Content.ReadAsStringAsync(ct);
            var token = JsonSerializer.Deserialize(tokenBody, AppJsonContext.App.OAuthTokenResponse);

            if (token?.AccessToken is not null)
            {
                _oauthToken = token.AccessToken;
                DebugLogger.LogAuth("DeviceFlow", "authentication successful");
                if (DebugLogger.Headless)
                    Console.WriteLine("✓ GitHub authentication successful!");
                else
                    AnsiConsole.MarkupLine("[green]✓ GitHub authentication successful![/]");
                return;
            }

            if (token?.Error == Constants.GitHub.ErrorSlowDown)
                interval += 5;
            else if (token?.Error != Constants.GitHub.ErrorAuthPending)
                throw new InvalidOperationException($"Auth error: {token?.Error} - {token?.ErrorDescription}");
        }

        throw new TimeoutException("Device flow authorization timed out");
    }

    // --- Copilot Token Exchange ---

    private async Task ExchangeForCopilotTokenAsync(CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, Constants.GitHub.CopilotTokenUrl);
        req.Headers.Authorization = new AuthenticationHeaderValue("token", _oauthToken);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Headers.UserAgent.ParseAdd(Constants.CopilotApi.UserAgentValue);
        req.Headers.TryAddWithoutValidation(Constants.CopilotApi.EditorVersionHeader, Constants.CopilotApi.EditorVersionValue);
        req.Headers.TryAddWithoutValidation(Constants.CopilotApi.PluginVersionHeader, Constants.CopilotApi.PluginVersionValue);

        var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var errorBody = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Copilot token exchange failed ({resp.StatusCode}): {errorBody}");
        }

        var body = await resp.Content.ReadAsStringAsync(ct);
        var copilotToken = JsonSerializer.Deserialize(body, AppJsonContext.App.CopilotTokenResponse)
            ?? throw new InvalidOperationException("Failed to parse Copilot token");

        if (copilotToken.Token is null)
            throw new InvalidOperationException($"No token in response: {copilotToken.ErrorDetails}");

        _copilotToken = copilotToken.Token;
        _copilotTokenExpiresAt = DateTimeOffset.FromUnixTimeSeconds(copilotToken.ExpiresAt);
    }

    private async Task RefreshCopilotTokenAsync(CancellationToken ct)
    {
        if (_oauthToken is null)
            throw new InvalidOperationException("No OAuth token available. Run InitializeAsync first.");

        try
        {
            await ExchangeForCopilotTokenAsync(ct);
        }
        catch
        {
            // OAuth token may have expired — try re-auth
            DeletePersistedToken();
            await RunDeviceFlowAsync(ct);
            PersistToken();
            await ExchangeForCopilotTokenAsync(ct);
        }
    }

    // --- Username ---

    private async Task FetchUsernameAsync(CancellationToken ct)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, Constants.GitHub.UserApiUrl);
            req.Headers.Authorization = new AuthenticationHeaderValue("token", _oauthToken);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Headers.UserAgent.ParseAdd(Constants.CopilotApi.UserAgentValue);

            var resp = await _http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(body);
                _username = doc.RootElement.TryGetProperty("login", out var login)
                    ? login.GetString() : null;
            }
        }
        catch { /* non-critical */ }
    }

    // --- Refresh Timer (removed — lazy refresh in GetTokenAsync is sufficient) ---

    // --- Persistence ---

    private bool TryLoadPersistedToken(out PersistedToken? token)
    {
        token = null;
        try
        {
            if (!File.Exists(TokenPath)) return false;
            var json = File.ReadAllText(TokenPath);
            token = JsonSerializer.Deserialize(json, AppJsonContext.App.PersistedToken);
            return token?.AccessToken is not null;
        }
        catch { return false; }
    }

    private void PersistToken()
    {
        try
        {
            Directory.CreateDirectory(TokenDir);
            var persisted = new PersistedToken(_oauthToken!, DateTimeOffset.UtcNow);
            var json = JsonSerializer.Serialize(persisted, AppJsonContext.App.PersistedToken);
            File.WriteAllText(TokenPath, json);
        }
        catch { /* non-critical */ }
    }

    private static void DeletePersistedToken()
    {
        try { if (File.Exists(TokenPath)) File.Delete(TokenPath); }
        catch { /* ignore */ }
    }

    private static bool TryCopyToClipboard(string text, out string message)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return TryRunClipboardProcess("pbcopy", messageOnSuccess: "Code copied to clipboard.", text, out message);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return TryRunClipboardProcess("clip", messageOnSuccess: "Code copied to clipboard.", text, out message);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (TryRunClipboardProcess("wl-copy", "Code copied to clipboard.", text, out message))
                    return true;

                return TryRunClipboardProcess("xclip", "Code copied to clipboard.", text, out message,
                    arguments: "-selection clipboard");
            }

            message = "Clipboard copy unavailable on this OS.";
            return false;
        }
        catch
        {
            message = "Unable to copy code automatically; please copy it manually.";
            return false;
        }
    }

    private static bool TryRunClipboardProcess(
        string fileName,
        string messageOnSuccess,
        string text,
        out string message,
        string? arguments = null)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments ?? string.Empty,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process is null)
            {
                message = "Unable to start clipboard utility.";
                return false;
            }

            process.StandardInput.Write(text);
            process.StandardInput.Close();
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                message = messageOnSuccess;
                return true;
            }

            message = "Clipboard utility exited with an error; copy the code manually.";
            return false;
        }
        catch
        {
            message = "Clipboard utility not found; copy the code manually.";
            return false;
        }
    }

    public void Dispose()
    {
        _refreshLock.Dispose();
    }
}
