using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sources.Migrations
{
    /// <inheritdoc />
    public partial class AddBorrowRequestUniqueActiveIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BorrowRequests_SourceId",
                table: "BorrowRequests");

            migrationBuilder.AddColumn<bool>(
                name: "IsEditor",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Permissions",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddedBy",
                table: "Sources",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddedBy",
                table: "Radioisotopes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddedBy",
                table: "Locations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddedBy",
                table: "BorrowRequests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BorrowRequests_SourceId",
                table: "BorrowRequests",
                column: "SourceId",
                unique: true,
                filter: "Status IN ('Delivered', 'Overdue')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BorrowRequests_SourceId",
                table: "BorrowRequests");

            migrationBuilder.DropColumn(
                name: "IsEditor",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Permissions",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AddedBy",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "AddedBy",
                table: "Radioisotopes");

            migrationBuilder.DropColumn(
                name: "AddedBy",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "AddedBy",
                table: "BorrowRequests");

            migrationBuilder.CreateIndex(
                name: "IX_BorrowRequests_SourceId",
                table: "BorrowRequests",
                column: "SourceId");
        }
    }
}
