using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Moq;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fixtures;
using Sources.ViewModels;
using Sources.Views;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// اختبار انحدار تكاملي (تصحيح الجولة 143) يتحقق من أن نموذج الاستعارة (BorrowView)
/// أصبح نافذة WPF حقيقية منفصلة (BorrowFormWindow) تُفتح عبر ShowDialog() من كود
/// BorrowView.xaml.cs — بنفس نمط LocationDetailsWindow — بدلاً من التراكب المنبثق
/// داخل العرض (In-View Modal Overlay) المُستخدَم سابقاً. يستضيف الاختبار BorrowView
/// فعلياً داخل Window حقيقية (وليس فقط Measure/Arrange) للتأكد من أن الجدول يبقى
/// ظاهراً دائماً، وأن فتح/إغلاق BorrowFormWindow يعكس فعلياً حالة IsEditing.
/// </summary>
public class BorrowViewOverlayTests
{
    private static void RunInSta(System.Action action) => Sources.Tests.Fixtures.WpfStaFixture.RunInSta(action);

    // يُستخدم Fixture حقيقي (SQLite In-Memory مع المخطط الكامل عبر Migrations) بدلاً من ترك
    // BorrowViewModel يعتمد على App.ServiceProvider الافتراضي، لأن أمر AddNewCommand يستدعي
    // LoadAvailableSources() التي تفتح DbContext فعلياً؛ الاعتماد على الاسترجاع الضمني
    // (fallback) يجعل النتيجة عرضة لتلوث الحالة الساكنة بين الاختبارات عند تشغيل الحزمة كاملة.
    private static BorrowViewModel CreateViewModel(SqliteInMemoryFixture fixture)
    {
        var mockBorrowService = new Mock<IBorrowService>();
        var mockSourceService = new Mock<ISourceService>();
        var mockUserService = new Mock<IUserService>();
        var mockReportingService = new Mock<IReportingService>();
        mockBorrowService.Setup(s => s.GetAll()).Returns(new List<BorrowRequest>());
        return new BorrowViewModel(mockBorrowService.Object, mockSourceService.Object, mockUserService.Object, mockReportingService.Object, fixture.ContextFactory);
    }

    [Fact]
    public void BorrowView_HostedInRealWindow_DataGridAlwaysVisible_RegardlessOfIsEditing()
    {
        RunInSta(() =>
        {
            using var fixture = new SqliteInMemoryFixture();
            var vm = CreateViewModel(fixture);
            try
            {
                var view = new BorrowView { DataContext = vm };

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
    public void BorrowView_AddNewCommand_OpensBorrowFormWindow_AndDataGridStaysVisibleUnderneath()
    {
        RunInSta(() =>
        {
            using var fixture = new SqliteInMemoryFixture();
            var vm = CreateViewModel(fixture);
            try
            {
                var view = new BorrowView { DataContext = vm };

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

                    // ShowDialog() الذي يستدعيه BorrowView.xaml.cs عند IsEditing=true يحجب
                    // مسار التنفيذ الحالي بمضخة رسائل متداخلة (nested message pump) خاصة به.
                    // لذا يُجدوَل التحقق والإغلاق عبر Dispatcher.BeginInvoke قبل استدعاء الأمر،
                    // فتُنفَّذ هذه الخطوة أثناء تشغيل حلقة ShowDialog() المتداخلة نفسها، لا بعدها.
                    BorrowFormWindow? capturedFormWindow = null;
                    Dispatcher.CurrentDispatcher.BeginInvoke(new System.Action(() =>
                    {
                        capturedFormWindow = Application.Current.Windows
                            .OfType<BorrowFormWindow>()
                            .FirstOrDefault();

                        Assert.NotNull(capturedFormWindow);
                        Assert.True(vm.IsEditing);
                        Assert.Equal(Visibility.Visible, dataGrid!.Visibility);

                        vm.CancelEditCommand.Execute(null);
                    }), DispatcherPriority.ApplicationIdle);

                    vm.AddNewCommand.Execute(null);

                    // بعد عودة AddNewCommand.Execute، تكون BorrowFormWindow قد أُغلقت فعلاً
                    // (CancelEditCommand أعاد IsEditing إلى false فأغلق الكود-خلف النافذة).
                    Assert.False(vm.IsEditing);
                    Assert.DoesNotContain(capturedFormWindow, Application.Current.Windows.OfType<BorrowFormWindow>());
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
    public void BorrowFormWindow_ClosedViaNativeCloseButton_ResetsIsEditing_AndAllowsReopening()
    {
        RunInSta(() =>
        {
            using var fixture = new SqliteInMemoryFixture();
            var vm = CreateViewModel(fixture);
            try
            {
                var view = new BorrowView { DataContext = vm };

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

                    // الخطوة 1: فتح BorrowFormWindow عبر AddNewCommand، ثم محاكاة الإغلاق عبر
                    // زر ✕ الأصلي (Close() مباشرة على النافذة نفسها — وليس عبر CancelEditCommand)،
                    // بنفس أسلوب الجدولة عبر Dispatcher.BeginInvoke المستخدم في الاختبار الآخر.
                    Dispatcher.CurrentDispatcher.BeginInvoke(new System.Action(() =>
                    {
                        var formWindow = Application.Current.Windows
                            .OfType<BorrowFormWindow>()
                            .FirstOrDefault();

                        Assert.NotNull(formWindow);
                        Assert.True(vm.IsEditing);

                        // محاكاة إغلاق عبر ✕ / Alt+F4: استدعاء Close() مباشرة على النافذة،
                        // وليس عبر CancelEditCommand.
                        formWindow!.Close();
                    }), DispatcherPriority.ApplicationIdle);

                    vm.AddNewCommand.Execute(null);

                    // بعد الإغلاق عبر ✕، يجب أن تُصفَّر IsEditing تلقائياً عبر معالج Closing
                    // في BorrowFormWindow (الذي يستدعي CancelEditCommand داخلياً)، دون أن يحتاج
                    // المستخدم لاستدعاء زر الإلغاء يدوياً.
                    Assert.False(vm.IsEditing);
                    Assert.Empty(Application.Current.Windows.OfType<BorrowFormWindow>());

                    // الخطوة 2: التأكد من أن نافذة جديدة فعلاً تُفتح عند استدعاء AddNewCommand
                    // مرة أخرى (وليس لا شيء بسبب مرجع نافذة سابق عالق يمنع الفتح).
                    BorrowFormWindow? secondFormWindow = null;
                    Dispatcher.CurrentDispatcher.BeginInvoke(new System.Action(() =>
                    {
                        secondFormWindow = Application.Current.Windows
                            .OfType<BorrowFormWindow>()
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
}
