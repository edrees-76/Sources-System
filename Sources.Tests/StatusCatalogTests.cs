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

    // كل حالة مخزَّنة: (القيمة المخزَّنة، الكود، النص العربي، النص الإنجليزي، اللون).
    private static readonly (string Stored, SourceStatusCode Code, string Arabic, string English, string Color)[] KnownStatuses =
    {
        ("InUse", SourceStatusCode.InUse, "قيد الاستخدام", "In Use", "#3FAE7A"),
        ("Storage", SourceStatusCode.Storage, "مخزن", "In Storage", "#4F7FA3"),
        ("Waste", SourceStatusCode.Waste, "نفايات", "Waste", "#E0A93E"),
        ("Transfer", SourceStatusCode.Transfer, "قيد النقل", "In Transfer", "#E0A93E"),
    };

    public static TheoryData<string, SourceStatusCode> KnownCodes()
    {
        var data = new TheoryData<string, SourceStatusCode>();
        foreach (var s in KnownStatuses) data.Add(s.Stored, s.Code);
        return data;
    }

    public static TheoryData<string, string> KnownArabic()
    {
        var data = new TheoryData<string, string>();
        foreach (var s in KnownStatuses) data.Add(s.Stored, s.Arabic);
        return data;
    }

    public static TheoryData<string, string> KnownEnglish()
    {
        var data = new TheoryData<string, string>();
        foreach (var s in KnownStatuses) data.Add(s.Stored, s.English);
        return data;
    }

    public static TheoryData<string, string> KnownColors()
    {
        var data = new TheoryData<string, string>();
        foreach (var s in KnownStatuses) data.Add(s.Stored, s.Color);
        return data;
    }

    [Theory]
    [MemberData(nameof(KnownCodes))]
    public void Parse_KnownStoredValue_ReturnsExpectedCode(string stored, SourceStatusCode expectedCode)
    {
        Assert.Equal(expectedCode, StatusCatalog.Parse(stored));
    }

    [Theory]
    [MemberData(nameof(KnownArabic))]
    public void GetArabicText_KnownStoredValue_ReturnsExactArabic(string stored, string expectedArabic)
    {
        Assert.Equal(expectedArabic, StatusCatalog.GetArabicText(stored));
    }

    [Theory]
    [MemberData(nameof(KnownEnglish))]
    public void GetDisplayText_WithEnglishLookup_ReturnsEnglishText(string stored, string expectedEnglish)
    {
        var enResources = LoadResourceDictionary("Strings.en.xaml");
        string? Lookup(string key) => enResources.TryGetValue(key, out var v) ? v : null;

        Assert.Equal(expectedEnglish, StatusCatalog.GetDisplayText(stored, Lookup));
    }

    [Theory]
    [MemberData(nameof(KnownArabic))]
    public void GetDisplayText_WithArabicLookup_ReturnsExactArabic(string stored, string expectedArabic)
    {
        var arResources = LoadResourceDictionary("Strings.ar.xaml");
        string? Lookup(string key) => arResources.TryGetValue(key, out var v) ? v : null;

        Assert.Equal(expectedArabic, StatusCatalog.GetDisplayText(stored, Lookup));
    }

    [Theory]
    [MemberData(nameof(KnownArabic))]
    public void GetDisplayText_WhenLookupReturnsNull_FallsBackToExactArabic(string stored, string expectedArabic)
    {
        Assert.Equal(expectedArabic, StatusCatalog.GetDisplayText(stored, _ => null));
    }

    [Theory]
    [MemberData(nameof(KnownColors))]
    public void GetColorHex_KnownStoredValue_ReturnsExpectedColor(string stored, string expectedColor)
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
