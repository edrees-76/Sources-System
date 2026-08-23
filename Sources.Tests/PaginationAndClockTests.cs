using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

public class PaginationAndClockTests
{
    [Fact]
    public void DashboardViewModel_Clock_InitializesAndUpdatesProperly()
    {
        // Arrange
        var mockSourceService = new Mock<ISourceService>();
        var mockIsotopeService = new Mock<IRadioisotopeService>();
        var mockLocationService = new Mock<ILocationService>();
        var mockDecayService = new Mock<IDecayCalculationService>();
        var mockBorrowService = new Mock<IBorrowService>();
        var mockSettingsService = new Mock<ISystemSettingsService>();

        mockSourceService.Setup(s => s.GetAllSources()).Returns(new List<Source>());

        var vm = new DashboardViewModel(
            mockSourceService.Object,
            mockIsotopeService.Object,
            mockLocationService.Object,
            mockDecayService.Object,
            mockBorrowService.Object,
            mockSettingsService.Object);

        // Act
        vm.UpdateClock();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(vm.CurrentDateDisplay));
        Assert.False(string.IsNullOrWhiteSpace(vm.CurrentTimeDisplay));

        // Cleanup
        vm.Dispose();
    }

    [Fact]
    public async Task DashboardViewModel_Pagination_FirstAndLastPageCommands_And_CanExecute()
    {
        // Arrange
        var mockSourceService = new Mock<ISourceService>();
        var mockIsotopeService = new Mock<IRadioisotopeService>();
        var mockLocationService = new Mock<ILocationService>();
        var mockDecayService = new Mock<IDecayCalculationService>();
        var mockBorrowService = new Mock<IBorrowService>();
        var mockSettingsService = new Mock<ISystemSettingsService>();

        var sources = new List<Source>();
        for (int i = 1; i <= 65; i++)
        {
            sources.Add(new Source
            {
                Id = Guid.NewGuid(),
                SourceCode = $"SRC-{i:D3}",
                Status = "InUse"
            });
        }
        mockSourceService.Setup(s => s.GetAllSources()).Returns(sources);

        var vm = new DashboardViewModel(
            mockSourceService.Object,
            mockIsotopeService.Object,
            mockLocationService.Object,
            mockDecayService.Object,
            mockBorrowService.Object,
            mockSettingsService.Object);

        await vm.LoadDataAsync();
        vm.PageSize = 20; // 65 items / 20 = 4 pages
        vm.ApplyFiltersAndPagination();

        // Assert Page 1 State
        Assert.Equal(1, vm.CurrentPage);
        Assert.Equal(4, vm.TotalPages);
        Assert.False(vm.FirstPageCommand.CanExecute(null));
        Assert.False(vm.PreviousPageCommand.CanExecute(null));
        Assert.True(vm.NextPageCommand.CanExecute(null));
        Assert.True(vm.LastPageCommand.CanExecute(null));

        // Act - Last Page
        vm.LastPageCommand.Execute(null);

        // Assert Last Page State
        Assert.Equal(4, vm.CurrentPage);
        Assert.True(vm.FirstPageCommand.CanExecute(null));
        Assert.True(vm.PreviousPageCommand.CanExecute(null));
        Assert.False(vm.NextPageCommand.CanExecute(null));
        Assert.False(vm.LastPageCommand.CanExecute(null));

        // Act - First Page
        vm.FirstPageCommand.Execute(null);

        // Assert First Page State
        Assert.Equal(1, vm.CurrentPage);
        Assert.False(vm.FirstPageCommand.CanExecute(null));
        Assert.False(vm.PreviousPageCommand.CanExecute(null));
        Assert.True(vm.NextPageCommand.CanExecute(null));
        Assert.True(vm.LastPageCommand.CanExecute(null));

        vm.Dispose();
    }

    [Fact]
    public void AlertsViewModel_Pagination_FirstAndLastPageCommands_And_CanExecute()
    {
        // Arrange
        var mockAlertService = new Mock<IAlertService>();
        var mockLocationService = new Mock<ILocationService>();
        var alerts = new List<AlertNotification>();
        for (int i = 1; i <= 55; i++)
        {
            alerts.Add(new AlertNotification
            {
                Id = Guid.NewGuid(),
                Source = new Source { Id = Guid.NewGuid(), SourceCode = $"SRC-{i:D3}" },
                Message = $"Alert {i}",
                Severity = "Warning",
                AlertType = "LowActivity",
                IsDismissed = false
            });
        }
        mockAlertService.Setup(s => s.GetAllAlerts(true)).Returns(alerts);

        var vm = new AlertsViewModel(mockAlertService.Object, mockLocationService.Object);
        vm.PageSize = 15; // 55 items / 15 = 4 pages
        vm.ApplyFiltersAndPagination();

        // Assert Page 1
        Assert.Equal(1, vm.CurrentPage);
        Assert.Equal(4, vm.TotalPages);
        Assert.False(vm.FirstPageCommand.CanExecute(null));
        Assert.False(vm.PreviousPageCommand.CanExecute(null));
        Assert.True(vm.NextPageCommand.CanExecute(null));
        Assert.True(vm.LastPageCommand.CanExecute(null));

        // Act - Go To Last Page
        vm.LastPageCommand.Execute(null);
        Assert.Equal(4, vm.CurrentPage);
        Assert.True(vm.FirstPageCommand.CanExecute(null));
        Assert.True(vm.PreviousPageCommand.CanExecute(null));
        Assert.False(vm.NextPageCommand.CanExecute(null));
        Assert.False(vm.LastPageCommand.CanExecute(null));

        // Act - Go To First Page
        vm.FirstPageCommand.Execute(null);
        Assert.Equal(1, vm.CurrentPage);
        Assert.False(vm.FirstPageCommand.CanExecute(null));
        Assert.False(vm.PreviousPageCommand.CanExecute(null));
        Assert.True(vm.NextPageCommand.CanExecute(null));
        Assert.True(vm.LastPageCommand.CanExecute(null));
    }

    [Fact]
    public async Task LeakTestsViewModel_Pagination_FirstAndLastPageCommands_And_CanExecute()
    {
        // Arrange
        var mockLeakTestService = new Mock<ILeakTestService>();
        var mockSourceService = new Mock<ISourceService>();
        var mockReportingService = new Mock<IReportingService>();
        var mockUserService = new Mock<IUserService>();
        var mockSettingsService = new Mock<ISystemSettingsService>();

        var records = new List<LeakTestRecord>();
        for (int i = 1; i <= 45; i++)
        {
            records.Add(new LeakTestRecord
            {
                Id = Guid.NewGuid(),
                TestDate = DateTime.Now.AddDays(-i),
                Result = "Pass"
            });
        }
        mockLeakTestService.Setup(s => s.GetAllRecords(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(records);

        var vm = new LeakTestsViewModel(
            mockLeakTestService.Object,
            mockSourceService.Object,
            mockReportingService.Object,
            mockUserService.Object,
            mockSettingsService.Object);

        vm.PageSize = 10; // 45 records / 10 = 5 pages
        await vm.LoadDataAsync();

        // Assert Page 1
        Assert.Equal(1, vm.CurrentPage);
        Assert.Equal(5, vm.TotalPages);
        Assert.False(vm.FirstPageCommand.CanExecute(null));
        Assert.False(vm.PreviousPageCommand.CanExecute(null));
        Assert.True(vm.NextPageCommand.CanExecute(null));
        Assert.True(vm.LastPageCommand.CanExecute(null));

        // Act - Last Page
        vm.LastPageCommand.Execute(null);
        Assert.Equal(5, vm.CurrentPage);
        Assert.True(vm.FirstPageCommand.CanExecute(null));
        Assert.True(vm.PreviousPageCommand.CanExecute(null));
        Assert.False(vm.NextPageCommand.CanExecute(null));
        Assert.False(vm.LastPageCommand.CanExecute(null));

        // Act - First Page
        vm.FirstPageCommand.Execute(null);
        Assert.Equal(1, vm.CurrentPage);
        Assert.False(vm.FirstPageCommand.CanExecute(null));
        Assert.False(vm.PreviousPageCommand.CanExecute(null));
        Assert.True(vm.NextPageCommand.CanExecute(null));
        Assert.True(vm.LastPageCommand.CanExecute(null));
    }
}
