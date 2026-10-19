using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SAloha.Api.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class SshInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ListeningPorts",
                table: "DiscoveredAssets",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Services",
                table: "DiscoveredAssets",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DiscoveredSoftware",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssetId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscoveredSoftware", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscoveredSoftware_DiscoveredAssets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "DiscoveredAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SshHostKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PerimeterId = table.Column<int>(type: "integer", nullable: false),
                    Host = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    Port = table.Column<int>(type: "integer", nullable: false),
                    Algorithm = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FirstSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PendingAlgorithm = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PendingFingerprint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PendingSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcceptedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SshHostKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SshHostKeys_Perimeters_PerimeterId",
                        column: x => x.PerimeterId,
                        principalTable: "Perimeters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveredSoftware_AssetId",
                table: "DiscoveredSoftware",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_SshHostKeys_PerimeterId",
                table: "SshHostKeys",
                column: "PerimeterId");

            migrationBuilder.CreateIndex(
                name: "IX_SshHostKeys_PerimeterId_Host_Port",
                table: "SshHostKeys",
                columns: new[] { "PerimeterId", "Host", "Port" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscoveredSoftware");

            migrationBuilder.DropTable(
                name: "SshHostKeys");

            migrationBuilder.DropColumn(
                name: "ListeningPorts",
                table: "DiscoveredAssets");

            migrationBuilder.DropColumn(
                name: "Services",
                table: "DiscoveredAssets");
        }
    }
}
