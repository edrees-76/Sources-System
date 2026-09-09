using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Messaging;
using Moq;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Sources.Views;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// اختبار انحدار تكاملي (الجولة 141) يتحقق من أن نموذج تحرير الموقع (LocationsView)
/// أصبح تراكباً منبثقاً (In-View Modal Overlay) فوق الجدول بدلاً من استبداله،
/// بنفس أسلوب الجولة 139/140 الخاص بـ NeutronSourceTypesWindow.
/// يستضيف الاختبار LocationsView فعلياً داخل Window حقيقية (وليس فقط Measure/Arrange)
/// للتأكد من أن الـ Visibility الفعلي في شجرة العرض يطابق التوقع.
/// </summary>
public class LocationsViewOverlayTests
{
    private static void RunInSta(System.Action action) => Sources.Tests.Fixtures.WpfStaFixture.RunInSta(action);

    private static LocationsViewModel CreateViewModel()
    {
        var mockService = new Mock<ILocationService>();
        mockService.Setup(s => s.GetAll()).Returns(new List<Location>());
        return new LocationsViewModel(mockService.Object);
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

    private static Grid FindOverlayGrid(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Grid grid && System.Windows.Controls.Panel.GetZIndex(grid) == 1000)
            {
                return grid;
            }
            var nested = FindOverlayGridOrNull(child);
            if (nested != null) return nested;
        }
        throw new Xunit.Sdk.XunitException("لم يتم العثور على Grid التراكب (Panel.ZIndex=1000) في شجرة LocationsView.");
    }

    private static Grid? FindOverlayGridOrNull(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Grid grid && System.Windows.Controls.Panel.GetZIndex(grid) == 1000)
            {
                return grid;
            }
            var nested = FindOverlayGridOrNull(child);
            if (nested != null) return nested;
        }
        return null;
    }

    [Fact]
    public void LocationsView_HostedInRealWindow_WhenNotEditing_TableVisibleAndOverlayCollapsed()
    {
        RunInSta(() =>
        {
            var vm = CreateViewModel();
            try
            {
                var view = new LocationsView { DataContext = vm };

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
    public void LocationsView_HostedInRealWindow_WhenEditing_OverlayVisibleAndTableStaysVisibleUnderneath()
    {
        RunInSta(() =>
        {
            var vm = CreateViewModel();
            try
            {
                vm.AddNewCommand.Execute(null);
                Assert.True(vm.IsEditing);

                var view = new LocationsView { DataContext = vm };

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
