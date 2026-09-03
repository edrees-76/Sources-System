using System;
using Sources.Helpers;
using Xunit;

namespace Sources.Tests;

public class LogTextHelperTests
{
    [Fact]
    public void Truncate_WhenLengthIs49_ReturnsUnchangedWithoutEllipsis()
    {
        // 1. قيمة طولها 49 → تُرجَع كما هي بلا «…» (الحالة السليمة الأقرب للحد)
        var input = new string('a', 49);
        var result = LogTextHelper.Truncate(input);

        Assert.Equal(49, result.Length);
        Assert.Equal(input, result);
        Assert.DoesNotContain("…", result);
    }

    [Fact]
    public void Truncate_WhenLengthIs50_ReturnsUnchangedWithoutEllipsis()
    {
        // 2. قيمة طولها 50 بالضبط → تُرجَع كما هي بلا «…» (الحد بالضبط)
        var input = new string('b', 50);
        var result = LogTextHelper.Truncate(input);

        Assert.Equal(50, result.Length);
        Assert.Equal(input, result);
        Assert.DoesNotContain("…", result);
    }

    [Fact]
    public void Truncate_WhenLengthIs51_ReturnsLength50EndingWithEllipsisAndMatchesFirst49Chars()
    {
        // 3. قيمة طولها 51 → الناتج طوله 50 بالضبط، ينتهي بـ «…»، وأول 49 محرفاً تطابق أصل القيمة
        var input = new string('c', 49) + "12";
        var result = LogTextHelper.Truncate(input);

        Assert.Equal(50, result.Length);
        Assert.EndsWith("…", result);
        Assert.Equal(input.Substring(0, 49) + "…", result);
        Assert.Equal(input.Substring(0, 49), result.Substring(0, 49));
    }

    [Fact]
    public void Truncate_WhenLengthIs200_ReturnsLength50()
    {
        // 4. قيمة طولها 200 → الناتج طوله 50 بالضبط
        var input = new string('d', 200);
        var result = LogTextHelper.Truncate(input);

        Assert.Equal(50, result.Length);
        Assert.EndsWith("…", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Truncate_WhenNullOrEmpty_ReturnsEmptyString(string? input)
    {
        // 5. null و "" → string.Empty في الحالتين
        var result = LogTextHelper.Truncate(input);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Truncate_WhenCustomMaxLength_ReturnsLengthExactlyEqualToMaxLength()
    {
        // 6. maxLength مخصّص (مثلاً 10) → الناتج طوله 10 بالضبط
        var input = "1234567890abcdef";
        var result = LogTextHelper.Truncate(input, 10);

        Assert.Equal(10, result.Length);
        Assert.EndsWith("…", result);
        Assert.Equal("123456789…", result);
    }

    [Fact]
    public void Truncate_WhenSurrogatePairAtBoundary_DoesNotSplitSurrogatePairAndDoesNotExceedMax()
    {
        // 7. قيمة تضع زوجاً بديلاً عند موضع القطع → الناتج لا ينتهي بـ high surrogate منفرد، ولا يتجاوز الطول الأقصى
        var input = new string('x', 48) + "😀" + new string('y', 20);
        var result = LogTextHelper.Truncate(input, 50);

        Assert.True(result.Length <= 50, "Result length must not exceed maxLength.");
        Assert.EndsWith("…", result);
        Assert.False(char.IsHighSurrogate(result[^2]), "Cut must not orphan a high surrogate before the ellipsis.");
        Assert.Equal(new string('x', 48) + "…", result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Truncate_WhenMaxLengthIsInvalid_ThrowsArgumentOutOfRangeException(int invalidMaxLength)
    {
        // 8. maxLength = 0 → ArgumentOutOfRangeException
        Assert.Throws<ArgumentOutOfRangeException>(() => LogTextHelper.Truncate("test", invalidMaxLength));
    }
}
