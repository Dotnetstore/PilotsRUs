using Microsoft.AspNetCore.Identity;
using PilotsRUs.API.WebApi.Data;

namespace PilotsRUs.API.WebApi.Features.Auth;

/// <summary>
/// Thin IPasswordHasher&lt;ApplicationUser&gt; adapter over IArgon2Hasher, so Identity's
/// UserManager/SignInManager can use Argon2id via the standard extensibility point. All the actual
/// Argon2id/PHC-string logic lives in Argon2Hasher (shared with Account, which has no
/// IPasswordHasher&lt;TUser&gt; of its own since it doesn't go through Identity).
/// </summary>
public sealed class Argon2PasswordHasher(IArgon2Hasher hasher) : IPasswordHasher<ApplicationUser>
{
    public string HashPassword(ApplicationUser user, string password) => hasher.HashPassword(password);

    public PasswordVerificationResult VerifyHashedPassword(ApplicationUser user, string hashedPassword, string providedPassword) =>
        hasher.VerifyPassword(hashedPassword, providedPassword) switch
        {
            Argon2VerificationResult.Success => PasswordVerificationResult.Success,
            Argon2VerificationResult.SuccessRehashNeeded => PasswordVerificationResult.SuccessRehashNeeded,
            _ => PasswordVerificationResult.Failed
        };
}
