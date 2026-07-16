using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace PilotsRUs.API.WebApi.Features.Auth;

public enum Argon2VerificationResult { Failed, Success, SuccessRehashNeeded }

/// <summary>
/// Identity-agnostic Argon2id hashing/verification, extracted from Argon2PasswordHasher so both
/// ApplicationUser (via that IPasswordHasher&lt;ApplicationUser&gt; adapter) and Account (which has no
/// UserManager/IPasswordHasher&lt;TUser&gt; of its own) can share the exact same PHC-string format and
/// parameters. See Argon2PasswordHasher's original remarks for the full PHC-string/rehash rationale -
/// unchanged here, just no longer parameterized by ApplicationUser.
/// </summary>
public interface IArgon2Hasher
{
    string HashPassword(string password);
    Argon2VerificationResult VerifyPassword(string hashedPassword, string providedPassword);
}

public sealed class Argon2Hasher(IOptions<Argon2Options> options) : IArgon2Hasher
{
    private readonly Argon2Options _options = options.Value;

    public string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(_options.SaltSize);
        var hash = ComputeHash(password, salt, _options.MemoryKib, _options.Iterations, _options.Parallelism, _options.HashSize);
        return $"$argon2id$v=19$m={_options.MemoryKib},t={_options.Iterations},p={_options.Parallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public Argon2VerificationResult VerifyPassword(string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword))
        {
            return Argon2VerificationResult.Failed;
        }

        (int MemoryKib, int Iterations, int Parallelism, byte[] Salt, byte[] Hash) parsed;
        try
        {
            parsed = Parse(hashedPassword);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or FormatException or OverflowException)
        {
            // Malformed or incompatible hash - fail cleanly rather than letting the exception escape.
            return Argon2VerificationResult.Failed;
        }

        var actualHash = ComputeHash(providedPassword, parsed.Salt, parsed.MemoryKib, parsed.Iterations, parsed.Parallelism, parsed.Hash.Length);

        if (!CryptographicOperations.FixedTimeEquals(actualHash, parsed.Hash))
        {
            return Argon2VerificationResult.Failed;
        }

        return parsed.MemoryKib == _options.MemoryKib
            && parsed.Iterations == _options.Iterations
            && parsed.Parallelism == _options.Parallelism
            && parsed.Hash.Length == _options.HashSize
            ? Argon2VerificationResult.Success
            : Argon2VerificationResult.SuccessRehashNeeded;
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
