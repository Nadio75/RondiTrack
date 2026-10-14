using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RondiTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContributionCycleClosingState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAtUtc",
                table: "ContributionCycles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                table: "ContributionCycles",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClosedAtUtc",
                table: "ContributionCycles");

            migrationBuilder.DropColumn(
                name: "IsClosed",
                table: "ContributionCycles");
        }
    }
}
