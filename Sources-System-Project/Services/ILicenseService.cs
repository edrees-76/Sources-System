namespace Sources.Services;

/// <summary>
/// خدمة الترخيص والوضع التجريبي. تُحدِّد ما إذا كانت المنظومة مُفعَّلة برقم تسلسلي صحيح،
/// وتحفظ علامة التفعيل محلياً بعد النجاح حتى لا يُطلَب الرقم مرة أخرى بعد إعادة التشغيل.
/// لا يوجد ربط بمعرِّف جهاز — التحقق عبر تجزئة (Hash) رقم تسلسلي ثابت فقط.
/// </summary>
public interface ILicenseService
{
    /// <summary>هل المنظومة مُفعَّلة حالياً (وفق آخر حالة معروفة عند بدء التشغيل أو بعد تفعيل ناجح)؟</summary>
    bool IsActivated { get; }

    /// <summary>يحاول تفعيل المنظومة برقم تسلسلي. عند النجاح تُحفَظ العلامة محلياً وتصبح IsActivated = true فوراً.</summary>
    (bool Success, string Message) Activate(string serialInput);
}
