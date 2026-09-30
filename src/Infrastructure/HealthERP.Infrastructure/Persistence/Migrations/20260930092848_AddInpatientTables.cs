using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInpatientTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inpatient");

            migrationBuilder.CreateTable(
                name: "beds",
                schema: "inpatient",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WardName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RoomNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BedNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BedType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DailyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_beds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "admissions",
                schema: "inpatient",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    BedId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdmittingDoctorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ChiefComplaint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AdmissionDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DischargeDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DischargeSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DailyRateSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalRoomCharge = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsDischarged = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_admissions_beds_BedId",
                        column: x => x.BedId,
                        principalSchema: "inpatient",
                        principalTable: "beds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_admissions_patients_PatientId",
                        column: x => x.PatientId,
                        principalSchema: "patients",
                        principalTable: "patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_admissions_BedId",
                schema: "inpatient",
                table: "admissions",
                column: "BedId");

            migrationBuilder.CreateIndex(
                name: "IX_admissions_IsDischarged",
                schema: "inpatient",
                table: "admissions",
                column: "IsDischarged");

            migrationBuilder.CreateIndex(
                name: "IX_admissions_PatientId",
                schema: "inpatient",
                table: "admissions",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_beds_Status",
                schema: "inpatient",
                table: "beds",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_beds_WardName_RoomNumber_BedNumber",
                schema: "inpatient",
                table: "beds",
                columns: new[] { "WardName", "RoomNumber", "BedNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admissions",
                schema: "inpatient");

            migrationBuilder.DropTable(
                name: "beds",
                schema: "inpatient");
        }
    }
}
