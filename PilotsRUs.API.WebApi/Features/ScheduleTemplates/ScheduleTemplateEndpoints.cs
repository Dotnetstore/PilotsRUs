using Microsoft.EntityFrameworkCore;
using PilotsRUs.API.WebApi.Data;
using PilotsRUs.Shared.SDK.ScheduleTemplates;

namespace PilotsRUs.API.WebApi.Features.ScheduleTemplates;

public static class ScheduleTemplateEndpoints
{
    public static IEndpointRouteBuilder MapScheduleTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        // No policy name - same as Manufacturers/AircraftModels/Aircraft, any authenticated user can manage
        // schedule templates.
        var group = app.MapGroup("/schedule-templates").RequireAuthorization();

        group.MapGet("/", async (IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var scheduleTemplates = await dbContext.ScheduleTemplates
                .Include(s => s.DepartureAirport)
                .Include(s => s.ArrivalAirport)
                .Include(s => s.Aircraft)
                .OrderBy(s => s.FlightNumber)
                .ToListAsync();
            return Results.Ok(scheduleTemplates.Select(ToResponse));
        }).WithName("GetScheduleTemplates");

        group.MapGet("/{id:guid}", async (Guid id, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var scheduleTemplate = await dbContext.ScheduleTemplates
                .Include(s => s.DepartureAirport)
                .Include(s => s.ArrivalAirport)
                .Include(s => s.Aircraft)
                .FirstOrDefaultAsync(s => s.Id == id);
            return scheduleTemplate is null ? Results.NotFound() : Results.Ok(ToResponse(scheduleTemplate));
        }).WithName("GetScheduleTemplateById");

        group.MapPost("/", async (CreateScheduleTemplateRequest request, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var departureAirport = await dbContext.Airports.FindAsync(request.DepartureAirportId);
            if (departureAirport is null)
            {
                return Results.BadRequest("The selected departure airport does not exist.");
            }

            var arrivalAirport = await dbContext.Airports.FindAsync(request.ArrivalAirportId);
            if (arrivalAirport is null)
            {
                return Results.BadRequest("The selected arrival airport does not exist.");
            }

            if (request.DepartureAirportId == request.ArrivalAirportId)
            {
                return Results.BadRequest("Departure and arrival airport cannot be the same.");
            }

            var aircraft = await dbContext.Aircraft.FindAsync(request.AircraftId);
            if (aircraft is null)
            {
                return Results.BadRequest("The selected aircraft does not exist.");
            }

            var scheduleTemplate = new ScheduleTemplate
            {
                Id = Guid.NewGuid(),
                FlightNumber = request.FlightNumber,
                DepartureAirportId = request.DepartureAirportId,
                ArrivalAirportId = request.ArrivalAirportId,
                AircraftId = request.AircraftId,
                DistanceNauticalMiles = request.DistanceNauticalMiles,
                FlightTime = request.FlightTime,
                Frequency = request.Frequency
            };
            dbContext.ScheduleTemplates.Add(scheduleTemplate);
            await dbContext.SaveChangesAsync();

            var response = new ScheduleTemplateResponse(
                scheduleTemplate.Id, scheduleTemplate.FlightNumber,
                scheduleTemplate.DepartureAirportId, departureAirport.IcaoCode, departureAirport.Name,
                scheduleTemplate.ArrivalAirportId, arrivalAirport.IcaoCode, arrivalAirport.Name,
                scheduleTemplate.AircraftId, aircraft.RegistrationNumber,
                scheduleTemplate.DistanceNauticalMiles, scheduleTemplate.FlightTime, scheduleTemplate.Frequency);
            return Results.Created($"/schedule-templates/{scheduleTemplate.Id}", response);
        }).WithName("CreateScheduleTemplate");

        group.MapPut("/{id:guid}", async (Guid id, UpdateScheduleTemplateRequest request, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var scheduleTemplate = await dbContext.ScheduleTemplates.FindAsync(id);
            if (scheduleTemplate is null)
            {
                return Results.NotFound();
            }

            var departureAirport = await dbContext.Airports.FindAsync(request.DepartureAirportId);
            if (departureAirport is null)
            {
                return Results.BadRequest("The selected departure airport does not exist.");
            }

            var arrivalAirport = await dbContext.Airports.FindAsync(request.ArrivalAirportId);
            if (arrivalAirport is null)
            {
                return Results.BadRequest("The selected arrival airport does not exist.");
            }

            if (request.DepartureAirportId == request.ArrivalAirportId)
            {
                return Results.BadRequest("Departure and arrival airport cannot be the same.");
            }

            var aircraft = await dbContext.Aircraft.FindAsync(request.AircraftId);
            if (aircraft is null)
            {
                return Results.BadRequest("The selected aircraft does not exist.");
            }

            scheduleTemplate.FlightNumber = request.FlightNumber;
            scheduleTemplate.DepartureAirportId = request.DepartureAirportId;
            scheduleTemplate.ArrivalAirportId = request.ArrivalAirportId;
            scheduleTemplate.AircraftId = request.AircraftId;
            scheduleTemplate.DistanceNauticalMiles = request.DistanceNauticalMiles;
            scheduleTemplate.FlightTime = request.FlightTime;
            scheduleTemplate.Frequency = request.Frequency;

            await dbContext.SaveChangesAsync();

            var response = new ScheduleTemplateResponse(
                scheduleTemplate.Id, scheduleTemplate.FlightNumber,
                scheduleTemplate.DepartureAirportId, departureAirport.IcaoCode, departureAirport.Name,
                scheduleTemplate.ArrivalAirportId, arrivalAirport.IcaoCode, arrivalAirport.Name,
                scheduleTemplate.AircraftId, aircraft.RegistrationNumber,
                scheduleTemplate.DistanceNauticalMiles, scheduleTemplate.FlightTime, scheduleTemplate.Frequency);
            return Results.Ok(response);
        }).WithName("UpdateScheduleTemplate");

        group.MapDelete("/{id:guid}", async (Guid id, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var scheduleTemplate = await dbContext.ScheduleTemplates.FindAsync(id);
            if (scheduleTemplate is null)
            {
                return Results.NotFound();
            }

            // Hard delete - nothing references ScheduleTemplate yet. Once the future Schedule entity does,
            // this needs the same Restrict-guard treatment DeleteManufacturer/DeleteAircraftModel/
            // DeleteSoftwareDeveloper have.
            dbContext.ScheduleTemplates.Remove(scheduleTemplate);
            await dbContext.SaveChangesAsync();

            return Results.NoContent();
        }).WithName("DeleteScheduleTemplate");

        return app;
    }

    private static ScheduleTemplateResponse ToResponse(ScheduleTemplate scheduleTemplate) => new(
        scheduleTemplate.Id, scheduleTemplate.FlightNumber,
        scheduleTemplate.DepartureAirportId, scheduleTemplate.DepartureAirport.IcaoCode, scheduleTemplate.DepartureAirport.Name,
        scheduleTemplate.ArrivalAirportId, scheduleTemplate.ArrivalAirport.IcaoCode, scheduleTemplate.ArrivalAirport.Name,
        scheduleTemplate.AircraftId, scheduleTemplate.Aircraft.RegistrationNumber,
        scheduleTemplate.DistanceNauticalMiles, scheduleTemplate.FlightTime, scheduleTemplate.Frequency);
}
