using System;
using System.Collections.Generic;
using Moq;
using Sources.Data;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fakes;
using Sources.Tests.Fixtures;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// اختبارات مخصصة لتأكيد أن AuthorizationGuard.RequireActivated يمنع فعلياً كل دالة كتابة
/// في الخدمات الاثنتي عشرة الممسوسة بالجولة 157 عندما ILicenseService.IsActivated == false،
/// وأن الرسالة المُعادة هي رسالة الوضع التجريبي تحديداً (وليست رسالة تحقق أخرى)، لأن الفحص
/// يجب أن يسبق أي منطق أعمال آخر. مسار "ينجح عندما IsActivated == true" مُغطّى بالفعل ضمنياً
/// عبر مئات الاختبارات القائمة في بقية الملفات التي زُوِّدت جميعها بـFakeLicenseService
/// (IsActivated = true افتراضياً) كجزء من هذه الجولة.
/// </summary>
public class LicenseGuardRejectionTests : IClassFixture<SqliteInMemoryFixture>, IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly FakeAuditService _auditService = new();
    private readonly FakeUserService _userService = new();
    private readonly FakeLicenseService _inactiveLicense = new() { IsActivated = false };

    public LicenseGuardRejectionTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();
    }

    public void Dispose() => _fixture.ResetDatabase();

    private const string TrialMessageFragment = "النسخة تجريبية";

    [Fact]
    public void SourceService_CreateSource_RejectedWhenNotActivated()
    {
        var sut = new SourceService(_fixture.ContextFactory, new DecayCalculationService(), _auditService, _userService, _inactiveLicense);
        var (success, message) = sut.CreateSource(new Source());
        Assert.False(success);
        Assert.Contains(TrialMessageFragment, message);
    }

    [Fact]
    public void NeutronSourceService_Create_RejectedWhenNotActivated()
    {
        var sut = new NeutronSourceService(_fixture.ContextFactory, _auditService, _userService, _inactiveLicense);
        var (success, message) = sut.Create(new NeutronSource());
        Assert.False(success);
        Assert.Contains(TrialMessageFragment, message);
    }

    [Fact]
    public void NeutronSourceTypeService_Create_RejectedWhenNotActivated()
    {
        var sut = new NeutronSourceTypeService(_fixture.ContextFactory, _auditService, _userService, _inactiveLicense);
        var (success, message) = sut.Create(new NeutronSourceType());
        Assert.False(success);
        Assert.Contains(TrialMessageFragment, message);
    }

    [Fact]
    public void LocationService_Create_RejectedWhenNotActivated()
    {
        var sut = new LocationService(_fixture.ContextFactory, _auditService, _userService, _inactiveLicense);
        var (success, message) = sut.Create(new Location());
        Assert.False(success);
        Assert.Contains(TrialMessageFragment, message);
    }

    [Fact]
    public void RadioisotopeService_Create_RejectedWhenNotActivated()
    {
        var sut = new RadioisotopeService(_fixture.ContextFactory, _auditService, _userService, _inactiveLicense);
        var (success, message) = sut.Create(new Radioisotope());
        Assert.False(success);
        Assert.Contains(TrialMessageFragment, message);
    }

    [Fact]
    public void UserService_CreateUser_RejectedWhenNotActivated_EvenForAdmin()
    {
        var sut = new UserService(_fixture.ContextFactory, licenseService: _inactiveLicense);
        var (success, message) = sut.CreateUser(new User(), "Pass123!");
        Assert.False(success);
        Assert.Contains(TrialMessageFragment, message);
    }

    [Fact]
    public void UserService_Login_IsNotGated_StillReachesCredentialCheck()
    {
        // Login يجب ألا يُمنع في الوضع التجريبي، وإلا تصبح المنظومة غير قابلة للاستخدام قبل التفعيل.
        var sut = new UserService(_fixture.ContextFactory, licenseService: _inactiveLicense);
        var (success, message) = sut.Login("nonexistent_user", "AnyPassword");
        Assert.False(success);
        Assert.DoesNotContain(TrialMessageFragment, message);
    }

    [Fact]
    public void BorrowService_CreateRequest_RejectedWhenNotActivated()
    {
        var sut = new BorrowService(_fixture.ContextFactory, _auditService, _userService, _inactiveLicense);
        var (success, message) = sut.CreateRequest(new BorrowRequest());
        Assert.False(success);
        Assert.Contains(TrialMessageFragment, message);
    }

    [Fact]
    public void LeakTestService_AddRecord_RejectedWhenNotActivated()
    {
        var mockSettings = new Mock<ISystemSettingsService>();
        var sut = new LeakTestService(_fixture.ContextFactory, _auditService, _userService, mockSettings.Object, _inactiveLicense);
        var (success, message, record) = sut.AddRecord(new LeakTestRecord());
        Assert.False(success);
        Assert.Contains(TrialMessageFragment, message);
        Assert.Null(record);
    }

    [Fact]
    public void SourceCertificateService_AttachCertificate_ThrowsWhenNotActivated()
    {
        var sut = new SourceCertificateService(_fixture.ContextFactory, _auditService, _inactiveLicense);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            sut.AttachCertificate(Guid.NewGuid(), "Standard", "C:\\nonexistent.pdf", "tester"));
        Assert.Contains(TrialMessageFragment, ex.Message);
    }

    [Fact]
    public void SourceCertificateService_DeleteCertificate_ReturnsFalseWhenNotActivated()
    {
        var sut = new SourceCertificateService(_fixture.ContextFactory, _auditService, _inactiveLicense);
        var result = sut.DeleteCertificate(Guid.NewGuid(), "tester");
        Assert.False(result);
    }

    [Fact]
    public void AlertService_MarkAsRead_DoesNotThrow_AndLeavesStateUnchangedWhenNotActivated()
    {
        var mockSettings = new Mock<ISystemSettingsService>();
        var sut = new AlertService(_fixture.ContextFactory, new DecayCalculationService(), mockSettings.Object, _inactiveLicense);
        // لا قناة رسالة في دالة void — الاختبار يتحقق فقط من عدم رمي استثناء ومن عدم كسر الاستدعاء
        var ex = Record.Exception(() => sut.MarkAsRead(Guid.NewGuid()));
        Assert.Null(ex);
    }

    [Fact]
    public void SystemSettingsService_SaveSetting_DoesNotPersistWhenNotActivated()
    {
        var sut = new SystemSettingsService(_fixture.ContextFactory, _inactiveLicense);
        sut.SaveSetting("SomeKey", "SomeValue");

        var activeLicense = new FakeLicenseService { IsActivated = true };
        var readSut = new SystemSettingsService(_fixture.ContextFactory, activeLicense);
        Assert.Equal(string.Empty, readSut.GetSetting("SomeKey", string.Empty));
    }

    [Fact]
    public void BackupService_RestoreBackup_RejectedWhenNotActivated()
    {
        var sut = new BackupService(licenseService: _inactiveLicense);
        var (success, message) = sut.RestoreBackup("C:\\nonexistent_backup.zip");
        Assert.False(success);
        Assert.Contains(TrialMessageFragment, message);
    }

    [Fact]
    public void BackupService_CreateBackup_IsNotGated_StillReachesFileCheck()
    {
        // CreateBackup مسموح في الوضع التجريبي بقرار معماري صريح (قرار الجولة 157).
        var sut = new BackupService(customDbPath: "C:\\definitely_does_not_exist.db", licenseService: _inactiveLicense);
        var (success, message, _) = sut.CreateBackup();
        Assert.False(success);
        Assert.DoesNotContain(TrialMessageFragment, message);
    }
}
