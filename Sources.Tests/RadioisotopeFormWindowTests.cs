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
    /// اختبار انحدار مشترك (الجولة 149) لخلل فقدان القيمة عند الضغط على Enter مباشرة بعد الكتابة
    /// دون فقدان التركيز يدويًا أولاً: يفتح النافذة مباشرة (بدون ShowDialog المتداخل)، ينتقل إلى
    /// الخطوة 2 حيث يظهر الحقل، يكتب قيمة جديدة عبر تحديث TextBox.Text (يحاكي الكتابة الفعلية عبر
    /// آلية الربط نفسها)، ثم يحاكي الضغط على Enter (KeyDown) على نفس العنصر المركَّز دون أي
    /// LostFocus/Focus() يدوي على عنصر آخر، ثم يتحقق من وصول القيمة الجديدة لخاصية الـ ViewModel.
    /// </summary>
    private static void AssertEnterCommitsNewValue_ForRadioisotopeField(
        string bindingPath,
        string newTextValue,
        System.Func<RadioisotopesViewModel, string> readBoundText)
    {
        RunInSta(() =>
        {
            var vm = CreateViewModel();
            try
            {
                vm.AddNewCommand.Execute(null);
                vm.NextStepCommand.Execute(null); // الانتقال إلى الخطوة 2 حيث تظهر الحقول الفنية وزر الحفظ

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

                    Assert.Equal(newTextValue, readBoundText(vm));
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
    public void RadioisotopeFormWindow_EnterAfterTyping_CommitsNewValue_ForEditHalfLifeText()
        => AssertEnterCommitsNewValue_ForRadioisotopeField("EditHalfLifeText", "45.5", vm => vm.EditHalfLifeText);

    [Fact]
    public void RadioisotopeFormWindow_EnterAfterTyping_CommitsNewValue_ForEditEnergyText()
        => AssertEnterCommitsNewValue_ForRadioisotopeField("EditEnergyText", "661.7", vm => vm.EditEnergyText);

    [Fact]
    public void RadioisotopeFormWindow_EnterAfterTyping_CommitsNewValue_ForEditYieldText()
        => AssertEnterCommitsNewValue_ForRadioisotopeField("EditYieldText", "0.85", vm => vm.EditYieldText);

    [Fact]
    public void RadioisotopeFormWindow_EnterAfterTyping_CommitsNewValue_ForEditGammaConstantText()
        => AssertEnterCommitsNewValue_ForRadioisotopeField("EditGammaConstantText", "0.0772", vm => vm.EditGammaConstantText);
}
