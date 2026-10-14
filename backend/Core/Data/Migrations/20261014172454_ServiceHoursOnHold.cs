using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAloha.Api.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class ServiceHoursOnHold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClosingTime",
                table: "Services",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Holidays",
                table: "Services",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OpeningTime",
                table: "Services",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                table: "Services",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkingDays",
                table: "Services",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "HeldMinutes",
                table: "Incidents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OnHoldReason",
                table: "Incidents",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OnHoldSince",
                table: "Incidents",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClosingTime",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "Holidays",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "OpeningTime",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "WorkingDays",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "HeldMinutes",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "OnHoldReason",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "OnHoldSince",
                table: "Incidents");
        }
    }
}
