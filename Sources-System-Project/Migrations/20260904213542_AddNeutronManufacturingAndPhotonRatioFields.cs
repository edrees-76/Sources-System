using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sources.Migrations
{
    /// <inheritdoc />
    public partial class AddNeutronManufacturingAndPhotonRatioFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "PhotonToNeutronDoseRatio",
                table: "NeutronSourceTypes",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CapsuleDiameterMm",
                table: "NeutronSources",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CapsuleLengthMm",
                table: "NeutronSources",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Manufacturer",
                table: "NeutronSources",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "NeutronSources",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotonToNeutronDoseRatio",
                table: "NeutronSourceTypes");

            migrationBuilder.DropColumn(
                name: "CapsuleDiameterMm",
                table: "NeutronSources");

            migrationBuilder.DropColumn(
                name: "CapsuleLengthMm",
                table: "NeutronSources");

            migrationBuilder.DropColumn(
                name: "Manufacturer",
                table: "NeutronSources");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "NeutronSources");
        }
    }
}
