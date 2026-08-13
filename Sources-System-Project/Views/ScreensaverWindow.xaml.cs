using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Sources.Views
{
    public partial class ScreensaverWindow : Window
    {
        public event EventHandler? Dismissed;

        private readonly DispatcherTimer _delayTimer;
        private bool _canDismiss = false;
        private bool _isDismissed = false;
        private Point _initialMousePos;
        private bool _hasInitialPos = false;

        public ScreensaverWindow()
        {
            InitializeComponent();

            // 200ms delay timer before allowing MouseMove to dismiss
            _delayTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _delayTimer.Tick += DelayTimer_Tick;

            Loaded += ScreensaverWindow_Loaded;
            PreviewMouseMove += ScreensaverWindow_PreviewMouseMove;
            PreviewKeyDown += ScreensaverWindow_PreviewKeyDown;
            PreviewMouseDown += ScreensaverWindow_PreviewMouseDown;
        }

        private void ScreensaverWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.Activate();
            RootGrid.Focus();
            _delayTimer.Start();
        }

        private void DelayTimer_Tick(object? sender, EventArgs e)
        {
            _delayTimer.Stop();
            _initialMousePos = Mouse.GetPosition(this);
            _hasInitialPos = true;
            _canDismiss = true;
        }

        private void ScreensaverWindow_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_canDismiss && _hasInitialPos)
            {
                var currentPos = e.GetPosition(this);
                // Require a small movement of at least 4 pixels to trigger dismissal
                if (Math.Abs(currentPos.X - _initialMousePos.X) > 4 || Math.Abs(currentPos.Y - _initialMousePos.Y) > 4)
                {
                    Dismiss();
                }
            }
        }

        private void ScreensaverWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            Dismiss();
        }

        private void ScreensaverWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Dismiss();
        }

        private void Dismiss()
        {
            if (_isDismissed) return;
            _isDismissed = true;

            _delayTimer.Stop();
            Dismissed?.Invoke(this, EventArgs.Empty);
        }
    }
}
