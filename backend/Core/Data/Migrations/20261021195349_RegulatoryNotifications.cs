using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SAloha.Api.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class RegulatoryNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSecurity",
                table: "Incidents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Nis2Important",
                table: "Incidents",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nis2Reason",
                table: "Incidents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PersonalDataBreach",
                table: "Incidents",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonalDataBreachReason",
                table: "Incidents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QualifiedAt",
                table: "Incidents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QualifiedBy",
                table: "Incidents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RegulatoryNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IncidentId = table.Column<int>(type: "integer", nullable: false),
                    Regime = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Authority = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AwareAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AwareAtReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    EarlyWarningDueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotificationDueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinalReportDueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EarlyWarningSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotificationSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinalReportSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuthorityCaseRef = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    Impact = table.Column<string>(type: "text", nullable: true),
                    MeasuresTaken = table.Column<string>(type: "text", nullable: true),
                    SuspectedMalicious = table.Column<bool>(type: "boolean", nullable: true),
                    CrossBorderImpact = table.Column<bool>(type: "boolean", nullable: true),
                    PersonalDataCategories = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DataSubjectsCount = table.Column<int>(type: "integer", nullable: true),
                    NotRequiredReason = table.Column<string>(type: "text", nullable: true),
                    LateReason = table.Column<string>(type: "text", nullable: true),
                    SentVersions = table.Column<string>(type: "text", nullable: false),
                    PerimeterId = table.Column<int>(type: "integer", nullable: false),
                    Reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OwnerType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    OwnerId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OwnerDisplayName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedByDisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegulatoryNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegulatoryNotifications_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RegulatoryNotifications_Perimeters_PerimeterId",
                        column: x => x.PerimeterId,
                        principalTable: "Perimeters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryNotifications_IncidentId_Regime",
                table: "RegulatoryNotifications",
                columns: new[] { "IncidentId", "Regime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryNotifications_PerimeterId",
                table: "RegulatoryNotifications",
                column: "PerimeterId");

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryNotifications_Reference",
                table: "RegulatoryNotifications",
                column: "Reference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegulatoryNotifications");

            migrationBuilder.DropColumn(
                name: "IsSecurity",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "Nis2Important",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "Nis2Reason",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "PersonalDataBreach",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "PersonalDataBreachReason",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "QualifiedAt",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "QualifiedBy",
                table: "Incidents");
        }
    }
}
