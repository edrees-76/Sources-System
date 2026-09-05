using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sources.Migrations
{
    /// <inheritdoc />
    public partial class AddNeutronSourceAm241Activity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "Am241ActivityUnitId",
                table: "NeutronSources",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Am241ActivityValue",
                table: "NeutronSources",
                type: "REAL",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NeutronSources_Am241ActivityUnitId",
                table: "NeutronSources",
                column: "Am241ActivityUnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_NeutronSources_ActivityUnits_Am241ActivityUnitId",
                table: "NeutronSources",
                column: "Am241ActivityUnitId",
                principalTable: "ActivityUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NeutronSources_ActivityUnits_Am241ActivityUnitId",
                table: "NeutronSources");

            migrationBuilder.DropIndex(
                name: "IX_NeutronSources_Am241ActivityUnitId",
                table: "NeutronSources");

            migrationBuilder.DropColumn(
                name: "Am241ActivityUnitId",
                table: "NeutronSources");

            migrationBuilder.DropColumn(
                name: "Am241ActivityValue",
                table: "NeutronSources");
        }
    }
}
