using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Models;
using Xunit;

namespace Sources.Tests;

public class SeedDataOwnershipTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly DbContextOptions<AppDbContext> _options;

    public SeedDataOwnershipTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"sources_test_seed_ownership_{Guid.NewGuid():N}.db");
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_tempDbPath}")
            .Options;
    }

    public void Dispose()
    {
        try
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath);
            if (File.Exists(_tempDbPath + "-wal")) File.Delete(_tempDbPath + "-wal");
            if (File.Exists(_tempDbPath + "-shm")) File.Delete(_tempDbPath + "-shm");
        }
        catch { }
    }

    /// <summary>
    /// 1. اختبار العيب الحاجب: نظير مخصص مرتبط بمصدر لا يُحذف ولا يرمي استثناءً عند إعادة تشغيل SeedData
    /// </summary>
    [Fact]
    public void Test1_CustomIsotopeLinkedToSource_MustNotThrowAndMustBePreserved()
    {
        // 1. Initial seed
        using (var db = new AppDbContext(_options))
        {
            db.InitializeDatabase();
        }

        Guid customIsoId = Guid.NewGuid();
        Guid sourceId = Guid.NewGuid();

        // 2. Add custom isotope and link a source to it
        using (var db = new AppDbContext(_options))
        {
            var activityUnit = db.ActivityUnits.First();
            var customIso = new Radioisotope
            {
                Id = customIsoId,
                Symbol = "Zz-999",
                Name = "Custom-999",
                ArabicName = "مخصص-999",
                HalfLife = 100,
                HalfLifeUnit = "days",
                RadiationType = "Gamma",
                Energy = 500,
                Yield = 1.0,
                Category = 3,
                ExemptionLimit = 1.0,
                Notes = "ملاحظات مخصصة للمستخدم",
                EnglishNotes = "Custom user notes"
            };
            db.Radioisotopes.Add(customIso);

            var source = new Source
            {
                Id = sourceId,
                SourceCode = "SRC-CUSTOM-001",
                RadioisotopeId = customIsoId,
                InitialActivityValue = 100,
                InitialActivityUnitId = activityUnit.Id,
                CurrentActivityValue = 100,
                CurrentActivityUnitId = activityUnit.Id,
                CalibrationDate = DateTime.Today.AddDays(-10),
                Status = "Storage"
            };
            db.Sources.Add(source);
            db.SaveChanges();
        }

        // 3. Re-run SeedData (simulating next app startup)
        using (var db = new AppDbContext(_options))
        {
            db.InitializeDatabase();
        }

        // 4. Verify custom isotope and source still exist intact
        using (var db = new AppDbContext(_options))
        {
            var customIso = db.Radioisotopes.FirstOrDefault(r => r.Symbol == "Zz-999");
            Assert.NotNull(customIso);
            Assert.Equal("Custom-999", customIso!.Name);
            Assert.Equal("ملاحظات مخصصة للمستخدم", customIso.Notes);

            var source = db.Sources.FirstOrDefault(s => s.SourceCode == "SRC-CUSTOM-001");
            Assert.NotNull(source);
            Assert.Equal(customIsoId, source!.RadioisotopeId);
        }
    }

    /// <summary>
    /// 2. نظير مخصص يتيم (غير مرتبط بأي مصدر) يبقى بعد إعادة البذر
    /// </summary>
    [Fact]
    public void Test2_OrphanCustomIsotope_MustBePreservedAfterReseed()
    {
        // 1. Initial seed
        using (var db = new AppDbContext(_options))
        {
            db.InitializeDatabase();
        }

        // 2. Add orphan custom isotope
        using (var db = new AppDbContext(_options))
        {
            db.Radioisotopes.Add(new Radioisotope
            {
                Symbol = "Orphan-101",
                Name = "Orphan Isotope",
                ArabicName = "نظير يتيم",
                HalfLife = 50,
                HalfLifeUnit = "hours",
                RadiationType = "Beta",
                Energy = 250,
                Yield = 0.5,
                Category = 4,
                ExemptionLimit = 10.0,
                Notes = "نظير يتيم غير مرتبط",
                EnglishNotes = "Orphan isotope not linked"
            });
            db.SaveChanges();
        }

        // 3. Re-run SeedData
        using (var db = new AppDbContext(_options))
        {
            db.InitializeDatabase();
        }

        // 4. Verify orphan isotope still exists
        using (var db = new AppDbContext(_options))
        {
            var orphanIso = db.Radioisotopes.FirstOrDefault(r => r.Symbol == "Orphan-101");
            Assert.NotNull(orphanIso);
            Assert.Equal("Orphan Isotope", orphanIso!.Name);
            Assert.Equal("نظير يتيم غير مرتبط", orphanIso.Notes);
        }
    }

    /// <summary>
    /// 3. حقول البرنامج تُصحَّح: تعديل HalfLife و ExemptionLimit لنظير مبذور يعود إلى القيمة المرجعية
    /// </summary>
    [Fact]
    public void Test3_ProgramFieldsModified_MustBeResetToReferenceValues()
    {
        // 1. Initial seed
        using (var db = new AppDbContext(_options))
        {
            db.InitializeDatabase();
        }

        // 2. Modify program-owned fields for Co-60
        using (var db = new AppDbContext(_options))
        {
            var co60 = db.Radioisotopes.First(r => r.Symbol == "Co-60");
            co60.HalfLife = 99999.0;
            co60.ExemptionLimit = 88888.0;
            co60.RadiationType = "Alpha";
            db.SaveChanges();
        }

        // 3. Re-run SeedData
        using (var db = new AppDbContext(_options))
        {
            db.InitializeDatabase();
        }

        // 4. Verify program-owned fields are reset to standard reference values
        using (var db = new AppDbContext(_options))
        {
            var co60 = db.Radioisotopes.First(r => r.Symbol == "Co-60");
            Assert.Equal(5.27, co60.HalfLife);
            Assert.Equal(0.001, co60.ExemptionLimit);
            Assert.Equal("Gamma", co60.RadiationType);
        }
    }

    /// <summary>
    /// 4. حقول المستخدم تبقى: تعديل Notes و EnglishNotes لنظير مبذور لا يتغيّر بعد البذر
    /// </summary>
    [Fact]
    public void Test4_UserNotesModified_MustBePreservedAfterReseed()
    {
        // 1. Initial seed
        using (var db = new AppDbContext(_options))
        {
            db.InitializeDatabase();
        }

        // 2. Modify user-owned notes for Cs-137
        using (var db = new AppDbContext(_options))
        {
            var cs137 = db.Radioisotopes.First(r => r.Symbol == "Cs-137");
            cs137.Notes = "ملاحظات المستخدم الخاصة بالمختبر - لا تحذف";
            cs137.EnglishNotes = "Custom laboratory user notes - do not overwrite";
            db.SaveChanges();
        }

        // 3. Re-run SeedData
        using (var db = new AppDbContext(_options))
        {
            db.InitializeDatabase();
        }

        // 4. Verify user-owned notes remain untouched
        using (var db = new AppDbContext(_options))
        {
            var cs137 = db.Radioisotopes.First(r => r.Symbol == "Cs-137");
            Assert.Equal("ملاحظات المستخدم الخاصة بالمختبر - لا تحذف", cs137.Notes);
            Assert.Equal("Custom laboratory user notes - do not overwrite", cs137.EnglishNotes);
        }
    }

    /// <summary>
    /// 5. أنواع المصادر النيترونية تُدهس بالكامل: تعديل AmbientDoseConversionCoefficient و Notes يعود كلاهما للقيمة المرجعية
    /// </summary>
    [Fact]
    public void Test5_NeutronSourceTypes_MustBeFullyOverwrittenWithReferenceValues()
    {
        // 1. Initial seed
        using (var db = new AppDbContext(_options))
        {
            db.InitializeDatabase();
        }

        // 2. Modify standard values and notes for Cf-252 neutron source type
        using (var db = new AppDbContext(_options))
        {
            var cf252 = db.NeutronSourceTypes.First(t => t.Code == "Cf-252");
            cf252.AmbientDoseConversionCoefficient = 999.0;
            cf252.Notes = "ملاحظات معدلة يدوياً";
            db.SaveChanges();
        }

        // 3. Re-run SeedData
        using (var db = new AppDbContext(_options))
        {
            db.InitializeDatabase();
        }

        // 4. Verify both standard coefficient and notes are reset to standard reference values
        using (var db = new AppDbContext(_options))
        {
            var cf252 = db.NeutronSourceTypes.First(t => t.Code == "Cf-252");
            Assert.Equal(385.0, cf252.AmbientDoseConversionCoefficient);
            Assert.Equal("انشطار تلقائي مستمر، انبعاث عالي.", cf252.Notes);
        }
    }
}
