using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using PilotsRUs.API.WebApi.Data;

namespace PilotsRUs.API.WebApi.Features.Auth;

/// <summary>
/// Replaces Identity's default PBKDF2-based PasswordHasher with Argon2id. Parameters follow OWASP's
/// 2023 Password Storage Cheat Sheet minimum baseline for Argon2id. Stored hashes use the standard PHC
/// string format so parameters are self-describing and can be tuned later without invalidating existing
/// hashes - VerifyHashedPassword parses the parameters out of the stored string rather than assuming the
/// current constants, and returns SuccessRehashNeeded when they differ so Identity transparently rehashes
/// on the user's next successful login.
/// </summary>
public sealed class Argon2PasswordHasher : IPasswordHasher<ApplicationUser>
{
    private const int MemoryKib = 19456;
    private const int Iterations = 2;
    private const int Parallelism = 1;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string HashPassword(ApplicationUser user, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = ComputeHash(password, salt, MemoryKib, Iterations, Parallelism);
        return $"$argon2id$v=19$m={MemoryKib},t={Iterations},p={Parallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public PasswordVerificationResult VerifyHashedPassword(ApplicationUser user, string hashedPassword, string providedPassword)
    {
        var (memoryKib, iterations, parallelism, salt, expectedHash) = Parse(hashedPassword);
        var actualHash = ComputeHash(providedPassword, salt, memoryKib, iterations, parallelism);

        if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
        {
            return PasswordVerificationResult.Failed;
        }

        return memoryKib == MemoryKib && iterations == Iterations && parallelism == Parallelism
            ? PasswordVerificationResult.Success
            : PasswordVerificationResult.SuccessRehashNeeded;
    }

    private static byte[] ComputeHash(string password, byte[] salt, int memoryKib, int iterations, int parallelism)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = parallelism,
            Iterations = iterations,
            MemorySize = memoryKib
        };
        return argon2.GetBytes(HashSize);
    }

    private static (int MemoryKib, int Iterations, int Parallelism, byte[] Salt, byte[] Hash) Parse(string encoded)
    {
        // $argon2id$v=19$m=19456,t=2,p=1$<salt>$<hash>
        var parts = encoded.Split('$', StringSplitOptions.RemoveEmptyEntries);
        var parameters = parts[2].Split(',').Select(p => int.Parse(p.Split('=')[1])).ToArray();
        return (parameters[0], parameters[1], parameters[2], Convert.FromBase64String(parts[3]), Convert.FromBase64String(parts[4]));
    }
}
