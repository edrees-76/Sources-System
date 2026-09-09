using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Sources.ViewModels;

namespace Sources.Views;

public partial class LocationsView : UserControl
{
    private LocationFormWindow? _formWindow;

    public LocationsView()
    {
        InitializeComponent();
        Loaded += LocationsView_Loaded;
        Unloaded += LocationsView_Unloaded;
    }

    private void LocationsView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged += DataContext_PropertyChanged;
        }
    }

    private void LocationsView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged -= DataContext_PropertyChanged;
        }
    }

    private void DataContext_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LocationsViewModel.IsEditing)) return;
        if (DataContext is not LocationsViewModel vm) return;

        if (vm.IsEditing)
        {
            if (_formWindow != null) return; // منع فتح نافذة ثانية عند إعادة الدخول

            _formWindow = new LocationFormWindow
            {
                DataContext = DataContext,
                Owner = Window.GetWindow(this)
            };
            _formWindow.Closed += FormWindow_Closed;
            _formWindow.ShowDialog();
        }
        else
        {
            // إذا كانت النافذة بصدد الإغلاق فعلياً (مثلاً: CancelEditCommand استُدعي من داخل
            // معالج Closing الخاص بها نتيجة إغلاق عبر ✕ أو Alt+F4)، فتجنّب استدعاء Close()
            // مرة أخرى بشكل متكرر (reentrant)؛ ستتابع النافذة إغلاقها من تلقاء نفسها.
            if (_formWindow != null && !_formWindow.IsClosingInProgress)
            {
                _formWindow.Close();
            }
        }
    }

    private void FormWindow_Closed(object? sender, System.EventArgs e)
    {
        if (_formWindow != null)
        {
            _formWindow.Closed -= FormWindow_Closed;
            _formWindow = null;
        }
    }
}
