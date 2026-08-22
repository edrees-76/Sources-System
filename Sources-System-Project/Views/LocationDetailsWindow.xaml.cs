using System.Windows;
using Sources.ViewModels;

namespace Sources.Views;

public partial class LocationDetailsWindow : Window
{
    public LocationDetailsWindow()
    {
        InitializeComponent();
    }

    public LocationDetailsWindow(LocationDetailsViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
