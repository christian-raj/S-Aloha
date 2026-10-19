using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SAloha.Api.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Categories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Practice = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Practice_Name",
                table: "Categories",
                columns: new[] { "Practice", "Name" },
                unique: true);

            // ADM-52 : les catégories déjà saisies deviennent la liste initiale
            // de chaque pratique (une par libellé, sans tenir compte de la casse).
            foreach (var (practice, table) in new[] { ("incident", "Incidents"), ("problem", "Problems") })
                migrationBuilder.Sql($$"""
                    INSERT INTO "Categories" ("Practice", "Name", "Active", "CreatedAt", "CreatedBy")
                    SELECT DISTINCT ON (lower(btrim("Category"))) '{{practice}}', btrim("Category"), true, now(), 'migration'
                    FROM "{{table}}" WHERE btrim("Category") <> ''
                    ORDER BY lower(btrim("Category")), btrim("Category");
                    """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
