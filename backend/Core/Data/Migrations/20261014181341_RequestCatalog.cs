using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SAloha.Api.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class RequestCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CatalogItemId",
                table: "ServiceRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RequestCatalogItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ServiceId = table.Column<int>(type: "integer", nullable: true),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    TargetHours = table.Column<int>(type: "integer", nullable: true),
                    FulfillmentGroup = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestCatalogItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestCatalogItems_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_CatalogItemId",
                table: "ServiceRequests",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestCatalogItems_ServiceId",
                table: "RequestCatalogItems",
                column: "ServiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceRequests_RequestCatalogItems_CatalogItemId",
                table: "ServiceRequests",
                column: "CatalogItemId",
                principalTable: "RequestCatalogItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceRequests_RequestCatalogItems_CatalogItemId",
                table: "ServiceRequests");

            migrationBuilder.DropTable(
                name: "RequestCatalogItems");

            migrationBuilder.DropIndex(
                name: "IX_ServiceRequests_CatalogItemId",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "CatalogItemId",
                table: "ServiceRequests");
        }
    }
}
