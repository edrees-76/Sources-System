using System;
using System.IO;
using Sources.Helpers;
using Xunit;

namespace Sources.Tests;

public class PasswordHelperTests
{
    // مسار ملف السجل نفسه المستخدم داخل LoggerService (Sources.Services.LoggerService)
    private static string LogFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Sources", "Logs", $"sources_{DateTime.Now:yyyy-MM-dd}.log");

    [Fact]
    public void VerifyPassword_WhenHashIsMalformed_ReturnsFalseAndLogsBCryptFailure()
    {
        // قبل الإصلاح: استثناء BCrypt على تجزئة تالفة كان يُبتلع بصمت دون أي أثر تشخيصي؛
        // هذا الاختبار يثبت أن النتيجة ما زالت false، وأن مسار التسجيل أصبح مفعَّلاً فعلياً.
        const string malformedHash = "not-a-valid-bcrypt-hash";

        var beforeLength = File.Exists(LogFilePath) ? new FileInfo(LogFilePath).Length : 0L;

        var result = PasswordHelper.VerifyPassword("any-password", malformedHash);

        Assert.False(result);

        Assert.True(File.Exists(LogFilePath), "Expected LoggerService to have created/updated the log file.");
        var content = File.ReadAllText(LogFilePath);
        Assert.Contains("PasswordHelper: BCrypt verification threw", content);

        // تأكيد أن شيئاً فعلياً كُتب إلى الملف نتيجة هذا الاستدعاء تحديداً (وليس بقايا سجل سابق فقط)
        var afterLength = new FileInfo(LogFilePath).Length;
        Assert.True(afterLength > beforeLength, "Expected the log file to grow after the failed verification.");
    }
}
