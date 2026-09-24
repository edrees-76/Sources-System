using System;
using System.IO;
using System.Runtime.CompilerServices;
using Sources.Data;
using Sources.Helpers;

namespace Sources.Tests;

internal static class TestModuleInitializer
{
    [ModuleInitializer]
    internal static void EnsureDialogTestMode()
    {
        // مصدر الحقيقة الوحيد لوضع الاختبار: يُثبَّت مرة عند تحميل وحدة تجميعة الاختبار،
        // قبل أي fixture أو اختبار، فلا يُطفأ العلم ولا يُسرَّب بين الفئات المتسلسلة.
        DialogHelper.IsTestMode = true;

        // عزل بيانات الاختبار (الجولة 199): توجيه كل مسار مشتق من DatabasePaths.AppDataDirectory
        // (القاعدة، الشهادات، النسخ الاحتياطي، السجلات، الإعدادات) إلى مجلد مؤقت فريد لهذا التشغيل،
        // قبل أول استخدام لأي منها، حتى لا يمس أي اختبار مجلد %LOCALAPPDATA%\Sources الحقيقي.
        var testRoot = Path.Combine(Path.GetTempPath(), "Sources_TestRun_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        DatabasePaths.AppDataDirectoryOverride = testRoot;

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            // أفضل محاولة فقط: كود اختبار، وفشل الحذف هنا لا يجب أن يوقف إنهاء العملية.
            try { Directory.Delete(testRoot, recursive: true); } catch { }
        };
    }
}
