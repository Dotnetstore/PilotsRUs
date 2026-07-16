using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PilotsRUs.API.WebApi.Data;
using PilotsRUs.Shared.SDK.Auth;

namespace PilotsRUs.API.WebApi.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/login", async (
            LoginRequest request,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IJwtTokenService jwtTokenService,
            IRefreshTokenService refreshTokenService,
            IOptions<JwtOptions> jwtOptions) =>
        {
            var user = await userManager.FindByEmailAsync(request.Email);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                return Results.Unauthorized();
            }

            var roles = await userManager.GetRolesAsync(user);
            var (accessToken, accessExpiresAtUtc) = jwtTokenService.CreateToken(
                user.Id, user.Email ?? string.Empty, jwtOptions.Value.Audience,
                [new Claim(ClaimTypes.GivenName, user.FirstName), new Claim(ClaimTypes.Surname, user.LastName)], roles);
            var (refreshToken, refreshExpiresAtUtc) = await refreshTokenService.IssueAsync(user.Id, Guid.NewGuid());

            return Results.Ok(new LoginResponse(accessToken, accessExpiresAtUtc, refreshToken, refreshExpiresAtUtc, (IReadOnlyList<string>)roles));
        })
        .WithName("Login")
        .AllowAnonymous();

        app.MapPost("/auth/refresh", async (
            RefreshTokenRequest request,
            IRefreshTokenService refreshTokenService,
            UserManager<ApplicationUser> userManager,
            IJwtTokenService jwtTokenService,
            IOptions<JwtOptions> jwtOptions) =>
        {
            var result = await refreshTokenService.RotateAsync(request.RefreshToken);
            if (result.Outcome != RefreshTokenOutcome.Success)
            {
                return Results.Unauthorized();
            }

            var roles = await userManager.GetRolesAsync(result.User!);
            var (accessToken, accessExpiresAtUtc) = jwtTokenService.CreateToken(
                result.User!.Id, result.User.Email ?? string.Empty, jwtOptions.Value.Audience,
                [new Claim(ClaimTypes.GivenName, result.User.FirstName), new Claim(ClaimTypes.Surname, result.User.LastName)], roles);

            return Results.Ok(new LoginResponse(accessToken, accessExpiresAtUtc, result.NewRawToken!, result.NewExpiresAtUtc!.Value, (IReadOnlyList<string>)roles));
        })
        .WithName("RefreshToken")
        .AllowAnonymous();

        app.MapPost("/auth/logout", async (RefreshTokenRequest request, IRefreshTokenService refreshTokenService) =>
        {
            await refreshTokenService.RevokeByRawTokenAsync(request.RefreshToken, "logout");
            return Results.NoContent();
        })
        .WithName("Logout")
        .AllowAnonymous();

        app.MapGet("/auth/me", (ClaimsPrincipal user) =>
        {
            var email = user.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
            var firstName = user.FindFirstValue(ClaimTypes.GivenName) ?? string.Empty;
            var lastName = user.FindFirstValue(ClaimTypes.Surname) ?? string.Empty;
            var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            return Results.Ok(new CurrentUserResponse(email, firstName, lastName, roles));
        })
        .WithName("GetCurrentUser")
        .RequireAuthorization();

        return app;
    }
}
