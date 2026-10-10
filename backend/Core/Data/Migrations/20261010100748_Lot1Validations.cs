using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAloha.Api.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Lot1Validations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "ServiceRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosureCode",
                table: "Problems",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "Problems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstResponseAt",
                table: "Incidents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionCode",
                table: "Incidents",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AbandonReason",
                table: "Improvements",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Changes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "ClosureCode",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "FirstResponseAt",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "ResolutionCode",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "AbandonReason",
                table: "Improvements");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Changes");
        }
    }
}
