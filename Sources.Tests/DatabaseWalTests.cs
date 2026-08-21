using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Xunit;

namespace Sources.Tests;

public class DatabaseWalTests : IDisposable
{
    private readonly string _tempDbPath;

    public DatabaseWalTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"sources_test_wal_{Guid.NewGuid():N}.db");
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

    [Fact]
    public void InitializeDatabase_EnablesWalModeAndBusyTimeout()
    {
        // Arrange
        var connStr = new SqliteConnectionStringBuilder
        {
            DataSource = _tempDbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            DefaultTimeout = 5
        }.ToString();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connStr)
            .Options;

        using (var db = new AppDbContext(options))
        {
            // Act
            db.InitializeDatabase();

            // Assert
            var conn = db.Database.GetDbConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "PRAGMA journal_mode;";
            var journalMode = cmd.ExecuteScalar()?.ToString();

            cmd.CommandText = "PRAGMA busy_timeout;";
            var busyTimeout = Convert.ToInt32(cmd.ExecuteScalar());

            conn.Close();

            Assert.Equal("wal", journalMode?.ToLower());
            Assert.Equal(5000, busyTimeout);
        }
    }

    [Fact]
    public void RealProductionDatabase_HasWalModeConfigured()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sources");
        var realDbPath = Path.Combine(appDataDir, "Sources.db");

        if (File.Exists(realDbPath))
        {
            using var conn = new SqliteConnection($"Data Source={realDbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "PRAGMA journal_mode;";
            var journalMode = cmd.ExecuteScalar()?.ToString();

            cmd.CommandText = "PRAGMA busy_timeout;";
            var busyTimeout = Convert.ToInt32(cmd.ExecuteScalar());

            conn.Close();

            Assert.Equal("wal", journalMode?.ToLower());
        }
    }
}
