using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Moq;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Sources.Views;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// اختبار انحدار تكاملي (الجولة 146) يتحقق من أن نموذج تحرير النظير المشع (RadioisotopesView)
/// أصبح نافذة WPF حقيقية منفصلة (RadioisotopeFormWindow) تُفتح عبر ShowDialog() من كود
/// RadioisotopesView.xaml.cs — بنفس نمط BorrowFormWindow (الجولة 143)/LocationFormWindow
/// (الجولة 145) — بدلاً من التراكب المنبثق داخل العرض (In-View Modal Overlay) المُستخدَم في
/// الجولة 142. يحل هذا الملف محل RadioisotopesViewOverlayTests.cs الذي كان يتحقق من نمط
/// التراكب القديم (Panel.ZIndex=1000) الذي لم يعد موجوداً بعد هذه الجولة.
/// </summary>
public class RadioisotopeFormWindowTests
{
    private static void RunInSta(System.Action action) => Sources.Tests.Fixtures.WpfStaFixture.RunInSta(action);

    private static RadioisotopesViewModel CreateViewModel()
    {
        var mockService = new Mock<IRadioisotopeService>();
        mockService.Setup(s => s.GetAll()).Returns(new List<Radioisotope>());
        return new RadioisotopesViewModel(mockService.Object);
    }

    private static T? FindFirstVisualChild<T>(System.Windows.DependencyObject root) where T : System.Windows.DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T typed) return typed;
            var nested = FindFirstVisualChild<T>(child);
            if (nested != null) return nested;
        }
        return null;
    }

    /// <summary>
    /// يبحث في الشجرة المرئية عن أول TextBox مربوط (Binding) بمسار الخاصية المحدَّدة على
    /// TextBox.TextProperty — يُستخدَم لإيجاد الحقل الفعلي دون الاعتماد على x:Name.
    /// </summary>
    private static TextBox? FindTextBoxByBindingPath(System.Windows.DependencyObject root, string propertyPath)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is TextBox textBox)
            {
                var expression = BindingOperations.GetBindingExpression(textBox, TextBox.TextProperty);
                if (expression?.ParentBinding?.Path?.Path == propertyPath) return textBox;
            }
            var nested = FindTextBoxByBindingPath(child, propertyPath);
            if (nested != null) return nested;
        }
        return null;
    }

    /// <summary>
    /// يحاكي ضغطة مفتاح Enter (KeyDown) على العنصر المركَّز دون استدعاء LostFocus أو Focus()
    /// على عنصر آخر — بنفس أسلوب المستخدم الفعلي الذي يضغط Enter مباشرة بعد الكتابة.
    /// </summary>
    private static void SimulateEnterKeyPress(UIElement element)
    {
        var source = PresentationSource.FromVisual(element);
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter)
        {
            RoutedEvent = Keyboard.KeyDownEvent
        };
        element.RaiseEvent(args);
    }

    [Fact]
    public void RadioisotopesView_HostedInRealWindow_DataGridAlwaysVisible_RegardlessOfIsEditing()
    {
        RunInSta(() =>
        {
            var vm = CreateViewModel();
            try
            {
                var view = new RadioisotopesView { DataContext = vm };

                var window = new Window
                {
                    Content = view,
                    Width = 1280,
                    Height = 800,
                    WindowStyle = WindowStyle.None,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    Left = -5000,
                    Top = -5000
                };

                window.Show();
                try
                {
                    window.UpdateLayout();

                    var dataGrid = FindFirstVisualChild<System.Windows.Controls.DataGrid>(view);
                    Assert.NotNull(dataGrid);
                    Assert.Equal(Visibility.Visible, dataGrid!.Visibility);
                    Assert.False(vm.IsEditing);
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                WeakReferenceMessenger.Default.UnregisterAll(vm);
            }
        });
    }

    [Fact]
    public void RadioisotopesView_AddNewCommand_OpensRadioisotopeFormWindow_AndDataGridStaysVisibleUnderneath()
    {
        RunInSta(() =>
        {
            var vm = CreateViewModel();
            try
            {
                var view = new RadioisotopesView { DataContext = vm };

                var window = new Window
                {
                    Content = view,
                    Width = 1280,
                    Height = 800,
                    WindowStyle = WindowStyle.None,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    Left = -5000,
                    Top = -5000
                };

                window.Show();
                try
                {
                    window.UpdateLayout();

                    var dataGrid = FindFirstVisualChild<System.Windows.Controls.DataGrid>(view);
                    Assert.NotNull(dataGrid);

                    // ShowDialog() الذي يستدعيه RadioisotopesView.xaml.cs عند IsEditing=true يحجب
                    // مسار التنفيذ الحالي بمضخة رسائل متداخلة (nested message pump) خاصة به.
                    // لذا يُجدوَل التحقق والإغلاق عبر Dispatcher.BeginInvoke قبل استدعاء الأمر،
                    // فتُنفَّذ هذه الخطوة أثناء تشغيل حلقة ShowDialog() المتداخلة نفسها، لا بعدها.
                    RadioisotopeFormWindow? capturedFormWindow = null;
                    Dispatcher.CurrentDispatcher.BeginInvoke(new System.Action(() =>
                    {
                        capturedFormWindow = Application.Current.Windows
                            .OfType<RadioisotopeFormWindow>()
                            .FirstOrDefault();

                        Assert.NotNull(capturedFormWindow);
                        Assert.True(vm.IsEditing);
                        Assert.Equal(vm, capturedFormWindow!.DataContext);
                        Assert.Equal(Visibility.Visible, dataGrid!.Visibility);

                        vm.CancelEditCommand.Execute(null);
                    }), DispatcherPriority.ApplicationIdle);

                    vm.AddNewCommand.Execute(null);

                    // بعد عودة AddNewCommand.Execute، تكون RadioisotopeFormWindow قد أُغلقت فعلاً
                    // (CancelEditCommand أعاد IsEditing إلى false فأغلق الكود-خلف النافذة).
                    Assert.False(vm.IsEditing);
                    Assert.DoesNotContain(capturedFormWindow, Application.Current.Windows.OfType<RadioisotopeFormWindow>());
                    Assert.Equal(Visibility.Visible, dataGrid!.Visibility);
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                WeakReferenceMessenger.Default.UnregisterAll(vm);
            }
        });
    }

    [Fact]
    public void RadioisotopeFormWindow_ClosedViaNativeCloseButton_ResetsIsEditing_AndAllowsReopening()
    {
        RunInSta(() =>
        {
            var vm = CreateViewModel();
            try
            {
                var view = new RadioisotopesView { DataContext = vm };

                var window = new Window
                {
                    Content = view,
                    Width = 1280,
                    Height = 800,
                    WindowStyle = WindowStyle.None,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    Left = -5000,
                    Top = -5000
                };

                window.Show();
                try
                {
                    window.UpdateLayout();

                    // الخطوة 1: فتح RadioisotopeFormWindow عبر AddNewCommand، ثم محاكاة الإغلاق
                    // عبر زر ✕ الأصلي (Close() مباشرة على النافذة نفسها — وليس عبر
                    // CancelEditCommand)، بنفس أسلوب الجدولة عبر Dispatcher.BeginInvoke.
                    Dispatcher.CurrentDispatcher.BeginInvoke(new System.Action(() =>
                    {
                        var formWindow = Application.Current.Windows
                            .OfType<RadioisotopeFormWindow>()
                            .FirstOrDefault();

                        Assert.NotNull(formWindow);
                        Assert.True(vm.IsEditing);

                        // محاكاة إغلاق عبر ✕ / Alt+F4: استدعاء Close() مباشرة على النافذة،
                        // وليس عبر CancelEditCommand.
                        formWindow!.Close();
                    }), DispatcherPriority.ApplicationIdle);

                    vm.AddNewCommand.Execute(null);

                    // بعد الإغلاق عبر ✕، يجب أن تُصفَّر IsEditing تلقائياً عبر معالج Closing
                    // في RadioisotopeFormWindow (الذي يستدعي CancelEditCommand داخلياً)، دون أن
                    // يحتاج المستخدم لاستدعاء زر الإلغاء يدوياً.
                    Assert.False(vm.IsEditing);
                    Assert.Empty(Application.Current.Windows.OfType<RadioisotopeFormWindow>());

                    // الخطوة 2: التأكد من أن نافذة جديدة فعلاً تُفتح عند استدعاء AddNewCommand
                    // مرة أخرى (وليس لا شيء بسبب مرجع نافذة سابق عالق يمنع الفتح).
                    RadioisotopeFormWindow? secondFormWindow = null;
                    Dispatcher.CurrentDispatcher.BeginInvoke(new System.Action(() =>
                    {
                        secondFormWindow = Application.Current.Windows
                            .OfType<RadioisotopeFormWindow>()
                            .FirstOrDefault();

                        Assert.NotNull(secondFormWindow);
                        Assert.True(vm.IsEditing);

                        vm.CancelEditCommand.Execute(null);
                    }), DispatcherPriority.ApplicationIdle);

                    vm.AddNewCommand.Execute(null);

                    Assert.NotNull(secondFormWindow);
                    Assert.False(vm.IsEditing);
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                WeakReferenceMessenger.Default.UnregisterAll(vm);
            }
        });
    }

    /// <summary>
    /// اختبار انحدار مشترك (الجولة 149، مُعاد كتابته بالكامل بعد اكتشاف بصري فعلي أظهر أن
    /// النسخة الأولى من هذا الاختبار كانت تُصدر نجاحاً زائفاً): النسخة الأولى كانت تكتب القيمة
    /// الجديدة مباشرة عبر `textBox.Text = newTextValue` ثم تتحقق فوراً من أن خاصية الـViewModel
    /// المرتبطة تساوي هذه القيمة — لكن مع UpdateSourceTrigger=PropertyChanged فإن تعيين `Text`
    /// برمجياً يُحدِّث خاصية الربط **فوراً وبشكل متزامن في نفس السطر**، قبل أن تُحاكى ضغطة Enter
    /// أصلاً. فكان التأكيد ينجح دائماً بصرف النظر عمّا فعلته ضغطة Enter فعلياً — أي أن الاختبار لم
    /// يكن يفحص مسار الحفظ الحقيقي إطلاقاً، بل فقط أن تعيين TextBox.Text يُحدِّث الربط (بديهي).
    /// هذه النسخة تُصلح ذلك جذرياً: تُحرِّر نظيراً **موجوداً فعلاً** (وليس نموذجاً جديداً فارغاً)
    /// عبر EditCommand الحقيقي، وتتحقق ليس من خاصية الـViewModel النصية بل من **القيمة الفعلية
    /// التي وصلت لطبقة الخدمة IRadioisotopeService.Update عبر Moq Callback** — وهذا هو الدليل
    /// الوحيد على أن الحفظ الحقيقي (الذي تستدعيه ضغطة Enter عبر KeyBinding/IsDefault) استخدم
    /// القيمة الجديدة لا القديمة. كما تتحقق أن Update استُدعيت مرة واحدة بالضبط (لا حفظ مزدوج
    /// من تضارب KeyBinding مع IsDefault) وأن IsEditing أصبحت false (نجاح حقيقي أغلق النافذة،
    /// لا رسالة نجاح كاذبة).
    /// </summary>
    private static void AssertEnterCommitsNewValue_ForRadioisotopeField(
        string bindingPath,
        string newTextValue,
        System.Func<Radioisotope, double?> readPersistedValue,
        double expectedPersistedValue)
    {
        RunInSta(() =>
        {
            var existing = new Radioisotope
            {
                Id = System.Guid.NewGuid(),
                Name = "Co-60",
                ArabicName = "كوبالت-60",
                Symbol = "Co-60",
                RadiationType = "Gamma",
                HalfLife = 5.27,
                HalfLifeUnit = "years",
                Energy = 1.17,
                Yield = 1.0,
                GammaConstant = 0.351,
                Notes = "",
                EnglishNotes = ""
            };

            var mockService = new Mock<IRadioisotopeService>();
            mockService.Setup(s => s.GetAll()).Returns(new List<Radioisotope> { existing });
            Radioisotope? persistedItem = null;
            int updateCallCount = 0;
            mockService.Setup(s => s.Update(It.IsAny<Radioisotope>()))
                .Callback<Radioisotope>(item => { persistedItem = item; updateCallCount++; })
                .Returns((true, "تم تحديث النظير"));

            var vm = new RadioisotopesViewModel(mockService.Object);
            try
            {
                vm.LoadData();
                vm.Selected = vm.Radioisotopes.First(r => r.Id == existing.Id);
                vm.EditCommand.Execute(null); // يملأ كل الحقول من العنصر القائم، IsEditing=true، الخطوة 1
                vm.NextStepCommand.Execute(null); // الانتقال إلى الخطوة 2 حيث تظهر الحقول الفنية

                var formWindow = new RadioisotopeFormWindow
                {
                    DataContext = vm,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    Left = -5000,
                    Top = -5000
                };

                formWindow.Show();
                try
                {
                    formWindow.UpdateLayout();

                    var textBox = FindTextBoxByBindingPath(formWindow, bindingPath);
                    Assert.NotNull(textBox);

                    textBox!.Focus();
                    textBox.Text = newTextValue;
                    SimulateEnterKeyPress(textBox);

                    Assert.Equal(1, updateCallCount); // حفظ واحد بالضبط، لا حفظ مزدوج
                    Assert.NotNull(persistedItem);
                    Assert.Equal(expectedPersistedValue, readPersistedValue(persistedItem!) ?? double.NaN, precision: 4);
                    Assert.False(vm.IsEditing); // النافذة أُغلقت فعلاً — نجاح حقيقي لا رسالة كاذبة
                }
                finally
                {
                    formWindow.Close();
                }
            }
            finally
            {
                WeakReferenceMessenger.Default.UnregisterAll(vm);
            }
        });
    }

    [Fact]
    public void RadioisotopeFormWindow_EnterAfterTyping_PersistsNewValue_ForEditHalfLifeText()
        => AssertEnterCommitsNewValue_ForRadioisotopeField("EditHalfLifeText", "45.5", r => r.HalfLife, 45.5);

    [Fact]
    public void RadioisotopeFormWindow_EnterAfterTyping_PersistsNewValue_ForEditEnergyText()
        => AssertEnterCommitsNewValue_ForRadioisotopeField("EditEnergyText", "661.7", r => r.Energy, 661.7);

    [Fact]
    public void RadioisotopeFormWindow_EnterAfterTyping_PersistsNewValue_ForEditYieldText()
        => AssertEnterCommitsNewValue_ForRadioisotopeField("EditYieldText", "0.85", r => r.Yield, 0.85);

    [Fact]
    public void RadioisotopeFormWindow_EnterAfterTyping_PersistsNewValue_ForEditGammaConstantText()
        => AssertEnterCommitsNewValue_ForRadioisotopeField("EditGammaConstantText", "0.0772", r => r.GammaConstant, 0.0772);
}
