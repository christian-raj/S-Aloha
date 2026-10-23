using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SAloha.Api.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class KnowledgeLot3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HelpfulCount",
                table: "KnowledgeArticles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NotHelpfulCount",
                table: "KnowledgeArticles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "KnowledgeArticles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "KnowledgeArticleVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ArticleId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ArticleType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Authors = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PublishedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeArticleVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeArticleVersions_KnowledgeArticles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "KnowledgeArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeFeedbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ArticleId = table.Column<int>(type: "integer", nullable: false),
                    Username = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Helpful = table.Column<bool>(type: "boolean", nullable: false),
                    At = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeFeedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeFeedbacks_KnowledgeArticles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "KnowledgeArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticleVersions_ArticleId_Version",
                table: "KnowledgeArticleVersions",
                columns: new[] { "ArticleId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeFeedbacks_ArticleId_Username",
                table: "KnowledgeFeedbacks",
                columns: new[] { "ArticleId", "Username" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnowledgeArticleVersions");

            migrationBuilder.DropTable(
                name: "KnowledgeFeedbacks");

            migrationBuilder.DropColumn(
                name: "HelpfulCount",
                table: "KnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "NotHelpfulCount",
                table: "KnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "KnowledgeArticles");
        }
    }
}
