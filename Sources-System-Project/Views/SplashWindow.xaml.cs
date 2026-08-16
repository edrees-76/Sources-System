using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Sources.Helpers;

namespace Sources.Views
{
    public partial class SplashWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private int _progress = 0;

        public SplashWindow()
        {
            InitializeComponent();

            // تطبيق اتجاه الواجهة وفق اللغة المحفوظة
            this.FlowDirection = SettingsHelper.Language == "en" ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(20)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _progress += 2;
            ProgressBar.Value = _progress;

            if (_progress == 30)
            {
                TxtStatus.Text = TranslationHelper.GetString("SplashStatusConnecting");
            }
            else if (_progress == 60)
            {
                TxtStatus.Text = TranslationHelper.GetString("SplashStatusLoadingAssets");
            }
            else if (_progress == 90)
            {
                TxtStatus.Text = TranslationHelper.GetString("SplashStatusReady");
            }
            else if (_progress >= 100)
            {
                _timer.Stop();
                OpenLoginWindow();
            }
        }

        private void OpenLoginWindow()
        {
            var loginWindow = App.ServiceProvider.GetRequiredService<LoginWindow>();
            Application.Current.MainWindow = loginWindow;
            loginWindow.Show();
            this.Close();
        }
    }
}
