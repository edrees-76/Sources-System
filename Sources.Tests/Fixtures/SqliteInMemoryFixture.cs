using System;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sources.Data;

namespace Sources.Tests.Fixtures;

public class SqliteInMemoryFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    public DbContextOptions<AppDbContext> Options { get; }
    public IDbContextFactory<AppDbContext> ContextFactory { get; }

    public SqliteInMemoryFixture()
    {
        Sources.Helpers.DialogHelper.IsTestMode = true;
        // استخدام اتصال In-Memory مفتوح طوال فترة عمل الـ Fixture
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        // تفعيل قيود المفاتيح الأجنبية في SQLite
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = ON;";
            command.ExecuteNonQuery();
        }

        Options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .EnableSensitiveDataLogging()
            .Options;

        ContextFactory = new TestDbContextFactory(Options);

        // إنشاء الجداول والمخطط في الذاكرة
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public AppDbContext CreateContext() => new AppDbContext(Options);

    private static readonly object _dbLock = new();

    /// <summary>
    /// إعادة تهيئة قاعدة البيانات بالكامل للحصول على حالة نظيفة لكل اختبار
    /// </summary>
    public void ResetDatabase()
    {
        lock (_dbLock)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                PRAGMA foreign_keys = OFF;
                DELETE FROM BorrowRequests;
                DELETE FROM SourceLocationHistories;
                DELETE FROM SourceIsotopes;
                DELETE FROM GammaLines;
                DELETE FROM LeakTestRecords;
                DELETE FROM SourceCertificates;
                DELETE FROM Sources;
                DELETE FROM NeutronSources;
                DELETE FROM NeutronSourceTypes;
                DELETE FROM ActivityUnits;
                DELETE FROM Locations;
                DELETE FROM Radioisotopes;
                DELETE FROM AuditLogs;
                DELETE FROM AlertNotifications;
                DELETE FROM AppSettings;
                DELETE FROM Users;
                DELETE FROM Roles;
                PRAGMA foreign_keys = ON;
            ";
            cmd.ExecuteNonQuery();
        }
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}

public class TestDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDbContextFactory(DbContextOptions<AppDbContext> options)
    {
        _options = options;
    }

    public AppDbContext CreateDbContext()
    {
        return new AppDbContext(_options);
    }
}
