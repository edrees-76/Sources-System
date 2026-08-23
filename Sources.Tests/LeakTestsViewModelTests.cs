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

public class LeakTestsViewModelTests
{
    private readonly Mock<ILeakTestService> _mockLeakTestService;
    private readonly Mock<ISourceService> _mockSourceService;
    private readonly Mock<IReportingService> _mockReportingService;
    private readonly Mock<IUserService> _mockUserService;
    private readonly Mock<ISystemSettingsService> _mockSettingsService;
    private readonly LeakTestsViewModel _viewModel;

    public LeakTestsViewModelTests()
    {
        _mockLeakTestService = new Mock<ILeakTestService>();
        _mockSourceService = new Mock<ISourceService>();
        _mockReportingService = new Mock<IReportingService>();
        _mockUserService = new Mock<IUserService>();
        _mockSettingsService = new Mock<ISystemSettingsService>();

        _viewModel = new LeakTestsViewModel(
            _mockLeakTestService.Object,
            _mockSourceService.Object,
            _mockReportingService.Object,
            _mockUserService.Object,
            _mockSettingsService.Object);
    }

    [Fact]
    public void OpenAddModal_SetsDefaultValues_AndOpensModal()
    {
        // Arrange
        _mockLeakTestService
            .Setup(s => s.CalculateNextDueDate(It.IsAny<DateTime>(), null))
            .Returns(DateTime.Today.AddMonths(6));

        // Act
        _viewModel.OpenAddModal();

        // Assert
        Assert.True(_viewModel.IsModalOpen);
        Assert.False(_viewModel.IsEditingRecord);
        Assert.Equal("تسجيل اختبار تسرب جديد", _viewModel.ModalTitle);
        Assert.Equal("Pass", _viewModel.FormResult);
        Assert.Equal(DateTime.Today, _viewModel.FormTestDate);
        Assert.Equal(DateTime.Today.AddMonths(6), _viewModel.FormNextDueDate);
    }

    [Fact]
    public void OpenEditModal_PopulatesFieldsFromRecord_AndOpensModal()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var record = new LeakTestRecord
        {
            Id = Guid.NewGuid(),
            SourceId = sourceId,
            Source = new Source { Id = sourceId, SourceCode = "SRC-TEST-99" },
            TestDate = new DateTime(2026, 2, 1),
            NextDueDate = new DateTime(2026, 8, 1),
            Result = "Fail",
            MeasuredActivityBq = 120.5,
            InspectorName = "المهندس فهد",
            CertificateNumber = "CERT-2026-X",
            Notes = "تم الكشف عن تلوث"
        };

        // Act
        _viewModel.OpenEditModal(record);

        // Assert
        Assert.True(_viewModel.IsModalOpen);
        Assert.True(_viewModel.IsEditingRecord);
        Assert.Equal(sourceId, _viewModel.FormSourceId);
        Assert.Equal(new DateTime(2026, 2, 1), _viewModel.FormTestDate);
        Assert.Equal(new DateTime(2026, 8, 1), _viewModel.FormNextDueDate);
        Assert.Equal("Fail", _viewModel.FormResult);
        Assert.Equal("120.5", _viewModel.FormMeasuredActivityText);
        Assert.Equal("المهندس فهد", _viewModel.FormInspectorName);
        Assert.Equal("CERT-2026-X", _viewModel.FormCertificateNumber);
        Assert.Equal("تم الكشف عن تلوث", _viewModel.FormNotes);
    }

    [Fact]
    public void CloseModal_ClosesModal()
    {
        // Arrange
        _viewModel.IsModalOpen = true;

        // Act
        _viewModel.CloseModalCommand.Execute(null);

        // Assert
        Assert.False(_viewModel.IsModalOpen);
    }

    [Fact]
    public async Task ResetSearch_ResetsFiltersAndReloads()
    {
        // Arrange
        _viewModel.SearchText = "something";
        _viewModel.ResultFilter = "Fail";
        _viewModel.DueStatusFilter = "Overdue";
        _viewModel.CurrentPage = 5;

        _mockLeakTestService
            .Setup(s => s.GetAllRecords(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new List<LeakTestRecord>());

        // Act
        await _viewModel.ResetSearchCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(string.Empty, _viewModel.SearchText);
        Assert.Equal("All", _viewModel.ResultFilter);
        Assert.Equal("All", _viewModel.DueStatusFilter);
        Assert.Equal(1, _viewModel.CurrentPage);
    }
}
