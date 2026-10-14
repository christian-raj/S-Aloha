using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAloha.Api.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class ServiceCatalogLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AffectedService",
                table: "ServiceRequests",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ServiceId",
                table: "ServiceRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServiceId",
                table: "Problems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServiceId",
                table: "Incidents",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_ServiceId",
                table: "ServiceRequests",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Problems_ServiceId",
                table: "Problems",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_ServiceId",
                table: "Incidents",
                column: "ServiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_Services_ServiceId",
                table: "Incidents",
                column: "ServiceId",
                principalTable: "Services",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Problems_Services_ServiceId",
                table: "Problems",
                column: "ServiceId",
                principalTable: "Services",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceRequests_Services_ServiceId",
                table: "ServiceRequests",
                column: "ServiceId",
                principalTable: "Services",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // INC-20, SLM-10 : l'ancien texte libre est repris — un nom de service connu du
            // catalogue (casse ignorée) rattache l'enregistrement à ce service, le plus
            // ancien en cas d'homonymes, en préférant un service non retiré.
            foreach (var table in new[] { "Incidents", "Problems" })
                migrationBuilder.Sql($"""
                    UPDATE "{table}" t SET "ServiceId" = (
                        SELECT s."Id" FROM "Services" s
                        WHERE lower(s."Title") = lower(btrim(t."AffectedService"))
                        ORDER BY (s."Status" = 'Retiré'), s."Id" LIMIT 1)
                    WHERE t."ServiceId" IS NULL AND btrim(t."AffectedService") <> '';
                    """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_Services_ServiceId",
                table: "Incidents");

            migrationBuilder.DropForeignKey(
                name: "FK_Problems_Services_ServiceId",
                table: "Problems");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceRequests_Services_ServiceId",
                table: "ServiceRequests");

            migrationBuilder.DropIndex(
                name: "IX_ServiceRequests_ServiceId",
                table: "ServiceRequests");

            migrationBuilder.DropIndex(
                name: "IX_Problems_ServiceId",
                table: "Problems");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_ServiceId",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "AffectedService",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "ServiceId",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "ServiceId",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "ServiceId",
                table: "Incidents");
        }
    }
}
