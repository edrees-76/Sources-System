using System.Windows;
using Sources.ViewModels;

namespace Sources.Views;

public partial class NeutronSourceDetailsWindow : Window
{
    public NeutronSourceDetailsWindow()
    {
        InitializeComponent();
    }

    public NeutronSourceDetailsWindow(NeutronSourceDetailsViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
