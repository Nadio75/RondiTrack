using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RondiTrack.Migrations
{
    /// <inheritdoc />
    public partial class AddCompositeUniqueConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contributions_UserId_StokvelId",
                table: "Contributions");

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_ContributionCycleId",
                table: "Payouts",
                column: "ContributionCycleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_UserId_StokvelId_ContributionCycleId",
                table: "Contributions",
                columns: new[] { "UserId", "StokvelId", "ContributionCycleId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payouts_ContributionCycleId",
                table: "Payouts");

            migrationBuilder.DropIndex(
                name: "IX_Contributions_UserId_StokvelId_ContributionCycleId",
                table: "Contributions");

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_UserId_StokvelId",
                table: "Contributions",
                columns: new[] { "UserId", "StokvelId" });
        }
    }
}
