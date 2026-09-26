using System.Windows.Controls;
using CommunityToolkit.Mvvm.Messaging;
using Sources.ViewModels;
using Sources.Helpers;
using Sources.Messages;

namespace Sources.Views;
public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        DecayChart.Tooltip = new AutoFlipChartTooltip();

        WeakReferenceMessenger.Default.Register<FocusDashboardSearchMessage>(this, (r, m) =>
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new System.Action(() =>
            {
                TxtDashboardGlobalSearch?.Focus();
                TxtDashboardGlobalSearch?.SelectAll();
            }));
        });

        Unloaded += (_, _) =>
            WeakReferenceMessenger.Default
                .Unregister<FocusDashboardSearchMessage>(this);
    }
}
