using Microsoft.AspNetCore.Identity;

namespace PilotsRUs.API.WebApi.Data;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
}
