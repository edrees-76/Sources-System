using System.Collections.Generic;
using System.Linq;
using System.Windows;
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
}
