using Microsoft.AspNetCore.Identity;

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
        if (await userManager.FindByEmailAsync(DefaultAdminEmail) is not null)
        {
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
    }
}
