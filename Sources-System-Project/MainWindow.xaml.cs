using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Sources.ViewModels;
using Sources.Helpers;
using Sources.Views;
using Sources.Services;
using Sources.Interfaces;
using Sources.Messages;

namespace Sources;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _isLockingScreen = false;
    private DispatcherTimer? _welcomeTimer;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = App.ServiceProvider.GetRequiredService<MainViewModel>();
        DataContext = _viewModel;
        
        // تطبيق اتجاه الواجهة بناءً على اللغة المحفوظة عند التشغيل
        this.FlowDirection = SettingsHelper.Language == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        // Inactivity timer event handlers (1-minute test / 15-minute screensaver)
        _viewModel.LockRequested += ViewModel_LockRequested;
        this.PreviewMouseMove += (s, e) => _viewModel.ResetInactivity();
        this.PreviewKeyDown += MainWindow_PreviewKeyDown;
        this.PreviewMouseDown += (s, e) => _viewModel.ResetInactivity();

        _viewModel.StartInactivityTimer();
        this.Loaded += MainWindow_Loaded;
    }

    private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        _viewModel.ResetInactivity();

        // اختصار Ctrl+K لتركيز شريط البحث الموحّد في لوحة التحكم (مع التنقل إليها إن لزم)
        if (e.Key == System.Windows.Input.Key.K && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
        {
            if (_viewModel.CurrentViewName != "Dashboard")
            {
                _viewModel.NavigateTo("Dashboard");
            }

            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
            {
                WeakReferenceMessenger.Default.Send(new FocusDashboardSearchMessage());
            }));

            e.Handled = true;
        }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var userName = _viewModel.CurrentUserName;
        if (string.IsNullOrWhiteSpace(userName)) return;

        var greeting = TranslationHelper.GetString("WelcomeGreeting") ?? "مرحباً";
        TxtWelcomeOverlay.Text = $"{greeting}، {userName}";
        WelcomeOverlay.Visibility = Visibility.Visible;
        WelcomeOverlay.Opacity = 1;

        _welcomeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _welcomeTimer.Tick += (s, args) =>
        {
            _welcomeTimer.Stop();
            var fadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(500)
            };
            fadeOut.Completed += (_, __) =>
            {
                WelcomeOverlay.Visibility = Visibility.Collapsed;
            };
            WelcomeOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        };
        _welcomeTimer.Start();
    }

    private void ViewModel_LockRequested(object? sender, EventArgs e)
    {
        _viewModel.StopInactivityTimer();
        _isLockingScreen = true;

        // إغلاق الجلسة الحالية أمنياً عند التوقف
        var userService = App.ServiceProvider.GetRequiredService<IUserService>();
        userService.Logout();

        var screensaver = App.ServiceProvider.GetRequiredService<ScreensaverWindow>();
        screensaver.Dismissed += (s, args) =>
        {
            screensaver.Close();
            
            // فتح واجهة الدخول مجدداً عند الخروج من شاشة التوقف
            var loginWindow = App.ServiceProvider.GetRequiredService<LoginWindow>();
            Application.Current.MainWindow = loginWindow;
            loginWindow.Show();
            this.Close();
        };

        screensaver.Show();
        this.Hide();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);

        if (_isLockingScreen)
        {
            return;
        }

        var viewModel = DataContext as MainViewModel ?? _viewModel;
        if (viewModel != null && viewModel.IsLoggedIn)
        {
            // التحقق من وجود تعديلات غير محفوظة في الشاشة النشطة حالياً
            if (viewModel.CurrentView is IEditableViewModel editable && editable.IsEditing)
            {
                DialogHelper.ShowWarning(
                    TranslationHelper.GetString("MsgErrSavePending") ?? "يرجى حفظ التغييرات أو إلغاؤها قبل الانتقال إلى قسم آخر.",
                    TranslationHelper.GetString("TitlePendingChanges") ?? "تنبيه: نافذة مفتوحة"
                );
                e.Cancel = true;
                return;
            }

            bool confirmed = DialogHelper.ShowConfirmation(
                TranslationHelper.GetString("MsgConfirmExitPrompt") ?? "هل تريد الخروج من المنظومة؟",
                TranslationHelper.GetString("MsgConfirmExitTitle") ?? "تأكيد الخروج"
            );

            if (!confirmed)
            {
                e.Cancel = true;
                return;
            }

            // إلغاء الإغلاق الافتراضي المباشر واستدعاء ForceLogout لإغلاق MainWindow وفتح LoginWindow بسلاسة
            e.Cancel = true;
            viewModel.ForceLogout();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.LockRequested -= ViewModel_LockRequested;
        _viewModel.StopInactivityTimer();
        base.OnClosed(e);
    }
}
