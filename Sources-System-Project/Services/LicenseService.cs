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
/// ملاحظة هامة لإدريس: التجزئة المُضمَّنة في _validHashes هي تجزئة الرقم التسلسلي الإنتاجي
/// المعتمد فعلياً. الرقم النائب (placeholder) السابق "SOURCES-2026-TRIAL-ACTIVATE" لم يعد صالحاً
/// ولا يجوز إعادته إلى هذه القائمة.
///
/// عند بدء التطبيق تُقرأ علامة التفعيل من %ProgramData%\Sources\license.dat (مشفَّرة عبر DPAPI
/// بنطاق الجهاز LocalMachine). غياب الملف أو تلفه أو فشل فك التشفير = وضع تجريبي (IsActivated = false)
/// دون أي استثناء يوقف بدء التشغيل.
/// </summary>
public class LicenseService : ILicenseService
{
    // علامة ثابتة يُتحقَّق من وجودها داخل محتوى الملف بعد فك التشفير، لرفض أي ملف تالف أو غريب المصدر.
    private const string ActivationMarker = "SOURCES-ACTIVATED-V1";

    // قائمة تجزئات SHA-256 (Hex، أحرف صغيرة) للأرقام التسلسلية الإنتاجية الصالحة.
    private static readonly string[] _validHashes = new[]
    {
        "777f6d79688ce35c8d3f22abc8fea26d890d5d39b84b0a2cdd3ad5a993874057"
    };

    private readonly string _licenseFilePath;
    private readonly string[] _instanceValidHashes;
    private bool _isActivated;

    public bool IsActivated => _isActivated;

    public LicenseService() : this(DatabasePaths.LicenseFilePath) { }

    /// <summary>مُنشئ يسمح بحقن مسار ملف مختلف (اختبارات) بدل %ProgramData%\Sources\license.dat.</summary>
    public LicenseService(string licenseFilePath) : this(licenseFilePath, _validHashes) { }

    /// <summary>
    /// مُنشئ يسمح أيضاً بحقن قائمة تجزئات صالحة مختلفة عن القائمة الإنتاجية الثابتة، لعزل
    /// اختبارات الوحدة عن الرقم التسلسلي الإنتاجي الحقيقي دون التأثير على سلوك الإنتاج
    /// (المُنشئ أعلاه يستمر باستخدام _validHashes الثابتة).
    /// </summary>
    public LicenseService(string licenseFilePath, string[] validHashes)
    {
        _licenseFilePath = licenseFilePath;
        _instanceValidHashes = validHashes;
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
        foreach (var validHash in _instanceValidHashes)
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
