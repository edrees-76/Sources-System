using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Sources.Interfaces;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// الجولة 212: رسوم لوحة التحكم الشريطية الأصلية (WPF) بدل LiveCharts/Skia —
/// سُلّم النشاط، والنظائر، والمواقع. تتحقق من المحور الصفري، وفصل «أخرى» كسطر نصي،
/// والنسب بأرقام لاتينية، والتمييز العددي العربي.
/// </summary>
public class Round212DashboardBarChartsTests
{
    // ─── BuildBarRows ───

    [Fact]
    public void BuildBarRows_BarLengthIsRelativeToMaxFromZero()
    {
        var rows = DashboardViewModel.BuildBarRows(
            new List<(string, int)> { ("Cs-137", 17), ("Co-60", 12), ("Na-22", 1) }, total: 30);

        Assert.Equal(1.0, rows[0].Fraction, 6);
        Assert.Equal(12.0 / 17.0, rows[1].Fraction, 6);   // لا خط أساس مقطوع: 12 ≈ 70% من 17
        Assert.Equal(1.0 / 17.0, rows[2].Fraction, 6);
        Assert.Equal(new[] { 0, 1, 2 }, rows.Select(r => r.Index));
    }

    [Fact]
    public void BuildBarRows_PercentUsesInvariantCultureAndTotal()
    {
        var rows = DashboardViewModel.BuildBarRows(
            new List<(string, int)> { ("A", 38), ("B", 1) }, total: 139);

        Assert.Equal("27.3%", rows[0].PercentText);
        Assert.Equal("0.7%", rows[1].PercentText);
        Assert.Equal("38", rows[0].CountText);
        Assert.All(rows, r => Assert.DoesNotMatch("[٠-٩٫]", r.PercentText));
    }

    [Fact]
    public void BuildBarRows_ZeroCount_IsZeroWithEmptyBar()
    {
        var rows = DashboardViewModel.BuildBarRows(
            new List<(string, int)> { ("PBq", 0), ("TBq", 24) }, total: 24);

        Assert.True(rows[0].IsZero);
        Assert.Equal(0.0, rows[0].Fraction);
        Assert.Equal("0.0%", rows[0].PercentText);
        Assert.False(rows[1].IsZero);
    }

    [Fact]
    public void BuildBarRows_AllZeroOrEmpty_DoesNotDivideByZero()
    {
        var allZero = DashboardViewModel.BuildBarRows(new List<(string, int)> { ("A", 0), ("B", 0) }, total: 0);
        Assert.All(allZero, r => Assert.Equal(0.0, r.Fraction));
        Assert.All(allZero, r => Assert.Equal("0.0%", r.PercentText));

        Assert.Empty(DashboardViewModel.BuildBarRows(new List<(string, int)>(), total: 0));
    }

    [Fact]
    public void BuildBarRows_ToolTipKeepsRawUnreversedText()
    {
        var rows = DashboardViewModel.BuildBarRows(
            new List<(string, int)> { ("المختبر الرئيسي", 3) }, total: 10);

        // لا إعادة تشكيل ولا قلب يدوي: النص الأصلي كما هو لتعالجه WPF
        Assert.StartsWith("المختبر الرئيسي\n", rows[0].ToolTipText);
        Assert.Contains("30.0%", rows[0].ToolTipText);
    }

    [Fact]
    public void DashboardBarRow_GridWidthsAreComplementaryStars()
    {
        var row = new DashboardBarRow { Fraction = 0.25 };
        Assert.True(row.BarWidth.IsStar);
        Assert.True(row.RestWidth.IsStar);
        Assert.Equal(0.25, row.BarWidth.Value, 6);
        Assert.Equal(0.75, row.RestWidth.Value, 6);
    }

    // ─── SplitTopNAndOthers ───

    [Fact]
    public void SplitTopNAndOthers_SeparatesOthersInsteadOfAddingABar()
    {
        var items = Enumerable.Range(1, 15).Select(i => ($"L{i}", i)).ToList();

        var (top, othersCount, othersGroups) = DashboardViewModel.SplitTopNAndOthers(items, 10);

        Assert.Equal(10, top.Count);
        Assert.Equal(15, top[0].Item2);
        Assert.Equal(6, top[9].Item2);
        Assert.Equal(5, othersGroups);
        Assert.Equal(1 + 2 + 3 + 4 + 5, othersCount);
    }

    [Fact]
    public void SplitTopNAndOthers_NoRemainder_ZeroOthers()
    {
        var (top, othersCount, othersGroups) = DashboardViewModel.SplitTopNAndOthers(
            new List<(string, int)> { ("A", 2), ("B", 1) }, 10);

        Assert.Equal(2, top.Count);
        Assert.Equal(0, othersCount);
        Assert.Equal(0, othersGroups);
    }

    [Fact]
    public void ComputeTopNPlusOthers_StillAppendsOthersForCompatibility()
    {
        var items = Enumerable.Range(1, 12).Select(i => ($"L{i}", i)).ToList();
        var result = DashboardViewModel.ComputeTopNPlusOthers(items, 10, "أخرى");

        Assert.Equal(11, result.Count);
        Assert.Equal(("أخرى", 3), result.Last());
    }

    // ─── التمييز العددي العربي ───

    [Theory]
    [InlineData(0, DashboardViewModel.ArabicCountForm.Zero)]
    [InlineData(1, DashboardViewModel.ArabicCountForm.One)]
    [InlineData(2, DashboardViewModel.ArabicCountForm.Two)]
    [InlineData(3, DashboardViewModel.ArabicCountForm.Few)]
    [InlineData(10, DashboardViewModel.ArabicCountForm.Few)]
    [InlineData(11, DashboardViewModel.ArabicCountForm.Many)]
    [InlineData(99, DashboardViewModel.ArabicCountForm.Many)]
    [InlineData(100, DashboardViewModel.ArabicCountForm.Other)]
    [InlineData(102, DashboardViewModel.ArabicCountForm.Other)]
    [InlineData(103, DashboardViewModel.ArabicCountForm.Few)]
    [InlineData(111, DashboardViewModel.ArabicCountForm.Many)]
    public void GetArabicCountForm_FollowsArabicNumberAgreement(int n, DashboardViewModel.ArabicCountForm expected)
    {
        Assert.Equal(expected, DashboardViewModel.GetArabicCountForm(n));
    }

    [Theory]
    [InlineData(1, "مصدر واحد")]
    [InlineData(2, "مصدران")]
    [InlineData(7, "7 مصادر")]
    [InlineData(42, "42 مصدراً")]
    [InlineData(100, "100 مصدر")]
    public void FormatSourceCount_ArabicFallbackForms(int n, string expected)
    {
        Assert.Equal(expected, DashboardViewModel.FormatSourceCount(n));
    }

    [Fact]
    public void FormatOthersSummary_TextLineWithGroupsCountAndPercent()
    {
        Assert.Equal("أخرى (5): 7 مصادر · 5.0%", DashboardViewModel.FormatOthersSummary(7, 5, 140));
        Assert.Equal(string.Empty, DashboardViewModel.FormatOthersSummary(0, 0, 140));
    }

    // ─── تسميات سُلّم النشاط ───

    [Fact]
    public void HistogramBinDisplayLabels_UseReadableUnitsForEveryBin()
    {
        var labels = Enumerable.Range(0, DashboardViewModel.HistogramBins.Length)
            .Select(DashboardViewModel.GetHistogramBinDisplayLabel)
            .ToArray();

        Assert.Equal(new[] { "أقل من 1 kBq", "kBq", "MBq", "GBq", "TBq", "1 PBq فأكثر" }, labels);
        Assert.All(labels, l => Assert.DoesNotContain("¹", l));
    }

    // ─── تكامل: LoadDataAsync يملأ الصفوف ───

    [Fact]
    public async Task LoadDataAsync_FillsLadderIsotopeAndLocationRows()
    {
        var bq = new ActivityUnit { UnitSymbol = "Bq", ConversionToBq = 1 };
        var cs = new Radioisotope { Symbol = "Cs-137" };
        var co = new Radioisotope { Symbol = "Co-60" };
        var store = new Location { Id = Guid.NewGuid(), LocationName = "المخزن الرئيسي" };
        var lab = new Location { Id = Guid.NewGuid(), LocationName = "المختبر" };

        var sources = new List<Source>
        {
            new() { SourceCode = "S1", Radioisotope = cs, Location = store, CurrentActivityValue = 5e10, CurrentActivityUnit = bq },
            new() { SourceCode = "S2", Radioisotope = cs, Location = store, CurrentActivityValue = 2e10, CurrentActivityUnit = bq },
            new() { SourceCode = "S3", Radioisotope = co, Location = lab,   CurrentActivityValue = 3e7,  CurrentActivityUnit = bq },
        };

        var mockSourceService = new Mock<ISourceService>();
        mockSourceService.Setup(s => s.GetAllSources()).Returns(sources);
        var mockBorrowService = new Mock<IBorrowService>();
        mockBorrowService.Setup(s => s.GetAll()).Returns(new List<BorrowRequest>());

        DashboardViewModel? vm = null;
        try
        {
            vm = new DashboardViewModel(
                mockSourceService.Object,
                new Mock<IRadioisotopeService>().Object,
                new Mock<ILocationService>().Object,
                new Mock<IDecayCalculationService>().Object,
                mockBorrowService.Object,
                new Mock<ISystemSettingsService>().Object);

            await vm.LoadDataAsync();

            // سُلّم النشاط: 6 صفوف من الأعلى (PBq) إلى الأدنى، والفارغة ظاهرة
            Assert.Equal(6, vm.ActivityLadderRows.Count);
            Assert.Equal(new[] { 5, 4, 3, 2, 1, 0 }, vm.ActivityLadderRows.Select(r => r.Index));
            Assert.Equal(2, vm.ActivityLadderRows.Single(r => r.Label == "GBq").Count);
            Assert.Equal(1, vm.ActivityLadderRows.Single(r => r.Label == "MBq").Count);
            Assert.True(vm.ActivityLadderRows.Single(r => r.Label == "TBq").IsZero);
            Assert.Contains("Bq", vm.ActivityLadderRows[0].SubLabel);

            // النظائر: Cs-137 أولاً، بلا صف «أخرى»
            Assert.True(vm.HasEnoughIsotopeData);
            Assert.Equal(new[] { "Cs-137", "Co-60" }, vm.IsotopeBarRows.Select(r => r.Label));
            Assert.Equal("66.7%", vm.IsotopeBarRows[0].PercentText);
            Assert.Equal(string.Empty, vm.IsotopeOthersText);

            // المواقع
            Assert.True(vm.HasEnoughLocationData);
            Assert.Equal("المخزن الرئيسي", vm.LocationBarRows[0].Label);
            Assert.Equal(1.0, vm.LocationBarRows[0].Fraction, 6);
            Assert.Equal(0.5, vm.LocationBarRows[1].Fraction, 6);

            // النقر على صف يفتح اللوحة الجانبية بمصادره
            vm.OpenLocationRowCommand.Execute(vm.LocationBarRows[0]);
            Assert.True(vm.IsSidePanelOpen);
            Assert.True(vm.SidePanelShowSources);
            Assert.Equal(new[] { "S1", "S2" }, vm.SidePanelSources.Select(r => r.Source!.SourceCode).OrderBy(c => c));

            vm.OpenIsotopeRowCommand.Execute(vm.IsotopeBarRows.Single(r => r.Label == "Co-60"));
            Assert.Equal(new[] { "S3" }, vm.SidePanelSources.Select(r => r.Source!.SourceCode));

            vm.OpenActivityBinRowCommand.Execute(vm.ActivityLadderRows.Single(r => r.Label == "GBq"));
            Assert.Equal(2, vm.SidePanelSources.Count);
            Assert.EndsWith("GBq", vm.SidePanelTitle);
        }
        finally
        {
            vm?.Dispose();
        }
    }
}
