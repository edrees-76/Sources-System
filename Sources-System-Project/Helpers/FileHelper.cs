using System;
using System.Diagnostics;
using System.IO;
using Sources.Services;

namespace Sources.Helpers
{
    public static class FileHelper
    {
        /// <summary>
        /// يفتح ملفاً ببرنامجه الافتراضي. يُرجع true عند النجاح.
        /// عند الفشل يُسجَّل الخطأ ويُخطَر المستخدم — المستخدم نقر صراحةً ويجب ألا يظن أن الزر معطل.
        /// </summary>
        public static bool OpenFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                LoggerService.LogWarning($"تعذّر فتح الملف — غير موجود في المسار: {filePath}");
                DialogHelper.ShowError(
                    (TranslationHelper.GetString("FileNotFound") ?? "الملف غير موجود في المسار المحدد:") + $"\n{filePath}",
                    TranslationHelper.GetString("FileOpenFailedTitle") ?? "تعذّر فتح الملف");
                return false;
            }

            try
            {
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"تعذّر فتح الملف تلقائياً: {filePath}", ex);
                DialogHelper.ShowError(
                    (TranslationHelper.GetString("FileOpenFailed") ?? "تعذّر فتح الملف تلقائياً. يمكنك فتحه يدوياً من المسار:") + $"\n{filePath}",
                    TranslationHelper.GetString("FileOpenFailedTitle") ?? "تعذّر فتح الملف");
                return false;
            }
        }
    }
}
