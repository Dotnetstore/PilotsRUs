namespace PilotsRUs.Shared.SDK.Users;

public sealed record UserValidationProblem(IReadOnlyList<UserValidationError> Errors);
