using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sources.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceLocationHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SourceLocationHistories");
        }
    }
}
