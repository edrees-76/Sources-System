using System;
using System.Collections.Generic;
using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using Moq;
using Sources.Interfaces;
using Sources.Messages;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Sources.Views;
using Xunit;

namespace Sources.Tests;

public class Round206IDisposableTests
{
    [Fact]
    public void SettingsViewModel_Dispose_UnsubscribesFromAutoBackupCompleted()
    {
        var mockBackupService = new Mock<IBackupService>();
        var mockSettingsService = new Mock<ISystemSettingsService>();
        var mockAutoBackupService = new Mock<IAutoBackupService>();

        var vm = new SettingsViewModel(
            mockBackupService.Object,
            mockSettingsService.Object,
            mockAutoBackupService.Object);

        // Act: Dispose the ViewModel
        vm.Dispose();

        // Verify that raising BackupCompleted does not invoke handler or cause issues
        mockAutoBackupService.Raise(s => s.BackupCompleted += null, EventArgs.Empty);
    }

    [Fact]
    public void LeakTestsViewModel_Dispose_UnregistersFromMessenger()
    {
        var messenger = new WeakReferenceMessenger();
        var mockLeakService = new Mock<ILeakTestService>();
        var mockSourceService = new Mock<ISourceService>();
        var mockReportingService = new Mock<IReportingService>();
        var mockUserService = new Mock<IUserService>();
        var mockSettingsService = new Mock<ISystemSettingsService>();

        mockLeakService.Setup(s => s.GetAllRecords(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>())).Returns(new List<LeakTestRecord>());
        mockSourceService.Setup(s => s.GetAllSources()).Returns(new List<Source>());

        var vm = new LeakTestsViewModel(
            mockLeakService.Object,
            mockSourceService.Object,
            mockReportingService.Object,
            mockUserService.Object,
            mockSettingsService.Object,
            messenger: messenger);

        // Before Dispose, recipient is registered
        Assert.True(messenger.IsRegistered<SourcesUpdatedMessage>(vm));

        // Act
        vm.Dispose();

        // After Dispose, recipient is unregistered
        Assert.False(messenger.IsRegistered<SourcesUpdatedMessage>(vm));
    }

    [Fact]
    public void SourcesViewModel_Dispose_UnregistersFromMessenger()
    {
        var messenger = new WeakReferenceMessenger();
        var mockSourceService = new Mock<ISourceService>();
        var mockIsotopeService = new Mock<IRadioisotopeService>();
        var mockLocationService = new Mock<ILocationService>();
        var mockReportingService = new Mock<IReportingService>();
        var mockDecayService = new Mock<IDecayCalculationService>();
        var mockNeutronService = new Mock<INeutronSourceService>();
        var mockNeutronTypeService = new Mock<INeutronSourceTypeService>();
        var mockNeutronDecay = new Mock<INeutronDecayCalculationService>();

        mockSourceService.Setup(s => s.GetAllSources()).Returns(new List<Source>());
        mockIsotopeService.Setup(s => s.GetAll()).Returns(new List<Radioisotope>());
        mockLocationService.Setup(s => s.GetAll()).Returns(new List<Location>());
        mockNeutronService.Setup(s => s.GetAll()).Returns(new List<NeutronSource>());
        mockNeutronTypeService.Setup(s => s.GetAll()).Returns(new List<NeutronSourceType>());

        var vm = new SourcesViewModel(
            mockSourceService.Object,
            mockIsotopeService.Object,
            mockLocationService.Object,
            mockReportingService.Object,
            mockDecayService.Object,
            mockNeutronService.Object,
            mockNeutronTypeService.Object,
            mockNeutronDecay.Object,
            messenger: messenger);

        Assert.True(messenger.IsRegistered<NavigateToSearchResultMessage>(vm));

        // Act
        vm.Dispose();

        // Assert
        Assert.False(messenger.IsRegistered<NavigateToSearchResultMessage>(vm));
    }

    [Fact]
    public void UsersViewModel_Dispose_UnregistersFromMessenger()
    {
        var messenger = new WeakReferenceMessenger();
        var mockUserService = new Mock<IUserService>();
        var mockReportingService = new Mock<IReportingService>();

        mockUserService.Setup(s => s.GetAllUsers()).Returns(new List<User>());
        mockUserService.Setup(s => s.GetAllRoles()).Returns(new List<Role>());
        mockUserService.Setup(s => s.GetAuditLogs(It.IsAny<Guid?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>())).Returns(new List<AuditLog>());

        var vm = new UsersViewModel(
            mockUserService.Object,
            mockReportingService.Object,
            messenger: messenger);

        Assert.True(messenger.IsRegistered<NavigateToSearchResultMessage>(vm));

        // Act
        vm.Dispose();

        // Assert
        Assert.False(messenger.IsRegistered<NavigateToSearchResultMessage>(vm));
    }

    [Fact]
    public void LocationsViewModel_Dispose_UnregistersFromMessenger()
    {
        var messenger = new WeakReferenceMessenger();
        var mockLocationService = new Mock<ILocationService>();
        mockLocationService.Setup(s => s.GetAll()).Returns(new List<Location>());

        var vm = new LocationsViewModel(
            mockLocationService.Object,
            messenger: messenger);

        Assert.True(messenger.IsRegistered<NavigateToSearchResultMessage>(vm));

        // Act
        vm.Dispose();

        // Assert
        Assert.False(messenger.IsRegistered<NavigateToSearchResultMessage>(vm));
    }

    [Fact]
    public void RadioisotopesViewModel_Dispose_UnregistersFromMessenger()
    {
        var messenger = new WeakReferenceMessenger();
        var mockIsotopeService = new Mock<IRadioisotopeService>();
        mockIsotopeService.Setup(s => s.GetAll()).Returns(new List<Radioisotope>());

        var vm = new RadioisotopesViewModel(
            mockIsotopeService.Object,
            messenger: messenger);

        Assert.True(messenger.IsRegistered<NavigateToSearchResultMessage>(vm));

        // Act
        vm.Dispose();

        // Assert
        Assert.False(messenger.IsRegistered<NavigateToSearchResultMessage>(vm));
    }

    [Fact]
    public void DashboardView_Unloaded_UnregistersFocusDashboardSearchMessage()
    {
        Fixtures.WpfStaFixture.RunInSta(() =>
        {
            var view = new DashboardView();

            // Registered on construction
            Assert.True(WeakReferenceMessenger.Default.IsRegistered<FocusDashboardSearchMessage>(view));

            // Act: Raise Unloaded
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

            // Assert: Unregistered after Unloaded
            Assert.False(WeakReferenceMessenger.Default.IsRegistered<FocusDashboardSearchMessage>(view));
        });
    }
}
