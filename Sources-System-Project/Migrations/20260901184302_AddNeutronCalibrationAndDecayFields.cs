using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sources.Migrations
{
    /// <inheritdoc />
    public partial class AddNeutronCalibrationAndDecayFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ─── 1. NeutronSourceTypes: إعادة تسمية متوسط الطاقة وإسقاط المردود وإضافة المعاملات المعيارية ───
            migrationBuilder.RenameColumn(
                name: "AverageNeutronEnergyMeV",
                table: "NeutronSourceTypes",
                newName: "MeanNeutronEnergyMeV");

            migrationBuilder.DropColumn(
                name: "TypicalNeutronYield",
                table: "NeutronSourceTypes");

            migrationBuilder.AddColumn<double>(
                name: "AmbientDoseConversionCoefficient",
                table: "NeutronSourceTypes",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StandardReference",
                table: "NeutronSourceTypes",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            // ─── 2. NeutronSources: إعادة تسمية معدل الانبعاث المعاير وإضافة حقول شهادة المعايرة ───
            migrationBuilder.RenameColumn(
                name: "EmissionRate",
                table: "NeutronSources",
                newName: "CalibratedEmissionRate");

            migrationBuilder.AddColumn<DateTime>(
                name: "EmissionCalibrationDate",
                table: "NeutronSources",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CalibrationReference",
                table: "NeutronSources",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AnisotropyFactor",
                table: "NeutronSources",
                type: "REAL",
                nullable: true);

            // ─── 3. ترحيل البيانات: نقل تاريخ المعايرة القائم إلى تاريخ معايرة معدل الانبعاث ───
            migrationBuilder.Sql("UPDATE NeutronSources SET EmissionCalibrationDate = CalibrationDate WHERE CalibrationDate IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ─── 1. تراجع NeutronSources ───
            migrationBuilder.DropColumn(
                name: "AnisotropyFactor",
                table: "NeutronSources");

            migrationBuilder.DropColumn(
                name: "CalibrationReference",
                table: "NeutronSources");

            migrationBuilder.DropColumn(
                name: "EmissionCalibrationDate",
                table: "NeutronSources");

            migrationBuilder.RenameColumn(
                name: "CalibratedEmissionRate",
                table: "NeutronSources",
                newName: "EmissionRate");

            // ─── 2. تراجع NeutronSourceTypes ───
            migrationBuilder.DropColumn(
                name: "StandardReference",
                table: "NeutronSourceTypes");

            migrationBuilder.DropColumn(
                name: "AmbientDoseConversionCoefficient",
                table: "NeutronSourceTypes");

            migrationBuilder.AddColumn<double>(
                name: "TypicalNeutronYield",
                table: "NeutronSourceTypes",
                type: "REAL",
                nullable: true);

            migrationBuilder.RenameColumn(
                name: "MeanNeutronEnergyMeV",
                table: "NeutronSourceTypes",
                newName: "AverageNeutronEnergyMeV");
        }
    }
}
