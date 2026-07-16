using Microsoft.Extensions.Options;
using PilotsRUs.API.WebApi.Features.Auth;

namespace PilotsRUs.API.WebApi.Tests.Features.Auth;

public sealed class Argon2HasherTests
{
    private readonly Argon2Hasher _hasher = new(Options.Create(new Argon2Options()));

    [Fact]
    public void HashPassword_ThenVerify_WithCorrectPassword_ReturnsSuccess()
    {
        var hash = _hasher.HashPassword("correct-password");

        var result = _hasher.VerifyPassword(hash, "correct-password");

        Assert.Equal(Argon2VerificationResult.Success, result);
    }

    [Fact]
    public void HashPassword_ThenVerify_WithWrongPassword_ReturnsFailed()
    {
        var hash = _hasher.HashPassword("correct-password");

        var result = _hasher.VerifyPassword(hash, "wrong-password");

        Assert.Equal(Argon2VerificationResult.Failed, result);
    }

    [Fact]
    public void VerifyPassword_WhenConfiguredParametersChange_ReturnsSuccessRehashNeeded()
    {
        var oldHasher = new Argon2Hasher(Options.Create(new Argon2Options { MemoryKib = 8, Iterations = 1, Parallelism = 1 }));
        var hash = oldHasher.HashPassword("correct-password");

        var newHasher = new Argon2Hasher(Options.Create(new Argon2Options { MemoryKib = 16, Iterations = 1, Parallelism = 1 }));

        var result = newHasher.VerifyPassword(hash, "correct-password");

        Assert.Equal(Argon2VerificationResult.SuccessRehashNeeded, result);
    }

    [Fact]
    public void VerifyPassword_WithMalformedHash_ReturnsFailedInsteadOfThrowing()
    {
        var result = _hasher.VerifyPassword("not-a-valid-hash", "any-password");

        Assert.Equal(Argon2VerificationResult.Failed, result);
    }
}
