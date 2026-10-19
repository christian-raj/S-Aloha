using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SAloha.Api.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class CmdbDiscovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppCode",
                table: "ConfigurationItems",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppLifecycle",
                table: "ConfigurationItems",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessOwnerDisplayName",
                table: "ConfigurationItems",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessOwnerId",
                table: "ConfigurationItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessOwnerType",
                table: "ConfigurationItems",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessUnit",
                table: "ConfigurationItems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Criticality",
                table: "ConfigurationItems",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndOfSupport",
                table: "ConfigurationItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GdprSensitive",
                table: "ConfigurationItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "ConfigurationItems",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MacAddress",
                table: "ConfigurationItems",
                type: "character varying(17)",
                maxLength: 17,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "ConfigurationItems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Nis2Scope",
                table: "ConfigurationItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OperatingSystem",
                table: "ConfigurationItems",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PurchaseDate",
                table: "ConfigurationItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RpoHours",
                table: "ConfigurationItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RtoHours",
                table: "ConfigurationItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SerialNumber",
                table: "ConfigurationItems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalOwnerDisplayName",
                table: "ConfigurationItems",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalOwnerId",
                table: "ConfigurationItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalOwnerType",
                table: "ConfigurationItems",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Vendor",
                table: "ConfigurationItems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WarrantyExpiry",
                table: "ConfigurationItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DiscoveryCredentials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PerimeterId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CredentialType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Username = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Domain = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    EncryptedSecret = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SecretChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscoveryCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscoveryCredentials_Perimeters_PerimeterId",
                        column: x => x.PerimeterId,
                        principalTable: "Perimeters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EndOfLifeRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Product = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    EndOfSupport = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EndOfLifeRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiscoveryJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PerimeterId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    TargetRange = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    ScanType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PortList = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    FrequencyHours = table.Column<int>(type: "integer", nullable: false),
                    WindowStart = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    WindowEnd = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    CredentialId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LastFoundCount = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscoveryJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscoveryJobs_DiscoveryCredentials_CredentialId",
                        column: x => x.CredentialId,
                        principalTable: "DiscoveryCredentials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiscoveryJobs_Perimeters_PerimeterId",
                        column: x => x.PerimeterId,
                        principalTable: "Perimeters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiscoveredAssets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PerimeterId = table.Column<int>(type: "integer", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    MacAddress = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: true),
                    Hostname = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Vendor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DetectedOs = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    DetectedType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Location = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    OpenPorts = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Sources = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InDirectory = table.Column<bool>(type: "boolean", nullable: false),
                    RawData = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ConfidenceScore = table.Column<int>(type: "integer", nullable: false),
                    MatchRule = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SuggestedAction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ConflictReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    MatchedCiId = table.Column<int>(type: "integer", nullable: true),
                    JobId = table.Column<int>(type: "integer", nullable: true),
                    FirstSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReconciledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReconciledBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscoveredAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscoveredAssets_ConfigurationItems_MatchedCiId",
                        column: x => x.MatchedCiId,
                        principalTable: "ConfigurationItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DiscoveredAssets_DiscoveryJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "DiscoveryJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DiscoveredAssets_Perimeters_PerimeterId",
                        column: x => x.PerimeterId,
                        principalTable: "Perimeters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationItems_AppCode",
                table: "ConfigurationItems",
                column: "AppCode");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationItems_MacAddress",
                table: "ConfigurationItems",
                column: "MacAddress");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationItems_SerialNumber",
                table: "ConfigurationItems",
                column: "SerialNumber");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveredAssets_JobId",
                table: "DiscoveredAssets",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveredAssets_MatchedCiId",
                table: "DiscoveredAssets",
                column: "MatchedCiId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveredAssets_PerimeterId",
                table: "DiscoveredAssets",
                column: "PerimeterId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveredAssets_PerimeterId_Status",
                table: "DiscoveredAssets",
                columns: new[] { "PerimeterId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveryCredentials_PerimeterId",
                table: "DiscoveryCredentials",
                column: "PerimeterId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveryJobs_CredentialId",
                table: "DiscoveryJobs",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveryJobs_PerimeterId",
                table: "DiscoveryJobs",
                column: "PerimeterId");

            migrationBuilder.CreateIndex(
                name: "IX_EndOfLifeRules_Product",
                table: "EndOfLifeRules",
                column: "Product",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscoveredAssets");

            migrationBuilder.DropTable(
                name: "EndOfLifeRules");

            migrationBuilder.DropTable(
                name: "DiscoveryJobs");

            migrationBuilder.DropTable(
                name: "DiscoveryCredentials");

            migrationBuilder.DropIndex(
                name: "IX_ConfigurationItems_AppCode",
                table: "ConfigurationItems");

            migrationBuilder.DropIndex(
                name: "IX_ConfigurationItems_MacAddress",
                table: "ConfigurationItems");

            migrationBuilder.DropIndex(
                name: "IX_ConfigurationItems_SerialNumber",
                table: "ConfigurationItems");

            migrationBuilder.DropColumn(
                name: "AppCode",
                table: "ConfigurationItems");

            migrationBuilder.DropColumn(
                name: "AppLifecycle",
                table: "ConfigurationItems");

            migrationBuilder.DropColumn(
                name: "BusinessOwnerDisplayName",
                table: "ConfigurationItems");

            migrationBuilder.DropColumn(
                name: "BusinessOwnerId",
                table: "ConfigurationItems");

            migrationBuilder.DropColumn(
                name: "BusinessOwnerType",
                table: "ConfigurationItems");

            migrationBuilder.DropColumn(
                name: "BusinessUnit",
                table: "ConfigurationItems");

            migrationBuilder.DropColumn(
                name: "Criticality",
                table: "ConfigurationItems");

            migrationBuilder.DropColumn(
                name: "EndOfSupport",
                table: "ConfigurationItems");

            migrationBuilder.DropColumn(
                name: "GdprSensitive",
                table: "ConfigurationItems");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "ConfigurationItems");

            migrationBuilder.DropColumn(
                name: "MacAddress",
                table: "ConfigurationItems");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "ConfigurationItems");

            migrationBuilder.DropColumn(
                name: "Nis2Scope",
                table: "ConfigurationItems");

            migrationBuilder.DropColumn(
                name: "OperatingSystem",
                table: "ConfigurationItems");

            migrationBuilder.DropColumn(
                name: "PurchaseDate",
                table: "ConfigurationItems");

            migrationBuilder.DropColumn(
                name: "RpoHours",
                table: "ConfigurationItems");

            migrationBuilder.DropColumn(
                name: "RtoHours",
                table: "ConfigurationItems");

            migrationBuilder.DropColumn(
                name: "SerialNumber",
                table: "ConfigurationItems");

            migrationBuilder.DropColumn(
                name: "TechnicalOwnerDisplayName",
                table: "ConfigurationItems");

            migrationBuilder.DropColumn(
                name: "TechnicalOwnerId",
                table: "ConfigurationItems");

            migrationBuilder.DropColumn(
                name: "TechnicalOwnerType",
                table: "ConfigurationItems");

            migrationBuilder.DropColumn(
                name: "Vendor",
                table: "ConfigurationItems");

            migrationBuilder.DropColumn(
                name: "WarrantyExpiry",
                table: "ConfigurationItems");
        }
    }
}
