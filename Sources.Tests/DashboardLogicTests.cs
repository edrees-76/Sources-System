using System.IO;
using Sources.ViewModels;

namespace Sources.Tests;

/// <summary>
/// اختبارات وحدة للمنطق البحت في لوحة التحكم (DashboardViewModel)
/// البند 1: Histogram bins — البند 2: Top-10+Others
/// </summary>
public class DashboardLogicTests
{
    // ═══════════════════════════════════════════════════════════════════════
    // البند 1: Histogram Bins Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void HistogramBins_CorrectCounts_WhenSourcesSpreadAcrossRanges()
    {
        // Arrange: نشاطات تقع في نطاقات مختلفة
        var activities = new double[]
        {
            500,        // < 10³ (bin 0)
            100,        // < 10³ (bin 0)
            5_000,      // 10³ – 10⁶ (bin 1)
            500_000,    // 10³ – 10⁶ (bin 1)
            2_000_000,  // 10⁶ – 10⁹ (bin 2)
            5e9,        // 10⁹ – 10¹² (bin 3)
            1e13,       // 10¹² – 10¹⁵ (bin 4)
            2e16,       // > 10¹⁵ (bin 5)
        };

        // Act
        var counts = DashboardViewModel.ComputeHistogramBins(activities);

        // Assert
        Assert.Equal(6, counts.Length);
        Assert.Equal(2, counts[0]); // < 10³
        Assert.Equal(2, counts[1]); // 10³ – 10⁶
        Assert.Equal(1, counts[2]); // 10⁶ – 10⁹
        Assert.Equal(1, counts[3]); // 10⁹ – 10¹²
        Assert.Equal(1, counts[4]); // 10¹² – 10¹⁵
        Assert.Equal(1, counts[5]); // > 10¹⁵
        Assert.Equal(8, counts.Sum()); // مجموع = الإجمالي
    }

    [Fact]
    public void HistogramBins_AllInOneBin_WhenAllActivitiesInSameRange()
    {
        // Arrange: كل المصادر في النطاق 10³ – 10⁶
        var activities = Enumerable.Range(1, 50).Select(i => 5000.0 + i * 100).ToArray();

        // Act
        var counts = DashboardViewModel.ComputeHistogramBins(activities);

        // Assert
        Assert.Equal(0, counts[0]);
        Assert.Equal(50, counts[1]); // كلها في bin 1
        Assert.Equal(0, counts[2]);
        Assert.Equal(0, counts[3]);
        Assert.Equal(0, counts[4]);
        Assert.Equal(0, counts[5]);
    }

    [Fact]
    public void HistogramBins_EmptySources_AllBinsZero()
    {
        // Act
        var counts = DashboardViewModel.ComputeHistogramBins(Array.Empty<double>());

        // Assert
        Assert.Equal(6, counts.Length);
        Assert.All(counts, c => Assert.Equal(0, c));
    }

    [Fact]
    public void HistogramBins_ZeroAndNegativeActivities_Ignored()
    {
        // Arrange
        var activities = new double[] { 0, -100, -5e6, 0 };

        // Act
        var counts = DashboardViewModel.ComputeHistogramBins(activities);

        // Assert
        Assert.All(counts, c => Assert.Equal(0, c));
    }

    [Fact]
    public void HistogramBins_BoundaryValues_CorrectPlacement()
    {
        // Arrange: اختبار القيم الحدية لكل نطاق
        var activities = new double[]
        {
            1,          // < 10³ (bin 0)
            999.99,     // < 10³ (bin 0) — حد أعلى بالضبط
            1000,       // = 10³ → 10³–10⁶ (bin 1)
            999_999.99, // 10³–10⁶ (bin 1)
            1e6,        // = 10⁶ → 10⁶–10⁹ (bin 2)
            1e9,        // = 10⁹ → 10⁹–10¹² (bin 3)
            1e12,       // = 10¹² → 10¹²–10¹⁵ (bin 4)
            1e15,       // = 10¹⁵ → > 10¹⁵ (bin 5)
        };

        // Act
        var counts = DashboardViewModel.ComputeHistogramBins(activities);

        // Assert
        Assert.Equal(2, counts[0]); // 1, 999.99
        Assert.Equal(2, counts[1]); // 1000, 999999.99
        Assert.Equal(1, counts[2]); // 1e6
        Assert.Equal(1, counts[3]); // 1e9
        Assert.Equal(1, counts[4]); // 1e12
        Assert.Equal(1, counts[5]); // 1e15
    }

    [Fact]
    public void HistogramBins_LargeDataset_SumEqualsTotal()
    {
        // Arrange: 300 مصدر (كما في بيانات الاختبار)
        var rng = new Random(42);
        var activities = Enumerable.Range(0, 300)
            .Select(_ => Math.Pow(10, rng.NextDouble() * 18)) // 10⁰ to 10¹⁸
            .ToArray();

        // Act
        var counts = DashboardViewModel.ComputeHistogramBins(activities);

        // Assert
        Assert.Equal(300, counts.Sum());
    }

    // ═══════════════════════════════════════════════════════════════════════
    // البند 2: Top-10 + Others Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void TopTenPlusOthers_CorrectOthersCount()
    {
        // Arrange: 15 عنصر
        var items = Enumerable.Range(1, 15)
            .Select(i => ($"Item-{i}", i))
            .ToList();

        // Act
        var result = DashboardViewModel.ComputeTopNPlusOthers(items, 10, "أخرى");

        // Assert
        Assert.Equal(11, result.Count); // top 10 + "أخرى"
        Assert.Equal("أخرى", result.Last().Label);

        int expectedOthers = Enumerable.Range(1, 5).Sum(); // 1+2+3+4+5 = 15 (أقل 5 قيم)
        Assert.Equal(expectedOthers, result.Last().Count);

        int totalOriginal = items.Sum(x => x.Item2);
        int totalResult = result.Sum(x => x.Count);
        Assert.Equal(totalOriginal, totalResult);
    }

    [Fact]
    public void TopTenPlusOthers_LessThan10Items_NoOthers()
    {
        // Arrange: 7 عناصر فقط
        var items = Enumerable.Range(1, 7)
            .Select(i => ($"Item-{i}", i * 10))
            .ToList();

        // Act
        var result = DashboardViewModel.ComputeTopNPlusOthers(items, 10, "أخرى");

        // Assert
        Assert.Equal(7, result.Count);
        Assert.DoesNotContain(result, x => x.Label == "أخرى");
    }

    [Fact]
    public void TopTenPlusOthers_ExactlyTenItems_NoOthers()
    {
        // Arrange: 10 عناصر بالضبط
        var items = Enumerable.Range(1, 10)
            .Select(i => ($"Item-{i}", i))
            .ToList();

        // Act
        var result = DashboardViewModel.ComputeTopNPlusOthers(items, 10, "أخرى");

        // Assert
        Assert.Equal(10, result.Count);
        Assert.DoesNotContain(result, x => x.Label == "أخرى");
    }

    [Fact]
    public void TopTenPlusOthers_SingleItem_NoOthers()
    {
        var items = new List<(string, int)> { ("Only", 100) };

        var result = DashboardViewModel.ComputeTopNPlusOthers(items, 10, "Others");

        Assert.Single(result);
        Assert.Equal("Only", result[0].Label);
        Assert.Equal(100, result[0].Count);
    }

    [Fact]
    public void TopTenPlusOthers_EmptyList_ReturnsEmpty()
    {
        var result = DashboardViewModel.ComputeTopNPlusOthers(
            Enumerable.Empty<(string, int)>(), 10, "Others");

        Assert.Empty(result);
    }

    [Fact]
    public void TopTenPlusOthers_OrderedDescending()
    {
        var items = new List<(string Label, int Count)>
        {
            ("A", 5), ("B", 100), ("C", 50), ("D", 1)
        };

        var result = DashboardViewModel.ComputeTopNPlusOthers(items, 10, "Others");

        // تحقق من الترتيب التنازلي
        Assert.Equal("B", result[0].Label);
        Assert.Equal("C", result[1].Label);
        Assert.Equal("A", result[2].Label);
        Assert.Equal("D", result[3].Label);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // البند 6: Category Color Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(1, "#8B0000")]
    [InlineData(2, "#D84315")]
    [InlineData(3, "#FB8C00")]
    [InlineData(4, "#FFD600")]
    [InlineData(5, "#B0BEC5")]
    [InlineData(0, "#B0BEC5")]
    [InlineData(99, "#B0BEC5")]
    public void GetCategoryColor_ReturnsCorrectHex(int category, string expectedHex)
    {
        var result = DashboardViewModel.GetCategoryColor(category);
        Assert.Equal(expectedHex, result);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // اختبارات FormatScientificBq
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0, "0 Bq")]
    [InlineData(500, "500 Bq")]
    [InlineData(999, "999 Bq")]
    public void FormatScientificBq_SmallValues(double bq, string expected)
    {
        Assert.Equal(expected, DashboardViewModel.FormatScientificBq(bq));
    }

    [Fact]
    public void FormatScientificBq_LargeValue_ContainsSuperscript()
    {
        var result = DashboardViewModel.FormatScientificBq(1.5e9);
        Assert.Contains("×10", result);
        Assert.Contains("Bq", result);
    }

    [Theory]
    [InlineData("Strings.ar.xaml")]
    [InlineData("Strings.en.xaml")]
    [InlineData("Colors.xaml")]
    [InlineData("Converters.xaml")]
    [InlineData("Styles.xaml")]
    [InlineData("LightTheme.xaml")]
    [InlineData("DarkTheme.xaml")]
    public void XamlResourceDictionaries_HaveNoDuplicateKeys(string fileName)
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Sources-System-Project", "Resources", fileName);
        if (!File.Exists(path))
            path = Path.Combine(@"d:\Sources-System\Sources-System-Project\Resources", fileName);

        Assert.True(File.Exists(path), $"File not found: {path}");

        var lines = File.ReadAllLines(path);
        var seen = new Dictionary<string, int>();
        var duplicates = new List<string>();

        var regex = new System.Text.RegularExpressions.Regex(@"x:Key=""([^""]+)""");
        for (int i = 0; i < lines.Length; i++)
        {
            var match = regex.Match(lines[i]);
            if (match.Success)
            {
                string key = match.Groups[1].Value;
                if (seen.ContainsKey(key))
                {
                    duplicates.Add($"Key '{key}' duplicate found at line {i + 1} (previously at line {seen[key]})");
                }
                else
                {
                    seen[key] = i + 1;
                }
            }
        }

        Assert.True(duplicates.Count == 0, $"Duplicates in {fileName}:\n" + string.Join("\n", duplicates));
    }

    [Theory]
    [InlineData(1, 20, 0, 1)]
    [InlineData(1, 20, 19, 20)]
    [InlineData(2, 20, 0, 21)]
    [InlineData(2, 20, 19, 40)]
    [InlineData(3, 50, 0, 101)]
    [InlineData(3, 50, 49, 150)]
    public void PaginationRowNumber_ContinuityAcrossPages(int currentPage, int pageSize, int indexInPage, int expectedRowNumber)
    {
        int skip = (currentPage - 1) * pageSize;
        int rowNumber = skip + indexInPage + 1;
        Assert.Equal(expectedRowNumber, rowNumber);
    }

    #region AutoFlipChartTooltip Tests

    [Fact]
    public void CalculateOptimalPosition_WhenPointAtFarLeft_AutoFlipsToRight_AndDoesNotClip()
    {
        // Scenario: Tc-99m / Nuclear Medicine with long bar extending to the far left (X = 40px)
        double targetX = 40.0;
        double targetY = 50.0;
        double tooltipWidth = 180.0;
        double tooltipHeight = 40.0;
        double controlWidth = 400.0;
        double controlHeight = 300.0;

        var (x, y) = Sources.Helpers.AutoFlipChartTooltip.CalculateOptimalPosition(
            targetX, targetY, tooltipWidth, tooltipHeight, controlWidth, controlHeight);

        // Assert: Tooltip must be placed to the RIGHT of targetX (since spaceLeft = 40 < tooltipWidth)
        Assert.True(x >= targetX, $"Expected tooltip X ({x}) to be to the right of targetX ({targetX})");
        // Assert: Tooltip must NOT clip on left or right
        Assert.True(x >= 6.0, $"Tooltip X ({x}) clipped on left");
        Assert.True(x + tooltipWidth <= controlWidth - 6.0, $"Tooltip right ({x + tooltipWidth}) clipped on right");
        // Assert: Tooltip must NOT clip on top or bottom
        Assert.True(y >= 6.0, $"Tooltip Y ({y}) clipped on top");
        Assert.True(y + tooltipHeight <= controlHeight - 6.0, $"Tooltip bottom ({y + tooltipHeight}) clipped on bottom");
    }

    [Fact]
    public void CalculateOptimalPosition_WhenPointAtFarRight_AutoFlipsToLeft_AndDoesNotClip()
    {
        // Scenario: Short bar with point on far right (X = 380px)
        double targetX = 380.0;
        double targetY = 150.0;
        double tooltipWidth = 160.0;
        double tooltipHeight = 40.0;
        double controlWidth = 400.0;
        double controlHeight = 300.0;

        var (x, y) = Sources.Helpers.AutoFlipChartTooltip.CalculateOptimalPosition(
            targetX, targetY, tooltipWidth, tooltipHeight, controlWidth, controlHeight);

        // Assert: Tooltip must be placed to the LEFT of targetX (since spaceRight = 20 < tooltipWidth)
        Assert.True(x + tooltipWidth <= targetX, $"Expected tooltip to be to the left of targetX");
        Assert.True(x >= 6.0);
        Assert.True(x + tooltipWidth <= controlWidth - 6.0);
    }

    [Fact]
    public void CalculateOptimalPosition_WhenPointAtTopEdge_ClampsSafelyWithoutTopClipping()
    {
        // Scenario: Top-most bar near Y = 10px
        double targetX = 200.0;
        double targetY = 10.0;
        double tooltipWidth = 150.0;
        double tooltipHeight = 40.0;
        double controlWidth = 400.0;
        double controlHeight = 300.0;

        var (x, y) = Sources.Helpers.AutoFlipChartTooltip.CalculateOptimalPosition(
            targetX, targetY, tooltipWidth, tooltipHeight, controlWidth, controlHeight);

        // Assert: Y must stay within [6, controlHeight - tooltipHeight - 6]
        Assert.True(y >= 6.0, $"Tooltip Y ({y}) clipped on top");
        Assert.True(y + tooltipHeight <= controlHeight - 6.0);
    }

    [Fact]
    public void CalculateOptimalPosition_WhenPointAtBottomEdge_ClampsSafelyWithoutBottomClipping()
    {
        // Scenario: Bottom-most bar near Y = 290px
        double targetX = 200.0;
        double targetY = 290.0;
        double tooltipWidth = 150.0;
        double tooltipHeight = 40.0;
        double controlWidth = 400.0;
        double controlHeight = 300.0;

        var (x, y) = Sources.Helpers.AutoFlipChartTooltip.CalculateOptimalPosition(
            targetX, targetY, tooltipWidth, tooltipHeight, controlWidth, controlHeight);

        // Assert: Y must stay within [6, controlHeight - tooltipHeight - 6]
        Assert.True(y >= 6.0);
        Assert.True(y + tooltipHeight <= controlHeight - 6.0, $"Tooltip Y ({y + tooltipHeight}) clipped on bottom");
    }

    #endregion
}




