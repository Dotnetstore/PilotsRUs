namespace PilotsRUs.User.App.Services;

public sealed class ApiOptions
{
    public const string SectionName = "Api";

    public required string BaseAddress { get; init; }
}
