using System.Text.Json.Serialization;

namespace CoClawBro.Data;

// --- GitHub OAuth Device Flow ---

public sealed record DeviceCodeResponse(
    [property: JsonPropertyName("device_code")] string? DeviceCode = null,
    [property: JsonPropertyName("user_code")] string? UserCode = null,
    [property: JsonPropertyName("verification_uri")] string? VerificationUri = null,
    [property: JsonPropertyName("expires_in")] int ExpiresIn = 900,
    [property: JsonPropertyName("interval")] int Interval = 5,
    [property: JsonPropertyName("error")] string? Error = null,
    [property: JsonPropertyName("error_description")] string? ErrorDescription = null
);

public sealed record OAuthTokenResponse(
    [property: JsonPropertyName("access_token")] string? AccessToken = null,
    [property: JsonPropertyName("token_type")] string? TokenType = null,
    [property: JsonPropertyName("scope")] string? Scope = null,
    [property: JsonPropertyName("error")] string? Error = null,
    [property: JsonPropertyName("error_description")] string? ErrorDescription = null
);

// --- Copilot Internal Token ---

public sealed record CopilotTokenResponse(
    [property: JsonPropertyName("token")] string? Token = null,
    [property: JsonPropertyName("expires_at")] long ExpiresAt = 0,
    [property: JsonPropertyName("refresh_in")] int RefreshIn = 1500,
    [property: JsonPropertyName("error_details")] string? ErrorDetails = null
);

// --- Persisted OAuth Token ---

public sealed record PersistedToken(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt
);

// --- User preferences stored in ~/.coclawbro/ ---

public sealed record LastModelPrefs(
    [property: JsonPropertyName("model")] string? Model = null
);
