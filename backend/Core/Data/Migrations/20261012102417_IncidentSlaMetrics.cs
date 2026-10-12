using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAloha.Api.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class IncidentSlaMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FirstContactResolution",
                table: "Incidents",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OccurredAt",
                table: "Incidents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OwnerChanged",
                table: "Incidents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolutionDueAt",
                table: "Incidents",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstContactResolution",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "OccurredAt",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "OwnerChanged",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "ResolutionDueAt",
                table: "Incidents");
        }
    }
}
