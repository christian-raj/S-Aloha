using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SAloha.Api.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class DuplicateDismissals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DuplicateDismissals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FirstId = table.Column<int>(type: "integer", nullable: false),
                    SecondId = table.Column<int>(type: "integer", nullable: false),
                    DismissedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DismissedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DuplicateDismissals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DuplicateDismissals_Type_FirstId_SecondId",
                table: "DuplicateDismissals",
                columns: new[] { "Type", "FirstId", "SecondId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DuplicateDismissals");
        }
    }
}
