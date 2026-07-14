namespace PilotsRUs.Shared.SDK.Users;

public sealed record CreateUserRequest(string Email, string FirstName, string LastName, string Password, bool IsAdmin);
