using System.Windows;
using System.Windows.Input;
using Sources.Helpers;
using Sources.Services;

namespace Sources.Views
{
    public partial class ActivationDialog : Window
    {
        private readonly ILicenseService _licenseService;

        public bool Result { get; private set; } = false;

        /// <summary>رسالة النجاح المُعادة فعلياً من ILicenseService.Activate عند التفعيل الناجح.</summary>
        public string? SuccessMessage { get; private set; }

        public ActivationDialog(ILicenseService licenseService)
        {
            InitializeComponent();
            _licenseService = licenseService;

            Loaded += (s, e) =>
            {
                TxtSerial.Focus();
            };
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            var serial = TxtSerial.Text;
            var (success, message) = _licenseService.Activate(serial);

            if (!success)
            {
                ErrorText.Text = message;
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            Result = true;
            SuccessMessage = message;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            DialogResult = false;
            Close();
        }

        private void TxtSerial_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ConfirmButton_Click(sender, e);
            }
        }
    }
}
