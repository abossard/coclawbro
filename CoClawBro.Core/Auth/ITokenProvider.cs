namespace CoClawBro.Auth;

/// <summary>
/// Provides Copilot bearer tokens. Extracted interface enables testing
/// without real GitHub authentication.
/// </summary>
public interface ITokenProvider
{
    ValueTask<string> GetTokenAsync(CancellationToken ct = default);
}
