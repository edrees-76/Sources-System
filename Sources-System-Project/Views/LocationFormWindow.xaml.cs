using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Sources.ViewModels;

namespace Sources.Views;

public partial class LocationFormWindow : Window
{
    /// <summary>
    /// يشير إلى أن هذه النافذة بصدد الإغلاق فعلياً (سواء عبر زر الإغلاق الأصلي ✕، Alt+F4،
    /// أو إغلاق برمجي مُستدعى من LocationsView). يُستخدَم لمنع LocationsView من استدعاء
    /// Close() مرة أخرى بشكل متكرر (reentrant) أثناء معالجة حدث Closing نفسه، وذلك عندما
    /// يُصفِّر CancelEditCommand خاصية IsEditing من داخل معالج Closing.
    /// </summary>
    internal bool IsClosingInProgress { get; private set; }

    public LocationFormWindow()
    {
        InitializeComponent();
        Closing += LocationFormWindow_Closing;
        PreviewKeyDown += LocationFormWindow_PreviewKeyDown;
    }

    /// <summary>
    /// يطبّق نفس إصلاح الجولة 149 (انظر SourceFormWindow.xaml.cs): معالجة PreviewKeyDown
    /// (نفقي/Tunnel) على مستوى النافذة لمفتاح Enter تُجبِر أي TextBox يحمل التركيز حالياً على
    /// تفريغ (Flush) قيمته إلى خاصية الربط فوراً عبر BindingExpression.UpdateSource() قبل أن
    /// يصل Enter إلى منطق زر الحفظ الافتراضي (IsDefault). أُضيف في الجولة 196.
    /// </summary>
    private void LocationFormWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (Keyboard.FocusedElement is not TextBox focusedTextBox) return;

        var bindingExpression = focusedTextBox.GetBindingExpression(TextBox.TextProperty);
        bindingExpression?.UpdateSource();
    }

    private void LocationFormWindow_Closing(object? sender, CancelEventArgs e)
    {
        IsClosingInProgress = true;

        // إذا كانت IsEditing لا تزال true عند لحظة الإغلاق، فهذا يعني أن الإغلاق حدث عبر
        // زر ✕ الأصلي أو Alt+F4 (وليس عبر مسار الحفظ/الإلغاء الطبيعي الذي يُصفّر IsEditing
        // بنفسه أولاً قبل أن يستدعي LocationsView.xaml.cs الإغلاق البرمجي). في هذه الحالة يجب
        // استدعاء CancelEditCommand لتصفير كامل حالة النموذج (الحقول، ...) عبر نفس المسار
        // المستخدم عند الإلغاء اليدوي من داخل النموذج.
        if (DataContext is LocationsViewModel vm && vm.IsEditing)
        {
            if (vm.CancelEditCommand.CanExecute(null))
            {
                vm.CancelEditCommand.Execute(null);
            }
        }
    }
}
