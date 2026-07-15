using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using PilotsRUs.API.WebApi.Data;
using PilotsRUs.API.WebApi.Features.Auth;
using PilotsRUs.Shared.SDK.Auth;
using PilotsRUs.Shared.SDK.Users;

namespace PilotsRUs.API.WebApi.Features.Users;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users").RequireAuthorization("AdminOnly");

        group.MapGet("/", async (UserManager<ApplicationUser> userManager) =>
        {
            // No pagination this pass - small expected user count (internal admin tool, not a public
            // directory). Hook Skip/Take + page query params into userManager.Users here if it grows.
            var users = userManager.Users.OrderBy(u => u.Email).ToList();

            // Batch admin-role lookup (one query for the whole list) instead of calling IsInRoleAsync per
            // user via ToResponseAsync - that was an N+1 query pattern. IsLockedOutAsync stays per-user
            // since it only reads LockoutEnabled/LockoutEnd off the already-loaded entity, no extra
            // DB round trip.
            var adminUserIds = (await userManager.GetUsersInRoleAsync(AuthConstants.AdminRoleName))
                .Select(u => u.Id)
                .ToHashSet();

            var responses = new List<UserResponse>();
            foreach (var user in users)
            {
                var isLockedOut = await userManager.IsLockedOutAsync(user);
                responses.Add(new UserResponse(user.Id, user.Email ?? string.Empty, user.FirstName, user.LastName, adminUserIds.Contains(user.Id), isLockedOut));
            }
            return Results.Ok(responses);
        }).WithName("GetUsers");

        group.MapGet("/{id:guid}", async (Guid id, UserManager<ApplicationUser> userManager) =>
        {
            var user = await userManager.FindByIdAsync(id.ToString());
            return user is null ? Results.NotFound() : Results.Ok(await ToResponseAsync(userManager, user));
        }).WithName("GetUserById");

        group.MapPost("/", async (CreateUserRequest request, UserManager<ApplicationUser> userManager) =>
        {
            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                EmailConfirmed = true,
                FirstName = request.FirstName,
                LastName = request.LastName
            };

            var createResult = await userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                return IdentityErrorResult(createResult.Errors);
            }

            if (request.IsAdmin)
            {
                var roleResult = await userManager.AddToRoleAsync(user, AuthConstants.AdminRoleName);
                if (!roleResult.Succeeded)
                {
                    // User row already created - roll back rather than leave an orphaned non-admin user
                    // behind a failed request.
                    await userManager.DeleteAsync(user);
                    return IdentityErrorResult(roleResult.Errors);
                }
            }

            return Results.Created($"/users/{user.Id}", await ToResponseAsync(userManager, user));
        }).WithName("CreateUser");

        group.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest request, UserManager<ApplicationUser> userManager) =>
        {
            var user = await userManager.FindByIdAsync(id.ToString());
            if (user is null)
            {
                return Results.NotFound();
            }

            var isCurrentlyAdmin = await userManager.IsInRoleAsync(user, AuthConstants.AdminRoleName);
            if (isCurrentlyAdmin && !request.IsAdmin)
            {
                var otherActiveAdmins = await CountOtherActiveAdminsAsync(userManager, excludingUserId: user.Id);
                if (otherActiveAdmins == 0)
                {
                    return Results.Conflict("Cannot remove the Admin role from the last remaining active admin.");
                }
            }

            user.Email = request.Email;
            user.UserName = request.Email; // keep UserName == Email, per the existing seeder/RequireUniqueEmail convention
            user.FirstName = request.FirstName;
            user.LastName = request.LastName;

            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return IdentityErrorResult(updateResult.Errors);
            }

            if (request.IsAdmin && !isCurrentlyAdmin)
            {
                var addResult = await userManager.AddToRoleAsync(user, AuthConstants.AdminRoleName);
                if (!addResult.Succeeded)
                {
                    return IdentityErrorResult(addResult.Errors);
                }
            }
            else if (!request.IsAdmin && isCurrentlyAdmin)
            {
                var removeResult = await userManager.RemoveFromRoleAsync(user, AuthConstants.AdminRoleName);
                if (!removeResult.Succeeded)
                {
                    return IdentityErrorResult(removeResult.Errors);
                }
            }

            return Results.Ok(await ToResponseAsync(userManager, user));
        }).WithName("UpdateUser");

        group.MapPost("/{id:guid}/deactivate", async (
            Guid id, ClaimsPrincipal caller, UserManager<ApplicationUser> userManager, IRefreshTokenService refreshTokenService) =>
        {
            // ASP.NET Core's JWT bearer handler remaps the short "sub" claim to ClaimTypes.NameIdentifier
            // by default (same MapInboundClaims behavior /auth/me already relies on for Email/GivenName/
            // Surname/Role) - the raw JwtRegisteredClaimNames.Sub name is not preserved on ClaimsPrincipal.
            var callerIdRaw = caller.FindFirstValue(ClaimTypes.NameIdentifier);
            if (callerIdRaw is not null && Guid.TryParse(callerIdRaw, out var callerId) && callerId == id)
            {
                return Results.BadRequest("You cannot deactivate your own account.");
            }

            var user = await userManager.FindByIdAsync(id.ToString());
            if (user is null)
            {
                return Results.NotFound();
            }

            if (await userManager.IsInRoleAsync(user, AuthConstants.AdminRoleName))
            {
                var otherActiveAdmins = await CountOtherActiveAdminsAsync(userManager, excludingUserId: user.Id);
                if (otherActiveAdmins == 0)
                {
                    return Results.Conflict("Cannot deactivate the last remaining active admin.");
                }
            }

            await userManager.SetLockoutEnabledAsync(user, true);
            await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            await refreshTokenService.RevokeAllForUserAsync(user.Id, "deactivated");

            return Results.NoContent();
        }).WithName("DeactivateUser");

        group.MapPost("/{id:guid}/reactivate", async (Guid id, UserManager<ApplicationUser> userManager) =>
        {
            var user = await userManager.FindByIdAsync(id.ToString());
            if (user is null)
            {
                return Results.NotFound();
            }

            await userManager.SetLockoutEndDateAsync(user, null);
            return Results.NoContent();
        }).WithName("ReactivateUser");

        return app;
    }

    // Shared by Update (role removal) and Deactivate (lockout): counts admins other than the target who
    // are NOT locked out. Both actions must be blocked at 0, not just "still has the role" - a role-holder
    // who's already locked out can't log in to exercise it, so counting them as "remaining" would let the
    // system reach a state where every Admin-role user is locked out and nobody can undo it.
    private static async Task<int> CountOtherActiveAdminsAsync(UserManager<ApplicationUser> userManager, Guid excludingUserId)
    {
        var admins = await userManager.GetUsersInRoleAsync(AuthConstants.AdminRoleName);
        var count = 0;
        foreach (var admin in admins)
        {
            if (admin.Id == excludingUserId)
            {
                continue;
            }

            if (!await userManager.IsLockedOutAsync(admin))
            {
                count++;
            }
        }
        return count;
    }

    private static async Task<UserResponse> ToResponseAsync(UserManager<ApplicationUser> userManager, ApplicationUser user)
    {
        var isAdmin = await userManager.IsInRoleAsync(user, AuthConstants.AdminRoleName);
        var isLockedOut = await userManager.IsLockedOutAsync(user);
        return new UserResponse(user.Id, user.Email ?? string.Empty, user.FirstName, user.LastName, isAdmin, isLockedOut);
    }

    private static IResult IdentityErrorResult(IEnumerable<IdentityError> errors)
    {
        var mapped = errors.Select(e => new UserValidationError(FieldFor(e.Code), e.Description)).ToList();
        return Results.BadRequest(new UserValidationProblem(mapped));
    }

    private static string FieldFor(string code) => code switch
    {
        "DuplicateEmail" or "InvalidEmail" or "DuplicateUserName" => "Email",
        _ when code.StartsWith("Password", StringComparison.Ordinal) => "Password",
        _ => string.Empty
    };
}
