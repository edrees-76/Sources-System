using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sources.Migrations
{
    /// <inheritdoc />
    public partial class AddLeakTestRecordsAndIsSealed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSealed",
                table: "Sources",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "LeakTestRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TestDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NextDueDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Result = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Pass"),
                    MeasuredActivityBq = table.Column<double>(type: "REAL", nullable: true),
                    PerformedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    InspectorName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CertificateNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeakTestRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeakTestRecords_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeakTestRecords_Users_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeakTestRecords_NextDueDate",
                table: "LeakTestRecords",
                column: "NextDueDate");

            migrationBuilder.CreateIndex(
                name: "IX_LeakTestRecords_PerformedByUserId",
                table: "LeakTestRecords",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeakTestRecords_SourceId",
                table: "LeakTestRecords",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_IsSealed",
                table: "Sources",
                column: "IsSealed");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeakTestRecords");

            migrationBuilder.DropIndex(
                name: "IX_Sources_IsSealed",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "IsSealed",
                table: "Sources");
        }
    }
}
