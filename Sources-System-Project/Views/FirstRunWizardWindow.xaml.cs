using System.Windows;
using Sources.Helpers;
using Sources.ViewModels;

namespace Sources.Views;

public partial class FirstRunWizardWindow : Window
{
    public FirstRunWizardWindow(FirstRunWizardViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;

        // تطبيق اتجاه الواجهة وفق اللغة المحفوظة. لا يوجد MainWindow بعد في هذه المرحلة من
        // الإقلاع (OnStartup)، لذا لا يجوز الاعتماد على Application.Current.MainWindow.FlowDirection.
        this.FlowDirection = SettingsHelper.Language == "en" ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;

        viewModel.CloseRequested += (s, e) => Close();
    }
}
