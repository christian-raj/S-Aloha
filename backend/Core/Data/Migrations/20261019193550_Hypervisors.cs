using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SAloha.Api.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Hypervisors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SshHostKeys_PerimeterId_Host_Port",
                table: "SshHostKeys");

            migrationBuilder.AddColumn<string>(
                name: "Protocol",
                table: "SshHostKeys",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                // Les clés déjà retenues sont toutes des clés d'hôte SSH (CFG-53).
                defaultValue: "ssh");

            migrationBuilder.CreateTable(
                name: "RelationProposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PerimeterId = table.Column<int>(type: "integer", nullable: false),
                    SourceAssetId = table.Column<int>(type: "integer", nullable: false),
                    TargetAssetId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RelationId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RelationProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RelationProposals_DiscoveredAssets_SourceAssetId",
                        column: x => x.SourceAssetId,
                        principalTable: "DiscoveredAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RelationProposals_DiscoveredAssets_TargetAssetId",
                        column: x => x.TargetAssetId,
                        principalTable: "DiscoveredAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RelationProposals_Perimeters_PerimeterId",
                        column: x => x.PerimeterId,
                        principalTable: "Perimeters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SshHostKeys_PerimeterId_Protocol_Host_Port",
                table: "SshHostKeys",
                columns: new[] { "PerimeterId", "Protocol", "Host", "Port" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RelationProposals_PerimeterId",
                table: "RelationProposals",
                column: "PerimeterId");

            migrationBuilder.CreateIndex(
                name: "IX_RelationProposals_SourceAssetId_TargetAssetId_Type",
                table: "RelationProposals",
                columns: new[] { "SourceAssetId", "TargetAssetId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RelationProposals_TargetAssetId",
                table: "RelationProposals",
                column: "TargetAssetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RelationProposals");

            migrationBuilder.DropIndex(
                name: "IX_SshHostKeys_PerimeterId_Protocol_Host_Port",
                table: "SshHostKeys");

            migrationBuilder.DropColumn(
                name: "Protocol",
                table: "SshHostKeys");

            migrationBuilder.CreateIndex(
                name: "IX_SshHostKeys_PerimeterId_Host_Port",
                table: "SshHostKeys",
                columns: new[] { "PerimeterId", "Host", "Port" },
                unique: true);
        }
    }
}
