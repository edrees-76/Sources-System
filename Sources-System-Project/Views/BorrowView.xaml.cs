using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Sources.Helpers;
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
            // منع الاشتراك المزدوج إن أُطلق Loaded أكثر من مرة (الجولة 199 — B1(b)).
            notifier.PropertyChanged -= DataContext_PropertyChanged;
            notifier.PropertyChanged += DataContext_PropertyChanged;
        }

        // تأجيل لا إعادة ضبط (الجولة 199 — B1(b)): نفس نمط DashboardViewModel.QuickAddSource
        // (Sources)؛ إن IsEditing=true قبل اكتمال التحميل والاشتراك، تُفتح النافذة بعده مباشرة
        // عبر BeginInvoke بدل إعادة ضبط IsEditing.
        if (DataContext is BorrowViewModel vm && vm.IsEditing && _formWindow == null)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (DataContext is BorrowViewModel currentVm && currentVm.IsEditing && _formWindow == null)
                {
                    OpenForm(currentVm);
                }
            });
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
            OpenForm(vm);
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

    /// <summary>
    /// يفتح نافذة التحرير ضمن try/catch (الجولة 199 — B1(a)): أي استثناء أثناء الإنشاء/تعيين
    /// Owner/ShowDialog يُلتقط بدل أن يتسرّب من داخل معالج IsEditing تاركاً IsEditing عالقاً
    /// true بلا نافذة فعلية.
    /// </summary>
    private void OpenForm(BorrowViewModel vm)
    {
        if (_formWindow != null) return; // منع فتح نافذة ثانية عند إعادة الدخول

        try
        {
            _formWindow = new BorrowFormWindow
            {
                DataContext = vm,
                Owner = Window.GetWindow(this)
            };
            _formWindow.Closed += FormWindow_Closed;
            EditingFormTracker.MarkOpen(vm);
            _formWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            if (_formWindow != null)
            {
                _formWindow.Closed -= FormWindow_Closed;
                _formWindow = null;
            }
            EditingFormTracker.MarkClosed(vm);
            EditingFormTracker.HandleOpenFailure(nameof(BorrowView), ex, vm.CancelEditCommand);
        }
    }

    private void FormWindow_Closed(object? sender, EventArgs e)
    {
        if (sender is Window closedWindow && closedWindow.DataContext is BorrowViewModel vm)
        {
            EditingFormTracker.MarkClosed(vm);
        }

        if (_formWindow != null)
        {
            _formWindow.Closed -= FormWindow_Closed;
            _formWindow = null;
        }
    }
}
