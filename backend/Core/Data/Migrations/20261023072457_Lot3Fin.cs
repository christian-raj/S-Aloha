using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAloha.Api.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Lot3Fin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BreachNature",
                table: "RegulatoryNotifications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompromiseIndicators",
                table: "RegulatoryNotifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LikelyConsequences",
                table: "RegulatoryNotifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Severity",
                table: "RegulatoryNotifications",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FollowUpDate",
                table: "Improvements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FollowUpDoneAt",
                table: "Improvements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FollowUpDoneBy",
                table: "Improvements",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FollowUpHolds",
                table: "Improvements",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FollowUpNote",
                table: "Improvements",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BreachNature",
                table: "RegulatoryNotifications");

            migrationBuilder.DropColumn(
                name: "CompromiseIndicators",
                table: "RegulatoryNotifications");

            migrationBuilder.DropColumn(
                name: "LikelyConsequences",
                table: "RegulatoryNotifications");

            migrationBuilder.DropColumn(
                name: "Severity",
                table: "RegulatoryNotifications");

            migrationBuilder.DropColumn(
                name: "FollowUpDate",
                table: "Improvements");

            migrationBuilder.DropColumn(
                name: "FollowUpDoneAt",
                table: "Improvements");

            migrationBuilder.DropColumn(
                name: "FollowUpDoneBy",
                table: "Improvements");

            migrationBuilder.DropColumn(
                name: "FollowUpHolds",
                table: "Improvements");

            migrationBuilder.DropColumn(
                name: "FollowUpNote",
                table: "Improvements");
        }
    }
}
