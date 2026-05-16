using System.Security.Cryptography;
using CookBot.Infrastructure.AI;

namespace CookBot.Tests.AI;

/// <summary>
/// Phase 9 / Plan 09-04 / PROD-10 + PITFALL C4.
///
/// SecretRedactor must scrub BOTH plaintext AI keys (existing behavior) AND the
/// CfDJ8-prefixed ciphertext blobs that show up in <see cref="CryptographicException"/>
/// messages when Unprotect fails on a corrupted row. Without this coverage, an attacker
/// who has filesystem access to the logs but not to cookbot.db could pivot to a
/// known-ciphertext attack.
/// </summary>
public class SecretRedactorDecryptPathTests
{
    [Fact]
    public void Redact_CryptographicException_DoesNotLeakCiphertext()
    {
        // 50 chars — well above the 44-char sentinel threshold the read-path uses to gate
        // Unprotect, and matches the shape of a real Data Protection blob (CfDJ8 + base64url).
        const string ciphertext = "CfDJ8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        var ex = new CryptographicException($"The payload was invalid: {ciphertext}");

        var result = SecretRedactor.Redact($"Failed to decrypt: {ex.Message}");

        Assert.DoesNotContain(ciphertext, result);
        Assert.Contains("[REDACTED-CIPHERTEXT]", result);
    }

    [Fact]
    public void Redact_CryptographicException_DoesNotLeakPlaintextWhenResolvedKeyProvided()
    {
        const string plaintext = "sk-ant-MyRealKey123XYZ";
        var ex = new CryptographicException($"Failed validating: {plaintext}");

        var result = SecretRedactor.Redact($"Failed to decrypt: {ex.Message}", resolvedKey: plaintext);

        Assert.DoesNotContain(plaintext, result);
        Assert.Contains("[REDACTED]", result);
    }
}
