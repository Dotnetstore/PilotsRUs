using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PilotsRUs.API.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleTemplateStartDateAndSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                table: "ScheduleTemplates",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.CreateTable(
                name: "Schedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduleTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    FlightDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Schedules_ScheduleTemplates_ScheduleTemplateId",
                        column: x => x.ScheduleTemplateId,
                        principalTable: "ScheduleTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_ScheduleTemplateId_FlightDate",
                table: "Schedules",
                columns: new[] { "ScheduleTemplateId", "FlightDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Schedules");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "ScheduleTemplates");
        }
    }
}
