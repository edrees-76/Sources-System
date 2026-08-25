using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sources.Migrations
{
    /// <inheritdoc />
    public partial class AddGammaConstantToRadioisotopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "GammaConstant",
                table: "Radioisotopes",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GammaConstant",
                table: "Radioisotopes");
        }
    }
}
