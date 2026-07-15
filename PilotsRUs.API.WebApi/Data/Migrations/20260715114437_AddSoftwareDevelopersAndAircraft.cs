using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PilotsRUs.API.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftwareDevelopersAndAircraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SoftwareDevelopers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftwareDevelopers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Aircraft",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PassengerCapacityEconomy = table.Column<int>(type: "integer", nullable: false),
                    PassengerCapacityBusiness = table.Column<int>(type: "integer", nullable: false),
                    PassengerCapacityFirst = table.Column<int>(type: "integer", nullable: false),
                    CargoCapacityKg = table.Column<int>(type: "integer", nullable: false),
                    AircraftModelId = table.Column<Guid>(type: "uuid", nullable: false),
                    SoftwareDeveloperId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aircraft", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Aircraft_AircraftModels_AircraftModelId",
                        column: x => x.AircraftModelId,
                        principalTable: "AircraftModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Aircraft_SoftwareDevelopers_SoftwareDeveloperId",
                        column: x => x.SoftwareDeveloperId,
                        principalTable: "SoftwareDevelopers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Aircraft_AircraftModelId",
                table: "Aircraft",
                column: "AircraftModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Aircraft_RegistrationNumber",
                table: "Aircraft",
                column: "RegistrationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Aircraft_SoftwareDeveloperId",
                table: "Aircraft",
                column: "SoftwareDeveloperId");

            migrationBuilder.CreateIndex(
                name: "IX_SoftwareDevelopers_Name",
                table: "SoftwareDevelopers",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Aircraft");

            migrationBuilder.DropTable(
                name: "SoftwareDevelopers");
        }
    }
}
