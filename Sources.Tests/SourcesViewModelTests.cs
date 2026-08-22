using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Sources.Data;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fixtures;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

public class SourcesViewModelTests : IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly Mock<ISourceService> _mockSourceService;
    private readonly Mock<IRadioisotopeService> _mockIsotopeService;
    private readonly Mock<ILocationService> _mockLocationService;
    private readonly Mock<IReportingService> _mockReportingService;

    private readonly Guid _unitBqId = Guid.NewGuid();
    private readonly Guid _unitCiId = Guid.NewGuid();
    private readonly Guid _isotopeCo60Id = Guid.NewGuid();
    private readonly Guid _isotopeCs137Id = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();

    public SourcesViewModelTests()
    {
        _fixture = new SqliteInMemoryFixture();

        // Seed basic reference data
        using (var db = _fixture.CreateContext())
        {
            db.ActivityUnits.AddRange(
                new ActivityUnit { Id = _unitBqId, UnitName = "Becquerel", UnitSymbol = "Bq", ConversionToBq = 1 },
                new ActivityUnit { Id = _unitCiId, UnitName = "Curie", UnitSymbol = "Ci", ConversionToBq = 3.7e10 }
            );
            db.Radioisotopes.AddRange(
                new Radioisotope { Id = _isotopeCo60Id, Name = "Cobalt-60", Symbol = "Co-60", HalfLife = 5.27, HalfLifeUnit = "years" },
                new Radioisotope { Id = _isotopeCs137Id, Name = "Cesium-137", Symbol = "Cs-137", HalfLife = 30.08, HalfLifeUnit = "years" }
            );
            db.Locations.Add(
                new Location { Id = _locationId, LocationName = "Main Storage", LocationType = "Storage" }
            );
            db.SaveChanges();
        }

        // Setup App.ServiceProvider for ViewModel
        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<AppDbContext>>(_fixture.ContextFactory);
        _sp = services.BuildServiceProvider();
        typeof(App).GetProperty("ServiceProvider", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, _sp);

        _mockSourceService = new Mock<ISourceService>();
        _mockIsotopeService = new Mock<IRadioisotopeService>();
        _mockLocationService = new Mock<ILocationService>();
        _mockReportingService = new Mock<IReportingService>();

        _mockIsotopeService.Setup(s => s.GetAll()).Returns(new List<Radioisotope>
        {
            new Radioisotope { Id = _isotopeCo60Id, Name = "Cobalt-60", Symbol = "Co-60" },
            new Radioisotope { Id = _isotopeCs137Id, Name = "Cesium-137", Symbol = "Cs-137" }
        });

        _mockLocationService.Setup(s => s.GetAll()).Returns(new List<Location>
        {
            new Location { Id = _locationId, LocationName = "Main Storage" }
        });

        _mockSourceService.Setup(s => s.GetAllSources()).Returns(new List<Source>());

        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Reset();
    }

    private readonly IServiceProvider _sp;

    private SourcesViewModel CreateViewModel()
    {
        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Reset();
        typeof(App).GetProperty("ServiceProvider", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, _sp);
        return new SourcesViewModel(
            _mockSourceService.Object,
            _mockIsotopeService.Object,
            _mockLocationService.Object,
            _mockReportingService.Object
        );
    }

    public void Dispose()
    {
        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Reset();
    }

    #region 1. CalibrationDate Validation Tests

    [Fact]
    public async Task SaveAsync_WithTodayCalibrationDate_Succeeds()
    {
        // Arrange
        var vm = CreateViewModel();
        _mockSourceService
            .Setup(s => s.CreateSource(It.IsAny<Source>(), null))
            .Returns((true, "تم إضافة المصدر بنجاح"));

        vm.AddNewCommand.Execute(null);
        vm.EditSourceCode = "SRC-TODAY-01";
        vm.EditRadioisotopeId = _isotopeCo60Id;
        vm.EditInitialActivityText = "100";
        vm.EditInitialUnitId = _unitBqId;
        vm.EditCurrentUnitId = _unitBqId;
        vm.EditLocationId = _locationId;
        vm.EditStatus = "InUse";
        vm.EditCalibrationDate = DateTime.Today;

        // Act
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        _mockSourceService.Verify(s => s.CreateSource(It.IsAny<Source>(), null), Times.Once);
        Assert.Equal("تم إضافة المصدر بنجاح", vm.Message);
        Assert.False(vm.IsEditing);
    }

    [Fact]
    public async Task SaveAsync_WithPastCalibrationDate_Succeeds()
    {
        // Arrange
        var vm = CreateViewModel();
        _mockSourceService
            .Setup(s => s.CreateSource(It.IsAny<Source>(), null))
            .Returns((true, "تم إضافة المصدر بنجاح"));

        vm.AddNewCommand.Execute(null);
        vm.EditSourceCode = "SRC-PAST-01";
        vm.EditRadioisotopeId = _isotopeCo60Id;
        vm.EditInitialActivityText = "250";
        vm.EditInitialUnitId = _unitBqId;
        vm.EditCurrentUnitId = _unitBqId;
        vm.EditLocationId = _locationId;
        vm.EditStatus = "InUse";
        vm.EditCalibrationDate = DateTime.Today.AddYears(-2);

        // Act
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        _mockSourceService.Verify(s => s.CreateSource(It.IsAny<Source>(), null), Times.Once);
        Assert.Equal("تم إضافة المصدر بنجاح", vm.Message);
        Assert.False(vm.IsEditing);
    }

    [Fact]
    public async Task SaveAsync_WithFutureCalibrationDate_FailsAndShowsErrorMessage()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.AddNewCommand.Execute(null);
        vm.EditSourceCode = "SRC-FUTURE-01";
        vm.EditRadioisotopeId = _isotopeCo60Id;
        vm.EditInitialActivityText = "100";
        vm.EditInitialUnitId = _unitBqId;
        vm.EditCurrentUnitId = _unitBqId;
        vm.EditLocationId = _locationId;
        vm.EditStatus = "InUse";
        vm.EditCalibrationDate = DateTime.Today.AddDays(1); // Future date

        // Act
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        _mockSourceService.Verify(s => s.CreateSource(It.IsAny<Source>(), It.IsAny<List<SourceIsotope>>()), Times.Never);
        Assert.True(vm.HasMessage);
        Assert.Equal(TranslationHelper.GetString("MsgErrCalibrationDateFuture"), vm.Message);
        Assert.True(vm.IsEditing); // Form remains open
    }

    [Fact]
    public async Task SaveAsync_MultiIsotope_WithFutureCalibrationDate_Fails()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.AddNewCommand.Execute(null);
        vm.EditSourceCode = "SRC-MIX-FUTURE";
        vm.IsMultiIsotope = true;
        vm.EditCurrentUnitId = _unitBqId;
        vm.EditLocationId = _locationId;
        vm.EditStatus = "InUse";
        vm.EditCalibrationDate = DateTime.Today.AddDays(5); // Future date

        vm.IsotopeEntries.Add(new IsotopeEntryViewModel
        {
            RadioisotopeId = _isotopeCo60Id,
            InitialActivityText = "50",
            ActivityUnitId = _unitBqId
        });

        // Act
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        _mockSourceService.Verify(s => s.CreateSource(It.IsAny<Source>(), It.IsAny<List<SourceIsotope>>()), Times.Never);
        Assert.True(vm.HasMessage);
        Assert.Equal(TranslationHelper.GetString("MsgErrCalibrationDateFuture"), vm.Message);
    }

    #endregion

    #region 2. Multi-Isotope Disable Validation Tests

    [Fact]
    public async Task SaveAsync_WhenDisablingMultiIsotope_ForSourceWithMultipleSavedIsotopes_FailsAndShowsErrorMessage()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var savedSource = new Source
        {
            Id = sourceId,
            SourceCode = "SRC-MULTI-01",
            RadioisotopeId = _isotopeCo60Id,
            InitialActivityValue = 100,
            InitialActivityUnitId = _unitBqId,
            CurrentActivityUnitId = _unitBqId,
            LocationId = _locationId,
            Status = "InUse",
            CalibrationDate = DateTime.Today.AddMonths(-6),
            HasDetailedIsotopes = true,
            SourceIsotopes = new List<SourceIsotope>
            {
                new SourceIsotope { Id = Guid.NewGuid(), SourceId = sourceId, RadioisotopeId = _isotopeCo60Id, InitialActivityValue = 60, ActivityUnitId = _unitBqId },
                new SourceIsotope { Id = Guid.NewGuid(), SourceId = sourceId, RadioisotopeId = _isotopeCs137Id, InitialActivityValue = 40, ActivityUnitId = _unitBqId }
            }
        };

        _mockSourceService.Setup(s => s.GetSourceById(sourceId)).Returns(savedSource);

        var vm = CreateViewModel();
        vm.EditSourceCommand.Execute(savedSource);

        // User attempts to uncheck IsMultiIsotope to convert to single-isotope without deleting the saved isotopes first
        vm.IsMultiIsotope = false;
        vm.EditRadioisotopeId = _isotopeCo60Id;
        vm.EditInitialActivityText = "100";
        vm.EditInitialUnitId = _unitBqId;

        // Act
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        _mockSourceService.Verify(s => s.UpdateSource(It.IsAny<Source>(), It.IsAny<List<SourceIsotope>>()), Times.Never);
        Assert.True(vm.HasMessage);
        Assert.Equal(TranslationHelper.GetString("MsgErrCannotDisableMultiIsotope"), vm.Message);
        Assert.True(vm.IsEditing);
    }

    [Fact]
    public async Task SaveAsync_WhenDisablingMultiIsotope_ForSourceWithSingleSavedIsotope_Succeeds()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var savedSource = new Source
        {
            Id = sourceId,
            SourceCode = "SRC-SINGLE-01",
            RadioisotopeId = _isotopeCo60Id,
            InitialActivityValue = 100,
            InitialActivityUnitId = _unitBqId,
            CurrentActivityUnitId = _unitBqId,
            LocationId = _locationId,
            Status = "InUse",
            CalibrationDate = DateTime.Today.AddMonths(-6),
            HasDetailedIsotopes = true,
            SourceIsotopes = new List<SourceIsotope>
            {
                new SourceIsotope { Id = Guid.NewGuid(), SourceId = sourceId, RadioisotopeId = _isotopeCo60Id, InitialActivityValue = 100, ActivityUnitId = _unitBqId }
            }
        };

        _mockSourceService.Setup(s => s.GetSourceById(sourceId)).Returns(savedSource);
        _mockSourceService
            .Setup(s => s.UpdateSource(It.IsAny<Source>(), It.IsAny<List<SourceIsotope>>()))
            .Returns((true, "تم تحديث المصدر بنجاح"));

        var vm = CreateViewModel();
        vm.EditSourceCommand.Execute(savedSource);

        // Disabling multi-isotope is allowed because only 1 isotope was saved
        vm.IsMultiIsotope = false;
        vm.EditRadioisotopeId = _isotopeCo60Id;
        vm.EditInitialActivityText = "100";
        vm.EditInitialUnitId = _unitBqId;

        // Act
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        _mockSourceService.Verify(s => s.UpdateSource(It.IsAny<Source>(), null), Times.Once);
        Assert.Equal("تم تحديث المصدر بنجاح", vm.Message);
        Assert.False(vm.IsEditing);
    }

    [Fact]
    public async Task SaveAsync_WhenCreatingNewSingleSource_SucceedsWithoutRestriction()
    {
        // Arrange
        var vm = CreateViewModel();
        _mockSourceService
            .Setup(s => s.CreateSource(It.IsAny<Source>(), It.IsAny<List<SourceIsotope>>()))
            .Returns((true, "تم إضافة المصدر بنجاح"));

        vm.AddNewCommand.Execute(null);
        Assert.True(vm.IsNew);
        Assert.False(vm.IsMultiIsotope);

        vm.EditSourceCode = "SRC-NEW-SINGLE-01";
        vm.EditRadioisotopeId = _isotopeCo60Id;
        vm.EditInitialActivityText = "75";
        vm.EditInitialUnitId = _unitBqId;
        vm.EditCurrentUnitId = _unitBqId;
        vm.EditLocationId = _locationId;
        vm.EditStatus = "InUse";
        vm.EditCalibrationDate = DateTime.Today;

        // Act
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        _mockSourceService.Verify(s => s.CreateSource(It.IsAny<Source>(), null), Times.Once);
        Assert.Equal("تم إضافة المصدر بنجاح", vm.Message);
        Assert.False(vm.IsEditing);
    }

    [Fact]
    public void EditSource_WhenSourceHasActiveBorrow_SetsIsActivelyBorrowedTrue()
    {
        // Arrange
        var vm = CreateViewModel();
        var sourceId = Guid.NewGuid();
        var source = new Source
        {
            Id = sourceId,
            SourceCode = "SRC-0021",
            Status = "InUse",
            LocationId = _locationId,
            RadioisotopeId = _isotopeCo60Id,
            InitialActivityValue = 100,
            InitialActivityUnitId = _unitBqId,
            CurrentActivityUnitId = _unitBqId,
            CalibrationDate = DateTime.Today
        };

        _mockSourceService.Setup(s => s.HasActiveBorrow(sourceId)).Returns(true);
        _mockSourceService.Setup(s => s.GetSourceById(sourceId)).Returns(source);

        // Act
        vm.EditSourceCommand.Execute(source);

        // Assert
        Assert.True(vm.IsActivelyBorrowed);
        Assert.Equal("SRC-0021", vm.EditSourceCode);
        Assert.Equal("InUse", vm.EditStatus);
        Assert.Equal(_locationId, vm.EditLocationId);
    }

    [Fact]
    public async Task SaveAsync_WhenActivelyBorrowedSource_AttemptsLocationChange_RejectsWithErrorMessage()
    {
        // Arrange
        var vm = CreateViewModel();
        var sourceId = Guid.NewGuid();
        var newLocationId = Guid.NewGuid();
        var source = new Source
        {
            Id = sourceId,
            SourceCode = "SRC-0021",
            Status = "InUse",
            LocationId = _locationId,
            RadioisotopeId = _isotopeCo60Id,
            InitialActivityValue = 100,
            InitialActivityUnitId = _unitBqId,
            CurrentActivityUnitId = _unitBqId,
            CalibrationDate = DateTime.Today
        };

        _mockSourceService.Setup(s => s.HasActiveBorrow(sourceId)).Returns(true);
        _mockSourceService.Setup(s => s.GetSourceById(sourceId)).Returns(source);

        vm.EditSourceCommand.Execute(source);
        Assert.True(vm.IsActivelyBorrowed);

        // Act: محاولة تعديل الموقع
        vm.EditLocationId = newLocationId;
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("لا يمكن تعديل الموقع أو الحالة لمصدر قيد الاستعارة النشطة حالياً", vm.Message);
        _mockSourceService.Verify(s => s.UpdateSource(It.IsAny<Source>(), It.IsAny<List<SourceIsotope>>()), Times.Never);
    }

    [Fact]
    public async Task DeleteSourceAsync_WhenUserCancelsConfirmation_DoesNotCallDeleteService()
    {
        // Arrange
        var vm = CreateViewModel();
        var source = new Source { Id = Guid.NewGuid(), SourceCode = "SRC-CANCEL-01" };

        DialogHelper.IsTestMode = true;
        DialogHelper.ShowConfirmationResult = false; // المستخدم يلغي الحذف

        try
        {
            // Act
            await vm.DeleteSourceCommand.ExecuteAsync(source);

            // Assert
            _mockSourceService.Verify(s => s.DeleteSource(It.IsAny<Guid>()), Times.Never);
        }
        finally
        {
            DialogHelper.IsTestMode = false;
            DialogHelper.ShowConfirmationResult = null;
        }
    }

    [Fact]
    public async Task DeleteSourceAsync_WhenDeleteFails_ShowsErrorDialogAndDoesNotReload()
    {
        // Arrange
        var vm = CreateViewModel();
        var sourceId = Guid.NewGuid();
        var source = new Source { Id = sourceId, SourceCode = "SRC-FAIL-01" };

        _mockSourceService.Setup(s => s.DeleteSource(sourceId))
            .Returns((false, "لا يمكن حذف المصدر لوجود استعارة نشطة عليه"));

        DialogHelper.IsTestMode = true;
        DialogHelper.ShowConfirmationResult = true; // المستخدم يؤكد
        DialogHelper.LastMessage = null;

        try
        {
            // Act
            await vm.DeleteSourceCommand.ExecuteAsync(source);

            // Assert
            _mockSourceService.Verify(s => s.DeleteSource(sourceId), Times.Once);
            Assert.Equal("لا يمكن حذف المصدر لوجود استعارة نشطة عليه", DialogHelper.LastMessage);
            Assert.Equal("لا يمكن حذف المصدر لوجود استعارة نشطة عليه", vm.Message);
        }
        finally
        {
            DialogHelper.IsTestMode = false;
            DialogHelper.ShowConfirmationResult = null;
            DialogHelper.LastMessage = null;
        }
    }

    [Fact]
    public async Task DeleteSourceAsync_WhenDeleteSucceeds_ReloadsDataAndBroadcastsMessage()
    {
        // Arrange
        var vm = CreateViewModel();
        var sourceId = Guid.NewGuid();
        var source = new Source { Id = sourceId, SourceCode = "SRC-OK-01" };

        _mockSourceService.Setup(s => s.DeleteSource(sourceId))
            .Returns((true, "تم حذف المصدر بنجاح"));
        _mockSourceService.Setup(s => s.GetAllSources()).Returns(new List<Source>());

        DialogHelper.IsTestMode = true;
        DialogHelper.ShowConfirmationResult = true; // المستخدم يؤكد

        try
        {
            // Act
            await vm.DeleteSourceCommand.ExecuteAsync(source);

            // Assert
            _mockSourceService.Verify(s => s.DeleteSource(sourceId), Times.Once);
            _mockSourceService.Verify(s => s.GetAllSources(), Times.AtLeastOnce);
            Assert.Equal("تم حذف المصدر بنجاح", vm.Message);
        }
        finally
        {
            DialogHelper.IsTestMode = false;
            DialogHelper.ShowConfirmationResult = null;
        }
    }

    #endregion
}

