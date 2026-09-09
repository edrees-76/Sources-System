using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Sources.ViewModels;

namespace Sources.Views;

public partial class SourcesView : UserControl
{
    private NeutronSourceTypesWindow? _neutronTypesWindow;

    public SourcesView()
    {
        InitializeComponent();
        Loaded += SourcesView_Loaded;
        Unloaded += SourcesView_Unloaded;
    }

    private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        e.Row.Header = (e.Row.GetIndex() + 1).ToString();
    }

    private void SourcesView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged += DataContext_PropertyChanged;
        }
    }

    private void SourcesView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged -= DataContext_PropertyChanged;
        }
    }

    private void DataContext_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SourcesViewModel.IsManagingNeutronTypes)) return;
        if (DataContext is not SourcesViewModel vm) return;

        if (vm.IsManagingNeutronTypes)
        {
            if (_neutronTypesWindow != null) return; // منع فتح نافذة ثانية عند إعادة الدخول

            _neutronTypesWindow = new NeutronSourceTypesWindow
            {
                DataContext = vm.NeutronTypesManagementViewModel,
                Owner = Window.GetWindow(this)
            };
            _neutronTypesWindow.Closed += NeutronTypesWindow_Closed;
            _neutronTypesWindow.ShowDialog();
        }
        else
        {
            // إذا كانت النافذة بصدد الإغلاق فعلياً (عبر معالج Closing الخاص بها نتيجة إغلاق
            // عبر ✕ أو Alt+F4)، فتجنّب استدعاء Close() مرة أخرى بشكل متكرر (reentrant)؛
            // ستتابع النافذة إغلاقها من تلقاء نفسها.
            if (_neutronTypesWindow != null && !_neutronTypesWindow.IsClosingInProgress)
            {
                _neutronTypesWindow.Close();
            }
        }
    }

    private void NeutronTypesWindow_Closed(object? sender, System.EventArgs e)
    {
        if (_neutronTypesWindow != null)
        {
            _neutronTypesWindow.Closed -= NeutronTypesWindow_Closed;
            _neutronTypesWindow = null;
        }
    }
}
