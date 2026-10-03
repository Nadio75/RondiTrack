using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RondiTrack.Migrations
{
    /// <inheritdoc />
    public partial class AddStokvelMemberCompositeKeyAndNavigations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_StokvelMembers",
                table: "StokvelMembers");

            migrationBuilder.DropIndex(
                name: "IX_StokvelMembers_StokvelId_UserId",
                table: "StokvelMembers");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "StokvelMembers");

            migrationBuilder.RenameColumn(
                name: "JoinedAt",
                table: "StokvelMembers",
                newName: "JoinedAtUtc");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "StokvelMembers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Member");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StokvelMembers",
                table: "StokvelMembers",
                columns: new[] { "UserId", "StokvelId" });

            migrationBuilder.CreateIndex(
                name: "IX_StokvelMembers_StokvelId",
                table: "StokvelMembers",
                column: "StokvelId");

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_RecipientUserId_StokvelId",
                table: "Payouts",
                columns: new[] { "RecipientUserId", "StokvelId" });

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_UserId_StokvelId",
                table: "Contributions",
                columns: new[] { "UserId", "StokvelId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ContributionCycles_Stokvels_StokvelId",
                table: "ContributionCycles",
                column: "StokvelId",
                principalTable: "Stokvels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Contributions_StokvelMembers_UserId_StokvelId",
                table: "Contributions",
                columns: new[] { "UserId", "StokvelId" },
                principalTable: "StokvelMembers",
                principalColumns: new[] { "UserId", "StokvelId" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payouts_StokvelMembers_RecipientUserId_StokvelId",
                table: "Payouts",
                columns: new[] { "RecipientUserId", "StokvelId" },
                principalTable: "StokvelMembers",
                principalColumns: new[] { "UserId", "StokvelId" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StokvelMembers_Stokvels_StokvelId",
                table: "StokvelMembers",
                column: "StokvelId",
                principalTable: "Stokvels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StokvelMembers_Users_UserId",
                table: "StokvelMembers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContributionCycles_Stokvels_StokvelId",
                table: "ContributionCycles");

            migrationBuilder.DropForeignKey(
                name: "FK_Contributions_StokvelMembers_UserId_StokvelId",
                table: "Contributions");

            migrationBuilder.DropForeignKey(
                name: "FK_Payouts_StokvelMembers_RecipientUserId_StokvelId",
                table: "Payouts");

            migrationBuilder.DropForeignKey(
                name: "FK_StokvelMembers_Stokvels_StokvelId",
                table: "StokvelMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_StokvelMembers_Users_UserId",
                table: "StokvelMembers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StokvelMembers",
                table: "StokvelMembers");

            migrationBuilder.DropIndex(
                name: "IX_StokvelMembers_StokvelId",
                table: "StokvelMembers");

            migrationBuilder.DropIndex(
                name: "IX_Payouts_RecipientUserId_StokvelId",
                table: "Payouts");

            migrationBuilder.DropIndex(
                name: "IX_Contributions_UserId_StokvelId",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "StokvelMembers");

            migrationBuilder.RenameColumn(
                name: "JoinedAtUtc",
                table: "StokvelMembers",
                newName: "JoinedAt");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "StokvelMembers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_StokvelMembers",
                table: "StokvelMembers",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_StokvelMembers_StokvelId_UserId",
                table: "StokvelMembers",
                columns: new[] { "StokvelId", "UserId" },
                unique: true);
        }
    }
}
