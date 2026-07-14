namespace PilotsRUs.Shared.SDK.Auth;

public sealed record CurrentUserResponse(string Email, IReadOnlyList<string> Roles);
