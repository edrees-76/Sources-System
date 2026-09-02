using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Sources.Data;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fixtures;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

public class NumericInputValidationTests : IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly IServiceProvider _sp;

    public NumericInputValidationTests()
    {
        _fixture = new SqliteInMemoryFixture();

        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<AppDbContext>>(_fixture.ContextFactory);
        _sp = services.BuildServiceProvider();
        typeof(App).GetProperty("ServiceProvider", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, _sp);
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }

    private SourcesViewModel CreateViewModel()
    {
        var mockSourceService = new Mock<ISourceService>();
        mockSourceService.Setup(s => s.GetAllSources()).Returns(new List<Source>());
        mockSourceService.Setup(s => s.GetDeletedSources()).Returns(new List<Source>());

        var mockIsotopeService = new Mock<IRadioisotopeService>();
        mockIsotopeService.Setup(s => s.GetAll()).Returns(new List<Radioisotope>());

        var mockLocationService = new Mock<ILocationService>();
        mockLocationService.Setup(s => s.GetAll()).Returns(new List<Location>());

        var mockReportingService = new Mock<IReportingService>();
        var mockDecayService = new Mock<IDecayCalculationService>();
        var mockNeutronService = new Mock<INeutronSourceService>();
        var mockNeutronTypeService = new Mock<INeutronSourceTypeService>();

        var vm = new SourcesViewModel(
            mockSourceService.Object,
            mockIsotopeService.Object,
            mockLocationService.Object,
            mockReportingService.Object,
            mockDecayService.Object,
            mockNeutronService.Object,
            mockNeutronTypeService.Object);

        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.UnregisterAll(vm);
        return vm;
    }

    #region 1. اختبارات المحوّل NumericInputParser

    [Theory]
    [InlineData("NaN")]
    [InlineData("nan")]
    [InlineData("NAN")]
    public void TryParseFinite_WhenInputIsNaN_ReturnsFalse(string input)
    {
        bool success = NumericInputParser.TryParseFinite(input, out double result);

        Assert.False(success);
        Assert.Equal(0, result);
    }

    [Theory]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("+Infinity")]
    [InlineData("∞")]
    [InlineData("-∞")]
    public void TryParseFinite_WhenInputIsInfinity_ReturnsFalse(string input)
    {
        bool success = NumericInputParser.TryParseFinite(input, out double result);

        Assert.False(success);
        Assert.Equal(0, result);
    }

    [Theory]
    [InlineData("12.5", 12.5)]
    [InlineData("-3", -3.0)]
    [InlineData("0", 0.0)]
    [InlineData("1.23e4", 12300.0)]
    public void TryParseFinite_WhenInputIsValidNumber_ReturnsTrueWithValue(string input, double expected)
    {
        bool success = NumericInputParser.TryParseFinite(input, out double result);

        Assert.True(success);
        Assert.Equal(expected, result, precision: 4);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("12.34.56")]
    [InlineData(null)]
    public void TryParseFinite_WhenInputIsEmptyOrGarbage_ReturnsFalse(string? input)
    {
        bool success = NumericInputParser.TryParseFinite(input, out double result);

        Assert.False(success);
        Assert.Equal(0, result);
    }

    [Fact]
    public void TryParseFinite_WhenInputHasThousandsSeparator_ParsesCorrectly()
    {
        bool success1 = NumericInputParser.TryParseFinite("1,000", out double result1);
        Assert.True(success1);
        Assert.Equal(1000.0, result1);

        bool success2 = NumericInputParser.TryParseFinite("1,000.5", out double result2);
        Assert.True(success2);
        Assert.Equal(1000.5, result2);
    }

    [Fact]
    public void TryParseFinite_NaNPassesLessThanOrEqualZeroGuard_ButIsRejectedHere()
    {
        // توثيق العيب الأساسي في IEEE 754:
        // double.TryParse مع الثقافة الثابتة يقبل "NaN" وينتج double.NaN
        bool rawSuccess = double.TryParse("NaN", System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double rawParsedNaN);
        Assert.True(rawSuccess, "double.TryParse مع InvariantCulture يقبل NaN");
        Assert.True(double.IsNaN(rawParsedNaN), "القيمة الناتجة هي double.NaN");

        // حارس التحقق التقليدي يعبره NaN لأن المقارنة ترجع false دائماً
        Assert.False(rawParsedNaN <= 0, "NaN يعبر حارس (value <= 0) لأن المقارنة false دائماً");

        // المحوّل الجديد يرفضه تماماً
        bool finiteSuccess = NumericInputParser.TryParseFinite("NaN", out _);
        Assert.False(finiteSuccess, "NumericInputParser.TryParseFinite يجب أن يرفض NaN تماماً");
    }

    #endregion

    #region 2. اختبارات سلوك ViewModel ومعالجات الإدخال

    [Fact]
    public void IsotopeEntry_WhenActivityTextIsNaN_SetsActivityToZero()
    {
        var entry = new IsotopeEntryViewModel();
        entry.InitialActivity = 50.0;

        entry.InitialActivityText = "NaN";

        Assert.Equal(0, entry.InitialActivity);
    }

    [Fact]
    public void EditInitialActivityText_WhenNaN_SetsValueToZero()
    {
        var vm = CreateViewModel();
        vm.EditInitialActivity = 100.0;

        vm.EditInitialActivityText = "NaN";

        Assert.Equal(0, vm.EditInitialActivity);
    }

    [Fact]
    public void EditAnisotropyFactorText_WhenNaN_SetsNull()
    {
        var vm = CreateViewModel();
        vm.EditAnisotropyFactor = 1.05;

        vm.EditAnisotropyFactorText = "NaN";

        Assert.Null(vm.EditAnisotropyFactor);
    }

    [Fact]
    public void EditRelativeUncertaintyText_WhenInfinity_SetsNull()
    {
        var vm = CreateViewModel();
        vm.EditRelativeUncertaintyPercent = 5.0;

        vm.EditRelativeUncertaintyText = "Infinity%";

        Assert.Null(vm.EditRelativeUncertaintyPercent);
    }

    #endregion
}
