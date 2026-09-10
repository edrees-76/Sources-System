using System.ComponentModel;
using System.Windows;
using Sources.ViewModels;

namespace Sources.Views;

public partial class UserFormWindow : Window
{
    /// <summary>
    /// يشير إلى أن هذه النافذة بصدد الإغلاق فعلياً (سواء عبر زر الإغلاق الأصلي ✕، Alt+F4،
    /// أو إغلاق برمجي مُستدعى من UsersView). يُستخدَم لمنع UsersView من استدعاء
    /// Close() مرة أخرى بشكل متكرر (reentrant) أثناء معالجة حدث Closing نفسه، وذلك عندما
    /// يُصفِّر CancelEditCommand خاصية IsEditing من داخل معالج Closing.
    /// </summary>
    internal bool IsClosingInProgress { get; private set; }

    public UserFormWindow()
    {
        InitializeComponent();
        Closing += UserFormWindow_Closing;
    }

    private void UserFormWindow_Closing(object? sender, CancelEventArgs e)
    {
        IsClosingInProgress = true;

        // إذا كانت IsEditing لا تزال true عند لحظة الإغلاق، فهذا يعني أن الإغلاق حدث عبر
        // زر ✕ الأصلي أو Alt+F4 (وليس عبر مسار الحفظ/الإلغاء الطبيعي الذي يُصفّر IsEditing
        // بنفسه أولاً قبل أن يستدعي UsersView.xaml.cs الإغلاق البرمجي). في هذه الحالة يجب
        // استدعاء CancelEditCommand لتصفير كامل حالة النموذج (الحقول، ...) عبر نفس المسار
        // المستخدم عند الإلغاء اليدوي من داخل النموذج.
        if (DataContext is UsersViewModel vm && vm.IsEditing)
        {
            if (vm.CancelEditCommand.CanExecute(null))
            {
                vm.CancelEditCommand.Execute(null);
            }
        }
    }
}
