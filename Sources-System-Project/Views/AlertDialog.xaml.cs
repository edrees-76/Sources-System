using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace Sources.Views
{
    public partial class AlertDialog : Window
    {
        public enum AlertResult
        {
            OK,
            Yes,
            No,
            Cancel,
            Extra
        }

        public AlertResult Result { get; private set; } = AlertResult.OK;

        public AlertDialog(string message, string? title = null, string type = "Info", bool showCancel = false, bool isQuestion = false, string? imagePath = null, string? extraButtonText = null)
        {
            InitializeComponent();
            MessageText.Text = message;
            
            if (string.IsNullOrEmpty(title))
            {
                // Title will be set by DynamicResource in XAML automatically
                // but we can set it explicitly here if needed for specific cases
                TitleText.Text = (string)Application.Current.FindResource("AlertTitle");
            }
            else
            {
                TitleText.Text = title;
            }

            // معالجة الزر الإضافي الاختياري
            if (!string.IsNullOrWhiteSpace(extraButtonText))
            {
                ExtraButton.Content = extraButtonText;
                ExtraButton.Visibility = Visibility.Visible;
            }

            // معالجة الصورة إذا وجدت
            if (!string.IsNullOrEmpty(imagePath))
            {
                try
                {
                    string fullPath = Path.IsPathRooted(imagePath) 
                        ? imagePath 
                        : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, imagePath);

                    if (File.Exists(fullPath))
                    {
                        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(fullPath);
                        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        
                        SourceImage.Source = bitmap;
                        ImageContainer.Visibility = Visibility.Visible;
                    }
                }
                catch { /* تجاهل أخطاء تحميل الصورة */ }
            }

            // ضبط الأيقونة والألوان حسب النوع
            switch (type)
            {
                case "Error":
                    AlertIcon.Kind = PackIconKind.Error;
                    AlertIcon.Foreground = (Brush)FindResource("DangerBrush");
                    break;
                case "Warning":
                    AlertIcon.Kind = PackIconKind.Alert;
                    break;
                case "Question":
                    AlertIcon.Kind = PackIconKind.QuestionMark;
                    break;
                default:
                    AlertIcon.Kind = PackIconKind.Information;
                    break;
            }

            if (isQuestion)
            {
                OkButton.Visibility = Visibility.Collapsed;
                YesButton.Visibility = Visibility.Visible;
                NoButton.Visibility = Visibility.Visible;
            }
            else if (showCancel)
            {
                CancelButton.Visibility = Visibility.Visible;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Result = AlertResult.Cancel;
            Close();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Result = AlertResult.OK;
            Close();
        }

        private void ExtraButton_Click(object sender, RoutedEventArgs e)
        {
            Result = AlertResult.Extra;
            Close();
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            Result = AlertResult.Yes;
            Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            Result = AlertResult.No;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = AlertResult.Cancel;
            Close();
        }
    }
}
