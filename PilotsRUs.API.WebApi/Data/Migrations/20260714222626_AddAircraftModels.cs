using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PilotsRUs.API.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAircraftModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AircraftModels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ManufacturerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IcaoTypeDesignator = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AircraftModels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AircraftModels_Manufacturers_ManufacturerId",
                        column: x => x.ManufacturerId,
                        principalTable: "Manufacturers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AircraftModels_IcaoTypeDesignator",
                table: "AircraftModels",
                column: "IcaoTypeDesignator",
                unique: true,
                filter: "\"IcaoTypeDesignator\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AircraftModels_ManufacturerId_Name",
                table: "AircraftModels",
                columns: new[] { "ManufacturerId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AircraftModels");
        }
    }
}
