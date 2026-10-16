using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SAloha.Api.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangeCabFreezeModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StandardModelId",
                table: "Changes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChangeApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChangeId = table.Column<int>(type: "integer", nullable: false),
                    Approver = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApproverDisplayName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Approved = table.Column<bool>(type: "boolean", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    At = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeApprovals_Changes_ChangeId",
                        column: x => x.ChangeId,
                        principalTable: "Changes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChangeFreezes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    End = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeFreezes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StandardChangeModels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ImplementationPlan = table.Column<string>(type: "text", nullable: false),
                    BackoutPlan = table.Column<string>(type: "text", nullable: false),
                    Risk = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StandardChangeModels", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Changes_StandardModelId",
                table: "Changes",
                column: "StandardModelId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeApprovals_ChangeId_Approver",
                table: "ChangeApprovals",
                columns: new[] { "ChangeId", "Approver" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Changes_StandardChangeModels_StandardModelId",
                table: "Changes",
                column: "StandardModelId",
                principalTable: "StandardChangeModels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Changes_StandardChangeModels_StandardModelId",
                table: "Changes");

            migrationBuilder.DropTable(
                name: "ChangeApprovals");

            migrationBuilder.DropTable(
                name: "ChangeFreezes");

            migrationBuilder.DropTable(
                name: "StandardChangeModels");

            migrationBuilder.DropIndex(
                name: "IX_Changes_StandardModelId",
                table: "Changes");

            migrationBuilder.DropColumn(
                name: "StandardModelId",
                table: "Changes");
        }
    }
}
