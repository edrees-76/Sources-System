using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sources.Migrations
{
    /// <inheritdoc />
    public partial class AddBorrowerName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UnitName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    UnitSymbol = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    ConversionToBq = table.Column<double>(type: "REAL", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityUnits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocationName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LocationType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Building = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Room = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ResponsiblePerson = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Radioisotopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ArabicName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    RadiationType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    HalfLife = table.Column<double>(type: "REAL", nullable: false),
                    HalfLifeUnit = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Energy = table.Column<double>(type: "REAL", nullable: false),
                    Yield = table.Column<double>(type: "REAL", nullable: true),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    ExemptionLimit = table.Column<double>(type: "REAL", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    EnglishNotes = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Radioisotopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoleName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Permissions = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GammaLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RadioisotopeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Energy = table.Column<double>(type: "REAL", nullable: false),
                    Intensity = table.Column<double>(type: "REAL", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GammaLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GammaLines_Radioisotopes_RadioisotopeId",
                        column: x => x.RadioisotopeId,
                        principalTable: "Radioisotopes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RadioisotopeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SerialNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Manufacturer = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    InitialActivityValue = table.Column<double>(type: "REAL", nullable: false),
                    InitialActivityUnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CalibrationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CurrentActivityValue = table.Column<double>(type: "REAL", nullable: false),
                    CurrentActivityUnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    HasDetailedIsotopes = table.Column<bool>(type: "INTEGER", nullable: false),
                    ImagePath = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sources_ActivityUnits_CurrentActivityUnitId",
                        column: x => x.CurrentActivityUnitId,
                        principalTable: "ActivityUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sources_ActivityUnits_InitialActivityUnitId",
                        column: x => x.InitialActivityUnitId,
                        principalTable: "ActivityUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sources_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Sources_Radioisotopes_RadioisotopeId",
                        column: x => x.RadioisotopeId,
                        principalTable: "Radioisotopes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    RoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FailedLoginAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    LockoutEnd = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastLoginDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AlertNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AlertType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    SourceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsRead = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDismissed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertNotifications_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SourceIsotopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RadioisotopeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InitialActivityValue = table.Column<double>(type: "REAL", nullable: true),
                    ActivityUnitId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CurrentActivityValue = table.Column<double>(type: "REAL", nullable: true),
                    CalibrationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceIsotopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceIsotopes_ActivityUnits_ActivityUnitId",
                        column: x => x.ActivityUnitId,
                        principalTable: "ActivityUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SourceIsotopes_Radioisotopes_RadioisotopeId",
                        column: x => x.RadioisotopeId,
                        principalTable: "Radioisotopes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SourceIsotopes_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Action = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TableName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    RecordId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ActionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Details = table.Column<string>(type: "TEXT", nullable: true),
                    OldValues = table.Column<string>(type: "TEXT", nullable: true),
                    NewValues = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BorrowRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BorrowerName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    BorrowerUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ApproverUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReturnedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Purpose = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    RequestDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpectedReturnDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ActualReturnDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApprovalDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeliveryDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    RejectionReason = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BorrowRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BorrowRequests_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BorrowRequests_Users_ApproverUserId",
                        column: x => x.ApproverUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BorrowRequests_Users_BorrowerUserId",
                        column: x => x.BorrowerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BorrowRequests_Users_ReturnedByUserId",
                        column: x => x.ReturnedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertNotifications_SourceId",
                table: "AlertNotifications",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BorrowRequests_ApproverUserId",
                table: "BorrowRequests",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BorrowRequests_BorrowerUserId",
                table: "BorrowRequests",
                column: "BorrowerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BorrowRequests_ReturnedByUserId",
                table: "BorrowRequests",
                column: "ReturnedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BorrowRequests_SourceId",
                table: "BorrowRequests",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_GammaLines_Energy",
                table: "GammaLines",
                column: "Energy");

            migrationBuilder.CreateIndex(
                name: "IX_GammaLines_RadioisotopeId",
                table: "GammaLines",
                column: "RadioisotopeId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceIsotopes_ActivityUnitId",
                table: "SourceIsotopes",
                column: "ActivityUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceIsotopes_RadioisotopeId",
                table: "SourceIsotopes",
                column: "RadioisotopeId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceIsotopes_SourceId",
                table: "SourceIsotopes",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_CurrentActivityUnitId",
                table: "Sources",
                column: "CurrentActivityUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_InitialActivityUnitId",
                table: "Sources",
                column: "InitialActivityUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_LocationId",
                table: "Sources",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_RadioisotopeId",
                table: "Sources",
                column: "RadioisotopeId");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_SourceCode",
                table: "Sources",
                column: "SourceCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertNotifications");

            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "BorrowRequests");

            migrationBuilder.DropTable(
                name: "GammaLines");

            migrationBuilder.DropTable(
                name: "SourceIsotopes");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Sources");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "ActivityUnits");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropTable(
                name: "Radioisotopes");
        }
    }
}
