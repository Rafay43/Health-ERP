using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicalNoteRecordedByStaff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RecordedByStaffId",
                schema: "patients",
                table: "clinical_notes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_clinical_notes_RecordedByStaffId",
                schema: "patients",
                table: "clinical_notes",
                column: "RecordedByStaffId");

            migrationBuilder.AddForeignKey(
                name: "FK_clinical_notes_staff_RecordedByStaffId",
                schema: "patients",
                table: "clinical_notes",
                column: "RecordedByStaffId",
                principalSchema: "staff",
                principalTable: "staff",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_clinical_notes_staff_RecordedByStaffId",
                schema: "patients",
                table: "clinical_notes");

            migrationBuilder.DropIndex(
                name: "IX_clinical_notes_RecordedByStaffId",
                schema: "patients",
                table: "clinical_notes");

            migrationBuilder.DropColumn(
                name: "RecordedByStaffId",
                schema: "patients",
                table: "clinical_notes");
        }
    }
}
