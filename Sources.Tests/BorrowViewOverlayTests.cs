using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
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
/// اختبار انحدار تكاملي (الجولة 143) يتحقق من أن نموذج الاستعارة (BorrowView)
/// أصبح تراكباً منبثقاً (In-View Modal Overlay) فوق الجدول بدلاً من استبداله،
/// بنفس أسلوب الجولات 139/140/141/142 الخاصة بـ NeutronSourceTypesWindow و LocationsView
/// و RadioisotopesView. يستضيف الاختبار BorrowView فعلياً داخل Window حقيقية
/// (وليس فقط Measure/Arrange) للتأكد من أن الـ Visibility الفعلي في شجرة العرض يطابق التوقع.
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

    private static T? FindFirstVisualChild<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed) return typed;
            var nested = FindFirstVisualChild<T>(child);
            if (nested != null) return nested;
        }
        return null;
    }

    private static System.Windows.Controls.Grid FindOverlayGrid(DependencyObject root)
    {
        var found = FindOverlayGridOrNull(root);
        if (found == null)
        {
            throw new Xunit.Sdk.XunitException("لم يتم العثور على Grid التراكب (Panel.ZIndex=1000) في شجرة BorrowView.");
        }
        return found;
    }

    private static System.Windows.Controls.Grid? FindOverlayGridOrNull(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is System.Windows.Controls.Grid grid && System.Windows.Controls.Panel.GetZIndex(grid) == 1000)
            {
                return grid;
            }
            var nested = FindOverlayGridOrNull(child);
            if (nested != null) return nested;
        }
        return null;
    }

    [Fact]
    public void BorrowView_HostedInRealWindow_WhenNotEditing_TableVisibleAndOverlayCollapsed()
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

                    var overlay = FindOverlayGrid(view);
                    Assert.Equal(Visibility.Collapsed, overlay.Visibility);
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
    public void BorrowView_HostedInRealWindow_WhenEditing_OverlayVisibleAndTableStaysVisibleUnderneath()
    {
        RunInSta(() =>
        {
            using var fixture = new SqliteInMemoryFixture();
            var vm = CreateViewModel(fixture);
            try
            {
                vm.AddNewCommand.Execute(null);
                Assert.True(vm.IsEditing);

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

                    var overlay = FindOverlayGrid(view);
                    Assert.Equal(Visibility.Visible, overlay.Visibility);

                    var dataGrid = FindFirstVisualChild<System.Windows.Controls.DataGrid>(view);
                    Assert.NotNull(dataGrid);
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
}
