using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PilotsRUs.API.WebApi.Data;
using PilotsRUs.API.WebApi.Features.Auth;
using PilotsRUs.Shared.SDK.Accounts;
using PilotsRUs.Shared.SDK.Auth;

namespace PilotsRUs.API.WebApi.Features.Accounts;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/account");

        group.MapPost("/register", async (
            RegisterAccountRequest request,
            IDbContextFactory<ApplicationDbContext> dbContextFactory,
            IArgon2Hasher hasher) =>
        {
            if (request.Password.Length < 8)
            {
                return Results.BadRequest("Password must be at least 8 characters long.");
            }

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            if (await dbContext.Accounts.AnyAsync(a => a.Email == request.Email))
            {
                return Results.Conflict($"An account with email '{request.Email}' already exists.");
            }

            var account = new Account
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                PasswordHash = hasher.HashPassword(request.Password),
                DisplayName = request.DisplayName,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            dbContext.Accounts.Add(account);

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Closes the TOCTOU gap between the AnyAsync check above and this insert.
                return Results.Conflict($"An account with email '{request.Email}' already exists.");
            }

            // No tokens returned - registration and login are two separate steps; the caller navigates to
            // the login screen next.
            return Results.Created($"/account/{account.Id}", new AccountResponse(account.Id, account.Email, account.DisplayName));
        })
        .WithName("RegisterAccount")
        .AllowAnonymous();

        group.MapPost("/login", async (
            AccountLoginRequest request,
            IDbContextFactory<ApplicationDbContext> dbContextFactory,
            IArgon2Hasher hasher,
            IJwtTokenService jwtTokenService,
            IAccountRefreshTokenService accountRefreshTokenService,
            IOptions<JwtOptions> jwtOptions) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var account = await dbContext.Accounts.SingleOrDefaultAsync(a => a.Email == request.Email);
            if (account is null)
            {
                return Results.Unauthorized();
            }

            var verification = hasher.VerifyPassword(account.PasswordHash, request.Password);
            if (verification == Argon2VerificationResult.Failed)
            {
                return Results.Unauthorized();
            }

            if (verification == Argon2VerificationResult.SuccessRehashNeeded)
            {
                // Mirrors what UserManager does transparently for ApplicationUser via
                // PasswordVerificationResult.SuccessRehashNeeded - done manually here since Account has no
                // UserManager<TUser> of its own.
                account.PasswordHash = hasher.HashPassword(request.Password);
                await dbContext.SaveChangesAsync();
            }

            var (accessToken, accessExpiresAtUtc) = jwtTokenService.CreateToken(
                account.Id, account.Email, jwtOptions.Value.AccountAudience,
                [new Claim(ClaimTypes.Name, account.DisplayName)], []);
            var (refreshToken, refreshExpiresAtUtc) = await accountRefreshTokenService.IssueAsync(account.Id, Guid.NewGuid());

            return Results.Ok(new AccountLoginResponse(accessToken, accessExpiresAtUtc, refreshToken, refreshExpiresAtUtc, account.DisplayName));
        })
        .WithName("AccountLogin")
        .AllowAnonymous();

        group.MapPost("/refresh", async (
            RefreshTokenRequest request,
            IAccountRefreshTokenService accountRefreshTokenService,
            IDbContextFactory<ApplicationDbContext> dbContextFactory,
            IJwtTokenService jwtTokenService,
            IOptions<JwtOptions> jwtOptions) =>
        {
            var result = await accountRefreshTokenService.RotateAsync(request.RefreshToken);
            if (result.Outcome != AccountRefreshTokenOutcome.Success)
            {
                return Results.Unauthorized();
            }

            var (accessToken, accessExpiresAtUtc) = jwtTokenService.CreateToken(
                result.Account!.Id, result.Account.Email, jwtOptions.Value.AccountAudience,
                [new Claim(ClaimTypes.Name, result.Account.DisplayName)], []);

            return Results.Ok(new AccountLoginResponse(accessToken, accessExpiresAtUtc, result.NewRawToken!, result.NewExpiresAtUtc!.Value, result.Account.DisplayName));
        })
        .WithName("RefreshAccountToken")
        .AllowAnonymous();

        group.MapPost("/logout", async (RefreshTokenRequest request, IAccountRefreshTokenService accountRefreshTokenService) =>
        {
            await accountRefreshTokenService.RevokeByRawTokenAsync(request.RefreshToken, "logout");
            return Results.NoContent();
        })
        .WithName("AccountLogout")
        .AllowAnonymous();

        group.MapGet("/me", (ClaimsPrincipal user) =>
        {
            var email = user.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
            var displayName = user.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
            return Results.Ok(new CurrentAccountResponse(email, displayName));
        })
        .WithName("GetCurrentAccount")
        .RequireAuthorization("Account");

        return app;
    }
}
