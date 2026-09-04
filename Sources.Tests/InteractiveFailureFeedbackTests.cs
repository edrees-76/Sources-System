using System;
using System.Collections.Generic;
using System.IO;
using Moq;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

public class InteractiveFailureFeedbackTests : IDisposable
{
    private readonly Mock<IRadioisotopeService> _mockIsotopeService;
    private readonly IDecayCalculationService _decayService;

    public InteractiveFailureFeedbackTests()
    {
        DialogHelper.LastMessage = null;
        DialogHelper.LastTitle = null;

        _mockIsotopeService = new Mock<IRadioisotopeService>();
        _decayService = new DecayCalculationService();

        var isotopes = new List<Radioisotope>
        {
            new Radioisotope
            {
                Id = Guid.NewGuid(),
                Name = "Cesium-137",
                Symbol = "Cs-137",
                HalfLife = 30.08,
                HalfLifeUnit = "years",
                RadiationType = "Beta/Gamma",
                GammaConstant = 0.0772
            }
        };

        _mockIsotopeService.Setup(s => s.GetAll()).Returns(isotopes);
    }

    public void Dispose()
    {
        DialogHelper.LastMessage = null;
        DialogHelper.LastTitle = null;
    }

    private ActivityCalculatorViewModel CreateCalculatorWithCalculatedResult(IClipboardService clipboard)
    {
        var vm = new ActivityCalculatorViewModel(_mockIsotopeService.Object, _decayService, clipboard);
        vm.IsFromDatabase = true;
        vm.SelectedIsotope = vm.Isotopes.Count > 0 ? vm.Isotopes[0] : null;
        vm.InitialActivityText = "100";
        vm.InitialActivityUnit = "MBq";
        vm.CalibrationDate = DateTime.Today.AddYears(-1);
        vm.CalculationDate = DateTime.Today;
        vm.CalculateCommand.Execute(null);
        return vm;
    }

    [Fact]
    public void CopyResult_WhenClipboardThrows_ShowsErrorDialogToUser()
    {
        // Arrange
        var mockClipboard = new Mock<IClipboardService>();
        mockClipboard.Setup(c => c.SetText(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Clipboard error"));

        var vm = CreateCalculatorWithCalculatedResult(mockClipboard.Object);
        Assert.False(string.IsNullOrEmpty(vm.ResultActivityText));

        // Act
        vm.CopyResultCommand.Execute(null);

        // Assert
        Assert.NotNull(DialogHelper.LastMessage);
        Assert.Contains("تعذّر نسخ النتيجة إلى الحافظة", DialogHelper.LastMessage);
        Assert.Equal("تعذّر النسخ", DialogHelper.LastTitle);
    }

    [Fact]
    public void CopyResult_WhenClipboardThrows_DoesNotThrow()
    {
        // Arrange
        var mockClipboard = new Mock<IClipboardService>();
        mockClipboard.Setup(c => c.SetText(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Clipboard locked by another process"));

        var vm = CreateCalculatorWithCalculatedResult(mockClipboard.Object);

        // Act
        var ex = Record.Exception(() => vm.CopyResultCommand.Execute(null));

        // Assert
        Assert.Null(ex);
    }

    [Fact]
    public void CopyResult_WhenClipboardSucceeds_ShowsNoDialog()
    {
        // Arrange
        string? capturedText = null;
        var mockClipboard = new Mock<IClipboardService>();
        mockClipboard.Setup(c => c.SetText(It.IsAny<string>()))
            .Callback<string>(text => capturedText = text);

        var vm = CreateCalculatorWithCalculatedResult(mockClipboard.Object);

        // Act
        vm.CopyResultCommand.Execute(null);

        // Assert
        Assert.Null(DialogHelper.LastMessage);
        Assert.NotNull(capturedText);
        Assert.Contains(vm.ResultActivityText, capturedText);
        mockClipboard.Verify(c => c.SetText(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void CopyResult_WhenNoResult_DoesNotCallClipboard()
    {
        // Arrange
        var mockClipboard = new Mock<IClipboardService>();
        var vm = new ActivityCalculatorViewModel(_mockIsotopeService.Object, _decayService, mockClipboard.Object);

        // Act
        vm.CopyResultCommand.Execute(null);

        // Assert
        mockClipboard.Verify(c => c.SetText(It.IsAny<string>()), Times.Never);
        Assert.Null(DialogHelper.LastMessage);
    }

    [Fact]
    public void OpenFile_WhenPathIsEmpty_ReturnsFalseAndShowsDialog()
    {
        // Act
        bool result = FileHelper.OpenFile(string.Empty);

        // Assert
        Assert.False(result);
        Assert.NotNull(DialogHelper.LastMessage);
        Assert.Contains("الملف غير موجود في المسار المحدد", DialogHelper.LastMessage);
    }

    [Fact]
    public void OpenFile_WhenFileDoesNotExist_ReturnsFalseAndShowsDialogContainingPath()
    {
        // Arrange
        string nonExistentPath = Path.Combine(Path.GetTempPath(), $"non_existent_file_{Guid.NewGuid():N}.pdf");

        // Act
        bool result = FileHelper.OpenFile(nonExistentPath);

        // Assert
        Assert.False(result);
        Assert.NotNull(DialogHelper.LastMessage);
        Assert.Contains(nonExistentPath, DialogHelper.LastMessage);
    }

    [Fact]
    public void OpenFile_WhenFileDoesNotExist_DoesNotThrow()
    {
        // Arrange
        string nonExistentPath = Path.Combine(Path.GetTempPath(), $"non_existent_file_{Guid.NewGuid():N}.pdf");

        // Act
        var ex = Record.Exception(() => FileHelper.OpenFile(nonExistentPath));

        // Assert
        Assert.Null(ex);
    }
}
