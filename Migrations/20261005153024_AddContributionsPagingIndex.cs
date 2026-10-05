using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RondiTrack.Migrations
{
    /// <inheritdoc />
    public partial class AddContributionsPagingIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Contributions_StokvelId_ContributionCycleId_CreatedAt_Id",
                table: "Contributions",
                columns: new[] { "StokvelId", "ContributionCycleId", "CreatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contributions_StokvelId_ContributionCycleId_CreatedAt_Id",
                table: "Contributions");
        }
    }
}
