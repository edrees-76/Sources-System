using System;
using System.Collections.Generic;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Sources.Interfaces;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fakes;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

public class MainViewModelLifecycleTests : IDisposable
{
    private readonly Mock<IUserService> _mockUserService;
    private readonly Mock<IAlertService> _mockAlertService;
    private readonly Mock<ISystemSettingsService> _mockSettingsService;
    private readonly IServiceProvider? _originalProvider;

    public MainViewModelLifecycleTests()
    {
        _mockUserService = new Mock<IUserService>();
        _mockAlertService = new Mock<IAlertService>();
        _mockSettingsService = new Mock<ISystemSettingsService>();

        _mockUserService.Setup(u => u.IsLoggedIn).Returns(true);
        _originalProvider = App.ServiceProvider;
    }

    private static void SetAppServiceProvider(IServiceProvider? provider)
    {
        typeof(App).GetProperty("ServiceProvider", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, provider);
    }

    private MainViewModel CreateViewModel()
    {
        return new MainViewModel(_mockUserService.Object, _mockAlertService.Object, _mockSettingsService.Object, new FakeLicenseService());
    }

    [Fact]
    public void NavigateTo_WhenCurrentViewIsDisposable_CallsDisposeOnOldView()
    {
        using var mainVm = CreateViewModel();
        var disposableOldView = new DisposableMockView();
        mainVm.CurrentView = disposableOldView;

        var services = new ServiceCollection();
        var dummyNewView = new DummyMockView();
        services.AddTransient(_ => dummyNewView);
        SetAppServiceProvider(services.BuildServiceProvider());

        try
        {
            mainVm.NavigateTo("Sources");

            // Assert: old view was disposed
            Assert.True(disposableOldView.IsDisposed, "The previous view should have been disposed upon navigation.");
        }
        finally
        {
            SetAppServiceProvider(_originalProvider);
        }
    }

    [Fact]
    public void MainViewModel_Dispose_DisposesCurrentViewAndClearsReference()
    {
        var mainVm = CreateViewModel();
        var disposableCurrentView = new DisposableMockView();
        mainVm.CurrentView = disposableCurrentView;

        // Act
        mainVm.Dispose();

        // Assert
        Assert.True(disposableCurrentView.IsDisposed, "MainViewModel.Dispose() should dispose CurrentView.");
        Assert.Null(mainVm.CurrentView);
    }

    [Fact]
    public void ForceLogout_DisposesCurrentViewAndClearsReference()
    {
        Fixtures.WpfStaFixture.RunInSta(() =>
        {
            using var mainVm = CreateViewModel();
            var disposableCurrentView = new DisposableMockView();
            mainVm.CurrentView = disposableCurrentView;

            // Act
            mainVm.ForceLogout();

            // Assert
            Assert.True(disposableCurrentView.IsDisposed, "ForceLogout() should dispose CurrentView.");
            Assert.Null(mainVm.CurrentView);
        });
    }

    [Fact]
    public void NavigateTo_FromDashboardToAnotherView_StopsAndNullsDashboardTimers()
    {
        Fixtures.WpfStaFixture.RunInSta(() =>
        {
            using var mainVm = CreateViewModel();

            var mockSourceService = new Mock<ISourceService>();
            mockSourceService.Setup(s => s.GetAllSources()).Returns(new List<Source>());
            var mockIsotopeService = new Mock<IRadioisotopeService>();
            mockIsotopeService.Setup(s => s.GetAll()).Returns(new List<Radioisotope>());
            var mockLocationService = new Mock<ILocationService>();
            mockLocationService.Setup(s => s.GetAll()).Returns(new List<Location>());
            var mockDecayService = new Mock<IDecayCalculationService>();
            var mockBorrowService = new Mock<IBorrowService>();
            var mockGlobalSearch = new Mock<IGlobalSearchService>();

            var dashboardVm = new DashboardViewModel(
                mockSourceService.Object,
                mockIsotopeService.Object,
                mockLocationService.Object,
                mockDecayService.Object,
                mockBorrowService.Object,
                _mockSettingsService.Object,
                _mockAlertService.Object,
                mockGlobalSearch.Object);

            var clockField = typeof(DashboardViewModel).GetField("_clockTimer", BindingFlags.NonPublic | BindingFlags.Instance);
            var debounceField = typeof(DashboardViewModel).GetField("_searchDebounceTimer", BindingFlags.NonPublic | BindingFlags.Instance);

            // Verify clock timer was started
            var clockTimerBefore = clockField?.GetValue(dashboardVm);
            Assert.NotNull(clockTimerBefore);

            // Act: Place DashboardViewModel as CurrentView
            mainVm.CurrentView = dashboardVm;

            // Set up DI provider for next view
            var services = new ServiceCollection();
            var dummyNewView = new DummyMockView();
            services.AddTransient(_ => dummyNewView);
            SetAppServiceProvider(services.BuildServiceProvider());

            try
            {
                // Navigate away from Dashboard to Sources
                mainVm.NavigateTo("Sources");

                // Assert: DashboardViewModel.Dispose() was called by NavigateTo
                var clockTimerAfter = clockField?.GetValue(dashboardVm);
                var debounceTimerAfter = debounceField?.GetValue(dashboardVm);

                Assert.Null(clockTimerAfter);
                Assert.Null(debounceTimerAfter);
            }
            finally
            {
                SetAppServiceProvider(_originalProvider);
                dashboardVm.Dispose();
            }
        });
    }

    public void Dispose()
    {
        SetAppServiceProvider(_originalProvider);
    }

    private sealed partial class DisposableMockView : ObservableObject, IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed partial class DummyMockView : ObservableObject
    {
    }
}
