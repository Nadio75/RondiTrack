using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RondiTrack.Infrastructure.Migrations
{
    public partial class AddXminConcurrencyTokens : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PostgreSQL xmin is a built-in system column.
            // No physical database column is created.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // PostgreSQL xmin is a built-in system column.
            // Nothing to remove.
        }
    }
}
