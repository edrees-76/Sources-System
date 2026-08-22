using System;
using System.Collections.Generic;
using Moq;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

public class LocationsViewModelTests
{
    private readonly Mock<ILocationService> _mockService;
    private readonly LocationsViewModel _vm;

    public LocationsViewModelTests()
    {
        _mockService = new Mock<ILocationService>();
        _mockService.Setup(s => s.GetAll()).Returns(new List<Location>());
        _vm = new LocationsViewModel(_mockService.Object);
    }

    [Fact]
    public void ViewLocationDetailsCommand_WithSources_OpensDetailsAndSetsHasLinkedSourcesTrue()
    {
        // Arrange
        var location = new Location { Id = Guid.NewGuid(), LocationName = "مختبر الأبحاث" };
        var sources = new List<Source>
        {
            new Source { Id = Guid.NewGuid(), SourceCode = "SRC-VM-01" },
            new Source { Id = Guid.NewGuid(), SourceCode = "SRC-VM-02" }
        };

        _mockService.Setup(s => s.GetSourcesLinkedToLocation(location.Id)).Returns(sources);

        // Act
        _vm.ViewLocationDetailsCommand.Execute(location);

        // Assert
        Assert.True(_vm.IsLocationDetailsOpen);
        Assert.Equal(location, _vm.SelectedLocationForDetails);
        Assert.Equal(2, _vm.LinkedSourcesForDetails.Count);
        Assert.True(_vm.HasLinkedSources);
    }

    [Fact]
    public void ViewLocationDetailsCommand_WithNoSources_OpensDetailsAndSetsHasLinkedSourcesFalse()
    {
        // Arrange
        var location = new Location { Id = Guid.NewGuid(), LocationName = "موقع فارغ" };
        _mockService.Setup(s => s.GetSourcesLinkedToLocation(location.Id)).Returns(new List<Source>());

        // Act
        _vm.ViewLocationDetailsCommand.Execute(location);

        // Assert
        Assert.True(_vm.IsLocationDetailsOpen);
        Assert.Equal(location, _vm.SelectedLocationForDetails);
        Assert.Empty(_vm.LinkedSourcesForDetails);
        Assert.False(_vm.HasLinkedSources);
    }

    [Fact]
    public void CloseLocationDetailsCommand_ResetsDetailsState()
    {
        // Arrange
        var location = new Location { Id = Guid.NewGuid(), LocationName = "مختبر" };
        var sources = new List<Source> { new Source { Id = Guid.NewGuid(), SourceCode = "SRC-VM-01" } };
        _mockService.Setup(s => s.GetSourcesLinkedToLocation(location.Id)).Returns(sources);

        _vm.ViewLocationDetailsCommand.Execute(location);
        Assert.True(_vm.IsLocationDetailsOpen);

        // Act
        _vm.CloseLocationDetailsCommand.Execute(null);

        // Assert
        Assert.False(_vm.IsLocationDetailsOpen);
        Assert.Null(_vm.SelectedLocationForDetails);
        Assert.Empty(_vm.LinkedSourcesForDetails);
        Assert.False(_vm.HasLinkedSources);
    }

    [Fact]
    public void LoadData_AssignsSequentialRowNumbersStartingFromOne()
    {
        // Arrange
        var locations = new List<Location>
        {
            new Location { Id = Guid.NewGuid(), LocationName = "موقع 1" },
            new Location { Id = Guid.NewGuid(), LocationName = "موقع 2" },
            new Location { Id = Guid.NewGuid(), LocationName = "موقع 3" }
        };
        _mockService.Setup(s => s.GetAll()).Returns(locations);

        // Act
        var vm = new LocationsViewModel(_mockService.Object);

        // Assert
        Assert.Equal(3, vm.Locations.Count);
        Assert.Equal(1, vm.Locations[0].RowNumber);
        Assert.Equal(2, vm.Locations[1].RowNumber);
        Assert.Equal(3, vm.Locations[2].RowNumber);
        Assert.Equal("موقع 1", vm.Locations[0].LocationName);
        Assert.Equal("موقع 2", vm.Locations[1].LocationName);
        Assert.Equal("موقع 3", vm.Locations[2].LocationName);
    }

    [Fact]
    public void ViewLocationDetails_AssignsSequentialRowNumbersToLinkedSources()
    {
        // Arrange
        var location = new Location { Id = Guid.NewGuid(), LocationName = "مختبر 1" };
        var sources = new List<Source>
        {
            new Source { Id = Guid.NewGuid(), SourceCode = "SRC-01" },
            new Source { Id = Guid.NewGuid(), SourceCode = "SRC-02" },
            new Source { Id = Guid.NewGuid(), SourceCode = "SRC-03" }
        };
        _mockService.Setup(s => s.GetSourcesLinkedToLocation(location.Id)).Returns(sources);

        // Act
        _vm.ViewLocationDetailsCommand.Execute(location);

        // Assert
        Assert.Equal(3, _vm.LinkedSourcesForDetails.Count);
        Assert.Equal(1, _vm.LinkedSourcesForDetails[0].RowNumber);
        Assert.Equal(2, _vm.LinkedSourcesForDetails[1].RowNumber);
        Assert.Equal(3, _vm.LinkedSourcesForDetails[2].RowNumber);
        Assert.Equal("SRC-01", _vm.LinkedSourcesForDetails[0].DisplaySourceCode);
        Assert.Equal("SRC-02", _vm.LinkedSourcesForDetails[1].DisplaySourceCode);
        Assert.Equal("SRC-03", _vm.LinkedSourcesForDetails[2].DisplaySourceCode);
    }

    [Fact]
    public void LocationSourceRow_WithDeletedSource_ReturnsDisplaySourceCodeWithDeletedBadge()
    {
        // Arrange
        var deletedSource = new Source
        {
            Id = Guid.NewGuid(),
            SourceCode = "SRC-DEL-01",
            IsDeleted = true
        };

        var row = new LocationSourceRow
        {
            RowNumber = 1,
            Source = deletedSource
        };

        // Assert
        Assert.Equal("SRC-DEL-01 (محذوف)", row.DisplaySourceCode);
        Assert.Equal("SRC-DEL-01 (محذوف)", row.SourceCode);
    }

    [Fact]
    public void DeleteCommand_WhenUserCancelsConfirmation_DoesNotCallDeleteService()
    {
        // Arrange
        var loc = new Location { Id = Guid.NewGuid(), LocationName = "موقع للاختبار" };
        var row = new LocationRow { RowNumber = 1, Location = loc };
        _vm.Locations = new System.Collections.ObjectModel.ObservableCollection<LocationRow> { row };
        _vm.Selected = row;

        Sources.Helpers.DialogHelper.IsTestMode = true;
        Sources.Helpers.DialogHelper.ShowConfirmationResult = false; // User cancels

        try
        {
            // Act
            _vm.DeleteCommand.Execute(row);

            // Assert
            _mockService.Verify(s => s.Delete(It.IsAny<Guid>()), Times.Never);
        }
        finally
        {
            Sources.Helpers.DialogHelper.IsTestMode = false;
            Sources.Helpers.DialogHelper.ShowConfirmationResult = null;
        }
    }

    [Fact]
    public void DeleteCommand_WhenUserConfirms_CallsDeleteServiceAndReloadsData()
    {
        // Arrange
        var loc = new Location { Id = Guid.NewGuid(), LocationName = "موقع للحذف" };
        var row = new LocationRow { RowNumber = 1, Location = loc };
        _vm.Locations = new System.Collections.ObjectModel.ObservableCollection<LocationRow> { row };
        _vm.Selected = row;

        _mockService.Setup(s => s.Delete(loc.Id)).Returns((true, "تم حذف الموقع"));
        _mockService.Setup(s => s.GetAll()).Returns(new List<Location>());

        Sources.Helpers.DialogHelper.IsTestMode = true;
        Sources.Helpers.DialogHelper.ShowConfirmationResult = true; // User confirms

        try
        {
            // Act
            _vm.DeleteCommand.Execute(row);

            // Assert
            _mockService.Verify(s => s.Delete(loc.Id), Times.Once);
            Assert.True(_vm.HasMessage);
            Assert.Equal("تم حذف الموقع", _vm.Message);
        }
        finally
        {
            Sources.Helpers.DialogHelper.IsTestMode = false;
            Sources.Helpers.DialogHelper.ShowConfirmationResult = null;
        }
    }

    [Fact]
    public void CloseMessageCommand_ClearsMessageAndSetsHasMessageFalse()
    {
        // Arrange
        _vm.Message = "رسالة نجاح";
        _vm.HasMessage = true;

        // Act
        _vm.CloseMessageCommand.Execute(null);

        // Assert
        Assert.False(_vm.HasMessage);
        Assert.Empty(_vm.Message);
    }

    [Fact]
    public void DeleteCommand_WhenTargetIsNull_ShowsWarningAndDoesNotCallService()
    {
        // Arrange
        _vm.Selected = null;
        Sources.Helpers.DialogHelper.IsTestMode = true;
        Sources.Helpers.DialogHelper.LastMessage = null;

        try
        {
            // Act
            _vm.DeleteCommand.Execute(null);

            // Assert
            _mockService.Verify(s => s.Delete(It.IsAny<Guid>()), Times.Never);
            Assert.Equal("الرجاء تحديد موقع أولاً للمتابعة.", Sources.Helpers.DialogHelper.LastMessage);
        }
        finally
        {
            Sources.Helpers.DialogHelper.IsTestMode = false;
            Sources.Helpers.DialogHelper.LastMessage = null;
        }
    }

    [Fact]
    public void DeleteCommand_WhenServiceReturnsFalse_ShowsErrorDialog()
    {
        // Arrange
        var loc = new Location { Id = Guid.NewGuid(), LocationName = "موقع مرتبط بمصادر" };
        var row = new LocationRow { RowNumber = 1, Location = loc };
        _vm.Locations = new System.Collections.ObjectModel.ObservableCollection<LocationRow> { row };
        _vm.Selected = row;

        _mockService.Setup(s => s.Delete(loc.Id)).Returns((false, "لا يمكن حذف الموقع لوجود مصادر مرتبطة به"));

        Sources.Helpers.DialogHelper.IsTestMode = true;
        Sources.Helpers.DialogHelper.ShowConfirmationResult = true; // User confirms
        Sources.Helpers.DialogHelper.LastMessage = null;

        try
        {
            // Act
            _vm.DeleteCommand.Execute(row);

            // Assert
            _mockService.Verify(s => s.Delete(loc.Id), Times.Once);
            Assert.Equal("لا يمكن حذف الموقع لوجود مصادر مرتبطة به", Sources.Helpers.DialogHelper.LastMessage);
        }
        finally
        {
            Sources.Helpers.DialogHelper.IsTestMode = false;
            Sources.Helpers.DialogHelper.ShowConfirmationResult = null;
            Sources.Helpers.DialogHelper.LastMessage = null;
        }
    }
}

