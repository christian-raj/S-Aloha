using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAloha.Api.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class IncidentParent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "Incidents",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_ParentId",
                table: "Incidents",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_Incidents_ParentId",
                table: "Incidents",
                column: "ParentId",
                principalTable: "Incidents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_Incidents_ParentId",
                table: "Incidents");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_ParentId",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "Incidents");
        }
    }
}
