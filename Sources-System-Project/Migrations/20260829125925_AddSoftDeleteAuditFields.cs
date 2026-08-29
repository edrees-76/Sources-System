using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sources.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Sources",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                table: "Sources",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSealed",
                table: "Sources",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Radioisotopes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                table: "Radioisotopes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "GammaConstant",
                table: "Radioisotopes",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Locations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                table: "Locations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LeakTestRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TestDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NextDueDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Result = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
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
                name: "IX_Users_DeletedBy",
                table: "Users",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_DeletedBy",
                table: "Sources",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Radioisotopes_DeletedBy",
                table: "Radioisotopes",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_DeletedBy",
                table: "Locations",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_LocationName",
                table: "Locations",
                column: "LocationName",
                unique: true,
                filter: "IsDeleted = 0");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Locations_Users_DeletedBy",
                table: "Locations",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Radioisotopes_Users_DeletedBy",
                table: "Radioisotopes",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Sources_Users_DeletedBy",
                table: "Sources",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_DeletedBy",
                table: "Users",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Locations_Users_DeletedBy",
                table: "Locations");

            migrationBuilder.DropForeignKey(
                name: "FK_Radioisotopes_Users_DeletedBy",
                table: "Radioisotopes");

            migrationBuilder.DropForeignKey(
                name: "FK_Sources_Users_DeletedBy",
                table: "Sources");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_DeletedBy",
                table: "Users");

            migrationBuilder.DropTable(
                name: "LeakTestRecords");

            migrationBuilder.DropIndex(
                name: "IX_Users_DeletedBy",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Sources_DeletedBy",
                table: "Sources");

            migrationBuilder.DropIndex(
                name: "IX_Radioisotopes_DeletedBy",
                table: "Radioisotopes");

            migrationBuilder.DropIndex(
                name: "IX_Locations_DeletedBy",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Locations_LocationName",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "IsSealed",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Radioisotopes");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Radioisotopes");

            migrationBuilder.DropColumn(
                name: "GammaConstant",
                table: "Radioisotopes");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Locations");
        }
    }
}
