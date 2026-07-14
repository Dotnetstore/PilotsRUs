using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PilotsRUs.API.WebApi.Data;

namespace PilotsRUs.API.WebApi.Features.Auth;

/// <summary>
/// Replaces Identity's default PBKDF2-based PasswordHasher with Argon2id. Parameters follow OWASP's
/// 2023 Password Storage Cheat Sheet minimum baseline for Argon2id (configurable via Argon2Options,
/// section "Argon2"). Stored hashes use the standard PHC string format so parameters - including the
/// output hash length - are self-describing and can be tuned later without invalidating existing hashes:
/// VerifyHashedPassword parses everything (memory/iterations/parallelism/salt/hash, and derives the
/// expected output length from the stored hash's own byte length) out of the stored string rather than
/// assuming the current constants, and returns SuccessRehashNeeded when the parsed cost parameters differ
/// from the currently configured ones, so Identity transparently rehashes on the user's next successful
/// login. IPasswordHasher&lt;TUser&gt;.VerifyHashedPassword must never throw for a malformed/incompatible
/// hash - Identity's own default hasher treats that as PasswordVerificationResult.Failed rather than an
/// exception, and callers such as UserManager.VerifyPasswordAsync do not guard against it throwing - so
/// parsing failures here are caught and treated the same way.
/// </summary>
public sealed class Argon2PasswordHasher(IOptions<Argon2Options> options) : IPasswordHasher<ApplicationUser>
{
    private readonly Argon2Options _options = options.Value;

    public string HashPassword(ApplicationUser user, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(_options.SaltSize);
        var hash = ComputeHash(password, salt, _options.MemoryKib, _options.Iterations, _options.Parallelism, _options.HashSize);
        return $"$argon2id$v=19$m={_options.MemoryKib},t={_options.Iterations},p={_options.Parallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public PasswordVerificationResult VerifyHashedPassword(ApplicationUser user, string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword))
        {
            return PasswordVerificationResult.Failed;
        }

        (int MemoryKib, int Iterations, int Parallelism, byte[] Salt, byte[] Hash) parsed;
        try
        {
            parsed = Parse(hashedPassword);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or FormatException or OverflowException)
        {
            // Malformed or incompatible hash (e.g. a hash from Identity's old default PasswordHasher,
            // predating this hasher, or a corrupted value) - fail the login attempt cleanly rather than
            // letting the exception escape through SignInManager/UserManager as an unhandled 500.
            return PasswordVerificationResult.Failed;
        }

        var actualHash = ComputeHash(providedPassword, parsed.Salt, parsed.MemoryKib, parsed.Iterations, parsed.Parallelism, parsed.Hash.Length);

        if (!CryptographicOperations.FixedTimeEquals(actualHash, parsed.Hash))
        {
            return PasswordVerificationResult.Failed;
        }

        return parsed.MemoryKib == _options.MemoryKib
            && parsed.Iterations == _options.Iterations
            && parsed.Parallelism == _options.Parallelism
            && parsed.Hash.Length == _options.HashSize
            ? PasswordVerificationResult.Success
            : PasswordVerificationResult.SuccessRehashNeeded;
    }

    private static byte[] ComputeHash(string password, byte[] salt, int memoryKib, int iterations, int parallelism, int hashSize)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = parallelism,
            Iterations = iterations,
            MemorySize = memoryKib
        };
        return argon2.GetBytes(hashSize);
    }

    private static (int MemoryKib, int Iterations, int Parallelism, byte[] Salt, byte[] Hash) Parse(string encoded)
    {
        // $argon2id$v=19$m=19456,t=2,p=1$<salt>$<hash>
        var parts = encoded.Split('$', StringSplitOptions.RemoveEmptyEntries);
        var parameters = parts[2].Split(',').Select(p => int.Parse(p.Split('=')[1])).ToArray();
        return (parameters[0], parameters[1], parameters[2], Convert.FromBase64String(parts[3]), Convert.FromBase64String(parts[4]));
    }
}
