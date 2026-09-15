using Sources.Services;

namespace Sources.Tests.Fakes;

/// <summary>
/// تنفيذ اختباري بسيط لـILicenseService. الحالة الافتراضية IsActivated = true حتى لا تتأثر
/// الاختبارات القائمة (غير المتعلقة بجولة الترخيص) بفحص الترخيص الجديد. تُستعمل حالة
/// IsActivated = false صراحة فقط في اختبارات الوضع التجريبي الجديدة.
/// </summary>
public class FakeLicenseService : ILicenseService
{
    public bool IsActivated { get; set; } = true;

    public (bool Success, string Message) Activate(string serialInput)
    {
        IsActivated = true;
        return (true, "تم التفعيل");
    }
}
