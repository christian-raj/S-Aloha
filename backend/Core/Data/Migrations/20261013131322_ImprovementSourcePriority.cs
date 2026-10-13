using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAloha.Api.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class ImprovementSourcePriority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Effort",
                table: "Improvements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Improvements",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Suggestion");   // CSI-10 : l'existant vient de suggestions

            migrationBuilder.AddColumn<int>(
                name: "Value",
                table: "Improvements",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Effort",
                table: "Improvements");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Improvements");

            migrationBuilder.DropColumn(
                name: "Value",
                table: "Improvements");
        }
    }
}
