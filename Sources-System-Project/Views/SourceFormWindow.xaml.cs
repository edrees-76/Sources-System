using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Sources.ViewModels;

namespace Sources.Views;

public partial class SourceFormWindow : Window
{
    /// <summary>
    /// يشير إلى أن هذه النافذة بصدد الإغلاق فعلياً (سواء عبر زر الإغلاق الأصلي ✕، Alt+F4،
    /// أو إغلاق برمجي مُستدعى من SourcesView). يُستخدَم لمنع SourcesView من استدعاء Close()
    /// مرة أخرى بشكل متكرر (reentrant) أثناء معالجة حدث Closing نفسه، وذلك عندما يُصفِّر
    /// CancelEditCommand خاصية IsEditing من داخل معالج Closing.
    /// </summary>
    internal bool IsClosingInProgress { get; private set; }

    public SourceFormWindow()
    {
        InitializeComponent();
        Closing += SourceFormWindow_Closing;
        PreviewKeyDown += SourceFormWindow_PreviewKeyDown;
    }

    /// <summary>
    /// تصحيح إضافي للجولة 149 بعد اكتشاف بصري فعلي (وليس آلياً): الاعتماد على
    /// UpdateSourceTrigger=PropertyChanged وحده في XAML لا يضمن ترتيب معالجة أحداث لوحة
    /// المفاتيح الحقيقية في كل الحالات. هذا الإصلاح مستقل تماماً عن UpdateSourceTrigger ويطبَّق
    /// بنفس منطق RadioisotopeFormWindow: معالجة PreviewKeyDown (نفقي/Tunnel) على مستوى النافذة
    /// لمفتاح Enter تُنفَّذ حتماً قبل أي معالجة فقاعية/Bubble لاحقة (زر الحفظ IsDefault في
    /// الخطوة الأخيرة) بحكم ترتيب توجيه الأحداث في WPF، فتُجبِر أي TextBox يحمل التركيز حالياً
    /// على تفريغ (Flush) قيمته إلى خاصية الربط فوراً عبر BindingExpression.UpdateSource() قبل
    /// أن يصل Enter إلى منطق الزر الافتراضي. لا أثر لهذا المعالج في الخطوات التي لا يكون فيها
    /// زر الحفظ IsDefault نشطاً (الخطوتان 1 و2) — وهذا سلوك مقصود موروث من قرار الجولة 148
    /// (لا KeyBinding على مستوى النافذة أصلاً)، وليس عطلاً يخص هذه الجولة.
    /// </summary>
    private void SourceFormWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (Keyboard.FocusedElement is not TextBox focusedTextBox) return;

        var bindingExpression = focusedTextBox.GetBindingExpression(TextBox.TextProperty);
        bindingExpression?.UpdateSource();
    }

    private void SourceFormWindow_Closing(object? sender, CancelEventArgs e)
    {
        IsClosingInProgress = true;

        // إذا كانت IsEditing لا تزال true عند لحظة الإغلاق، فهذا يعني أن الإغلاق حدث عبر
        // زر ✕ الأصلي أو Alt+F4 (وليس عبر مسار الحفظ/الإلغاء الطبيعي الذي يُصفّر IsEditing
        // بنفسه أولاً قبل أن يستدعي SourcesView.xaml.cs الإغلاق البرمجي). في هذه الحالة يجب
        // استدعاء CancelEditCommand لتصفير كامل حالة النموذج (الحقول، IsNew، CurrentStep، ...)
        // عبر نفس المسار المستخدم عند الإلغاء اليدوي من داخل النموذج — بغض النظر عن الخطوة
        // التي كان المعالج متوقفاً عندها، ولكلا نوعي المصدر (العادي والنيتروني) على السواء.
        if (DataContext is SourcesViewModel vm && vm.IsEditing)
        {
            if (vm.CancelEditCommand.CanExecute(null))
            {
                vm.CancelEditCommand.Execute(null);
            }
        }
    }
}
