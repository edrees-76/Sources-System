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
}
