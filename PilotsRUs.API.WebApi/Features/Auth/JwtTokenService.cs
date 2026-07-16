using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace PilotsRUs.API.WebApi.Features.Auth;

public interface IJwtTokenService
{
    (string Token, DateTimeOffset ExpiresAtUtc) CreateToken(
        Guid subjectId, string email, string audience, IEnumerable<Claim> additionalClaims, IEnumerable<string> roles);
}

// Shared signing mechanics (key/issuer/expiry) for both Admin (ApplicationUser) and Account tokens - the
// audience differs per caller (see AuthServiceCollectionExtensions.AddApplicationJwtAuth's two registered
// JWT bearer schemes), which is what keeps the two token types from validating against each other's
// endpoints despite sharing this one signing implementation.
public sealed class JwtTokenService(IOptions<JwtOptions> options) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public (string Token, DateTimeOffset ExpiresAtUtc) CreateToken(
        Guid subjectId, string email, string audience, IEnumerable<Claim> additionalClaims, IEnumerable<string> roles)
    {
        var expiresAtUtc = DateTimeOffset.UtcNow.Add(_options.Expiry);

        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, subjectId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ..additionalClaims,
            ..roles.Select(role => new Claim(ClaimTypes.Role, role))
        ];

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: audience,
            claims: claims,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: signingCredentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
