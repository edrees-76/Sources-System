using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Sources.Views;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// الجولة 152: اختبارات انحدارية تثبت أن بانر الرسائل (Message/HasMessage) أصبح مربوطاً فعلياً
/// في XAML لكل شاشة اكتُشف فيها الخلل (UsersView وSourcesView وRadioisotopesView وAlertsView)،
/// عبر استضافة الـView داخل Window حقيقية (Show + UpdateLayout)، لا فحص XAML الساكن فقط —
/// بنفس النمط المعتمد في SourcesViewNeutronOverlayTests.cs.
/// </summary>
public class MessageBannerBindingTests
{
    private static void RunInSta(Action action) => Sources.Tests.Fixtures.WpfStaFixture.RunInSta(action);

    private static Window CreateHiddenWindow(UserControl view) => new()
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

    /// <summary>يُفرِّغ طابور Dispatcher حتى أولوية Background مرة واحدة فقط.</summary>
    private static void DrainDispatcherOnce()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            frame.Continue = false;
        }));
        Dispatcher.PushFrame(frame);
    }

    /// <summary>
    /// ينتظر اكتمال التحميل غير المتزامن لـ SourcesViewModel (مُنشئها يستدعي LoadDataAsync()
    /// بأسلوب fire-and-forget) — بنفس نمط SourcesViewNeutronOverlayTests.WaitForSourcesLoaded.
    /// </summary>
    private static void WaitForSourcesLoaded(SourcesViewModel vm)
    {
        const int maxIterations = 200;
        for (int i = 0; i < maxIterations && vm.Sources.Count == 0; i++)
        {
            DrainDispatcherOnce();
            System.Threading.Thread.Sleep(10);
        }
    }

    private static void AssertBannerHiddenThenVisibleWithText(UserControl view, Window window, Action setMessage, string expectedText)
    {
        window.UpdateLayout();

        var banner = view.FindName("MessageBanner") as Border;
        var bannerText = view.FindName("MessageBannerText") as TextBlock;
        Assert.NotNull(banner);
        Assert.NotNull(bannerText);

        // قبل تعيين أي رسالة: HasMessage تبدأ false افتراضياً، فالبانر يجب أن يكون مخفياً.
        Assert.Equal(Visibility.Collapsed, banner!.Visibility);

        setMessage();
        window.UpdateLayout();

        Assert.Equal(Visibility.Visible, banner.Visibility);
        Assert.Equal(expectedText, bannerText!.Text);
    }

    [Fact]
    public void UsersView_MessageBanner_BecomesVisibleWithCorrectText_WhenHasMessageTrue()
    {
        RunInSta(() =>
        {
            var mockUserService = new Mock<IUserService>();
            mockUserService.Setup(s => s.GetAllUsers()).Returns(new List<User>());
            mockUserService.Setup(s => s.GetAllRoles()).Returns(new List<Role>());
            mockUserService.Setup(s => s.GetAuditLogs(null, null, null)).Returns(new List<AuditLog>());
            var mockReportingService = new Mock<IReportingService>();

            var vm = new UsersViewModel(mockUserService.Object, mockReportingService.Object);
            try
            {
                var view = new UsersView { DataContext = vm };
                var window = CreateHiddenWindow(view);
                window.Show();
                try
                {
                    AssertBannerHiddenThenVisibleWithText(view, window, () =>
                    {
                        vm.Message = "تم تحديث بيانات المستخدم";
                        vm.HasMessage = true;
                    }, "تم تحديث بيانات المستخدم");
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
    public void SourcesView_MessageBanner_BecomesVisibleWithCorrectText_WhenHasMessageTrue()
    {
        RunInSta(() =>
        {
            var mockSourceService = new Mock<ISourceService>();
            var mockIsotopeService = new Mock<IRadioisotopeService>();
            var mockLocationService = new Mock<ILocationService>();
            var mockReportingService = new Mock<IReportingService>();
            var mockNeutronSourceService = new Mock<INeutronSourceService>();
            var mockNeutronSourceTypeService = new Mock<INeutronSourceTypeService>();

            mockSourceService.Setup(s => s.GetAllSources()).Returns(new List<Source>
            {
                new Source { SourceCode = "TEST-SRC-152" }
            });
            mockSourceService.Setup(s => s.GetDeletedSources()).Returns(new List<Source>());
            mockIsotopeService.Setup(s => s.GetAll()).Returns(new List<Radioisotope>());
            mockLocationService.Setup(s => s.GetAll()).Returns(new List<Location>());
            mockNeutronSourceTypeService.Setup(s => s.GetAll()).Returns(new List<NeutronSourceType>());

            var vm = new SourcesViewModel(
                mockSourceService.Object,
                mockIsotopeService.Object,
                mockLocationService.Object,
                mockReportingService.Object,
                null,
                mockNeutronSourceService.Object,
                mockNeutronSourceTypeService.Object);
            try
            {
                var view = new SourcesView { DataContext = vm };
                var window = CreateHiddenWindow(view);
                window.Show();
                try
                {
                    WaitForSourcesLoaded(vm);
                    AssertBannerHiddenThenVisibleWithText(view, window, () =>
                    {
                        vm.Message = "تم حفظ المصدر بنجاح";
                        vm.HasMessage = true;
                    }, "تم حفظ المصدر بنجاح");
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
    public void RadioisotopesView_MessageBanner_BecomesVisibleWithCorrectText_WhenHasMessageTrue()
    {
        RunInSta(() =>
        {
            var mockService = new Mock<IRadioisotopeService>();
            mockService.Setup(s => s.GetAll()).Returns(new List<Radioisotope>());

            var vm = new RadioisotopesViewModel(mockService.Object);
            try
            {
                var view = new RadioisotopesView { DataContext = vm };
                var window = CreateHiddenWindow(view);
                window.Show();
                try
                {
                    AssertBannerHiddenThenVisibleWithText(view, window, () =>
                    {
                        vm.Message = "تم حذف النظير بنجاح";
                        vm.HasMessage = true;
                    }, "تم حذف النظير بنجاح");
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
    public void AlertsView_MessageBanner_BecomesVisibleWithCorrectText_WhenHasMessageTrue()
    {
        RunInSta(() =>
        {
            var mockAlertService = new Mock<IAlertService>();
            var mockLocationService = new Mock<ILocationService>();
            mockAlertService.Setup(s => s.GetAllAlerts(true)).Returns(new List<AlertNotification>());
            mockLocationService.Setup(s => s.GetAll()).Returns(new List<Location>());

            var vm = new AlertsViewModel(mockAlertService.Object, mockLocationService.Object);
            try
            {
                var view = new AlertsView { DataContext = vm };
                var window = CreateHiddenWindow(view);
                window.Show();
                try
                {
                    AssertBannerHiddenThenVisibleWithText(view, window, () =>
                    {
                        vm.Message = "تم تعليم كل التنبيهات كمقروءة";
                        vm.HasMessage = true;
                    }, "تم تعليم كل التنبيهات كمقروءة");
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
