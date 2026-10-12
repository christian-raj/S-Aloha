using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SAloha.Api.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Perimeters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PER-10 : chaque enregistrement existant est rattaché à « Organisation » (Id 1).
            migrationBuilder.AddColumn<int>(
                name: "PerimeterId",
                table: "Services",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PerimeterId",
                table: "ServiceRequests",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PerimeterId",
                table: "Projects",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PerimeterId",
                table: "Problems",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PerimeterId",
                table: "KnowledgeArticles",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PerimeterId",
                table: "Incidents",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PerimeterId",
                table: "Improvements",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PerimeterId",
                table: "ConfigurationItems",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PerimeterId",
                table: "Changes",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PerimeterId",
                table: "Assessments",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PerimeterId",
                table: "Agreements",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "Perimeters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Perimeters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PerimeterAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PerimeterId = table.Column<int>(type: "integer", nullable: false),
                    PrincipalType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PrincipalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PrincipalDisplayName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Role = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerimeterAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerimeterAssignments_Perimeters_PerimeterId",
                        column: x => x.PerimeterId,
                        principalTable: "Perimeters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Perimeters",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "Name" },
                values: new object[] { 1, new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "système", null, null, "Périmètre par défaut : toute l'organisation.", "Organisation" });

            // L'identifiant 1 est imposé : la séquence doit repartir après lui.
            migrationBuilder.Sql("SELECT setval(pg_get_serial_sequence('\"Perimeters\"', 'Id'), (SELECT MAX(\"Id\") FROM \"Perimeters\"));");

            migrationBuilder.CreateIndex(
                name: "IX_Services_PerimeterId",
                table: "Services",
                column: "PerimeterId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_PerimeterId",
                table: "ServiceRequests",
                column: "PerimeterId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_PerimeterId",
                table: "Projects",
                column: "PerimeterId");

            migrationBuilder.CreateIndex(
                name: "IX_Problems_PerimeterId",
                table: "Problems",
                column: "PerimeterId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticles_PerimeterId",
                table: "KnowledgeArticles",
                column: "PerimeterId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_PerimeterId",
                table: "Incidents",
                column: "PerimeterId");

            migrationBuilder.CreateIndex(
                name: "IX_Improvements_PerimeterId",
                table: "Improvements",
                column: "PerimeterId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationItems_PerimeterId",
                table: "ConfigurationItems",
                column: "PerimeterId");

            migrationBuilder.CreateIndex(
                name: "IX_Changes_PerimeterId",
                table: "Changes",
                column: "PerimeterId");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_PerimeterId",
                table: "Assessments",
                column: "PerimeterId");

            migrationBuilder.CreateIndex(
                name: "IX_Agreements_PerimeterId",
                table: "Agreements",
                column: "PerimeterId");

            migrationBuilder.CreateIndex(
                name: "IX_PerimeterAssignments_PerimeterId_PrincipalType_PrincipalId_~",
                table: "PerimeterAssignments",
                columns: new[] { "PerimeterId", "PrincipalType", "PrincipalId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Perimeters_Name",
                table: "Perimeters",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Agreements_Perimeters_PerimeterId",
                table: "Agreements",
                column: "PerimeterId",
                principalTable: "Perimeters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Assessments_Perimeters_PerimeterId",
                table: "Assessments",
                column: "PerimeterId",
                principalTable: "Perimeters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Changes_Perimeters_PerimeterId",
                table: "Changes",
                column: "PerimeterId",
                principalTable: "Perimeters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ConfigurationItems_Perimeters_PerimeterId",
                table: "ConfigurationItems",
                column: "PerimeterId",
                principalTable: "Perimeters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Improvements_Perimeters_PerimeterId",
                table: "Improvements",
                column: "PerimeterId",
                principalTable: "Perimeters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_Perimeters_PerimeterId",
                table: "Incidents",
                column: "PerimeterId",
                principalTable: "Perimeters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeArticles_Perimeters_PerimeterId",
                table: "KnowledgeArticles",
                column: "PerimeterId",
                principalTable: "Perimeters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Problems_Perimeters_PerimeterId",
                table: "Problems",
                column: "PerimeterId",
                principalTable: "Perimeters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Perimeters_PerimeterId",
                table: "Projects",
                column: "PerimeterId",
                principalTable: "Perimeters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceRequests_Perimeters_PerimeterId",
                table: "ServiceRequests",
                column: "PerimeterId",
                principalTable: "Perimeters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Services_Perimeters_PerimeterId",
                table: "Services",
                column: "PerimeterId",
                principalTable: "Perimeters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Agreements_Perimeters_PerimeterId",
                table: "Agreements");

            migrationBuilder.DropForeignKey(
                name: "FK_Assessments_Perimeters_PerimeterId",
                table: "Assessments");

            migrationBuilder.DropForeignKey(
                name: "FK_Changes_Perimeters_PerimeterId",
                table: "Changes");

            migrationBuilder.DropForeignKey(
                name: "FK_ConfigurationItems_Perimeters_PerimeterId",
                table: "ConfigurationItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Improvements_Perimeters_PerimeterId",
                table: "Improvements");

            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_Perimeters_PerimeterId",
                table: "Incidents");

            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeArticles_Perimeters_PerimeterId",
                table: "KnowledgeArticles");

            migrationBuilder.DropForeignKey(
                name: "FK_Problems_Perimeters_PerimeterId",
                table: "Problems");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Perimeters_PerimeterId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceRequests_Perimeters_PerimeterId",
                table: "ServiceRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Services_Perimeters_PerimeterId",
                table: "Services");

            migrationBuilder.DropTable(
                name: "PerimeterAssignments");

            migrationBuilder.DropTable(
                name: "Perimeters");

            migrationBuilder.DropIndex(
                name: "IX_Services_PerimeterId",
                table: "Services");

            migrationBuilder.DropIndex(
                name: "IX_ServiceRequests_PerimeterId",
                table: "ServiceRequests");

            migrationBuilder.DropIndex(
                name: "IX_Projects_PerimeterId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Problems_PerimeterId",
                table: "Problems");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeArticles_PerimeterId",
                table: "KnowledgeArticles");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_PerimeterId",
                table: "Incidents");

            migrationBuilder.DropIndex(
                name: "IX_Improvements_PerimeterId",
                table: "Improvements");

            migrationBuilder.DropIndex(
                name: "IX_ConfigurationItems_PerimeterId",
                table: "ConfigurationItems");

            migrationBuilder.DropIndex(
                name: "IX_Changes_PerimeterId",
                table: "Changes");

            migrationBuilder.DropIndex(
                name: "IX_Assessments_PerimeterId",
                table: "Assessments");

            migrationBuilder.DropIndex(
                name: "IX_Agreements_PerimeterId",
                table: "Agreements");

            migrationBuilder.DropColumn(
                name: "PerimeterId",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "PerimeterId",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "PerimeterId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "PerimeterId",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "PerimeterId",
                table: "KnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "PerimeterId",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "PerimeterId",
                table: "Improvements");

            migrationBuilder.DropColumn(
                name: "PerimeterId",
                table: "ConfigurationItems");

            migrationBuilder.DropColumn(
                name: "PerimeterId",
                table: "Changes");

            migrationBuilder.DropColumn(
                name: "PerimeterId",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "PerimeterId",
                table: "Agreements");
        }
    }
}
