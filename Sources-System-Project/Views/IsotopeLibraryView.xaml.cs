using System.Windows.Controls;
using Sources.ViewModels;

namespace Sources.Views;

public partial class IsotopeLibraryView : UserControl
{
    public IsotopeLibraryView()
    {
        InitializeComponent();
    }

    public IsotopeLibraryView(IsotopeLibraryViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnListBoxDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is IsotopeLibraryViewModel vm && vm.SelectedEntry != null)
        {
            vm.OpenDetailsDialogCommand.Execute(vm.SelectedEntry);
        }
    }
}
