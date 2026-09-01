using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Sources.Data;
using Sources.Models;
using Xunit;

namespace Sources.Tests;

public class NeutronCalibrationSchemaTests
{
    [Fact]
    public void Migration_WithPreExistingNeutronSources_MigratesCalibratedEmissionRateAndEmissionCalibrationDate_PreservingCalibrationDate()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), "Sources_NeutronMigTest_" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .Options;

            // 1. تطبيق الترحيل السابق فقط (UnifyAddedByToGuid)
            using (var ctx = new AppDbContext(options))
            {
                var migrator = ctx.Database.GetService<IMigrator>();
                migrator.Migrate("20260901133004_UnifyAddedByToGuid");
            }

            var typeId = Guid.NewGuid();
            var locId = Guid.NewGuid();
            var ns1Id = Guid.NewGuid();
            var ns2Id = Guid.NewGuid();

            // 2. زرع صفين نيترونيين في المخطط القديم: أحدهما يحمل تاريخ معايرة والآخر بلا تاريخ معايرة
            using (var conn = new SqliteConnection($"Data Source={tempDbPath}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();

                cmd.CommandText = $@"
INSERT INTO NeutronSourceTypes (Id, Code, NameAr, NameEn, ReactionType, HalfLife, HalfLifeUnit, AverageNeutronEnergyMeV, TypicalNeutronYield, IsDeleted, CreatedAt)
VALUES ('{typeId}', 'Am-241/Be-Mig', 'أمريسيوم', 'Americium', '(α,n)', 432.2, 'years', 4.2, 2200000.0, 0, '2026-01-01');

INSERT INTO Locations (Id, LocationName, IsDeleted)
VALUES ('{locId}', 'مختبر المعايرة', 0);

-- صف 1: يحمل EmissionRate وتاريخ معايرة غير فارغ
INSERT INTO NeutronSources (Id, SourceCode, NeutronSourceTypeId, LocationId, EmissionRate, CalibrationDate, Status, IsDeleted, CreatedAt)
VALUES ('{ns1Id}', 'NS-WITH-CALIB', '{typeId}', '{locId}', 5000000.0, '2024-05-15 00:00:00', 'Storage', 0, '2026-01-01');

-- صف 2: يحمل EmissionRate وتاريخ معايرة فارغ (NULL)
INSERT INTO NeutronSources (Id, SourceCode, NeutronSourceTypeId, LocationId, EmissionRate, CalibrationDate, Status, IsDeleted, CreatedAt)
VALUES ('{ns2Id}', 'NS-WITHOUT-CALIB', '{typeId}', '{locId}', 1200000.0, NULL, 'Storage', 0, '2026-01-01');
";
                cmd.ExecuteNonQuery();
            }

            // 3. تطبيق الترحيل الجديد AddNeutronCalibrationAndDecayFields
            using (var ctx = new AppDbContext(options))
            {
                ctx.Database.Migrate();
            }

            // 4. التحقق بـ SQL خام تحت الإنفاذ الافتراضي للمفاتيح الخارجية
            using (var conn = new SqliteConnection($"Data Source={tempDbPath}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();

                // فحص الصف الأول: CalibratedEmissionRate = 5000000, EmissionCalibrationDate ممتلئ، CalibrationDate باقٍ
                cmd.CommandText = "SELECT CalibratedEmissionRate, EmissionCalibrationDate, CalibrationDate, CalibrationReference, AnisotropyFactor FROM NeutronSources WHERE SourceCode = 'NS-WITH-CALIB';";
                using (var reader = cmd.ExecuteReader())
                {
                    Assert.True(reader.Read());
                    Assert.Equal(5000000.0, reader.GetDouble(0));
                    Assert.False(reader.IsDBNull(1));
                    Assert.Contains("2024-05-15", reader.GetString(1));
                    Assert.False(reader.IsDBNull(2));
                    Assert.Contains("2024-05-15", reader.GetString(2)); // CalibrationDate باقٍ بقيمته الأصلية
                    Assert.True(reader.IsDBNull(3)); // CalibrationReference فارغ
                    Assert.True(reader.IsDBNull(4)); // AnisotropyFactor فارغ
                }

                // فحص الصف الثاني: CalibratedEmissionRate = 1200000, EmissionCalibrationDate فارغ، CalibrationDate فارغ
                cmd.CommandText = "SELECT CalibratedEmissionRate, EmissionCalibrationDate, CalibrationDate, CalibrationReference, AnisotropyFactor FROM NeutronSources WHERE SourceCode = 'NS-WITHOUT-CALIB';";
                using (var reader = cmd.ExecuteReader())
                {
                    Assert.True(reader.Read());
                    Assert.Equal(1200000.0, reader.GetDouble(0));
                    Assert.True(reader.IsDBNull(1)); // EmissionCalibrationDate فارغ
                    Assert.True(reader.IsDBNull(2)); // CalibrationDate فارغ
                    Assert.True(reader.IsDBNull(3)); // CalibrationReference فارغ
                    Assert.True(reader.IsDBNull(4)); // AnisotropyFactor فارغ
                }
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }

    [Fact]
    public void SeedData_PopulatesStandardIso8529Fields_OnlyForCf252_AndDocumentsStandardReferenceForAmBeAndAmB()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), "Sources_SeedDataTest_" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .Options;

            using (var ctx = new AppDbContext(options))
            {
                ctx.InitializeDatabase();
            }

            using (var ctx = new AppDbContext(options))
            {
                var types = ctx.NeutronSourceTypes.ToList();
                Assert.Equal(10, types.Count);

                // 1. Cf-252 المجرد هو النوع الوحيد ذو المعامل المحدد للنوع ككل في ISO 8529-1:2021 Table 1 و ISO 8529-3:2023 Table 2
                var cf252 = types.FirstOrDefault(t => t.Code == "Cf-252");
                Assert.NotNull(cf252);
                Assert.Equal(2.13, cf252!.MeanNeutronEnergyMeV);
                Assert.Equal(385.0, cf252.AmbientDoseConversionCoefficient);
                Assert.Equal("ISO 8529-1:2021 Table 1; ISO 8529-3:2023 Table 2", cf252.StandardReference);

                // 2. Am-241/Be: المعامل يعتمد على حجم الكبسولة ومصدرها، الحقول NULL والتوثيق المرجعي موجود
                var amBe = types.FirstOrDefault(t => t.Code == "Am-241/Be");
                Assert.NotNull(amBe);
                Assert.Null(amBe!.MeanNeutronEnergyMeV);
                Assert.Null(amBe.AmbientDoseConversionCoefficient);
                Assert.Equal("ISO 8529-3:2023 Table 2 — يعتمد على حجم المصدر (صغير 393 / كبير 387)؛ غير محدد للنوع", amBe.StandardReference);

                // 3. Am-241/B: خرج من الإشعاعات المرجعية في ISO 8529-1:2021، الحقول NULL والتوثيق المرجعي موجود
                var amB = types.FirstOrDefault(t => t.Code == "Am-241/B");
                Assert.NotNull(amB);
                Assert.Null(amB!.MeanNeutronEnergyMeV);
                Assert.Null(amB.AmbientDoseConversionCoefficient);
                Assert.Equal("خارج الإشعاعات المرجعية في ISO 8529-1:2021؛ لا معامل معياري حالي", amB.StandardReference);

                // 4. الأنواع السبعة الأخرى يجب أن تكون كافة الحقول المعيارية فيها NULL
                var otherCodes = new[]
                {
                    "Pu-239/Be", "Pu-238/Be", "Am-241/F", "Am-241/Li",
                    "Ra-226/Be", "Sb-124/Be", "NBS-1 (Ra-Be)"
                };

                foreach (var code in otherCodes)
                {
                    var item = types.FirstOrDefault(t => t.Code == code);
                    Assert.NotNull(item);
                    Assert.Null(item!.MeanNeutronEnergyMeV);
                    Assert.Null(item.AmbientDoseConversionCoefficient);
                    Assert.Null(item.StandardReference);
                }
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }
}
