using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Sources.ViewModels;
using Sources.Helpers;
using Sources.Views;
using Sources.Services;

namespace Sources;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _isLockingScreen = false;

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
        if (_isLockingScreen)
        {
            base.OnClosing(e);
            return;
        }

        var viewModel = DataContext as MainViewModel;
        
        // إذا كان المستخدم مسجلاً دخوله، نقوم بتحويل "الإغلاق" إلى "تسجيل خروج"
        if (viewModel != null && viewModel.IsLoggedIn)
        {
            e.Cancel = true; // نمنع إغلاق النافذة فوراً
            viewModel.LogoutCommand.Execute(null); // نفتح رسالة التأكيد الموحدة
        }
        else
        {
            // إذا كان في واجهة الدخول، يتم إغلاق البرنامج بشكل طبيعي
            base.OnClosing(e);
        }
    }
}
