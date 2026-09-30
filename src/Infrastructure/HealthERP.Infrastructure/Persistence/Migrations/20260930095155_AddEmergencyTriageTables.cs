using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmergencyTriageTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "triage");

            migrationBuilder.CreateTable(
                name: "triage_records",
                schema: "triage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChiefComplaint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BloodPressure = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    HeartRateBpm = table.Column<int>(type: "integer", nullable: false),
                    OxygenSaturationSpO2 = table.Column<int>(type: "integer", nullable: false),
                    BodyTemperatureCelsius = table.Column<decimal>(type: "numeric", nullable: false),
                    RespiratoryRateBpm = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssignedBay = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TriageNurseName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AttendingPhysicianName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ArrivalDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DispositionDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TransferredBedId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_triage_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_triage_records_patients_PatientId",
                        column: x => x.PatientId,
                        principalSchema: "patients",
                        principalTable: "patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_triage_records_PatientId",
                schema: "triage",
                table: "triage_records",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_triage_records_Severity",
                schema: "triage",
                table: "triage_records",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_triage_records_Status",
                schema: "triage",
                table: "triage_records",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "triage_records",
                schema: "triage");
        }
    }
}
