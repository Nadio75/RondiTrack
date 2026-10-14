
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RondiTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStokvelClosingDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClosingDayOfMonth",
                table: "Stokvels",
                type: "integer",
                nullable: false,
                defaultValue: 25);

            // PostgreSQL must reject invalid closing days,
            // even when data is inserted outside the application.
            migrationBuilder.AddCheckConstraint(
                name: "CK_Stokvels_ClosingDayOfMonth",
                table: "Stokvels",
                sql: "\"ClosingDayOfMonth\" BETWEEN 1 AND 31");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Stokvels_ClosingDayOfMonth",
                table: "Stokvels");

            migrationBuilder.DropColumn(
                name: "ClosingDayOfMonth",
                table: "Stokvels");
        }
    }
}
