using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Sources.Data;
using Sources.Helpers;

namespace Sources.Services;

/// <summary>
/// تنفيذ ILicenseService. التحقق عبر مقارنة تجزئة SHA-256 للرقم المُدخَل (بعد تطبيع بسيط:
/// حذف الفراغات الطرفية، وتحويل الأحرف لحالة كبيرة Uppercase لعدم حساسية حالة الأحرف)
/// بقائمة تجزئات ثابتة مُضمَّنة في الكود — لا يُخزَّن الرقم الصريح كنص في الكود المصدري إطلاقاً.
///
/// ملاحظة هامة لإدريس: الرقم التسلسلي المُضمَّن حالياً "SOURCES-2026-TRIAL-ACTIVATE" هو رقم
/// عنصر نائب (placeholder) لأغراض هذه الجولة فقط. يجب استبدال/إضافة تجزئات الأرقام الحقيقية
/// في _validHashes قبل أي إصدار نهائي (Shipping) — لا تُعامَل هذه القيمة كنهائية.
///
/// عند بدء التطبيق تُقرأ علامة التفعيل من %ProgramData%\Sources\license.dat (مشفَّرة عبر DPAPI
/// بنطاق الجهاز LocalMachine). غياب الملف أو تلفه أو فشل فك التشفير = وضع تجريبي (IsActivated = false)
/// دون أي استثناء يوقف بدء التشغيل.
/// </summary>
public class LicenseService : ILicenseService
{
    // علامة ثابتة يُتحقَّق من وجودها داخل محتوى الملف بعد فك التشفير، لرفض أي ملف تالف أو غريب المصدر.
    private const string ActivationMarker = "SOURCES-ACTIVATED-V1";

    // قائمة تجزئات SHA-256 (Hex، أحرف صغيرة) للأرقام التسلسلية الصالحة.
    // TODO(إدريس): استبدل/أضف التجزئة الحقيقية قبل الشحن. القيمة الحالية تخص الرقم النائب
    // "SOURCES-2026-TRIAL-ACTIVATE" فقط، وهي غير مناسبة للإنتاج.
    private static readonly string[] _validHashes = new[]
    {
        "ee1bc9661019e287d7b297bd956e6e1424e0f062b71a40ee28668cc9076db54a"
    };

    private readonly string _licenseFilePath;
    private bool _isActivated;

    public bool IsActivated => _isActivated;

    public LicenseService() : this(DatabasePaths.LicenseFilePath) { }

    /// <summary>مُنشئ يسمح بحقن مسار ملف مختلف (اختبارات) بدل %ProgramData%\Sources\license.dat.</summary>
    public LicenseService(string licenseFilePath)
    {
        _licenseFilePath = licenseFilePath;
        _isActivated = ReadActivationState();
    }

    private bool ReadActivationState()
    {
        try
        {
            if (!File.Exists(_licenseFilePath))
                return false;

            var protectedBytes = File.ReadAllBytes(_licenseFilePath);
            byte[] plainBytes;
            try
            {
                plainBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.LocalMachine);
            }
            catch (CryptographicException)
            {
                // ملف تالف أو مشفَّر بمفتاح جهاز مختلف — يُعامَل كغياب تفعيل، لا كخطأ يوقف بدء التشغيل.
                return false;
            }

            var content = Encoding.UTF8.GetString(plainBytes);
            return content.StartsWith(ActivationMarker, StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            LoggerService.LogWarning($"تعذّرت قراءة ملف علامة التفعيل، سيُعامَل النظام كوضع تجريبي: {ex.Message}");
            return false;
        }
    }

    public (bool Success, string Message) Activate(string serialInput)
    {
        if (string.IsNullOrWhiteSpace(serialInput))
            return (false, TranslationHelper.GetString("MsgErrLicenseSerialEmpty")
                ?? "الرجاء إدخال الرقم التسلسلي.");

        var normalized = serialInput.Trim().ToUpperInvariant();
        var inputHash = ComputeSha256Hex(normalized);

        var isValid = false;
        foreach (var validHash in _validHashes)
        {
            if (string.Equals(inputHash, validHash, StringComparison.OrdinalIgnoreCase))
            {
                isValid = true;
                break;
            }
        }

        if (!isValid)
            return (false, TranslationHelper.GetString("MsgErrLicenseSerialInvalid")
                ?? "الرقم التسلسلي غير صحيح. الرجاء التأكد منه والمحاولة مرة أخرى.");

        try
        {
            PersistActivation();
        }
        catch (Exception ex)
        {
            LoggerService.LogError("فشل حفظ علامة التفعيل بعد التحقق الناجح من الرقم التسلسلي", ex);
            return (false, TranslationHelper.GetString("MsgErrLicensePersistFailed")
                ?? "تم التحقق من الرقم بنجاح لكن تعذّر حفظ حالة التفعيل محلياً. حاول مرة أخرى.");
        }

        _isActivated = true;
        return (true, TranslationHelper.GetString("MsgSuccessLicenseActivated")
            ?? "تم تفعيل المنظومة بنجاح.");
    }

    private void PersistActivation()
    {
        var dir = Path.GetDirectoryName(_licenseFilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var plainBytes = Encoding.UTF8.GetBytes(ActivationMarker);
        var protectedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.LocalMachine);
        File.WriteAllBytes(_licenseFilePath, protectedBytes);
    }

    private static string ComputeSha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(bytes);
        var sb = new StringBuilder(hashBytes.Length * 2);
        foreach (var b in hashBytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
