using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sources.Migrations
{
    /// <inheritdoc />
    public partial class RenameNeutronSourceActivityToGeneric : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NeutronSources_ActivityUnits_Am241ActivityUnitId",
                table: "NeutronSources");

            migrationBuilder.RenameColumn(
                name: "Am241ActivityValue",
                table: "NeutronSources",
                newName: "ActivityValue");

            migrationBuilder.RenameColumn(
                name: "Am241ActivityUnitId",
                table: "NeutronSources",
                newName: "ActivityUnitId");

            migrationBuilder.RenameIndex(
                name: "IX_NeutronSources_Am241ActivityUnitId",
                table: "NeutronSources",
                newName: "IX_NeutronSources_ActivityUnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_NeutronSources_ActivityUnits_ActivityUnitId",
                table: "NeutronSources",
                column: "ActivityUnitId",
                principalTable: "ActivityUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NeutronSources_ActivityUnits_ActivityUnitId",
                table: "NeutronSources");

            migrationBuilder.RenameColumn(
                name: "ActivityValue",
                table: "NeutronSources",
                newName: "Am241ActivityValue");

            migrationBuilder.RenameColumn(
                name: "ActivityUnitId",
                table: "NeutronSources",
                newName: "Am241ActivityUnitId");

            migrationBuilder.RenameIndex(
                name: "IX_NeutronSources_ActivityUnitId",
                table: "NeutronSources",
                newName: "IX_NeutronSources_Am241ActivityUnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_NeutronSources_ActivityUnits_Am241ActivityUnitId",
                table: "NeutronSources",
                column: "Am241ActivityUnitId",
                principalTable: "ActivityUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
