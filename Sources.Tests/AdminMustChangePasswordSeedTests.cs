using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Helpers;
using Sources.Models;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// الجولة 164 (ب): يتحقق من زرع admin بحقل MustChangePassword الصحيح حسب حالة كلمة مروره.
/// </summary>
public class AdminMustChangePasswordSeedTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly DbContextOptions<AppDbContext> _options;

    public AdminMustChangePasswordSeedTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"sources_test_admin_mcp_{Guid.NewGuid():N}.db");
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

    [Fact]
    public void FreshAdmin_IsSeededWithMustChangePasswordTrue()
    {
        // Act
        using (var db = new AppDbContext(_options))
        {
            db.InitializeDatabase();
        }

        // Assert
        using (var db = new AppDbContext(_options))
        {
            var admin = db.Users.First(u => u.Username == "admin");
            Assert.True(admin.MustChangePassword);
        }
    }

    [Fact]
    public void ExistingAdminWithDefaultPassword_ReseedSetsMustChangePasswordTrue()
    {
        // Arrange: seed once, then simulate an older DB where MustChangePassword was never set
        // even though the password is still literally "admin".
        using (var db = new AppDbContext(_options))
        {
            db.InitializeDatabase();
        }

        using (var db = new AppDbContext(_options))
        {
            var admin = db.Users.First(u => u.Username == "admin");
            admin.MustChangePassword = false;
            db.SaveChanges();
        }

        // Act: re-run seeding (simulates next app startup)
        using (var db = new AppDbContext(_options))
        {
            db.InitializeDatabase();
        }

        // Assert
        using (var db = new AppDbContext(_options))
        {
            var admin = db.Users.First(u => u.Username == "admin");
            Assert.True(admin.MustChangePassword);
        }
    }

    [Fact]
    public void ExistingAdminWithChangedPassword_IsNotForcedToChangeAgain()
    {
        // Arrange
        using (var db = new AppDbContext(_options))
        {
            db.InitializeDatabase();
        }

        using (var db = new AppDbContext(_options))
        {
            var admin = db.Users.First(u => u.Username == "admin");
            admin.PasswordHash = PasswordHelper.HashPassword("SomeOtherStrongPassword123!");
            admin.MustChangePassword = false;
            db.SaveChanges();
        }

        // Act: re-run seeding
        using (var db = new AppDbContext(_options))
        {
            db.InitializeDatabase();
        }

        // Assert: admin already changed their password, must not be forced again
        using (var db = new AppDbContext(_options))
        {
            var admin = db.Users.First(u => u.Username == "admin");
            Assert.False(admin.MustChangePassword);
        }
    }

    [Fact]
    public void ExistingAdminAlreadyFlagged_ReseedDoesNotUnsetFlag()
    {
        // Arrange
        using (var db = new AppDbContext(_options))
        {
            db.InitializeDatabase();
        }

        using (var db = new AppDbContext(_options))
        {
            var admin = db.Users.First(u => u.Username == "admin");
            Assert.True(admin.MustChangePassword); // fresh admin already true
        }

        // Act: re-run seeding again
        using (var db = new AppDbContext(_options))
        {
            db.InitializeDatabase();
        }

        // Assert: still true
        using (var db = new AppDbContext(_options))
        {
            var admin = db.Users.First(u => u.Username == "admin");
            Assert.True(admin.MustChangePassword);
        }
    }
}
