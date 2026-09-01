using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sources.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
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
                name: "SourceCertificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    StoredFileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    AttachedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AttachedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceCertificates", x => x.Id);
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
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FailedLoginAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    LockoutEnd = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastLoginDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Permissions = table.Column<string>(type: "TEXT", nullable: true),
                    IsEditor = table.Column<bool>(type: "INTEGER", nullable: false)
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
                    table.ForeignKey(
                        name: "FK_Users_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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
                name: "Locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocationName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LocationType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Building = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Room = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ResponsiblePerson = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    AddedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Locations_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

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
                    GammaConstant = table.Column<double>(type: "REAL", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    EnglishNotes = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    AddedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Radioisotopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Radioisotopes_Users_DeletedBy",
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
                    IsSealed = table.Column<bool>(type: "INTEGER", nullable: false),
                    ImagePath = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    AddedBy = table.Column<string>(type: "TEXT", nullable: true),
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
                    table.ForeignKey(
                        name: "FK_Sources_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    AddedBy = table.Column<string>(type: "TEXT", nullable: true)
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
                name: "SourceLocationHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PreviousLocationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MovedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceLocationHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceLocationHistories_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SourceLocationHistories_Locations_PreviousLocationId",
                        column: x => x.PreviousLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SourceLocationHistories_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertNotifications_IsRead",
                table: "AlertNotifications",
                column: "IsRead");

            migrationBuilder.CreateIndex(
                name: "IX_AlertNotifications_SourceId",
                table: "AlertNotifications",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActionDate",
                table: "AuditLogs",
                column: "ActionDate");

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
                name: "IX_BorrowRequests_SourceId_Active",
                table: "BorrowRequests",
                column: "SourceId",
                unique: true,
                filter: "Status IN ('Delivered', 'Overdue')");

            migrationBuilder.CreateIndex(
                name: "IX_BorrowRequests_Status",
                table: "BorrowRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_GammaLines_Energy",
                table: "GammaLines",
                column: "Energy");

            migrationBuilder.CreateIndex(
                name: "IX_GammaLines_RadioisotopeId",
                table: "GammaLines",
                column: "RadioisotopeId");

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
                name: "IX_Locations_DeletedBy",
                table: "Locations",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_IsDeleted",
                table: "Locations",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_LocationName",
                table: "Locations",
                column: "LocationName",
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
                column: "SourceCode",
                unique: true,
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_NeutronSources_Status",
                table: "NeutronSources",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_NeutronSourceTypes_Code",
                table: "NeutronSourceTypes",
                column: "Code",
                unique: true,
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_NeutronSourceTypes_DeletedBy",
                table: "NeutronSourceTypes",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_NeutronSourceTypes_IsDeleted",
                table: "NeutronSourceTypes",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Radioisotopes_DeletedBy",
                table: "Radioisotopes",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Radioisotopes_IsDeleted",
                table: "Radioisotopes",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_SourceCertificates_SourceId",
                table: "SourceCertificates",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceCertificates_SourceType",
                table: "SourceCertificates",
                column: "SourceType");

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
                name: "IX_SourceLocationHistories_LocationId",
                table: "SourceLocationHistories",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceLocationHistories_MovedAt",
                table: "SourceLocationHistories",
                column: "MovedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SourceLocationHistories_PreviousLocationId",
                table: "SourceLocationHistories",
                column: "PreviousLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceLocationHistories_SourceId",
                table: "SourceLocationHistories",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_CalibrationDate",
                table: "Sources",
                column: "CalibrationDate");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_CurrentActivityUnitId",
                table: "Sources",
                column: "CurrentActivityUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_DeletedBy",
                table: "Sources",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_InitialActivityUnitId",
                table: "Sources",
                column: "InitialActivityUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_IsDeleted",
                table: "Sources",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_IsSealed",
                table: "Sources",
                column: "IsSealed");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_LocationId",
                table: "Sources",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_RadioisotopeId",
                table: "Sources",
                column: "RadioisotopeId");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_SerialNumber",
                table: "Sources",
                column: "SerialNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_SourceCode",
                table: "Sources",
                column: "SourceCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sources_Status",
                table: "Sources",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DeletedBy",
                table: "Users",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsDeleted",
                table: "Users",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true,
                filter: "IsDeleted = 0");
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
                name: "LeakTestRecords");

            migrationBuilder.DropTable(
                name: "NeutronSources");

            migrationBuilder.DropTable(
                name: "SourceCertificates");

            migrationBuilder.DropTable(
                name: "SourceIsotopes");

            migrationBuilder.DropTable(
                name: "SourceLocationHistories");

            migrationBuilder.DropTable(
                name: "NeutronSourceTypes");

            migrationBuilder.DropTable(
                name: "Sources");

            migrationBuilder.DropTable(
                name: "ActivityUnits");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropTable(
                name: "Radioisotopes");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Roles");
        }
    }
}
