using System.Windows;
using Sources.ViewModels;

namespace Sources.Views;

/// <summary>
/// الجولة 212: نافذة كاملة تعرض المصادر خلف صف مختار في رسوم لوحة التحكم.
/// </summary>
public partial class DashboardSourcesWindow : Window
{
    public DashboardSourcesWindow()
    {
        InitializeComponent();
    }

    public DashboardSourcesWindow(DashboardDrillDown drillDown) : this()
    {
        DataContext = drillDown;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
