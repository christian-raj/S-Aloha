using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SAloha.Api.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class RequestTasksSatisfaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoClosed",
                table: "ServiceRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SatisfactionAt",
                table: "ServiceRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SatisfactionComment",
                table: "ServiceRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SatisfactionScore",
                table: "ServiceRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RequestCatalogTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CatalogItemId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OwnerType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    OwnerId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OwnerDisplayName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestCatalogTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestCatalogTasks_RequestCatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "RequestCatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequestTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OwnerType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    OwnerId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OwnerDisplayName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    FromCatalog = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestTasks_ServiceRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "ServiceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RequestCatalogTasks_CatalogItemId",
                table: "RequestCatalogTasks",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestTasks_RequestId",
                table: "RequestTasks",
                column: "RequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequestCatalogTasks");

            migrationBuilder.DropTable(
                name: "RequestTasks");

            migrationBuilder.DropColumn(
                name: "AutoClosed",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "SatisfactionAt",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "SatisfactionComment",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "SatisfactionScore",
                table: "ServiceRequests");
        }
    }
}
