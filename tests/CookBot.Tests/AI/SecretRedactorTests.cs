using CookBot.Infrastructure.AI;

namespace CookBot.Tests.AI;

public class SecretRedactorTests
{
    [Fact]
    public void Redact_StripsApiKeyPatternAndHeaderValue_FromCanonicalFixture()
    {
        // D-18 canonical fixture
        var input = "error: x-api-key: sk-ant-foo123 with body {api_key: sk-ant-bar456}";
        var result = SecretRedactor.Redact(input);

        Assert.DoesNotContain("sk-ant-", result);
        Assert.DoesNotContain(": sk-ant", result);
    }

    [Fact]
    public void Redact_StripsAuthorizationHeaderValue()
    {
        var result = SecretRedactor.Redact("authorization: Bearer abc123def");
        Assert.DoesNotContain("Bearer abc123def", result);
        Assert.Contains("[REDACTED]", result);
    }

    [Fact]
    public void Redact_StripsVerbatimResolvedKey()
    {
        var result = SecretRedactor.Redact(
            "my key is my-custom-secret-XYZ embedded here",
            resolvedKey: "my-custom-secret-XYZ");
        Assert.DoesNotContain("my-custom-secret-XYZ", result);
        Assert.Contains("[REDACTED]", result);
    }

    [Fact]
    public void Redact_PreservesInputWithoutSecrets()
    {
        const string clean = "nothing sensitive here, just a regular error message";
        Assert.Equal(clean, SecretRedactor.Redact(clean));
    }

    [Fact]
    public void Redact_EmptyOrNullInput_ReturnsInputWithoutThrowing()
    {
        Assert.Equal("", SecretRedactor.Redact(""));
        Assert.Null(SecretRedactor.Redact(null!));
    }

    [Fact]
    public void Redact_IsCaseInsensitive_ForHeaderAndKeyPatterns()
    {
        var result = SecretRedactor.Redact("X-API-KEY: sk-ant-UPPER123");
        Assert.DoesNotContain("sk-ant-UPPER123", result);
    }
}
