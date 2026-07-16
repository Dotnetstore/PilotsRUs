namespace PilotsRUs.API.WebApi.Data;

public sealed class Account
{
    public Guid Id { get; init; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string DisplayName { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; }
}
