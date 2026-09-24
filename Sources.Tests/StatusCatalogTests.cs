using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Sources.Helpers;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// الجولة 200: اختبارات كتالوج الحالات (StatusCatalog) — قيم الذهاب والإياب لكل حالة مخزَّنة
/// وسلوك القيم غير المعروفة (Unknown)، دون أي اعتماد على WPF Application.
/// </summary>
public class StatusCatalogTests
{
    private static string GetProjectDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Sources.sln")))
        {
            dir = dir.Parent;
        }
        if (dir == null)
        {
            throw new DirectoryNotFoundException("Could not locate Sources.sln");
        }
        return Path.Combine(dir.FullName, "Sources-System-Project");
    }

    private static Dictionary<string, string> LoadResourceDictionary(string fileName)
    {
        var path = Path.Combine(GetProjectDirectory(), "Resources", fileName);
        var content = File.ReadAllText(path);
        var matches = Regex.Matches(content, @"x:Key=""([^""]+)""[^>]*>([^<]*)</system:String>");
        var dict = new Dictionary<string, string>();
        foreach (Match m in matches)
        {
            dict[m.Groups[1].Value] = m.Groups[2].Value;
        }
        return dict;
    }

    public static IEnumerable<object[]> KnownStatuses => new List<object[]>
    {
        new object[] { "InUse", SourceStatusCode.InUse, "قيد الاستخدام", "In Use", "#3FAE7A" },
        new object[] { "Storage", SourceStatusCode.Storage, "مخزن", "In Storage", "#4F7FA3" },
        new object[] { "Waste", SourceStatusCode.Waste, "نفايات", "Waste", "#E0A93E" },
        new object[] { "Transfer", SourceStatusCode.Transfer, "قيد النقل", "In Transfer", "#E0A93E" },
    };

    [Theory]
    [MemberData(nameof(KnownStatuses))]
    public void Parse_KnownStoredValue_ReturnsExpectedCode(string stored, SourceStatusCode expectedCode, string _, string __, string ___)
    {
        Assert.Equal(expectedCode, StatusCatalog.Parse(stored));
    }

    [Theory]
    [MemberData(nameof(KnownStatuses))]
    public void GetArabicText_KnownStoredValue_ReturnsExactArabic(string stored, SourceStatusCode _, string expectedArabic, string __, string ___)
    {
        Assert.Equal(expectedArabic, StatusCatalog.GetArabicText(stored));
    }

    [Theory]
    [MemberData(nameof(KnownStatuses))]
    public void GetDisplayText_WithEnglishLookup_ReturnsEnglishText(string stored, SourceStatusCode _, string __, string expectedEnglish, string ___)
    {
        var enResources = LoadResourceDictionary("Strings.en.xaml");
        string? Lookup(string key) => enResources.TryGetValue(key, out var v) ? v : null;

        Assert.Equal(expectedEnglish, StatusCatalog.GetDisplayText(stored, Lookup));
    }

    [Theory]
    [MemberData(nameof(KnownStatuses))]
    public void GetDisplayText_WithArabicLookup_ReturnsExactArabic(string stored, SourceStatusCode _, string expectedArabic, string __, string ___)
    {
        var arResources = LoadResourceDictionary("Strings.ar.xaml");
        string? Lookup(string key) => arResources.TryGetValue(key, out var v) ? v : null;

        Assert.Equal(expectedArabic, StatusCatalog.GetDisplayText(stored, Lookup));
    }

    [Theory]
    [MemberData(nameof(KnownStatuses))]
    public void GetDisplayText_WhenLookupReturnsNull_FallsBackToExactArabic(string stored, SourceStatusCode _, string expectedArabic, string __, string ___)
    {
        Assert.Equal(expectedArabic, StatusCatalog.GetDisplayText(stored, _ => null));
    }

    [Theory]
    [MemberData(nameof(KnownStatuses))]
    public void GetColorHex_KnownStoredValue_ReturnsExpectedColor(string stored, SourceStatusCode _, string __, string ___, string expectedColor)
    {
        Assert.Equal(expectedColor, StatusCatalog.GetColorHex(stored));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Active")]
    [InlineData("Decayed")]
    [InlineData("Disposed")]
    [InlineData("Lost")]
    [InlineData("garbage")]
    [InlineData(" InUse")]
    [InlineData("inuse")]
    public void UnknownOrLegacyValues_NeverThrow_ReturnUnknownAndRawText(string? stored)
    {
        var code = StatusCatalog.Parse(stored);
        Assert.Equal(SourceStatusCode.Unknown, code);

        var arabic = StatusCatalog.GetArabicText(stored);
        Assert.Equal(stored ?? "", arabic);

        var display = StatusCatalog.GetDisplayText(stored, _ => "should-not-be-used-for-unknown");
        Assert.Equal(stored ?? "", display);

        Assert.Equal("#9E9E9E", StatusCatalog.GetColorHex(stored));
    }

    [Fact]
    public void ResourceKeys_StatusDisplayFour_ExistInBothDictionaries_WithExpectedValues()
    {
        var ar = LoadResourceDictionary("Strings.ar.xaml");
        var en = LoadResourceDictionary("Strings.en.xaml");

        Assert.Equal("قيد الاستخدام", ar["StatusDisplayInUse"]);
        Assert.Equal("مخزن", ar["StatusDisplayStorage"]);
        Assert.Equal("نفايات", ar["StatusDisplayWaste"]);
        Assert.Equal("قيد النقل", ar["StatusDisplayTransfer"]);

        Assert.Equal("In Use", en["StatusDisplayInUse"]);
        Assert.Equal("In Storage", en["StatusDisplayStorage"]);
        Assert.Equal("Waste", en["StatusDisplayWaste"]);
        Assert.Equal("In Transfer", en["StatusDisplayTransfer"]);
    }
}
