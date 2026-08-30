using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sources.Data;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;

namespace Sources.Views
{
    public partial class LoginWindow : Window
    {
        private readonly IUserService _userService;
        private int _failedAttempts = 0;
        private DispatcherTimer? _lockoutTimer;
        private int _lockoutSecondsRemaining = 0;
        private DispatcherTimer? _welcomeTimer;

        public LoginWindow(IUserService userService)
        {
            InitializeComponent();
            _userService = userService;

            // تطبيق اتجاه الواجهة وموضع أزرار التحكم وفق اللغة المحفوظة
            bool isEn = SettingsHelper.Language == "en";
            this.FlowDirection = isEn ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            WindowControlsPanel.HorizontalAlignment = isEn ? HorizontalAlignment.Right : HorizontalAlignment.Left;

            Loaded += LoginWindow_Loaded;
        }

        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var activeUsers = _userService.GetAllUsers()
                    .Where(u => u.IsActive && !u.IsDeleted)
                    .Select(u => u.Username)
                    .ToList();
                TxtUsername.ItemsSource = activeUsers;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading active users for login dropdown: {ex.Message}");
            }

            ChkRememberMe.IsChecked = SettingsHelper.RememberMe;
            if (SettingsHelper.RememberMe && !string.IsNullOrWhiteSpace(SettingsHelper.SavedUsername))
            {
                TxtUsername.Text = SettingsHelper.SavedUsername;
                TxtPassword.Focus();
            }
            else
            {
                TxtUsername.Focus();
            }

            StartWelcomeTimer();
        }

        private void StartWelcomeTimer()
        {
            _welcomeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _welcomeTimer.Tick += WelcomeTimer_Tick;
            _welcomeTimer.Start();
        }

        private void WelcomeTimer_Tick(object? sender, EventArgs e)
        {
            _welcomeTimer?.Stop();

            // إخفاء أنيق مع Fade-out لبانر الترحيب
            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(400)
            };
            fadeOut.Completed += (s, args) =>
            {
                WelcomeBanner.Visibility = Visibility.Collapsed;
            };
            WelcomeBanner.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            bool confirmed = Helpers.DialogHelper.ShowConfirmation(
                TranslationHelper.GetString("MsgConfirmExitPrompt"),
                TranslationHelper.GetString("MsgConfirmExitTitle")
            );

            if (confirmed)
            {
                _welcomeTimer?.Stop();
                _lockoutTimer?.Stop();
                Environment.Exit(0);
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                IconMaximize.Kind = MaterialDesignThemes.Wpf.PackIconKind.WindowMaximize;
                BtnMaximize.ToolTip = TranslationHelper.GetString("TooltipMaximize");
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                IconMaximize.Kind = MaterialDesignThemes.Wpf.PackIconKind.WindowRestore;
                BtnMaximize.ToolTip = TranslationHelper.GetString("TooltipRestore");
            }
        }

        private void BtnRevealPassword_Click(object sender, RoutedEventArgs e)
        {
            if (TxtPassword.Visibility == Visibility.Visible)
            {
                TxtPasswordReveal.Text = TxtPassword.Password;
                TxtPasswordReveal.Visibility = Visibility.Visible;
                TxtPassword.Visibility = Visibility.Collapsed;
                IconRevealPassword.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOff;
                BtnRevealPassword.ToolTip = TranslationHelper.GetString("TooltipHidePassword");
                TxtPasswordReveal.Focus();
                if (TxtPasswordReveal.Text.Length > 0)
                    TxtPasswordReveal.CaretIndex = TxtPasswordReveal.Text.Length;
            }
            else
            {
                TxtPassword.Password = TxtPasswordReveal.Text;
                TxtPassword.Visibility = Visibility.Visible;
                TxtPasswordReveal.Visibility = Visibility.Collapsed;
                IconRevealPassword.Kind = MaterialDesignThemes.Wpf.PackIconKind.Eye;
                BtnRevealPassword.ToolTip = TranslationHelper.GetString("TooltipShowPassword");
                TxtPassword.Focus();
            }
        }

        private void TxtUsername_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (string.IsNullOrWhiteSpace(GetPassword()))
                {
                    if (TxtPassword.Visibility == Visibility.Visible)
                        TxtPassword.Focus();
                    else
                        TxtPasswordReveal.Focus();
                }
                else
                {
                    BtnLogin_Click(sender, e);
                }
            }
        }

        private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnLogin_Click(sender, e);
            }
        }

        private string GetPassword()
        {
            return TxtPassword.Visibility == Visibility.Visible ? TxtPassword.Password : TxtPasswordReveal.Text;
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = TxtUsername.Text.Trim();
            string password = GetPassword();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ShowError(TranslationHelper.GetString("MsgErrUsernamePasswordReq"));
                return;
            }

            BtnLogin.IsEnabled = false;
            TxtError.Visibility = Visibility.Collapsed;

            try
            {
                var (success, message) = _userService.Login(username, password);
                if (success)
                {
                    if (ChkRememberMe.IsChecked == true)
                    {
                        SettingsHelper.RememberMe = true;
                        SettingsHelper.SavedUsername = username;
                    }
                    else
                    {
                        SettingsHelper.RememberMe = false;
                        SettingsHelper.SavedUsername = string.Empty;
                    }

                    _welcomeTimer?.Stop();
                    _lockoutTimer?.Stop();
                    var mainWindow = App.ServiceProvider.GetRequiredService<MainWindow>();
                    if (mainWindow.DataContext is ViewModels.MainViewModel mainVm)
                    {
                        mainVm.InitializeUserSession();
                    }
                    Application.Current.MainWindow = mainWindow;
                    Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
                    mainWindow.Show();
                    this.Close();
                }
                else
                {
                    _failedAttempts++;
                    if (_failedAttempts >= 3)
                    {
                        StartLockoutTimer();
                    }
                    else
                    {
                        var attemptStr = TranslationHelper.GetFormat("MsgAttemptCount", _failedAttempts);
                        ShowError($"{message}. {attemptStr}");
                        BtnLogin.IsEnabled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError(TranslationHelper.GetString("MsgLoginErrorPrefix") + ex.Message);
                BtnLogin.IsEnabled = true;
            }
        }

        private void StartLockoutTimer()
        {
            _lockoutSecondsRemaining = 30;
            BtnLogin.IsEnabled = false;
            TxtUsername.IsEnabled = false;
            TxtPassword.IsEnabled = false;
            TxtPasswordReveal.IsEnabled = false;
            BtnRevealPassword.IsEnabled = false;

            ShowError(TranslationHelper.GetFormat("MsgLockoutActive", _lockoutSecondsRemaining));

            _lockoutTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _lockoutTimer.Tick += LockoutTimer_Tick;
            _lockoutTimer.Start();
        }

        private void LockoutTimer_Tick(object? sender, EventArgs e)
        {
            _lockoutSecondsRemaining--;
            if (_lockoutSecondsRemaining <= 0)
            {
                _lockoutTimer?.Stop();
                _failedAttempts = 0;
                BtnLogin.IsEnabled = true;
                TxtUsername.IsEnabled = true;
                TxtPassword.IsEnabled = true;
                TxtPasswordReveal.IsEnabled = true;
                BtnRevealPassword.IsEnabled = true;
                TxtError.Visibility = Visibility.Collapsed;

                if (TxtPassword.Visibility == Visibility.Visible)
                    TxtPassword.Focus();
                else
                    TxtPasswordReveal.Focus();
            }
            else
            {
                ShowError(TranslationHelper.GetFormat("MsgLockoutActive", _lockoutSecondsRemaining));
            }
        }

        private void ShowError(string message)
        {
            TxtError.Text = message;
            TxtError.Visibility = Visibility.Visible;
        }
    }
}
