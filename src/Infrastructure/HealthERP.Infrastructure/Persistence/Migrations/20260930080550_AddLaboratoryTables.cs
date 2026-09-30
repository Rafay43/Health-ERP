using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLaboratoryTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "laboratory");

            migrationBuilder.CreateTable(
                name: "lab_tests",
                schema: "laboratory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReferenceRange = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StandardPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SampleType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lab_tests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "lab_orders",
                schema: "laboratory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    LabTestId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderingDoctorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TestName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TestCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OrderedDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResultValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReferenceRange = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PerformedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CompletedDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsAbnormal = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lab_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lab_orders_lab_tests_LabTestId",
                        column: x => x.LabTestId,
                        principalSchema: "laboratory",
                        principalTable: "lab_tests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_lab_orders_patients_PatientId",
                        column: x => x.PatientId,
                        principalSchema: "patients",
                        principalTable: "patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lab_orders_LabTestId",
                schema: "laboratory",
                table: "lab_orders",
                column: "LabTestId");

            migrationBuilder.CreateIndex(
                name: "IX_lab_orders_PatientId",
                schema: "laboratory",
                table: "lab_orders",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_lab_orders_Priority",
                schema: "laboratory",
                table: "lab_orders",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_lab_orders_Status",
                schema: "laboratory",
                table: "lab_orders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_lab_tests_Code",
                schema: "laboratory",
                table: "lab_tests",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lab_orders",
                schema: "laboratory");

            migrationBuilder.DropTable(
                name: "lab_tests",
                schema: "laboratory");
        }
    }
}
