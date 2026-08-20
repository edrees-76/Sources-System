using Sources.Helpers;
using Xunit;

namespace Sources.Tests;

public class ArabicReshaperTests
{
    [Fact]
    public void IsArabicChar_IdentifiesArabicLettersCorrectly()
    {
        Assert.True(ArabicReshaper.IsArabicChar('ع'));
        Assert.True(ArabicReshaper.IsArabicChar('ا'));
        Assert.True(ArabicReshaper.IsArabicChar('ل'));
        Assert.False(ArabicReshaper.IsArabicChar('A'));
        Assert.False(ArabicReshaper.IsArabicChar('1'));
    }

    [Fact]
    public void ReshapeAndReverse_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, ArabicReshaper.ReshapeAndReverse(null));
        Assert.Equal(string.Empty, ArabicReshaper.ReshapeAndReverse(""));
    }

    [Fact]
    public void ReshapeAndReverse_EnglishText_ReturnsSame()
    {
        var english = "Source Code: SRC-001";
        var result = ArabicReshaper.ReshapeAndReverse(english);
        Assert.Equal(english, result);
    }

    [Fact]
    public void ReshapeAndReverse_ArabicWord_ReshapesAndReverses()
    {
        var arabic = "موقع";
        var result = ArabicReshaper.ReshapeAndReverse(arabic);

        Assert.NotEmpty(result);
        Assert.NotEqual(arabic, result); // Has presentation forms in reverse order
    }

    [Fact]
    public void ReshapeAndReverse_LamAlef_HandlesLigatureCorrectly()
    {
        var text = "الإشعاعي";
        var result = ArabicReshaper.ReshapeAndReverse(text);

        Assert.NotEmpty(result);
    }
}
