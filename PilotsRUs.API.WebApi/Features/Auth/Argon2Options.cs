using System.ComponentModel.DataAnnotations;

namespace PilotsRUs.API.WebApi.Features.Auth;

public sealed class Argon2Options
{
    public const string SectionName = "Argon2";

    [Range(1, int.MaxValue)]
    public int MemoryKib { get; init; } = 19456;

    [Range(1, int.MaxValue)]
    public int Iterations { get; init; } = 2;

    [Range(1, int.MaxValue)]
    public int Parallelism { get; init; } = 1;

    [Range(8, int.MaxValue)]
    public int SaltSize { get; init; } = 16;

    [Range(16, int.MaxValue)]
    public int HashSize { get; init; } = 32;
}
