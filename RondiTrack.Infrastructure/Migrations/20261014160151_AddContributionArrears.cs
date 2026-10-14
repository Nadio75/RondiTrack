using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RondiTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContributionArrears : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContributionArrears",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StokvelId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContributionCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountCents = table.Column<long>(type: "bigint", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContributionArrears", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContributionArrears_ContributionCycles_ContributionCycleId",
                        column: x => x.ContributionCycleId,
                        principalTable: "ContributionCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContributionArrears_StokvelMembers_UserId_StokvelId",
                        columns: x => new { x.UserId, x.StokvelId },
                        principalTable: "StokvelMembers",
                        principalColumns: new[] { "UserId", "StokvelId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContributionArrears_ContributionCycleId_UserId",
                table: "ContributionArrears",
                columns: new[] { "ContributionCycleId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContributionArrears_UserId_StokvelId",
                table: "ContributionArrears",
                columns: new[] { "UserId", "StokvelId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContributionArrears");
        }
    }
}
