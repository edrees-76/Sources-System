using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Sources.ViewModels;

namespace Sources.Views;

public partial class RadioisotopeFormWindow : Window
{
    /// <summary>
    /// يشير إلى أن هذه النافذة بصدد الإغلاق فعلياً (سواء عبر زر الإغلاق الأصلي ✕، Alt+F4،
    /// أو إغلاق برمجي مُستدعى من RadioisotopesView). يُستخدَم لمنع RadioisotopesView من
    /// استدعاء Close() مرة أخرى بشكل متكرر (reentrant) أثناء معالجة حدث Closing نفسه، وذلك
    /// عندما يُصفِّر CancelEditCommand خاصية IsEditing من داخل معالج Closing.
    /// </summary>
    internal bool IsClosingInProgress { get; private set; }

    public RadioisotopeFormWindow()
    {
        InitializeComponent();
        Closing += RadioisotopeFormWindow_Closing;
        PreviewKeyDown += RadioisotopeFormWindow_PreviewKeyDown;
    }

    /// <summary>
    /// تصحيح إضافي للجولة 149 بعد اكتشاف بصري فعلي (وليس آلياً) أن الاعتماد على
    /// UpdateSourceTrigger=PropertyChanged وحده لا يضمن ترتيب معالجة أحداث لوحة المفاتيح
    /// الحقيقية في كل الحالات: ضغط Enter مباشرة بعد الكتابة كان يُغلق النافذة برسالة نجاح
    /// كاذبة بينما القيمة الفعلية المحفوظة في قاعدة البيانات تبقى القديمة. الإصلاح الحاسم
    /// هنا مستقل تماماً عن UpdateSourceTrigger: يُعالَج PreviewKeyDown (نفقي/Tunnel) على
    /// مستوى النافذة نفسها لمفتاح Enter — وهذا يُنفَّذ حتماً قبل أي معالجة فقاعية/Bubble لاحقة
    /// على مستوى النافذة (سواء عبر KeyBinding أو زر IsDefault) بحكم ترتيب توجيه الأحداث في
    /// WPF (Tunnel من الجذر نزولاً، ثم Bubble من العنصر المركَّز صعوداً) — فيُجبَر أي TextBox
    /// يحمل التركيز حالياً على تفريغ (Flush) قيمته إلى خاصية الربط فوراً عبر
    /// BindingExpression.UpdateSource()، بصرف النظر عن أي تفاصيل توقيت داخلية في WPF لا يمكن
    /// إثباتها من قراءة الكود الساكن وحدها.
    /// </summary>
    private void RadioisotopeFormWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (Keyboard.FocusedElement is not TextBox focusedTextBox) return;

        var bindingExpression = focusedTextBox.GetBindingExpression(TextBox.TextProperty);
        bindingExpression?.UpdateSource();
    }

    private void RadioisotopeFormWindow_Closing(object? sender, CancelEventArgs e)
    {
        IsClosingInProgress = true;

        // إذا كانت IsEditing لا تزال true عند لحظة الإغلاق، فهذا يعني أن الإغلاق حدث عبر
        // زر ✕ الأصلي أو Alt+F4 (وليس عبر مسار الحفظ/الإلغاء الطبيعي الذي يُصفّر IsEditing
        // بنفسه أولاً قبل أن يستدعي RadioisotopesView.xaml.cs الإغلاق البرمجي). في هذه الحالة
        // يجب استدعاء CancelEditCommand لتصفير كامل حالة النموذج (الحقول، ...) عبر نفس المسار
        // المستخدم عند الإلغاء اليدوي من داخل النموذج.
        if (DataContext is RadioisotopesViewModel vm && vm.IsEditing)
        {
            if (vm.CancelEditCommand.CanExecute(null))
            {
                vm.CancelEditCommand.Execute(null);
            }
        }
    }
}
