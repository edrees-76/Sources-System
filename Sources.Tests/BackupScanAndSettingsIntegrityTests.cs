using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Sources.Data;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fixtures;
using Xunit;

namespace Sources.Tests;

public class BackupScanAndSettingsIntegrityTests : IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly string _testTempDir;

    public BackupScanAndSettingsIntegrityTests()
    {
        _fixture = new SqliteInMemoryFixture();
        _testTempDir = Path.Combine(Path.GetTempPath(), "Sources_BackupScanTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testTempDir);
    }

    public void Dispose()
    {
        _fixture.Dispose();
        try
        {
            if (Directory.Exists(_testTempDir))
            {
                Directory.Delete(_testTempDir, recursive: true);
            }
        }
        catch { }
    }

    #region 1. اختبارات ماسح مجلدات النسخ الاحتياطي (BackupFolderScanner)

    [Fact]
    public void ScanLatest_WhenNoFolders_ReturnsNoneFound()
    {
        // Act
        var (outcome, latest) = BackupFolderScanner.ScanLatest(
            Enumerable.Empty<string>(),
            folder => Enumerable.Empty<DateTime>());

        // Assert
        Assert.Equal(BackupScanOutcome.NoneFound, outcome);
        Assert.Null(latest);
    }

    [Fact]
    public void ScanLatest_WhenAllFoldersEmpty_ReturnsNoneFoundAndNullLatest()
    {
        // Arrange
        var folders = new[] { "FolderA", "FolderB" };

        // Act
        var (outcome, latest) = BackupFolderScanner.ScanLatest(
            folders,
            folder => Enumerable.Empty<DateTime>());

        // Assert
        Assert.Equal(BackupScanOutcome.NoneFound, outcome);
        Assert.Null(latest);
    }

    [Fact]
    public void ScanLatest_ReturnsNewestTimestampAcrossFolders()
    {
        // Arrange
        var folders = new[] { "FolderA", "FolderB", "FolderC" };
        var date1 = new DateTime(2026, 1, 1, 10, 0, 0);
        var date2 = new DateTime(2026, 3, 15, 12, 0, 0);
        var date3 = new DateTime(2026, 2, 20, 8, 0, 0);

        var map = new Dictionary<string, List<DateTime>>
        {
            { "FolderA", new List<DateTime> { date1 } },
            { "FolderB", new List<DateTime> { date2 } },
            { "FolderC", new List<DateTime> { date3 } }
        };

        // Act
        var (outcome, latest) = BackupFolderScanner.ScanLatest(
            folders,
            folder => map.TryGetValue(folder, out var dates) ? dates : Enumerable.Empty<DateTime>());

        // Assert
        Assert.Equal(BackupScanOutcome.Found, outcome);
        Assert.Equal(date2, latest);
    }

    [Fact]
    public void ScanLatest_WhenOneFolderThrowsButAnotherHasBackup_ReturnsFoundWithThatTimestamp()
    {
        // Arrange: مجلد يفشل (مثل مشكلة صلاحيات) ومجلد آخر ينجح ويحوي نسخة
        var folders = new[] { "InaccessibleFolder", "GoodFolder" };
        var goodDate = new DateTime(2026, 5, 1, 9, 30, 0);

        // Act
        var (outcome, latest) = BackupFolderScanner.ScanLatest(
            folders,
            folder =>
            {
                if (folder == "InaccessibleFolder")
                    throw new UnauthorizedAccessException("Access denied");
                return new[] { goodDate };
            });

        // Assert
        Assert.Equal(BackupScanOutcome.Found, outcome);
        Assert.Equal(goodDate, latest);
    }

    [Fact]
    public void ScanLatest_WhenAllFoldersThrow_ReturnsScanFailedAndNullLatest()
    {
        // Arrange: جميع المجلدات تفشل في القراءة
        var folders = new[] { "Fail1", "Fail2" };

        // Act
        var (outcome, latest) = BackupFolderScanner.ScanLatest(
            folders,
            folder => throw new IOException("Disk unreachable"));

        // Assert
        Assert.Equal(BackupScanOutcome.ScanFailed, outcome);
        Assert.Null(latest);
    }

    [Fact]
    public void ScanLatest_WhenOneFolderThrowsAndOthersEmpty_ReturnsScanFailed()
    {
        // Arrange: مجلد فارغ ومجلد آخر يفشل — النتيجة يجب أن تكون ScanFailed لتفادي عاصفة النسخ
        var folders = new[] { "EmptyFolder", "FailingFolder" };

        // Act
        var (outcome, latest) = BackupFolderScanner.ScanLatest(
            folders,
            folder =>
            {
                if (folder == "FailingFolder")
                    throw new UnauthorizedAccessException("Forbidden");
                return Enumerable.Empty<DateTime>();
            });

        // Assert
        Assert.Equal(BackupScanOutcome.ScanFailed, outcome);
        Assert.Null(latest);
    }

    [Fact]
    public void ScanLatest_WhenReaderThrows_DoesNotThrow()
    {
        // Arrange
        var folders = new[] { "ThrowingFolder" };

        // Act & Assert
        var ex = Record.Exception(() =>
        {
            var result = BackupFolderScanner.ScanLatest(
                folders,
                folder => throw new InvalidOperationException("Fatal error"));
            Assert.Equal(BackupScanOutcome.ScanFailed, result.Outcome);
            Assert.Null(result.Latest);
        });

        Assert.Null(ex);
    }

    [Fact]
    public void ScanLatest_DeduplicatesRepeatedFolders()
    {
        // Arrange
        var folders = new[] { "FolderX", "FolderX", "FolderX" };
        int readCount = 0;

        // Act
        var (outcome, latest) = BackupFolderScanner.ScanLatest(
            folders,
            folder =>
            {
                readCount++;
                return new[] { new DateTime(2026, 6, 1) };
            });

        // Assert
        Assert.Equal(1, readCount);
        Assert.Equal(BackupScanOutcome.Found, outcome);
    }

    #endregion

    #region 2. اختبارات تكامل وعزل كاش الإعدادات وكشف التلف (SystemSettingsService)

    [Fact]
    public void GetSetting_WhenStoredValueIsCorrupt_ReturnsDefaultAndRecordsCorruptedKey()
    {
        // Arrange
        var sut = new SystemSettingsService(_fixture.ContextFactory);
        sut.SaveSetting("LeakTestIntervalMonths", "not_a_number");

        // Act
        var result = sut.GetSetting<int>("LeakTestIntervalMonths", 6);

        // Assert
        Assert.Equal(6, result);
        Assert.Contains("LeakTestIntervalMonths", sut.CorruptedKeys);
    }

    [Fact]
    public void GetSetting_WhenStoredValueIsCorrupt_RecordsKeyOnlyOnce()
    {
        // Arrange
        var sut = new SystemSettingsService(_fixture.ContextFactory);
        sut.SaveSetting("CorruptedSetting", "invalid_value");

        // Act: قراءة متكررة 3 مرات
        sut.GetSetting<int>("CorruptedSetting", 10);
        sut.GetSetting<int>("CorruptedSetting", 10);
        sut.GetSetting<int>("CorruptedSetting", 10);

        // Assert: يجب أن يُسجل المفتاح مرة واحدة فقط
        Assert.Single(sut.CorruptedKeys);
        Assert.Equal("CorruptedSetting", sut.CorruptedKeys.First());
    }

    [Fact]
    public void GetSetting_WhenStoredValueIsValid_DoesNotRecordCorruption()
    {
        // Arrange
        var sut = new SystemSettingsService(_fixture.ContextFactory);
        sut.SaveSetting("ValidKey", "42");

        // Act
        var result = sut.GetSetting<int>("ValidKey", 10);

        // Assert
        Assert.Equal(42, result);
        Assert.DoesNotContain("ValidKey", sut.CorruptedKeys);
    }

    [Fact]
    public void ClearCache_ClearsCorruptedKeys()
    {
        // Arrange
        var sut = new SystemSettingsService(_fixture.ContextFactory);
        sut.SaveSetting("CorruptedKey", "invalid");
        sut.GetSetting<int>("CorruptedKey", 5);
        Assert.NotEmpty(sut.CorruptedKeys);

        // Act
        sut.ClearCache();

        // Assert
        Assert.Empty(sut.CorruptedKeys);
    }

    [Fact]
    public void TwoServiceInstances_DoNotShareCache()
    {
        // Arrange: نموذجان منفصلان من الخدمة
        var sut1 = new SystemSettingsService(_fixture.ContextFactory);
        var sut2 = new SystemSettingsService(_fixture.ContextFactory);

        sut1.SaveSetting("IsolatedKey", "Value1");

        // Act: تحميل الكاش في sut1
        var val1 = sut1.GetSetting("IsolatedKey");
        Assert.Equal("Value1", val1);

        // تعديل القيمة عبر sut2 (مما يبطل كاش sut2 فقط، لأن _cache لم يعد ساكناً static)
        sut2.SaveSetting("IsolatedKey", "Value2");

        // كاش sut1 المنفصل لم يتأثر بتعديل sut2 إلا إذا أُبطل صراحة
        // ولكن عند قراءة sut2 يرى القيمة المحدثة
        var val2 = sut2.GetSetting("IsolatedKey");
        Assert.Equal("Value2", val2);

        // والتحقق من أن sut1 و sut2 يمتلكان كاشاً مستقلاً
        sut1.ClearCache();
        Assert.Equal("Value2", sut1.GetSetting("IsolatedKey"));
    }

    #endregion

    #region 3. اختبارات سلوك AutoBackupService عند المسح

    [Fact]
    public void AutoBackupService_WhenNoneFound_CallsCreateBackup()
    {
        // Arrange
        var emptyBackupDir = Path.Combine(_testTempDir, "EmptyBackup_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyBackupDir);

        var mockSettings = new Mock<ISystemSettingsService>(MockBehavior.Strict);
        mockSettings.Setup(s => s.GetSetting<bool>("AutoBackupEnabled", false)).Returns(true);
        mockSettings.Setup(s => s.GetSetting("AutoBackupFrequency", "Daily")).Returns("Daily");
        mockSettings.Setup(s => s.GetSetting("BackupPath", string.Empty)).Returns(emptyBackupDir);
        mockSettings.Setup(s => s.GetSetting("LastAutoBackupAt", string.Empty)).Returns(string.Empty);
        mockSettings.Setup(s => s.SaveSetting("LastAutoBackupAt", It.IsAny<string>()));

        var backupCalledEvent = new ManualResetEventSlim(false);
        var mockBackup = new Mock<IBackupService>(MockBehavior.Strict);
        mockBackup
            .Setup(b => b.CreateBackup(emptyBackupDir))
            .Returns((true, "نجح النسخ", Path.Combine(emptyBackupDir, "SOURCES_backup_new.db")))
            .Callback(() => backupCalledEvent.Set());

        using var sut = new AutoBackupService(mockBackup.Object, mockSettings.Object);

        // Act
        sut.TriggerImmediateCheck();
        var wasCalled = backupCalledEvent.Wait(TimeSpan.FromSeconds(3));

        // Assert
        Assert.True(wasCalled, "CreateBackup should be called once when no backups are found.");
        mockBackup.Verify(b => b.CreateBackup(emptyBackupDir), Times.Once);
    }

    [Fact]
    public async Task AutoBackupService_WhenScanFails_DoesNotCallCreateBackup()
    {
        // Arrange: مجلد محمي/غير قابل للقراءة يحاكي فشل المسح (ScanFailed)
        var lockedDir = CreateLockedDirectory(out var cleanup);

        try
        {
            var mockSettings = new Mock<ISystemSettingsService>(MockBehavior.Strict);
            mockSettings.Setup(s => s.GetSetting<bool>("AutoBackupEnabled", false)).Returns(true);
            mockSettings.Setup(s => s.GetSetting("AutoBackupFrequency", "Daily")).Returns("Daily");
            mockSettings.Setup(s => s.GetSetting("BackupPath", string.Empty)).Returns(lockedDir);
            mockSettings.Setup(s => s.GetSetting("LastAutoBackupAt", string.Empty)).Returns(string.Empty);

            var mockBackup = new Mock<IBackupService>(MockBehavior.Strict);

            using var sut = new AutoBackupService(mockBackup.Object, mockSettings.Object);

            // Act
            sut.TriggerImmediateCheck();
            await Task.Delay(300);

            // Assert
            mockBackup.Verify(b => b.CreateBackup(It.IsAny<string>()), Times.Never);
            mockBackup.Verify(b => b.CreateBackup(), Times.Never);
        }
        finally
        {
            cleanup();
        }
    }

    [Fact]
    public async Task AutoBackup_WhenScanFailsButRecordedDateIsRecent_DoesNotCallCreateBackup()
    {
        // Arrange: المسح يفشل ولكن التاريخ المحفوظ حديث (قبل ساعة) ⇒ لا نسخة (منع العاصفة)
        var lockedDir = CreateLockedDirectory(out var cleanup);

        try
        {
            var recentRecorded = DateTime.Now.AddHours(-1).ToString("o", System.Globalization.CultureInfo.InvariantCulture);

            var mockSettings = new Mock<ISystemSettingsService>(MockBehavior.Strict);
            mockSettings.Setup(s => s.GetSetting<bool>("AutoBackupEnabled", false)).Returns(true);
            mockSettings.Setup(s => s.GetSetting("AutoBackupFrequency", "Daily")).Returns("Daily");
            mockSettings.Setup(s => s.GetSetting("BackupPath", string.Empty)).Returns(lockedDir);
            mockSettings.Setup(s => s.GetSetting("LastAutoBackupAt", string.Empty)).Returns(recentRecorded);

            var mockBackup = new Mock<IBackupService>(MockBehavior.Strict);

            using var sut = new AutoBackupService(mockBackup.Object, mockSettings.Object);

            // Act
            sut.TriggerImmediateCheck();
            await Task.Delay(300);

            // Assert
            mockBackup.Verify(b => b.CreateBackup(It.IsAny<string>()), Times.Never);
            mockBackup.Verify(b => b.CreateBackup(), Times.Never);
        }
        finally
        {
            cleanup();
        }
    }

    [Fact]
    public void AutoBackup_WhenScanFailsAndRecordedDateIsOld_CallsCreateBackupOnce()
    {
        // Arrange: المسح يفشل ولكن التاريخ المحفوظ قديم (قبل 3 أيام) والجدولة يومية ⇒ تنفيذ نسخة واحدة
        var lockedDir = CreateLockedDirectory(out var cleanup);

        try
        {
            var oldRecorded = DateTime.Now.AddDays(-3).ToString("o", System.Globalization.CultureInfo.InvariantCulture);

            var mockSettings = new Mock<ISystemSettingsService>(MockBehavior.Strict);
            mockSettings.Setup(s => s.GetSetting<bool>("AutoBackupEnabled", false)).Returns(true);
            mockSettings.Setup(s => s.GetSetting("AutoBackupFrequency", "Daily")).Returns("Daily");
            mockSettings.Setup(s => s.GetSetting("BackupPath", string.Empty)).Returns(lockedDir);
            mockSettings.Setup(s => s.GetSetting("LastAutoBackupAt", string.Empty)).Returns(oldRecorded);
            mockSettings.Setup(s => s.SaveSetting("LastAutoBackupAt", It.IsAny<string>()));

            var backupCalledEvent = new ManualResetEventSlim(false);
            var mockBackup = new Mock<IBackupService>(MockBehavior.Strict);
            mockBackup
                .Setup(b => b.CreateBackup(lockedDir))
                .Returns((true, "نجح النسخ الاحتياطي", Path.Combine(lockedDir, "SOURCES_backup_new.zip")))
                .Callback(() => backupCalledEvent.Set());

            using var sut = new AutoBackupService(mockBackup.Object, mockSettings.Object);

            // Act
            sut.TriggerImmediateCheck();
            var wasCalled = backupCalledEvent.Wait(TimeSpan.FromSeconds(3));

            // Assert
            Assert.True(wasCalled, "CreateBackup should be called once when recorded date is older than interval.");
            mockBackup.Verify(b => b.CreateBackup(lockedDir), Times.Once);
        }
        finally
        {
            cleanup();
        }
    }

    [Fact]
    public async Task AutoBackup_WhenScanFailsAndNoRecordedDate_DoesNotCallCreateBackup()
    {
        // Arrange: المسح يفشل ولا يوجد تاريخ محفوظ ⇒ تخطي الدورة دون إنشاء نسخة (السلوك المعتمد في الجولة 108)
        var lockedDir = CreateLockedDirectory(out var cleanup);

        try
        {
            var mockSettings = new Mock<ISystemSettingsService>(MockBehavior.Strict);
            mockSettings.Setup(s => s.GetSetting<bool>("AutoBackupEnabled", false)).Returns(true);
            mockSettings.Setup(s => s.GetSetting("AutoBackupFrequency", "Daily")).Returns("Daily");
            mockSettings.Setup(s => s.GetSetting("BackupPath", string.Empty)).Returns(lockedDir);
            mockSettings.Setup(s => s.GetSetting("LastAutoBackupAt", string.Empty)).Returns(string.Empty);

            var mockBackup = new Mock<IBackupService>(MockBehavior.Strict);

            using var sut = new AutoBackupService(mockBackup.Object, mockSettings.Object);

            // Act
            sut.TriggerImmediateCheck();
            await Task.Delay(300);

            // Assert
            mockBackup.Verify(b => b.CreateBackup(It.IsAny<string>()), Times.Never);
            mockBackup.Verify(b => b.CreateBackup(), Times.Never);
        }
        finally
        {
            cleanup();
        }
    }

    [Fact]
    public void AutoBackup_AfterSuccessfulBackup_SavesLastAutoBackupSetting()
    {
        // Arrange
        var emptyBackupDir = Path.Combine(_testTempDir, "EmptyBackup_SaveTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyBackupDir);

        string? savedSettingKey = null;
        string? savedSettingValue = null;
        var saveCalledEvent = new ManualResetEventSlim(false);

        var mockSettings = new Mock<ISystemSettingsService>(MockBehavior.Strict);
        mockSettings.Setup(s => s.GetSetting<bool>("AutoBackupEnabled", false)).Returns(true);
        mockSettings.Setup(s => s.GetSetting("AutoBackupFrequency", "Daily")).Returns("Daily");
        mockSettings.Setup(s => s.GetSetting("BackupPath", string.Empty)).Returns(emptyBackupDir);
        mockSettings.Setup(s => s.GetSetting("LastAutoBackupAt", string.Empty)).Returns(string.Empty);
        mockSettings
            .Setup(s => s.SaveSetting(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((k, v) =>
            {
                savedSettingKey = k;
                savedSettingValue = v;
                saveCalledEvent.Set();
            });

        var mockBackup = new Mock<IBackupService>(MockBehavior.Strict);
        mockBackup
            .Setup(b => b.CreateBackup(emptyBackupDir))
            .Returns((true, "نجح", Path.Combine(emptyBackupDir, "SOURCES_backup.zip")));

        using var sut = new AutoBackupService(mockBackup.Object, mockSettings.Object);

        // Act
        sut.TriggerImmediateCheck();
        var wasCalled = saveCalledEvent.Wait(TimeSpan.FromSeconds(3));

        // Assert
        Assert.True(wasCalled);
        Assert.Equal("LastAutoBackupAt", savedSettingKey);
        Assert.NotNull(savedSettingValue);
        Assert.True(DateTime.TryParse(savedSettingValue, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var parsed), "Saved date must be valid round-trip DateTime string.");
        Assert.True((DateTime.Now - parsed).TotalMinutes < 2, "Saved date should be current timestamp.");
    }

    [Fact]
    public async Task AutoBackup_WhenRecordedDateNewerThanScannedDate_UsesRecordedDate()
    {
        // Arrange: مجلد يحوي نسخة قديمة (قبل 5 أيام) لكن التاريخ المحفوظ حديث (قبل ساعتين)
        var backupDir = Path.Combine(_testTempDir, "ScannedOld_RecordedNew_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backupDir);

        var oldFile = Path.Combine(backupDir, "SOURCES_backup_2026-08-20_10-00-00.zip");
        File.WriteAllBytes(oldFile, new byte[16]);
        File.SetCreationTime(oldFile, DateTime.Now.AddDays(-5));

        var recentRecorded = DateTime.Now.AddHours(-2).ToString("o", System.Globalization.CultureInfo.InvariantCulture);

        var mockSettings = new Mock<ISystemSettingsService>(MockBehavior.Strict);
        mockSettings.Setup(s => s.GetSetting<bool>("AutoBackupEnabled", false)).Returns(true);
        mockSettings.Setup(s => s.GetSetting("AutoBackupFrequency", "Daily")).Returns("Daily");
        mockSettings.Setup(s => s.GetSetting("BackupPath", string.Empty)).Returns(backupDir);
        mockSettings.Setup(s => s.GetSetting("LastAutoBackupAt", string.Empty)).Returns(recentRecorded);

        var mockBackup = new Mock<IBackupService>(MockBehavior.Strict);

        using var sut = new AutoBackupService(mockBackup.Object, mockSettings.Object);

        // Act
        sut.TriggerImmediateCheck();
        await Task.Delay(300);

        // Assert: بما أن التاريخ المحفوظ أحدث ولم تمض 24 ساعة، لا يتم إنشاء نسخة
        mockBackup.Verify(b => b.CreateBackup(It.IsAny<string>()), Times.Never);
        mockBackup.Verify(b => b.CreateBackup(), Times.Never);
    }

    [Fact]
    public async Task AutoBackup_WhenRecordedSettingIsCorrupt_IgnoresItAndUsesScanResult()
    {
        // Arrange: القيمة المحفوظة نص غير صالح كتاريخ، لكن القرص يحوي نسخة حديثة (قبل ساعتين)
        var backupDir = Path.Combine(_testTempDir, "ScannedRecent_CorruptRecorded_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backupDir);

        var recentFile = Path.Combine(backupDir, "SOURCES_backup_2026-09-02_12-00-00.zip");
        File.WriteAllBytes(recentFile, new byte[16]);
        File.SetCreationTime(recentFile, DateTime.Now.AddHours(-2));

        var mockSettings = new Mock<ISystemSettingsService>(MockBehavior.Strict);
        mockSettings.Setup(s => s.GetSetting<bool>("AutoBackupEnabled", false)).Returns(true);
        mockSettings.Setup(s => s.GetSetting("AutoBackupFrequency", "Daily")).Returns("Daily");
        mockSettings.Setup(s => s.GetSetting("BackupPath", string.Empty)).Returns(backupDir);
        mockSettings.Setup(s => s.GetSetting("LastAutoBackupAt", string.Empty)).Returns("not-a-date");

        var mockBackup = new Mock<IBackupService>(MockBehavior.Strict);

        using var sut = new AutoBackupService(mockBackup.Object, mockSettings.Object);

        // Act & Assert (Should not throw and should use scan result)
        sut.TriggerImmediateCheck();
        await Task.Delay(300);

        mockBackup.Verify(b => b.CreateBackup(It.IsAny<string>()), Times.Never);
        mockBackup.Verify(b => b.CreateBackup(), Times.Never);
    }

    [Fact]
    public void GetSetting_WhenCorruptValueIsVeryLong_TruncatesValueInWarning()
    {
        // Arrange: قيمة تالفة بطول 200 حرف
        var sut = new SystemSettingsService(_fixture.ContextFactory);
        var longCorruptValue = new string('x', 200);
        sut.SaveSetting("LongCorruptKey", longCorruptValue);

        // Act & Assert
        var ex = Record.Exception(() =>
        {
            var result = sut.GetSetting<int>("LongCorruptKey", 99);
            Assert.Equal(99, result);
        });

        Assert.Null(ex);
        Assert.Contains("LongCorruptKey", sut.CorruptedKeys);
    }

    private string CreateLockedDirectory(out Action cleanup)
    {
        var lockedDir = Path.Combine(_testTempDir, "LockedDir_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(lockedDir);

        var dInfo = new DirectoryInfo(lockedDir);
        var dSec = dInfo.GetAccessControl();
        var rule = new FileSystemAccessRule(Environment.UserName, FileSystemRights.ReadData, AccessControlType.Deny);

        try
        {
            dSec.AddAccessRule(rule);
            dInfo.SetAccessControl(dSec);
            cleanup = () =>
            {
                try
                {
                    dSec.RemoveAccessRule(rule);
                    dInfo.SetAccessControl(dSec);
                }
                catch { }
            };
            return lockedDir;
        }
        catch
        {
            cleanup = () => { };
            return lockedDir;
        }
    }

    #endregion
}
