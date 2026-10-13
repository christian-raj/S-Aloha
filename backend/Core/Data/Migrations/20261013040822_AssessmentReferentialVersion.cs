using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAloha.Api.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AssessmentReferentialVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FrozenRequirementsJson",
                table: "Assessments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferentialVersion",
                table: "Assessments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "ReCyF v2.5 du 17/03/2026 (version de travail)");   // NIS-18 : version évaluée jusqu'ici
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FrozenRequirementsJson",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "ReferentialVersion",
                table: "Assessments");
        }
    }
}
