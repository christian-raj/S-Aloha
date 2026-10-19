using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAloha.Api.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class PassiveFingerprint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Banners",
                table: "DiscoveredAssets",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MacVendors",
                columns: table => new
                {
                    Prefix = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    Organization = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MacVendors", x => x.Prefix);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MacVendors");

            migrationBuilder.DropColumn(
                name: "Banners",
                table: "DiscoveredAssets");
        }
    }
}
