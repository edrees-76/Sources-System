using System;
using System.Runtime.CompilerServices;
using Sources.Interfaces;
using Sources.Services;

namespace Sources.Helpers;

/// <summary>
/// يتتبع ما إذا كانت نافذة تحرير مفتوحة فعلياً لكل ViewModel قابل للتحرير (الجولة 199).
/// تستخدمه الشاشات الخمس القابلة للتحرير (Users, Locations, Sources, Radioisotopes, Borrow)
/// لتمييز "IsEditing=true مع نافذة مفتوحة فعلاً" (يبقى الحجب كما هو) عن "IsEditing=true عالق
/// بلا نافذة" (حارس الشفاء الذاتي في MainViewModel.NavigateTo/Logout وMainWindow.OnClosing
/// يسمح بالمتابعة بعد إعادة الضبط بلا حفظ). ConditionalWeakTable لا يمنع جمع القمامة لكائنات
/// ViewModel التي أُغلقت شاشاتها.
/// </summary>
public static class EditingFormTracker
{
    private static readonly ConditionalWeakTable<IEditableViewModel, object> OpenForms = new();
    private static readonly object Marker = new();

    public static void MarkOpen(IEditableViewModel vm) => OpenForms.AddOrUpdate(vm, Marker);

    public static void MarkClosed(IEditableViewModel vm) => OpenForms.Remove(vm);

    public static bool IsFormOpen(IEditableViewModel vm) => OpenForms.TryGetValue(vm, out _);

    /// <summary>
    /// يُستدعى عند فشل فتح نافذة تحرير (استثناء أثناء الإنشاء/تعيين Owner/ShowDialog) في أي من
    /// الشاشات الخمس. يُسجّل تحذيراً، يعيد ضبط حالة التحرير بلا حفظ عبر CancelEditCommand
    /// الحالي لكل ViewModel (يقتصر تحقّقاً على IsEditing=false + ClearForm() في الشاشات الخمس)،
    /// ويعرض رسالة خطأ عامة موجودة.
    /// </summary>
    public static void HandleOpenFailure(string viewName, Exception ex, System.Windows.Input.ICommand cancelEditCommand)
    {
        LoggerService.LogWarning(
            $"{viewName}: استثناء غير متوقع أثناء فتح نافذة التحرير — إعادة الضبط بلا حفظ. {ex}");

        if (cancelEditCommand.CanExecute(null))
        {
            cancelEditCommand.Execute(null);
        }

        DialogHelper.ShowError(TranslationHelper.GetString("MsgErrFormOpenFailed") ?? "تعذّر فتح نافذة التحرير، تم إلغاء العملية");
    }
}
