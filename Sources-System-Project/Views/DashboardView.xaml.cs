using System.Windows.Controls;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Kernel;
using Sources.ViewModels;
using Sources.Helpers;

namespace Sources.Views;
public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        IsotopeChart.Tooltip = new AutoFlipChartTooltip();
        LocationChart.Tooltip = new AutoFlipChartTooltip();
        HistogramChart.Tooltip = new AutoFlipChartTooltip();
        DecayChart.Tooltip = new AutoFlipChartTooltip();
    }

    /// <summary>
    /// معالج حدث الضغط على عمود في الـ Histogram لفتح Side Panel مع تفاصيل النطاق (البند 1 + 4)
    /// </summary>
    private void HistogramChart_DataPointerDown(IChartView chart,
        IEnumerable<ChartPoint> points)
    {
        var point = points.FirstOrDefault();
        if (point == null) return;

        int binIndex = point.Index;
        if (DataContext is DashboardViewModel vm)
        {
            vm.OpenHistogramDrillDownCommand.Execute(binIndex);
        }
    }
}
