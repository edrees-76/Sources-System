using System.Windows;
using Sources.ViewModels;

namespace Sources.Views;

public partial class NeutronSourceTypesWindow : Window
{
    public NeutronSourceTypesWindow()
    {
        InitializeComponent();
    }

    public NeutronSourceTypesWindow(NeutronSourceTypesViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
