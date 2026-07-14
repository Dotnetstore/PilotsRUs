namespace PilotsRUs.Shared.SDK.Users;

public sealed record UserResponse(Guid Id, string Email, string FirstName, string LastName, bool IsAdmin, bool IsLockedOut);
