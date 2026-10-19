using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAloha.Api.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class SnmpV3Interfaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthProtocol",
                table: "DiscoveryCredentials",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivProtocol",
                table: "DiscoveryCredentials",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Interfaces",
                table: "DiscoveredAssets",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthProtocol",
                table: "DiscoveryCredentials");

            migrationBuilder.DropColumn(
                name: "PrivProtocol",
                table: "DiscoveryCredentials");

            migrationBuilder.DropColumn(
                name: "Interfaces",
                table: "DiscoveredAssets");
        }
    }
}
