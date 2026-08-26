using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Sources.Models;
using Sources.Services;
using Xunit;

namespace Sources.Tests;

public class IsotopeLibraryServiceTests
{
    private readonly IIsotopeLibraryService _service;

    public IsotopeLibraryServiceTests()
    {
        _service = new IsotopeLibraryService();
    }

    [Fact]
    public async Task GetAllEntriesAsync_ContainsBothOrnlAndIcrpDistinctNuclides()
    {
        var entries = await _service.GetAllEntriesAsync();

        Assert.NotNull(entries);
        Assert.True(entries.Count >= 1200, $"Total entries should be >= 1200, got {entries.Count}");

        int ornlCount = entries.Count(e => e.IsOrnlSource);
        int icrpCount = entries.Count(e => e.IsIcrpSource);

        Assert.Equal(320, ornlCount);
        Assert.True(icrpCount > 800, $"ICRP distinct fallback entries should be > 800, got {icrpCount}");
    }

    [Fact]
    public async Task GetAllEntriesAsync_IsSortedAlphabeticallyByElementAndMass()
    {
        var entries = await _service.GetAllEntriesAsync();
        Assert.NotNull(entries);
        Assert.NotEmpty(entries);

        // Verification of relative alphabetical positions:
        // Co-57 must appear before Co-60
        // Co-60 must appear before Cs-131
        // Be-7 must appear before C-11
        var symbols = entries.Select(e => e.DisplaySymbol).ToList();

        int idxBe7 = symbols.IndexOf("Be-7");
        int idxC11 = symbols.IndexOf("C-11");
        int idxCo57 = symbols.IndexOf("Co-57");
        int idxCo60 = symbols.IndexOf("Co-60");
        int idxCs131 = symbols.IndexOf("Cs-131");
        int idxCs137 = symbols.IndexOf("Cs-137");

        Assert.True(idxBe7 >= 0 && idxC11 >= 0 && idxBe7 < idxC11, "Be-7 should precede C-11");
        Assert.True(idxCo57 >= 0 && idxCo60 >= 0 && idxCo57 < idxCo60, "Co-57 should precede Co-60");
        Assert.True(idxCo60 >= 0 && idxCs131 >= 0 && idxCo60 < idxCs131, "Co-60 should precede Cs-131");
        Assert.True(idxCs131 >= 0 && idxCs137 >= 0 && idxCs131 < idxCs137, "Cs-131 should precede Cs-137");
    }

    [Theory]
    [InlineData("60Co", "5.3y")]
    [InlineData("137Cs", "30.17y")]
    [InlineData("24Na", "15.0h")]
    [InlineData("192Ir", "74.0d")]
    [InlineData("131I", "8.0d")]
    [InlineData("99mTc", "6.0h")]
    [InlineData("131Cs", "7d")]
    public async Task SearchAsync_OrnlNuclides_ReturnsCertifiedGammaConstants(string query, string expectedHalfLife)
    {
        var results = await _service.SearchAsync(query);

        Assert.NotEmpty(results);
        var match = results.FirstOrDefault(r => r.NuclideSymbol.Equals(query, StringComparison.OrdinalIgnoreCase) || r.DisplaySymbol.Equals(query, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(match);
        Assert.True(match.IsOrnlSource);
        Assert.Equal(expectedHalfLife, match.HalfLife);
        Assert.NotNull(match.SpecificGammaConstantValue);
        Assert.True(match.SpecificGammaConstantValue > 0);
    }

    [Theory]
    [InlineData("H-3", "B-")]
    [InlineData("C-14", "B-")]
    [InlineData("Sr-90", "B-")]
    [InlineData("P-32", "B-")]
    public async Task SearchAsync_IcrpOnlyNuclides_ReturnsDecayDataWithoutGammaConstant(string query, string expectedDecayMode)
    {
        var results = await _service.SearchAsync(query);

        Assert.NotEmpty(results);
        var match = results.FirstOrDefault(r => r.DisplaySymbol.Equals(query, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(match);
        Assert.True(match.IsIcrpSource);
        Assert.False(match.IsOrnlSource);
        Assert.Null(match.SpecificGammaConstantValue);
        Assert.Equal(expectedDecayMode, match.DecayMode);
        Assert.NotNull(match.TotalEnergyMeV);
    }

    [Fact]
    public async Task SearchAsync_WithHyphenAndCaseInsensitive_FindsCo60()
    {
        // Testing user input variations: "co-60", "Co 60", "CO-60", "كوبالت 60"
        var resultsHyphen = await _service.SearchAsync("co-60");
        Assert.Contains(resultsHyphen, r => r.NuclideSymbol == "60Co" || r.DisplaySymbol == "Co-60");

        var resultsSpace = await _service.SearchAsync("Co 60");
        Assert.Contains(resultsSpace, r => r.NuclideSymbol == "60Co" || r.DisplaySymbol == "Co-60");

        var resultsArabic = await _service.SearchAsync("كوبالت 60");
        Assert.Contains(resultsArabic, r => r.NuclideSymbol == "60Co" || r.DisplaySymbol == "Co-60");

        var resultsCsArabic = await _service.SearchAsync("سيزيوم 137");
        Assert.Contains(resultsCsArabic, r => r.NuclideSymbol == "137Cs" || r.DisplaySymbol == "Cs-137");
    }

    [Fact]
    public async Task SearchAsync_NonExistentNuclide_ReturnsEmpty()
    {
        var results = await _service.SearchAsync("Unobtainium-9999");
        Assert.Empty(results);
    }

    [Fact]
    public async Task ConversionFactor_GammaMremPerUci_StrictlyMultipliesBy3Point7()
    {
        var results = await _service.SearchAsync("60Co");
        var co60 = results.First(r => r.NuclideSymbol == "60Co" || r.DisplaySymbol == "Co-60");

        Assert.NotNull(co60.SpecificGammaConstantValue);
        Assert.NotNull(co60.GammaMremPerUci);

        double expected = co60.SpecificGammaConstantValue.Value * 3.7;
        Assert.Equal(expected, co60.GammaMremPerUci.Value, precision: 8);
    }

    [Fact]
    public void ReferencePdf_PathsResolveAndFilesExist()
    {
        var ornlPath = _service.GetReferencePdfPath();
        Assert.NotNull(ornlPath);
        Assert.True(File.Exists(ornlPath), $"ORNL Reference PDF should exist at path: {ornlPath}");

        var icrpPath = _service.GetIcrpPdfPath();
        Assert.NotNull(icrpPath);
        Assert.True(File.Exists(icrpPath), $"ICRP 107 Reference PDF should exist at path: {icrpPath}");
    }

    [Fact]
    public async Task GetFormattedDetailsText_GeneratesAccurateFormattedStrings_ForBothOrnlAndIcrp()
    {
        // 1. Test ORNL Isotope (Cs-131)
        var ornlResults = await _service.SearchAsync("Cs-131");
        var cs131 = ornlResults.First(x => x.DisplaySymbol == "Cs-131");
        var ornlText = cs131.GetFormattedDetailsText();

        Assert.Contains("رمز النظير: Cs-131", ornlText);
        Assert.Contains("نصف العمر: 7 d", ornlText);
        Assert.Contains("ثابت غاما (الوحدة الدولية SI):", ornlText);
        Assert.Contains("ثابت غاما (الوحدة المرجعية):", ornlText);
        Assert.Contains("سُمك درع الرصاص لتوهين 95%", ornlText);
        Assert.Contains("معامل التوهين الخطي المتوسط", ornlText);
        Assert.Contains("عدد خطوط الانبعاث الفوتونية:", ornlText);
        Assert.Contains("ORNL/RSIC-45/R1", ornlText);

        // 2. Test ICRP Isotope (H-3)
        var icrpResults = await _service.SearchAsync("H-3");
        var h3 = icrpResults.First(x => x.DisplaySymbol == "H-3");
        var icrpText = h3.GetFormattedDetailsText();

        Assert.Contains("رمز النظير: H-3", icrpText);
        Assert.Contains("نصف العمر: 12.32 y", icrpText);
        Assert.Contains("نمط الانحلال الإشعاعي: B-", icrpText);
        Assert.Contains("طاقة الإلكترونات (Electron):", icrpText);
        Assert.Contains("طاقة الفوتونات (Photon):", icrpText);
        Assert.Contains("إجمالي الطاقة المنبعثة:", icrpText);
        Assert.Contains("ICRP Publication 107", icrpText);
        Assert.Contains("لا تُستخدم لحساب معدل الجرعة", icrpText);
    }

    [Theory]
    [InlineData("1.28+9y", "1.28 × 10⁹ y")]
    [InlineData("1.251E+9 y", "1.251 × 10⁹ y")]
    [InlineData("4.2-6s", "4.2 × 10⁻⁶ s")]
    [InlineData("1.6-4s", "1.6 × 10⁻⁴ s")]
    [InlineData("1.8-3s", "1.8 × 10⁻³ s")]
    [InlineData("7.7+4y", "7.7 × 10⁴ y")]
    [InlineData("1.4+10y", "1.4 × 10¹⁰ y")]
    [InlineData("7.04+8y", "7.04 × 10⁸ y")]
    [InlineData("2.34+7y", "2.34 × 10⁷ y")]
    [InlineData("4.47+9y", "4.47 × 10⁹ y")]
    [InlineData("5.3y", "5.3 y")]
    [InlineData("30.17y", "30.17 y")]
    [InlineData("6.0h", "6.0 h")]
    [InlineData("74.0d", "74.0 d")]
    public void FormatHalfLife_CorrectlyFormatsExponentialAndStandardScientificNotation(string raw, string expected)
    {
        var formatted = IsotopeReferenceEntry.FormatHalfLife(raw);
        Assert.Equal(expected, formatted);
    }

    [Fact]
    public void CopyValueCommand_ExecutesWithoutError_ForValidValue()
    {
        var vm = new Sources.ViewModels.IsotopeLibraryViewModel(_service);
        var exception = Record.Exception(() =>
        {
            vm.CopyValueCommand.Execute("2.1970E-05");
            vm.CopyValueCommand.Execute("4.843");
            vm.CopyValueCommand.Execute("1.28 × 10⁹ y");
            vm.CopyValueCommand.Execute("—");
            vm.CopyValueCommand.Execute(null);
        });

        Assert.Null(exception);
    }
}
