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
        Assert.Equal("SRC-01", _vm.LinkedSourcesForDetails[0].SourceCode);
        Assert.Equal("SRC-02", _vm.LinkedSourcesForDetails[1].SourceCode);
        Assert.Equal("SRC-03", _vm.LinkedSourcesForDetails[2].SourceCode);
    }
}
