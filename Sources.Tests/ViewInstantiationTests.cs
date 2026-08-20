using System;
using System.Threading;
using System.Windows;
using Sources.Views;
using Xunit;

using System.Windows.Threading;

namespace Sources.Tests;

/// <summary>
/// اختبارات التحقق من بناء وسلامة ملفات XAML وتكوين الواجهات (WPF View Instantiation Tests)
/// تضمن هذه الاختبارات تحميل كافة عناصر XAML والربط والقواميس في الـ Visual Tree دون أي XamlParseException
/// </summary>
public class ViewInstantiationTests
{
    private static void RunInSta(Action action) => Sources.Tests.Fixtures.WpfStaFixture.RunInSta(action);

    [Fact]
    public void DashboardView_InstantiatesSuccessfully_WithCustomTooltips_AndNoXamlErrors()
    {
        RunInSta(() =>
        {
            var view = new DashboardView();
            Assert.NotNull(view);
            var isotopeChart = view.FindName("IsotopeChart") as LiveChartsCore.SkiaSharpView.WPF.CartesianChart;
            var locationChart = view.FindName("LocationChart") as LiveChartsCore.SkiaSharpView.WPF.CartesianChart;
            var histogramChart = view.FindName("HistogramChart") as LiveChartsCore.SkiaSharpView.WPF.CartesianChart;
            var decayChart = view.FindName("DecayChart") as LiveChartsCore.SkiaSharpView.WPF.CartesianChart;

            Assert.NotNull(isotopeChart);
            Assert.NotNull(locationChart);
            Assert.NotNull(histogramChart);
            Assert.NotNull(decayChart);

            Assert.NotNull(isotopeChart.Tooltip);
            Assert.NotNull(locationChart.Tooltip);
            Assert.NotNull(histogramChart.Tooltip);
            Assert.NotNull(decayChart.Tooltip);
        });
    }

    [Fact]
    public void LocationsView_InstantiatesSuccessfully()
    {
        RunInSta(() =>
        {
            var view = new LocationsView();
            Assert.NotNull(view);
        });
    }

    [Fact]
    public void BorrowView_InstantiatesSuccessfully()
    {
        RunInSta(() =>
        {
            var view = new BorrowView();
            Assert.NotNull(view);
        });
    }

    [Fact]
    public void ReportsView_InstantiatesSuccessfully()
    {
        RunInSta(() =>
        {
            var view = new ReportsView();
            Assert.NotNull(view);
        });
    }

    [Fact]
    public void SourcesView_InstantiatesSuccessfully()
    {
        RunInSta(() =>
        {
            var view = new SourcesView();
            Assert.NotNull(view);
        });
    }

    [Fact]
    public void RadioisotopesView_InstantiatesSuccessfully()
    {
        RunInSta(() =>
        {
            var view = new RadioisotopesView();
            Assert.NotNull(view);
        });
    }

    [Fact]
    public void UsersView_InstantiatesSuccessfully()
    {
        RunInSta(() =>
        {
            var view = new UsersView();
            Assert.NotNull(view);
        });
    }

    [Fact]
    public void SettingsView_InstantiatesSuccessfully()
    {
        RunInSta(() =>
        {
            var view = new SettingsView();
            Assert.NotNull(view);
        });
    }

    [Fact]
    public void ActivityCalculatorView_InstantiatesSuccessfully()
    {
        RunInSta(() =>
        {
            var view = new ActivityCalculatorView();
            Assert.NotNull(view);
        });
    }

    [Fact]
    public void HelpView_InstantiatesSuccessfully()
    {
        RunInSta(() =>
        {
            var view = new HelpView();
            Assert.NotNull(view);
        });
    }

    [Fact]
    public void AboutSystemView_InstantiatesSuccessfully()
    {
        RunInSta(() =>
        {
            var view = new AboutSystemView();
            Assert.NotNull(view);
        });
    }
}
