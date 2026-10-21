using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAloha.Api.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class IncidentProblemLot2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Problems",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                // PRB-20 : les problèmes existants ne sont pas bloqués après coup ; les nouveaux sont réactifs par défaut.
                defaultValue: "Proactif");

            migrationBuilder.AddColumn<string>(
                name: "CallerDisplayName",
                table: "Incidents",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CallerId",
                table: "Incidents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Channel",
                table: "Incidents",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EscalationReason",
                table: "Incidents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidatedAt",
                table: "Analyses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidatedBy",
                table: "Analyses",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Source",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "CallerDisplayName",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "CallerId",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "EscalationReason",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "ValidatedAt",
                table: "Analyses");

            migrationBuilder.DropColumn(
                name: "ValidatedBy",
                table: "Analyses");
        }
    }
}
