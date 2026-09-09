using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Sources.ViewModels;

namespace Sources.Views;

public partial class BorrowView : UserControl
{
    private BorrowFormWindow? _formWindow;

    public BorrowView()
    {
        InitializeComponent();
        Loaded += BorrowView_Loaded;
        Unloaded += BorrowView_Unloaded;
    }

    private void BorrowView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged += DataContext_PropertyChanged;
        }
    }

    private void BorrowView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged -= DataContext_PropertyChanged;
        }
    }

    private void DataContext_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BorrowViewModel.IsEditing)) return;
        if (DataContext is not BorrowViewModel vm) return;

        if (vm.IsEditing)
        {
            if (_formWindow != null) return; // منع فتح نافذة ثانية عند إعادة الدخول

            _formWindow = new BorrowFormWindow
            {
                DataContext = DataContext,
                Owner = Window.GetWindow(this)
            };
            _formWindow.Closed += FormWindow_Closed;
            _formWindow.ShowDialog();
        }
        else
        {
            _formWindow?.Close();
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
