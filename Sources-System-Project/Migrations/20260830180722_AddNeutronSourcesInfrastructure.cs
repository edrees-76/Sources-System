using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sources.Migrations
{
    /// <inheritdoc />
    public partial class AddNeutronSourcesInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.CreateTable(
                name: "NeutronSourceTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    NameEn = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NameAr = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ReactionType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TargetMaterial = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ParentNuclide = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    HalfLife = table.Column<double>(type: "REAL", nullable: false),
                    HalfLifeUnit = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AverageNeutronEnergyMeV = table.Column<double>(type: "REAL", nullable: true),
                    TypicalNeutronYield = table.Column<double>(type: "REAL", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    AddedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NeutronSourceTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NeutronSourceTypes_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "NeutronSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SerialNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    NeutronSourceTypeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EmissionRate = table.Column<double>(type: "REAL", nullable: false),
                    RelativeExpandedUncertaintyPercent = table.Column<double>(type: "REAL", nullable: true),
                    CalibrationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    AddedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NeutronSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NeutronSources_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_NeutronSources_NeutronSourceTypes_NeutronSourceTypeId",
                        column: x => x.NeutronSourceTypeId,
                        principalTable: "NeutronSourceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NeutronSources_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true,
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_NeutronSources_DeletedBy",
                table: "NeutronSources",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_NeutronSources_IsDeleted",
                table: "NeutronSources",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_NeutronSources_LocationId",
                table: "NeutronSources",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_NeutronSources_NeutronSourceTypeId",
                table: "NeutronSources",
                column: "NeutronSourceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_NeutronSources_SerialNumber",
                table: "NeutronSources",
                column: "SerialNumber");

            migrationBuilder.CreateIndex(
                name: "IX_NeutronSources_SourceCode",
                table: "NeutronSources",
                column: "SourceCode");

            migrationBuilder.CreateIndex(
                name: "IX_NeutronSources_Status",
                table: "NeutronSources",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_NeutronSourceTypes_Code",
                table: "NeutronSourceTypes",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_NeutronSourceTypes_DeletedBy",
                table: "NeutronSourceTypes",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_NeutronSourceTypes_IsDeleted",
                table: "NeutronSourceTypes",
                column: "IsDeleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NeutronSources");

            migrationBuilder.DropTable(
                name: "NeutronSourceTypes");

            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }
    }
}
