using Microsoft.EntityFrameworkCore;
using Sources.Models;
using System;
using System.IO;
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
            // Use LocalAppData so the app doesn't need admin privileges
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Sources");
            Directory.CreateDirectory(appDataDir);
            var dbPath = Path.Combine(appDataDir, "Sources.db");

            // Migrate: if DB exists in old location (beside exe), copy it once
            var oldDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sources.db");
            if (!File.Exists(dbPath) && File.Exists(oldDbPath))
            {
                try { File.Copy(oldDbPath, dbPath); } catch { }
            }

            var connStringBuilder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
                DefaultTimeout = 5
            };

            options.UseSqlite(connStringBuilder.ToString());
        }
    }

    public void InitializeDatabase()
    {
        Database.EnsureCreated();
        MigrateSchema();
        SeedData();
    }

    /// <summary>
    /// ترحيل المخطط: إضافة الأعمدة والجداول الجديدة بدون حذف البيانات
    /// </summary>
    private void MigrateSchema()
    {
        var conn = Database.GetDbConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();

        // تفعيل نمط WAL وضبط مهلة الانتظار 5 ثوانٍ
        cmd.CommandText = "PRAGMA journal_mode = WAL; PRAGMA busy_timeout = 5000;";
        cmd.ExecuteNonQuery();

        // إضافة جدول SourceIsotopes إذا لم يكن موجوداً
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS SourceIsotopes (
                Id TEXT PRIMARY KEY NOT NULL,
                SourceId TEXT NOT NULL,
                RadioisotopeId TEXT NOT NULL,
                InitialActivityValue REAL,
                ActivityUnitId TEXT,
                CurrentActivityValue REAL,
                CalibrationDate TEXT,
                Notes TEXT,
                FOREIGN KEY (SourceId) REFERENCES Sources(Id) ON DELETE CASCADE,
                FOREIGN KEY (RadioisotopeId) REFERENCES Radioisotopes(Id) ON DELETE RESTRICT,
                FOREIGN KEY (ActivityUnitId) REFERENCES ActivityUnits(Id) ON DELETE RESTRICT
            );";
        cmd.ExecuteNonQuery();

        // إضافة عمود HasDetailedIsotopes إلى Sources إذا لم يكن موجوداً
        try
        {
            cmd.CommandText = "ALTER TABLE Sources ADD COLUMN HasDetailedIsotopes INTEGER NOT NULL DEFAULT 0;";
            cmd.ExecuteNonQuery();
        }
        catch { /* العمود موجود بالفعل */ }

        // إضافة عمود ImagePath إلى Sources إذا لم يكن موجوداً
        try
        {
            cmd.CommandText = "ALTER TABLE Sources ADD COLUMN ImagePath TEXT;";
            cmd.ExecuteNonQuery();
        }
        catch { /* العمود موجود بالفعل */ }

        // ─── مرحلة 2: حقول الأمان (RBAC) ───
        try { cmd.CommandText = "ALTER TABLE Users ADD COLUMN FailedLoginAttempts INTEGER NOT NULL DEFAULT 0;"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "ALTER TABLE Users ADD COLUMN LockoutEnd TEXT;"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "ALTER TABLE Users ADD COLUMN LastLoginDate TEXT;"; cmd.ExecuteNonQuery(); } catch { }

        // ─── مرحلة 3: صلاحيات الدور ───
        try { cmd.CommandText = "ALTER TABLE Roles ADD COLUMN Permissions TEXT;"; cmd.ExecuteNonQuery(); } catch { }

        // ─── مرحلة 3b: صلاحيات المستخدم التفصيلية ───
        try { cmd.CommandText = "ALTER TABLE Users ADD COLUMN Permissions TEXT;"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "ALTER TABLE Users ADD COLUMN IsEditor INTEGER NOT NULL DEFAULT 1;"; cmd.ExecuteNonQuery(); } catch { }

        // ─── مرحلة 4: سجل التدقيق المُوسع ───
        try { cmd.CommandText = "ALTER TABLE AuditLogs ADD COLUMN OldValues TEXT;"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "ALTER TABLE AuditLogs ADD COLUMN NewValues TEXT;"; cmd.ExecuteNonQuery(); } catch { }

        // ─── مرحلة 5: جدول التنبيهات الذكية ───
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS AlertNotifications (
                Id TEXT PRIMARY KEY NOT NULL,
                AlertType TEXT NOT NULL,
                Severity TEXT NOT NULL DEFAULT 'Warning',
                Message TEXT NOT NULL,
                SourceId TEXT,
                CreatedAt TEXT NOT NULL,
                IsRead INTEGER NOT NULL DEFAULT 0,
                IsDismissed INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (SourceId) REFERENCES Sources(Id) ON DELETE SET NULL
            );";
        cmd.ExecuteNonQuery();

        // ─── جدول إعدادات النظام ───
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS AppSettings (
                Key TEXT PRIMARY KEY NOT NULL,
                Value TEXT NOT NULL,
                Description TEXT
            );";
        cmd.ExecuteNonQuery();

        // ─── فهارس الأداء ───
        try { cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_Sources_Status ON Sources(Status);"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_Sources_CalibrationDate ON Sources(CalibrationDate);"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_AuditLogs_ActionDate ON AuditLogs(ActionDate);"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_AuditLogs_UserId ON AuditLogs(UserId);"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_AlertNotifications_IsRead ON AlertNotifications(IsRead);"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "DELETE FROM AlertNotifications WHERE AlertType = 'CalibrationDue' OR Message LIKE '%معايرة%';"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "DELETE FROM AppSettings WHERE Key = 'CalibrationThresholdDays';"; cmd.ExecuteNonQuery(); } catch { }

        // ─── مرحلة 7: جدول استعارة المصادر ───
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS BorrowRequests (
                Id TEXT PRIMARY KEY NOT NULL,
                SourceId TEXT NOT NULL,
                BorrowerUserId TEXT,
                ApproverUserId TEXT,
                ReturnedByUserId TEXT,
                Purpose TEXT NOT NULL,
                RequestDate TEXT NOT NULL,
                ExpectedReturnDate TEXT NOT NULL,
                ActualReturnDate TEXT,
                ApprovalDate TEXT,
                DeliveryDate TEXT,
                Status TEXT NOT NULL DEFAULT 'Pending',
                RejectionReason TEXT,
                Notes TEXT,
                FOREIGN KEY (SourceId) REFERENCES Sources(Id) ON DELETE CASCADE,
                FOREIGN KEY (BorrowerUserId) REFERENCES Users(Id) ON DELETE RESTRICT,
                FOREIGN KEY (ApproverUserId) REFERENCES Users(Id) ON DELETE SET NULL,
                FOREIGN KEY (ReturnedByUserId) REFERENCES Users(Id) ON DELETE SET NULL
            );";
        cmd.ExecuteNonQuery();

        try { cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_BorrowRequests_Status ON BorrowRequests(Status);"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_BorrowRequests_SourceId ON BorrowRequests(SourceId);"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS IX_BorrowRequests_SourceId_Active ON BorrowRequests(SourceId) WHERE Status IN ('Delivered', 'Overdue');"; cmd.ExecuteNonQuery(); } catch { }

        // إضافة عمود BorrowerName إذا لم يكن موجوداً
        try { cmd.CommandText = "ALTER TABLE BorrowRequests ADD COLUMN BorrowerName TEXT NOT NULL DEFAULT '';"; cmd.ExecuteNonQuery(); } catch { }


        // ─── مرحلة 6: الحذف الناعم (Soft Delete) وفهارس إضافية ───
        try { cmd.CommandText = "ALTER TABLE Sources ADD COLUMN IsDeleted INTEGER NOT NULL DEFAULT 0;"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "ALTER TABLE Locations ADD COLUMN IsDeleted INTEGER NOT NULL DEFAULT 0;"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "ALTER TABLE Users ADD COLUMN IsDeleted INTEGER NOT NULL DEFAULT 0;"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "ALTER TABLE Radioisotopes ADD COLUMN IsDeleted INTEGER NOT NULL DEFAULT 0;"; cmd.ExecuteNonQuery(); } catch { }
        
        try { cmd.CommandText = "ALTER TABLE Radioisotopes ADD COLUMN EnglishNotes TEXT;"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "ALTER TABLE Radioisotopes ADD COLUMN GammaConstant REAL;"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "ALTER TABLE Sources ADD COLUMN AddedBy TEXT;"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "ALTER TABLE Sources ADD COLUMN DeletedAt TEXT;"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "ALTER TABLE Sources ADD COLUMN DeletedBy TEXT;"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "ALTER TABLE Locations ADD COLUMN DeletedAt TEXT;"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "ALTER TABLE Locations ADD COLUMN DeletedBy TEXT;"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "ALTER TABLE Users ADD COLUMN DeletedAt TEXT;"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "ALTER TABLE Users ADD COLUMN DeletedBy TEXT;"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "ALTER TABLE Radioisotopes ADD COLUMN DeletedAt TEXT;"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "ALTER TABLE Radioisotopes ADD COLUMN DeletedBy TEXT;"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "ALTER TABLE Radioisotopes ADD COLUMN AddedBy TEXT;"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "ALTER TABLE Locations ADD COLUMN AddedBy TEXT;"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "ALTER TABLE BorrowRequests ADD COLUMN AddedBy TEXT;"; cmd.ExecuteNonQuery(); } catch { }

        try { cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_Sources_IsDeleted ON Sources(IsDeleted);"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_Locations_IsDeleted ON Locations(IsDeleted);"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_Users_IsDeleted ON Users(IsDeleted);"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_Radioisotopes_IsDeleted ON Radioisotopes(IsDeleted);"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_Sources_SerialNumber ON Sources(SerialNumber);"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS IX_Locations_LocationName ON Locations(LocationName) WHERE IsDeleted = 0;"; cmd.ExecuteNonQuery(); } catch { }

        // ─── جدول تاريخ تنقلات المصادر بين المواقع ───
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS SourceLocationHistories (
                Id TEXT PRIMARY KEY NOT NULL,
                SourceId TEXT NOT NULL,
                LocationId TEXT,
                PreviousLocationId TEXT,
                MovedAt TEXT NOT NULL,
                FOREIGN KEY (SourceId) REFERENCES Sources(Id) ON DELETE CASCADE,
                FOREIGN KEY (LocationId) REFERENCES Locations(Id) ON DELETE SET NULL,
                FOREIGN KEY (PreviousLocationId) REFERENCES Locations(Id) ON DELETE SET NULL
            );";
        cmd.ExecuteNonQuery();

        try { cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_SourceLocationHistories_SourceId ON SourceLocationHistories(SourceId);"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_SourceLocationHistories_LocationId ON SourceLocationHistories(LocationId);"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_SourceLocationHistories_MovedAt ON SourceLocationHistories(MovedAt);"; cmd.ExecuteNonQuery(); } catch { }

        // ─── مرحلة 8: اختبارات التسرب الدوري والمسح الإشعاعي (Leak/Wipe Tests) ───
        try { cmd.CommandText = "ALTER TABLE Sources ADD COLUMN IsSealed INTEGER NOT NULL DEFAULT 1;"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_Sources_IsSealed ON Sources(IsSealed);"; cmd.ExecuteNonQuery(); } catch { }

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS LeakTestRecords (
                Id TEXT PRIMARY KEY NOT NULL,
                SourceId TEXT NOT NULL,
                TestDate TEXT NOT NULL,
                NextDueDate TEXT NOT NULL,
                Result TEXT NOT NULL DEFAULT 'Pass',
                MeasuredActivityBq REAL,
                PerformedByUserId TEXT,
                InspectorName TEXT,
                CertificateNumber TEXT,
                Notes TEXT,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (SourceId) REFERENCES Sources(Id) ON DELETE CASCADE,
                FOREIGN KEY (PerformedByUserId) REFERENCES Users(Id) ON DELETE SET NULL
            );";
        cmd.ExecuteNonQuery();

        try { cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_LeakTestRecords_SourceId ON LeakTestRecords(SourceId);"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_LeakTestRecords_NextDueDate ON LeakTestRecords(NextDueDate);"; cmd.ExecuteNonQuery(); } catch { }

        // ─── مرحلة 9: المصادر النيترونية والأنواع المرجعية (Neutron Sources) ───
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS NeutronSourceTypes (
                Id TEXT PRIMARY KEY NOT NULL,
                Code TEXT NOT NULL,
                NameEn TEXT NOT NULL,
                NameAr TEXT NOT NULL,
                ReactionType TEXT NOT NULL,
                TargetMaterial TEXT,
                ParentNuclide TEXT,
                HalfLife REAL NOT NULL,
                HalfLifeUnit TEXT NOT NULL DEFAULT 'years',
                AverageNeutronEnergyMeV REAL,
                TypicalNeutronYield REAL,
                Notes TEXT,
                IsDeleted INTEGER NOT NULL DEFAULT 0,
                DeletedAt TEXT,
                DeletedBy TEXT,
                AddedBy TEXT,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (DeletedBy) REFERENCES Users(Id) ON DELETE SET NULL
            );";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS NeutronSources (
                Id TEXT PRIMARY KEY NOT NULL,
                SourceCode TEXT NOT NULL,
                SerialNumber TEXT,
                NeutronSourceTypeId TEXT NOT NULL,
                LocationId TEXT,
                EmissionRate REAL NOT NULL,
                RelativeExpandedUncertaintyPercent REAL,
                CalibrationDate TEXT,
                Status TEXT NOT NULL DEFAULT 'Storage',
                Notes TEXT,
                IsDeleted INTEGER NOT NULL DEFAULT 0,
                DeletedAt TEXT,
                DeletedBy TEXT,
                AddedBy TEXT,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (NeutronSourceTypeId) REFERENCES NeutronSourceTypes(Id) ON DELETE RESTRICT,
                FOREIGN KEY (LocationId) REFERENCES Locations(Id) ON DELETE SET NULL,
                FOREIGN KEY (DeletedBy) REFERENCES Users(Id) ON DELETE SET NULL
            );";
        cmd.ExecuteNonQuery();

        try { cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_NeutronSourceTypes_IsDeleted ON NeutronSourceTypes(IsDeleted);"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS IX_NeutronSourceTypes_Code ON NeutronSourceTypes(Code) WHERE IsDeleted = 0;"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_NeutronSources_IsDeleted ON NeutronSources(IsDeleted);"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_NeutronSources_NeutronSourceTypeId ON NeutronSources(NeutronSourceTypeId);"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_NeutronSources_LocationId ON NeutronSources(LocationId);"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_NeutronSources_Status ON NeutronSources(Status);"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS IX_NeutronSources_SourceCode ON NeutronSources(SourceCode) WHERE IsDeleted = 0;"; cmd.ExecuteNonQuery(); } catch { }

        // ─── مرحلة 10: شهادات ومستندات المصادر (Source Certificates) ───
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS SourceCertificates (
                Id TEXT PRIMARY KEY NOT NULL,
                SourceId TEXT NOT NULL,
                SourceType TEXT NOT NULL DEFAULT 'Standard',
                StoredFileName TEXT NOT NULL,
                OriginalFileName TEXT NOT NULL,
                AttachedAt TEXT NOT NULL,
                AttachedBy TEXT NOT NULL
            );";
        cmd.ExecuteNonQuery();

        try { cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_SourceCertificates_SourceId ON SourceCertificates(SourceId);"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_SourceCertificates_SourceType ON SourceCertificates(SourceType);"; cmd.ExecuteNonQuery(); } catch { }

        conn.Close();
    }

    private void SeedData()
    {
        // ─── وحدات النشاط الإشعاعي ───
        var units = ActivityUnits.ToList();
        var becquerel = units.FirstOrDefault(u => u.UnitName == "Becquerel");
        if (becquerel != null) becquerel.UnitSymbol = "Bq";
        else ActivityUnits.Add(new ActivityUnit { UnitName = "Becquerel", UnitSymbol = "Bq", ConversionToBq = 1, Description = "الوحدة الدولية للنشاط الإشعاعي (SI)" });

        var curie = units.FirstOrDefault(u => u.UnitName == "Curie");
        if (curie != null) curie.UnitSymbol = "Ci";
        else ActivityUnits.Add(new ActivityUnit { UnitName = "Curie", UnitSymbol = "Ci", ConversionToBq = 3.7e10, Description = "1 Ci = 3.7 × 10¹⁰ Bq" });

        var mcurie = units.FirstOrDefault(u => u.UnitName == "Millicurie");
        if (mcurie != null) mcurie.UnitSymbol = "mCi";
        else ActivityUnits.Add(new ActivityUnit { UnitName = "Millicurie", UnitSymbol = "mCi", ConversionToBq = 3.7e7, Description = "1 mCi = 3.7 × 10⁷ Bq" });

        var ucurie = units.FirstOrDefault(u => u.UnitName == "Microcurie");
        if (ucurie != null) ucurie.UnitSymbol = "µCi";
        else ActivityUnits.Add(new ActivityUnit { UnitName = "Microcurie", UnitSymbol = "µCi", ConversionToBq = 3.7e4, Description = "1 µCi = 3.7 × 10⁴ Bq" });

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

        var librarySymbols = isotopeLibraryData.Select(i => i.Symbol).ToList();

        // 1. تحديث أو إضافة النظائر من المكتبة الجديدة
        foreach (var item in isotopeLibraryData)
        {
            var symbol = item.Symbol;
            var existing = Radioisotopes.FirstOrDefault(r => r.Symbol == symbol);
            if (existing != null)
            {
                existing.Name = item.Name;
                existing.ArabicName = item.ArabicName;
                existing.HalfLife = item.HalfLife;
                existing.HalfLifeUnit = item.HalfLifeUnit;
                existing.RadiationType = item.RadiationType;
                existing.Energy = item.Energy;
                existing.Yield = item.Yield;
                existing.Category = item.Category;
                existing.ExemptionLimit = item.ExemptionLimit;
                existing.Notes = item.Notes;
                existing.EnglishNotes = item.EnglishNotes;
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

        // 2. حذف النظائر التي لم تعد موجودة في المكتبة الجديدة (لتنظيف المنظومة)
        var toRemove = Radioisotopes.Where(r => !librarySymbols.Contains(r.Symbol)).ToList();
        if (toRemove.Any())
        {
            Radioisotopes.RemoveRange(toRemove);
        }

        SaveChanges();

        // ─── أنواع المصادر النيترونية المرجعية (10 أنواع معتمدة) ───
        var neutronTypeData = new[]
        {
            new { Code = "Cf-252", NameEn = "Californium-252", NameAr = "كاليفورنيوم-252", ReactionType = "Spontaneous Fission", TargetMaterial = (string?)null, ParentNuclide = "Cf-252", HalfLife = 2.645, HalfLifeUnit = "years", Notes = "انشطار تلقائي مستمر، طيف طاقة بمتوسط ~2.1 MeV، انبعاث عالي." },
            new { Code = "Am-241/Be", NameEn = "Americium-241/Beryllium", NameAr = "أمريسيوم-241 / بيريليوم", ReactionType = "(α,n)", TargetMaterial = (string?)"Be", ParentNuclide = "Am-241", HalfLife = 432.2, HalfLifeUnit = "years", Notes = "الأكثر شيوعاً في التطبيقات الصناعية وسبر الآبار، متوسط طاقة ~4.2 MeV." },
            new { Code = "Pu-239/Be", NameEn = "Plutonium-239/Beryllium", NameAr = "بلوتونيوم-239 / بيريليوم", ReactionType = "(α,n)", TargetMaterial = (string?)"Be", ParentNuclide = "Pu-239", HalfLife = 24110.0, HalfLifeUnit = "years", Notes = "مصدر قياسي مستقر جداً طويل العمر، متوسط طاقة ~4.5 MeV." },
            new { Code = "Pu-238/Be", NameEn = "Plutonium-238/Beryllium", NameAr = "بلوتونيوم-238 / بيريليوم", ReactionType = "(α,n)", TargetMaterial = (string?)"Be", ParentNuclide = "Pu-238", HalfLife = 87.7, HalfLifeUnit = "years", Notes = "انبعاث نيتروني مرتفع بحجم مدمج، متوسط طاقة ~4.5 MeV." },
            new { Code = "Am-241/B", NameEn = "Americium-241/Boron", NameAr = "أمريسيوم-241 / بورون", ReactionType = "(α,n)", TargetMaterial = (string?)"B", ParentNuclide = "Am-241", HalfLife = 432.2, HalfLifeUnit = "years", Notes = "طيف طاقة أقل من Am-Be بمتوسط ~2.8 MeV." },
            new { Code = "Am-241/F", NameEn = "Americium-241/Fluorine", NameAr = "أمريسيوم-241 / فلور", ReactionType = "(α,n)", TargetMaterial = (string?)"F", ParentNuclide = "Am-241", HalfLife = 432.2, HalfLifeUnit = "years", Notes = "طاقة نيترونات متوسطة منخفضة (~1.4 MeV)." },
            new { Code = "Am-241/Li", NameEn = "Americium-241/Lithium", NameAr = "أمريسيوم-241 / ليثيوم", ReactionType = "(α,n)", TargetMaterial = (string?)"Li", ParentNuclide = "Am-241", HalfLife = 432.2, HalfLifeUnit = "years", Notes = "نيترونات شبه حرارية ومنخفضة الطاقة بمتوسط ~0.5 MeV." },
            new { Code = "Ra-226/Be", NameEn = "Radium-226/Beryllium (α,n)", NameAr = "راديوم-226 / بيريليوم (ألفا)", ReactionType = "(α,n)", TargetMaterial = (string?)"Be", ParentNuclide = "Ra-226", HalfLife = 1600.0, HalfLifeUnit = "years", Notes = "مصدر تاريخي قياسي ذو طيف معقد ومستوى غاما مرتفع." },
            new { Code = "Sb-124/Be", NameEn = "Antimony-124/Beryllium", NameAr = "أنتيمون-124 / بيريليوم", ReactionType = "(γ,n)", TargetMaterial = (string?)"Be", ParentNuclide = "Sb-124", HalfLife = 60.2, HalfLifeUnit = "days", Notes = "مصدر نيترونات ضوئية شبه أحادية الطاقة (24 keV)." },
            new { Code = "NBS-1 (Ra-Be)", NameEn = "NBS-1 Standard Radium-Beryllium", NameAr = "معيار NBS-1 راديوم-بيريليوم", ReactionType = "(γ,n)", TargetMaterial = (string?)"Be", ParentNuclide = "Ra-226", HalfLife = 1600.0, HalfLifeUnit = "years", Notes = "المصدر المعياري القومي للمعايرة والتوثيق المرجعي." }
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
                    Notes = item.Notes
                });
            }
        }

        SaveChanges();

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

        // ─── Source relationships ───
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

            entity.HasIndex(s => s.SourceCode).IsUnique();
        });

        // ─── Location relationships & Unique Index ───
        modelBuilder.Entity<Location>(entity =>
        {
            entity.HasOne(l => l.DeletedByUser)
                .WithMany()
                .HasForeignKey(l => l.DeletedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(l => l.LocationName)
                .HasFilter("IsDeleted = 0")
                .IsUnique();
        });

        // ─── Radioisotope relationships ───
        modelBuilder.Entity<Radioisotope>(entity =>
        {
            entity.HasOne(r => r.DeletedByUser)
                .WithMany()
                .HasForeignKey(r => r.DeletedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ─── BorrowRequest relationships ───
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

            entity.HasIndex(b => b.SourceId)
                .HasFilter("Status IN ('Delivered', 'Overdue')")
                .IsUnique();
        });

        // ─── User relationships ───
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
        });

        // ─── AuditLog relationships ───
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasOne(a => a.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull);
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
            entity.HasIndex(t => t.Code);
            entity.HasIndex(t => t.IsDeleted);

            entity.HasOne(t => t.DeletedByUser)
                .WithMany()
                .HasForeignKey(t => t.DeletedBy)
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

            entity.HasIndex(n => n.SourceCode);
            entity.HasIndex(n => n.SerialNumber);
            entity.HasIndex(n => n.LocationId);
            entity.HasIndex(n => n.Status);
            entity.HasIndex(n => n.IsDeleted);
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
