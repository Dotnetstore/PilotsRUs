using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PilotsRUs.API.WebApi.Data;
using PilotsRUs.API.WebApi.Features.Auth;

namespace PilotsRUs.API.WebApi.Tests.Features.Auth;

public sealed class Argon2PasswordHasherTests
{
    private readonly Argon2PasswordHasher _hasher = new(Options.Create(new Argon2Options()));
    private readonly ApplicationUser _user = new() { UserName = "test@pilotsrus.test", Email = "test@pilotsrus.test", FirstName = "Test", LastName = "User" };

    [Fact]
    public void HashPassword_ThenVerify_WithCorrectPassword_ReturnsSuccess()
    {
        var hash = _hasher.HashPassword(_user, "correct-password");

        var result = _hasher.VerifyHashedPassword(_user, hash, "correct-password");

        Assert.Equal(PasswordVerificationResult.Success, result);
    }

    [Fact]
    public void HashPassword_ThenVerify_WithWrongPassword_ReturnsFailed()
    {
        var hash = _hasher.HashPassword(_user, "correct-password");

        var result = _hasher.VerifyHashedPassword(_user, hash, "wrong-password");

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-valid-hash")]
    [InlineData("AQAAAAEAACcQAAAAEIdyDzMVN9sZ8QzL5J6vXA==")] // realistic-looking legacy PBKDF2-format hash, no '$' delimiters
    [InlineData("$argon2id$v=19$m=19456,t=2,p=1$onlysalt")] // truncated - missing the hash segment
    [InlineData("$argon2id$v=19$m=notanumber,t=2,p=1$c2FsdA==$aGFzaA==")] // non-numeric parameter
    public void VerifyHashedPassword_WithMalformedOrIncompatibleHash_ReturnsFailedInsteadOfThrowing(string malformedHash)
    {
        var result = _hasher.VerifyHashedPassword(_user, malformedHash, "any-password");

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    [Fact]
    public void VerifyHashedPassword_WhenConfiguredParametersChange_ReturnsSuccessRehashNeeded()
    {
        var oldOptions = Options.Create(new Argon2Options { MemoryKib = 8, Iterations = 1, Parallelism = 1 });
        var oldHasher = new Argon2PasswordHasher(oldOptions);
        var hash = oldHasher.HashPassword(_user, "correct-password");

        var newOptions = Options.Create(new Argon2Options { MemoryKib = 16, Iterations = 1, Parallelism = 1 });
        var newHasher = new Argon2PasswordHasher(newOptions);

        var result = newHasher.VerifyHashedPassword(_user, hash, "correct-password");

        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, result);
    }
}
