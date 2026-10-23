using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAloha.Api.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProblemChangeLot3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "StandardChangeModels",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "StandardChangeModels",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedBy",
                table: "StandardChangeModels",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedAt",
                table: "StandardChangeModels",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuspendedByChange",
                table: "StandardChangeModels",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SuspendedByChangeId",
                table: "StandardChangeModels",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "StandardChangeModels");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "StandardChangeModels");

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                table: "StandardChangeModels");

            migrationBuilder.DropColumn(
                name: "SuspendedAt",
                table: "StandardChangeModels");

            migrationBuilder.DropColumn(
                name: "SuspendedByChange",
                table: "StandardChangeModels");

            migrationBuilder.DropColumn(
                name: "SuspendedByChangeId",
                table: "StandardChangeModels");
        }
    }
}
