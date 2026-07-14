namespace PilotsRUs.Shared.SDK.Auth;

public sealed record CurrentUserResponse(string Email, string FirstName, string LastName, IReadOnlyList<string> Roles);
