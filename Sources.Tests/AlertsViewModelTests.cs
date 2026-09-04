using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Sources.Helpers;
using Sources.Messages;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

public class AlertsViewModelTests : IDisposable
{
    private readonly Mock<IAlertService> _mockAlertService;
    private readonly Mock<ILocationService> _mockLocationService;

    private readonly List<AlertNotification> _testAlerts;
    private readonly List<Location> _testLocations;

    public AlertsViewModelTests()
    {
        _mockAlertService = new Mock<IAlertService>();
        _mockLocationService = new Mock<ILocationService>();

        var loc1 = new Location { Id = Guid.NewGuid(), LocationName = "المستودع الرئيسي" };
        var loc2 = new Location { Id = Guid.NewGuid(), LocationName = "مختبر المعايرة" };
        _testLocations = new List<Location> { loc1, loc2 };

        var isoCo60 = new Radioisotope { Id = Guid.NewGuid(), Symbol = "Co-60", Name = "Cobalt-60", HalfLife = 5.27, HalfLifeUnit = "years" };
        var isoCs137 = new Radioisotope { Id = Guid.NewGuid(), Symbol = "Cs-137", Name = "Cesium-137", HalfLife = 30.08, HalfLifeUnit = "years" };

        var src1 = new Source
        {
            Id = Guid.NewGuid(),
            SourceCode = "SRC-001",
            Radioisotope = isoCo60,
            RadioisotopeId = isoCo60.Id,
            Location = loc1,
            LocationId = loc1.Id,
            CalibrationDate = DateTime.Now.AddDays(-365.25 * 5.27 * 6.5) // ~6.5 T1/2 -> Critical
        };

        var src2 = new Source
        {
            Id = Guid.NewGuid(),
            SourceCode = "SRC-002",
            Radioisotope = isoCs137,
            RadioisotopeId = isoCs137.Id,
            Location = loc2,
            LocationId = loc2.Id,
            CalibrationDate = DateTime.Now.AddDays(-365.25 * 30.08 * 5.2) // ~5.2 T1/2 -> Warning
        };

        _testAlerts = new List<AlertNotification>
        {
            new AlertNotification
            {
                Id = Guid.NewGuid(),
                SourceId = src1.Id,
                Source = src1,
                AlertType = "LowActivity",
                Severity = "Critical",
                Message = "انخفض نشاط المصدر SRC-001 إلى مستوى حرج",
                CreatedAt = DateTime.Today.AddDays(-2),
                IsRead = false,
                IsDismissed = false
            },
            new AlertNotification
            {
                Id = Guid.NewGuid(),
                SourceId = src2.Id,
                Source = src2,
                AlertType = "LowActivity",
                Severity = "Warning",
                Message = "تنبيه انخفاض نشاط المصدر SRC-002",
                CreatedAt = DateTime.Today.AddDays(-1),
                IsRead = true,
                IsDismissed = false
            },
            new AlertNotification
            {
                Id = Guid.NewGuid(),
                SourceId = src1.Id,
                Source = src1,
                AlertType = "LowActivity",
                Severity = "Critical",
                Message = "تنبيه مخفي قديم",
                CreatedAt = DateTime.Today.AddDays(-10),
                IsRead = true,
                IsDismissed = true
            }
        };

        _mockAlertService.Setup(s => s.GetAllAlerts(It.IsAny<bool>()))
            .Returns(_testAlerts);

        _mockLocationService.Setup(s => s.GetAll())
            .Returns(_testLocations);
    }

    private readonly List<AlertsViewModel> _createdVms = new();

    private AlertsViewModel CreateViewModel()
    {
        var vm = new AlertsViewModel(_mockAlertService.Object, _mockLocationService.Object);
        _createdVms.Add(vm);
        return vm;
    }

    public void Dispose()
    {
        foreach (var vm in _createdVms)
        {
            vm.Dispose();
        }
        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Reset();
    }

    [Fact]
    public void LoadData_PopulatesAlertsAndComputesStatisticsCorrectly()
    {
        // Arrange & Act
        var vm = CreateViewModel();

        // Assert
        Assert.Equal(2, vm.TotalAlertsCount);      // 2 not dismissed
        Assert.Equal(1, vm.CriticalAlertsCount);  // 1 active critical
        Assert.Equal(1, vm.WarningAlertsCount);   // 1 active warning
        Assert.Equal(1, vm.UnreadAlertsCount);    // 1 active unread
        Assert.Equal(2, vm.PagedAlerts.Count);
    }

    [Fact]
    public void Filter_BySeverity_FiltersOnlyMatchingSeverity()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.SelectedSeverityFilter = "Critical";

        // Assert
        Assert.Single(vm.PagedAlerts);
        Assert.Equal("Critical", vm.PagedAlerts[0].Severity);
        Assert.Equal("SRC-001", vm.PagedAlerts[0].SourceCode);
    }

    [Fact]
    public void Filter_ByLocation_FiltersMatchingLocation()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.SelectedLocationFilter = "مختبر المعايرة";

        // Assert
        Assert.Single(vm.PagedAlerts);
        Assert.Equal("SRC-002", vm.PagedAlerts[0].SourceCode);
    }

    [Fact]
    public void Filter_ByDateRange_FiltersMatchingDates()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act - Only yesterday
        vm.FilterStartDate = DateTime.Today.AddDays(-1);
        vm.FilterEndDate = DateTime.Today.AddDays(-1);

        // Assert
        Assert.Single(vm.PagedAlerts);
        Assert.Equal("SRC-002", vm.PagedAlerts[0].SourceCode);
    }

    [Fact]
    public void Filter_ShowDismissed_IncludesDismissedAlertsWhenTrue()
    {
        // Arrange
        var vm = CreateViewModel();
        Assert.Equal(2, vm.PagedAlerts.Count);

        // Act
        vm.ShowDismissed = true;

        // Assert
        Assert.Equal(3, vm.PagedAlerts.Count);
        Assert.Contains(vm.PagedAlerts, a => a.IsDismissed);
    }

    [Fact]
    public void SearchText_FiltersBySourceCodeOrMessageOrIsotope()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.SearchText = "SRC-001";

        // Assert
        Assert.Single(vm.PagedAlerts);
        Assert.Equal("SRC-001", vm.PagedAlerts[0].SourceCode);

        // Act - Search Isotope
        vm.SearchText = "Cs-137";
        Assert.Single(vm.PagedAlerts);
        Assert.Equal("SRC-002", vm.PagedAlerts[0].SourceCode);
    }

    [Fact]
    public void MarkAsRead_CallsAlertServiceAndReloads()
    {
        // Arrange
        var vm = CreateViewModel();
        var targetRow = vm.PagedAlerts.First(a => !a.IsRead);

        // Act
        vm.MarkAsReadCommand.Execute(targetRow);

        // Assert
        _mockAlertService.Verify(s => s.MarkAsRead(targetRow.Id), Times.Once);
        _mockAlertService.Verify(s => s.GetAllAlerts(It.IsAny<bool>()), Times.AtLeast(2));
    }

    [Fact]
    public void DismissAlert_WithConfirmation_CallsAlertServiceDismiss()
    {
        // Arrange
        var vm = CreateViewModel();
        var targetRow = vm.PagedAlerts.First();

        // Act
        vm.DismissAlertCommand.Execute(targetRow);

        // Assert
        _mockAlertService.Verify(s => s.DismissAlert(targetRow.Id), Times.Once);
        _mockAlertService.Verify(s => s.GetAllAlerts(It.IsAny<bool>()), Times.AtLeast(2));
    }

    [Fact]
    public void MarkAllAsRead_CallsAlertServiceMarkAllAsRead()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.MarkAllAsReadCommand.Execute(null);

        // Assert
        _mockAlertService.Verify(s => s.MarkAllAsRead(), Times.Once);
    }

    [Fact]
    public void ResetFilters_ResetsAllFiltersToDefault()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.SearchText = "Test";
        vm.SelectedSeverityFilter = "Critical";
        vm.SelectedLocationFilter = "Main";
        vm.FilterStartDate = DateTime.Today;
        vm.FilterEndDate = DateTime.Today;
        vm.ShowDismissed = true;

        // Act
        vm.ResetFiltersCommand.Execute(null);

        // Assert
        Assert.Equal(string.Empty, vm.SearchText);
        Assert.Equal("All", vm.SelectedSeverityFilter);
        Assert.Equal(string.Empty, vm.SelectedLocationFilter);
        Assert.Null(vm.FilterStartDate);
        Assert.Null(vm.FilterEndDate);
        Assert.False(vm.ShowDismissed);
        Assert.Equal(2, vm.PagedAlerts.Count);
    }

    [Fact]
    public void Pagination_CalculatesPagesAndNavigatesCorrectly()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.PageSize = 1;
        vm.ApplyFiltersAndPagination();

        // Assert Page count
        Assert.Equal(2, vm.TotalPages);
        Assert.Equal(1, vm.CurrentPage);
        Assert.Single(vm.PagedAlerts);
        Assert.Equal(1, vm.PagedAlerts[0].RowNumber);

        // Act - Next Page
        vm.NextPageCommand.Execute(null);
        Assert.Equal(2, vm.CurrentPage);
        Assert.Single(vm.PagedAlerts);
        Assert.Equal(2, vm.PagedAlerts[0].RowNumber);

        // Act - Previous Page
        vm.PreviousPageCommand.Execute(null);
        Assert.Equal(1, vm.CurrentPage);
        Assert.Equal(1, vm.PagedAlerts[0].RowNumber);
    }

    [Fact]
    public void RefreshAlerts_CallsGenerateAlertsAndSetsMessage()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.RefreshAlertsCommand.Execute(null);

        // Assert
        _mockAlertService.Verify(s => s.GenerateAlerts(), Times.Once);
        Assert.True(vm.HasMessage);
    }

    [Fact]
    public void ChangingPageSize_UpdatesPaginationWithoutException()
    {
        // Arrange
        var vm = CreateViewModel();
        Assert.Contains(10, vm.AvailablePageSizes);
        Assert.Contains(20, vm.AvailablePageSizes);
        Assert.Contains(50, vm.AvailablePageSizes);
        Assert.Contains(100, vm.AvailablePageSizes);

        // Act - Change via Property
        vm.PageSize = 1;

        // Assert
        Assert.Equal(1, vm.PageSize);
        Assert.Equal(1, vm.CurrentPage);
        Assert.Equal(2, vm.TotalPages);
        Assert.Single(vm.PagedAlerts);

        // Act - Change via Command
        vm.ChangePageSizeCommand.Execute("50");
        Assert.Equal(50, vm.PageSize);
        Assert.Equal(1, vm.TotalPages);
        Assert.Equal(2, vm.PagedAlerts.Count);
    }

    [Fact]
    public void ViewSourceDetails_WhenSourceExists_ExecutesGracefully()
    {
        // Arrange
        var vm = CreateViewModel();
        var row = vm.Alerts.First(a => a.Source != null);

        // Act & Assert (Should not throw exception)
        var exception = Record.Exception(() => vm.ViewSourceDetailsCommand.Execute(row));
        Assert.Null(exception);
    }

    [Fact]
    public void ViewSourceDetails_WhenSourceIsMissing_HandlesGracefullyWithoutException()
    {
        // Arrange
        var vm = CreateViewModel();
        var alertWithoutSource = new AlertNotification
        {
            Id = Guid.NewGuid(),
            AlertType = "LowActivity",
            Severity = "Critical",
            Message = "Test without source",
            SourceId = null,
            Source = null
        };
        var row = new AlertRow { RowNumber = 99, Alert = alertWithoutSource };

        // Act & Assert
        var exception = Record.Exception(() => vm.ViewSourceDetailsCommand.Execute(row));
        Assert.Null(exception);
    }

    [Fact]
    public void AlertsViewModel_Calculates_LeakTestAndLowActivityCounts_Correctly()
    {
        // Arrange
        var alerts = new List<AlertNotification>
        {
            new AlertNotification { Id = Guid.NewGuid(), AlertType = "LeakTestDue", Severity = "Warning", IsDismissed = false },
            new AlertNotification { Id = Guid.NewGuid(), AlertType = "LeakTestOverdue", Severity = "Critical", IsDismissed = false },
            new AlertNotification { Id = Guid.NewGuid(), AlertType = "LowActivity", Severity = "Critical", IsDismissed = false },
            new AlertNotification { Id = Guid.NewGuid(), AlertType = "LowActivity", Severity = "Warning", IsDismissed = false },
            new AlertNotification { Id = Guid.NewGuid(), AlertType = "LeakTestDue", Severity = "Warning", IsDismissed = true } // Dismissed should not be counted
        };
        _mockAlertService.Setup(s => s.GetAllAlerts(true)).Returns(alerts);

        // Act
        var vm = CreateViewModel();

        // Assert
        Assert.Equal(2, vm.LeakTestAlertsCount);
        Assert.Equal(2, vm.LowActivityAlertsCount);
        Assert.Equal(4, vm.TotalAlertsCount);
    }

    [Fact]
    public void AlertsViewModel_SelectedAlertTypeFilter_FiltersCorrectly()
    {
        // Arrange
        var alerts = new List<AlertNotification>
        {
            new AlertNotification { Id = Guid.NewGuid(), AlertType = "LeakTestDue", Severity = "Warning", IsDismissed = false },
            new AlertNotification { Id = Guid.NewGuid(), AlertType = "LeakTestOverdue", Severity = "Critical", IsDismissed = false },
            new AlertNotification { Id = Guid.NewGuid(), AlertType = "LowActivity", Severity = "Critical", IsDismissed = false },
        };
        _mockAlertService.Setup(s => s.GetAllAlerts(true)).Returns(alerts);
        var vm = CreateViewModel();

        // Act - Filter LeakTest
        vm.SelectedAlertTypeFilter = "LeakTest";
        Assert.Equal(2, vm.Alerts.Count);
        Assert.All(vm.Alerts, a => Assert.Contains("LeakTest", a.AlertType));

        // Act - Filter LowActivity
        vm.SelectedAlertTypeFilter = "LowActivity";
        Assert.Single(vm.Alerts);
        Assert.Equal("LowActivity", vm.Alerts[0].AlertType);

        // Act - Filter All
        vm.SelectedAlertTypeFilter = "All";
        Assert.Equal(3, vm.Alerts.Count);
    }
}
