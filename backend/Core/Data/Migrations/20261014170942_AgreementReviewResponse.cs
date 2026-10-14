using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SAloha.Api.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgreementReviewResponse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ResponseDueAt",
                table: "Incidents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResponseHoursP1",
                table: "Agreements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResponseHoursP2",
                table: "Agreements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResponseHoursP3",
                table: "Agreements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResponseHoursP4",
                table: "Agreements",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgreementReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AgreementId = table.Column<int>(type: "integer", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Findings = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Decisions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    NextReviewDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgreementReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgreementReviews_Agreements_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "Agreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgreementReviews_AgreementId",
                table: "AgreementReviews",
                column: "AgreementId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgreementReviews");

            migrationBuilder.DropColumn(
                name: "ResponseDueAt",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "ResponseHoursP1",
                table: "Agreements");

            migrationBuilder.DropColumn(
                name: "ResponseHoursP2",
                table: "Agreements");

            migrationBuilder.DropColumn(
                name: "ResponseHoursP3",
                table: "Agreements");

            migrationBuilder.DropColumn(
                name: "ResponseHoursP4",
                table: "Agreements");
        }
    }
}
