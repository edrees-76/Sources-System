using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Sources.Interfaces;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

public class RadioisotopesOrnlSuggestionTests
{
    private readonly Mock<IRadioisotopeService> _isotopeServiceMock;
    private readonly Mock<IIsotopeLibraryService> _libraryServiceMock;

    public RadioisotopesOrnlSuggestionTests()
    {
        _isotopeServiceMock = new Mock<IRadioisotopeService>();
        _libraryServiceMock = new Mock<IIsotopeLibraryService>();

        _isotopeServiceMock.Setup(s => s.GetAll()).Returns(new List<Radioisotope>());

        var ornlEntries = new List<IsotopeReferenceEntry>
        {
            new IsotopeReferenceEntry
            {
                NuclideSymbol = "137Cs",
                HalfLife = "30.0y",
                SpecificGammaConstantValue = 7.72e-5, // (mSv/h)/MBq at 1 meter in ORNL
                PageNumber = 11,
                SourceType = ReferenceSourceType.ORNL_RSIC_45_R1
            },
            new IsotopeReferenceEntry
            {
                NuclideSymbol = "60Co",
                HalfLife = "5.27y",
                SpecificGammaConstantValue = 0.000305, // (mSv/h)/MBq at 1 meter in ORNL
                PageNumber = 8,
                SourceType = ReferenceSourceType.ORNL_RSIC_45_R1
            },
            new IsotopeReferenceEntry
            {
                NuclideSymbol = "192Ir",
                HalfLife = "74.0d",
                SpecificGammaConstantValue = 0.0001597, // (mSv/h)/MBq at 1 meter in ORNL
                PageNumber = 58,
                SourceType = ReferenceSourceType.ORNL_RSIC_45_R1
            },
            new IsotopeReferenceEntry
            {
                NuclideSymbol = "90Sr",
                HalfLife = "28.79y",
                SpecificGammaConstantValue = null, // Non gamma or missing
                SourceType = ReferenceSourceType.ICRP_107
            }
        };

        _libraryServiceMock.Setup(s => s.GetAllEntriesAsync())
            .ReturnsAsync(ornlEntries);
    }

    [Fact]
    public async Task SuggestGammaConstant_WhenSymbolExistsInOrnl_CalculatesConvertedValueMultipliedBy1000()
    {
        // Arrange
        var vm = new RadioisotopesViewModel(_isotopeServiceMock.Object, _libraryServiceMock.Object);
        vm.EditSymbol = "Cs-137";

        // Act
        await vm.SuggestGammaConstantCommand.ExecuteAsync(null);

        // Assert
        // ORNL value = 7.72e-5 (mSv/h)/MBq -> x1000 = 0.0772 µSv·m²/(MBq·h)
        Assert.NotNull(vm.EditGammaConstant);
        Assert.Equal(0.0772, vm.EditGammaConstant.Value, precision: 6);
        Assert.Equal("0.0772", vm.EditGammaConstantText);
        Assert.True(vm.HasMessage);
        Assert.Contains("11", vm.Message); // Page number 11 from ORNL
    }

    [Fact]
    public async Task SuggestGammaConstant_ForCobalt60_CalculatesCorrectOperationalValue()
    {
        // Arrange
        var vm = new RadioisotopesViewModel(_isotopeServiceMock.Object, _libraryServiceMock.Object);
        vm.EditSymbol = "Co-60";

        // Act
        await vm.SuggestGammaConstantCommand.ExecuteAsync(null);

        // Assert
        // ORNL value = 0.000305 -> x1000 = 0.305 µSv·m²/(MBq·h)
        Assert.NotNull(vm.EditGammaConstant);
        Assert.Equal(0.305, vm.EditGammaConstant.Value, precision: 6);
        Assert.Equal("0.305", vm.EditGammaConstantText);
        Assert.True(vm.HasMessage);
        Assert.Contains("8", vm.Message); // Page number 8
    }

    [Fact]
    public async Task SuggestGammaConstant_WithReversedSymbol_FindsIsotopeAndConvertsValue()
    {
        // Arrange
        var vm = new RadioisotopesViewModel(_isotopeServiceMock.Object, _libraryServiceMock.Object);
        vm.EditSymbol = "192Ir"; // Reversed format

        // Act
        await vm.SuggestGammaConstantCommand.ExecuteAsync(null);

        // Assert
        // ORNL value = 0.0001597 -> x1000 = 0.1597
        Assert.NotNull(vm.EditGammaConstant);
        Assert.Equal(0.1597, vm.EditGammaConstant.Value, precision: 6);
        Assert.Equal("0.1597", vm.EditGammaConstantText);
        Assert.True(vm.HasMessage);
        Assert.Contains("58", vm.Message); // Page number 58
    }

    [Fact]
    public async Task SuggestGammaConstant_WhenSymbolNotFoundInOrnl_DoesNotModifyFieldAndShowsMessage()
    {
        // Arrange
        var vm = new RadioisotopesViewModel(_isotopeServiceMock.Object, _libraryServiceMock.Object);
        vm.EditSymbol = "Sr-90"; // Only in ICRP without ORNL gamma constant
        vm.EditGammaConstant = null;
        vm.EditGammaConstantText = string.Empty;

        // Act
        await vm.SuggestGammaConstantCommand.ExecuteAsync(null);

        // Assert
        Assert.Null(vm.EditGammaConstant);
        Assert.Empty(vm.EditGammaConstantText);
        Assert.True(vm.HasMessage);
        Assert.Contains("ORNL", vm.Message);
    }

    [Fact]
    public async Task SuggestGammaConstant_WhenSymbolIsEmpty_ShowsSymbolRequiredMessageAndDoesNotSearch()
    {
        // Arrange
        var vm = new RadioisotopesViewModel(_isotopeServiceMock.Object, _libraryServiceMock.Object);
        vm.EditSymbol = "   ";
        vm.EditGammaConstantText = "0.05";
        vm.EditGammaConstant = 0.05;

        // Act
        await vm.SuggestGammaConstantCommand.ExecuteAsync(null);

        // Assert
        // Field remains unchanged
        Assert.Equal(0.05, vm.EditGammaConstant);
        Assert.Equal("0.05", vm.EditGammaConstantText);
        Assert.True(vm.HasMessage);
        _libraryServiceMock.Verify(s => s.GetAllEntriesAsync(), Times.Never);
    }
}
