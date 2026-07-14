using Microsoft.AspNetCore.Identity;
using PilotsRUs.Shared.SDK.Auth;

namespace PilotsRUs.API.WebApi.Data;

/// <summary>
/// Seeds roles that role-gated endpoints/pages depend on. Runs unconditionally (every environment),
/// unlike <see cref="DevelopmentDataSeeder"/> which stays dev-only.
/// </summary>
public static class RoleSeeder
{
    public static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager)
    {
        if (await roleManager.RoleExistsAsync(AuthConstants.AdminRoleName))
        {
            return;
        }

        var result = await roleManager.CreateAsync(new ApplicationRole { Name = AuthConstants.AdminRoleName });
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
