using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Sources.Helpers;
using Xunit;

namespace Sources.Tests;

public class TranslationKeysTests
{
    private static readonly HashSet<string> KnownDeadKeys = new()
    {
        "LabelSerialShort" // Unused key in English dictionary, excluded per Round 100 specifications
    };

    private static string GetProjectDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Sources.sln")))
        {
            dir = dir.Parent;
        }

        if (dir != null)
        {
            var projDir = Path.Combine(dir.FullName, "Sources-System-Project");
            if (Directory.Exists(projDir))
            {
                return projDir;
            }
        }

        var fallback = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Sources-System-Project"));
        if (Directory.Exists(fallback))
        {
            return fallback;
        }

        throw new DirectoryNotFoundException("Could not locate Sources-System-Project relative to test assembly directory.");
    }

    private static HashSet<string> ExtractKeysFromXaml(string xamlFilePath)
    {
        if (!File.Exists(xamlFilePath))
            throw new FileNotFoundException($"XAML file not found: {xamlFilePath}");

        var content = File.ReadAllText(xamlFilePath);
        var matches = Regex.Matches(content, @"x:Key=""([^""]+)""");
        return matches.Select(m => m.Groups[1].Value).ToHashSet();
    }

    /// <summary>
    /// 1. اختبار التطابق بين اللغتين: يقرأ ملفي الترجمة ويتحقق أن المجموعتين متطابقتان
    /// </summary>
    [Fact]
    public void AllKeys_MustMatchBetweenArabicAndEnglishDictionaries()
    {
        var projDir = GetProjectDirectory();
        var arPath = Path.Combine(projDir, "Resources", "Strings.ar.xaml");
        var enPath = Path.Combine(projDir, "Resources", "Strings.en.xaml");

        var arKeys = ExtractKeysFromXaml(arPath);
        var enKeys = ExtractKeysFromXaml(enPath);

        var missingInAr = enKeys.Except(arKeys).Except(KnownDeadKeys).OrderBy(k => k).ToList();
        var missingInEn = arKeys.Except(enKeys).OrderBy(k => k).ToList();

        var errorMsg = string.Empty;
        if (missingInAr.Any())
        {
            errorMsg += $"Keys missing in Strings.ar.xaml ({missingInAr.Count}): {string.Join(", ", missingInAr)}\n";
        }
        if (missingInEn.Any())
        {
            errorMsg += $"Keys missing in Strings.en.xaml ({missingInEn.Count}): {string.Join(", ", missingInEn)}\n";
        }

        Assert.True(string.IsNullOrEmpty(errorMsg), errorMsg);
    }

    /// <summary>
    /// 2. اختبار اكتمال المفاتيح المستعملة: يتحقق أن كل مفتاح مستعمل في C# موجود في ملفي الترجمة
    /// </summary>
    [Fact]
    public void AllKeysUsedInCode_MustExistInBothDictionaries()
    {
        var projDir = GetProjectDirectory();
        var arPath = Path.Combine(projDir, "Resources", "Strings.ar.xaml");
        var enPath = Path.Combine(projDir, "Resources", "Strings.en.xaml");

        var arKeys = ExtractKeysFromXaml(arPath);
        var enKeys = ExtractKeysFromXaml(enPath);

        var csFiles = Directory.GetFiles(projDir, "*.cs", SearchOption.AllDirectories);
        var keyUsages = new Dictionary<string, List<string>>();

        foreach (var file in csFiles)
        {
            var content = File.ReadAllText(file);
            var relativeFile = Path.GetRelativePath(projDir, file);

            var matches = Regex.Matches(content, @"(?:TranslationHelper\s*\.\s*GetString|GetString|TranslationHelper\s*\.\s*GetFormat|GetFormat)\s*\(\s*""([^""]+)""");
            foreach (Match m in matches)
            {
                var key = m.Groups[1].Value;
                if (!keyUsages.ContainsKey(key))
                {
                    keyUsages[key] = new List<string>();
                }
                keyUsages[key].Add(relativeFile);
            }
        }

        var missingInAr = new List<string>();
        var missingInEn = new List<string>();

        foreach (var kvp in keyUsages)
        {
            var key = kvp.Key;
            var files = string.Join(", ", kvp.Value.Distinct());

            if (!arKeys.Contains(key))
            {
                missingInAr.Add($"{key} (used in: {files})");
            }
            if (!enKeys.Contains(key))
            {
                missingInEn.Add($"{key} (used in: {files})");
            }
        }

        var errorMsg = string.Empty;
        if (missingInAr.Any())
        {
            errorMsg += $"Keys used in C# but missing in Strings.ar.xaml ({missingInAr.Count}):\n - " + string.Join("\n - ", missingInAr) + "\n";
        }
        if (missingInEn.Any())
        {
            errorMsg += $"Keys used in C# but missing in Strings.en.xaml ({missingInEn.Count}):\n - " + string.Join("\n - ", missingInEn) + "\n";
        }

        Assert.True(string.IsNullOrEmpty(errorMsg), errorMsg);
    }

    /// <summary>
    /// 3. اختبار سلوك الارتداد: GetString بمفتاح غير موجود تُرجع null
    /// </summary>
    [Fact]
    public void GetString_WhenKeyNotFound_ReturnsNull()
    {
        var result = TranslationHelper.GetString("NonExistentTestKey_Round100_XYZ");
        Assert.Null(result);
    }
}
