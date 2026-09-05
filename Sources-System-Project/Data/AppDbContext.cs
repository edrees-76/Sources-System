using Microsoft.EntityFrameworkCore;
using Sources.Models;
using System;
using System.Linq;

namespace Sources.Data;

public class AppDbContext : DbContext
{
    public AppDbContext() { }
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ─── الجداول ───
    public DbSet<Radioisotope> Radioisotopes { get; set; } = null!;
    public DbSet<ActivityUnit> ActivityUnits { get; set; } = null!;
    public DbSet<Source> Sources { get; set; } = null!;
    public DbSet<Location> Locations { get; set; } = null!;
    public DbSet<BorrowRequest> BorrowRequests { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<GammaLine> GammaLines { get; set; } = null!;
    public DbSet<SourceIsotope> SourceIsotopes { get; set; } = null!;
    public DbSet<AlertNotification> AlertNotifications { get; set; } = null!;
    public DbSet<AppSetting> AppSettings { get; set; } = null!;
    public DbSet<SourceLocationHistory> SourceLocationHistories { get; set; } = null!;
    public DbSet<LeakTestRecord> LeakTestRecords { get; set; } = null!;
    public DbSet<NeutronSourceType> NeutronSourceTypes { get; set; } = null!;
    public DbSet<NeutronSource> NeutronSources { get; set; } = null!;
    public DbSet<SourceCertificate> SourceCertificates { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
        {
            // مسار LocalAppData حتى لا يحتاج البرنامج صلاحيات مدير.
            // الاستيراد من المسار القديم مسؤولية LegacyDatabaseImporter ويُستدعى مرة واحدة عند الإقلاع؛
            // ممنوع أي أثر على نظام الملفات هنا عدا ضمان وجود المجلد، لأن هذه الدالة
            // تُستدعى مع كل إنشاء سياق عبر IDbContextFactory.
            DatabasePaths.EnsureAppDataDirectory();

            var connStringBuilder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = DatabasePaths.DbPath,
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
                DefaultTimeout = 5
            };

            options.UseSqlite(connStringBuilder.ToString());
        }
    }

    public void InitializeDatabase()
    {
        Database.Migrate();
        ApplyPragmas();
        SeedData();
    }

    /// <summary>
    /// تطبيق إعدادات WAL ومهلة الانتظار لقاعدة بيانات SQLite
    /// </summary>
    private void ApplyPragmas()
    {
        var conn = Database.GetDbConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode = WAL; PRAGMA busy_timeout = 5000;";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    private void SeedData()
    {
        // ─── وحدات النشاط الإشعاعي ───
        var units = ActivityUnits.ToList();
        var becquerel = units.FirstOrDefault(u => u.UnitName == "Becquerel");
        if (becquerel != null) { becquerel.UnitSymbol = "Bq"; becquerel.DisplayOrder = 1; }
        else ActivityUnits.Add(new ActivityUnit { UnitName = "Becquerel", UnitSymbol = "Bq", ConversionToBq = 1, DisplayOrder = 1, Description = "الوحدة الدولية للنشاط الإشعاعي (SI)" });

        var curie = units.FirstOrDefault(u => u.UnitName == "Curie");
        if (curie != null) { curie.UnitSymbol = "Ci"; curie.DisplayOrder = 8; }
        else ActivityUnits.Add(new ActivityUnit { UnitName = "Curie", UnitSymbol = "Ci", ConversionToBq = 3.7e10, DisplayOrder = 8, Description = "1 Ci = 3.7 × 10¹⁰ Bq" });

        var mcurie = units.FirstOrDefault(u => u.UnitName == "Millicurie");
        if (mcurie != null) { mcurie.UnitSymbol = "mCi"; mcurie.DisplayOrder = 7; }
        else ActivityUnits.Add(new ActivityUnit { UnitName = "Millicurie", UnitSymbol = "mCi", ConversionToBq = 3.7e7, DisplayOrder = 7, Description = "1 mCi = 3.7 × 10⁷ Bq" });

        var ucurie = units.FirstOrDefault(u => u.UnitName == "Microcurie");
        if (ucurie != null) { ucurie.UnitSymbol = "µCi"; ucurie.DisplayOrder = 6; }
        else ActivityUnits.Add(new ActivityUnit { UnitName = "Microcurie", UnitSymbol = "µCi", ConversionToBq = 3.7e4, DisplayOrder = 6, Description = "1 µCi = 3.7 × 10⁴ Bq" });

        var kbecquerel = units.FirstOrDefault(u => u.UnitName == "Kilobecquerel");
        if (kbecquerel != null) { kbecquerel.UnitSymbol = "kBq"; kbecquerel.DisplayOrder = 2; }
        else ActivityUnits.Add(new ActivityUnit { UnitName = "Kilobecquerel", UnitSymbol = "kBq", ConversionToBq = 1e3, DisplayOrder = 2, Description = "1 kBq = 10³ Bq" });

        var mbecquerel = units.FirstOrDefault(u => u.UnitName == "Megabecquerel");
        if (mbecquerel != null) { mbecquerel.UnitSymbol = "MBq"; mbecquerel.DisplayOrder = 3; }
        else ActivityUnits.Add(new ActivityUnit { UnitName = "Megabecquerel", UnitSymbol = "MBq", ConversionToBq = 1e6, DisplayOrder = 3, Description = "1 MBq = 10⁶ Bq" });

        var gbecquerel = units.FirstOrDefault(u => u.UnitName == "Gigabecquerel");
        if (gbecquerel != null) { gbecquerel.UnitSymbol = "GBq"; gbecquerel.DisplayOrder = 4; }
        else ActivityUnits.Add(new ActivityUnit { UnitName = "Gigabecquerel", UnitSymbol = "GBq", ConversionToBq = 1e9, DisplayOrder = 4, Description = "1 GBq = 10⁹ Bq" });

        var tbecquerel = units.FirstOrDefault(u => u.UnitName == "Terabecquerel");
        if (tbecquerel != null) { tbecquerel.UnitSymbol = "TBq"; tbecquerel.DisplayOrder = 5; }
        else ActivityUnits.Add(new ActivityUnit { UnitName = "Terabecquerel", UnitSymbol = "TBq", ConversionToBq = 1e12, DisplayOrder = 5, Description = "1 TBq = 10¹² Bq" });

        SaveChanges();

        // ─── النظائر المشعة ───
        // ─── النظائر المشعة (المكتبة الجديدة المعتمدة بـ keV) ───
        var isotopeLibraryData = new[]
        {
            new { Symbol = "Am-241", Name = "Americium-241", ArabicName = "أمريسيوم-241", HalfLife = 432.2, HalfLifeUnit = "years", RadiationType = "Alpha", Energy = 59.54, Yield = 0.359, Category = 4, ExemptionLimit = 0.01, Notes = "صناعي (كواشف الدخان)، معايرة الطاقات المنخفضة.", EnglishNotes = "Industrial (smoke detectors), low energy calibration." },
            new { Symbol = "Ba-133", Name = "Barium-133", ArabicName = "باريوم-133", HalfLife = 10.51, HalfLifeUnit = "years", RadiationType = "Gamma", Energy = 356.01, Yield = 0.6205, Category = 4, ExemptionLimit = 1.0, Notes = "معايرة كواشف HPGe و NaI.", EnglishNotes = "Calibration of HPGe and NaI detectors." },
            new { Symbol = "Cf-252", Name = "Californium-252", ArabicName = "كاليفورنيوم-252", HalfLife = 2.645, HalfLifeUnit = "years", RadiationType = "Neutron", Energy = 100.2, Yield = 0.013, Category = 1, ExemptionLimit = 0.0001, Notes = "أمني وصناعي (مصدر نيوتروني عالي الخطورة).", EnglishNotes = "Security and industrial (high-risk neutron source)." },
            new { Symbol = "Co-57", Name = "Cobalt-57", ArabicName = "كوبالت-57", HalfLife = 271.7, HalfLifeUnit = "days", RadiationType = "Gamma", Energy = 122.06, Yield = 0.856, Category = 5, ExemptionLimit = 10.0, Notes = "معايرة طبية ومختبرية.", EnglishNotes = "Medical and laboratory calibration." },
            new { Symbol = "Co-60", Name = "Cobalt-60", ArabicName = "كوبالت-60", HalfLife = 5.27, HalfLifeUnit = "years", RadiationType = "Gamma", Energy = 1332.5, Yield = 0.9998, Category = 1, ExemptionLimit = 0.001, Notes = "تعقيم صناعي، علاج إشعاعي، معايرة طاقات عالية.", EnglishNotes = "Industrial sterilization, radiotherapy, high energy calibration." },
            new { Symbol = "Cs-137", Name = "Cesium-137", ArabicName = "سيزيوم-137", HalfLife = 30.08, HalfLifeUnit = "years", RadiationType = "Beta/Gamma", Energy = 661.7, Yield = 0.851, Category = 2, ExemptionLimit = 0.01, Notes = "المعيار العالمي للمعايرة والتصوير الصناعي.", EnglishNotes = "Global standard for calibration and industrial radiography." },
            new { Symbol = "Eu-152", Name = "Europium-152", ArabicName = "يوروبيوم-152", HalfLife = 13.53, HalfLifeUnit = "years", RadiationType = "Gamma", Energy = 1408.0, Yield = 0.208, Category = 4, ExemptionLimit = 1.0, Notes = "معايرة الكفاءة متعددة القمم.", EnglishNotes = "Multi-peak efficiency calibration." },
            new { Symbol = "F-18", Name = "Fluorine-18", ArabicName = "فلور-18", HalfLife = 109.7, HalfLifeUnit = "minutes", RadiationType = "Beta+", Energy = 511.0, Yield = 1.934, Category = 5, ExemptionLimit = 0.1, Notes = "طبي (تصوير PET Scan).", EnglishNotes = "Medical (PET Scan imaging)." },
            new { Symbol = "I-131", Name = "Iodine-131", ArabicName = "يود-131", HalfLife = 8.02, HalfLifeUnit = "days", RadiationType = "Beta/Gamma", Energy = 364.5, Yield = 0.817, Category = 4, ExemptionLimit = 0.01, Notes = "طبي (علاج الغدة الدرقية).", EnglishNotes = "Medical (Thyroid treatment)." },
            new { Symbol = "Ir-192", Name = "Iridium-192", ArabicName = "إريديوم-192", HalfLife = 73.83, HalfLifeUnit = "days", RadiationType = "Gamma", Energy = 316.5, Yield = 0.827, Category = 2, ExemptionLimit = 0.01, Notes = "تصوير إشعاعي صناعي (فحص الأنابيب).", EnglishNotes = "Industrial radiography (pipe inspection)." },
            new { Symbol = "K-40", Name = "Potassium-40", ArabicName = "بوتاسيوم-40", HalfLife = 1.25e9, HalfLifeUnit = "years", RadiationType = "Beta/Gamma", Energy = 1460.82, Yield = 0.106, Category = 5, ExemptionLimit = 100.0, Notes = "طبيعي (تعريف الخلفية الإشعاعية).", EnglishNotes = "Natural (identifying radiation background)." },
            new { Symbol = "Lu-177", Name = "Lutetium-177", ArabicName = "لوتيتيوم-177", HalfLife = 6.64, HalfLifeUnit = "days", RadiationType = "Beta/Gamma", Energy = 208.36, Yield = 0.1036, Category = 4, ExemptionLimit = 1.0, Notes = "طبي حديث (علاجات سرطانية موجهة).", EnglishNotes = "Modern medical (targeted cancer therapies)." },
            new { Symbol = "Na-22", Name = "Sodium-22", ArabicName = "صوديوم-22", HalfLife = 2.6, HalfLifeUnit = "years", RadiationType = "Beta+/Gamma", Energy = 1274.5, Yield = 0.999, Category = 4, ExemptionLimit = 0.1, Notes = "معايرة وبحوث فيزيائية.", EnglishNotes = "Calibration and physical research." },
            new { Symbol = "Pu-239", Name = "Plutonium-239", ArabicName = "بلوتونيوم-239", HalfLife = 24110.0, HalfLifeUnit = "years", RadiationType = "Alpha", Energy = 51.62, Yield = 0.000063, Category = 1, ExemptionLimit = 0.001, Notes = "أمني (مواد نووية خاضعة للضمانات).", EnglishNotes = "Security (safeguarded nuclear materials)." },
            new { Symbol = "Ra-226", Name = "Radium-226", ArabicName = "راديوم-226", HalfLife = 1600.0, HalfLifeUnit = "years", RadiationType = "Alpha/Gamma", Energy = 186.2, Yield = 0.0359, Category = 2, ExemptionLimit = 0.01, Notes = "طبيعي وأثري، وصناعي قديم.", EnglishNotes = "Natural, archaeological, and old industrial." },
            new { Symbol = "Se-75", Name = "Selenium-75", ArabicName = "سيلينيوم-75", HalfLife = 119.7, HalfLifeUnit = "days", RadiationType = "Gamma", Energy = 264.65, Yield = 0.589, Category = 3, ExemptionLimit = 0.1, Notes = "تصوير إشعاعي صناعي للمواد الرقيقة.", EnglishNotes = "Industrial radiography for thin materials." },
            new { Symbol = "Tc-99m", Name = "Technetium-99m", ArabicName = "تكنيشيوم-99m", HalfLife = 6.01, HalfLifeUnit = "hours", RadiationType = "Gamma", Energy = 140.5, Yield = 0.891, Category = 5, ExemptionLimit = 0.1, Notes = "التصوير الطبي الأكثر شيوعاً.", EnglishNotes = "The most common medical imaging." },
            new { Symbol = "Tl-208", Name = "Thallium-208", ArabicName = "ثاليوم-208", HalfLife = 3.05, HalfLifeUnit = "minutes", RadiationType = "Gamma", Energy = 2614.51, Yield = 0.997, Category = 5, ExemptionLimit = 1.0, Notes = "تعريف نهاية الطيف والخلفية الطبيعية.", EnglishNotes = "End-of-spectrum definition and natural background." },
            new { Symbol = "Y-88", Name = "Yttrium-88", ArabicName = "يتريوم-88", HalfLife = 106.6, HalfLifeUnit = "days", RadiationType = "Gamma", Energy = 1836.06, Yield = 0.992, Category = 5, ExemptionLimit = 1.0, Notes = "معايرة الطاقات العالية (المصادر المختلطة).", EnglishNotes = "High energy calibration (mixed sources)." }
        };

        // 1. تحديث أو إضافة النظائر من المكتبة المعتمدة
        foreach (var item in isotopeLibraryData)
        {
            var symbol = item.Symbol;
            var existing = Radioisotopes.FirstOrDefault(r => r.Symbol == symbol);
            if (existing != null)
            {
                // تحديث حقول البرنامج ذات المرجع الخارجي القياسي؛ الملاحظات يملكها المستخدم فلا تُدهس
                existing.Name = item.Name;
                existing.ArabicName = item.ArabicName;
                existing.HalfLife = item.HalfLife;
                existing.HalfLifeUnit = item.HalfLifeUnit;
                existing.RadiationType = item.RadiationType;
                existing.Energy = item.Energy;
                existing.Yield = item.Yield;
                existing.Category = item.Category;
                existing.ExemptionLimit = item.ExemptionLimit;
            }
            else
            {
                Radioisotopes.Add(new Radioisotope
                {
                    Symbol = item.Symbol,
                    Name = item.Name,
                    ArabicName = item.ArabicName,
                    HalfLife = item.HalfLife,
                    HalfLifeUnit = item.HalfLifeUnit,
                    RadiationType = item.RadiationType,
                    Energy = item.Energy,
                    Yield = item.Yield,
                    Category = item.Category,
                    ExemptionLimit = item.ExemptionLimit,
                    Notes = item.Notes,
                    EnglishNotes = item.EnglishNotes
                });
            }
        }

        SaveChanges();

        // ─── أنواع المصادر النيترونية المرجعية (10 أنواع معتمدة) ───
        var neutronTypeData = new[]
        {
            new { Code = "Cf-252", NameEn = "Californium-252", NameAr = "كاليفورنيوم-252", ReactionType = "Spontaneous Fission", TargetMaterial = (string?)null, ParentNuclide = "Cf-252", HalfLife = 2.645, HalfLifeUnit = "years", MeanNeutronEnergyMeV = (double?)2.13, AmbientDoseConversionCoefficient = (double?)385.0, StandardReference = (string?)"ISO 8529-1:2021 Table 1; ISO 8529-3:2023 Table 2", Notes = "انشطار تلقائي مستمر، انبعاث عالي." },
            new { Code = "Am-241/Be-Small", NameEn = "Americium-241/Beryllium (small source)", NameAr = "أمريسيوم-241 / بيريليوم (مصدر صغير)", ReactionType = "(α,n)", TargetMaterial = (string?)"Be", ParentNuclide = "Am-241", HalfLife = 432.2, HalfLifeUnit = "years", MeanNeutronEnergyMeV = (double?)4.17, AmbientDoseConversionCoefficient = (double?)393.0, StandardReference = (string?)"ISO 8529-1:2021 Table 1; ISO 8529-3:2023 Table 2", Notes = "الأكثر شيوعاً في التطبيقات الصناعية وسبر الآبار؛ مصدر صغير (نشاط نموذجي أقل، وفق ISO 8529-1 §4.4)." },
            new { Code = "Am-241/Be-Large", NameEn = "Americium-241/Beryllium (large source)", NameAr = "أمريسيوم-241 / بيريليوم (مصدر كبير)", ReactionType = "(α,n)", TargetMaterial = (string?)"Be", ParentNuclide = "Am-241", HalfLife = 432.2, HalfLifeUnit = "years", MeanNeutronEnergyMeV = (double?)4.05, AmbientDoseConversionCoefficient = (double?)387.0, StandardReference = (string?)"ISO 8529-1:2021 Table 1; ISO 8529-3:2023 Table 2", Notes = "الأكثر شيوعاً في التطبيقات الصناعية وسبر الآبار؛ مصدر كبير (نشاط نموذجي أعلى، وفق ISO 8529-1 §4.4)." },
            new { Code = "Pu-239/Be", NameEn = "Plutonium-239/Beryllium", NameAr = "بلوتونيوم-239 / بيريليوم", ReactionType = "(α,n)", TargetMaterial = (string?)"Be", ParentNuclide = "Pu-239", HalfLife = 24110.0, HalfLifeUnit = "years", MeanNeutronEnergyMeV = (double?)null, AmbientDoseConversionCoefficient = (double?)null, StandardReference = (string?)null, Notes = "مصدر قياسي مستقر جداً طويل العمر." },
            new { Code = "Pu-238/Be", NameEn = "Plutonium-238/Beryllium", NameAr = "بلوتونيوم-238 / بيريليوم", ReactionType = "(α,n)", TargetMaterial = (string?)"Be", ParentNuclide = "Pu-238", HalfLife = 87.7, HalfLifeUnit = "years", MeanNeutronEnergyMeV = (double?)null, AmbientDoseConversionCoefficient = (double?)null, StandardReference = (string?)null, Notes = "انبعاث نيتروني مرتفع بحجم مدمج." },
            new { Code = "Am-241/B", NameEn = "Americium-241/Boron", NameAr = "أمريسيوم-241 / بورون", ReactionType = "(α,n)", TargetMaterial = (string?)"B", ParentNuclide = "Am-241", HalfLife = 432.2, HalfLifeUnit = "years", MeanNeutronEnergyMeV = (double?)null, AmbientDoseConversionCoefficient = (double?)null, StandardReference = (string?)"خارج الإشعاعات المرجعية في ISO 8529-1:2021؛ لا معامل معياري حالي", Notes = "طيف طاقة أقل من Am-Be." },
            new { Code = "Am-241/F", NameEn = "Americium-241/Fluorine", NameAr = "أمريسيوم-241 / فلور", ReactionType = "(α,n)", TargetMaterial = (string?)"F", ParentNuclide = "Am-241", HalfLife = 432.2, HalfLifeUnit = "years", MeanNeutronEnergyMeV = (double?)null, AmbientDoseConversionCoefficient = (double?)null, StandardReference = (string?)null, Notes = "طاقة نيترونات متوسطة منخفضة." },
            new { Code = "Am-241/Li", NameEn = "Americium-241/Lithium", NameAr = "أمريسيوم-241 / ليثيوم", ReactionType = "(α,n)", TargetMaterial = (string?)"Li", ParentNuclide = "Am-241", HalfLife = 432.2, HalfLifeUnit = "years", MeanNeutronEnergyMeV = (double?)null, AmbientDoseConversionCoefficient = (double?)null, StandardReference = (string?)null, Notes = "نيترونات منخفضة الطاقة." },
            new { Code = "Ra-226/Be", NameEn = "Radium-226/Beryllium (α,n)", NameAr = "راديوم-226 / بيريليوم (ألفا)", ReactionType = "(α,n)", TargetMaterial = (string?)"Be", ParentNuclide = "Ra-226", HalfLife = 1600.0, HalfLifeUnit = "years", MeanNeutronEnergyMeV = (double?)null, AmbientDoseConversionCoefficient = (double?)null, StandardReference = (string?)null, Notes = "مصدر تاريخي قياسي ذو طيف معقد ومستوى غاما مرتفع." },
            new { Code = "Sb-124/Be", NameEn = "Antimony-124/Beryllium", NameAr = "أنتيمون-124 / بيريليوم", ReactionType = "(γ,n)", TargetMaterial = (string?)"Be", ParentNuclide = "Sb-124", HalfLife = 60.2, HalfLifeUnit = "days", MeanNeutronEnergyMeV = (double?)null, AmbientDoseConversionCoefficient = (double?)null, StandardReference = (string?)null, Notes = "مصدر نيترونات ضوئية شبه أحادية الطاقة (24 keV)." },
            new { Code = "NBS-1 (Ra-Be)", NameEn = "NBS-1 Standard Radium-Beryllium", NameAr = "معيار NBS-1 راديوم-بيريليوم", ReactionType = "(γ,n)", TargetMaterial = (string?)"Be", ParentNuclide = "Ra-226", HalfLife = 1600.0, HalfLifeUnit = "years", MeanNeutronEnergyMeV = (double?)null, AmbientDoseConversionCoefficient = (double?)null, StandardReference = (string?)null, Notes = "المصدر المعياري القومي للمعايرة والتوثيق المرجعي." }
        };

        foreach (var item in neutronTypeData)
        {
            var existing = NeutronSourceTypes.IgnoreQueryFilters().FirstOrDefault(nt => nt.Code == item.Code);
            if (existing != null)
            {
                existing.NameEn = item.NameEn;
                existing.NameAr = item.NameAr;
                existing.ReactionType = item.ReactionType;
                existing.TargetMaterial = item.TargetMaterial;
                existing.ParentNuclide = item.ParentNuclide;
                existing.HalfLife = item.HalfLife;
                existing.HalfLifeUnit = item.HalfLifeUnit;
                existing.MeanNeutronEnergyMeV = item.MeanNeutronEnergyMeV;
                existing.AmbientDoseConversionCoefficient = item.AmbientDoseConversionCoefficient;
                existing.StandardReference = item.StandardReference;
                existing.Notes = item.Notes;
            }
            else
            {
                NeutronSourceTypes.Add(new NeutronSourceType
                {
                    Code = item.Code,
                    NameEn = item.NameEn,
                    NameAr = item.NameAr,
                    ReactionType = item.ReactionType,
                    TargetMaterial = item.TargetMaterial,
                    ParentNuclide = item.ParentNuclide,
                    HalfLife = item.HalfLife,
                    HalfLifeUnit = item.HalfLifeUnit,
                    MeanNeutronEnergyMeV = item.MeanNeutronEnergyMeV,
                    AmbientDoseConversionCoefficient = item.AmbientDoseConversionCoefficient,
                    StandardReference = item.StandardReference,
                    Notes = item.Notes
                });
            }
        }

        SaveChanges();

        // ─── إبطال الصف القديم "Am-241/Be" غير المحدد بعد استبداله بصفَي (صغير/كبير) — الجولة 124 ───
        var legacyAmBe = NeutronSourceTypes.IgnoreQueryFilters().FirstOrDefault(nt => nt.Code == "Am-241/Be");
        if (legacyAmBe != null && !legacyAmBe.IsDeleted)
        {
            legacyAmBe.IsDeleted = true;
            legacyAmBe.DeletedAt = DateTime.Now;
            SaveChanges();
        }

        // ─── الصلاحيات (مُحدّثة بـ RBAC لتكون مدير ومستخدم فقط) ───
        var existingRoles = Roles.ToList();

        // 1. صيانة دور المدير
        var adminRole2 = existingRoles.FirstOrDefault(r => r.RoleName == "مدير النظام");
        if (adminRole2 == null)
        {
            adminRole2 = new Role { RoleName = "مدير النظام", Description = "صلاحيات كاملة لإدارة النظام", Permissions = "All" };
            Roles.Add(adminRole2);
            SaveChanges();
        }

        // 2. صيانة دور المستخدم العادي
        var userRole = existingRoles.FirstOrDefault(r => r.RoleName == "مستخدم");
        if (userRole == null)
        {
            userRole = new Role { RoleName = "مستخدم", Description = "مستخدم عادي في المنظومة", Permissions = "" };
            Roles.Add(userRole);
            SaveChanges();
        }

        // 3. مسح الأدوار القديمة وتخصيص مستخدميها لدور "مستخدم"
        var rolesToDelete = Roles.Where(r => r.RoleName != "مدير النظام" && r.RoleName != "مستخدم").ToList();
        if (rolesToDelete.Any())
        {
            foreach (var role in rolesToDelete)
            {
                var usersInRole = Users.Where(u => u.RoleId == role.Id).ToList();
                foreach (var u in usersInRole) u.RoleId = userRole.Id;
            }
            Roles.RemoveRange(rolesToDelete);
            SaveChanges();
        }

        // ─── مستخدم مدير النظام ───
        var adminUser = Users.FirstOrDefault(u => u.Username == "admin");
        var adminRole = Roles.FirstOrDefault(r => r.RoleName == "مدير النظام") ?? Roles.First();

        if (adminUser == null)
        {
            Users.Add(new User
            {
                FullName = "مدير النظام",
                Username = "admin",
                PasswordHash = Helpers.PasswordHelper.HashPassword("admin"),
                RoleId = adminRole.Id,
                Email = "admin@sources.local",
                IsActive = true
            });
            SaveChanges();
        }
        else
        {
            // محاولة التحقق من كلمة المرور القديمة أو تحديثها للنظام الجديد إذا كانت "admin"
            // ملاحظة: PasswordHelper.VerifyPassword سيفشل إذا كان الهاش قديماً (SHA256)
            // ولذلك سنقوم بتحديث الهاش إذا كانت كلمة السر ما زالت هي الافتراضية "admin"
            try 
            {
                if (!Helpers.PasswordHelper.VerifyPassword("admin", adminUser.PasswordHash))
                {
                    // التحقق يدوياً إذا كان الهاش هو SHA256 لكلمة "admin"
                    using var sha = System.Security.Cryptography.SHA256.Create();
                    var oldHash = Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("admin")));
                    
                    if (adminUser.PasswordHash == oldHash)
                    {
                        adminUser.PasswordHash = Helpers.PasswordHelper.HashPassword("admin");
                        SaveChanges();
                    }
                }
            }
            catch { /* تجاوز أي خطأ في التحقق */ }
        }

        // ─── مواقع افتراضية ───
        if (!Locations.Any())
        {
            Locations.AddRange(
                new Location { LocationName = "المخزن الرئيسي", LocationType = "Storage", Building = "المبنى A", Room = "غرفة 101", ResponsiblePerson = "المشرف العام" },
                new Location { LocationName = "معمل القياسات", LocationType = "Lab", Building = "المبنى B", Room = "غرفة 201", ResponsiblePerson = "رئيس المعمل" },
                new Location { LocationName = "قسم الطب النووي", LocationType = "Hospital", Building = "المبنى C", Room = "غرفة 301", ResponsiblePerson = "الطبيب المختص" }
            );
            SaveChanges();
        }

        // ─── إعدادات افتراضية ───
        if (!AppSettings.Any())
        {
            foreach (var kvp in SystemSettingsDefaults.AllDefaults)
            {
                AppSettings.Add(new AppSetting { Key = kvp.Key, Value = kvp.Value });
            }
            SaveChanges();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ─── Source relationships & Indexes ───
        modelBuilder.Entity<Source>(entity =>
        {
            entity.HasOne(s => s.Radioisotope)
                .WithMany(r => r.Sources)
                .HasForeignKey(s => s.RadioisotopeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.InitialActivityUnit)
                .WithMany(u => u.SourcesInitial)
                .HasForeignKey(s => s.InitialActivityUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.CurrentActivityUnit)
                .WithMany(u => u.SourcesCurrent)
                .HasForeignKey(s => s.CurrentActivityUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.Location)
                .WithMany(l => l.Sources)
                .HasForeignKey(s => s.LocationId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(s => s.DeletedByUser)
                .WithMany()
                .HasForeignKey(s => s.DeletedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(s => s.AddedByUser)
                .WithMany()
                .HasForeignKey(s => s.AddedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            // كود المصدر معرّف دائم لجسم مشع خاضع للرقابة ولا يُعاد استخدامه بعد الحذف الناعم حفاظاً على سلامة سجل الحيازة والتدقيق
            entity.HasIndex(s => s.SourceCode).IsUnique();

            // فهارس الأداء للمصادر
            entity.HasIndex(s => s.Status);
            entity.HasIndex(s => s.CalibrationDate);
            entity.HasIndex(s => s.IsDeleted);
            entity.HasIndex(s => s.SerialNumber);
            entity.HasIndex(s => s.IsSealed);
        });

        // ─── Location relationships & Unique Index ───
        modelBuilder.Entity<Location>(entity =>
        {
            entity.HasOne(l => l.DeletedByUser)
                .WithMany()
                .HasForeignKey(l => l.DeletedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(l => l.AddedByUser)
                .WithMany()
                .HasForeignKey(l => l.AddedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(l => l.LocationName)
                .HasFilter("IsDeleted = 0")
                .IsUnique();

            entity.HasIndex(l => l.IsDeleted);
        });

        // ─── Radioisotope relationships & Indexes ───
        modelBuilder.Entity<Radioisotope>(entity =>
        {
            entity.HasOne(r => r.DeletedByUser)
                .WithMany()
                .HasForeignKey(r => r.DeletedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(r => r.AddedByUser)
                .WithMany()
                .HasForeignKey(r => r.AddedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(r => r.IsDeleted);
        });

        // ─── BorrowRequest relationships & Indexes ───
        modelBuilder.Entity<BorrowRequest>(entity =>
        {
            entity.HasOne(b => b.Source)
                .WithMany(s => s.BorrowRequests)
                .HasForeignKey(b => b.SourceId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(b => b.BorrowerUser)
                .WithMany(u => u.BorrowRequestsAsBorrower)
                .HasForeignKey(b => b.BorrowerUserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.ApproverUser)
                .WithMany(u => u.BorrowRequestsAsApprover)
                .HasForeignKey(b => b.ApproverUserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(b => b.ReturnedByUser)
                .WithMany()
                .HasForeignKey(b => b.ReturnedByUserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(b => b.AddedByUser)
                .WithMany()
                .HasForeignKey(b => b.AddedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            // فهرس المفتاح الخارجي العادي + الفهرس الفريد المفلتر للطلبات النشطة
            entity.HasIndex(b => b.SourceId).HasDatabaseName("IX_BorrowRequests_SourceId");
            entity.HasIndex(b => b.SourceId)
                .HasDatabaseName("IX_BorrowRequests_SourceId_Active")
                .HasFilter("Status IN ('Delivered', 'Overdue')")
                .IsUnique();

            entity.HasIndex(b => b.Status);
        });

        // ─── User relationships & Indexes ───
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(u => u.DeletedByUser)
                .WithMany()
                .HasForeignKey(u => u.DeletedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(u => u.Username)
                .HasFilter("IsDeleted = 0")
                .IsUnique();

            entity.HasIndex(u => u.IsDeleted);
        });

        // ─── AuditLog relationships & Indexes ───
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasOne(a => a.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(a => a.ActionDate);
        });

        // ─── AlertNotification relationships & Indexes ───
        modelBuilder.Entity<AlertNotification>(entity =>
        {
            entity.HasOne(n => n.Source)
                .WithMany()
                .HasForeignKey(n => n.SourceId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(n => n.IsRead);
        });

        // ─── GammaLine relationships & Performance indexing ───
        modelBuilder.Entity<GammaLine>(entity =>
        {
            entity.HasOne(g => g.Radioisotope)
                .WithMany(r => r.GammaLines)
                .HasForeignKey(g => g.RadioisotopeId)
                .OnDelete(DeleteBehavior.Cascade);

            // فهرس الأداء للبحث عن الطاقة (ضروري لسرعة البحث في 30,000 سجل)
            entity.HasIndex(g => g.Energy);
        });

        // ─── SourceIsotope relationships ───
        modelBuilder.Entity<SourceIsotope>(entity =>
        {
            entity.HasOne(si => si.Source)
                .WithMany(s => s.SourceIsotopes)
                .HasForeignKey(si => si.SourceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(si => si.Radioisotope)
                .WithMany()
                .HasForeignKey(si => si.RadioisotopeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(si => si.ActivityUnit)
                .WithMany()
                .HasForeignKey(si => si.ActivityUnitId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── SourceLocationHistory relationships ───
        modelBuilder.Entity<SourceLocationHistory>(entity =>
        {
            entity.HasOne(h => h.Source)
                .WithMany(s => s.LocationHistories)
                .HasForeignKey(h => h.SourceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(h => h.Location)
                .WithMany()
                .HasForeignKey(h => h.LocationId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(h => h.PreviousLocation)
                .WithMany()
                .HasForeignKey(h => h.PreviousLocationId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(h => h.SourceId);
            entity.HasIndex(h => h.LocationId);
            entity.HasIndex(h => h.MovedAt);
        });

        // ─── LeakTestRecord relationships & Performance indexing ───
        modelBuilder.Entity<LeakTestRecord>(entity =>
        {
            entity.HasOne(l => l.Source)
                .WithMany(s => s.LeakTestRecords)
                .HasForeignKey(l => l.SourceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(l => l.PerformedByUser)
                .WithMany()
                .HasForeignKey(l => l.PerformedByUserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(l => l.SourceId);
            entity.HasIndex(l => l.NextDueDate);
        });

        // ─── NeutronSourceType & NeutronSource relationships & indexing ───
        modelBuilder.Entity<NeutronSourceType>(entity =>
        {
            entity.HasIndex(t => t.Code)
                .HasDatabaseName("IX_NeutronSourceTypes_Code")
                .HasFilter("IsDeleted = 0")
                .IsUnique();
            entity.HasIndex(t => t.IsDeleted);

            entity.HasOne(t => t.DeletedByUser)
                .WithMany()
                .HasForeignKey(t => t.DeletedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(t => t.AddedByUser)
                .WithMany()
                .HasForeignKey(t => t.AddedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<NeutronSource>(entity =>
        {
            entity.HasOne(n => n.NeutronSourceType)
                .WithMany(t => t.NeutronSources)
                .HasForeignKey(n => n.NeutronSourceTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(n => n.Location)
                .WithMany()
                .HasForeignKey(n => n.LocationId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(n => n.DeletedByUser)
                .WithMany()
                .HasForeignKey(n => n.DeletedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(n => n.AddedByUser)
                .WithMany()
                .HasForeignKey(n => n.AddedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(n => n.SourceCode)
                .HasDatabaseName("IX_NeutronSources_SourceCode")
                .HasFilter("IsDeleted = 0")
                .IsUnique();
            entity.HasIndex(n => n.SerialNumber);
            entity.HasIndex(n => n.LocationId);
            entity.HasIndex(n => n.Status);
            entity.HasIndex(n => n.IsDeleted);
        });

        // ─── SourceCertificate indexing ───
        modelBuilder.Entity<SourceCertificate>(entity =>
        {
            entity.HasIndex(c => c.SourceId);
            entity.HasIndex(c => c.SourceType);
        });

        // ─── Global Query Filters (Soft Delete) ───
        modelBuilder.Entity<Source>().HasQueryFilter(s => !s.IsDeleted);
        modelBuilder.Entity<Location>().HasQueryFilter(l => !l.IsDeleted);
        modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
        modelBuilder.Entity<Radioisotope>().HasQueryFilter(r => !r.IsDeleted);
        modelBuilder.Entity<NeutronSourceType>().HasQueryFilter(t => !t.IsDeleted);
        modelBuilder.Entity<NeutronSource>().HasQueryFilter(n => !n.IsDeleted);
    }
}
