using Microsoft.AspNetCore.Identity;
using PilotsRUs.Shared.SDK.Auth;

namespace PilotsRUs.API.WebApi.Data;

/// <summary>
/// Creates a default admin user in Development so there's always a way to exercise the login flow
/// locally without a registration endpoint (none exists yet - infra-only scope, see CLAUDE.md).
/// </summary>
public static class DevelopmentDataSeeder
{
    public const string DefaultAdminEmail = "hasse29@hotmail.com";
    public const string DefaultAdminPassword = "admin";
    public const string DefaultAdminFirstName = "Hans";
    public const string DefaultAdminLastName = "Sjödin";

    public static async Task SeedDevelopmentAdminAsync(UserManager<ApplicationUser> userManager)
    {
        var existing = await userManager.FindByEmailAsync(DefaultAdminEmail);
        if (existing is not null)
        {
            if (!await userManager.IsInRoleAsync(existing, AuthConstants.AdminRoleName))
            {
                await userManager.AddToRoleAsync(existing, AuthConstants.AdminRoleName);
            }
            return;
        }

        var user = new ApplicationUser
        {
            UserName = DefaultAdminEmail,
            Email = DefaultAdminEmail,
            EmailConfirmed = true,
            FirstName = DefaultAdminFirstName,
            LastName = DefaultAdminLastName
        };

        var result = await userManager.CreateAsync(user, DefaultAdminPassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        var roleResult = await userManager.AddToRoleAsync(user, AuthConstants.AdminRoleName);
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", roleResult.Errors.Select(e => e.Description)));
        }
    }
}
