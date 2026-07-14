using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PilotsRUs.API.WebApi.Data;
using PilotsRUs.API.WebApi.Extensions;
using PilotsRUs.API.WebApi.Features.AircraftModels;
using PilotsRUs.API.WebApi.Features.Auth;
using PilotsRUs.API.WebApi.Features.Manufacturers;
using PilotsRUs.API.WebApi.Features.Users;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddApplicationIdentity();
builder.AddApplicationJwtAuth();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var migrationScope = app.Services.CreateScope();
    var dbContext = migrationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Must run after the migration block above - RoleSeeder queries AspNetRoles, which doesn't exist until
// migrations have applied. Runs unconditionally (every environment, not just Development) since
// role-gated endpoints/pages must be reachable everywhere.
using (var roleSeedingScope = app.Services.CreateScope())
{
    var roleManager = roleSeedingScope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    await RoleSeeder.SeedRolesAsync(roleManager);
}

// Must also run after the migration block above - Manufacturers doesn't exist until migrations have
// applied. Runs unconditionally (every environment), same reasoning as RoleSeeder - reference data needed
// everywhere, not dev-only. Uses IDbContextFactory directly (already a singleton registration) rather than
// CreateScope(), since this isn't an Identity concern.
await ManufacturerSeeder.SeedAsync(app.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());

// Must run after ManufacturerSeeder - resolves ManufacturerIds by name, which requires manufacturer rows
// to already exist. Same unconditional/every-environment reasoning as ManufacturerSeeder.
await AircraftModelSeeder.SeedAsync(app.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());

if (app.Environment.IsDevelopment())
{
    using var seederScope = app.Services.CreateScope();
    var userManager = seederScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    await DevelopmentDataSeeder.SeedDevelopmentAdminAsync(userManager);
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapManufacturerEndpoints();
app.MapAircraftModelEndpoints();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
