using Sources.Services;
using Sources.Views;
using System;
using System.Windows;

namespace Sources.Helpers
{
    public static class DialogHelper
    {
        public static bool IsTestMode { get; set; } = false;
        public static bool? ShowConfirmationResult { get; set; }
        public static string? LastMessage { get; set; }
        public static string? LastTitle { get; set; }

        /// <summary>
        /// نتيجة اختبارية صريحة لطلب صلاحية مدير النظام (المكان الوحيد المسموح له بمعرفة وضع
        /// الاختبار لهذا الطلب تحديداً). لا منح تلقائي: القيمة الافتراضية null تعني رفضاً
        /// (الجولة 199 — الفشل المغلق).
        /// </summary>
        public static bool? TestAdminPromptResult { get; set; }

        /// <summary>
        /// نقطة العزل الوحيدة لعرض نافذة تأكيد صلاحية مدير النظام. في وضع الاختبار لا يُنفَّذ
        /// الحوار الحقيقي إطلاقاً، وتُعاد TestAdminPromptResult أو false افتراضياً (رفض، لا منح
        /// تلقائي). في الإنتاج يُنفَّذ الحوار ضمن try/catch: أي استثناء يُسجَّل تحذيراً عبر
        /// LoggerService ويُعرض خطأ عام، وتُرفض الصلاحية بدل رميه أو منحها ضمناً (الجولة 199 —
        /// الفشل المغلق بدل الفشل المفتوح).
        /// </summary>
        public static bool ShowAdminPrompt(Func<bool> showDialogAndGetResult)
        {
            if (IsTestMode)
            {
                return TestAdminPromptResult ?? false;
            }

            return ExecuteAdminPromptWithFailClosedHandling(showDialogAndGetResult);
        }

        /// <summary>
        /// منطق الفشل المغلق نفسه (try/catch + تسجيل + رسالة عامة) معزول في تابع داخلي مستقل عن
        /// IsTestMode، حتى يمكن اختبار مسار الاستثناء دون إطفاء IsTestMode مؤقتاً — إطفاؤه كان
        /// سيسمح لهذا التابع باستدعاء ShowError الحقيقية، التي قد تفتح نافذة حوار فعلية وتحجب
        /// الاختبار إلى الأبد إن كان Application.Current معرَّفاً بالفعل من اختبار STA سابق في نفس
        /// العملية (لا يوجد أي اختبار حالي يُطفئ IsTestMode لهذا السبب بالضبط). عبر InternalsVisibleTo
        /// لمجمّعة Sources.Tests (الجولة 199)، يبقى IsTestMode مفعّلاً أثناء الاختبار فتظل ShowError
        /// آمنة (تُسجّل LastMessage فقط ولا تعرض نافذة حقيقية).
        /// </summary>
        internal static bool ExecuteAdminPromptWithFailClosedHandling(Func<bool> showDialogAndGetResult)
        {
            try
            {
                return showDialogAndGetResult();
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning(
                    $"RequestAdminAccess: استثناء غير متوقع أثناء عرض نافذة تأكيد صلاحية مدير النظام — تم الرفض افتراضياً: {ex}");
                ShowError(TranslationHelper.GetString("MsgErrAdminPromptFailed") ?? "تعذّر التحقق من هوية مدير النظام، تم رفض الطلب");
                return false;
            }
        }

        public static void ShowInfo(string message, string? title = null, string? imagePath = null)
        {
            LastMessage = message;
            LastTitle = title;
            if (IsTestMode || Application.Current?.Dispatcher == null) return;
            Application.Current.Dispatcher.Invoke(() =>
            {
                var finalTitle = title ?? TranslationHelper.GetString("AlertTitle") ?? "تنبيه";
                var dialog = new AlertDialog(message, finalTitle, "Info", imagePath: imagePath);
                if (Application.Current.MainWindow != null && Application.Current.MainWindow != dialog)
                {
                    dialog.Owner = Application.Current.MainWindow;
                }
                dialog.ShowDialog();
            });
        }

        public static void ShowWarning(string message, string? title = null)
        {
            LastMessage = message;
            LastTitle = title;
            if (IsTestMode || Application.Current?.Dispatcher == null) return;
            Application.Current.Dispatcher.Invoke(() =>
            {
                var finalTitle = title ?? TranslationHelper.GetString("AlertWarning") ?? "تحذير";
                var dialog = new AlertDialog(message, finalTitle, "Warning");
                if (Application.Current.MainWindow != null && Application.Current.MainWindow != dialog)
                {
                    dialog.Owner = Application.Current.MainWindow;
                }
                dialog.ShowDialog();
            });
        }

        public static void ShowError(string message, string? title = null)
        {
            LastMessage = message;
            LastTitle = title;
            if (IsTestMode || Application.Current?.Dispatcher == null) return;
            Application.Current.Dispatcher.Invoke(() =>
            {
                var finalTitle = title ?? TranslationHelper.GetString("AlertError") ?? "خطأ";
                var dialog = new AlertDialog(message, finalTitle, "Error");
                if (Application.Current.MainWindow != null && Application.Current.MainWindow != dialog)
                {
                    dialog.Owner = Application.Current.MainWindow;
                }
                dialog.ShowDialog();
            });
        }

        public static bool ShowConfirmation(string message, string? title = null)
        {
            LastMessage = message;
            LastTitle = title;
            if (ShowConfirmationResult.HasValue) return ShowConfirmationResult.Value;
            if (IsTestMode || Application.Current?.Dispatcher == null) return true;
            bool result = false;
            Application.Current.Dispatcher.Invoke(() =>
            {
                var finalTitle = title ?? TranslationHelper.GetString("AlertConfirmation") ?? "تأكيد";
                var dialog = new AlertDialog(message, finalTitle, "Question", isQuestion: true);
                if (Application.Current.MainWindow != null && Application.Current.MainWindow != dialog)
                {
                    dialog.Owner = Application.Current.MainWindow;
                }
                dialog.ShowDialog();
                result = dialog.Result == AlertDialog.AlertResult.Yes;
            });
            return result;
        }

        /// <summary>
        /// يعرض نافذة حوارية خارجية عبر الإجراء المُمرَّر. في وضع الاختبار (IsTestMode) لا يُنفَّذ
        /// الإجراء إطلاقاً وتُعاد القيمة false؛ خلاف ذلك يُنفَّذ الإجراء وتُعاد القيمة true.
        /// يوفّر هذا نقطة عزل وحيدة لفتح النوافذ الخارجية بدلاً من تفرّق فحص IsTestMode في كل موقع استدعاء.
        /// </summary>
        public static bool ShowWindowDialog(System.Action showWindow)
        {
            if (IsTestMode) return false;
            showWindow();
            return true;
        }

        public static AlertDialog.AlertResult? ShowInfoWithExtraOptionResult { get; set; }

        public static AlertDialog.AlertResult ShowInfoWithExtraOption(string message, string extraButtonText, string? title = null)
        {
            LastMessage = message;
            LastTitle = title;
            if (ShowInfoWithExtraOptionResult.HasValue) return ShowInfoWithExtraOptionResult.Value;
            if (IsTestMode || Application.Current?.Dispatcher == null) return AlertDialog.AlertResult.OK;
            AlertDialog.AlertResult result = AlertDialog.AlertResult.OK;
            Application.Current.Dispatcher.Invoke(() =>
            {
                var finalTitle = title ?? TranslationHelper.GetString("AlertTitle") ?? "تنبيه";
                var dialog = new AlertDialog(message, finalTitle, "Info", extraButtonText: extraButtonText);
                if (Application.Current.MainWindow != null && Application.Current.MainWindow != dialog)
                {
                    dialog.Owner = Application.Current.MainWindow;
                }
                dialog.ShowDialog();
                result = dialog.Result;
            });
            return result;
        }
    }
}
