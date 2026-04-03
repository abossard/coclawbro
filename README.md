[![CI](https://github.com/{owner}/coclawbro/actions/workflows/ci.yml/badge.svg)](https://github.com/{owner}/coclawbro/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

# CoClawBro

**Claude Code ↔ GitHub Copilot Lawful Broker**

A .NET 10 AOT-compiled proxy that connects [Claude Code](https://docs.anthropic.com/en/docs/claude-code) to [GitHub Copilot's model API](https://docs.github.com/en/copilot). Translates between the Anthropic Messages API and OpenAI Chat Completions format in real-time, including full SSE streaming support.

## Features

- **Protocol Translation** — Anthropic Messages API ↔ OpenAI Chat Completions (batch + streaming)
- **GitHub OAuth Device Flow** — Authenticates via browser, persists tokens
- **Automatic Token Refresh** — Proactively refreshes Copilot tokens before expiry
- **Model Selection** — Switch between Copilot-hosted models at runtime
- **Thinking Parameter Control** — Intercept and configure thinking budgets (Off/Low/Medium/High)
- **Live Statistics** — Request counts, token usage, latency, error tracking
- **Terminal UI** — Spectre.Console-powered status display with keyboard commands
- **Claude Code Auto-Config** — Writes `~/.claude/settings.json` and generates shell exports
- **Single Binary** — AOT-compiled native executable, no runtime needed
- **Retry Logic** — Exponential backoff on transient upstream errors (429, 502, 503, 504)

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (for building)
- A [GitHub Copilot](https://github.com/features/copilot) subscription
- [Claude Code](https://docs.anthropic.com/en/docs/claude-code) installed

### Build & Run

```bash
# Clone and build
cd coclawbro
dotnet run

# Or publish as AOT native binary
dotnet publish -c Release
./bin/Release/net10.0/<rid>/publish/coclawbro
```

### Custom Port

```bash
dotnet run -- 8080
```

### First Run

1. CoClawBro starts and displays a GitHub device code
2. Visit the URL shown and enter the code
3. The proxy authenticates and starts listening on `http://localhost:5050`
4. Press **[C]** to auto-configure Claude Code, or **[E]** to see env exports

### Configure Claude Code

**Option A: Auto-configure** (press `C` in the UI)

Writes the required env vars directly into `~/.claude/settings.json`. Restart Claude Code to apply.

**Option B: Manual setup** (press `E` in the UI)

Copy the displayed exports into your shell:

```bash
export ANTHROPIC_BASE_URL="http://localhost:5050"
export ANTHROPIC_AUTH_TOKEN="coclawbro-..."
export ANTHROPIC_MODEL="claude-sonnet-4"
export CLAUDE_CODE_DISABLE_EXPERIMENTAL_BETAS="1"
export DISABLE_PROMPT_CACHING="1"
```

Then start Claude Code in the same shell.

## Keyboard Commands

| Key | Action |
|-----|--------|
| `Q` | Quit |
| `R` | Force token refresh |
| `M` | Select Copilot model |
| `T` | Set thinking mode |
| `C` | Auto-configure Claude Code |
| `E` | Show env export commands |

## Architecture

```
Claude Code                    CoClawBro                      GitHub Copilot
───────────                    ─────────                      ──────────────
POST /v1/messages  ──────►  Anthropic→OpenAI  ──────►  POST /chat/completions
(Anthropic API)              Translation                (OpenAI API)
                                                              │
SSE stream (Anthropic)  ◄──  OpenAI→Anthropic  ◄──────  SSE stream (OpenAI)
                              Stream Rewriting
```

### Design Principles

- **Deep Modules** (A Philosophy of Software Design) — TokenManager has a single `GetTokenAsync()` interface hiding device flow, OAuth, exchange, refresh, and disk persistence
- **Actions / Calculations / Data** (Grokking Simplicity) — Protocol translators are pure calculations, HTTP handlers are thin action wrappers, API types are immutable data records
- **1 external dependency** — Only [Spectre.Console](https://spectreconsole.net/) for terminal rendering
- **JSON source generators** — All serialization is AOT-compatible via `[JsonSerializable]`

### Project Structure

```
├── CoClawBro.Core/             # Class library — protocol translation, auth, proxy logic
│   ├── Data/                   # Immutable records (Anthropic, OpenAI, GitHub, Config, Stats)
│   ├── Serialization/          # JSON source generator context
│   ├── Translation/            # Pure protocol translators
│   ├── Auth/                   # TokenManager (deep module)
│   ├── Proxy/                  # HTTP client + endpoint handlers
│   ├── Thinking/               # Thinking parameter interception
│   ├── Stats/                  # Thread-safe metrics collection
│   └── Config/                 # Claude Code settings.json writer
├── CoClawBro.App/              # Console application — entry point, terminal UI
│   ├── Program.cs              # Entry point, wires everything
│   └── Ui/                     # Spectre.Console terminal UI
├── CoClawBro.Tests/            # Tests (TUnit + WireMock.Net)
└── CoClawBro.slnx              # Solution file
```

## Model Mapping

Claude Code sends Anthropic model names. CoClawBro maps them to Copilot-available models:

| Claude Code sends | Copilot receives |
|---|---|
| `claude-sonnet-4` | `claude-sonnet-4` |
| `claude-opus-4` | `claude-opus-4` |
| `claude-haiku-4.5` | `gpt-4o` |

Use the **[M]odel** selector to override which Copilot model all requests use.

## Known Limitations

- **Tool calling** may not work with all Copilot-hosted models (known upstream limitation)
- **Thinking parameters** are stripped before forwarding (Copilot doesn't support them)
- **Prompt caching** is disabled (not supported through the proxy)
- **Token counts** from `/v1/messages/count_tokens` are estimates (~4 chars/token heuristic)

## Installation

### Pre-built Binaries

Download the latest release for your platform from [GitHub Releases](https://github.com/{owner}/coclawbro/releases):

| Platform | Architecture | Download |
|----------|-------------|----------|
| Linux | x64 | `coclawbro-*-linux-x64.tar.gz` |
| Linux | ARM64 | `coclawbro-*-linux-arm64.tar.gz` |
| Linux | ARM (RPi) | `coclawbro-*-linux-arm.tar.gz` |
| macOS | Intel | `coclawbro-*-osx-x64.tar.gz` |
| macOS | Apple Silicon | `coclawbro-*-osx-arm64.tar.gz` |
| Windows | x64 | `coclawbro-*-win-x64.zip` |
| Windows | ARM64 | `coclawbro-*-win-arm64.zip` |

```bash
# Linux/macOS
tar xzf coclawbro-*-linux-x64.tar.gz
chmod +x coclawbro
./coclawbro
```

### Verify Downloads

Each release includes `SHA256SUMS.txt` for verification:

```bash
sha256sum -c SHA256SUMS.txt
```

## Docker

### Quick Start

```bash
docker run -p 8080:8080 ghcr.io/{owner}/coclawbro:latest
```

### Docker Compose

```bash
docker compose up -d
```

### Available Tags

| Tag | Description |
|-----|-------------|
| `latest` | Latest release |
| `vX.Y.Z` | Specific version |
| `X.Y` | Latest patch of major.minor |

Supported architectures: `linux/amd64`, `linux/arm64`

## Development

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Running Tests

```bash
dotnet test
```

### Building

```bash
# Debug build
dotnet build

# Release AOT binary (current platform)
dotnet publish -c Release -p:PublishDir=./publish CoClawBro.App/CoClawBro.App.csproj
```

### Project Layout

| Project | Purpose |
|---------|---------|
| `CoClawBro.Core/` | Class library — protocol translation, auth, proxy logic |
| `CoClawBro.App/` | Console application — entry point, terminal UI |
| `CoClawBro.Tests/` | Tests (TUnit + WireMock.Net) |

### Design Philosophy

The project follows [A Philosophy of Software Design](https://web.stanford.edu/~ouster/cgi-bin/book.php) (deep modules) and [Grokking Simplicity](https://www.manning.com/books/grokking-simplicity) (actions/calculations/data separation):

- **Data** (`Core/Data/`) — Immutable records, zero behavior
- **Calculations** (`Core/Translation/`) — Pure functions, no I/O
- **Actions** (`Core/Proxy/`, `Core/Auth/`) — Side effects isolated in thin wrappers

## License

MIT
