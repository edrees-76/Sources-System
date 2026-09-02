using System;
using Sources.Services;

namespace Sources.Helpers;

/// <summary>
/// نسخ إلى الحافظة مع إخطار المستخدم بالنتيجة نجاحاً أو فشلاً.
/// السبب في وجوده: النسخ يبدأ بنقرة صريحة من المستخدم ينتظر بعدها تأكيداً،
/// فالفشل الصامت يجعله يظن أن النسخ تم فيلصق قيمة قديمة.
/// </summary>
public static class ClipboardCopyHelper
{
    /// <summary>يُرجع true عند نجاح النسخ. لا يرمي أبداً.</summary>
    public static bool CopyWithFeedback(
        IClipboardService clipboard,
        string? text,
        string successMessage,
        string successTitle,
        string context)
    {
        if (clipboard == null || string.IsNullOrWhiteSpace(text)) return false;

        try
        {
            clipboard.SetText(text);
            DialogHelper.ShowInfo(successMessage, successTitle);
            return true;
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"تعذّر النسخ إلى الحافظة: {context}", ex);
            DialogHelper.ShowError(
                TranslationHelper.GetString("ClipboardCopyFailed")
                    ?? "تعذّر النسخ إلى الحافظة. قد يكون تطبيق آخر يستعملها الآن. أعد المحاولة بعد قليل.",
                TranslationHelper.GetString("ClipboardCopyFailedTitle") ?? "تعذّر النسخ");
            return false;
        }
    }
}
