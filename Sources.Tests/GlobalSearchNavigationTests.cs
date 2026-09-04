using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Moq;
using Sources.Data;
using Sources.Helpers;
using Sources.Messages;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

public class GlobalSearchNavigationTests : IDisposable
{
    public GlobalSearchNavigationTests()
    {
        WeakReferenceMessenger.Default.Reset();
    }

    public void Dispose()
    {
        WeakReferenceMessenger.Default.Reset();
    }

    [Fact]
    public void SelectGlobalSearchResult_SourcesCategory_DispatchesCorrectMessageAndClearsSearch()
    {
        // Arrange
        var searchServiceMock = new Mock<IGlobalSearchService>();
        var sourceServiceMock = new Mock<ISourceService>();
        var isotopeServiceMock = new Mock<IRadioisotopeService>();
        var locationServiceMock = new Mock<ILocationService>();
        var decayServiceMock = new Mock<IDecayCalculationService>();
        var borrowServiceMock = new Mock<IBorrowService>();
        var settingsServiceMock = new Mock<ISystemSettingsService>();

        var vm = new DashboardViewModel(
            sourceServiceMock.Object,
            isotopeServiceMock.Object,
            locationServiceMock.Object,
            decayServiceMock.Object,
            borrowServiceMock.Object,
            settingsServiceMock.Object,
            null,
            searchServiceMock.Object
        );

        vm.GlobalSearchQuery = "Cobalt";
        vm.IsGlobalSearchResultsOpen = true;

        var sourceId = Guid.NewGuid();
        var item = new GlobalSearchResultItem
        {
            Id = sourceId,
            Category = SearchCategory.Sources,
            Title = "SRC-001",
            TargetView = "Sources"
        };

        NavigateToSearchResultMessage? receivedMessage = null;
        WeakReferenceMessenger.Default.Register<NavigateToSearchResultMessage>(this, (r, m) =>
        {
            receivedMessage = m;
        });

        // Act
        vm.SelectGlobalSearchResult(item);

        // Assert
        Assert.False(vm.IsGlobalSearchResultsOpen);
        Assert.Equal(string.Empty, vm.GlobalSearchQuery);
        Assert.Null(vm.SelectedGlobalSearchResultItem);
        Assert.NotNull(receivedMessage);
        Assert.Equal(SearchCategory.Sources, receivedMessage!.Category);
        Assert.Equal(sourceId, receivedMessage.EntityId);
    }

    [Fact]
    public void SelectGlobalSearchResult_LocationsCategory_DispatchesCorrectMessage()
    {
        // Arrange
        var searchServiceMock = new Mock<IGlobalSearchService>();
        var vm = new DashboardViewModel(
            new Mock<ISourceService>().Object,
            new Mock<IRadioisotopeService>().Object,
            new Mock<ILocationService>().Object,
            new Mock<IDecayCalculationService>().Object,
            new Mock<IBorrowService>().Object,
            new Mock<ISystemSettingsService>().Object,
            null,
            searchServiceMock.Object
        );

        var locId = Guid.NewGuid();
        var item = new GlobalSearchResultItem
        {
            Id = locId,
            Category = SearchCategory.Locations,
            Title = "مستودع أ",
            TargetView = "Locations"
        };

        NavigateToSearchResultMessage? receivedMessage = null;
        WeakReferenceMessenger.Default.Register<NavigateToSearchResultMessage>(this, (r, m) =>
        {
            receivedMessage = m;
        });

        // Act
        vm.SelectGlobalSearchResult(item);

        // Assert
        Assert.NotNull(receivedMessage);
        Assert.Equal(SearchCategory.Locations, receivedMessage!.Category);
        Assert.Equal(locId, receivedMessage.EntityId);
    }

    [Fact]
    public void SelectGlobalSearchResult_UsersCategory_DispatchesCorrectMessage()
    {
        // Arrange
        var searchServiceMock = new Mock<IGlobalSearchService>();
        var vm = new DashboardViewModel(
            new Mock<ISourceService>().Object,
            new Mock<IRadioisotopeService>().Object,
            new Mock<ILocationService>().Object,
            new Mock<IDecayCalculationService>().Object,
            new Mock<IBorrowService>().Object,
            new Mock<ISystemSettingsService>().Object,
            null,
            searchServiceMock.Object
        );

        var userId = Guid.NewGuid();
        var item = new GlobalSearchResultItem
        {
            Id = userId,
            Category = SearchCategory.Users,
            Title = "أحمد محمد",
            TargetView = "Users"
        };

        NavigateToSearchResultMessage? receivedMessage = null;
        WeakReferenceMessenger.Default.Register<NavigateToSearchResultMessage>(this, (r, m) =>
        {
            receivedMessage = m;
        });

        // Act
        vm.SelectGlobalSearchResult(item);

        // Assert
        Assert.NotNull(receivedMessage);
        Assert.Equal(SearchCategory.Users, receivedMessage!.Category);
        Assert.Equal(userId, receivedMessage.EntityId);
    }

    [Fact]
    public void SelectGlobalSearchResult_RadioisotopesCategory_DispatchesCorrectMessage()
    {
        // Arrange
        var searchServiceMock = new Mock<IGlobalSearchService>();
        var vm = new DashboardViewModel(
            new Mock<ISourceService>().Object,
            new Mock<IRadioisotopeService>().Object,
            new Mock<ILocationService>().Object,
            new Mock<IDecayCalculationService>().Object,
            new Mock<IBorrowService>().Object,
            new Mock<ISystemSettingsService>().Object,
            null,
            searchServiceMock.Object
        );

        var isotopeId = Guid.NewGuid();
        var item = new GlobalSearchResultItem
        {
            Id = isotopeId,
            Category = SearchCategory.Radioisotopes,
            Title = "Co-60",
            TargetView = "Radioisotopes"
        };

        NavigateToSearchResultMessage? receivedMessage = null;
        WeakReferenceMessenger.Default.Register<NavigateToSearchResultMessage>(this, (r, m) =>
        {
            receivedMessage = m;
        });

        // Act
        vm.SelectGlobalSearchResult(item);

        // Assert
        Assert.NotNull(receivedMessage);
        Assert.Equal(SearchCategory.Radioisotopes, receivedMessage!.Category);
        Assert.Equal(isotopeId, receivedMessage.EntityId);
    }

    [Fact]
    public void KeyboardNavigation_NextAndPrevious_CyclesThroughResults()
    {
        // Arrange
        var searchServiceMock = new Mock<IGlobalSearchService>();
        var vm = new DashboardViewModel(
            new Mock<ISourceService>().Object,
            new Mock<IRadioisotopeService>().Object,
            new Mock<ILocationService>().Object,
            new Mock<IDecayCalculationService>().Object,
            new Mock<IBorrowService>().Object,
            new Mock<ISystemSettingsService>().Object,
            null,
            searchServiceMock.Object
        );

        var item1 = new GlobalSearchResultItem { Id = Guid.NewGuid(), Title = "Item 1" };
        var item2 = new GlobalSearchResultItem { Id = Guid.NewGuid(), Title = "Item 2" };
        var item3 = new GlobalSearchResultItem { Id = Guid.NewGuid(), Title = "Item 3" };

        var group1 = new GlobalSearchResultGroup
        {
            Items = new List<GlobalSearchResultItem> { item1, item2 }
        };
        var group2 = new GlobalSearchResultGroup
        {
            Items = new List<GlobalSearchResultItem> { item3 }
        };

        vm.GlobalSearchResultGroups = new ObservableCollection<GlobalSearchResultGroup> { group1, group2 };
        vm.IsGlobalSearchResultsOpen = true;
        vm.SelectedGlobalSearchResultItem = item1;

        // Act & Assert 1: Next -> item2
        vm.SelectNextSearchResultCommand.Execute(null);
        Assert.Equal(item2, vm.SelectedGlobalSearchResultItem);
        Assert.True(item2.IsSelected);
        Assert.False(item1.IsSelected);

        // Act & Assert 2: Next -> item3
        vm.SelectNextSearchResultCommand.Execute(null);
        Assert.Equal(item3, vm.SelectedGlobalSearchResultItem);

        // Act & Assert 3: Next (wrap around) -> item1
        vm.SelectNextSearchResultCommand.Execute(null);
        Assert.Equal(item1, vm.SelectedGlobalSearchResultItem);

        // Act & Assert 4: Previous (wrap backwards) -> item3
        vm.SelectPreviousSearchResultCommand.Execute(null);
        Assert.Equal(item3, vm.SelectedGlobalSearchResultItem);
    }

    [Fact]
    public async Task ConfirmGlobalSearchResult_WithSelectedResult_DispatchesSelection()
    {
        // Arrange
        var searchServiceMock = new Mock<IGlobalSearchService>();
        var vm = new DashboardViewModel(
            new Mock<ISourceService>().Object,
            new Mock<IRadioisotopeService>().Object,
            new Mock<ILocationService>().Object,
            new Mock<IDecayCalculationService>().Object,
            new Mock<IBorrowService>().Object,
            new Mock<ISystemSettingsService>().Object,
            null,
            searchServiceMock.Object
        );

        var targetId = Guid.NewGuid();
        var item = new GlobalSearchResultItem
        {
            Id = targetId,
            Category = SearchCategory.Sources,
            Title = "SRC-999",
            TargetView = "Sources"
        };

        vm.GlobalSearchResultGroups = new ObservableCollection<GlobalSearchResultGroup>
        {
            new GlobalSearchResultGroup { Items = new List<GlobalSearchResultItem> { item } }
        };
        vm.IsGlobalSearchResultsOpen = true;
        vm.SelectedGlobalSearchResultItem = item;

        NavigateToSearchResultMessage? receivedMessage = null;
        WeakReferenceMessenger.Default.Register<NavigateToSearchResultMessage>(this, (r, m) =>
        {
            receivedMessage = m;
        });

        // Act
        await vm.ConfirmGlobalSearchResultCommand.ExecuteAsync(null);

        // Assert
        Assert.NotNull(receivedMessage);
        Assert.Equal(targetId, receivedMessage!.EntityId);
        Assert.False(vm.IsGlobalSearchResultsOpen);
    }

    [Fact]
    public void TargetViewModel_LocationsViewModel_SelectLocationById_OpensDetails()
    {
        // Arrange
        var locId = Guid.NewGuid();
        var loc = new Location
        {
            Id = locId,
            LocationName = "المختبر المركزي"
        };

        var locServiceMock = new Mock<ILocationService>();
        locServiceMock.Setup(s => s.GetAll()).Returns(new List<Location> { loc });
        locServiceMock.Setup(s => s.GetSourcesLinkedToLocation(locId)).Returns(new List<Source>());

        var vm = new LocationsViewModel(locServiceMock.Object);

        bool customDetailsOpened = false;
        vm.OpenDetailsWindowCustomAction = (l, s) =>
        {
            customDetailsOpened = true;
        };

        // Act: Send message
        WeakReferenceMessenger.Default.Send(new NavigateToSearchResultMessage(SearchCategory.Locations, locId));

        // Assert
        Assert.NotNull(vm.Selected);
        Assert.Equal(locId, vm.Selected!.Id);
        Assert.True(customDetailsOpened);
    }

    [Fact]
    public void TargetViewModel_UsersViewModel_SelectUserById_SelectsUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Username = "johndoe",
            FullName = "John Doe",
            IsActive = true
        };

        var userServiceMock = new Mock<IUserService>();
        userServiceMock.Setup(u => u.GetAllUsers()).Returns(new List<User> { user });
        userServiceMock.Setup(u => u.GetAllRoles()).Returns(new List<Role>());
        userServiceMock.Setup(u => u.GetAuditLogs(It.IsAny<Guid?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .Returns(new List<AuditLog>());

        var vm = new UsersViewModel(userServiceMock.Object, new Mock<IReportingService>().Object);

        // Act: Send message
        WeakReferenceMessenger.Default.Send(new NavigateToSearchResultMessage(SearchCategory.Users, userId));

        // Assert
        Assert.NotNull(vm.Selected);
        Assert.Equal(userId, vm.Selected!.Id);
        Assert.Equal("UsersManagement", vm.SelectedTab);
    }

    [Fact]
    public void TargetViewModel_RadioisotopesViewModel_SelectRadioisotopeById_SelectsIsotope()
    {
        // Arrange
        var isotopeId = Guid.NewGuid();
        var isotope = new Radioisotope
        {
            Id = isotopeId,
            Symbol = "Ir-192",
            Name = "Iridium-192"
        };

        var isotopeServiceMock = new Mock<IRadioisotopeService>();
        isotopeServiceMock.Setup(s => s.GetAll()).Returns(new List<Radioisotope> { isotope });

        var vm = new RadioisotopesViewModel(isotopeServiceMock.Object);

        // Act: Send message
        WeakReferenceMessenger.Default.Send(new NavigateToSearchResultMessage(SearchCategory.Radioisotopes, isotopeId));

        // Assert
        Assert.NotNull(vm.Selected);
        Assert.Equal(isotopeId, vm.Selected!.Id);
    }

    [Fact]
    public void TargetViewModel_SourcesViewModel_SelectSourceById_SelectsSource()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var source = new Source
        {
            Id = sourceId,
            SourceCode = "SRC-ABC-123",
            Status = "InUse",
            Radioisotope = new Radioisotope { Symbol = "Co-60", Name = "Cobalt-60" }
        };

        var sourceServiceMock = new Mock<ISourceService>();
        sourceServiceMock.Setup(s => s.GetAllSources()).Returns(new List<Source> { source });
        sourceServiceMock.Setup(s => s.GetDeletedSources()).Returns(new List<Source>());

        var isotopeServiceMock = new Mock<IRadioisotopeService>();
        isotopeServiceMock.Setup(s => s.GetAll()).Returns(new List<Radioisotope>());

        var locationServiceMock = new Mock<ILocationService>();
        locationServiceMock.Setup(s => s.GetAll()).Returns(new List<Location>());

        var reportingServiceMock = new Mock<IReportingService>();

        var vm = new SourcesViewModel(
            sourceServiceMock.Object,
            isotopeServiceMock.Object,
            locationServiceMock.Object,
            reportingServiceMock.Object
        );

        // Act: Send message
        WeakReferenceMessenger.Default.Send(new NavigateToSearchResultMessage(SearchCategory.Sources, sourceId));

        // Assert
        Assert.NotNull(vm.SelectedSource);
        Assert.Equal(sourceId, vm.SelectedSource!.Id);
        Assert.False(vm.IsDeletedSourcesView);
    }
}
