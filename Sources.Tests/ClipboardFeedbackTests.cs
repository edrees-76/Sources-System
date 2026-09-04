using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

public class ClipboardFeedbackTests : IDisposable
{
    private readonly Mock<IIsotopeLibraryService> _mockLibraryService;

    public ClipboardFeedbackTests()
    {
        DialogHelper.LastMessage = null;
        DialogHelper.LastTitle = null;

        _mockLibraryService = new Mock<IIsotopeLibraryService>();
        _mockLibraryService.Setup(s => s.GetAllEntriesAsync())
            .ReturnsAsync(new List<IsotopeReferenceEntry>());
        _mockLibraryService.Setup(s => s.SearchAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<IsotopeReferenceEntry>());
    }

    public void Dispose()
    {
        DialogHelper.LastMessage = null;
        DialogHelper.LastTitle = null;
    }

    // ─── ClipboardCopyHelper Tests ───

    [Fact]
    public void CopyWithFeedback_WhenClipboardSucceeds_ReturnsTrueAndShowsSuccessMessage()
    {
        // Arrange
        var mockClipboard = new Mock<IClipboardService>();
        string expectedText = "Sample text";
        string successMsg = "تم النسخ بنجاح";
        string successTitle = "نجاح";

        // Act
        bool result = ClipboardCopyHelper.CopyWithFeedback(
            mockClipboard.Object,
            expectedText,
            successMsg,
            successTitle,
            "TestContext");

        // Assert
        Assert.True(result);
        mockClipboard.Verify(c => c.SetText(expectedText), Times.Once);
        Assert.Equal(successMsg, DialogHelper.LastMessage);
        Assert.Equal(successTitle, DialogHelper.LastTitle);
    }

    [Fact]
    public void CopyWithFeedback_WhenClipboardThrows_ReturnsFalseAndShowsFailureMessage()
    {
        // Arrange
        var mockClipboard = new Mock<IClipboardService>();
        mockClipboard.Setup(c => c.SetText(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Clipboard locked"));

        string successMsg = "تم النسخ بنجاح";
        string successTitle = "نجاح";

        // Act
        bool result = ClipboardCopyHelper.CopyWithFeedback(
            mockClipboard.Object,
            "Sample text",
            successMsg,
            successTitle,
            "TestContext");

        // Assert
        Assert.False(result);
        Assert.NotNull(DialogHelper.LastMessage);
        Assert.Equal(ClipboardCopyHelper.FallbackFailureMessage, DialogHelper.LastMessage);
        Assert.NotEqual(successMsg, DialogHelper.LastMessage);
        Assert.Equal(ClipboardCopyHelper.FallbackFailureTitle, DialogHelper.LastTitle);
    }

    [Fact]
    public void CopyWithFeedback_WhenClipboardThrows_DoesNotThrow()
    {
        // Arrange
        var mockClipboard = new Mock<IClipboardService>();
        mockClipboard.Setup(c => c.SetText(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Clipboard locked"));

        // Act
        var ex = Record.Exception(() => ClipboardCopyHelper.CopyWithFeedback(
            mockClipboard.Object,
            "Sample text",
            "Success",
            "Title",
            "TestContext"));

        // Assert
        Assert.Null(ex);
    }

    [Fact]
    public void CopyWithFeedback_WhenTextIsNullOrWhitespace_ReturnsFalseAndDoesNotCallClipboard()
    {
        // Arrange
        var mockClipboard = new Mock<IClipboardService>();

        // Act
        bool resultNull = ClipboardCopyHelper.CopyWithFeedback(mockClipboard.Object, null, "S", "T", "Ctx");
        bool resultEmpty = ClipboardCopyHelper.CopyWithFeedback(mockClipboard.Object, "", "S", "T", "Ctx");
        bool resultWhitespace = ClipboardCopyHelper.CopyWithFeedback(mockClipboard.Object, "   ", "S", "T", "Ctx");

        // Assert
        Assert.False(resultNull);
        Assert.False(resultEmpty);
        Assert.False(resultWhitespace);
        mockClipboard.Verify(c => c.SetText(It.IsAny<string>()), Times.Never);
        Assert.Null(DialogHelper.LastMessage);
    }

    // ─── IsotopeLibraryViewModel Tests ───

    [Fact]
    public void CopyDetails_WhenClipboardThrows_ShowsFailureMessageToUser()
    {
        // Arrange
        var mockClipboard = new Mock<IClipboardService>();
        mockClipboard.Setup(c => c.SetText(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Clipboard locked"));

        var entry = new IsotopeReferenceEntry { NuclideSymbol = "Cs-137", HalfLife = "30.08 y" };
        var vm = new IsotopeLibraryViewModel(_mockLibraryService.Object, mockClipboard.Object)
        {
            SelectedEntry = entry
        };

        // Act
        vm.CopyDetailsCommand.Execute(null);

        // Assert
        Assert.NotNull(DialogHelper.LastMessage);
        Assert.Equal(ClipboardCopyHelper.FallbackFailureMessage, DialogHelper.LastMessage);
        Assert.Equal(ClipboardCopyHelper.FallbackFailureTitle, DialogHelper.LastTitle);
    }

    [Fact]
    public void CopyDetails_WhenClipboardSucceeds_ShowsSuccessMessage()
    {
        // Arrange
        var mockClipboard = new Mock<IClipboardService>();
        var entry = new IsotopeReferenceEntry { NuclideSymbol = "Co-60", HalfLife = "5.27 y" };
        var vm = new IsotopeLibraryViewModel(_mockLibraryService.Object, mockClipboard.Object)
        {
            SelectedEntry = entry
        };

        // Act
        vm.CopyDetailsCommand.Execute(null);

        // Assert
        Assert.NotNull(DialogHelper.LastMessage);
        Assert.NotEqual(ClipboardCopyHelper.FallbackFailureMessage, DialogHelper.LastMessage);
        Assert.NotEqual(ClipboardCopyHelper.FallbackFailureTitle, DialogHelper.LastTitle);
        mockClipboard.Verify(c => c.SetText(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void CopyDetails_WhenNoEntrySelected_DoesNotCallClipboard()
    {
        // Arrange
        var mockClipboard = new Mock<IClipboardService>();
        var vm = new IsotopeLibraryViewModel(_mockLibraryService.Object, mockClipboard.Object)
        {
            SelectedEntry = null
        };

        // Act
        vm.CopyDetailsCommand.Execute(null);

        // Assert
        mockClipboard.Verify(c => c.SetText(It.IsAny<string>()), Times.Never);
        Assert.Null(DialogHelper.LastMessage);
    }

    [Fact]
    public void CopyValue_WhenClipboardThrows_ShowsFailureMessageToUser()
    {
        // Arrange
        var mockClipboard = new Mock<IClipboardService>();
        mockClipboard.Setup(c => c.SetText(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Clipboard locked"));

        var vm = new IsotopeLibraryViewModel(_mockLibraryService.Object, mockClipboard.Object);

        // Act
        vm.CopyValueCommand.Execute("30.08 years");

        // Assert
        Assert.NotNull(DialogHelper.LastMessage);
        Assert.Equal(ClipboardCopyHelper.FallbackFailureMessage, DialogHelper.LastMessage);
        Assert.Equal(ClipboardCopyHelper.FallbackFailureTitle, DialogHelper.LastTitle);
    }

    [Fact]
    public void CopyValue_WhenValueIsDash_DoesNotCallClipboard()
    {
        // Arrange
        var mockClipboard = new Mock<IClipboardService>();
        var vm = new IsotopeLibraryViewModel(_mockLibraryService.Object, mockClipboard.Object);

        // Act
        vm.CopyValueCommand.Execute("—");

        // Assert
        mockClipboard.Verify(c => c.SetText(It.IsAny<string>()), Times.Never);
        Assert.Null(DialogHelper.LastMessage);
    }
}
