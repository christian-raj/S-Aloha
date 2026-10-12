using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAloha.Api.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class KnowledgeLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArchiveRequestReason",
                table: "KnowledgeArticles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchiveRequestedAt",
                table: "KnowledgeArticles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveRequestedBy",
                table: "KnowledgeArticles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReviewedAt",
                table: "KnowledgeArticles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastReviewedBy",
                table: "KnowledgeArticles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "KnowledgeArticles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmittedBy",
                table: "KnowledgeArticles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchiveRequestReason",
                table: "KnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "ArchiveRequestedAt",
                table: "KnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "ArchiveRequestedBy",
                table: "KnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "LastReviewedAt",
                table: "KnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "LastReviewedBy",
                table: "KnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "KnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "SubmittedBy",
                table: "KnowledgeArticles");
        }
    }
}
