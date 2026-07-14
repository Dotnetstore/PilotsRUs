using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using PilotsRUs.API.WebApi.Data;
using PilotsRUs.Shared.SDK.Auth;

namespace PilotsRUs.API.WebApi.Features.Auth;

public static class LoginEndpoint
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/login", async (
            LoginRequest request,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IJwtTokenService jwtTokenService) =>
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
            var (token, expiresAtUtc) = jwtTokenService.CreateToken(user, roles);

            return Results.Ok(new LoginResponse(token, expiresAtUtc));
        })
        .WithName("Login")
        .AllowAnonymous();

        app.MapGet("/auth/me", (ClaimsPrincipal user) =>
        {
            var email = user.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
            var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            return Results.Ok(new CurrentUserResponse(email, roles));
        })
        .WithName("GetCurrentUser")
        .RequireAuthorization();

        return app;
    }
}
