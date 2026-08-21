using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Sources.ViewModels;
using Sources.Helpers;
using Sources.Views;
using Sources.Services;
using Sources.Interfaces;

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
        this.PreviewKeyDown += (s, e) => _viewModel.ResetInactivity();
        this.PreviewMouseDown += (s, e) => _viewModel.ResetInactivity();

        _viewModel.StartInactivityTimer();
        this.Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var userName = _viewModel.CurrentUserName;
        if (string.IsNullOrWhiteSpace(userName)) return;

        var greeting = TranslationHelper.GetString("WelcomeGreeting");
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
                    TranslationHelper.GetString("MsgErrSavePending"),
                    TranslationHelper.GetString("TitlePendingChanges")
                );
                e.Cancel = true;
                return;
            }

            bool confirmed = DialogHelper.ShowConfirmation(
                TranslationHelper.GetString("MsgConfirmExitPrompt"),
                TranslationHelper.GetString("MsgConfirmExitTitle")
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
