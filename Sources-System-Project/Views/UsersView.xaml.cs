using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Sources.Helpers;
using Sources.ViewModels;

namespace Sources.Views;

public partial class UsersView : UserControl
{
    private UserFormWindow? _formWindow;

    public UsersView()
    {
        InitializeComponent();
        Loaded += UsersView_Loaded;
        Unloaded += UsersView_Unloaded;
    }

    private void UsersView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is INotifyPropertyChanged notifier)
        {
            // منع الاشتراك المزدوج إن أُطلق Loaded أكثر من مرة (الجولة 199 — B1(b)).
            notifier.PropertyChanged -= DataContext_PropertyChanged;
            notifier.PropertyChanged += DataContext_PropertyChanged;
        }

        // تأجيل لا إعادة ضبط (الجولة 199 — B1(b)): حالة حقيقية موجودة فعلياً
        // (DashboardViewModel.QuickAddSource) تُنشئ IsEditing=true قبل اكتمال تحميل الشاشة
        // المستهدفة واشتراكها في PropertyChanged. إعادة ضبط IsEditing هنا كانت ستكسر الإضافة
        // السريعة، فتُفتح النافذة بدلاً من ذلك بعد اكتمال التحميل عبر BeginInvoke.
        if (DataContext is UsersViewModel vm && vm.IsEditing && _formWindow == null)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (DataContext is UsersViewModel currentVm && currentVm.IsEditing && _formWindow == null)
                {
                    OpenForm(currentVm);
                }
            });
        }
    }

    private void UsersView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged -= DataContext_PropertyChanged;
        }
    }

    private void DataContext_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(UsersViewModel.IsEditing)) return;
        if (DataContext is not UsersViewModel vm) return;

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
    private void OpenForm(UsersViewModel vm)
    {
        if (_formWindow != null) return; // منع فتح نافذة ثانية عند إعادة الدخول

        try
        {
            _formWindow = new UserFormWindow
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
            EditingFormTracker.HandleOpenFailure(nameof(UsersView), ex, vm.CancelEditCommand);
        }
    }

    private void FormWindow_Closed(object? sender, EventArgs e)
    {
        if (sender is Window closedWindow && closedWindow.DataContext is UsersViewModel vm)
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
