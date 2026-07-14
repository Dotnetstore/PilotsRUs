using Microsoft.AspNetCore.Identity;

namespace PilotsRUs.API.WebApi.Data;

/// <summary>
/// Creates a default admin user in Development so there's always a way to exercise the login flow
/// locally without a registration endpoint (none exists yet - infra-only scope, see CLAUDE.md).
/// </summary>
public static class DevelopmentDataSeeder
{
    public const string DefaultAdminEmail = "admin@pilotsrus.local";
    public const string DefaultAdminPassword = "P@ssw0rd123!";

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
            EmailConfirmed = true
        };

        await userManager.CreateAsync(user, DefaultAdminPassword);
    }
}
