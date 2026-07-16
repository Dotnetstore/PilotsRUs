using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PilotsRUs.User.App.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFlightAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FlightAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FlightNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DepartureAirportIcaoCode = table.Column<string>(type: "TEXT", maxLength: 4, nullable: false),
                    ArrivalAirportIcaoCode = table.Column<string>(type: "TEXT", maxLength: 4, nullable: false),
                    FlightDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    AircraftRegistrationNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AssignedPassengersEconomy = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedPassengersBusiness = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedPassengersFirst = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedCargoKg = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlightAssignments", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlightAssignments");
        }
    }
}
