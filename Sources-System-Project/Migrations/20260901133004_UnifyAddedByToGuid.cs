using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sources.Migrations
{
    /// <inheritdoc />
    public partial class UnifyAddedByToGuid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. ترحيل البيانات النصية القائمة إلى User.Id عند وجود تطابق وحيد مع Users.FullName
            migrationBuilder.Sql(@"
UPDATE Sources
SET AddedBy = (
    SELECT u.Id FROM Users u
    WHERE u.FullName = Sources.AddedBy
    GROUP BY u.FullName
    HAVING COUNT(*) = 1
)
WHERE AddedBy IS NOT NULL;

UPDATE Locations
SET AddedBy = (
    SELECT u.Id FROM Users u
    WHERE u.FullName = Locations.AddedBy
    GROUP BY u.FullName
    HAVING COUNT(*) = 1
)
WHERE AddedBy IS NOT NULL;

UPDATE Radioisotopes
SET AddedBy = (
    SELECT u.Id FROM Users u
    WHERE u.FullName = Radioisotopes.AddedBy
    GROUP BY u.FullName
    HAVING COUNT(*) = 1
)
WHERE AddedBy IS NOT NULL;

UPDATE BorrowRequests
SET AddedBy = (
    SELECT u.Id FROM Users u
    WHERE u.FullName = BorrowRequests.AddedBy
    GROUP BY u.FullName
    HAVING COUNT(*) = 1
)
WHERE AddedBy IS NOT NULL;
");

            // تنظيف القيم اليتيمة في الجدولين النيترونيين قبل إنشاء قيود المفاتيح الخارجية.
            // العمودان كانا Guid? منذ ما قبل الجولة 95 ويُكتبان بلا حارس وجود، فقد يحملان
            // معرّفات لمستخدمين غير موجودين أو محذوفين صلبياً.
            migrationBuilder.Sql(@"
UPDATE NeutronSources
SET AddedBy = NULL
WHERE AddedBy IS NOT NULL
  AND AddedBy NOT IN (SELECT Id FROM Users);

UPDATE NeutronSourceTypes
SET AddedBy = NULL
WHERE AddedBy IS NOT NULL
  AND AddedBy NOT IN (SELECT Id FROM Users);
");

            // 2. تعديل نوع الأعمدة
            migrationBuilder.AlterColumn<Guid>(
                name: "AddedBy",
                table: "Sources",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AddedBy",
                table: "Locations",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AddedBy",
                table: "Radioisotopes",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AddedBy",
                table: "BorrowRequests",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            // 3. إنشاء الفهارس والمفاتيح الخارجية
            migrationBuilder.CreateIndex(
                name: "IX_Sources_AddedBy",
                table: "Sources",
                column: "AddedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Radioisotopes_AddedBy",
                table: "Radioisotopes",
                column: "AddedBy");

            migrationBuilder.CreateIndex(
                name: "IX_NeutronSourceTypes_AddedBy",
                table: "NeutronSourceTypes",
                column: "AddedBy");

            migrationBuilder.CreateIndex(
                name: "IX_NeutronSources_AddedBy",
                table: "NeutronSources",
                column: "AddedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_AddedBy",
                table: "Locations",
                column: "AddedBy");

            migrationBuilder.CreateIndex(
                name: "IX_BorrowRequests_AddedBy",
                table: "BorrowRequests",
                column: "AddedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_BorrowRequests_Users_AddedBy",
                table: "BorrowRequests",
                column: "AddedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Locations_Users_AddedBy",
                table: "Locations",
                column: "AddedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_NeutronSources_Users_AddedBy",
                table: "NeutronSources",
                column: "AddedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_NeutronSourceTypes_Users_AddedBy",
                table: "NeutronSourceTypes",
                column: "AddedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Radioisotopes_Users_AddedBy",
                table: "Radioisotopes",
                column: "AddedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Sources_Users_AddedBy",
                table: "Sources",
                column: "AddedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BorrowRequests_Users_AddedBy",
                table: "BorrowRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Locations_Users_AddedBy",
                table: "Locations");

            migrationBuilder.DropForeignKey(
                name: "FK_NeutronSources_Users_AddedBy",
                table: "NeutronSources");

            migrationBuilder.DropForeignKey(
                name: "FK_NeutronSourceTypes_Users_AddedBy",
                table: "NeutronSourceTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_Radioisotopes_Users_AddedBy",
                table: "Radioisotopes");

            migrationBuilder.DropForeignKey(
                name: "FK_Sources_Users_AddedBy",
                table: "Sources");

            migrationBuilder.DropIndex(
                name: "IX_Sources_AddedBy",
                table: "Sources");

            migrationBuilder.DropIndex(
                name: "IX_Radioisotopes_AddedBy",
                table: "Radioisotopes");

            migrationBuilder.DropIndex(
                name: "IX_NeutronSourceTypes_AddedBy",
                table: "NeutronSourceTypes");

            migrationBuilder.DropIndex(
                name: "IX_NeutronSources_AddedBy",
                table: "NeutronSources");

            migrationBuilder.DropIndex(
                name: "IX_Locations_AddedBy",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_BorrowRequests_AddedBy",
                table: "BorrowRequests");

            migrationBuilder.AlterColumn<string>(
                name: "AddedBy",
                table: "Sources",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AddedBy",
                table: "Locations",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AddedBy",
                table: "Radioisotopes",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AddedBy",
                table: "BorrowRequests",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.Sql(@"
UPDATE Sources
SET AddedBy = (SELECT u.FullName FROM Users u WHERE u.Id = Sources.AddedBy)
WHERE AddedBy IS NOT NULL;

UPDATE Locations
SET AddedBy = (SELECT u.FullName FROM Users u WHERE u.Id = Locations.AddedBy)
WHERE AddedBy IS NOT NULL;

UPDATE Radioisotopes
SET AddedBy = (SELECT u.FullName FROM Users u WHERE u.Id = Radioisotopes.AddedBy)
WHERE AddedBy IS NOT NULL;

UPDATE BorrowRequests
SET AddedBy = (SELECT u.FullName FROM Users u WHERE u.Id = BorrowRequests.AddedBy)
WHERE AddedBy IS NOT NULL;
");
        }
    }
}
