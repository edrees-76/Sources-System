using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Sources.Services;
using Xunit;

namespace Sources.Tests;

public class AutoBackupServiceTests : IDisposable
{
    private readonly Mock<IBackupService> _mockBackupService;
    private readonly Mock<ISystemSettingsService> _mockSettingsService;
    private readonly string _testTempDir;

    public AutoBackupServiceTests()
    {
        _mockBackupService = new Mock<IBackupService>(MockBehavior.Strict);
        _mockSettingsService = new Mock<ISystemSettingsService>(MockBehavior.Strict);

        _mockSettingsService
            .Setup(s => s.GetSetting("LastAutoBackupAt", string.Empty))
            .Returns(string.Empty);
        _mockSettingsService
            .Setup(s => s.SaveSetting("LastAutoBackupAt", It.IsAny<string>()));

        _testTempDir = Path.Combine(Path.GetTempPath(), "Sources_AutoBackupTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testTempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testTempDir))
            {
                Directory.Delete(_testTempDir, recursive: true);
            }
        }
        catch
        {
            // Ignore temp folder cleanup errors
        }
    }

    [Fact]
    public async Task TriggerImmediateCheck_WhenAutoBackupDisabled_DoesNotCallCreateBackup()
    {
        // Arrange
        _mockSettingsService
            .Setup(s => s.GetSetting<bool>("AutoBackupEnabled", false))
            .Returns(false);

        using var sut = new AutoBackupService(_mockBackupService.Object, _mockSettingsService.Object);

        // Act
        sut.TriggerImmediateCheck();
        await Task.Delay(250);

        // Assert
        _mockBackupService.Verify(b => b.CreateBackup(), Times.Never);
        _mockBackupService.Verify(b => b.CreateBackup(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("Daily")]
    [InlineData("Weekly")]
    [InlineData("Monthly")]
    public void TriggerImmediateCheck_WhenNoPreviousBackupExists_CreatesBackupImmediately(string frequency)
    {
        // Arrange
        var emptyBackupDir = Path.Combine(_testTempDir, "Empty_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyBackupDir);

        _mockSettingsService
            .Setup(s => s.GetSetting<bool>("AutoBackupEnabled", false))
            .Returns(true);
        _mockSettingsService
            .Setup(s => s.GetSetting("AutoBackupFrequency", "Daily"))
            .Returns(frequency);
        _mockSettingsService
            .Setup(s => s.GetSetting("BackupPath", string.Empty))
            .Returns(emptyBackupDir);

        var backupCalledEvent = new ManualResetEventSlim(false);
        _mockBackupService
            .Setup(b => b.CreateBackup(emptyBackupDir))
            .Returns((true, "نجح النسخ", Path.Combine(emptyBackupDir, "SOURCES_backup.db")))
            .Callback(() => backupCalledEvent.Set());

        using var sut = new AutoBackupService(_mockBackupService.Object, _mockSettingsService.Object);

        // Act
        sut.TriggerImmediateCheck();
        var wasCalled = backupCalledEvent.Wait(TimeSpan.FromSeconds(3));

        // Assert
        Assert.True(wasCalled, $"CreateBackup should be called immediately when no backup exists for {frequency} frequency.");
        _mockBackupService.Verify(b => b.CreateBackup(emptyBackupDir), Times.Once);
    }

    [Fact]
    public void TriggerImmediateCheck_WhenNoBackupPathConfiguredAndNoPreviousBackup_CallsDefaultCreateBackup()
    {
        // Arrange
        _mockSettingsService
            .Setup(s => s.GetSetting<bool>("AutoBackupEnabled", false))
            .Returns(true);
        _mockSettingsService
            .Setup(s => s.GetSetting("AutoBackupFrequency", "Daily"))
            .Returns("Daily");
        _mockSettingsService
            .Setup(s => s.GetSetting("BackupPath", string.Empty))
            .Returns(string.Empty);

        var backupCalledEvent = new ManualResetEventSlim(false);
        _mockBackupService
            .Setup(b => b.CreateBackup())
            .Returns((true, "نجح النسخ الافتراضي", "default_backup.db"))
            .Callback(() => backupCalledEvent.Set());

        using var sut = new AutoBackupService(_mockBackupService.Object, _mockSettingsService.Object);

        // Act
        sut.TriggerImmediateCheck();
        var wasCalled = backupCalledEvent.Wait(TimeSpan.FromSeconds(3));

        // Assert
        Assert.True(wasCalled, "CreateBackup() with no parameters should be called when BackupPath is empty.");
        _mockBackupService.Verify(b => b.CreateBackup(), Times.Once);
    }

    [Fact]
    public async Task TriggerImmediateCheck_DailyFrequency_JustBeforeDue_DoesNotCreateBackup()
    {
        // Arrange - Last backup was 23 hours ago (interval is 24 hours -> not due yet)
        var backupDir = Path.Combine(_testTempDir, "Daily_BeforeDue");
        Directory.CreateDirectory(backupDir);

        var dummyBackup = Path.Combine(backupDir, "SOURCES_backup_2026-08-15_16-00-00.db");
        File.WriteAllBytes(dummyBackup, new byte[32]);
        File.SetCreationTime(dummyBackup, DateTime.Now.AddHours(-23));

        _mockSettingsService
            .Setup(s => s.GetSetting<bool>("AutoBackupEnabled", false))
            .Returns(true);
        _mockSettingsService
            .Setup(s => s.GetSetting("AutoBackupFrequency", "Daily"))
            .Returns("Daily");
        _mockSettingsService
            .Setup(s => s.GetSetting("BackupPath", string.Empty))
            .Returns(backupDir);

        using var sut = new AutoBackupService(_mockBackupService.Object, _mockSettingsService.Object);

        // Act
        sut.TriggerImmediateCheck();
        await Task.Delay(250);

        // Assert
        _mockBackupService.Verify(b => b.CreateBackup(It.IsAny<string>()), Times.Never);
        _mockBackupService.Verify(b => b.CreateBackup(), Times.Never);
    }

    [Fact]
    public void TriggerImmediateCheck_DailyFrequency_JustAfterDue_CreatesBackup()
    {
        // Arrange - Last backup was 25 hours ago (interval is 24 hours -> due now)
        var backupDir = Path.Combine(_testTempDir, "Daily_AfterDue");
        Directory.CreateDirectory(backupDir);

        var dummyBackup = Path.Combine(backupDir, "SOURCES_backup_2026-08-15_14-00-00.db");
        File.WriteAllBytes(dummyBackup, new byte[32]);
        File.SetCreationTime(dummyBackup, DateTime.Now.AddHours(-25));

        _mockSettingsService
            .Setup(s => s.GetSetting<bool>("AutoBackupEnabled", false))
            .Returns(true);
        _mockSettingsService
            .Setup(s => s.GetSetting("AutoBackupFrequency", "Daily"))
            .Returns("Daily");
        _mockSettingsService
            .Setup(s => s.GetSetting("BackupPath", string.Empty))
            .Returns(backupDir);

        var backupCalledEvent = new ManualResetEventSlim(false);
        _mockBackupService
            .Setup(b => b.CreateBackup(backupDir))
            .Returns((true, "نجح النسخ اليومي", Path.Combine(backupDir, "SOURCES_backup_new.db")))
            .Callback(() => backupCalledEvent.Set());

        using var sut = new AutoBackupService(_mockBackupService.Object, _mockSettingsService.Object);

        // Act
        sut.TriggerImmediateCheck();
        var wasCalled = backupCalledEvent.Wait(TimeSpan.FromSeconds(3));

        // Assert
        Assert.True(wasCalled, "Daily backup should be created when 25 hours elapsed since last backup.");
        _mockBackupService.Verify(b => b.CreateBackup(backupDir), Times.Once);
    }

    [Fact]
    public async Task TriggerImmediateCheck_WeeklyFrequency_JustBeforeDue_DoesNotCreateBackup()
    {
        // Arrange - Last backup was 6.8 days ago (interval is 7 days -> not due yet)
        var backupDir = Path.Combine(_testTempDir, "Weekly_BeforeDue");
        Directory.CreateDirectory(backupDir);

        var dummyBackup = Path.Combine(backupDir, "SOURCES_backup_prev_weekly.db");
        File.WriteAllBytes(dummyBackup, new byte[32]);
        File.SetCreationTime(dummyBackup, DateTime.Now.AddDays(-6.8));

        _mockSettingsService
            .Setup(s => s.GetSetting<bool>("AutoBackupEnabled", false))
            .Returns(true);
        _mockSettingsService
            .Setup(s => s.GetSetting("AutoBackupFrequency", "Daily"))
            .Returns("Weekly");
        _mockSettingsService
            .Setup(s => s.GetSetting("BackupPath", string.Empty))
            .Returns(backupDir);

        using var sut = new AutoBackupService(_mockBackupService.Object, _mockSettingsService.Object);

        // Act
        sut.TriggerImmediateCheck();
        await Task.Delay(250);

        // Assert
        _mockBackupService.Verify(b => b.CreateBackup(It.IsAny<string>()), Times.Never);
        _mockBackupService.Verify(b => b.CreateBackup(), Times.Never);
    }

    [Fact]
    public void TriggerImmediateCheck_WeeklyFrequency_JustAfterDue_CreatesBackup()
    {
        // Arrange - Last backup was 7.2 days ago (interval is 7 days -> due now)
        var backupDir = Path.Combine(_testTempDir, "Weekly_AfterDue");
        Directory.CreateDirectory(backupDir);

        var dummyBackup = Path.Combine(backupDir, "SOURCES_backup_prev_weekly.db");
        File.WriteAllBytes(dummyBackup, new byte[32]);
        File.SetCreationTime(dummyBackup, DateTime.Now.AddDays(-7.2));

        _mockSettingsService
            .Setup(s => s.GetSetting<bool>("AutoBackupEnabled", false))
            .Returns(true);
        _mockSettingsService
            .Setup(s => s.GetSetting("AutoBackupFrequency", "Daily"))
            .Returns("Weekly");
        _mockSettingsService
            .Setup(s => s.GetSetting("BackupPath", string.Empty))
            .Returns(backupDir);

        var backupCalledEvent = new ManualResetEventSlim(false);
        _mockBackupService
            .Setup(b => b.CreateBackup(backupDir))
            .Returns((true, "نجح النسخ الأسبوعي", Path.Combine(backupDir, "SOURCES_backup_new_weekly.db")))
            .Callback(() => backupCalledEvent.Set());

        using var sut = new AutoBackupService(_mockBackupService.Object, _mockSettingsService.Object);

        // Act
        sut.TriggerImmediateCheck();
        var wasCalled = backupCalledEvent.Wait(TimeSpan.FromSeconds(3));

        // Assert
        Assert.True(wasCalled, "Weekly backup should be created when 7.2 days elapsed since last backup.");
        _mockBackupService.Verify(b => b.CreateBackup(backupDir), Times.Once);
    }

    [Fact]
    public async Task TriggerImmediateCheck_MonthlyFrequency_JustBeforeDue_DoesNotCreateBackup()
    {
        // Arrange - Last backup was 29 days ago (interval is 30 days -> not due yet)
        var backupDir = Path.Combine(_testTempDir, "Monthly_BeforeDue");
        Directory.CreateDirectory(backupDir);

        var dummyBackup = Path.Combine(backupDir, "SOURCES_backup_prev_monthly.db");
        File.WriteAllBytes(dummyBackup, new byte[32]);
        File.SetCreationTime(dummyBackup, DateTime.Now.AddDays(-29));

        _mockSettingsService
            .Setup(s => s.GetSetting<bool>("AutoBackupEnabled", false))
            .Returns(true);
        _mockSettingsService
            .Setup(s => s.GetSetting("AutoBackupFrequency", "Daily"))
            .Returns("Monthly");
        _mockSettingsService
            .Setup(s => s.GetSetting("BackupPath", string.Empty))
            .Returns(backupDir);

        using var sut = new AutoBackupService(_mockBackupService.Object, _mockSettingsService.Object);

        // Act
        sut.TriggerImmediateCheck();
        await Task.Delay(250);

        // Assert
        _mockBackupService.Verify(b => b.CreateBackup(It.IsAny<string>()), Times.Never);
        _mockBackupService.Verify(b => b.CreateBackup(), Times.Never);
    }

    [Fact]
    public void TriggerImmediateCheck_MonthlyFrequency_JustAfterDue_CreatesBackup()
    {
        // Arrange - Last backup was 31 days ago (interval is 30 days -> due now)
        var backupDir = Path.Combine(_testTempDir, "Monthly_AfterDue");
        Directory.CreateDirectory(backupDir);

        var dummyBackup = Path.Combine(backupDir, "SOURCES_backup_prev_monthly.db");
        File.WriteAllBytes(dummyBackup, new byte[32]);
        File.SetCreationTime(dummyBackup, DateTime.Now.AddDays(-31));

        _mockSettingsService
            .Setup(s => s.GetSetting<bool>("AutoBackupEnabled", false))
            .Returns(true);
        _mockSettingsService
            .Setup(s => s.GetSetting("AutoBackupFrequency", "Daily"))
            .Returns("Monthly");
        _mockSettingsService
            .Setup(s => s.GetSetting("BackupPath", string.Empty))
            .Returns(backupDir);

        var backupCalledEvent = new ManualResetEventSlim(false);
        _mockBackupService
            .Setup(b => b.CreateBackup(backupDir))
            .Returns((true, "نجح النسخ الشهري", Path.Combine(backupDir, "SOURCES_backup_new_monthly.db")))
            .Callback(() => backupCalledEvent.Set());

        using var sut = new AutoBackupService(_mockBackupService.Object, _mockSettingsService.Object);

        // Act
        sut.TriggerImmediateCheck();
        var wasCalled = backupCalledEvent.Wait(TimeSpan.FromSeconds(3));

        // Assert
        Assert.True(wasCalled, "Monthly backup should be created when 31 days elapsed since last backup.");
        _mockBackupService.Verify(b => b.CreateBackup(backupDir), Times.Once);
    }

    [Fact]
    public void TriggerImmediateCheck_ConcurrencyGuard_PreventsMultipleSimultaneousExecutions()
    {
        // Arrange
        var backupDir = Path.Combine(_testTempDir, "ConcurrencyTest");
        Directory.CreateDirectory(backupDir);

        _mockSettingsService
            .Setup(s => s.GetSetting<bool>("AutoBackupEnabled", false))
            .Returns(true);
        _mockSettingsService
            .Setup(s => s.GetSetting("AutoBackupFrequency", "Daily"))
            .Returns("Daily");
        _mockSettingsService
            .Setup(s => s.GetSetting("BackupPath", string.Empty))
            .Returns(backupDir);

        var insideBackupSignal = new ManualResetEventSlim(false);
        var proceedSignal = new ManualResetEventSlim(false);
        int invocationCount = 0;

        _mockBackupService
            .Setup(b => b.CreateBackup(backupDir))
            .Returns(() =>
            {
                Interlocked.Increment(ref invocationCount);
                insideBackupSignal.Set();
                proceedSignal.Wait(TimeSpan.FromSeconds(3));
                return (true, "تم النسخ", Path.Combine(backupDir, "SOURCES_backup_concurrency.db"));
            });

        using var sut = new AutoBackupService(_mockBackupService.Object, _mockSettingsService.Object);

        // Act - Trigger first check
        sut.TriggerImmediateCheck();

        // Wait until first check has entered CreateBackup and set _isChecking = true
        var reachedInside = insideBackupSignal.Wait(TimeSpan.FromSeconds(3));
        Assert.True(reachedInside, "First check should enter CreateBackup.");

        // Trigger second check while first check is still executing
        sut.TriggerImmediateCheck();
        Thread.Sleep(100);

        // Allow first check to complete
        proceedSignal.Set();
        Thread.Sleep(150);

        // Assert - CreateBackup should have been called only once
        Assert.Equal(1, invocationCount);
        _mockBackupService.Verify(b => b.CreateBackup(backupDir), Times.Once);
    }

    [Fact]
    public void BackupCompleted_EventFired_WhenBackupSucceeds()
    {
        // Arrange
        var backupDir = Path.Combine(_testTempDir, "EventSuccessTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backupDir);

        _mockSettingsService
            .Setup(s => s.GetSetting<bool>("AutoBackupEnabled", false))
            .Returns(true);
        _mockSettingsService
            .Setup(s => s.GetSetting("AutoBackupFrequency", "Daily"))
            .Returns("Daily");
        _mockSettingsService
            .Setup(s => s.GetSetting("BackupPath", string.Empty))
            .Returns(backupDir);

        _mockBackupService
            .Setup(b => b.CreateBackup(backupDir))
            .Returns((true, "نجاح", Path.Combine(backupDir, "SOURCES_backup_evt.db")));

        using var sut = new AutoBackupService(_mockBackupService.Object, _mockSettingsService.Object);

        var eventFiredSignal = new ManualResetEventSlim(false);
        sut.BackupCompleted += (sender, args) => eventFiredSignal.Set();

        // Act
        sut.TriggerImmediateCheck();
        var wasFired = eventFiredSignal.Wait(TimeSpan.FromSeconds(10));

        // Assert
        Assert.True(wasFired, "BackupCompleted event must be raised upon successful backup.");
    }

    [Fact]
    public async Task BackupCompleted_EventNotFired_WhenBackupFails()
    {
        // Arrange
        var backupDir = Path.Combine(_testTempDir, "EventFailureTest");
        Directory.CreateDirectory(backupDir);

        _mockSettingsService
            .Setup(s => s.GetSetting<bool>("AutoBackupEnabled", false))
            .Returns(true);
        _mockSettingsService
            .Setup(s => s.GetSetting("AutoBackupFrequency", "Daily"))
            .Returns("Daily");
        _mockSettingsService
            .Setup(s => s.GetSetting("BackupPath", string.Empty))
            .Returns(backupDir);

        _mockBackupService
            .Setup(b => b.CreateBackup(backupDir))
            .Returns((false, "فشل إنشاء النسخة الاحتياطية", null));

        using var sut = new AutoBackupService(_mockBackupService.Object, _mockSettingsService.Object);

        bool eventFired = false;
        sut.BackupCompleted += (sender, args) => eventFired = true;

        // Act
        sut.TriggerImmediateCheck();
        await Task.Delay(250);

        // Assert
        Assert.False(eventFired, "BackupCompleted event must NOT be raised when CreateBackup fails.");
    }

    [Fact]
    public void StartAndStop_Lifecycle_ExecutesCleanlyWithoutExceptions()
    {
        // Arrange
        using var sut = new AutoBackupService(_mockBackupService.Object, _mockSettingsService.Object);

        // Act & Assert (Should not throw)
        var exStart = Record.Exception(() => sut.Start());
        Assert.Null(exStart);

        var exStop = Record.Exception(() => sut.Stop());
        Assert.Null(exStop);
    }

    [Fact]
    public void Stop_MultipleTimesAndBeforeStart_DoesNotThrowException()
    {
        // Arrange
        using var sut = new AutoBackupService(_mockBackupService.Object, _mockSettingsService.Object);

        // Act & Assert - Stop before Start
        var ex1 = Record.Exception(() => sut.Stop());
        Assert.Null(ex1);

        // Act & Assert - Multiple consecutive Stop calls
        var ex2 = Record.Exception(() => sut.Stop());
        Assert.Null(ex2);

        // Act & Assert - Dispose multiple times
        var ex3 = Record.Exception(() => sut.Dispose());
        Assert.Null(ex3);
    }

    [Fact]
    public void Start_CalledMultipleTimes_DoesNotThrowException()
    {
        // Arrange
        using var sut = new AutoBackupService(_mockBackupService.Object, _mockSettingsService.Object);

        // Act & Assert
        var ex1 = Record.Exception(() => sut.Start());
        Assert.Null(ex1);

        var ex2 = Record.Exception(() => sut.Start());
        Assert.Null(ex2);

        sut.Stop();
    }
}
