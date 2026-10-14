
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RondiTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncStokvelClosingDayConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No database change is needed.
            //
            // The closing-day check constraint was already created
            // by the AddStokvelClosingDay migration.
            //
            // This migration synchronizes the EF Core model snapshot
            // with the existing PostgreSQL database structure.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No database change is needed.
            //
            // The earlier AddStokvelClosingDay migration owns
            // the creation and removal of this constraint.
        }
    }
}
