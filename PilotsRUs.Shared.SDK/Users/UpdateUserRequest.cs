namespace PilotsRUs.Shared.SDK.Users;

public sealed record UpdateUserRequest(string Email, string FirstName, string LastName, bool IsAdmin);
