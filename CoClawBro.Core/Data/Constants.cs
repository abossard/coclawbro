namespace CoClawBro.Data;

/// <summary>Centralised string/int constants grouped by topic/intent.</summary>
public static class Constants
{
    // -------------------------------------------------------------------------
    // Proxy defaults
    // -------------------------------------------------------------------------
    public static class Defaults
    {
        public const int    Port            = 5050;
        public const string Model           = Models.Sonnet4;
        public const string AuthTokenPrefix = "coclawbro-";
        public const int    AuthTokenLength = 24;
    }

    // -------------------------------------------------------------------------
    // GitHub OAuth / device-flow authentication
    // -------------------------------------------------------------------------
    public static class GitHub
    {
        public const string ClientId       = "Iv1.b507a08c87ecfe98";
        public const string Scope          = "read:user";
        public const string DeviceCodeUrl  = "https://github.com/login/device/code";
        public const string AccessTokenUrl = "https://github.com/login/oauth/access_token";
        public const string CopilotTokenUrl = "https://api.github.com/copilot_internal/v2/token";
        public const string UserApiUrl     = "https://api.github.com/user";
        public const string DeviceGrantType = "urn:ietf:params:oauth:grant-type:device_code";

        // OAuth polling error codes
        public const string ErrorSlowDown    = "slow_down";
        public const string ErrorAuthPending = "authorization_pending";

        // Local token storage
        public const string TokenDir  = ".coclawbro";
        public const string TokenFile = "oauth_token.json";
    }

    // -------------------------------------------------------------------------
    // Copilot upstream API — base URL, paths, HTTP header names/values
    // -------------------------------------------------------------------------
    public static class CopilotApi
    {
        public const string BaseUrl = "https://api.githubcopilot.com";

        // Upstream request paths
        public const string ChatCompletionsPath = "/chat/completions";
        public const string ModelsPath          = "/models";

        // HTTP header names
        public const string IntegrationIdHeader = "Copilot-Integration-Id";
        public const string EditorVersionHeader = "Editor-Version";
        public const string PluginVersionHeader = "Editor-Plugin-Version";

        // HTTP header values (must match what GitHub Copilot expects)
        public const string IntegrationIdValue = "vscode-chat";
        public const string EditorVersionValue = "vscode/1.85.1";
        public const string PluginVersionValue = "copilot/1.155.0";
        public const string UserAgentValue     = "GithubCopilot/1.155.0";
    }

    // -------------------------------------------------------------------------
    // Local proxy endpoint paths  (ASP.NET route strings)
    // -------------------------------------------------------------------------
    public static class Endpoints
    {
        public const string Health         = "/health";
        public const string MessagesV1     = "/v1/messages";
        public const string CountTokensV1  = "/v1/messages/count_tokens";
        public const string Models         = "/models";
        public const string ModelsV1       = "/v1/models";
    }

    // -------------------------------------------------------------------------
    // Model identifiers
    // -------------------------------------------------------------------------
    public static class Models
    {
        // Canonical Copilot model IDs (used as output / override targets)
        public const string Sonnet4 = "claude-sonnet-4";
        public const string Opus4   = "claude-opus-4";
        public const string Gpt4o   = "gpt-4o";
        public const string Gpt41   = "gpt-4.1";

        // Versioned / aliased Anthropic model names (used as input mapping keys)
        public const string Sonnet4Dated  = "claude-sonnet-4-20250514";
        public const string Sonnet45      = "claude-sonnet-4.5";
        public const string Opus4Dated    = "claude-opus-4-20250514";
        public const string Opus45        = "claude-opus-4.5";
        public const string Opus46        = "claude-opus-4.6";
        public const string Haiku45       = "claude-haiku-4.5";
        public const string Haiku35Dated  = "claude-3-5-haiku-20241022";
    }

    // -------------------------------------------------------------------------
    // Claude Code settings — env-var key names and file-system paths
    // -------------------------------------------------------------------------
    public static class ClaudeCode
    {
        // Environment variable keys written into ~/.claude/settings.json
        public const string EnvBaseUrl             = "ANTHROPIC_BASE_URL";
        public const string EnvAuthToken           = "ANTHROPIC_AUTH_TOKEN";
        public const string EnvModel               = "ANTHROPIC_MODEL";
        public const string EnvDefaultSonnet       = "ANTHROPIC_DEFAULT_SONNET_MODEL";
        public const string EnvDefaultOpus         = "ANTHROPIC_DEFAULT_OPUS_MODEL";
        public const string EnvDefaultHaiku        = "ANTHROPIC_DEFAULT_HAIKU_MODEL";
        public const string EnvDisableBetas        = "CLAUDE_CODE_DISABLE_EXPERIMENTAL_BETAS";
        public const string EnvDisablePromptCache  = "DISABLE_PROMPT_CACHING";

        // File-system locations
        public const string SettingsDir  = ".claude";
        public const string SettingsFile = "settings.json";
    }

    // -------------------------------------------------------------------------
    // User preferences persisted locally
    // -------------------------------------------------------------------------
    public static class Prefs
    {
        public const string LastModelFile = "last_model.json";
    }
}
