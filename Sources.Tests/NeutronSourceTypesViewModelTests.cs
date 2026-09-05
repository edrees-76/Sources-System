using System;
using System.Collections.Generic;
using Moq;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

public class NeutronSourceTypesViewModelTests
{
    private readonly Mock<INeutronSourceTypeService> _mockService;

    public NeutronSourceTypesViewModelTests()
    {
        _mockService = new Mock<INeutronSourceTypeService>();
        _mockService.Setup(s => s.GetAll()).Returns(new List<NeutronSourceType>());
    }

    private NeutronSourceTypesViewModel CreateViewModel() => new(_mockService.Object);

    [Fact]
    public void Save_Create_WithPhotonToNeutronRatio_PersistsRatio()
    {
        // Arrange
        NeutronSourceType? captured = null;
        _mockService
            .Setup(s => s.Create(It.IsAny<NeutronSourceType>()))
            .Callback<NeutronSourceType>(t => captured = t)
            .Returns((true, "تم إضافة النوع المرجعي بنجاح"));

        var vm = CreateViewModel();
        vm.AddNewCommand.Execute(null);
        vm.EditCode = "AmBe-Test";
        vm.EditHalfLifeText = "432.2";
        vm.EditPhotonToNeutronRatioText = "0.35";

        // Act
        vm.SaveCommand.Execute(null);

        // Assert
        _mockService.Verify(s => s.Create(It.IsAny<NeutronSourceType>()), Times.Once);
        Assert.NotNull(captured);
        Assert.Equal(0.35, captured!.PhotonToNeutronDoseRatio);
    }

    [Fact]
    public void Save_Update_WithPhotonToNeutronRatio_PersistsRatio()
    {
        // Arrange
        var typeId = Guid.NewGuid();
        var existing = new NeutronSourceType
        {
            Id = typeId,
            Code = "AmBe-Existing",
            HalfLife = 432.2,
            PhotonToNeutronDoseRatio = null
        };
        _mockService.Setup(s => s.GetById(typeId)).Returns(existing);

        NeutronSourceType? captured = null;
        _mockService
            .Setup(s => s.Update(It.IsAny<NeutronSourceType>()))
            .Callback<NeutronSourceType>(t => captured = t)
            .Returns((true, "تم تحديث النوع المرجعي بنجاح"));

        var vm = CreateViewModel();
        vm.EditCommand.Execute(existing);
        vm.EditPhotonToNeutronRatioText = "0.5";

        // Act
        vm.SaveCommand.Execute(null);

        // Assert
        _mockService.Verify(s => s.Update(It.IsAny<NeutronSourceType>()), Times.Once);
        Assert.NotNull(captured);
        Assert.Equal(0.5, captured!.PhotonToNeutronDoseRatio);
    }

    [Fact]
    public void Edit_PrefillsPhotonToNeutronRatioFromTarget()
    {
        // Arrange
        var target = new NeutronSourceType
        {
            Id = Guid.NewGuid(),
            Code = "AmBe-Edit",
            HalfLife = 432.2,
            PhotonToNeutronDoseRatio = 0.42
        };

        var vm = CreateViewModel();

        // Act
        vm.EditCommand.Execute(target);

        // Assert
        Assert.Equal(0.42, vm.EditPhotonToNeutronRatio);
        Assert.Equal("0.42", vm.EditPhotonToNeutronRatioText);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("abc")]
    public void Save_Create_WithNonFiniteRatio_RejectsBeforeCallingService(string invalidValue)
    {
        // Arrange
        var vm = CreateViewModel();
        vm.AddNewCommand.Execute(null);
        vm.EditCode = "AmBe-Invalid";
        vm.EditHalfLifeText = "432.2";
        vm.EditPhotonToNeutronRatioText = invalidValue;

        // Act
        vm.SaveCommand.Execute(null);

        // Assert
        _mockService.Verify(s => s.Create(It.IsAny<NeutronSourceType>()), Times.Never);
    }

    [Fact]
    public void Save_Create_WithNegativeFiniteRatio_IsAccepted()
    {
        // Arrange - a negative-but-finite ratio must NOT be rejected (dimensionless ratio, no <=0 guard per round 123's decision)
        NeutronSourceType? captured = null;
        _mockService
            .Setup(s => s.Create(It.IsAny<NeutronSourceType>()))
            .Callback<NeutronSourceType>(t => captured = t)
            .Returns((true, "تم إضافة النوع المرجعي بنجاح"));

        var vm = CreateViewModel();
        vm.AddNewCommand.Execute(null);
        vm.EditCode = "AmBe-Negative";
        vm.EditHalfLifeText = "432.2";
        vm.EditPhotonToNeutronRatioText = "-0.1";

        // Act
        vm.SaveCommand.Execute(null);

        // Assert
        _mockService.Verify(s => s.Create(It.IsAny<NeutronSourceType>()), Times.Once);
        Assert.NotNull(captured);
        Assert.Equal(-0.1, captured!.PhotonToNeutronDoseRatio);
    }
}
