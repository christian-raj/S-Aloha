using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAloha.Api.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProblemReopenActionApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReopenReason",
                table: "Problems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovalRequestedAt",
                table: "Actions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalRequestedBy",
                table: "Actions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChangeId",
                table: "Actions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangeReference",
                table: "Actions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReopenReason",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "ApprovalRequestedAt",
                table: "Actions");

            migrationBuilder.DropColumn(
                name: "ApprovalRequestedBy",
                table: "Actions");

            migrationBuilder.DropColumn(
                name: "ChangeId",
                table: "Actions");

            migrationBuilder.DropColumn(
                name: "ChangeReference",
                table: "Actions");
        }
    }
}
