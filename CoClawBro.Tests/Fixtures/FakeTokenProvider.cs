using CoClawBro.Auth;

namespace CoClawBro.Tests.Fixtures;

public sealed class FakeTokenProvider : ITokenProvider
{
    public ValueTask<string> GetTokenAsync(CancellationToken ct = default) => new("fake-token");
}
