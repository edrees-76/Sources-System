using System;
using System.IO;
using Sources.Services;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// اختبارات وحدة لـLicenseService: التحقق من الرقم التسلسلي، التطبيع، والحفظ المحلي عبر DPAPI.
/// يُستعمل ملف مؤقت في Temp بدل %ProgramData%\Sources\license.dat الحقيقي لعزل الاختبارات
/// عن حالة الجهاز الفعلي وتفادي أي تأثير جانبي على بيئة التشغيل.
/// </summary>
public class LicenseServiceTests : IDisposable
{
    // رقم تسلسلي وهمي لأغراض الاختبار فقط، لا علاقة له بالرقم الإنتاجي الحقيقي.
    private const string ValidSerial = "TEST-ONLY-DUMMY-SERIAL-2026";

    // تجزئة SHA-256 لـValidSerial أعلاه (بعد Trim + ToUpperInvariant، بنفس منطق Activate()).
    private static readonly string[] TestValidHashes = new[]
    {
        "3c474cb53e5f1276720af31f9ae9950b861b849484f2d94bac7dc2af93c59f72"
    };

    private readonly string _tempLicenseFile;

    public LicenseServiceTests()
    {
        _tempLicenseFile = Path.Combine(Path.GetTempPath(), $"license_test_{Guid.NewGuid():N}.dat");
    }

    private LicenseService CreateSut() => new LicenseService(_tempLicenseFile, TestValidHashes);

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempLicenseFile))
                File.Delete(_tempLicenseFile);
        }
        catch { }
    }

    [Fact]
    public void IsActivated_WhenNoStateFileExists_DefaultsToFalse()
    {
        var sut = CreateSut();
        Assert.False(sut.IsActivated);
    }

    [Fact]
    public void Activate_WithValidSerial_ActivatesAndReturnsSuccess()
    {
        var sut = CreateSut();

        var (success, message) = sut.Activate(ValidSerial);

        Assert.True(success);
        Assert.False(string.IsNullOrWhiteSpace(message));
        Assert.True(sut.IsActivated);
    }

    [Fact]
    public void Activate_WithValidSerial_IsCaseInsensitiveAndTrimsWhitespace()
    {
        var sut = CreateSut();

        var (success, _) = sut.Activate("   test-only-dummy-serial-2026   ");

        Assert.True(success);
        Assert.True(sut.IsActivated);
    }

    [Fact]
    public void Activate_WithInvalidSerial_DoesNotActivateAndReturnsClearFailureMessage()
    {
        var sut = CreateSut();

        var (success, message) = sut.Activate("NOT-A-VALID-SERIAL");

        Assert.False(success);
        Assert.False(string.IsNullOrWhiteSpace(message));
        Assert.False(sut.IsActivated);
    }

    [Fact]
    public void Activate_WithEmptySerial_ReturnsFailureWithoutThrowing()
    {
        var sut = CreateSut();

        var (success, message) = sut.Activate("");

        Assert.False(success);
        Assert.False(string.IsNullOrWhiteSpace(message));
        Assert.False(sut.IsActivated);
    }

    [Fact]
    public void Activate_PersistsState_AndFreshInstanceReadingSameFileReportsActivated()
    {
        // ملاحظة: يعتمد هذا الاختبار على توفر DPAPI (ProtectedData) بنطاق LocalMachine في بيئة
        // التشغيل. إن تعذّر ذلك في بيئة CI معزولة عن سياق مستخدم/جهاز حقيقي، سيفشل هذا الاختبار
        // تحديداً بخطأ تشفير بدل نجاح كاذب، وهو سلوك متوقَّع موثَّق هنا صراحة.
        var first = CreateSut();
        var (success, _) = first.Activate(ValidSerial);
        Assert.True(success);
        Assert.True(File.Exists(_tempLicenseFile));

        var second = CreateSut();
        Assert.True(second.IsActivated);
    }
}
