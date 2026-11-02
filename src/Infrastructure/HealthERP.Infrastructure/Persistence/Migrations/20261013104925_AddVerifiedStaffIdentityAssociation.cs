using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVerifiedStaffIdentityAssociation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StaffKind",
                schema: "staff",
                table: "staff",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                schema: "staff",
                table: "staff",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_UserId",
                schema: "staff",
                table: "staff",
                column: "UserId",
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_staff_users_UserId",
                schema: "staff",
                table: "staff",
                column: "UserId",
                principalSchema: "identity",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_staff_users_UserId",
                schema: "staff",
                table: "staff");

            migrationBuilder.DropIndex(
                name: "IX_staff_UserId",
                schema: "staff",
                table: "staff");

            migrationBuilder.DropColumn(
                name: "StaffKind",
                schema: "staff",
                table: "staff");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "staff",
                table: "staff");
        }
    }
}
