using System.Windows;
using Sources.ViewModels;

namespace Sources.Views;

public partial class SourceDetailsWindow : Window
{
    public SourceDetailsWindow()
    {
        InitializeComponent();
    }

    public SourceDetailsWindow(SourceDetailsViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
